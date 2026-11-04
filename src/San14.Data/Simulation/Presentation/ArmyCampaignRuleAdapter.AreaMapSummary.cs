using System.Globalization;
using System.Text.RegularExpressions;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //927E00: the compact ordinary map caller, independent of902610's full page.
    internal MapSummaryPage QueryAreaMapSummaryCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver)
    {
        var output = new AreaSummaryPublication(receiver, "00_82_04_sum_area");
        AreaReference area;
        try
        {
            area = current.ReadAreaTableReference(receiver.ReceiverId) ?? throw AreaSummaryMissing("Area_pointer");
            output.Valid = AreaSummaryNeed(current.ReadAreaNativeValidity(area), "Area_validity");
            if (output.Valid == false) return output.Page();
        }
        catch (InvalidDataException error) { output.Missing[-1] = error.Message; return output.Page(); }
        int physical = AreaSummaryNeed(area.PhysicalSlot, "Area_identity");
        output.Read(45, () => { output.Text[45] = ReadHistoryDataText(1, 0x5D5); output.Presets[45] = 13; });
        output.Read(49, () => { output.Text[49] = ReadHistoryDataText(1, 0x540); output.Presets[49] = 13; });
        output.Visible[44] = false;
        output.Visible[43] = false;
        output.Read(46, () =>
        {
            int security = AreaSummarySecurity(state, physical);
            output.Text[46] = AreaSummaryNumber(security);
            output.Visible[43] = true;
            output.Bars.Add(new(43, security, 100, 1) { InitialCurrent = 70, InitialMaximum = 100 });
        });
        //This caller does not apply924610's tribal population suppression.
        output.Read(50, () => output.Text[50] = AreaSummaryNumber(AreaSummaryPopulation(state, physical)));
        output.Child(152, "identity", () =>
        {
            var force = AreaSummaryForce(current, area);
            int district = 0;
            var city = AreaSummaryCity(current, area);
            if (AreaSummaryNeed(current.ReadBaseNativeValidity(city), "identity_City_validity"))
            {
                int rawForce = AreaSummaryNeed(current.ReadBaseForceSlot(city), "identity_City_Force_slot");
                var cityForce = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
                    ?? throw AreaSummaryMissing("identity_City_Force");
                if (AreaSummaryNeed(current.ReadForceNativeValidity(force), "identity_Area_Force_validity") &&
                    AreaSummaryNeed(current.ReadForceNativeValidity(cityForce), "identity_City_Force_validity") && force == cityForce)
                {
                    int raw = AreaSummaryNeed(current.ReadBaseDistrictSlot(city), "identity_City_District");
                    var selected = current.ReadDistrictTableReference(raw < 52 ? raw : 0) ?? throw AreaSummaryMissing("identity_District");
                    if (AreaSummaryNeed(current.ReadDistrictNativeValidity(selected), "identity_District_validity"))
                        district = AreaSummaryNeed(current.ReadDistrictNumber(AreaSummaryNeed(selected.PhysicalSlot, "identity_District_identity")), "identity_District11");
                }
            }
            string name = state.Areas.GetValueOrDefault(physical)?.Name ?? throw AreaSummaryMissing("Area_name");
            return SummaryCityIdentity(current, receiver, force, name, district, false, false);
        });
        output.Child(7, "officer", () => BuildAreaMapSummaryOfficerCurrent(state, current, receiver, area));
        output.Child(148, "city", () =>
        {
            var city = AreaSummaryCity(current, area);
            bool valid = AreaSummaryNeed(current.ReadBaseNativeValidity(city), "City_validity");
            string name = valid ? state.Cities.GetValueOrDefault(AreaSummaryNeed(city.PhysicalSlot, "City_identity"))?.Name
                ?? throw AreaSummaryMissing("City_name") : string.Empty;
            var child = SummaryCityPair(receiver, 0x201, name);
            return child with { Visibility = new Dictionary<int, bool> { [3] = valid } };
        });
        int[] developmentMounts = [11, 21, 31];
        int[] incomeMounts = [51, 56, 61];
        var incomeInputs = new AreaSummaryIncomeInputs();
        for (int kind = 1; kind <= 3; kind++)
        {
            int captured = kind;
            output.Child(developmentMounts[kind - 1], "development" + kind, () =>
                BuildAreaMapSummaryDevelopmentCurrent(state, current, receiver, area, captured));
            output.Child(incomeMounts[kind - 1], "income" + kind, () =>
                BuildAreaMapSummaryIncomeCurrent(state, current, receiver, area, captured, incomeInputs));
        }
        output.Child(66, "coverage", () => BuildAreaMapSummaryCoverageCurrent(state, current, receiver, area));
        output.Child(140, "disaster", () => BuildAreaMapSummaryDisasterCurrent(state, current, receiver, area));
        return output.Page();
    }

    //924610 publishes these fields into the actual City/Gate parent. The
    //caller supplies its audited resource node array and merges this page.
    internal MapSummaryPage BuildBaseAreaMapSummaryCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver, AreaReference area, string layoutName, IReadOnlyList<int> nodes,
        int securityGaugeNode)
    {
        if (nodes.Count != 11) throw AreaSummaryMissing("Base_Area_node_table");
        var output = new AreaSummaryPublication(receiver, layoutName) { Valid = true };
        int physical = AreaSummaryNeed(area.PhysicalSlot, "Base_Area_identity");
        var incomeInputs = new AreaSummaryIncomeInputs();
        for (int kind = 1; kind <= 3; kind++)
        {
            int captured = kind;
            output.Child(nodes[kind - 1], "development" + kind, () =>
                BuildAreaMapSummaryDevelopmentCurrent(state, current, receiver, area, captured));
            output.Child(nodes[kind + 7], "income" + kind, () =>
                BuildAreaMapSummaryIncomeCurrent(state, current, receiver, area, captured, incomeInputs));
        }
        output.Child(nodes[3], "officer", () => BuildAreaMapSummaryOfficerCurrent(state, current, receiver, area));
        output.Visible[securityGaugeNode] = false;
        output.Read(nodes[5], () =>
        {
            int security = AreaSummarySecurity(state, physical);
            output.Text[nodes[5]] = AreaSummaryNumber(security);
            output.Visible[securityGaugeNode] = true;
            output.Bars.Add(new(securityGaugeNode, security, 100, 1) { InitialCurrent = 70, InitialMaximum = 100 });
        });
        output.Read(nodes[7], () => output.Text[nodes[7]] = AreaSummarySpecial(current, area)
            ? ReadHistoryDataText(1, 0x353) : AreaSummaryNumber(AreaSummaryPopulation(state, physical)));
        return output.Page();
    }

    internal MapSummaryPage BuildAreaMapSummaryIncomeCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver, AreaReference area, int kind)
        => BuildAreaMapSummaryIncomeCurrent(state, current, receiver, area, kind, new AreaSummaryIncomeInputs());

    // The three scalar consumers belong to one synchronous parent publication.
    // Bind lazily only after the original validity/special-city gates, then
    // retain these exact state/current inputs until that publication returns.
    private sealed class AreaSummaryIncomeInputs
    {
        internal AreaAdministrationPreviewSource? Preview;
    }

    private MapSummaryPage BuildAreaMapSummaryIncomeCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver, AreaReference area, int kind, AreaSummaryIncomeInputs incomeInputs)
    {
        if (kind is < 1 or > 3) throw AreaSummaryMissing("income_kind");
        var output = new AreaSummaryPublication(receiver, "00_82_05_sum_city_offset_03") { Valid = true };
        output.Presets[4] = 0;
        output.Read(3, () => { output.Text[3] = ReadHistoryDataText(1, 0x20F); output.Presets[3] = 13; });
        output.Images.Add(new(2, kind == 1 ? "000_cmn_icon_kane_fuchi" : kind == 2
            ? "000_cmn_icon_hyourou_fuchi" : "000_cmn_icon_heishi_fuchi"));
        output.Colors[2] = SummaryChildPalettes[21];
        output.Read(4, () =>
        {
            bool valid = AreaSummaryNeed(current.ReadAreaNativeValidity(area), "income_Area_validity");
            output.Visible[4] = valid;
            if (!valid) return;
            if (AreaSummarySpecial(current, area)) { output.Text[4] = ReadHistoryDataText(1, 0x353); return; }
            var city = AreaSummaryCity(current, area);
            if (!AreaSummaryNeed(current.ReadBaseNativeValidity(city), "income_City_validity"))
            { output.Text[4] = string.Empty; return; }
            var preview = incomeInputs.Preview;
            if (preview is null)
            {
                bool bound = _baseCapacityInputs.BindCurrent(state, current);
                preview = new AreaAdministrationPreviewSource(state, current, _scenario, Seed.Language, _baseCapacityInputs, bound);
                incomeInputs.Preview = preview;
            }
            int physical = AreaSummaryNeed(area.PhysicalSlot, "income_Area_identity");
            int Count(int person) => PersonAssignmentPrecallInputs.ReadSavedPersonAreaActiveCount(state, person)
                ?? throw AreaSummaryMissing("income_Person_task_vector");
            var query = kind switch { 1 => preview.Gold(physical, Count), 2 => preview.Food(physical, Count),
                _ => preview.Soldiers(physical, mode: 2) };
            output.Text[4] = query.Available && query.Value is int value ? AreaSummaryNumber(value)
                : throw AreaSummaryMissing(query.UnresolvedInput ?? "income_scalar");
        });
        return output.Page();
    }

    internal MapSummaryPage BuildAreaMapSummaryDevelopmentCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver, AreaReference area, int kind)
    {
        if (kind is < 1 or > 3) throw AreaSummaryMissing("development_kind");
        var output = new AreaSummaryPublication(receiver, "00_82_05_sum_city_offset_05") { Valid = true };
        output.Visible[3] = false; output.Visible[5] = false; output.Visible[10] = false;
        output.Visible[7] = output.Visible[8] = output.Visible[9] = output.Visible[11] = false;
        output.Read(4, () => { output.Text[4] = ReadHistoryDataText(1, 0x2B2 + kind); output.Presets[4] = 13; });
        output.Read(2, () =>
        {
            var field = kind == 1 ? CeAreaDevelopmentField.Commerce : kind == 2 ? CeAreaDevelopmentField.Agriculture : CeAreaDevelopmentField.Barracks;
            var context = new OwnedAreaDevelopmentContext(state, current, _normalAftermathInputs);
            ushort raw = AreaSummaryNeed(context.ReadAreaProgress(area, field), "development_progress");
            var rules = _normalAftermathInputs.Development;
            int level = rules.ReadLevel(raw);
            bool maximum = level > 4;
            output.Visible[8] = maximum; output.Visible[9] = !maximum; output.Visible[11] = !maximum;
            output.Images.Add(new(8, "cmn_mrk_max"));
            int value, capacity;
            if (level == 5) { value = capacity = 1000; }
            else
            {
                int lower = level == 1 ? 0 : rules.LevelThresholds[level - 2];
                capacity = rules.LevelThresholds[level - 1] - lower;
                value = raw - lower;
            }
            output.Bars.Add(new(2, value, capacity, 1) { InitialCurrent = 70, InitialMaximum = 100 });
            if (!maximum)
            {
                string prefix = ReadHistoryDataText(1, 0x250), valueText = AreaSummaryNumber(level);
                output.DevelopmentMeasure = new(prefix, valueText);
                bool special = AreaSummarySpecial(current, area);
                output.Text[9] = special ? string.Empty : prefix;
                output.Text[11] = special ? string.Empty : valueText;
                int physical = AreaSummaryNeed(area.PhysicalSlot, "development_Area_identity");
                bool arrow = false;
                if (AreaSummaryNeed(current.ReadAssignmentArea72(physical), "development_Area72") == kind)
                {
                    int person = AreaSummaryAssignedPerson(current, physical);
                    arrow = AreaSummaryNeed(current.ReadPersonNativeValidity(person), "development_Person_validity");
                }
                output.Visible[7] = arrow;
                output.Images.Add(new(7, "000_cmn_icon_genek_fuchi"));
            }
            else output.Visible[7] = false;
        });
        return output.Page();
    }

    internal MapSummaryPage BuildAreaMapSummaryDisasterCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver, AreaReference area)
    {
        var output = new AreaSummaryPublication(receiver, "00_82_05_sum_city_offset_06_pk") { Valid = true };
        output.Read(2, () => { output.Text[2] = ReadHistoryDataText(1, 0x3FD); output.Presets[2] = 13; });
        int[] masks = [1, 4, 16];
        string[] names = ["000_cmn_icon_disaster_ekibyou", "000_cmn_icon_disaster_inago", "000_cmn_icon_disaster_housaku"];
        for (int index = 0; index < 3; index++)
        {
            int node = index + 3, mask = masks[index], palette = index == 2 ? 11 : 12;
            output.Images.Add(new(node, names[index]));
            output.Read(node, () => output.Colors[node] = SummaryChildPalettes[
                (AreaSummaryNeed(current.ReadAreaFlags38(area), "disaster_Area38") & mask) != 0 ? palette : 49]);
        }
        output.Visible[6] = output.Visible[7] = false;
        return output.Page();
    }

    internal MapSummaryPage BuildAreaMapSummaryCoverageCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver, AreaReference area)
    {
        var output = new AreaSummaryPublication(receiver, "00_82_05_sum_city_offset_07") { Valid = true };
        output.Visible[4] = false;
        output.Visible[3] = output.Visible[6] = false;
        for (int row = 0; row < 8; row++) output.Visible[8 + row * 8] = false;
        output.Read(5, () => { output.Text[5] = ReadHistoryDataText(1, 0x3D3); output.Presets[5] = 13; });
        output.Read(7, () => { output.Text[7] = ReadHistoryDataText(1, 0x5FE); output.Presets[7] = 13; });
        output.Read(6, () =>
        {
            var force = AreaSummaryForce(current, area);
            bool valid = AreaSummaryNeed(current.ReadForceNativeValidity(force), "coverage_Force_validity");
            output.Visible[6] = output.Visible[3] = valid;
            if (!valid) return;
            int owned = AreaSummarySavedOwnedCellCount(state, area, "coverage_Area68");
            int total = AreaSummarySavedTerrainCellCount(state, area, "coverage_Area76");
            output.Text[6] = AreaSummaryFormat(ReadHistoryDataText(1, 0x325), owned, total);
            int style = total == 0 || owned * 100 / total == 100 ? 0 : owned * 100 / total < 50 ? 2 : 1;
            output.Presets[6] = new[] { 17, 18, 16 }[style];
            output.Bars.Add(new(3, total == 0 ? 70 : owned, total == 0 ? 100 : total, new[] { 13, 10, 12 }[style])
                { InitialCurrent = 70, InitialMaximum = 100 });
        });
        output.Read(8, () =>
        {
            int physical = AreaSummaryNeed(area.PhysicalSlot, "neighbors_Area_identity");
            var definition = _scenario.Snapshot.Areas[physical];
            var neighbors = new List<AreaReference>();
            for (int slot = 0; slot < 8; slot++)
            {
                if ((definition.RegionalNeighborMask & (1 << slot)) == 0) continue;
                int raw = AreaSummaryNeed(current.ReadAreaNeighborSlot(area, slot), "neighbors_Area_word");
                var next = current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw AreaSummaryMissing("neighbor_pointer");
                if (AreaSummaryNeed(current.ReadAreaNativeValidity(next), "neighbor_validity")) neighbors.Add(next);
            }
            neighbors = neighbors.OrderBy(next => AreaSummaryNeed(current.ReadAreaPhysicalSlot(next), "neighbor_identity")).ToList();
            for (int row = 0; row < 8; row++)
            {
                int mount = 8 + row * 8;
                output.Visible[mount] = row < neighbors.Count;
                if (row >= neighbors.Count) continue;
                var next = neighbors[row];
                output.Child(mount, "neighbor" + row, () => BuildAreaMapSummaryNeighborCurrent(state, current, receiver, next));
            }
        });
        return output.Page();
    }

    private MapSummaryPage BuildAreaMapSummaryNeighborCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver, AreaReference area)
    {
        var output = new AreaSummaryPublication(receiver, "00_82_05_sum_city_offset_08") { Valid = true };
        output.Visible[7] = false;
        output.Visible[6] = false;
        output.Read(3, () => AreaSummaryFlag(current, AreaSummaryForce(current, area), output));
        if (output.Missing.ContainsKey(3)) output.Visible[4] = false;
        output.Read(5, () =>
        {
            int owned = AreaSummarySavedOwnedCellCount(state, area, "neighbor_Area68");
            var force = AreaSummaryForce(current, area);
            if (!AreaSummaryNeed(current.ReadForceNativeValidity(force), "neighbor_Force_validity"))
            {
                var player = current.ReadCurrentPlayerForce() ?? throw AreaSummaryMissing("neighbor_player_Force");
                int owner = AreaSummaryNeed(current.ReadForcePhysicalSlot(player), "neighbor_player_identity");
                int physical = AreaSummaryNeed(area.PhysicalSlot, "neighbor_Area_identity");
                owned = 0;
                for (int index = AreaHexIterator.FindNextIndex(0, physical, current); index < 48400;
                    index = AreaHexIterator.FindNextIndex(index + 1, physical, current))
                {
                    var hex = current.ReadHexTableReference(index) ?? throw AreaSummaryMissing("neighbor_Hex_pointer");
                    if (AreaSummaryNeed(current.ReadHexForceSlot(hex), "neighbor_Hex14") == owner) owned++;
                }
            }
            int total = AreaSummarySavedTerrainCellCount(state, area, "neighbor_Area76");
            output.Text[5] = AreaSummaryFormat(ReadHistoryDataText(1, 0x560), owned, total);
            output.Visible[6] = true;
            output.Bars.Add(new(6, owned, total, 1) { InitialCurrent = 70, InitialMaximum = 100 });
        });
        return output.Page();
    }

    private MapSummaryPage BuildAreaMapSummaryOfficerCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver, AreaReference area)
    {
        var output = new AreaSummaryPublication(receiver, "00_82_05_sum_city_offset_02") { Valid = true };
        output.Presets[3] = 0;
        output.Read(2, () => { output.Text[2] = ReadHistoryDataText(1, 0x3D4); output.Presets[2] = 13; });
        output.Read(3, () =>
        {
            int physical = AreaSummaryAssignedPerson(current, AreaSummaryNeed(area.PhysicalSlot, "officer_Area_identity"));
            bool valid = AreaSummaryNeed(current.ReadPersonNativeValidity(physical), "officer_Person_validity");
            output.Visible[3] = valid;
            if (!valid) return;
            int identity = AreaSummaryNeed(current.ReadPersonIdentity(physical), "officer_Person_identity");
            output.Text[3] = state.Officers.GetValueOrDefault(identity)?.Name ?? throw AreaSummaryMissing("officer_Person_name");
        });
        return output.Page();
    }

    private void AreaSummaryFlag(SourceWorldContext current, ForceReference force, AreaSummaryPublication output)
    {
        bool valid = AreaSummaryNeed(current.ReadForceNativeValidity(force), "flag_Force_validity");
        int physical = AreaSummaryNeed(force.PhysicalSlot, "flag_Force_identity");
        int color = AreaSummaryNeed(current.ReadForceReportColor(force), "flag_Force49");
        var fill = valid || physical is >= 46 and <= 51 ? MapSpritePalettes.ForceFill(color)
            : MapSpritePalettes.FromAuthored(0xFFFFFFFF);
        var letter = SummaryChildPalettes.ForceLetter(color);
        output.Colors[3] = new(fill, fill, fill, fill); output.Colors[4] = new(letter, letter, letter, letter);
        _mapInformationNames ??= ArmyFlagPaneNames.Load(_scenario);
        output.Text[4] = valid || physical is >= 46 and <= 51 ? _mapInformationNames.Resolve(current, force).NativeShortName : string.Empty;
    }

    private static ForceReference AreaSummaryForce(SourceWorldContext current, AreaReference area)
    {
        int raw = AreaSummaryNeed(current.ReadAreaCurrentForceSlot(area), "Area_representative_Force");
        return current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw AreaSummaryMissing("Area_Force_pointer");
    }
    private static bool AreaSummarySpecial(SourceWorldContext current, AreaReference area) =>
        AreaSummaryNeed(current.ReadForcePhysicalSlot(AreaSummaryForce(current, area)), "Area_Force_identity") is >= 46 and <= 50;
    private static BaseReference AreaSummaryCity(SourceWorldContext current, AreaReference area)
    {
        int raw = AreaSummaryNeed(current.ReadAreaCitySlot(area), "Area35_City");
        return current.ReadCityTableReference(raw < 52 ? raw : 0) ?? throw AreaSummaryMissing("Area_City_pointer");
    }
    private static int AreaSummaryAssignedPerson(SourceWorldContext current, int physical)
    {
        int raw = AreaSummaryNeed(current.ReadAssignmentArea36(physical), "Area36_Person");
        var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw AreaSummaryMissing("assigned_Person_lookup");
        return AreaSummaryNeed(person.PhysicalSlot, "assigned_Person_pointer");
    }
    //927E00's coverage/neighbor pages consume the saved raw68/76 words.
    //The lightweight map current need not install regional-effects inputs;
    //their separate qualification flag must not hide these latched UI values.
    //Use the same published World as population/security, without recounting
    //or promoting any shared-current regional-effects input.
    private static AreaMapStatisticsEntry AreaSummarySavedStatistics(GameState state, AreaReference area, string field)
    {
        int physical = AreaSummaryNeed(area.PhysicalSlot, "statistics_Area_identity");
        return state.RegionStatistics?.Entries.GetValueOrDefault(physical)
            ?? throw AreaSummaryMissing(field);
    }
    private static int AreaSummarySavedOwnedCellCount(GameState state, AreaReference area, string field) =>
        AreaMapStatisticsInitialization.CountToNativeWord(AreaSummarySavedStatistics(state, area, field).OwnedCellCount);
    private static int AreaSummarySavedTerrainCellCount(GameState state, AreaReference area, string field) =>
        AreaMapStatisticsInitialization.CountToNativeWord(AreaSummarySavedStatistics(state, area, field).TerrainCellCount);

    private static int AreaSummaryPopulation(GameState state, int physical) =>
        state.Areas.GetValueOrDefault(physical)?.NativeNormalAftermath?.Quantity52 ?? throw AreaSummaryMissing("Area52_population");
    private static int AreaSummarySecurity(GameState state, int physical) =>
        state.Areas.GetValueOrDefault(physical)?.Security is int value and >= 0 and <= 255 ? value : throw AreaSummaryMissing("Area54_security");
    private static string AreaSummaryNumber(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string AreaSummaryFormat(string format, params int[] values)
    {
        int index = 0;
        string result = Regex.Replace(format, @"%(-?)(\d*)[du]", match =>
        {
            if (index >= values.Length) throw AreaSummaryMissing("number_format_arguments");
            string text = AreaSummaryNumber(values[index++]);
            if (match.Groups[2].Value.Length != 0)
            {
                int width = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                text = match.Groups[1].Value == "-" ? text.PadRight(width) : text.PadLeft(width);
            }
            return text;
        });
        if (index != values.Length) throw AreaSummaryMissing("number_format");
        return result;
    }
    private static T AreaSummaryNeed<T>(T? value, string field) where T : struct => value ?? throw AreaSummaryMissing(field);
    private static InvalidDataException AreaSummaryMissing(string field) => new("map-summary.area." + field);

    private sealed class AreaSummaryPublication(MapSummaryReceiver receiver, string layout)
    {
        internal bool? Valid;
        internal MapSummaryDevelopmentMeasure? DevelopmentMeasure;
        internal readonly Dictionary<int, string> Text = [], Missing = [];
        internal readonly Dictionary<int, int> Presets = [];
        internal readonly Dictionary<int, bool> Visible = [];
        internal readonly Dictionary<int, MapCornerColors> Colors = [];
        internal readonly List<MapSummaryImage> Images = [];
        internal readonly List<MapSummaryBar> Bars = [];
        internal readonly List<MapSummaryChild> Children = [];
        internal void Read(int node, Action read)
        {
            try { read(); }
            catch (InvalidDataException error) { Missing[node] = error.Message; Visible[node] = false; }
        }
        internal void Child(int mount, string key, Func<MapSummaryPage> read) => Read(mount, () =>
        {
            var page = read();
            var visibility = new Dictionary<int, bool>(page.Visibility);
            foreach (int node in page.UnresolvedFields.Keys) if (node >= 0) visibility[node] = false;
            Children.Add(new(key, mount, page with { Visibility = visibility }));
        });
        internal MapSummaryPage Page() => new(receiver.Slot, receiver.ReceiverId, layout, Valid, Text)
        {
            TextPresets = Presets, Visibility = Visible, Colors = Colors, Images = Images, Bars = Bars,
            Children = Children, UnresolvedFields = Missing, DevelopmentMeasure = DevelopmentMeasure,
            UnresolvedOwner = Missing.Count == 0 ? null : string.Join("; ", Missing.Values.Distinct(StringComparer.Ordinal))
        };
    }
}
