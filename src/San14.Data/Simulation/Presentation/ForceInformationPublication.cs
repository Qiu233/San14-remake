using System.Globalization;
using System.Text;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>906C50's ordinary campaign Force page. One current owner is
    /// shared by its numeric, diplomatic, nameplate and map publications.</summary>
    public ForceInformationPage QueryForceInformation(GameState state, int forceId)
    {
        var fields = new Dictionary<int, string>();
        var missing = new Dictionary<int, string>();
        var styles = new Dictionary<int, int>();
        //8DEC90 hides the net-income groups. Body4 replaces its old nameplate
        //subtree; its independent child8 ruby remains an empty authored field.
        var hidden = new HashSet<int> { 80, 101 };
        var page = new ForceInformationPage(forceId, state.Forces.GetValueOrDefault(forceId)?.Name ?? "", fields);
        try
        {
            if (forceId is < 0 or > 51) throw Missing("Force_identity");
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            BaseReturnCaptivesInputs.BindCurrent(state, current);
            current.EventConditions.ScenarioMode = state.CampaignEventWorld?.ScenarioMode;
            var force = current.ReadForceTableReference(forceId) ?? throw Missing("Force_pointer");
            bool valid = Need(current.ReadForceNativeValidity(force), "Force_validity");
            if (!valid) throw Missing("Force_invalid");
            foreach (var caption in ForceInformationSources.Captions)
            { Text(caption.Node, () => ReadHistoryDataText(1, caption.Message)); styles[caption.Node] = 13; }
            foreach (int node in new[] { 14, 16, 18, 24, 29, 34, 39, 44, 45, 46, 51, 56, 61,
                69, 74, 79, 90, 95, 100, 111, 116, 121, 133, 138, 144, 149, 154, 159, 165, 170, 175, 182, 187 }) styles[node] = 0;

            int? rulerId = null, appearanceId = null;
            DeploymentNarrationPortraitRoute? standing = null;
            DetailForceNameplate? nameplate = null;
            ushort? rawRuler = current.ReadForceRecord(forceId)?.RawRuler10;
            Read(2, () =>
            {
                int physical = Person(Need(rawRuler, "Ruler10"));
                bool personValid = Need(current.ReadPersonNativeValidity(physical), "Ruler_validity");
                int id = personValid ? Need(current.ReadPersonIdentity(physical), "Ruler_identity") : 0;
                ushort appearance = personValid ? Need(current.ReadPersonPortraitAppearance(physical), "Ruler_A6") : (ushort)2198;
                rulerId = personValid ? id : null; appearanceId = appearance;
                standing = personValid ? ReadNarrationPortrait(state, current, _definitions, id, appearance)
                    : new DeploymentNarrationPortraitRoute(2198, 2198, "texture_chara_anime_2198",
                        ["face_2198_b", "face_2198_a", "face_2198_c"]) { Kind = StandingPortraitKind.ThreePart };
            });
            Read(4, () =>
            {
                int physical = Person(Need(rawRuler, "Nameplate_Ruler10"));
                bool personValid = Need(current.ReadPersonNativeValidity(physical), "Nameplate_Ruler_validity");
                nameplate = new(valid, personValid, null, null, null, null);
                if (!personValid) { hidden.Add(4); return; }
                int id = Need(current.ReadPersonIdentity(physical), "Nameplate_Ruler_identity");
                var person = state.Officers.GetValueOrDefault(id) ?? throw Missing("Nameplate_Person_owner");
                string name = (person.CurrentSurname ?? throw Missing("Nameplate_surname")) +
                    (person.CurrentGivenName ?? throw Missing("Nameplate_given_name"));
                nameplate = nameplate with { RulerId = id, RulerName = name };
                var owner = current.ReadPersonForce(physical) ?? throw Missing("Nameplate_Person_Force");
                bool flagValid = Need(current.ReadForceNativeValidity(owner), "Nameplate_Force_validity");
                nameplate = nameplate with { FlagValid = flagValid };
                if (!flagValid) return;
                _mapInformationNames ??= ArmyFlagPaneNames.Load(_scenario);
                var appearance = ReadDetailForceAppearance(state, current, _mapInformationNames, owner, true, MapSpritePalettes.Create())
                    ?? throw Missing("Nameplate_Flag_appearance");
                nameplate = nameplate with { FlagAppearance = appearance };
            });
            //The native refresh computes both forecast nets before binding
            //the visible scalar fields. Keep that actual quote order.
            var economy = ReadForceDetailEconomyCurrent(state, current, forceId);
            Text(14, () =>
            {
                byte raw = Need(current.ReadForceKingdomNameIndex(forceId), "Kingdom48");
                var definition = ReadEmpireNameResourcesCurrent().CustomNames.Definitions[raw <= 100 ? raw : 0];
                return definition.NativeValidity ? definition.ShortName : "";
            });
            Text(16, () =>
            {
                int raw = Need(current.ReadMoraleWaveForceRank47(forceId), "Rank47");
                int rank = raw <= 12 ? raw : 0;
                //CRankData2344D0: row0 is invalid;3622D0 maps row9 to53F.
                if (rank == 0) return "";
                return rank == 9 ? ReadHistoryDataText(1, 0x53F)
                    : (_scenario.Snapshot.RankParameters ?? throw Missing("Rank_definitions")).Ranks[rank].Name;
            });
            Text(18, () =>
            {
                int emperor = Person(Need(current.ReadEmperorId(), "Emperor1654"));
                int home = Need(current.ReadPersonFirstLocation(emperor), "Emperor_E2");
                if (home is < 1 or > 51) return "";
                var city = current.ReadCityTableReference(home) ?? throw Missing("Emperor_City_pointer");
                return Need(current.ReadBaseForceSlot(city), "Emperor_City_Force") == forceId ? ReadHistoryDataText(1, 0x338) : "";
            });
            Number(24, () =>
            {
                if (forceId == 0) return 0;
                int count = 0;
                foreach (var entry in current.ReadRegistryReferences(RegistryKind.District) ?? throw Missing("District_A0_registry"))
                {
                    if (entry is not { Kind: RegistryKind.District, PhysicalSlot: int physical }) throw Missing("District_A0_pointer");
                    if (Need(current.ReadDistrictForceId(physical), "District10") == forceId) count++;
                }
                return count;
            });
            Number(29, () =>
            {
                if (forceId == 0) return 0;
                int count = 0;
                foreach (var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw Missing("City90_registry"))
                {
                    if (entry is not { Kind: RegistryKind.City, PhysicalSlot: int physical }) throw Missing("City90_pointer");
                    var city = current.ReadCityTableReference(physical) ?? throw Missing("City_table");
                    if (Need(current.ReadBaseForceSlot(city), "City_Force") == forceId) count++;
                }
                return count;
            });
            Number(34, () =>
            {
                int count = 0;
                //35F880 scans the loaded physical Person table, independent of
                //the registry membership/status filters used by other lists.
                for (int slot = 0; slot < _scenario.Snapshot.Characters.Count; slot++)
                {
                    int physical = Need(current.ReadTransferPersonTableReference(slot)?.PhysicalSlot, "Person_table");
                    if (!Need(current.ReadPersonNativeValidity(physical), "Person_validity")) continue;
                    int raw = Need(current.ReadPersonDistrictSlot(physical), "Person_E0");
                    var district = current.ReadDistrictTableReference(raw <= 51 ? raw : 0) ?? throw Missing("Person_District_pointer");
                    int districtId = Need(district.PhysicalSlot, "Person_District_identity");
                    if (Need(current.ReadDistrictForceId(districtId), "Person_District10") != forceId) continue;
                    if (forceId == 51) continue;
                    int first = Need(current.ReadPersonFirstLocation(physical), "Person_E2");
                    if (forceId is >= 46 and <= 50 && (first is < 1 or > 61)) continue;
                    count++;
                }
                return count;
            });
            Text(39, () =>
            {
                if (forceId is >= 46 and <= 50) return ReadHistoryDataText(1, 0x353);
                int physical = Person(Need(rawRuler, "Doctrine_Ruler10"));
                int id = Need(current.ReadPersonIdentity(physical), "Doctrine_Ruler_identity");
                var person = state.Officers.GetValueOrDefault(id) ?? throw Missing("Doctrine_Ruler_owner");
                int raw = person.DoctrineId ?? person.AbilityState?.ObservedDoctrineId ?? throw Missing("Doctrine_Person14C");
                int doctrine = raw is >= 0 and <= 6 ? raw : 0;
                string name = (_scenario.Snapshot.DoctrineParameters ?? throw Missing("Doctrine_definitions")).Doctrines[doctrine].Name;
                int level = Need(OwnedForce164Inputs.ReadLevel(state, Need(current.ReadForce164(force), "Doctrine_Force164")), "Doctrine_level");
                return name + " " + FormatSceneryPanelNumber(ReadHistoryDataText(1, 0x384), level);
            });
            Read(44, () =>
            {
                int owned = 0, total = 0;
                //906C50 deliberately walks all concrete map cells. CHexData
                //native validity is constanttrue; the terrain2A gate is real.
                for (int slot = 0; slot < 48400; slot++)
                {
                    var hex = current.ReadHexTableReference(slot) ?? throw Missing("Land_Hex_pointer");
                    if (!hex.PhysicalSlot.HasValue) continue;
                    int code = Need(current.ReadHexSurfaceCode(hex), "Land_surface");
                    var surface = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw Missing("Land_surface_pointer");
                    if (Need(current.ReadSurfaceTypeRawField2A(surface), "Land_surface2A") == 0) continue;
                    total++;
                    if (Need(current.ReadHexForceSlot(hex), "Land_Hex14") == forceId) owned++;
                }
                fields[44] = Integer(owned); fields[45] = Integer(total); fields[46] = ReadHistoryDataText(1, 0x60C);
                fields[51] = FormatForceInformationRate(ReadHistoryDataText(1, 0x561), (float)owned * 100f / total);
            });
            Text(56, () =>
            {
                if (SuppressedContext() || forceId is >= 46 and <= 50) return ReadHistoryDataText(1, 0x353);
                byte flags = state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(forceId, out var image) == true
                    ? Need(image[0x12], "Heaven_Force12") : Need(state.NativeArmySimulation?.Forces.GetValueOrDefault(forceId)?.Flags12, "Heaven_Force12");
                return (flags & 8) != 0 ? ReadHistoryDataText(1, 0x338) : (flags & 0x10) != 0 ? ReadHistoryDataText(1, 0x339) : "";
            });
            Text(61, () => SuppressedContext() || forceId is >= 46 and <= 50 ? ReadHistoryDataText(1, 0x353)
                : Integer(Need(state.NativeForceUnregisterCurrent?.ForceRankings.GetValueOrDefault(forceId)?.Word1BC, "PopularSupport_Force1BC")));
            foreach (var pair in economy.Text) fields[pair.Key] = pair.Value;
            foreach (var pair in economy.UnresolvedFields) missing[pair.Key] = pair.Value;
            foreach (var pair in economy.TextStyles) styles[pair.Key] = pair.Value;
            var diplomacy = ReadForceDetailDiplomacyCurrent(state, current, forceId);
            foreach (var pair in diplomacy.Text) fields[pair.Key] = pair.Value;
            foreach (var pair in diplomacy.UnresolvedFields) missing[pair.Key] = pair.Value;
            hidden.UnionWith(diplomacy.HiddenNodes);
            DetailMiniMapValues? miniMap = null;
            Read(176, () =>
            {
                int physical = Person(Need(rawRuler, "MiniMap_Ruler10"));
                var home = BaseLookup.Find(Need(current.ReadPersonFirstLocation(physical), "MiniMap_E2"), current);
                var bases = ReadDetailMiniMapBases(current) ?? throw Missing("MiniMap_bases");
                byte color = Need(ReadMiniMapColor(current, forceId), "MiniMap_Force_colour");
                miniMap = new(bases, DetailMiniMapProjection.ProjectForce(forceId, current.ReadBaseCurrentHexIndex(home), color));
            });
            return Finish(page with { RulerId = rulerId, PortraitAppearanceId = appearanceId, StandingPortrait = standing,
                Nameplate = nameplate, MiniMap = miniMap, Relation = diplomacy.Relation });

            int Person(ushort raw) => Need((raw <= 6000 ? current.ReadPersonLookupReference(raw)
                : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_pointer");
            bool SuppressedContext()
            {
                var control = ReadTutorialControlState(state);
                int header = Need(TacticRemakeCurrentBattleControl.ReadHeader(control), "Tutorial_controller");
                if (header != 0 && Need(TacticRemakeCurrentBattleControl.ReadSelectedLessonIndex(control), "Tutorial_selected_lesson") != -1)
                    return true;
                int mode = Need(current.EventConditions.ScenarioMode, "Scenario_mode");
                if (mode == 3) throw Missing("CCCPersonSelectState_presence");
                return false;
            }
        }
        catch (InvalidDataException error) { missing[-1] = error.Message; return Finish(page); }

        ForceInformationPage Finish(ForceInformationPage value) => value with { HiddenNodes = hidden, TextStyles = styles,
            UnresolvedFields = missing, UnresolvedOwner = missing.Count == 0 ? null : string.Join("; ", missing.Values.Distinct().Order(StringComparer.Ordinal)) };
        void Read(int node, Action query) { try { query(); } catch (InvalidDataException error) { missing[node] = error.Message; } }
        void Text(int node, Func<string> query) => Read(node, () => fields[node] = query());
        void Number(int node, Func<int> query) => Text(node, () => Integer(query()));
        static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);
        static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        static InvalidDataException Missing(string field) => new("force-information." + field);
    }

    //Data0561 is a normal resource printf template with one floating operand.
    private static string FormatForceInformationRate(string format, float value)
    {
        var output = new StringBuilder(); int operands = 0;
        for (int index = 0; index < format.Length; index++)
        {
            if (format[index] != '%') { output.Append(format[index]); continue; }
            if (++index >= format.Length) throw Missing();
            if (format[index] == '%') { output.Append('%'); continue; }
            int width = 0, precision = 6;
            while (index < format.Length && char.IsAsciiDigit(format[index]))
                width = checked(width * 10 + format[index++] - '0');
            if (index < format.Length && format[index] == '.')
            {
                index++; precision = 0;
                while (index < format.Length && char.IsAsciiDigit(format[index]))
                    precision = checked(precision * 10 + format[index++] - '0');
            }
            if (index >= format.Length || format[index] != 'f' || ++operands != 1 || precision > 9 || width > 256) throw Missing();
            output.Append(((double)value).ToString("F" + precision, CultureInfo.InvariantCulture).PadLeft(width));
        }
        if (operands != 1) throw Missing();
        return output.ToString();
        static InvalidDataException Missing() => new("force-information.Land_Data0561_format");
    }
}
