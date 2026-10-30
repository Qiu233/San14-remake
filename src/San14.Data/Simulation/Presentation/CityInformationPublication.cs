using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ordinary City default page, from one current capture.</summary>
    public CityInformationPage QueryCityInformation(GameState state, int cityId)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            BaseReturnCaptivesInputs.BindCurrent(state, current);
            return ReadCityInformationCurrent(state, current, cityId, false);
        }
        catch (InvalidDataException error)
        {
            return new(cityId, state.Cities.GetValueOrDefault(cityId)?.Name ?? "", new Dictionary<int, string>())
            { UnresolvedOwner = error.Message, UnresolvedFields = new Dictionary<int, string> { [-1] = error.Message } };
        }
    }

    /// <summary>The report page uses its retained settlement current. Its five
    /// income/expense fields are the actual report accumulators, not forecasts.</summary>
    public CityInformationPage QueryCityInformation(CampaignIncomeReportPublication report, int cityId)
        => ReadCityInformationCurrent(report.World, report.Current, cityId, true);

    private CityInformationPage ReadCityInformationCurrent(GameState state, SourceWorldContext current,
        int cityId, bool reportContext)
    {
        var fields = new Dictionary<int, string>();
        var missing = new Dictionary<int, string>();
        var styles = new Dictionary<int, int>();
        var colors = new Dictionary<int, MapCornerColors>();
        var hidden = new HashSet<int>();
        var gauges = new List<CityInformationGauge>();
        var palettes = MapSpritePalettes.Create();
        var page = new CityInformationPage(cityId, state.Cities.GetValueOrDefault(cityId)?.Name ?? "", fields)
        { ReportContext = reportContext };
        try
        {
            if (cityId is < 1 or > 51 || !state.Cities.TryGetValue(cityId, out var city)) throw Missing("City_identity");
            var receiver = current.ReadCityTableReference(cityId) ?? throw Missing("City_pointer");
            if (!Need(current.ReadBaseNativeValidity(receiver), "City_validity")) throw Missing("City_invalid");
            foreach (var caption in CityInformationSources.Captions)
            {
                Text(caption.Node, () => ReadHistoryDataText(1, caption.Message));
                styles[caption.Node] = 13;
            }
            Text(80, () => ReadHistoryDataText(1, 0x469));
            Text(87, () => ReadHistoryDataText(1, 0x469));
            foreach (int node in new[] { 14, 146, 152, 154, 156 }) styles[node] = 0;
            fields[10] = city.Name;
            fields[34] = fields[41] = fields[128] = "/";
            int? forceId = null, forceColor = null, districtNumber = null;
            DetailForceAppearance? flag = null;
            Read(14, () =>
            {
                int raw = Need(current.ReadBaseForceSlot(receiver), "Force_identity");
                forceId = raw;
                var force = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw Missing("Force_pointer");
                _mapInformationNames ??= ArmyFlagPaneNames.Load(_scenario);
                fields[14] = Need(current.ReadForceNativeValidity(force), "Force_validity")
                    ? _mapInformationNames.Resolve(current, force).NativeShortName : "";
                forceColor = Need(current.ReadForceReportColor(force), "Force_color49");
                flag = ReadDetailForceAppearance(state, current, _mapInformationNames, force,
                    Need(current.ReadForceNativeValidity(force), "Flag_Force_validity") || raw is >= 46 and <= 51, palettes);
            });
            Read(19, () =>
            {
                int raw = Need(current.ReadBaseDistrictSlot(receiver), "District_ordinal");
                var district = current.ReadDistrictTableReference(raw < 52 ? raw : 0) ?? throw Missing("District_pointer");
                if (!Need(current.ReadDistrictNativeValidity(district), "District_validity"))
                { fields[19] = ""; hidden.Add(18); return; }
                byte number = Need(current.ReadDistrictNumber(raw), "District11");
                districtNumber = number;
                fields[19] = FormatSceneryPanelNumber(ReadHistoryDataText(1, 0x494), number);
            });
            var geography = ReadCurrentCityGeography(state, cityId);
            Text(23, () => geography.Region ?? throw Missing("geography_region"));
            Text(25, () => geography.Province ?? throw Missing("geography_province"));
            OfficerPortraitRoute? portrait = null;
            DeploymentNarrationPortraitRoute? standing = null;
            int? appearanceId = null;
            Read(2, () =>
            {
                int physical = Person(Need(current.ReadArmyReturnBaseGovernor2E(receiver), "Governor2E"));
                bool valid = Need(current.ReadPersonNativeValidity(physical), "Governor_validity");
                int id = valid ? Need(current.ReadPersonIdentity(physical), "Governor_identity") : 0;
                fields[16] = valid ? state.Officers.GetValueOrDefault(id)?.Name ?? throw Missing("Governor_name") : "";
                ushort appearance = valid ? Need(current.ReadPersonPortraitAppearance(physical), "Governor_appearanceA6") : (ushort)2198;
                appearanceId = appearance;
                portrait = ReadOfficerPortraitAppearance(state, current, _definitions, id, appearance, ArmyMapUiSources.Create());
                if (portrait.Kind == OfficerPortraitRouteKind.Unresolved) missing[2] = portrait.UnresolvedInput ?? "city-information.Governor_portrait";
                standing = valid ? ReadNarrationPortrait(state, current, _definitions, id, appearance)
                    : new DeploymentNarrationPortraitRoute(2198, 2198, "texture_chara_anime_2198",
                        ["face_2198_b", "face_2198_a", "face_2198_c"]) { Kind = StandingPortraitKind.ThreePart };
            });
            Number(95, () => Need(current.ReadBaseResource(receiver, BaseResourceKind.Gold), "Gold34"));
            Number(111, () => Need(current.ReadBaseResource(receiver, BaseResourceKind.Supplies), "Food38"));
            Number(127, () => Need(current.ReadBaseResource(receiver, BaseResourceKind.Troops), "Troops3C"));
            Number(129, () => BasePostLoad.ReadTroopCapacity(receiver, current));
            Number(139, () => Need(current.ReadArmyReturnBaseWounded40(receiver), "Wounded40"));
            //CFootholdPersonIterator modes0/1/2 use the registered Person chain,
            //first location E2 and statuses1..4 /6 /5 respectively.
            Read(48, () =>
            {
                ushort home = Need(current.ReadBaseObjectLink(receiver), "City4E");
                int serving = 0, free = 0, captive = 0;
                foreach (int? pointer in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100_registry"))
                {
                    int physical = Need(pointer, "Person100_pointer");
                    if (Need(current.ReadPersonFirstLocation(physical), "Person_E2") != home) continue;
                    byte status = Need(current.ReadPersonStatus(physical), "Person_E6");
                    if (status is >= 1 and <= 4) serving++;
                    else if (status == 5) free++;
                    else if (status == 6) captive++;
                }
                fields[48] = Integer(serving);
                fields[53] = forceId is >= 46 and <= 50 ? ReadHistoryDataText(1, 0x353) : Integer(free);
                fields[58] = forceId is >= 46 and <= 50 ? ReadHistoryDataText(1, 0x353) : Integer(captive);
            });
            Number(63, () =>
            {
                int count = 0;
                foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw Missing("AreaB0_registry"))
                {
                    var area = entry ?? throw Missing("AreaB0_pointer");
                    if (Need(current.ReadAreaCitySlot(area), "Area35") == cityId) count++;
                }
                return count;
            });
            Text(68, () =>
            {
                if (forceId is null) throw Missing("Size_Force");
                if (forceId is >= 46 and <= 50 && cityId == forceId + 1) return ReadHistoryDataText(1, 0x6DF);
                int size = Need(current.ReadCitySizeB8(receiver), "SizeB8");
                int[] messages = [0x46C, 0x46C, 0x46D, 0x46E, 0x46F];
                return ReadHistoryDataText(1, size < messages.Length ? messages[size] : throw Missing("SizeB8_range"));
            });
            Text(73, () =>
            {
                int emperor = Person(Need(current.ReadEmperorId(), "Emperor1654"));
                ushort home = Need(current.ReadPersonFirstLocation(emperor), "Emperor_E2");
                return home is >= 1 and <= 51 && home == cityId ? ReadHistoryDataText(1, 0x338) : "";
            });
            Read(36, () =>
            {
                int durability = BasePostLoad.ReadLinkedField14(receiver, current);
                int maximum = BasePostLoad.ReadCapacities(receiver, current).Field46;
                fields[33] = Integer(durability); fields[35] = Integer(maximum);
                gauges.Add(new(36, durability, maximum, durability > (float)palettes.DurabilityStylePercent * maximum / 100f ? 1 : 2));
            });
            Read(43, () =>
            {
                int morale = Need(current.ReadBaseMorale44(receiver), "Morale44");
                int owner = forceId ?? throw Missing("Morale_Force");
                var force = current.ReadForceTableReference(owner < 52 ? owner : 0) ?? throw Missing("Morale_Force_pointer");
                int maximum = Need(current.ReadForceNativeValidity(force), "Morale_Force_validity")
                    ? Need(Context(current).ForceMoraleMaximum(state, owner), "Morale_maximum2690D0")
                    : CounterParameter(state, SimulationParameterKeys.Morale.Maximum);
                fields[40] = Integer(morale); fields[42] = Integer(maximum);
                gauges.Add(new(43, morale, maximum, morale > palettes.MoraleStyleThreshold ? 1 : 2));
            });
            Read(79, () =>
            {
                var status = current.ReadCaptureBaseThreatFields(receiver) ?? throw Missing("status_owner");
                Status(79, 78, Need(status.Byte4B, "Provocation4B"));
                Status(86, 85, Need(status.Byte49, "Confusion49"));
            });
            CityInformationDutyPortrait? recruiter = null, trainer = null;
            Read(161, () => recruiter = DutyPortrait(161, Need(current.ReadAssignmentCityCA(cityId), "RecruiterCA")));
            Read(169, () => trainer = DutyPortrait(169, Need(current.ReadAssignmentCityCC(cityId), "TrainerCC")));
            CityInformationTactic? tactic = null;
            Read(178, () =>
            {
                byte slot = Need((current.ReadCaptureBaseThreatFields(receiver) ?? throw Missing("tactic_owner")).Byte48, "Tactic48");
                var reference = current.ReadSkillTacticReference(slot <= 200 ? slot : 0) ?? throw Missing("Tactic_pointer");
                if (!Need(current.ReadSkillTacticValidity(reference), "Tactic_validity"))
                { tactic = new(178, null, ReadHistoryDataText(1, 0x53F), 0x21E); return; }
                int id = Need(current.ReadSkillTacticPhysicalSlot(reference), "Tactic_identity");
                tactic = new(178, id, state.Tactics.GetValueOrDefault(id)?.Name ?? throw Missing("Tactic_name"), 0x222);
            });
            Read(146, () =>
            {
                if (forceId is null) throw Missing("Landmark_Force");
                if (forceId is >= 46 and <= 50)
                { foreach (int node in new[] { 146, 152, 154, 156 }) fields[node] = ReadHistoryDataText(1, 0x353); return; }
                var summary = ReadCityInformationLandmarks(state, current, cityId, forceId.Value);
                fields[146] = Integer(summary.Count);
                string format = ReadHistoryDataText(1, 0x862);
                fields[152] = FormatSceneryPanelNumber(format, summary.Gold);
                fields[154] = FormatSceneryPanelNumber(format, summary.Food);
                fields[156] = FormatSceneryPanelNumber(format, summary.Troops);
            });
            var economy = ReadCityDetailEconomyCurrent(state, current, cityId, reportContext);
            foreach (var pair in economy.Text) fields[pair.Key] = pair.Value;
            foreach (var pair in economy.UnresolvedFields) missing[pair.Key] = pair.Value;
            bool? nearby = city.HasNearbyEnemyArmy;
            if (nearby == false) hidden.Add(26);
            if (nearby is null) missing[26] = "city-information.City67_nearby_enemy";
            DetailMiniMapValues? miniMap = null;
            Read(194, () =>
            {
                int hex = current.ReadBaseCurrentHexIndex(receiver);
                var bases = ReadDetailMiniMapBases(current) ?? throw Missing("MiniMap_bases");
                byte color = forceId is int owner ? Need(ReadMiniMapColor(current, owner), "MiniMap_color") : throw Missing("MiniMap_Force");
                miniMap = new(bases, DetailMiniMapProjection.Project(DetailMiniMapIconKind.City, cityId, hex, color));
            });
            return Finish(page with { PortraitAppearanceId = appearanceId, Portrait = portrait, StandingPortrait = standing,
                ForceId = forceId, ForceColorIndex = forceColor, ForceNameColor = forceColor is int c ? palettes.ForceLetter(c) : null,
                ForceFlag = flag, DistrictNumber = districtNumber, NearbyEnemyPanelVisible = nearby, MiniMap = miniMap,
                Recruiter = recruiter, Trainer = trainer, Tactic = tactic });

            int Person(ushort raw) => Need((raw <= 6000 ? current.ReadPersonLookupReference(raw)
                : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_pointer");
            CityInformationDutyPortrait DutyPortrait(int node, ushort raw)
            {
                int physical = Person(raw);
                bool valid = Need(current.ReadPersonNativeValidity(physical), "Duty_Person_validity");
                int id = valid ? Need(current.ReadPersonIdentity(physical), "Duty_Person_identity") : 0;
                ushort appearance = valid ? Need(current.ReadPersonPortraitAppearance(physical), "Duty_appearanceA6") : (ushort)2198;
                var route = ReadOfficerPortraitAppearance(state, current, _definitions, id, appearance, ArmyMapUiSources.Create());
                string? issue = route.Kind == OfficerPortraitRouteKind.Unresolved ? route.UnresolvedInput ?? "city-information.Duty_portrait" : null;
                if (issue is not null) missing[node] = issue;
                return new(node, valid ? id : null, route, issue);
            }
        }
        catch (InvalidDataException error) { missing[-1] = error.Message; return Finish(page); }

        CityInformationPage Finish(CityInformationPage value) => value with { HiddenNodes = hidden, TextStyles = styles,
            SpriteIds = new Dictionary<int, int> { [150] = 0x1C1, [153] = 0x1A0, [155] = 0x19C },
            TextColors = colors, Gauges = gauges, UnresolvedFields = missing,
            UnresolvedOwner = missing.Count == 0 ? null : string.Join("; ", missing.Values.Distinct().Order(StringComparer.Ordinal)) };
        void Read(int node, Action query) { try { query(); } catch (InvalidDataException error) { missing[node] = error.Message; } }
        void Text(int node, Func<string> query) => Read(node, () => fields[node] = query());
        void Number(int node, Func<int> query) => Text(node, () => Integer(query()));
        void Status(int node, int styleNode, byte ticks)
        {
            int days = (int)MathF.Ceiling(ticks / 4f);
            fields[node] = days > 0 ? Integer(days) : "";
            styles[styleNode] = ticks != 0 ? 12 : 45;
            colors[styleNode] = palettes[styles[styleNode]];
        }
        static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);
        static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        static InvalidDataException Missing(string field) => new("city-information." + field);
    }

    //32EAC0: count and sum only active, owned Sceneries of kinds1..3.
    //One bounded pass supplies all four page values.
    private (int Count, int Gold, int Food, int Troops) ReadCityInformationLandmarks(GameState state,
        SourceWorldContext current, int cityId, int forceId, int? areaId = null)
    {
        int count = 0; int[] sums = new int[3];
        foreach (var row in _landmarkDefinitions.Catalog.Sceneries)
        {
            if (row.Slot == 0) continue;
            var hex = current.ReadHexTableReference(row.RawHex44 < 48400 ? row.RawHex44 : 0) ?? throw Missing("Hex_pointer");
            int rawArea = Need(current.ReadHexAreaSlot(hex), "Hex12");
            if (rawArea == 0) continue;
            if (areaId is int selected && rawArea != selected) continue;
            var landmark = state.LandmarkInvestments?.GetValueOrDefault(row.Slot) ?? throw Missing("current_owner");
            if ((Need(landmark.Flags, "Flags47") & 1) == 0) continue;
            var area = current.ReadAreaTableReference(rawArea < 501 ? rawArea : 0) ?? throw Missing("Area_pointer");
            int owner = Need(current.ReadHexForceSlot(hex), "Hex14");
            int mapped = Need(current.ReadAreaCurrentForceSlot(area), "Area_Force") == owner
                ? Need(current.ReadAreaCitySlot(area), "Area35") : 0;
            if (mapped == 0 || owner == 0 || mapped != cityId || owner != forceId) continue;
            var development = landmark.Development ?? throw Missing("Development55_56");
            if (development.EffectKind is < 1 or > 3) continue;
            int effect = development.Effect;
            if (Need(current.ReadAreaNativeValidity(area), "Area_validity"))
            {
                ushort raw = Need(current.ReadAssignmentArea36(Need(area.PhysicalSlot, "Area_identity")), "Area36");
                if (raw != 0)
                {
                    int person = PersonAreaAppointmentSource.Resolve(current, raw);
                    if (Need(current.ReadPersonNativeValidity(person), "Person_validity") &&
                        Need(current.ReadPersonOldForceSlot(person), "Person_Force") == owner)
                    {
                        int trait = Need(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x7B), "Trait123");
                        if (trait != 0 && effect != 0)
                        {
                            if (state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(CityFoodIncomeSource.LandmarkOfficerEffectScaleKey, out int scale) != true)
                                throw Missing(CityFoodIncomeSource.LandmarkOfficerEffectScaleKey);
                            float extra = (float)unchecked(scale * trait) * effect / 100f;
                            int integer = extra > 0 && extra < 1 ? 1 : extra < 0 && extra > -1 ? -1
                                : extra >= -2147483648f && extra < 2147483648f ? (int)extra : int.MinValue;
                            effect = unchecked(effect + integer);
                        }
                    }
                }
            }
            count++; sums[development.EffectKind - 1] = unchecked(sums[development.EffectKind - 1] + effect);
        }
        return (count, sums[0], sums[1], sums[2]);
        static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        static InvalidDataException Missing(string field) => new("city-information.landmarks." + field);
    }

}
