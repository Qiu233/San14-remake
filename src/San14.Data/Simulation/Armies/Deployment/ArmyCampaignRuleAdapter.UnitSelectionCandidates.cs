using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>92E9A0's world-side candidate projection for a mode3 drag.
    /// One current capture, live registry order, no camera, selection writes,
    /// Hex scan, simulated orders, or World copy. Call on drag start, not frame.</summary>
    public UnitSelectionCandidates QueryUnitSelectionCandidates(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var rows = new List<UnitSelectionCandidate>();
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var arena = state.ReadArmyLifecycle() ?? throw SelectionMissing("Army_lifecycle");
            var slots = arena.ReceiverSlots();
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw SelectionMissing("Scenario3A_Force_pointer");
            if (player.PhysicalSlot is null) throw SelectionMissing("Scenario3A_Force_receiver");

            //222FD0 captures manager60 head before Force::identity. Its
            //334740 filter compares Army's raw Force10 with that identity;
            //do not normalize the Army byte through a different Force lookup.
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Army)
                ?? throw SelectionMissing("Army60_descriptor");
            var node = current.ReadRegistryHead(descriptor) ?? throw SelectionMissing("Army60_head");
            int forceKey = Need(current.ReadForcePhysicalSlot(player), "begin_Force_identity");
            int ordinal = 0;
            while (true)
            {
                //223250 constructs a fresh end from this same live receiver.
                _ = Need(current.ReadForcePhysicalSlot(player), "end_Force_identity");
                if (!node.Identity.HasValue) break;
                if (ordinal >= slots.Length) throw SelectionMissing("Army60_receiver_limit");
                UnitSelectionCandidate? row = ReadRow(node, ordinal);
                if (row is not null) rows.Add(row);
                node = current.ReadRegistryNodeNext(node) ?? throw SelectionMissing("Army60_next");
                ordinal++;
            }
            return new(rows.AsReadOnly(), true, null);

            UnitSelectionCandidate? ReadRow(RegistryListNodeReference at, int sourceOrdinal)
            {
                int? physical = null, unitId = null, lookupHex = null, physicalHex = null, districtIdentity = null;
                UnitSelectionPartition? partition = null;
                try
                {
                    //334740 does not inspect payloads for a default Force key.
                    if (unchecked((uint)(forceKey - 1)) >= 51) return null;
                    var payload = current.ReadRegistryNodePayload(at);
                    if (payload is not { Kind: RegistryKind.Army }) throw SelectionMissing("Army60_payload");
                    physical = payload.PhysicalSlot ?? throw SelectionMissing("Army60_pointer");
                    if ((uint)physical.Value >= (uint)slots.Length) throw SelectionMissing("Army60_identity");
                    unitId = slots[physical.Value].ActiveUnitId;
                    var army = new ArmyUnitReference(physical);
                    //This current getter returns byte (not byte?). Missing
                    //Person/District inputs throw and retain an unknown row.
                    if (current.ReadArmyUnitRawForceSlot(army) != forceKey) return null;
                    if (Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current,
                        physical.Value), "Army233560")) return null;

                    ushort rawHex = Need(current.ReadArmyUnitRawHex2A(army), "Army2A");
                    lookupHex = rawHex < 48400 ? rawHex : 0;
                    var hex = current.ReadHexTableReference(lookupHex.Value)
                        ?? throw SelectionMissing("Hex_pointer");
                    physicalHex = hex.PhysicalSlot ?? throw SelectionMissing("Hex_receiver");
                    if ((uint)physicalHex.Value >= 48400) throw SelectionMissing("Hex_identity");

                    //22A9E0 and22D0D0 consume raw Hex1A independently. A
                    //resolved Object physical range would lose table aliases.
                    ushort link = Need(current.ReadHexObjectLink(hex), "Hex1A");
                    var city = current.ReadCityTableReference(link <= 51 ? link : 0)
                        ?? throw SelectionMissing("City_pointer");
                    if (Need(current.ReadBaseNativeValidity(city), "City_validity")) return null;
                    var gate = current.ReadGateTableReference(link is >= 52 and <= 61 ? link - 51 : 0)
                        ?? throw SelectionMissing("Gate_pointer");
                    if (Need(current.ReadBaseNativeValidity(gate), "Gate_validity")) return null;

                    //22BB10 follows the commander Person to District, using
                    //District0 for an actually invalid Person, not for unknown.
                    ushort commander = Need(current.ReadArmyUnitCommanderId(army), "Commander12");
                    var person = (commander <= 6000 ? current.ReadPersonLookupReference(commander)
                        : current.ReadTransferPersonTableReference(0)) ?? throw SelectionMissing("Person_pointer");
                    bool validPerson = person.PhysicalSlot is int personPhysical &&
                        Need(current.ReadPersonNativeValidity(personPhysical), "Person_validity");
                    byte districtRaw = validPerson
                        ? Need(current.ReadPersonDistrictSlot(person.PhysicalSlot!.Value), "Person_District") : (byte)0;
                    var district = current.ReadDistrictTableReference(districtRaw <= 51 ? districtRaw : 0)
                        ?? throw SelectionMissing("District_pointer");
                    int districtPhysical = district.PhysicalSlot ?? throw SelectionMissing("District_receiver");
                    districtIdentity = districtPhysical;
                    if (Need(current.ReadDistrictNumber(districtPhysical), "District11") != 1) return null;

                    if (unitId is not int id || !state.Units.TryGetValue(id, out var unit) ||
                        unit.NativeArmyOrigin?.NativeSlotIndex != physical.Value)
                        throw SelectionMissing("active_Army_domain_owner");
                    if (Need(unit.NativeMapUiByte13C, "Army13C") != 0) return null;
                    if (Need(ArmySavedCommandKnowledge.ReadControl(state, unit), "Army140") != 0) return null;
                    byte land = Need(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit), "Army1B");
                    partition = land is 19 or 20 ? UnitSelectionPartition.Transport : UnitSelectionPartition.Ordinary;
                    return Result(true, null);
                }
                catch (InvalidDataException error) { return Result(null, error.Message); }

                UnitSelectionCandidate Result(bool? included, string? missing)
                {
                    var row = new UnitSelectionCandidate(sourceOrdinal, unitId, physical, lookupHex,
                        physicalHex, partition, included, missing) { DistrictPhysicalSlot = districtIdentity };
                    if (included != true || unitId is not int id || physical is not int groupPhysical || !state.Units.TryGetValue(id, out var selected))
                        return row with { RowUnresolvedInput = missing };
                    var fields = ReadSelectionGroupingFields(state, current, selected, groupPhysical);
                    return row with { ActionCode = fields.Action, TargetKind = fields.Kind,
                        TargetIdentity = fields.Identity, RowUnresolvedInput = fields.Missing };
                }
            }
        }
        catch (InvalidDataException error) { return new(rows.AsReadOnly(), false, error.Message); }

        static T Need<T>(T? value, string field) where T : struct => value ?? throw SelectionMissing(field);
    }

    /// <summary>Read existing rows for initial selection and actual Ctrl-click
    /// hits. This deliberately has none of92E9A0's Box-specific rejection
    /// filters. The District argument is the real page owner captured on entry,
    /// not an inferred first/player District.</summary>
    public UnitSelectionRows QueryUnitSelectionRows(GameState state, IReadOnlyList<int> unitIds,
        int? sourceDistrictPhysicalSlot = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(unitIds);
        if (unitIds.Count == 0) return new(Array.Empty<UnitSelectionRow>(), null);
        if (unitIds.Count > 30 || unitIds.Distinct().Count() != unitIds.Count)
            return new(Array.Empty<UnitSelectionRow>(), SelectionMissing("row_selection").Message);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var rows = unitIds.Select(id => ReadSelectionRow(state, current, id, sourceDistrictPhysicalSlot)).ToArray();
            return new(Array.AsReadOnly(rows), null);
        }
        catch (InvalidDataException error) { return new(Array.Empty<UnitSelectionRow>(), error.Message); }
    }

    private UnitSelectionRow ReadSelectionRow(GameState state, SourceWorldContext current,
        int unitId, int? sourceDistrictPhysicalSlot)
    {
        var missing = new List<string>();
        T? Value<T>(Func<T?> read, string field) where T : struct
        {
            try { return read() ?? throw SelectionMissing(field); }
            catch (InvalidDataException error) { missing.Add(error.Message); return null; }
        }
        if (!state.Units.TryGetValue(unitId, out var unit))
            return new(unitId, null, null, null, null, null, null, null, null, false, false, null);
        int? physical = unit.NativeArmyOrigin?.NativeSlotIndex;
        var arena = state.ReadArmyLifecycle();
        if (physical is not int at || arena is null || (uint)at >= (uint)arena.ReceiverSlots().Length ||
            arena.ReceiverSlots()[at].ActiveUnitId != unitId)
            return new(unitId, physical, null, null, null, null, null, null, null, null, null,
                SelectionMissing("row_active_owner").Message);
        var army = new ArmyUnitReference(at);
        bool? valid = Value(() => current.ReadArmyUnitNativeValidity(army), "row_Army_validity");
        ushort? commander = Value(() => current.ReadArmyUnitCommanderId(army), "row_Commander12");
        if (commander.HasValue && commander.Value != unit.OfficerId)
            return new(unitId, physical, null, null, null, null, null, null, null, null, null,
                SelectionMissing("row_commander_identity").Message);
        ushort? rawHex = Value(() => current.ReadArmyUnitRawHex2A(army), "row_Army2A");
        int? lookupHex = rawHex.HasValue ? rawHex.Value < 48400 ? rawHex.Value : 0 : null;
        int? hex = lookupHex is int lookup ? Value<int>(() =>
        {
            var pointer = current.ReadHexTableReference(lookup) ?? throw SelectionMissing("row_Hex_pointer");
            return pointer.PhysicalSlot;
        }, "row_Hex_receiver") : null;
        byte? land = Value(() => ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit), "row_Army1B");
        UnitSelectionPartition? partition = land.HasValue ? land is 19 or 20
            ? UnitSelectionPartition.Transport : UnitSelectionPartition.Ordinary : null;
        var fields = ReadSelectionGroupingFields(state, current, unit, at);
        if (fields.Missing is not null) missing.Add(fields.Missing);
        int? district = commander.HasValue ? Value<int>(() =>
        {
            var person = (commander.Value <= 6000 ? current.ReadPersonLookupReference(commander.Value)
                : current.ReadTransferPersonTableReference(0)) ?? throw SelectionMissing("row_Person_pointer");
            bool personValid = person.PhysicalSlot is int personAt
                ? current.ReadPersonNativeValidity(personAt) ?? throw SelectionMissing("row_Person_validity") : false;
            int raw = personValid ? current.ReadPersonDistrictSlot(person.PhysicalSlot!.Value)
                ?? throw SelectionMissing("row_Person_District") : 0;
            return (current.ReadDistrictTableReference(raw <= 51 ? raw : 0)
                ?? throw SelectionMissing("row_District_pointer")).PhysicalSlot;
        }, "row_District_receiver") : null;

        bool? click = null;
        string? clickMissing = null;
        if (valid == false) click = false;
        else if (valid is null) clickMissing = SelectionMissing("click_Army_validity").Message;
        else if (!sourceDistrictPhysicalSlot.HasValue) clickMissing = SelectionMissing("click_page_District_owner").Message;
        else if (!district.HasValue) clickMissing = SelectionMissing("click_District_pointer").Message;
        else if (district != sourceDistrictPhysicalSlot) click = false;
        else
        {
            var values = state.NativeArmySimulation?.Parameters.Int32Values;
            if (values is null || values.GetValueOrDefault(TacticRemakeCurrentBattleControl.VersionKey) != 1 ||
                !values.TryGetValue(TacticRemakeCurrentBattleControl.HeaderKey, out int tutorial))
                clickMissing = SelectionMissing("click_tutorial_owner").Message;
            else
            {
                try { click = TutorialTargetQualification.AllowsArmy(ReadTutorialControlState(state), at, fields.Action); }
                catch (InvalidDataException error) { clickMissing = error.Message; }
            }
        }
        return new(unitId, physical, lookupHex, hex, partition, fields.Action, fields.Kind,
            fields.Identity, district, valid, click, missing.Count == 0 ? null : string.Join("; ", missing))
            { ClickSelectionUnresolvedInput = clickMissing };
    }

    private static (byte? Action, byte? Kind, ushort? Identity, string? Missing) ReadSelectionGroupingFields(
        GameState state, SourceWorldContext current, UnitState unit, int physical)
    {
        //Action37 already has a dedicated current source owner. Target38/3A
        //use the one directory retained by this same captured current context.
        //Never re-encode a coarse MilitaryPlan or construct a directory per row.
        byte? action = ArmyConfigurationSourceAdapter.ReadAction(state, unit);
        var missing = new List<string>();
        if (!action.HasValue) missing.Add(SelectionMissing("row_Action37").Message);
        byte? kind = null; ushort? identity = null;
        try
        {
            var fields = current.ReadArmyLifecycleTargetFields(physical);
            kind = fields?.Kind38; identity = fields?.Identity3A;
            if (!kind.HasValue) missing.Add(SelectionMissing("row_Target38").Message);
            if (!identity.HasValue) missing.Add(SelectionMissing("row_Target3A").Message);
        }
        catch (InvalidDataException error) { missing.Add(error.Message); }
        return (action, kind, identity, missing.Count == 0 ? null : string.Join("; ", missing));
    }

    private static InvalidDataException SelectionMissing(string field) =>
        new("unit.selection_candidates." + field);
}
