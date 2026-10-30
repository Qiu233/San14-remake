using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal MapSummaryPage QueryCityMapSummaryCurrent(GameState state, SourceWorldContext current, MapSummaryReceiver receiver)
        => QueryFootholdMapSummaryCurrent(state, current, receiver, false);

    // The City/Gate summaries share scalar source getters and authored child
    // layouts. They retain their own exact mounts, fields and receiver types.
    private MapSummaryPage QueryFootholdMapSummaryCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver, bool gate)
    {
        string layout = gate ? "00_82_06_sum_seki" : "00_82_05_sum_city";
        var text = new Dictionary<int, string>();
        var missing = new Dictionary<int, string>();
        var children = new List<MapSummaryChild>();
        var page = new MapSummaryPage(receiver.Slot, receiver.ReceiverId, layout, null, text);
        BaseReference target;
        try
        {
            if (receiver.Slot != (gate ? 3 : 2)) throw FootholdSummaryMissing("manager_slot");
            if (gate ? receiver.ReceiverId is < 52 or > 61 : receiver.ReceiverId is < 0 or > 51)
                throw FootholdSummaryMissing("receiver_identity");
            target = gate ? current.ReadGateTableReference(receiver.ReceiverId - 51)
                ?? throw FootholdSummaryMissing("Gate_pointer")
                : current.ReadCityTableReference(receiver.ReceiverId) ?? throw FootholdSummaryMissing("City_pointer");
            if (!FootholdSummaryNeed(current.ReadBaseNativeValidity(target), "Base_validity"))
                return page with { ReceiverValid = false };
        }
        catch (InvalidDataException error) { return page with { UnresolvedOwner = error.Message }; }
        page = page with
        {
            ReceiverValid = true,
            TextPresets = new Dictionary<int, int> { [gate ? 114 : 132] = 0xD, [gate ? 118 : 136] = 0xD,
                [gate ? 115 : 133] = 0, [gate ? 119 : 137] = 0 },
            Visibility = new Dictionary<int, bool> { [gate ? 113 : 131] = false }
        };
        StaticText(gate ? 114 : 132, 0x5D5);
        StaticText(gate ? 118 : 136, 0x540);
        var values = new FootholdMapSummaryValues(this, state, current, target, receiver.ReceiverId);
        int header = gate ? 65 : 25;
        var force = Reference(header, () => values.Force);
        Child("identity", header, () => SummaryCityIdentity(current, receiver, force,
            String(header, () => values.Name), Number(header, () => values.DistrictNumber),
            showArmyMarker: false, emperorVisible: gate ? false : Number(header, () => values.EmperorVisible)));
        if (!gate)
        {
            Pair("size", 9, 0x5EA, () => values.Size);
            Pair("recruiter", 17, 0x4BB, () => values.Assignee(true));
            Pair("trainer", 21, 0x21, () => values.Assignee(false));
        }
        Pair("governor", gate ? 8 : 13, 0x215, () => values.Governor);
        if (gate) Pair("linked-city", 25, 0x201, () => values.LinkedCityName);
        Child("gold", gate ? 29 : 36, () => SummaryCityNumber(receiver, 0x1C1, 0x20C,
            String(gate ? 29 : 36, () => values.Resource(BaseResourceKind.Gold))));
        Child("food", gate ? 34 : 41, () => SummaryCityNumber(receiver, 0x1A0, 0x20D,
            String(gate ? 34 : 41, () => values.Resource(BaseResourceKind.Supplies))));
        int troops = gate ? 12 : 46;
        Child("troops", troops, () => SummaryCityTotal(receiver,
            String(troops, () => values.Troops.ToString(CultureInfo.InvariantCulture)),
            String(troops, () => values.TroopCapacity.ToString(CultureInfo.InvariantCulture))));
        int morale = gate ? 17 : 51;
        int? moraleCurrent = Number(morale, () => values.Morale), moraleMax = Number(morale, () => values.MoraleMaximum);
        Child("morale", morale, () => SummaryCityGauge(receiver, 0x1E2, 0x225,
            moraleCurrent?.ToString(CultureInfo.InvariantCulture), moraleCurrent, moraleMax));
        int defense = gate ? 18 : 58;
        int? defenseCurrent = Number(defense, () => values.Defense), defenseMax = Number(defense, () => values.DefenseMaximum);
        Child("defense", defense, () => SummaryCityGauge(receiver, 0x1F2, 0x20B,
            String(defense, () => FormatFootholdSummaryRatio(values.Defense, values.DefenseMaximum)), defenseCurrent, defenseMax));
        int people = gate ? 19 : 59;
        Child("people", people, () => SummaryCityNumber(receiver, 0x199, 0x218,
            String(people, () => values.HomePeople.ToString(CultureInfo.InvariantCulture))));
        int tactic = gate ? 40 : 66;
        Child("tactic", tactic, () => BuildFootholdSummaryTactic(receiver, values, missing, tactic));
        int status = gate ? 51 : 77;
        Child("status", status, () => SummaryCityStatus(receiver, new Dictionary<int, int?>
        {
            [3] = Number(status, () => values.StatusPreset(false)),
            [4] = Number(status, () => values.StatusPreset(true))
        }));

        // These are the actual nested Area presentations, not a full detail
        // page. The Area author owns their shared source-current readers.
        try
        {
            var area = CurrentGateMapQueries.FindArea(target, current);
            page = MergeFootholdSummaryArea(page, BuildBaseAreaMapSummaryCurrent(state, current, receiver, area,
                layout, gate ? GateSummaryAreaNodes : CitySummaryAreaNodes, gate ? 112 : 130));
            children.Add(new("area-coverage", gate ? 135 : 153,
                BuildAreaMapSummaryCoverageCurrent(state, current, receiver, area)));
            children.Add(new("area-disaster", gate ? 58 : 84,
                BuildAreaMapSummaryDisasterCurrent(state, current, receiver, area)));
        }
        catch (InvalidDataException error) { missing[gate ? 112 : 130] = error.Message; }
        return page with { Children = [.. children, .. page.Children], UnresolvedFields = MergeFootholdSummaryMap(page.UnresolvedFields, missing) };

        void StaticText(int node, int caption)
        { var value = String(node, () => ReadHistoryDataText(1, caption)); if (value is not null) text[node] = value; }
        void Pair(string key, int mount, int caption, Func<string> read)
            => Child(key, mount, () => SummaryCityPair(receiver, caption, String(mount, read)));
        void Child(string key, int mount, Func<MapSummaryPage> read)
        {
            try { children.Add(new(key, mount, read())); }
            catch (InvalidDataException error) { missing[mount] = error.Message; }
        }
        string? String(int node, Func<string> read)
        { try { return read(); } catch (InvalidDataException error) { missing[node] = error.Message; return null; } }
        T? Number<T>(int node, Func<T> read) where T : struct
        { try { return read(); } catch (InvalidDataException error) { missing[node] = error.Message; return null; } }
        T? Reference<T>(int node, Func<T> read) where T : class
        { try { return read(); } catch (InvalidDataException error) { missing[node] = error.Message; return null; } }
    }

    private string FormatFootholdSummaryRatio(int current, int maximum)
        => FormatCityDetailPrice(ReadHistoryDataText(1, 0x325), current, maximum);

    private MapSummaryPage BuildFootholdSummaryTactic(MapSummaryReceiver receiver, FootholdMapSummaryValues values,
        IDictionary<int, string> outerMissing, int mount)
    {
        var text = new Dictionary<int, string> { [4] = ReadHistoryDataText(1, 0x234) };
        //918700 hides the Lv group6. The authored MAX sprite8 is its
        //descendant, so it stays hidden throughout this tactic presentation.
        var visible = new Dictionary<int, bool> { [3] = false, [6] = false, [8] = false };
        var colors = new Dictionary<int, MapCornerColors>();
        var missing = new Dictionary<int, string>();
        var bars = new List<MapSummaryBar>();
        try
        {
            int? tactic = values.Tactic;
            if (tactic is null) { text[5] = ReadHistoryDataText(1, 0x53F); visible[2] = false; }
            else
            {
                visible[2] = true;
                try { text[5] = values.TacticName(tactic.Value); }
                catch (InvalidDataException error) { missing[5] = error.Message; }
                try
                {
                    int cooldown = values.Cooldown;
                    var color = MapSpritePalettes.FromAuthored(cooldown != 0 ? 0xFF808080u : 0xFFFFFFFFu);
                    colors[5] = new(color, color, color, color);
                    int upper = values.CooldownMaximum;
                    bars.Add(new(2, unchecked(upper - cooldown), upper, 1));
                }
                catch (InvalidDataException error) { missing[2] = error.Message; }
            }
        }
        catch (InvalidDataException error) { missing[5] = error.Message; outerMissing[mount] = error.Message; }
        return new(receiver.Slot, receiver.ReceiverId, "00_82_05_sum_city_offset_05", true, text)
        {
            TextPresets = new Dictionary<int, int> { [4] = 0xD, [5] = 0 },
            Colors = colors, Visibility = visible, Bars = bars, UnresolvedFields = missing
        };
    }

    private static MapSummaryPage MergeFootholdSummaryArea(MapSummaryPage root, MapSummaryPage area) => root with
    {
        Text = MergeFootholdSummaryMap(root.Text, area.Text),
        TextPresets = MergeFootholdSummaryMap(root.TextPresets, area.TextPresets),
        Colors = MergeFootholdSummaryMap(root.Colors, area.Colors),
        Visibility = MergeFootholdSummaryMap(root.Visibility, area.Visibility),
        Images = [.. root.Images, .. area.Images], Gauges = [.. root.Gauges, .. area.Gauges],
        Bars = [.. root.Bars, .. area.Bars], Children = [.. root.Children, .. area.Children],
        UnresolvedFields = MergeFootholdSummaryMap(root.UnresolvedFields, area.UnresolvedFields)
    };
    private static IReadOnlyDictionary<int, T> MergeFootholdSummaryMap<T>(IReadOnlyDictionary<int, T> first,
        IReadOnlyDictionary<int, T> second)
    {
        var result = new Dictionary<int, T>(first);
        foreach (var (node, value) in second) result[node] = value;
        return result;
    }

    private static readonly int[] CitySummaryAreaNodes = [98,108,118,94,132,133,136,137,138,143,148];
    private static readonly int[] GateSummaryAreaNodes = [80,90,100,76,114,115,118,119,120,125,130];

    private sealed class FootholdMapSummaryValues
    {
        private readonly ArmyCampaignRuleAdapter rules;
        private readonly GameState state;
        private readonly SourceWorldContext current;
        private readonly BaseReference target;
        private readonly int id;
        private readonly CityInformationCurrent city;
        private readonly GateInformationCurrent gate;
        internal FootholdMapSummaryValues(ArmyCampaignRuleAdapter rules, GameState state, SourceWorldContext current,
            BaseReference target, int id)
        {
            this.rules = rules; this.state = state; this.current = current; this.target = target; this.id = id;
            city = new(rules, state, current); gate = new(rules, state, current);
        }
        internal string Name => target.Kind == BaseKind.City ? city.SummaryName(id) : gate.SummaryName(id);
        internal ForceReference Force
        {
            get { int raw = FootholdSummaryNeed(current.ReadBaseForceSlot(target), "Base_Force");
                return current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw FootholdSummaryMissing("Force_pointer"); }
        }
        internal int DistrictNumber
        {
            get { var district = current.ReadBaseDistrictReference(target) ?? throw FootholdSummaryMissing("District_pointer");
                if (!FootholdSummaryNeed(current.ReadDistrictNativeValidity(district), "District_validity")) return 0;
                return FootholdSummaryNeed(current.ReadDistrictNumber(FootholdSummaryNeed(district.PhysicalSlot, "District_identity")), "District_number11"); }
        }
        internal bool EmperorVisible
        {
            get
            {
                int raw = FootholdSummaryNeed(current.ReadEmperorId(), "World_Emperor1654");
                if (raw is < 1 or > 6000) return false;
                int physical = FootholdSummaryNeed(current.ReadPersonLookupReference(raw)?.PhysicalSlot, "Emperor_pointer");
                if (!FootholdSummaryNeed(current.ReadPersonNativeValidity(physical), "Emperor_validity")) return false;
                var position = PersonPositionRepair.DecodeSecondLocation(FootholdSummaryNeed(current.ReadPersonSecondLocation(physical), "Emperor_E4"));
                return position.Kind == 5 && position.Index == id;
            }
        }
        internal string Size => city.SummarySize(id);
        internal string Governor
        {
            get { int raw = FootholdSummaryNeed(current.ReadArmyReturnBaseGovernor2E(target), "Governor2E");
                return PersonName(raw, rules.ReadHistoryDataText(1, 0x610)); }
        }
        internal string Assignee(bool recruiter) => PersonName(FootholdSummaryNeed(recruiter
            ? current.ReadAssignmentCityCA(id) : current.ReadAssignmentCityCC(id), recruiter ? "CityCA" : "CityCC"), "");
        private string PersonName(int raw, string invalid) => city.SummaryPersonName(raw, invalid);
        internal string Resource(BaseResourceKind kind)
        {
            int slot = FootholdSummaryNeed(current.ReadForcePhysicalSlot(Force), "Force_identity");
            if (slot is >= 46 and <= 50) return rules.ReadHistoryDataText(1, 0x353);
            return (target.Kind == BaseKind.City ? city.SummaryResource(id, kind) : gate.SummaryResource(id, kind))
                .ToString(CultureInfo.InvariantCulture);
        }
        internal int Troops => FootholdSummaryNeed(current.ReadBaseResource(target, BaseResourceKind.Troops), "Base3C");
        internal int TroopCapacity => BasePostLoad.ReadResourceCapacities(target, current).Troops;
        internal int Morale => FootholdSummaryNeed(current.ReadBaseMorale44(target), "Base44");
        internal int MoraleMaximum => BasePostLoad.ReadMoraleUpper(Force, current);
        internal int Defense => BasePostLoad.ReadLinkedField14(target, current);
        internal int DefenseMaximum => BasePostLoad.ReadCapacities(target, current).Field46;
        internal int HomePeople => DistrictCreationHomePeople(current, target).Count();
        internal string LinkedCityName
        {
            get
            {
                var selected = CurrentGateMapQueries.FindCity(target, current);
                if (!FootholdSummaryNeed(current.ReadBaseNativeValidity(selected), "linked_City_validity"))
                {
                    var area = CurrentGateMapQueries.FindArea(target, current);
                    if (FootholdSummaryNeed(current.ReadAreaNativeValidity(area), "linked_Area_validity"))
                    { int raw = FootholdSummaryNeed(current.ReadAreaValidityCode(area), "linked_Area34");
                        selected = current.ReadCityTableReference(raw < 52 ? raw : 0) ?? throw FootholdSummaryMissing("original_City"); }
                }
                return FootholdSummaryNeed(current.ReadBaseNativeValidity(selected), "fallback_City_validity")
                    ? city.SummaryName(FootholdSummaryNeed(selected.PhysicalSlot, "linked_City_identity")) : "";
            }
        }
        private CaptureBaseThreatFields Threat => current.ReadCaptureBaseThreatFields(target) ?? throw FootholdSummaryMissing("Base_threat_fields");
        internal int? Tactic => target.Kind == BaseKind.City ? city.SummaryTactic(id) : gate.SummaryTactic(id);
        internal string TacticName(int tactic) => state.Tactics.GetValueOrDefault(tactic)?.Name ?? throw FootholdSummaryMissing("Tactic_name");
        internal int Cooldown => FootholdSummaryNeed(Threat.Word4C, "Base4C_cooldown");
        internal int CooldownMaximum => FootholdSummaryNeed(ArmyDestructionParameterSeed.ReadCurrent(state,
            SimulationParameterKeys.SubdayStatus.BaseCooldownUpper), "231490_cooldown_upper");
        internal int StatusPreset(bool first) => FootholdSummaryNeed(first ? Threat.Byte49 : Threat.Byte4B,
            first ? "Base49_status" : "Base4B_status") == 0 ? 0x2D : 0xC;
    }

    private sealed partial class CityInformationCurrent
    {
        internal CityInformationCurrent(ArmyCampaignRuleAdapter rules, GameState state, SourceWorldContext current)
        { this.rules = rules; this.state = state; playerForceId = 0; captured = new(() => current); }
        internal string SummaryName(int id) => ReadCity(id).Owner.Name;
        internal string SummarySize(int id) => SizeText(ReadCity(id));
        internal int SummaryResource(int id, BaseResourceKind kind) => Resource(ReadCity(id), kind);
        internal int? SummaryTactic(int id) => Tactic(ReadCity(id));
        internal string SummaryPersonName(int raw, string invalid)
        { int person = Person(raw); return Need(Current.ReadPersonNativeValidity(person), "summary_Person_validity") ? PersonText(person) : invalid; }
    }

    private static T FootholdSummaryNeed<T>(T? value, string field) where T : struct
        => value ?? throw FootholdSummaryMissing(field);
    private static InvalidDataException FootholdSummaryMissing(string field) => new("map-summary.foothold." + field);
}
