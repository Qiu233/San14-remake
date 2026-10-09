using San14.Core;

namespace San14.Data;

/// <summary>Initializes the military owners, assigns creator groups and
/// consumes ordered deferred retirements without presentation receivers.</summary>
public static class OriginalOwnedArmyLifecycleAuxiliaries
{
    internal static NativeArmyLifecycleState InstallFresh(NativeArmyLifecycleState arena,
        OriginalEventInitializationContext current, WorldIndexContext pool)
    {
        //220A40/220A70/227F00/2269E0 constructors. These zero-byte scenario
        //sections do not supply additional initial field stores.
        for (int group = 0; group < 501; group++) pool.CautionGroupHandle(group);
        foreach (var kind in new[] { OriginalRegistryKind.CautionTroops, OriginalRegistryKind.Table81B50 })
        {
            var descriptor = pool.AllocateDescriptor();
            current.SetRegistryDescriptor(kind, new(descriptor.Identity));
        }
        //1D7DD0 command-manager18 ptr_list constructor; currently implemented
        //owned orders do not allocate original CCmdAction receivers.
        pool.CreateAssociatedTaskIndex();
        return arena with { Indexes = pool.ExportOwnedState(),
            ArmyGroups = new() { ResetLeader = ArmyGroupSourceAdapter.Officer(current, 0) },
            ArmyCautions = new() };
    }

    internal static NativeArmyLifecycleState? PrepareCreation(NativeArmyLifecycleState arena,
        GameState state, UnitState created, OriginalEventInitializationContext current, WorldIndexContext pool)
    {
        if (state.PendingCombatSettlements is null || arena.ArmyGroups is not { } groups) return null;
        var indexes = pool.ExportOwnedState();
        var roster = ArmyGroupSourceAdapter.Roster(indexes);
        var excluded = ArmyGroupSourceAdapter.Excluded(state, arena, current, roster);
        int? playerDistrict = CurrentPlayerPrimaryDistrict(state, current);
        if (playerDistrict is null) return null;
        var admitted = ArmyGroupRules.AdmitCreation(groups, created.Id, roster, excluded,
            id => ArmyGroupSourceAdapter.Commander(arena, current, id), playerDistrict == 0 ? null : playerDistrict,
            Enumerable.Range(1, 500));
        return admitted is null ? null : arena with { Indexes = indexes, ArmyGroups = admitted };
    }

    /// <summary>Consumes the saved pending order, all caution containers,
    /// reset and manager unlink. Remaining settlements supply current group
    /// exclusions; retirement does not consume the settlement queue.</summary>
    public static GameState? ConsumePending(GameState state, OriginalOwnedArmyCurrentDefinitions definitions,
        Action<int>? committedReset = null)
    {
        var arena = state.NativeArmyLifecycle;
        if (arena is null || arena.ArmyGroups is not { } groups || arena.ArmyCautions is not { } cautions || state.PendingCombatSettlements is null || arena.Slots.Length != 501) return null;
        var current = OriginalOwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions);
        var pool = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
        current.RestoreOwnedArmyReturnBindings(arena);
        RestoreHeaders(current, arena.Slots);
        var pendingOrder = Chain(current, current.ReadArmyReturnPending70Descriptor());
        if (pendingOrder.Any(x => x.Kind != OriginalRegistryKind.Army || x.PhysicalSlot is not (>= 1 and <= 500))) return null;
        if (pendingOrder.Any(x => arena.Slots[x.PhysicalSlot!.Value].ActiveUnitId is not null ||
            arena.Slots[x.PhysicalSlot.Value].PendingImage is null)) return null;
        var cautionMembers = arena.Indexes.CautionGroupMembers;
        var slots = arena.Slots.ToArray();
        var armyFlags150 = arena.ArmyFlags150?.ToArray();
        var armyTargets38 = arena.Targets38?.ToArray();
        try
        {
            foreach (var receiver in pendingOrder)
            {
                int army = receiver.PhysicalSlot!.Value;
                int unitId = ArmyGroupSourceAdapter.UnitId(arena, army);
                var retiredCautions = ArmyCautionRules.RetireArmy(cautions, cautionMembers, unitId)
                    ?? throw new InvalidDataException("Current caution retirement is unavailable.");
                cautions = retiredCautions.Cautions;
                cautionMembers = retiredCautions.GroupMembers;
                // Reset relation facts before removing every registry occurrence.
                ArmyCautionSourceAdapter.InstallEligibility(current, cautions);
                foreach (int relationId in retiredCautions.RemovedRelationIds)
                    RemoveAll(current, current.ReadRegistryDescriptor(OriginalRegistryKind.Table81B50),
                        new(OriginalRegistryKind.Table81B50, relationId));
                foreach (int groupId in retiredCautions.ChangedGroupIds)
                    pool.ReplaceCautionGroupMembers(groupId, cautionMembers[groupId]!);
                var roster = ArmyGroupSourceAdapter.Roster(arena, current);
                var currentState = state with { NativeArmyLifecycle = arena with { Slots = slots } };
                var excluded = ArmyGroupSourceAdapter.Excluded(currentState, arena, current, roster);
                groups = ArmyGroupRules.UpdateLeaderBeforeRetirement(groups, unitId, roster, excluded,
                    id => ArmyGroupSourceAdapter.Commander(arena, current, id))
                    ?? throw new InvalidDataException("Current group retirement is unavailable.");
                foreach (var descriptor in pool.TraitHandles(army))
                    current.ClearTemporaryHexList(new(descriptor.Identity));
                //2269E0 resets numeric fields and clears all nine existing
                //descriptor chains; it does not replace their identities.
                slots[army] = slots[army] with { Commander12 = 0, Status10 = 0, Priority14 = 0, ActiveUnitId = null,
                    GroupAssignmentReady = true,
                    PendingImage = null, Gold5C = 0, Supplies60 = 0, AuxiliaryAC = new ushort[36], AuxiliaryF4 = new ushort[36],
                    OverrideTarget = new(0xFF, 0), Direction36 = OriginalArmyDirectionSource.ResetDirection36,
                    MapUiByte13C = 0, CommandControl140 = 0, RouteHold56 = 0, RouteGoal1F8 = 0xBD10 };
                current.SetArmyUnitCommanderId(army, 0); current.SetArmyUnitStatusFlag10(army, 0);
                if (armyFlags150 is not null) armyFlags150[army] = 0; //2269E0 byte150 explicit reset.
                if (armyTargets38 is not null) armyTargets38[army] = new(0xFF, 0); //2269E0 target reset.
                _ = OriginalArmyMovementManagerLifecycle.Remove(receiver, current);
                pool.RemoveArmyIdentity(army);
                roster = ArmyGroupSourceAdapter.Roster(arena, current);
                excluded = ArmyGroupSourceAdapter.Excluded(currentState, arena, current, roster);
                groups = ArmyGroupRules.RemoveUnitAndResetEmptyGroup(groups, unitId, roster, excluded)
                    ?? throw new InvalidDataException("Current group retirement membership is unavailable.");
            }
            current.ClearTemporaryHexList(new(current.ReadArmyReturnPending70Descriptor()!.Identity));
            var result = state with { NativeArmyLifecycle = arena with { Indexes = pool.ExportOwnedState(), Slots = slots, ArmyFlags150 = armyFlags150, Targets38 = armyTargets38,
                ArmyGroups = groups, ArmyCautions = cautions } };
            foreach (var receiver in pendingOrder) committedReset?.Invoke(receiver.PhysicalSlot!.Value);
            return result;
        }
        catch (InvalidDataException) { return null; }
    }

    internal static void RestoreCurrent(OriginalEventInitializationContext current, NativeArmyLifecycleState arena)
    {
        if (arena.ArmyGroups is not null) ArmyGroupSourceAdapter.RestoreAssignments(current, arena);
        if (arena.ArmyCautions is { } cautions) ArmyCautionSourceAdapter.Restore(current, cautions);
    }

    internal static int? CurrentPlayerPrimaryDistrict(GameState state, OriginalEventInitializationContext current)
    {
        var player = OriginalOwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        if (player?.PhysicalSlot is not int force) return null;
        //22E930 tests the Force physical getter60, not Force header validity.
        ushort ruler;
        if (force == 0) ruler = 5014;
        else if (current.ReadForceRecord(force) is { } record) ruler = record.RawRuler10;
        else return null;
        foreach (var receiver in Chain(current, new(current.ReadRegistryDescriptor(OriginalRegistryKind.District)!.Identity)))
        {
            int district = receiver.PhysicalSlot!.Value;
            if (force is < 1 or > 51 || current.ReadDistrictRawByte(district, 0x10) != force) continue;
            if (current.ReadDistrictLeader(district) == ruler || current.ReadDistrictRawByte(district, 0x11) == 1) return district;
        }
        return 0;
    }
    private static void RemoveAll(OriginalEventInitializationContext current, OriginalRegistryListHandle? descriptor, OriginalRegistryObjectReference target)
    {
        var handle = descriptor is null ? throw new InvalidDataException("Current cleanup container descriptor is unknown.") : descriptor;
        var node = current.ReadRegistryHead(handle) ?? throw new InvalidDataException("Current cleanup container head is unknown.");
        while (node.Identity is not null)
        {
            var next = current.ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current cleanup container next is unknown.");
            var payload = current.ReadRegistryNodePayload(node) ?? throw new InvalidDataException("Current cleanup container payload is unknown.");
            if (payload == target) current.UnlinkArmyReturnTraitNode(handle, node);
            node = next;
        }
    }
    private static OriginalRegistryObjectReference[] Chain(OriginalEventInitializationContext current, OriginalRegistryListHandle? descriptor)
    {
        var handle = descriptor is null ? throw new InvalidDataException("Current lifecycle chain descriptor is unknown.") : descriptor;
        var values = new List<OriginalRegistryObjectReference>();
        var node = current.ReadRegistryHead(handle) ?? throw new InvalidDataException("Current lifecycle chain head is unknown.");
        while (node.Identity is not null)
        { values.Add(current.ReadRegistryNodePayload(node) ?? throw new InvalidDataException("Current lifecycle payload is unknown."));
          node = current.ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current lifecycle next is unknown."); }
        return values.ToArray();
    }
    private static void RestoreHeaders(OriginalEventInitializationContext current, NativeArmyLifecycleSlot[] slots)
    {
        foreach (var slot in slots)
        { current.SetArmyUnitTablePointer(slot.PhysicalSlot, slot.PhysicalSlot); current.SetArmyUnitCommanderId(slot.PhysicalSlot, slot.Commander12);
          current.SetArmyUnitStatusFlag10(slot.PhysicalSlot, slot.Status10); }
    }
}
