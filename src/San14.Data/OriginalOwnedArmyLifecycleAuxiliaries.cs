using San14.Core;

namespace San14.Data;

/// <summary>Owned constructor, ordinary creator group allocation and deferred
/// numeric cleanup. Original native presentation objects are not constructed
/// by this client; no rendering receiver participates in these operations.</summary>
public static class OriginalOwnedArmyLifecycleAuxiliaries
{
    public const string Revision = "san14:ce-owned-army-lifecycle-auxiliaries-20261007";

    internal static NativeArmyLifecycleState InstallFresh(NativeArmyLifecycleState arena,
        OriginalEventInitializationContext current, OriginalCurrentListPool pool)
    {
        //220A40/220A70/227F00/2269E0 constructors. These zero-byte scenario
        //sections do not supply additional initial field stores.
        var descriptors = Enumerable.Range(0, 501).Select(_ => (NativeSharedListHandle?)new(pool.AllocateDescriptor().Identity)).ToArray();
        var registries = new Dictionary<string, NativeSharedListHandle?>(arena.RegistryDescriptors);
        foreach (var kind in new[] { OriginalRegistryKind.CautionTroops, OriginalRegistryKind.Table81B50 })
        {
            var descriptor = pool.AllocateDescriptor();
            current.SetRegistryDescriptor(kind, new(descriptor.Identity));
            registries[kind.ToString()] = new(descriptor.Identity);
        }
        //1D7DD0 command-manager18 ptr_list constructor; currently implemented
        //owned orders do not allocate original CCmdAction receivers.
        var associated = new NativeSharedListHandle(pool.AllocateDescriptor().Identity);
        return arena with { Pool = pool.ExportOwnedState(), RegistryDescriptors = registries,
            Auxiliaries = new(Revision, Zero(501), Zero(501), Zero(1001), Zero(1001), Zero(501), descriptors, associated)
                { InteractionPairs140 = [] } };
    }

    internal static NativeArmyLifecycleState? PrepareCreation(NativeArmyLifecycleState arena,
        GameState state, UnitState created, OriginalEventInitializationContext current, OriginalCurrentListPool pool)
    {
        if (arena.Auxiliaries is null) return arena; //Older arena keeps unknown.
        var aux = arena.Auxiliaries;
        if (!InteractionEmpty(arena)) return null;
        int army = created.NativeArmyOrigin!.NativeSlotIndex!.Value;
        var assignment = aux.ArmyTroops58.ToArray(); var leaders = aux.TroopsLeader2C.ToArray();
        var pending = Chain(current, arena.Pending70Descriptor).Select(x => x.PhysicalSlot!.Value).ToHashSet();
        var manager = OriginalArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current).Select(x => x.PhysicalSlot!.Value).ToArray();
        //2D32xx creates the actual one-pointer temporary descriptor before
        //2D9630 searches the complete CTroops table. Its free-chain effects
        //must survive along with manager registration.
        var temporary = pool.AllocateDescriptor();
        if (temporary.Identity is null) return null;
        var node = current.AllocateRegistryNode(new(temporary.Identity));
        if (node?.Identity is null) { current.ReleaseTemporaryHexList(temporary); return null; }
        current.WriteRegistryNodePayload(node, new(OriginalRegistryKind.Army, army));
        int group = 0;
        for (int physical = 1; physical < 501; physical++)
            if (!GroupValid(physical, assignment, manager, pending, current)) { group = physical; break; }
        if (group == 0) { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); return null; }
        //241D90: fresh/recycled receiver's old Army58 is zero.
        if (assignment[army] != 0) return null;
        assignment[army] = (ushort)group;
        int groupDistrict = GroupDistrict(group, assignment, manager, pending, current);
        int? playerDistrict = CurrentPlayerPrimaryDistrict(state, current);
        if (playerDistrict is null) return null;
        if (created.OfficerId < 6000)
            leaders[group] = groupDistrict == playerDistrict.Value ? leaders[group] : (ushort)created.OfficerId;
        current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary);
        return arena with { Pool = pool.ExportOwnedState(), Auxiliaries = aux with { ArmyTroops58 = assignment, TroopsLeader2C = leaders } };
    }

    /// <summary>28E6D0→282580 for this client's current retired receiver
    /// window, including actual saved pending order, all Caution containers,
    /// reset and manager unlink. The independent client owns no original
    /// presentation receiver or secondary interaction tuple. Nonempty tuple
    /// callbacks and live pending admission remain an explicit unclosed scope.</summary>
    public static GameState? ConsumePending(GameState state, OriginalOwnedArmyCurrentDefinitions definitions,
        Action<int>? committedReset = null)
    {
        var arena = state.NativeArmyLifecycle;
        if (arena?.Auxiliaries is not { } aux || !InteractionEmpty(arena) || arena.Slots.Length != 501) return null;
        var current = OriginalOwnedArmyReturnCurrentFields.CaptureCurrent(state, definitions);
        var pool = OriginalCurrentListPool.ImportOwnedState(current, arena.Pool);
        current.RestoreOwnedArmyReturnBindings(arena);
        RestoreHeaders(current, arena.Slots);
        var pendingOrder = Chain(current, arena.Pending70Descriptor);
        if (pendingOrder.Any(x => x.Kind != OriginalRegistryKind.Army || x.PhysicalSlot is not (>= 1 and <= 500))) return null;
        if (pendingOrder.Any(x => arena.Slots[x.PhysicalSlot!.Value].ActiveUnitId is not null ||
            arena.Slots[x.PhysicalSlot.Value].PendingImage is null)) return null;
        var pending = pendingOrder.Select(x => x.PhysicalSlot!.Value).ToHashSet();
        var assignments = aux.ArmyTroops58.ToArray(); var leaders = aux.TroopsLeader2C.ToArray();
        var caution10 = aux.CautionArmy10.ToArray(); var caution12 = aux.CautionArmy12.ToArray();
        var slots = arena.Slots.ToArray();
        var armyFlags150 = arena.ArmyFlags150?.ToArray();
        var armyTargets38 = arena.Targets38?.ToArray();
        try
        {
            foreach (var receiver in pendingOrder)
            {
                int army = receiver.PhysicalSlot!.Value;
                for (int i = 0; i < caution10.Length; i++)
                {
                    ushort value = Known(caution10[i], "CautionArmy10");
                    if (value != army) continue;
                    //32B4D0:226BA0 reset before manager130 pointer removal.
                    caution10[i] = caution12[i] = 0;
                    RemoveAll(current, arena.RegistryDescriptors.GetValueOrDefault("Table81B50"),
                        new(OriginalRegistryKind.Table81B50, i));
                }
                foreach (var descriptor in aux.CautionArmy30Descriptors) RemoveAll(current, descriptor, receiver);
                int raw = Known(assignments[army], "Army58"); int group = raw < 501 ? raw : 0;
                var manager = OriginalArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current).Select(x => x.PhysicalSlot!.Value).ToArray();
                if (GroupValid(group, assignments, manager, pending, current) &&
                    Person(current, Known(leaders[group], "CTroops2C")) == Person(current, Known(slots[army].Commander12, "Army12")))
                    UpdateLeader(group, assignments, leaders, manager, pending, slots, current, pool);
                foreach (var descriptor in slots[army].Trait160Descriptors ?? throw new InvalidDataException("Current Army160 descriptors are unknown."))
                    current.ClearTemporaryHexList(new(descriptor.Identity));
                //2269E0 resets numeric fields and clears all nine existing
                //descriptor chains; it does not replace their identities.
                assignments[army] = 0;
                slots[army] = slots[army] with { Commander12 = 0, Status10 = 0, Priority14 = 0, ActiveUnitId = null,
                    PendingImage = null, Gold5C = 0, Supplies60 = 0, AuxiliaryAC = new ushort[36], AuxiliaryF4 = new ushort[36],
                    OverrideTarget = new(0xFF, 0), Direction36 = OriginalArmyDirectionSource.ResetDirection36,
                    MapUiByte13C = 0, CommandControl140 = 0, RouteHold56 = 0, RouteGoal1F8 = 0xBD10 };
                current.SetArmyUnitCommanderId(army, 0); current.SetArmyUnitStatusFlag10(army, 0);
                if (armyFlags150 is not null) armyFlags150[army] = 0; //2269E0 byte150 explicit reset.
                if (armyTargets38 is not null) armyTargets38[army] = new(0xFF, 0); //2269E0 target reset.
                _ = OriginalArmyMovementManagerLifecycle.Remove(receiver, current);
                manager = OriginalArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current).Select(x => x.PhysicalSlot!.Value).ToArray();
                if (group != 0 && !Members(group, assignments, manager, pending).Any()) leaders[group] = 0; //282990→227F00.
            }
            current.ClearTemporaryHexList(new(arena.Pending70Descriptor!.Identity));
            var result = state with { NativeArmyLifecycle = arena with { Pool = pool.ExportOwnedState(), Slots = slots, ArmyFlags150 = armyFlags150, Targets38 = armyTargets38,
                Auxiliaries = aux with { ArmyTroops58 = assignments, TroopsLeader2C = leaders,
                    CautionArmy10 = caution10, CautionArmy12 = caution12 } } };
            NativeArmyLifecycleStateValidation.Validate(result);
            foreach (var receiver in pendingOrder) committedReset?.Invoke(receiver.PhysicalSlot!.Value);
            return result;
        }
        catch (InvalidDataException) { return null; }
    }

    internal static void RestoreCurrent(OriginalEventInitializationContext current, NativeArmyLifecycleState arena)
    {
        if (arena.Auxiliaries is not { } aux) return;
        for (int i = 0; i < 501; i++)
        {
            current.SetStartupArmyTroopsSlot58(i, aux.ArmyTroops58[i]);
            current.SetRegistryTablePointer(OriginalRegistryKind.CautionTroops, i, i);
            current.SetRegistryCautionTroopsRawField18(i, aux.CautionTroops18[i]);
        }
        for (int i = 0; i < 1001; i++)
        {
            current.SetRegistryTablePointer(OriginalRegistryKind.Table81B50, i, i);
            current.SetRegistryTable81B50RawField10(i, aux.CautionArmy10[i]); current.SetRegistryTable81B50RawField12(i, aux.CautionArmy12[i]);
        }
        if (aux.AssociatedCommand18Descriptor is { } descriptor)
        { current.SetCurrentAssociatedContainer(new(0)); current.SetAssociatedChainDescriptor(0, new(descriptor.Identity)); }
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
    private static bool GroupValid(int group, ushort?[] assignment, int[] manager, HashSet<int> pending, OriginalEventInitializationContext current)
        => group != 0 && GroupDistrict(group, assignment, manager, pending, current) is >= 1 and <= 51;
    private static int GroupDistrict(int group, ushort?[] assignment, int[] manager, HashSet<int> pending, OriginalEventInitializationContext current)
    {
        foreach (int army in Members(group, assignment, manager, pending))
        {
            int? person = Person(current, Known(current.ReadArmyUnitCommanderId(new(army)), "Army12"));
            return person is int physical && current.ReadPersonNativeValidity(physical) == true
                ? current.ReadPersonDistrictSlot(physical) ?? throw new InvalidDataException("Current commander District is unknown.") : 0;
        }
        return 0;
    }
    private static IEnumerable<int> Members(int group, ushort?[] assignment, int[] manager, HashSet<int> pending)
    {
        if (group is < 1 or > 500) yield break;
        foreach (int army in manager)
            if (Known(assignment[army], "Army58") == group && !pending.Contains(army)) yield return army;
    }
    private static int? Person(OriginalEventInitializationContext current, ushort id) =>
        (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot;
    private static void UpdateLeader(int group, ushort?[] assignments, ushort?[] leaders, int[] manager, HashSet<int> pending,
        NativeArmyLifecycleSlot[] slots, OriginalEventInitializationContext current, OriginalCurrentListPool pool)
    {
        var temporary = pool.AllocateDescriptor();
        if (temporary.Identity is null) throw new InvalidDataException("Current CTroops temporary descriptor allocation failed.");
        var members = Members(group, assignments, manager, pending).ToArray();
        foreach (int army in members)
        {
            var node = current.AllocateRegistryNode(new(temporary.Identity));
            if (node?.Identity is null) throw new InvalidDataException("Current CTroops temporary node allocation failed.");
            current.WriteRegistryNodePayload(node, new(OriginalRegistryKind.Army, army));
        }
        foreach (int army in members)
        {
            ushort commander = Known(slots[army].Commander12, "Army12");
            if (Person(current, commander) == Person(current, Known(leaders[group], "CTroops2C"))) continue;
            if (commander < 6000) leaders[group] = commander;
            break;
        }
        current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary);
    }
    private static void RemoveAll(OriginalEventInitializationContext current, NativeSharedListHandle? descriptor, OriginalRegistryObjectReference target)
    {
        var handle = descriptor is null ? throw new InvalidDataException("Current cleanup container descriptor is unknown.") : new OriginalRegistryListHandle(descriptor.Identity);
        var node = current.ReadRegistryHead(handle) ?? throw new InvalidDataException("Current cleanup container head is unknown.");
        while (node.Identity is not null)
        {
            var next = current.ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current cleanup container next is unknown.");
            var payload = current.ReadRegistryNodePayload(node) ?? throw new InvalidDataException("Current cleanup container payload is unknown.");
            if (payload == target) current.UnlinkArmyReturnTraitNode(handle, node);
            node = next;
        }
    }
    private static OriginalRegistryObjectReference[] Chain(OriginalEventInitializationContext current, NativeSharedListHandle? descriptor)
    {
        var handle = descriptor is null ? throw new InvalidDataException("Current lifecycle chain descriptor is unknown.") : new OriginalRegistryListHandle(descriptor.Identity);
        var values = new List<OriginalRegistryObjectReference>();
        var node = current.ReadRegistryHead(handle) ?? throw new InvalidDataException("Current lifecycle chain head is unknown.");
        while (node.Identity is not null)
        { values.Add(current.ReadRegistryNodePayload(node) ?? throw new InvalidDataException("Current lifecycle payload is unknown."));
          node = current.ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current lifecycle next is unknown."); }
        return values.ToArray();
    }
    private static ushort Known(ushort? value, string owner) => value ?? throw new InvalidDataException($"Current {owner} is unknown.");
    private static bool InteractionEmpty(NativeArmyLifecycleState arena) => arena.DeferredDestructionQueue is { } queue
        ? queue.Manager140DescriptorKey is int key && (uint)key < 64 && queue.Heads10[key] is null
        : arena.Auxiliaries?.InteractionPairs140 is { Length: 0 };
    private static ushort?[] Zero(int count) => Enumerable.Repeat<ushort?>(0, count).ToArray();
    private static void RestoreHeaders(OriginalEventInitializationContext current, NativeArmyLifecycleSlot[] slots)
    {
        foreach (var slot in slots)
        { current.SetArmyUnitTablePointer(slot.PhysicalSlot, slot.PhysicalSlot); current.SetArmyUnitCommanderId(slot.PhysicalSlot, slot.Commander12);
          current.SetArmyUnitStatusFlag10(slot.PhysicalSlot, slot.Status10); }
    }
}
