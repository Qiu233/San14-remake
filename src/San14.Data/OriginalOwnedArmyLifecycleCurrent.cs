using San14.Core;

namespace San14.Data;

/// <summary>Initializes source receiver numeric fields and the campaign
/// domain indexes. Source traversal cursors exist only inside an operation;
/// saved registration and retirement order use logical entity IDs.</summary>
public static class OriginalOwnedArmyLifecycleCurrent
{
    public const string Revision = "san14:ce-owned-army-lifecycle-current-20261007";
    public static GameState InstallFresh(GameState state, OriginalOwnedArmyCurrentDefinitions definitions)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(definitions);
        if (state.NativeArmyLifecycle is not null || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.NativeArmySimulation is null)
            throw new InvalidDataException("Fresh Army lifecycle requires its explicit initial owned campaign.");
        var current = OriginalOwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions);
        definitions.BindFreshLifecycleUnprojectedReceivers(state, current);
        var pool = current.ReadOwnedCurrentListPool()
            ?? throw new InvalidDataException("Fresh world indexes are missing.");
        pool.BindWorld(state);
        //31ADB0: each manager container obtains one known empty descriptor.
        foreach (var kind in new[] { OriginalRegistryKind.Army, OriginalRegistryKind.Gate, OriginalRegistryKind.Force,
            OriginalRegistryKind.Area, OriginalRegistryKind.District, OriginalRegistryKind.Person })
            current.SetRegistryDescriptor(kind, Registry(pool.AllocateDescriptor()));
        current.SetArmyReturnPending70Descriptor(Registry(pool.AllocateDescriptor()));

        var slots = new NativeArmyLifecycleSlot[501];
        for (int physical = 0; physical < slots.Length; physical++)
        {
            //326990:501 x200-byte CArmy;2209A0:9 x10-byte list ctor;
            //2269E0: header/raw5C60 and all36-word64/AC/F4 reset.
            slots[physical] = new(physical, 0, 0, 0, null, null, 0, 0, new ushort[36], new ushort[36])
                { OverrideTarget = new(0xFF, 0), Direction36 = OriginalArmyDirectionSource.ResetDirection36,
                    MapUiByte13C = 0, CommandControl140 = 0, RouteHold56 = 0, RouteGoal1F8 = 0xBD10,
                    GroupAssignmentReady = true };
            current.SetArmyUnitTablePointer(physical, physical);
            current.SetArmyUnitCommanderId(physical, 0); current.SetArmyUnitStatusFlag10(physical, 0);
        }
        foreach (var kind in new[] { OriginalBaseKind.City, OriginalBaseKind.Gate })
            for (int physical = 0; physical < (kind == OriginalBaseKind.City ? 52 : 11); physical++)
            {
                var target = new OriginalBaseReference(kind, physical);
                //221090 creates BOTH containers, including placeholder0.
                var hexes = current.ReadBaseHexListHandle(target) ?? pool.AllocateDescriptor();
                current.SetBaseHexListHandle(target, hexes);
                var people = Registry(pool.AllocateDescriptor()); current.SetArmyReturnBasePerson88Descriptor(target, people);
            }
        //Actual registration loops use current native physical receiver
        //tables and validity/status. They do not enumerate Core Officer IDs.
        foreach (var range in OriginalObjectRegistration.Ranges.Where(x => x.Kind is OriginalRegistryKind.Army or
            OriginalRegistryKind.Gate or OriginalRegistryKind.Force or OriginalRegistryKind.Area or
            OriginalRegistryKind.District or OriginalRegistryKind.Person))
            for (int physical = 0; physical < range.Count; physical++)
            {
                var reference = current.ReadRegistryTableReference(range.Kind, physical);
                if (reference is null || current.ReadRegistryObjectNativeValidity(reference) is null)
                    throw new InvalidDataException($"Fresh Army lifecycle {range.Kind}[{physical}] native validity owner is unknown.");
                _ = OriginalObjectRegistration.Register(reference, current);
            }
        var source = definitions.LifecycleScenarioSource;
        var parameters = state.NativeArmySimulation.Parameters;
        var result = state with { NativeArmyLifecycle = new(Revision, parameters.Language, parameters.RuleVersion,
            source.ResourceUri, source.Sha256, pool.ExportOwnedState(), slots,
            Enumerable.Range(0, checked(state.MapWidth * state.MapHeight)).ToDictionary(x => x, _ => (ushort?)0)) };
        result = result with { NativeArmyLifecycle = OriginalOwnedArmyLifecycleAuxiliaries.InstallFresh(result.NativeArmyLifecycle!, current, pool) };
        result = OriginalOwnedArmyLifecycleDetachedInputs.InstallFresh(result, definitions);
        result = OriginalOwnedArmyDestructorFlags.InstallFresh(result);
        result = OriginalOwnedArmyLifecycleTargets.InstallFresh(result);
        result = OriginalArmyDestructionQueue.InstallFreshOwned(result);
        NativeArmyLifecycleStateValidation.Validate(result); return result;
    }

    /// <summary>The native501 scan chooses the first non-placeholder invalid
    /// header. Pending receivers retain their header until actual cleanup.</summary>
    public static int? FirstCreationSlot(GameState state)
    {
        if (state.NativeArmyLifecycle is not { Slots.Length: 501 } arena) return null;
        for (int physical = 1; physical < arena.Slots.Length; physical++)
        {
            var slot = arena.Slots[physical];
            if (slot.Commander12 is null || slot.Commander12 != 0 && slot.Status10 is null) return null;
            if (slot.Commander12 != 0 && slot.Status10 != 0) continue;
            if (slot.ActiveUnitId is not null || slot.PendingImage is not null) return null;
            return physical;
        }
        return null;
    }

    /// <summary>Actual31F430 registration after the caller has constructed a
    /// complete Unit image. Does not execute creator governor/model tail.</summary>
    public static NativeArmyLifecycleState? PrepareCreationAdmission(GameState state, UnitState created,
        OriginalOwnedArmyCurrentDefinitions definitions, byte? packetTargetKind38 = null) =>
        PrepareCreationAdmissionCore(state, created, definitions, 1, packetTargetKind38, null);

    /// <summary>2D8FE0 writes cargo and status2 before the same31F430 owner.
    /// Ordinary allocation continues to preserve the prior receiver cargo.</summary>
    public static NativeArmyLifecycleState? PrepareTransportCreationAdmission(GameState state, UnitState created,
        OriginalOwnedArmyCurrentDefinitions definitions, byte packetTargetKind38, ushort packetTargetIdentity3A) =>
        PrepareCreationAdmissionCore(state, created, definitions, 2, packetTargetKind38, packetTargetIdentity3A);

    private static NativeArmyLifecycleState? PrepareCreationAdmissionCore(GameState state, UnitState created,
        OriginalOwnedArmyCurrentDefinitions definitions, byte status, byte? packetTargetKind38, ushort? packetTargetIdentity3A)
    {
        if (state.NativeArmyLifecycle is not { } arena || created.NativeArmyOrigin is not { NativeSlotIndex: int physical } origin ||
            origin.Language != arena.Language || origin.RuleVersion != arena.RuleVersion ||
            FirstCreationSlot(state) != physical || created.NativeTacticRuntime?.Status10 != status || created.OfficerId is < 1 or > 6000 ||
            created.NativeReturnResources is not { } resources) return null;
        var old = arena.Slots[physical];
        if (arena.ArmyGroups is not null && old.GroupAssignmentReady != true) return null;
        if (status == 1 && (old.Gold5C != resources.Gold5C || old.Supplies60 != resources.Supplies60)) return null;
        if (status == 2 && (created.NativeTransport is null || created.Mission?.Kind != UnitMissionKind.Transport)) return null;
        var current = OriginalOwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions);
        var pool = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
        current.RestoreOwnedArmyReturnBindings(arena);
        foreach (var slot in arena.Slots)
        {
            current.SetArmyUnitTablePointer(slot.PhysicalSlot, slot.PhysicalSlot);
            current.SetArmyUnitCommanderId(slot.PhysicalSlot, slot.Commander12);
            current.SetArmyUnitStatusFlag10(slot.PhysicalSlot, slot.Status10);
        }
        pool.BindArmyIdentity(physical, created.Id);
        pool.TraitHandles(physical);
        current.SetArmyUnitCommanderId(physical, (ushort)created.OfficerId); current.SetArmyUnitStatusFlag10(physical, status);
        var pointer = new OriginalRegistryObjectReference(OriginalRegistryKind.Army, physical);
        if (!OriginalArmyMovementManagerLifecycle.Register(pointer, current) ||
            !OriginalArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current).Contains(pointer)) return null;
        var slots = arena.Slots.ToArray();
        slots[physical] = old with { Commander12 = (ushort)created.OfficerId, Status10 = status,
            //Ordinary uses packet+4; type2 creator independently writes0.
            Priority14 = status == 2 ? (ushort)0 : created.NativeMarch?.Credit14,
            ActiveUnitId = created.Id, PendingImage = null,
            GroupAssignmentReady = false,
            OverrideTarget = created.NativeOverrideTarget, Direction36 = created.NativeDirection36,
            MapUiByte13C = created.NativeMapUiByte13C,
            CommandControl140 = created.NativeCommandControl140,
            RouteHold56 = created.NativeRouteHold56, RouteGoal1F8 = created.NativeRouteGoal1F8,
            Gold5C = resources.Gold5C, Supplies60 = resources.Supplies60 };
        var registered = arena with { Slots = slots, Indexes = pool.ExportOwnedState() };
        registered = OriginalOwnedArmyLifecycleTargets.PublishCreation(registered, physical, packetTargetKind38, packetTargetIdentity3A);
        return OriginalOwnedArmyLifecycleAuxiliaries.PrepareCreation(registered, state, created, current, pool);
    }

    /// <summary>Only31F370's container operation for a known pooled pointer.
    /// The caller owns ordinary-return/destruction admission. This receipt
    /// does not retire a Unit, reset a header or execute28E6D0 cleanup.</summary>
    public static NativeArmyLifecycleState? PreparePendingAdmission(GameState state, int physical,
        OriginalOwnedArmyCurrentDefinitions definitions)
    {
        if (state.NativeArmyLifecycle is not { Slots.Length: 501 } arena || physical is < 1 or > 500 ||
            arena.Slots[physical] is not { Commander12: > 0, Status10: > 0 } slot ||
            slot.ActiveUnitId is null && slot.PendingImage is null) return null;
        var current = OriginalOwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions);
        var pool = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
        current.RestoreOwnedArmyReturnBindings(arena);
        var outcome = OriginalArmyPending70Admission.EnqueueCurrentPointer(new(OriginalRegistryKind.Army, physical), new Pending(current));
        return outcome == OriginalArmyPending70AdmissionOutcome.KnownAllocationFailure ? null :
            arena with { Indexes = pool.ExportOwnedState() };
    }

    private sealed class Pending(OriginalEventInitializationContext current) : IOriginalArmyPending70AdmissionState
    {
        public OriginalRegistryListHandle? ReadPending70Descriptor() => current.ReadArmyReturnPending70Descriptor();
        public OriginalRegistryListNodeReference? ReadPending70Head(OriginalRegistryListHandle descriptor) => current.ReadRegistryHead(descriptor);
        public OriginalRegistryObjectReference? ReadPending70Payload(OriginalRegistryListNodeReference node) => current.ReadRegistryNodePayload(node);
        public OriginalRegistryListNodeReference? ReadPending70Next(OriginalRegistryListNodeReference node) => current.ReadRegistryNodeNext(node);
        public bool? ReadPending70LockEnabled() => current.ReadRegistryLockEnabled();
        public void EnterPending70Lock() => current.EnterRegistryLock();
        public void ExitPending70Lock() => current.ExitRegistryLock();
        public OriginalRegistryListNodeReference? AppendPending70Node(OriginalRegistryListHandle descriptor) => current.AllocateRegistryNode(descriptor);
        public void WritePending70Payload(OriginalRegistryListNodeReference node, OriginalRegistryObjectReference army) => current.WriteRegistryNodePayload(node, army);
    }

    private static OriginalRegistryListHandle Registry(OriginalHexListHandle handle) => new(handle.Identity);
}

public sealed partial class OriginalOwnedArmyCurrentDefinitions
{
    internal SourceProvenance LifecycleScenarioSource => _source.ScenarioSource;
    internal void BindFreshLifecycleUnprojectedReceivers(GameState state, OriginalEventInitializationContext current)
    {
        //Core nullable RulerId does not encode a native raw word. This is the
        //explicit fresh serializer value, installed only during allocation.
        foreach (var row in _source.Snapshot.Forces)
            if (current.ReadForceRecord(row.Slot) is null && state.Forces.GetValueOrDefault(row.Slot)?.RulerId is null && row.RulerSlot == 0)
                current.SetForceRecord(new(row.Slot, row.RulerSlot));
        var represented = state.Officers.Values.Select(x => x.NativeAbilityRuntime!.PhysicalSlot).ToHashSet();
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

public sealed partial class OriginalEventInitializationContext : IOriginalArmyMovementManagerLifecycleState
{
    public void UnlinkArmyManagerNode(OriginalRegistryListHandle descriptor, OriginalRegistryListNodeReference node) =>
        UnlinkArmyReturnTraitNode(descriptor, node);
}
