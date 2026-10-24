using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Actual DA5D0 row and D2770(param0) saved-route seeds in the
    /// caller's real page-row order. Initial rows come from the map's existing
    /// selected-Army vector; later retained rows are requested once on append.
    /// This does not scan a Force registry, filter by current selection, sort
    /// rows, find routes or write the world.</summary>
    public UnitSelectionPageRows QueryUnitSelectionPageRows(GameState state,
        IReadOnlyList<int> orderedPageUnitIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(orderedPageUnitIds);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            return new(Array.AsReadOnly(orderedPageUnitIds.Select(ReadRow).ToArray()), null);

            UnitSelectionPageRow ReadRow(int id, int ordinal)
            {
                var missing = new List<string>();
                UnitSelectionPartition? partition = null;
                UnitSelectionGroupKey? key = null;
                UnitSelectionSavedGroup? group = null;
                IReadOnlyList<ushort>? words = null;
                byte? kind = null; ushort? identity = null;
                try
                {
                    if (!state.Units.TryGetValue(id, out var unit)) throw SelectionPageMissing("unit-owner");
                    int physical = unit.NativeArmyOrigin?.NativeSlotIndex ?? throw SelectionPageMissing("army-receiver");
                    var arena = state.ReadArmyLifecycle() ?? throw SelectionPageMissing("lifecycle");
                    var slots = arena.ReceiverSlots();
                    if ((uint)physical >= (uint)slots.Length || slots[physical].ActiveUnitId != id)
                        throw SelectionPageMissing("army-active-owner");
                    var army = new ArmyUnitReference(physical);
                    byte status = SelectionPageKnown(current.ReadArmyReturnUnitStatus10(army), "army-status10");
                    partition = status switch { 1 => UnitSelectionPartition.Ordinary, 2 => UnitSelectionPartition.Transport,
                        _ => throw SelectionPageMissing("row-kind-not-ordinary-or-transport") };
                    var fields = ReadSelectionGroupingFields(state, current, unit, physical);
                    kind = fields.Kind; identity = fields.Identity;
                    if (fields.Missing is not null) missing.Add(fields.Missing);
                    try
                    {
                        byte action = SelectionPageKnown(fields.Action, "row-action");
                        if (status == 1)
                            key = new(action, SelectionPageKnown(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit), "row-land"),
                                SelectionPageKnown(ArmyConfigurationSourceAdapter.ReadNaval(state, unit), "row-naval"));
                        else
                        {
                            var cargo = current.ReadArmyReturnResourceFields(army) ?? throw SelectionPageMissing("row-cargo-owner");
                            key = new(action, SelectionPageKnown(cargo.Gold5C, "row-cargo-gold"),
                                SelectionPageKnown(cargo.Supplies60, "row-cargo-food"));
                        }
                    }
                    catch (InvalidDataException error) { missing.Add(error.Message); }
                    try
                    {
                        var raw = status == 1 ? ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(unit.March)
                            : TransportRouteSourceAdapter.ReadWaypoints(state, unit);
                        if (raw is null) throw SelectionPageMissing("row-five-waypoints");
                        words = Array.AsReadOnly(raw);
                    }
                    catch (InvalidDataException error) { missing.Add(error.Message); }
                    try
                    {
                        if (key is null) throw SelectionPageMissing("saved-group-row-key");
                        var currentHex = ReadSelectionPagePosition(current, unit.OfficerId);
                        var points = new List<int?>();
                        int activeIndex = 0; bool hasFinal = false;
                        if (currentHex?.PhysicalSlot is int at)
                        {
                            points.Add(at);
                            if (words is null) throw SelectionPageMissing("saved-group-five-waypoints");
                            foreach (ushort raw in words)
                            {
                                if (raw >= 48400) break;
                                var waypoint = current.ReadHexTableReference(raw) ?? throw SelectionPageMissing("saved-waypoint-pointer");
                                points.Add(waypoint.PhysicalSlot); activeIndex++;
                            }
                            var final = ReadSelectionPageTarget(current,
                                SelectionPageKnown(kind, "saved-target-kind"), SelectionPageKnown(identity, "saved-target-identity"));
                            if (final?.PhysicalSlot is int target) { points.Add(target); hasFinal = true; }
                        }
                        group = new(id, key, Array.AsReadOnly(new[] { id }), points.AsReadOnly(), activeIndex, hasFinal);
                    }
                    catch (InvalidDataException error) { missing.Add(error.Message); }
                }
                catch (InvalidDataException error) { missing.Add(error.Message); }
                return new(ordinal, id, partition, key, group, words, kind, identity,
                    missing.Count == 0 ? null : string.Join("; ", missing));
            }
        }
        catch (InvalidDataException error)
        {
            return new(Array.AsReadOnly(orderedPageUnitIds.Select((id, ordinal) =>
                new UnitSelectionPageRow(ordinal, id, null, null, null, null, null, null, error.Message)).ToArray()), error.Message);
        }
    }

    //7CDA50 resolves Person E4, not domain HomeCity or Army's coarse position.
    private static HexReference? ReadSelectionPagePosition(SourceWorldContext current, int officerId)
    {
        if (officerId < 0) throw SelectionPageMissing("position-person-index");
        var person = current.ReadPersonLookupReference(officerId <= 6000 ? officerId : 0)
            ?? throw SelectionPageMissing("position-person-pointer");
        if (person.PhysicalSlot is not int physical ||
            !SelectionPageKnown(current.ReadPersonNativeValidity(physical), "position-person-validity")) return null;
        var location = PersonPositionRepair.DecodeSecondLocation(
            SelectionPageKnown(current.ReadPersonSecondLocation(physical), "position-person-E4"));
        if (location.Kind == 27)
        {
            var army = current.ReadArmyUnitTableReference(location.Index <= 500 ? location.Index : 0)
                ?? throw SelectionPageMissing("position-army-pointer");
            if (!SelectionPageKnown(current.ReadArmyUnitNativeValidity(army), "position-army-validity")) return null;
            int raw = SelectionPageKnown(current.ReadArmyUnitRawHex2A(army), "position-army2A");
            return current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw SelectionPageMissing("position-Hex-pointer");
        }
        BaseReference? basis = location.Kind switch
        {
            5 => current.ReadCityTableReference(location.Index),
            6 => current.ReadGateTableReference(location.Index),
            _ => new(BaseKind.City, null)
        };
        if (basis is null) throw SelectionPageMissing("position-Base-pointer");
        return SelectionPageKnown(current.ReadBaseNativeValidity(basis), "position-Base-validity")
            ? CurrentGateMapQueries.FindHex(basis, current) : null;
    }

    //7CE220 ->2AE470, followed by native CHex validity. An unsupported or
    //known-invalid target appends nothing; an unavailable field stays unknown.
    private static HexReference? ReadSelectionPageTarget(SourceWorldContext current, byte kind, ushort identity)
    {
        if (kind == 9) return Hex(identity);
        if (kind is 5 or 6)
        {
            var basis = kind == 5 ? current.ReadCityTableReference(identity <= 51 ? identity : 0)
                : current.ReadGateTableReference(identity <= 10 ? identity : 0);
            if (basis is null) throw SelectionPageMissing("target-Base-pointer");
            return SelectionPageKnown(current.ReadBaseNativeValidity(basis), "target-Base-validity")
                ? CurrentGateMapQueries.FindHex(basis, current) : null;
        }
        if (kind == 11)
        {
            var obj = current.ReadObjectTableReference(identity <= 3000 ? identity : 0)
                ?? throw SelectionPageMissing("target-Object-pointer");
            return SelectionPageKnown(current.ReadObjectNativeValidity(obj), "target-Object-validity")
                ? Hex(SelectionPageKnown(current.ReadObjectHexIndex(obj), "target-Object10")) : null;
        }
        if (kind == 27)
        {
            var army = current.ReadArmyUnitTableReference(identity <= 500 ? identity : 0)
                ?? throw SelectionPageMissing("target-Army-pointer");
            return SelectionPageKnown(current.ReadArmyUnitNativeValidity(army), "target-Army-validity")
                ? Hex(SelectionPageKnown(current.ReadArmyUnitRawHex2A(army), "target-Army2A")) : null;
        }
        return null;
        HexReference Hex(int raw) => current.ReadHexTableReference(raw < 48400 ? raw : 0)
            ?? throw SelectionPageMissing("target-Hex-pointer");
    }

    private static T SelectionPageKnown<T>(T? value, string name) where T : struct =>
        value ?? throw SelectionPageMissing(name);
    private static InvalidDataException SelectionPageMissing(string name) => new("unit.selection_page." + name);
}
