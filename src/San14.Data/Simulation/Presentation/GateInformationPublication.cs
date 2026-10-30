using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ordinary907570 Gate default page, captured once from the
    /// committed world. Each body field retains its own unresolved input.</summary>
    public GateInformationPage QueryGateInformation(GameState state, int gateId)
    {
        var fields = new Dictionary<int, string>();
        var styles = new Dictionary<int, int>();
        var colors = new Dictionary<int, MapCornerColors>();
        var palettes = MapSpritePalettes.Create();
        var hidden = new HashSet<int>();
        var gauges = new List<GateInformationGauge>();
        var unresolved = new Dictionary<int, string>();
        var page = new GateInformationPage(gateId, "", fields);
        try
        {
            if (gateId is < 52 or > 61 || !state.Gates.TryGetValue(gateId, out var gate)) throw Missing("Gate_identity");
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            BaseReturnCaptivesInputs.BindCurrent(state, current);
            var receiver = current.ReadGateTableReference(gateId - 51) ?? throw Missing("Gate_pointer");
            if (!K(current.ReadBaseNativeValidity(receiver), "Gate_validity")) throw Missing("Gate_invalid");
            page = page with { Name = gate.Name };
            foreach (var caption in GateInformationSources.Captions)
            {
                Text(caption.Node, () => ReadHistoryDataText(1, caption.Message));
                styles[caption.Node] = caption.Node is 61 or 68 ? 0 : 13;
            }
            fields[10] = gate.Name;
            fields[88] = "/";
            int? forceId = Value(() => current.ReadBaseForceSlot(receiver), "Force_identity");
            int? forceColor = null;
            DetailForceAppearance? flag = null;
            if (forceId is int owner)
            {
                var force = current.ReadForceTableReference(owner < 52 ? owner : 0) ?? throw Missing("Force_pointer");
                Text(13, () =>
                {
                    if (!K(current.ReadForceNativeValidity(force), "Force_validity")) return "";
                    _mapInformationNames ??= ArmyFlagPaneNames.Load(_scenario);
                    return _mapInformationNames.Resolve(current, force).NativeShortName;
                });
                forceColor = Value(() => current.ReadForceReportColor(force), "Force_color49");
                Read(7, () =>
                {
                    _mapInformationNames ??= ArmyFlagPaneNames.Load(_scenario);
                    bool display = K(current.ReadForceNativeValidity(force), "Flag_Force_validity") || owner is >= 46 and <= 51;
                    flag = ReadDetailForceAppearance(state, current, _mapInformationNames, force, display, palettes)
                        ?? throw Missing("Force_flag");
                });
            }
            int? districtNumber = null;
            Read(18, () =>
            {
                int raw = K(current.ReadBaseDistrictSlot(receiver), "District_ordinal");
                var district = current.ReadDistrictTableReference(raw < 52 ? raw : 0) ?? throw Missing("District_pointer");
                if (!K(current.ReadDistrictNativeValidity(district), "District_validity"))
                { fields[18] = ""; hidden.Add(17); return; }
                byte number = K(current.ReadDistrictNumber(raw), "District11");
                districtNumber = number;
                fields[18] = FormatSceneryPanelNumber(ReadHistoryDataText(1, 0x494), number);
                //17 is the district's source numeric sprite; its identity is
                //published separately rather than replacing it with text.
            });
            Read(22, () =>
            {
                var city = CurrentGateMapQueries.FindCity(receiver, current);
                if (!K(current.ReadBaseNativeValidity(city), "geography_City_validity"))
                {
                    var area = PersonFreedomHomeSource.FindBaseArea229FD0(current, receiver);
                    int original = K(current.ReadAreaNativeValidity(area), "geography_Area_validity")
                        ? K(current.ReadAreaValidityCode(area), "geography_Area34") : 0;
                    city = current.ReadCityTableReference(original < 52 ? original : 0) ?? throw Missing("geography_fallback_City");
                }
                if (!K(current.ReadBaseNativeValidity(city), "geography_final_City_validity"))
                { fields[22] = ""; fields[24] = ""; fields[27] = ""; return; }
                int id = K(city.PhysicalSlot, "geography_City_identity");
                var geography = ReadCurrentCityGeography(state, id);
                fields[22] = geography.Region ?? throw Missing("geography_region");
                fields[24] = geography.Province ?? throw Missing("geography_province");
                fields[27] = state.Cities.GetValueOrDefault(id)?.Name ?? throw Missing("geography_City_name");
            });
            OfficerPortraitRoute? portrait = null;
            DeploymentNarrationPortraitRoute? standingPortrait = null;
            int? appearanceId = null;
            Read(2, () =>
            {
                ushort governor = K(current.ReadArmyReturnBaseGovernor2E(receiver), "Governor2E");
                var person = governor <= 6000 ? current.ReadPersonLookupReference(governor) : current.ReadTransferPersonTableReference(0);
                int physical = K(person?.PhysicalSlot, "Governor_pointer");
                bool valid = K(current.ReadPersonNativeValidity(physical), "Governor_validity");
                int id = valid ? K(current.ReadPersonIdentity(physical), "Governor_identity") : 0;
                fields[15] = valid ? state.Officers.GetValueOrDefault(id)?.Name ?? throw Missing("Governor_name") : "";
                ushort appearance = valid ? K(current.ReadPersonPortraitAppearance(physical), "Governor_appearanceA6") : (ushort)2198;
                appearanceId = appearance;
                portrait = ReadOfficerPortraitAppearance(state, current, _definitions, id, appearance, ArmyMapUiSources.Create());
                if (portrait.Kind == OfficerPortraitRouteKind.Unresolved)
                    unresolved[2] = portrait.UnresolvedInput ?? "gate-information.Governor_portrait";
                standingPortrait = valid ? ReadNarrationPortrait(state, current, _definitions, id, appearance)
                    : new DeploymentNarrationPortraitRoute(2198, 2198, "texture_chara_anime_2198",
                        ["face_2198_b", "face_2198_a", "face_2198_c"]) { Kind = StandingPortraitKind.ThreePart };
            });
            Number(39, () => K(current.ReadBaseMorale44(receiver), "Morale44"));
            Number(77, () => K(current.ReadBaseResource(receiver, BaseResourceKind.Troops), "Troops3C"));
            Number(82, () => K(current.ReadArmyReturnBaseWounded40(receiver), "Wounded40"));
            Number(89, () => BasePostLoad.ReadTroopCapacity(receiver, current));
            Number(45, () => EnumerateBaseResidents(current, (ushort)gateId, Missing).Count());
            Number(55, () => People(6, (ushort)gateId).Count());
            Number(50, () =>
            {
                //349B00 intentionally counts each mode2 City resident once,
                //and once more if231D90 resolves this actual Gate. Both INCs
                //are present in the source instruction body.
                var city = CurrentGateMapQueries.FindCity(receiver, current);
                ushort home = K(current.ReadBaseObjectLink(city), "Free_City4E");
                int count = 0;
                foreach (int person in People(5, home))
                {
                    var location = PersonPositionRepair.DecodeSecondLocation(K(current.ReadPersonSecondLocation(person), "Free_E4"));
                    if (location.Kind == 6 && location.Index == gateId - 51) count++;
                    count++;
                }
                return count;
            });
            Read(34, () =>
            {
                int maximum = BasePostLoad.ReadCapacities(receiver, current).Field46;
                int durability = BasePostLoad.ReadLinkedField14(receiver, current);
                fields[34] = durability.ToString(CultureInfo.InvariantCulture) + "/" + maximum.ToString(CultureInfo.InvariantCulture);
                gauges.Add(new(35, durability, maximum, durability > (float)palettes.DurabilityStylePercent * maximum / 100f ? 1 : 2));
            });
            Read(40, () =>
            {
                int morale = K(current.ReadBaseMorale44(receiver), "Morale44");
                int owner = forceId ?? throw Missing("Morale_Force");
                var force = current.ReadForceTableReference(owner < 52 ? owner : 0) ?? throw Missing("Morale_Force_pointer");
                int maximum = K(current.ReadForceNativeValidity(force), "Morale_Force_validity")
                    ? K(Context(current).ForceMoraleMaximum(state, owner), "Morale_maximum2690D0")
                    : CounterParameter(state, SimulationParameterKeys.Morale.Maximum);
                gauges.Add(new(40, morale, maximum, morale > palettes.MoraleStyleThreshold ? 1 : 2));
            });
            Read(60, () =>
            {
                var status = current.ReadCaptureBaseThreatFields(receiver) ?? throw Missing("status_owner");
                Status(60, 62, K(status.Byte4B, "Provocation4B"));
            });
            Read(67, () =>
            {
                var status = current.ReadCaptureBaseThreatFields(receiver) ?? throw Missing("status_owner");
                Status(67, 69, K(status.Byte49, "Confusion49"));
            });
            bool? nearby = gate.HasNearbyEnemyArmy;
            if (nearby == false) hidden.Add(25);
            else if (nearby is null) unresolved[25] = "gate-information.Gate67_nearby_enemy";
            DetailMiniMapValues? miniMap = null;
            Read(87, () =>
            {
                int hex = current.ReadBaseCurrentHexIndex(receiver);
                var bases = ReadDetailMiniMapBases(current) ?? throw Missing("MiniMap_bases");
                byte color = forceId is int owner ? K(ReadMiniMapColor(current, owner), "MiniMap_Force_color") : throw Missing("MiniMap_Force");
                miniMap = new(bases, DetailMiniMapProjection.Project(DetailMiniMapIconKind.Gate, gateId, hex, color));
            });
            CityInformationTactic? tactic = null;
            Read(86, () =>
            {
                //907570 declares param_2 as longlong*. Its param_2+9 is
                //byte offset48, the actual saved Base tactic, not a new header.
                byte raw = K((current.ReadCaptureBaseThreatFields(receiver) ?? throw Missing("Tactic_owner")).Byte48, "Tactic48");
                var reference = current.ReadSkillTacticReference(raw <= 200 ? raw : 0) ?? throw Missing("Tactic_pointer");
                if (!K(current.ReadSkillTacticValidity(reference), "Tactic_validity"))
                { tactic = new(86, null, ReadHistoryDataText(1, 0x53F), 0x21E); return; }
                int id = K(current.ReadSkillTacticPhysicalSlot(reference), "Tactic_identity");
                tactic = new(86, id, state.Tactics.GetValueOrDefault(id)?.Name ?? throw Missing("Tactic_name"), 0x222);
            });
            return page with { PortraitAppearanceId = appearanceId, Portrait = portrait, StandingPortrait = standingPortrait, ForceId = forceId,
                ForceColorIndex = forceColor, ForceNameColor = forceColor is int color ? MapSpritePalettes.Create().ForceLetter(color) : null,
                ForceFlag = flag,
                DistrictNumber = districtNumber, NearbyEnemyPanelVisible = nearby, MiniMap = miniMap,
                HiddenNodes = hidden, TextStyles = styles, TextColors = colors, Gauges = gauges, Tactic = tactic,
                UnresolvedFields = unresolved, UnresolvedOwner = Summary() };

            IEnumerable<int> People(byte status, ushort home)
            {
                if (home is < 1 or > 61) yield break;
                foreach (int? pointer in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100_registry"))
                {
                    int physical = K(pointer, "Person100_pointer");
                    if (K(current.ReadPersonFirstLocation(physical), "Person_E2") == home &&
                        K(current.ReadPersonStatus(physical), "Person_E6") == status) yield return physical;
                }
            }
            void Status(int node, int colorNode, byte ticks)
            {
                //34F190/345FD0 use the display rule's four subdays per day.
                int days = (int)MathF.Ceiling(ticks / 4f);
                fields[node] = days > 0 ? days.ToString(CultureInfo.InvariantCulture) : "";
                //930D10 is a corner-color palette, not a text preset.
                colors[colorNode] = palettes[days > 0 ? 12 : 45];
            }
        }
        catch (InvalidDataException error)
        { unresolved[-1] = error.Message; return page with { HiddenNodes = hidden, TextStyles = styles, TextColors = colors, Gauges = gauges,
            UnresolvedFields = unresolved, UnresolvedOwner = Summary() }; }

        string? Summary()
        {
            //These source controls share a single lookup prefix. Preserve the
            //same missing input at every unfilled value, not only its first node.
            if (unresolved.TryGetValue(22, out string? geography))
                foreach (int node in new[] { 24, 27 })
                    if (!fields.ContainsKey(node)) unresolved.TryAdd(node, geography);
            if (unresolved.TryGetValue(2, out string? governor) && !fields.ContainsKey(15))
                unresolved.TryAdd(15, governor);
            var all = unresolved.Values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            return all.Length == 0 ? null : string.Join("; ", all);
        }
        void Read(int node, Action query)
        { try { query(); } catch (InvalidDataException error) { unresolved[node] = error.Message; } }
        void Text(int node, Func<string> query) => Read(node, () => fields[node] = query());
        void Number(int node, Func<int> query) => Text(node, () => query().ToString(CultureInfo.InvariantCulture));
        int? Value(Func<int?> query, string field)
        { int? value = null; Read(13, () => value = K(query(), field)); return value; }
        static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        static InvalidDataException Missing(string field) => new("gate-information." + field);
    }

    /// <summary>8F7570 footer0 scans the physical Person table in order,
    /// status1..6 and22A6C0 first-location Gate. It is queried on the actual
    /// footer action, independently of the page's current-position counts.</summary>
    public AreaAdministrationQuery<IReadOnlyList<int>> QueryGateResidentOfficerIds(GameState state, int gateId)
    {
        try
        {
            if (gateId is < 52 or > 61 || !state.Gates.ContainsKey(gateId))
                throw new InvalidDataException("gate-information.residents.Gate_identity");
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var result = new List<int>();
            for (int slot = 0; slot < _scenario.Snapshot.Characters.Count; slot++)
            {
                int physical = current.ReadTransferPersonTableReference(slot)?.PhysicalSlot
                    ?? throw new InvalidDataException("gate-information.residents.Person_table");
                byte status = current.ReadPersonStatus(physical)
                    ?? throw new InvalidDataException("gate-information.residents.Person_E6");
                if (status is < 1 or > 6) continue;
                ushort first = current.ReadPersonFirstLocation(physical)
                    ?? throw new InvalidDataException("gate-information.residents.Person_E2");
                if (first != gateId) continue;
                result.Add(current.ReadPersonIdentity(physical)
                    ?? throw new InvalidDataException("gate-information.residents.Person_identity"));
            }
            return AreaAdministrationQuery<IReadOnlyList<int>>.Known(result.AsReadOnly());
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<IReadOnlyList<int>>.Unknown(error.Message); }
    }
}
