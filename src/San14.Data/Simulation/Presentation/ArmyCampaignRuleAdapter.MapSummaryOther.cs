using System.Globalization;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // One normal resource table per publication, never a per-field parse or hash.
    private Func<int, int, string> OtherSummaryText()
    {
        var messages = _repository.LoadMessages(Seed.Language, MessageKind.Data).Messages;
        return (section, index) => messages.Sections[section].Records[index].Text
            ?? throw OtherSummaryMissing("message." + section + "." + index);
    }

    private static InvalidDataException OtherSummaryMissing(string owner) => new("map.summary.other." + owner);
    private static T OtherSummaryNeed<T>(T? value, string owner) where T : struct => value ?? throw OtherSummaryMissing(owner);
    private static string OtherSummaryNumber(int value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed class OtherSummaryBuilder(int slot, int id, string layout, Func<int, int, string> data)
    {
        internal readonly Dictionary<int, string> Text = [];
        internal readonly Dictionary<int, int> Presets = [];
        internal readonly Dictionary<int, bool> Visible = [];
        internal readonly Dictionary<int, string> Missing = [];
        internal readonly Dictionary<int, MapCornerColors> Colors = [];
        internal readonly List<MapSummaryImage> Images = [];
        internal readonly List<MapSummaryPortrait> Portraits = [];
        internal readonly List<MapSummaryForceFlag> Flags = [];
        internal readonly List<MapSummaryChild> Children = [];
        internal readonly List<MapSummaryGauge> Gauges = [];
        internal string Data(int section, int index) => data(section, index);
        internal void Read(int node, Action query)
        {
            try { query(); }
            catch (InvalidDataException error) { Missing[node] = error.Message; Visible[node] = false; }
        }
        internal void Value(int node, Func<string> query) => Read(node, () => { Text[node] = query(); Presets[node] = 0; });
        internal void Caption(int node, int index) => Read(node, () => { Text[node] = Data(1, index); Presets[node] = 13; });
        internal MapSummaryPage Finish(bool? valid = true) => new(slot, id, layout, valid, Text)
        { TextPresets = Presets, Visibility = Visible, Images = Images, Portraits = Portraits,
            Colors = Colors, Children = Children, Gauges = Gauges, Flags = Flags, UnresolvedFields = Missing,
            UnresolvedOwner = Missing.Count == 0 ? null : string.Join("; ", Missing.Values.Distinct(StringComparer.Ordinal)) };
    }

    private void OtherSummaryFlag(GameState state, SourceWorldContext current, OtherSummaryBuilder page,
        int node, int forceId, int? letterNode = null)
    {
        var force = current.ReadForceTableReference(forceId) ?? throw OtherSummaryMissing("flag.force_pointer");
        bool valid = OtherSummaryNeed(current.ReadForceNativeValidity(force), "flag.force_valid");
        _mapInformationNames ??= ArmyFlagPaneNames.Load(_scenario);
        var appearance = ReadDetailForceAppearance(state, current, _mapInformationNames, force, valid, MapSpritePalettes.Create())
            ?? throw OtherSummaryMissing("flag.current_appearance");
        if (letterNode is int letter)
        {
            page.Colors[node] = new(appearance.Fill, appearance.Fill, appearance.Fill, appearance.Fill);
            page.Text[letter] = appearance.Glyph;
            page.Colors[letter] = new(appearance.Letter, appearance.Letter, appearance.Letter, appearance.Letter);
        }
        else page.Flags.Add(new(node, appearance));
    }

    private void OtherSummaryPortrait(GameState state, SourceWorldContext current,
        OtherSummaryBuilder page, int node, int physical)
    {
        page.Read(node, () =>
        {
            if (!OtherSummaryNeed(current.ReadPersonNativeValidity(physical), "portrait.person_valid"))
            { page.Visible[node] = false; return; }
            int identity = OtherSummaryNeed(current.ReadPersonIdentity(physical), "portrait.identity");
            ushort appearance = OtherSummaryNeed(current.ReadPersonPortraitAppearance(physical), "portrait.appearance");
            var route = ReadOfficerPortraitAppearance(state, current, _definitions, identity, appearance, ArmyMapUiSources.Create());
            if (route.Kind == OfficerPortraitRouteKind.Unresolved) throw new InvalidDataException(route.UnresolvedInput);
            page.Portraits.Add(new(node, route) { SourceNode = node }); page.Visible[node] = route.Kind != OfficerPortraitRouteKind.Hidden;
        });
    }

    private static int OtherSummaryPerson(SourceWorldContext current, int raw) => OtherSummaryNeed(
        (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
        "person.pointer");
    private static string OtherSummaryPersonName(GameState state, SourceWorldContext current, int physical)
    {
        if (!OtherSummaryNeed(current.ReadPersonNativeValidity(physical), "person.valid")) return "";
        int identity = OtherSummaryNeed(current.ReadPersonIdentity(physical), "person.identity");
        return state.Officers.GetValueOrDefault(identity)?.Name ?? throw OtherSummaryMissing("person.current_name");
    }

    internal MapSummaryPage QueryDistrictMapSummaryCurrent(GameState state, SourceWorldContext current, MapSummaryReceiver receiver)
    {
        int id = receiver.ReceiverId;
        var data = OtherSummaryText();
        var page = new OtherSummaryBuilder(receiver.Slot, id, "00_82_09_sum_gundan", data);
        bool? valid = null;
        page.Read(-1, () => valid = OtherSummaryNeed(current.ReadDistrictNativeValidity(
            current.ReadDistrictTableReference(id) ?? throw OtherSummaryMissing("district.pointer")), "district.valid"));
        if (valid != true) return page.Finish(valid);
        var values = new DistrictInformationCurrent(this, state, current);
        int? leader = null;
        page.Read(0xC, () => leader = OtherSummaryPerson(current, OtherSummaryNeed(current.ReadDistrictLeader(id), "district.leader")));
        if (leader is int physical) OtherSummaryPortrait(state, current, page, 0xC, physical);
        var header = new OtherSummaryBuilder(receiver.Slot, id, "00_82_05_sum_city_offset_01", data);
        header.Read(2, () =>
        {
            byte number = OtherSummaryNeed(current.ReadDistrictNumber(id), "district.number");
            var naming = ArmyMapUiSources.Create();
            header.Images.Add(new(2, naming.IconName(ArmyMapUiSources.DistrictIcon(number))));
        });
        header.Value(3, () => values.SummaryDistrictName(id, data));
        page.Children.Add(new("district-name", 7, header.Finish()));
        Child("leader", 0xD, "00_82_05_sum_city_offset_02", 0x214,
            () => leader is int person ? OtherSummaryPersonName(state, current, person) : throw OtherSummaryMissing("district.leader_pointer"));
        Child("cities", 0x11, "00_82_05_sum_city_offset_03", 0x207, () => OtherSummaryNumber(values.SummaryCityCount(id)), 4);
        Child("people", 0x16, "00_82_05_sum_city_offset_03", 0x218,
            () => OtherSummaryNumber(ReadDistrictInformationPersonCount(state, current, id, specialFilter: false)), 4);
        var policy = Child("policy", 0x1C, "00_82_05_sum_city_offset_02", 0x21F,
            () => { int raw = OtherSummaryNeed(current.ReadDistrictRawByte(id, 0x17), "district.policy"); return raw is >= 1 and <= 5 ? data(0, 0x175 + raw) : ""; });
        policy.Read(3, () => policy.Visible[3] = OtherSummaryNeed(current.ReadDistrictNumber(id), "district.number") != 1);
        Child("target", 0x20, "00_82_05_sum_city_offset_02", 0x299, () => values.SummaryTarget(id, data));
        Child("priority", 0x24, "00_82_05_sum_city_offset_02", 0x544,
            () => { int raw = OtherSummaryNeed(current.ReadDistrictRawByte(id, 0x1A), "district.priority"); return raw is >= 1 and <= 4 ? data(0, 0x17B + raw) : ""; });
        return page.Finish();

        OtherSummaryBuilder Child(string key, int mount, string layout, int caption, Func<string> read, int valueNode = 3)
        {
            var child = new OtherSummaryBuilder(receiver.Slot, id, layout, data);
            child.Caption(valueNode == 4 ? 3 : 2, caption); child.Value(valueNode, read);
            page.Children.Add(new(key, mount, child.Finish())); return child;
        }
    }

    private sealed partial class DistrictInformationCurrent
    {
        internal DistrictInformationCurrent(ArmyCampaignRuleAdapter rules, GameState state, SourceWorldContext current)
        { this.rules = rules; this.state = state; playerForceId = -1; captured = new(() => current); }
        internal int SummaryCityCount(int id) => CityCount(id);
        internal string SummaryDistrictName(int id, Func<int, int, string> data)
        {
            int person = Leader(id);
            if (!Need(Current.ReadPersonNativeValidity(person), "summary.leader_valid")) return "";
            int identity = Need(Current.ReadPersonIdentity(person), "summary.leader_identity");
            var owner = state.Officers.GetValueOrDefault(identity) ?? throw Missing("summary.leader_owner");
            return Special(id) ? identity is >= 5101 and <= 5200 or >= 2062 and <= 2066 ? owner.Name
                : owner.CurrentSurname ?? throw Missing("summary.leader_surname") : owner.Name + data(1, 0x350);
        }
        internal string SummaryTarget(int id, Func<int, int, string> data)
        {
            int kind = Raw(id, 0x19), raw = Raw(id, 0x18);
            if (kind == 2)
            {
                var home = BaseLookup.Find(raw, Current);
                return rules.UnitDetailBaseName(state, Current, home.Kind, Need(home.PhysicalSlot, "summary.target_base"));
            }
            if (kind == 7)
            {
                var force = Current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw Missing("summary.target_force");
                if (!Need(Current.ReadForceNativeValidity(force), "summary.target_force_valid")) return "";
                return new CampaignHistoryWriter(rules._repository, rules._scenario, rules.Seed.Language).FallbackName(state, Current,
                    force, (person, full) => full ? person.Name : person.CurrentSurname ?? throw Missing("summary.target_surname"));
            }
            return kind == 8 ? FormatSceneryPanelNumber(data(1, 0x494), Number11(Need(District(raw < 52 ? raw : 0).PhysicalSlot, "summary.target_district"))) : "";
        }
    }
}
