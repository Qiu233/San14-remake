using San14.Core;

namespace San14.Data;

/// <summary>Initializes source receiver numeric fields and the campaign
/// domain indexes. Source traversal cursors exist only inside an operation;
/// saved registration and retirement order use logical entity IDs.</summary>
public static class OwnedArmyLifecycleCurrent
{
    public const string Revision = "san14:ce-owned-army-lifecycle-current-20261007";
    public static GameState InstallFresh(GameState state, ArmyOperationContext definitions)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(definitions);
        if (state.ReadArmyLifecycle() is not null || state.ArmyMapOccupancy is not null || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.NativeArmySimulation is null)
            throw new InvalidDataException("Fresh Army lifecycle requires its explicit initial owned campaign.");
        var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions, ArmyReceiverSourceDirectory.CreateFresh(501));
        definitions.BindFreshLifecycleUnprojectedReceivers(state, current);
        var pool = current.ReadOwnedCurrentListPool()
            ?? throw new InvalidDataException("Fresh world indexes are missing.");
        pool.BindWorld(state);
        // The fresh CWorld F0 container is allocated once. Restore/import
        // preserves an unavailable queue rather than making one here.
        pool.PendingObjectRetirements = [];
        //31ADB0: each manager container obtains one known empty descriptor.
        foreach (var kind in new[] { RegistryKind.Army, RegistryKind.Gate, RegistryKind.Force,
            RegistryKind.Area, RegistryKind.District, RegistryKind.Person })
            current.SetRegistryDescriptor(kind, Registry(pool.AllocateDescriptor()));
        current.SetArmyReturnPending70Descriptor(Registry(pool.AllocateDescriptor()));

        var slots = new ArmyReceiverSourceSlot[501];
        var receiverDirectory = ArmyReceiverSourceDirectory.CreateFresh(slots.Length);
        for (int physical = 0; physical < slots.Length; physical++)
        {
            //326990:501 x200-byte CArmy;2209A0:9 x10-byte list ctor;
            //2269E0: header/raw5C60 and all36-word64/AC/F4 reset.
            receiverDirectory.TryResolveReceiverId(physical, out var receiverId);
            slots[physical] = new(physical, 0, 0, 0)
                { ReceiverId = receiverId,
                    AuxiliaryAC = new ushort[36], AuxiliaryF4 = new ushort[36],
                    Direction36 = ArmyDirectionSource.ResetDirection36,
                    MapUiByte13C = 0, CommandControl140 = 0, RouteHold56 = 0, RouteGoal1F8 = 0xBD10 };
            current.SetArmyUnitTablePointer(physical, physical);
            current.SetArmyUnitCommanderId(physical, 0); current.SetArmyUnitStatusFlag10(physical, 0);
        }
        foreach (var kind in new[] { BaseKind.City, BaseKind.Gate })
            for (int physical = 0; physical < (kind == BaseKind.City ? 52 : 11); physical++)
            {
                var target = new BaseReference(kind, physical);
                //221090 creates BOTH containers, including placeholder0.
                var hexes = current.ReadBaseHexListHandle(target) ?? pool.AllocateDescriptor();
                current.SetBaseHexListHandle(target, hexes);
                var people = Registry(pool.AllocateDescriptor()); current.SetArmyReturnBasePerson88Descriptor(target, people);
            }
        //Actual registration loops use current native physical receiver
        //tables and validity/status. They do not enumerate Core Officer IDs.
        foreach (var range in ObjectRegistration.Ranges.Where(x => x.Kind is RegistryKind.Army or
            RegistryKind.Gate or RegistryKind.Force or RegistryKind.Area or
            RegistryKind.District or RegistryKind.Person))
            for (int physical = 0; physical < range.Count; physical++)
            {
                var reference = current.ReadRegistryTableReference(range.Kind, physical);
                if (reference is null || current.ReadRegistryObjectNativeValidity(reference) is null)
                    throw new InvalidDataException($"Fresh Army lifecycle {range.Kind}[{physical}] native validity owner is unknown.");
                _ = ObjectRegistration.Register(reference, current);
            }
        var source = definitions.LifecycleScenarioSource;
        var parameters = state.NativeArmySimulation.Parameters;
        var receivers = new ArmyReceiverRoster(slots.Select(slot => new ArmyReceiverState(slot.ReceiverId,
            slot.PhysicalSlot != 0, ArmyReceiverPhase.Ready, null, null)
            { OverrideTarget = ArmyOverrideTargetState.Cleared, SavedCommand = ArmySavedCommandState.Cleared, FalseReportDestination = ArmyMarchDestination.None,
                FacilityConstruction = FacilityConstructionIntent.None,
                GroupAssignmentReady = true, SupplyInterdiction = ArmySupplyInterdictionIntent.Inactive }).ToArray());
        var domain = new ArmyLifecycleState(receivers, pool.ExportOwnedState());
        var sourceState = new ArmyLifecycleSourceState(Revision, parameters.Language, parameters.RuleVersion,
            source.ResourceUri, source.Sha256) { Slots = slots };
        var result = state.WithArmyLifecycle(new(domain, sourceState));
        result = ArmyMapOccupancyAdapter.InstallFresh(result, result.ReadArmyLifecycle()!);
        result = result.WithArmyLifecycle(OwnedArmyLifecycleAuxiliaries.InstallFresh(result.ReadArmyLifecycle()!, current, pool));
        result = OwnedArmyLifecycleDetachedInputs.InstallFresh(result, definitions);
        result = OwnedArmyDestructorFlags.InstallFresh(result);
        result = OwnedArmyLifecycleTargets.InstallFresh(result);
        result = ArmyDestructionQueue.InstallFreshOwned(result);
        return result;
    }

    /// <summary>The native501 scan chooses the first non-placeholder invalid
    /// header. Pending receivers retain their header until actual cleanup.</summary>
    public static int? FirstCreationSlot(GameState state)
    {
        if (state.ReadArmyLifecycle() is not { Slots.Count: 501 } arena) return null;
        return arena.Receivers.FirstAssignable() is { } receiver
            ? arena.Slots.First(slot => slot.ReceiverId == receiver).PhysicalSlot : null;
    }

    /// <summary>Actual31F430 registration after the caller has constructed a
    /// complete Unit image. Does not execute creator governor/model tail.</summary>
    public static ArmyLifecycleCreationAdmission? PrepareCreationAdmission(GameState state, UnitState created,
        ArmyOperationContext definitions, byte? packetTargetKind38 = null, ushort? packetTargetIdentity3A = null,
        bool prepareGroups = true) =>
        PrepareCreationAdmissionCore(state, created, definitions, 1, packetTargetKind38, packetTargetIdentity3A, prepareGroups);

    /// <summary>2D8FE0 writes cargo and status2 before the same31F430 owner.
    /// Ordinary allocation continues to preserve the prior receiver cargo.</summary>
    public static ArmyLifecycleCreationAdmission? PrepareTransportCreationAdmission(GameState state, UnitState created,
        ArmyOperationContext definitions, byte packetTargetKind38, ushort packetTargetIdentity3A) =>
        PrepareCreationAdmissionCore(state, created, definitions, 2, packetTargetKind38, packetTargetIdentity3A);

    private static ArmyLifecycleCreationAdmission? PrepareCreationAdmissionCore(GameState state, UnitState created,
        ArmyOperationContext definitions, byte status, byte? packetTargetKind38, ushort? packetTargetIdentity3A, bool prepareGroups = true)
    {
        if (state.ReadArmyLifecycle() is not { } arena || created.NativeArmyOrigin is not { NativeSlotIndex: int physical } origin ||
            origin.Language != arena.Language || origin.RuleVersion != arena.RuleVersion ||
            FirstCreationSlot(state) != physical || created.NativeTacticRuntime?.Status10 != status || created.OfficerId is < 1 or > 6000 ||
            created.CarriedStores is not { } resources) return null;
        var old = arena.ReceiverSlots()[physical];
        if (state.ArmyMapOccupancy is not { } occupancy || old.OccupancyBindingId is not int binding) return null;
        var assignedOccupancy = ArmyMapOccupancyRules.AssignBinding(occupancy, binding, created.Id);
        if (assignedOccupancy is null) return null;
        if (arena.ArmyGroups is not null && old.GroupAssignmentReady != true) return null;
        if (status == 1 && (old.StoredResources.Gold != resources.Gold || old.StoredResources.Food != resources.Food)) return null;
        if (status == 2 && (created.TransportRoute is null || created.Mission?.Kind != UnitMissionKind.Transport)) return null;
        var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions);
        var pool = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
        current.BindArmyMapOccupancy(state);
        foreach (var slot in arena.ReceiverSlots())
        {
            current.SetArmyUnitTablePointer(slot.PhysicalSlot, slot.PhysicalSlot);
            current.SetArmyUnitCommanderId(slot.PhysicalSlot, slot.Commander12);
            current.SetArmyUnitStatusFlag10(slot.PhysicalSlot, slot.Status10);
        }
        pool.BindArmyIdentity(physical, created.Id);
        pool.TraitHandles(physical);
        current.SetArmyUnitCommanderId(physical, (ushort)created.OfficerId); current.SetArmyUnitStatusFlag10(physical, status);
        var pointer = new RegistryObjectReference(RegistryKind.Army, physical);
        if (!ArmyMovementManagerLifecycle.Register(pointer, current) ||
            !ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current).Contains(pointer)) return null;
        var slots = arena.ReceiverSlots().ToArray();
        slots[physical] = old with { Commander12 = (ushort)created.OfficerId, Status10 = status,
            //Ordinary uses packet+4; type2 creator independently writes0.
            Priority14 = status == 2 ? (ushort)0 : ArmyMarchSourceAdapter.EncodeCredit(created.March),
            ActiveUnitId = created.Id, PendingImage = null,
            GroupAssignmentReady = false,
            OverrideTarget = created.OverrideTarget, Direction36 = created.NativeDirection36,
            MapUiByte13C = created.NativeMapUiByte13C,
            CommandControl140 = created.NativeCommandControl140,
            RouteHold56 = created.NativeRouteHold56, RouteGoal1F8 = created.NativeRouteGoal1F8,
            StoredResources = new(resources.Gold, resources.Food) };
        var registered = arena.WithReceiverSlots(slots) with { Indexes = pool.ExportOwnedState() };
        var targetState = OwnedArmyLifecycleTargets.PublishCreation(state.WithArmyLifecycle(registered), physical, packetTargetKind38, packetTargetIdentity3A);
        targetState = targetState with { ResourceState = CampaignSourceKnowledge.From(targetState.ResourceState).WithoutArmyPosition(created.Id) };
        targetState = ArmyOverrideTargetSourceAdapter.InheritReceiverKnowledge(targetState, created);
        registered = targetState.ReadArmyLifecycle()!;
        var admitted = prepareGroups ? OwnedArmyLifecycleAuxiliaries.PrepareCreation(registered, state, created, current, pool) : registered;
        return admitted is null ? null : new(admitted, assignedOccupancy) { ResourceState = targetState.ResourceState };
    }

    /// <summary>Only31F370's container operation for a known pooled pointer.
    /// The caller owns ordinary-return/destruction admission. This receipt
    /// does not retire a Unit, reset a header or execute28E6D0 cleanup.</summary>
    public static ArmyLifecycleSourceView? PreparePendingAdmission(GameState state, int physical,
        ArmyOperationContext definitions)
    {
        if (state.ReadArmyLifecycle() is not { Slots.Count: 501 } arena || physical is < 1 or > 500 ||
            arena.ReceiverSlots()[physical] is not { Commander12: > 0, Status10: > 0 } slot ||
            slot.ActiveUnitId is null && slot.PendingImage is null) return null;
        var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions);
        var pool = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
        current.BindArmyMapOccupancy(state);
        var outcome = ArmyPending70Admission.EnqueueCurrentPointer(new(RegistryKind.Army, physical), new Pending(current));
        return outcome == ArmyPending70AdmissionOutcome.KnownAllocationFailure ? null :
            arena with { Indexes = pool.ExportOwnedState() };
    }

    private sealed class Pending(SourceWorldContext current) : IArmyPending70AdmissionState
    {
        public RegistryListHandle? ReadPending70Descriptor() => current.ReadArmyReturnPending70Descriptor();
        public RegistryListNodeReference? ReadPending70Head(RegistryListHandle descriptor) => current.ReadRegistryHead(descriptor);
        public RegistryObjectReference? ReadPending70Payload(RegistryListNodeReference node) => current.ReadRegistryNodePayload(node);
        public RegistryListNodeReference? ReadPending70Next(RegistryListNodeReference node) => current.ReadRegistryNodeNext(node);
        public bool? ReadPending70LockEnabled() => current.ReadRegistryLockEnabled();
        public void EnterPending70Lock() => current.EnterRegistryLock();
        public void ExitPending70Lock() => current.ExitRegistryLock();
        public RegistryListNodeReference? AppendPending70Node(RegistryListHandle descriptor) => current.AllocateRegistryNode(descriptor);
        public void WritePending70Payload(RegistryListNodeReference node, RegistryObjectReference army) => current.WriteRegistryNodePayload(node, army);
    }

    private static RegistryListHandle Registry(HexListHandle handle) => new(handle.Identity);
}

public sealed partial class ArmyOperationContext
{
    internal SourceProvenance LifecycleScenarioSource => _source.ScenarioSource;
    internal void BindFreshLifecycleUnprojectedReceivers(GameState state, SourceWorldContext current)
    {
        //Core nullable RulerId does not encode a native raw word. This is the
        //explicit fresh serializer value, installed only during allocation.
        foreach (var row in _source.Snapshot.Forces)
            if (current.ReadForceRecord(row.Slot) is null && state.Forces.GetValueOrDefault(row.Slot)?.RulerId is null && row.RulerSlot == 0)
                current.SetForceRecord(new(row.Slot, row.RulerSlot));
        var represented = state.Officers.Values.Select(x => current.ResolveOfficerSource(x.Id)
            ?? throw new InvalidDataException("Fresh Person source identity is unknown.")).ToHashSet();
        foreach (var row in _source.Snapshot.Characters)
        {
            if (represented.Contains(row.Slot) || row.Slot == 0) continue;
            if (row.CreditReference is not { RawFlagsWord: ushort flags } appendix)
                throw new InvalidDataException($"Fresh unprojected Person[{row.Slot}] serializer fields are unknown.");
            current.SetPersonRecord(new(row.Slot, (ushort)row.CharacterId, flags,
                appendix.RawTailCode, row.StatusCode, row.SecondaryCitySlot));
            if (current.ReadPersonNativeValidity(row.Slot) == true && row.StatusCode is >= 1 and <= 6 or 8)
                throw new InvalidDataException($"Fresh registered Person[{row.Slot}] has no saved gameplay owner.");
        }
    }
}

public sealed partial class SourceWorldContext : IArmyMovementManagerLifecycleState
{
    public void UnlinkArmyManagerNode(RegistryListHandle descriptor, RegistryListNodeReference node) =>
        UnlinkArmyReturnTraitNode(descriptor, node);
}
