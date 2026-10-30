using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal MapSummaryPage QueryArmyGroupMapSummaryCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver)
    {
        var children = new List<MapSummaryChild>(); var visible = new Dictionary<int, bool>();
        var result = new MapSummaryPage(receiver.Slot, receiver.ReceiverId, "00_82_11_sum_gunzei", null,
            new Dictionary<int, string>()) { Children = children, Visibility = visible };
        try
        {
            int physical = receiver.ReceiverId;
            if (!SummaryArmyNeed(current.ReadArmyUnitNativeValidity(new(physical)), "group.validity"))
                return result with { ReceiverValid = false };
            result = result with { ReceiverValid = true };
            int index = Math.Clamp(receiver.SourceZoomBand ?? throw SummaryArmyMissing("group.source_zoom_band"), 3, 5) - 3;
            var unit = SummaryArmyUnit(state, physical);
            var queries = MapArmyGroupingQueries(state, current);
            int district = SummaryArmyNeed(queries.ReadDistrict22BC70(physical), "group.district");
            var districtPointer = current.ReadDistrictTableReference(district <= 51 ? district : 0)
                ?? throw SummaryArmyMissing("group.district_pointer");
            var members = new List<(int Physical, ulong Key)>();
            if (SummaryArmyNeed(current.ReadDistrictNativeValidity(districtPointer), "group.district_validity"))
            {
                //222E60's DistrictArmyUnitIterator traverses the actual
                //manager60 list and filters22BB10; it is not the nearby-Hex
                //member list used by the map's aggregate label producer.
                foreach (var reference in current.ReadRegistryReferences(RegistryKind.Army) ?? throw SummaryArmyMissing("group.manager60"))
                {
                    if (reference is not { Kind: RegistryKind.Army, PhysicalSlot: int at }) throw SummaryArmyMissing("group.manager_entry");
                    if (at == physical) continue;
                    if (SummaryArmyNeed(queries.ReadDistrict22BC70(at), "group.member_district") != district) continue;
                    if (SummaryArmyNeed(queries.IsExcluded233560(at), "group.member_excluded")) continue;
                    if (SummaryArmyNeed(ReadMapSummaryGroupRootCurrent(state, current, at, index + 3), "group.member_root") != physical) continue;
                    members.Add((at, SummaryArmyGroupSortKey(state, current, at)));
                    if (members.Count == 100) break;
                }
            }
            members = members.OrderByDescending(value => value.Key).ToList();
            var rules = new TacticRemakeCurrentContext(current, _catalog, _map, _terrain, _usage, Revision);
            var details = SummaryArmyScalars(state, current, unit, rules);
            var force = current.ReadArmyUnitForceReference(new(physical)) ?? throw SummaryArmyMissing("group.force");
            int number = SummaryArmyNeed(current.ReadDistrictNativeValidity(districtPointer), "group.header_district_validity")
                ? SummaryArmyNeed(current.ReadDistrictNumber(SummaryArmyNeed(districtPointer.PhysicalSlot, "group.district_receiver")), "group.district_number") : 0;
            byte mark = unit.NativeMapUiByte13C ?? state.ReadArmyLifecycle()?.ReceiverSlots()[physical].MapUiByte13C
                ?? throw SummaryArmyMissing("group.Army13C");
            children.Add(new("identity", 103, SummaryCityIdentity(current, receiver, force,
                ReadHistoryArmyName(state, current, new(physical)), number, mark is >= 1 and <= 50)));
            children.Add(new("formation", 12, SummaryArmyFormation(state, current, receiver, unit, details)));
            children.Add(new("troops", 29, SummaryCityNumber(receiver, 0x19C, 0x20E,
                details.Resources.ReadyTroops?.ToString(CultureInfo.InvariantCulture))));
            children.Add(new("morale", 24, SummaryCityGauge(receiver, 0x1E2, 0x225,
                details.Resources.CurrentMorale?.ToString(CultureInfo.InvariantCulture),
                details.Resources.CurrentMorale, details.Resources.MaximumMorale)));
            var runtime = unit.NativeTacticRuntime ?? throw SummaryArmyMissing("group.runtime");
            var statuses = new Dictionary<int, int?> { [3] = runtime.Abnormal25 != 0 ? 12 : 45,
                [4] = runtime.Abnormal23 != 0 ? 12 : 45, [5] = runtime.Abnormal24 != 0 ? 12 : 45,
                [6] = ReadUnitDetailSupplyPalette(state, current, unit), [7] = SummaryArmyFire(state, unit) };
            children.Add(new("status", 36, SummaryCityStatus(receiver, statuses)));
            int total = unit.Troops;
            foreach (var member in members) total = unchecked(total + SummaryArmyUnit(state, member.Physical).Troops);
            children.Add(new("total", 8, SummaryCityPair(receiver, 0x3C2, total.ToString(CultureInfo.InvariantCulture))));
            int[] mounts = [44, 58, 72, 86];
            for (int row = 0; row < 4; row++)
            {
                visible[mounts[row]] = row < members.Count;
                if (row < members.Count) children.Add(new("member" + row, mounts[row],
                    SummaryArmyGroupRow(state, current, receiver with { ReceiverId = members[row].Physical }, rules)));
            }
            visible[100] = members.Count >= 5;
            if (members.Count >= 5) children.Add(new("overflow", 100, SummaryShuOverflow(receiver, members.Count - 4)));
            return result;
        }
        catch (InvalidDataException error) { return result with { UnresolvedOwner = error.Message }; }
    }

    /// <summary>One actual derived1E8/1EA/1EC field. An omitted saved root
    /// remains unknown; this never runs grouping or captures a whole frame.</summary>
    internal int? ReadMapSummaryGroupRootCurrent(GameState state, SourceWorldContext current, int slot, int band)
    {
        if (slot is < 0 or > 500 || band is < 3 or > 5) throw SummaryArmyMissing("group.root_receiver");
        return _mapArmyGrouping.ReadRoot(slot, band - 3) is ushort raw ? raw <= 500 ? raw : 0 : null;
    }

    private MapSummaryPage SummaryArmyGroupRow(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver, TacticRemakeCurrentContext rules)
    {
        var unit = SummaryArmyUnit(state, receiver.ReceiverId);
        var details = SummaryArmyScalars(state, current, unit, rules);
        var text = new Dictionary<int, string>(); var images = new List<MapSummaryImage>();
        var portraits = new List<MapSummaryPortrait>(); var colors = new Dictionary<int, MapCornerColors>();
        var visible = new Dictionary<int, bool>();
        var bars = new List<MapSummaryBar>(); var issues = new Dictionary<int, string>();
        var indicators = new List<MapSummaryIndicator>();
        var result = new MapSummaryPage(receiver.Slot, receiver.ReceiverId, "00_82_11_sum_gunzei_offset_00", true, text)
            { Images = images, Portraits = portraits, Colors = colors, Bars = bars, Indicators = indicators, UnresolvedFields = issues, Visibility = visible };
        try
        {
            int person = SummaryArmyCommander(current, receiver.ReceiverId);
            ushort appearance = SummaryArmyNeed(current.ReadPersonPortraitAppearance(person), "group.row_portrait");
            portraits.Add(new(4, ReadOfficerPortraitAppearance(state, current, _definitions, unit.OfficerId,
                appearance, ArmyMapUiSources.Create())));
            var formation = details.Formation?.Selected ?? throw SummaryArmyMissing("group.row_formation");
            images.Add(new(5, ArmyMapUiSources.Create().IconName(formation.Icon)));
            colors[5] = MapSpritePalettes.Create()[21];
            var indicatorFrame = QueryMapSummaryIndicatorStates(state, current);
            if (indicatorFrame.ArmyStates.TryGetValue(receiver.ReceiverId, out var states))
                indicators.Add(new(6, receiver.ReceiverId, states));
            else if (indicatorFrame.UnresolvedArmies.TryGetValue(receiver.ReceiverId, out var missing))
            { issues[6] = missing; visible[6] = false; }
            else
            { issues[6] = "map-summary.army.group.indicator_unpublished"; visible[6] = false; }
            text[12] = ReadHistoryArmyName(state, current, new(receiver.ReceiverId));
            text[13] = unit.Troops.ToString(CultureInfo.InvariantCulture);
            if (details.Resources.CurrentMorale is int morale && details.Resources.MaximumMorale is int maximum)
                bars.Add(new(11, morale, maximum, 3));
            else issues[11] = "map-summary.army.group.row_morale";
        }
        catch (InvalidDataException error)
        {
            issues[4] = error.Message;
            if (portraits.Count == 0) visible[4] = false;
        }
        return result;
    }

    private ulong SummaryArmyGroupSortKey(GameState state, SourceWorldContext current, int physical)
    {
        //915140's descending packed key. The source uses role/office and
        //current leadership, not display name or the military-group leader.
        int person = SummaryArmyCommander(current, physical);
        var force = current.ReadPersonForce(person) ?? throw SummaryArmyMissing("group.sort_force");
        int rank = SummaryArmyNeed(current.ReadMoraleWaveForceRank47(SummaryArmyNeed(force.PhysicalSlot, "group.sort_force_receiver")), "group.sort_rank");
        byte status = SummaryArmyNeed(current.ReadPersonStatus(person), "group.sort_status");
        byte office = SummaryArmyNeed(OwnedArmyMoraleWaveInputs.ReadPersonPostE7(state, current, person), "group.sort_office");
        int identity = SummaryArmyNeed(current.ReadPersonIdentity(person), "group.sort_identity");
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        uint leadership = unchecked((uint)SummaryArmyNeed(abilities.ResolveDimension(state, identity, 0), "group.sort_leadership"));
        var unit = SummaryArmyUnit(state, physical);
        bool supplied = SummaryArmySupplied(current, unit, person, physical);
        ulong high = unchecked((uint)(-rank - 4)) & 15;
        if (!supplied) high |= 16;
        high = ((high * 2 | (status == 1 ? 1ul : 0)) * 2 | (status == 2 ? 1ul : 0)) * 2 | (status == 3 ? 1ul : 0);
        high = (unchecked((uint)~office) & 127) | (high << 7);
        return ((high << 8 | (leadership & 255)) << 16) | (unchecked((uint)(6000 - identity)) & 65535);
    }

    private static bool SummaryArmySupplied(SourceWorldContext current, UnitState unit, int person, int physical)
    {
        int home = SummaryArmyNeed(current.ReadPersonNativeValidity(person), "group.sort_person_validity")
            ? SummaryArmyNeed(current.ReadPersonFirstLocation(person), "group.sort_home") : 0;
        var basis = home is >= 52 and <= 61 ? current.ReadGateTableReference(home - 51)
            : current.ReadCityTableReference(home is >= 1 and <= 51 ? home : 0);
        if (basis is null) throw SummaryArmyMissing("group.sort_home_pointer");
        if (SummaryArmyNeed(current.ReadBaseNativeValidity(basis), "group.sort_home_validity") &&
            current.ReadBaseCurrentHexIndex(basis) == SummaryArmyNeed(current.ReadArmyUnitRawHex2A(new(physical)), "group.sort_hex")) return true;
        return ((unit.NativeTacticRuntime ?? throw SummaryArmyMissing("group.sort_runtime")).Flags26 & 0x42) == 0x42;
    }
}
