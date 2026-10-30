using San14.Core;

namespace San14.Data;

/// <summary>Initializes the military owners, assigns creator groups and
/// consumes ordered deferred retirements without presentation receivers.</summary>
public static class OwnedArmyLifecycleAuxiliaries
{
    internal static ArmyLifecycleSourceView InstallFresh(ArmyLifecycleSourceView arena,
        SourceWorldContext current, WorldIndexContext pool)
    {
        //220A40/220A70/227F00/2269E0 constructors. These zero-byte scenario
        //sections do not supply additional initial field stores.
        for (int group = 0; group < 501; group++) pool.CautionGroupHandle(group);
        foreach (var kind in new[] { RegistryKind.CautionTroops, RegistryKind.Table81B50 })
        {
            var descriptor = pool.AllocateDescriptor();
            current.SetRegistryDescriptor(kind, new(descriptor.Identity));
        }
        //1D7DD0 command-manager18 ptr_list constructor; currently implemented
        //owned orders do not allocate original CCmdAction receivers.
        pool.CreateAssociatedTaskIndex();
        return arena with { Indexes = pool.ExportOwnedState(),
            ArmyGroups = new() { ResetLeader = ArmyGroupSourceAdapter.Officer(current, 0) },
            ArmyCautions = ArmyCautionGroupPlanningSource.Fresh() };
    }

    internal static ArmyLifecycleSourceView? PrepareCreation(ArmyLifecycleSourceView arena,
        GameState state, UnitState created, SourceWorldContext current, WorldIndexContext pool)
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
    public static GameState? ConsumePending(GameState state, ArmyOperationContext definitions,
        Action<int>? committedReset = null)
    {
        var arena = state.ReadArmyLifecycle();
        if (arena is null || arena.ArmyGroups is not { } groups || arena.ArmyCautions is not { } cautions || state.PendingCombatSettlements is null || arena.ReceiverSlots().Length != 501) return null;
        // The exact saved retirement queue is authoritative. Its known-empty
        // pass has no reset/unlink/publish work; keep the stage26 caller's
        // separate nearby-enemy refresh. Null remains unknown.
        if (arena.Indexes.PendingUnitIds is { Count: 0 }) return state;
        var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions);
        var pool = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
        current.BindArmyMapOccupancy(state);
        RestoreHeaders(current, arena.ReceiverSlots());
        return ConsumePendingCore(state, current, pool, arena, groups, cautions, committedReset, borrowed: false);
    }

    /// <summary>The real deferred retirement pass over the caller's live pool.
    /// It is invoked at the lifecycle stage, never by the return publisher.</summary>
    internal static GameState? ConsumePendingCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, Action<int>? committedReset = null)
    {
        var arena = state.ReadArmyLifecycle();
        if (arena is null || arena.ArmyGroups is not { } groups || arena.ArmyCautions is not { } cautions ||
            state.PendingCombatSettlements is null || arena.ReceiverSlots().Length != 501 ||
            !ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile()) return null;
        //The borrowed queue may differ from its last saved projection. Read
        //its actual live head; a known-empty pass has no reset or unlink work
        //and must not copy occupancy/receiver tables for every quote-only bill.
        var descriptor = current.ReadArmyReturnPending70Descriptor()
            ?? throw new InvalidDataException("Current borrowed pending70 descriptor is unknown.");
        var head = current.ReadRegistryHead(descriptor)
            ?? throw new InvalidDataException("Current borrowed pending70 head is unknown.");
        if (!head.Identity.HasValue) return state;
        return ConsumePendingCore(state, current, pool, arena, groups, cautions, committedReset, borrowed: true);
    }

    private static GameState? ConsumePendingCore(GameState state, SourceWorldContext current, WorldIndexContext pool,
        ArmyLifecycleSourceView arena, ArmyGroupsState groups, ArmyCautionsState cautions,
        Action<int>? committedReset, bool borrowed)
    {
        var pendingOrder = Chain(current, current.ReadArmyReturnPending70Descriptor());
        if (pendingOrder.Any(x => x.Kind != RegistryKind.Army || x.PhysicalSlot is not (>= 1 and <= 500))) return null;
        if (pendingOrder.Any(x => arena.ReceiverSlots()[x.PhysicalSlot!.Value].ActiveUnitId is not null ||
            arena.ReceiverSlots()[x.PhysicalSlot.Value].PendingImage is null)) return null;
        var cautionMembers = arena.Indexes.CautionGroupMembers;
        var slots = arena.ReceiverSlots().ToArray();
        var armyFlags150 = arena.ArmyFlags150?.ToArray();
        OwnedArmyLifecycleTargets.BindCurrent(state, current);
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
                    RemoveAll(current, current.ReadRegistryDescriptor(RegistryKind.Table81B50),
                        new(RegistryKind.Table81B50, relationId));
                foreach (int groupId in retiredCautions.ChangedGroupIds)
                    pool.ReplaceCautionGroupMembers(groupId, cautionMembers[groupId]!);
                var roster = ArmyGroupSourceAdapter.Roster(arena, current);
                var currentState = state.WithArmyLifecycle(arena.WithReceiverSlots(slots));
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
                    PendingImage = null, StoredResources = ArmyStoreKnowledge.Empty, AuxiliaryAC = new ushort[36], AuxiliaryF4 = new ushort[36],
                    OverrideTarget = ArmyOverrideTargetState.Cleared, Direction36 = ArmyDirectionSource.ResetDirection36,
                    FalseReportDestination = ArmyMarchDestination.None, FacilityConstruction = FacilityConstructionIntent.None,
                    MapUiByte13C = 0, CommandControl140 = 0, RouteHold56 = 0, RouteGoal1F8 = 0xBD10, AiActionFlags = 0 };
                current.ReleaseArmyOccupancyReceiver(army);
                current.SetArmyUnitCommanderId(army, 0); current.SetArmyUnitStatusFlag10(army, 0);
                if (armyFlags150 is not null) armyFlags150[army] = 0; //2269E0 byte150 explicit reset.
                if (borrowed)
                {
                    current.SetArmyUnitFlags150(army, 0);
                    current.SetStartupArmyTroopsSlot58(army, 0);
                }
                if (arena.Targets is not null) current.SetArmyLifecycleTargetFields(army, new(0xFF, 0)); //2269E0 target reset.
                _ = ArmyMovementManagerLifecycle.Remove(receiver, current);
                pool.RemoveArmyIdentity(army);
                roster = ArmyGroupSourceAdapter.Roster(arena, current);
                excluded = ArmyGroupSourceAdapter.Excluded(currentState, arena, current, roster);
                groups = ArmyGroupRules.RemoveUnitAndResetEmptyGroup(groups, unitId, roster, excluded)
                    ?? throw new InvalidDataException("Current group retirement membership is unavailable.");
            }
            current.ClearTemporaryHexList(new(current.ReadArmyReturnPending70Descriptor()!.Identity));
            var result = (state with { ArmyMapOccupancy = current.ExportArmyMapOccupancy() }).WithArmyLifecycle(arena.WithReceiverSlots(slots) with { Indexes = borrowed ? pool.ProjectOwnedRoles() : pool.ExportOwnedState(), ArmyFlags150 = armyFlags150,
                ArmyGroups = groups, ArmyCautions = cautions });
            var resetRoster = result.ReadArmyLifecycle()!.Receivers;
            foreach (var receiver in pendingOrder)
            {
                var id = arena.ReceiverSlots()[receiver.PhysicalSlot!.Value].ReceiverId;
                resetRoster = resetRoster.With(resetRoster.Find(id)! with { SupplyInterdiction = ArmySupplyInterdictionIntent.Inactive });
            }
            result = result.WithArmyLifecycle(result.ReadArmyLifecycle()! with { Receivers = resetRoster });
            result = OwnedArmyLifecycleTargets.PublishCurrent(result, current);
            if (result.ResourceState is CampaignSourceKnowledge knowledge)
            {
                var resetReceivers = pendingOrder.Select(receiver => arena.ReceiverSlots()[receiver.PhysicalSlot!.Value].ReceiverId).ToHashSet();
                var retiredUnits = pendingOrder.Select(receiver => arena.ReceiverSlots()[receiver.PhysicalSlot!.Value].PendingImage!.Id).ToHashSet();
                foreach (var resetReceiver in resetReceivers)
                    knowledge = knowledge.WithArmySavedTargetLookupAlias(resetReceiver, null);
                result = result with { ResourceState = knowledge.ReplaceArmyOverrides(knowledge.ArmyOverrides
                    .Where(row => row.Owner.ReceiverId is not { } id || !resetReceivers.Contains(id)))
                    .ReplaceTransportRoutes(knowledge.TransportRoutes.Where(row => row.Owner.DefeatedForceId is not null || !retiredUnits.Contains(row.Owner.UnitId)))
                    .ReplaceArmyTraits(knowledge.ArmyTraits.Where(row => row.Owner.DefeatedForceId is not null || !retiredUnits.Contains(row.Owner.UnitId)))
                    .ReplaceArmyConfigurations(knowledge.ArmyConfigurations.Where(row => row.Owner.DefeatedForceId is not null || !retiredUnits.Contains(row.Owner.UnitId))) };
            }
            foreach (var receiver in pendingOrder) committedReset?.Invoke(receiver.PhysicalSlot!.Value);
            return result;
        }
        catch (InvalidDataException)
        {
            if (borrowed) throw;
            return null;
        }
    }

    internal static void RestoreCurrent(SourceWorldContext current, ArmyLifecycleSourceView arena)
    {
        if (arena.ArmyGroups is not null) ArmyGroupSourceAdapter.RestoreAssignments(current, arena);
        if (arena.ArmyCautions is { } cautions) ArmyCautionSourceAdapter.Restore(current, cautions);
    }

    internal static int? CurrentPlayerPrimaryDistrict(GameState state, SourceWorldContext current)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        if (player?.PhysicalSlot is not int force) return null;
        //22E930's candidate match requires Force physical1..51, not its
        //header validity. No district field can change the physical0 result.
        if (force is < 1 or > 51) return 0;
        if (current.ReadForceRecord(force) is not { } record) return null;
        ushort ruler = record.RawRuler10;
        foreach (var receiver in Chain(current, new(current.ReadRegistryDescriptor(RegistryKind.District)!.Identity)))
        {
            int district = receiver.PhysicalSlot!.Value;
            if (current.ReadDistrictForceId(district) is not byte candidateForce) return null;
            if (candidateForce != force) continue;
            ushort? leader = current.ReadDistrictLeader(district);
            if (leader == ruler) return district;
            byte? number = current.ReadDistrictNumber(district);
            if (number == 1) return district;
            if (leader is null || number is null) return null;
        }
        return 0;
    }
    private static void RemoveAll(SourceWorldContext current, RegistryListHandle? descriptor, RegistryObjectReference target)
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
    private static RegistryObjectReference[] Chain(SourceWorldContext current, RegistryListHandle? descriptor)
    {
        var handle = descriptor is null ? throw new InvalidDataException("Current lifecycle chain descriptor is unknown.") : descriptor;
        var values = new List<RegistryObjectReference>();
        var node = current.ReadRegistryHead(handle) ?? throw new InvalidDataException("Current lifecycle chain head is unknown.");
        while (node.Identity is not null)
        { values.Add(current.ReadRegistryNodePayload(node) ?? throw new InvalidDataException("Current lifecycle payload is unknown."));
          node = current.ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current lifecycle next is unknown."); }
        return values.ToArray();
    }
    private static void RestoreHeaders(SourceWorldContext current, IReadOnlyList<ArmyLifecycleReceiverView> slots)
    {
        foreach (var slot in slots)
        { current.SetArmyUnitTablePointer(slot.PhysicalSlot, slot.PhysicalSlot); current.SetArmyUnitCommanderId(slot.PhysicalSlot, slot.Commander12);
          current.SetArmyUnitStatusFlag10(slot.PhysicalSlot, slot.Status10); }
    }
}
