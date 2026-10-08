using San14.Core;

namespace San14.Data;

public sealed record OriginalOwnedArmyDestructionAdmission(GameState? State, int? NativeReturn,
    bool Duplicate, int? AllocatedNode, string? UnresolvedOwner)
{
    public bool RequiresImmediate259360 => NativeReturn == 0;
}

/// <summary>Operation-local adapters over the one saved lifecycle and its
/// actual secondary request pool. The narrow old World.ObjectPairs catalogue
/// is not another source of current queue membership.</summary>
public static class OriginalOwnedArmyDestructionCurrent
{
    /// <summary>Normal current queries borrow the saved primary/header view.
    /// Only a writable return/destruction world imports the complete arena.</summary>
    public static void BindOwnedCurrentContext(GameState state, OriginalEventInitializationContext current)
    {
        if (state.NativeArmyLifecycle?.DeferredDestructionQueue is null) return;
        current.BindOwnedDestructionReadOnlyView(state);
    }

    public static bool? ReadArmyExcluded233560(GameState state, OriginalEventInitializationContext current, int physical)
    {
        if (state.NativeArmyLifecycle is not { } arena || physical is < 0 or > 500 ||
            ReadPending70(arena) is not { } pending) return null;
        var receiver = new NativeArmyDestructionReceiverReference(NativeArmyDestructionReceiverKind.Army, physical);
        //233560 matches pending pointers before inspecting any native validity
        //or the secondary descriptor. A missing old queue does not erase this.
        if (pending.Contains(receiver)) return true;
        return arena.DeferredDestructionQueue is { } queue
            ? OriginalArmyDestructionQueue.ReadArmyExcludedByQueue233560(queue, receiver, BindCurrent(state, current)) : null;
    }

    public static bool? ReadBaseExcluded2334B0(GameState state, OriginalEventInitializationContext current, int location)
    {
        if (state.NativeArmyLifecycle?.DeferredDestructionQueue is not { } queue) return null;
        var receiver = BaseReference(location);
        return receiver is null ? null : OriginalArmyDestructionQueue.ReadBaseExcluded2334B0(queue, receiver, BindCurrent(state, current));
    }

    /// <summary>The ordinary Army request fields written at174801. This
    /// enqueues only;450830 still owns259360, followed by the separate pending
    /// consumer28E6D0. Return0 requires an immediate259360 instead.</summary>
    public static OriginalOwnedArmyDestructionAdmission AdmitOrdinaryArmy1745B0(GameState state,
        OriginalEventInitializationContext current, int victimPhysical, int? attackerPhysical, int? attackerBaseLocation)
    {
        if (state.NativeArmyLifecycle?.DeferredDestructionQueue is not { } queue)
            return new(null, null, false, null, "current.saved_secondary_destruction_queue");
        if (victimPhysical is < 0 or > 500 || attackerPhysical is < 0 or > 500)
            return new(null, null, false, null, "current.destruction_Army_physical_pointer");
        var queries = BindCurrent(state, current);
        var victim = new NativeArmyDestructionReceiverReference(NativeArmyDestructionReceiverKind.Army, victimPhysical);
        if (queries.ReadNativeValidity(victim) is not bool victimValid)
            return new(null, null, false, null, "current.destruction_victim_native_validity");
        if (!victimValid) return new(state, null, false, null, null); //1745B0 returns without calling31F160.
        var attacker = new NativeArmyDestructionReceiverReference(NativeArmyDestructionReceiverKind.Army, attackerPhysical);
        var attackerBase = attackerBaseLocation is int location ? BaseReference(location) :
            new NativeArmyDestructionReceiverReference(NativeArmyDestructionReceiverKind.City, null);
        if (attackerBase is null) return new(null, null, false, null, "current.destruction_attacker_Base_pointer");
        bool? attackerValid = queries.ReadNativeValidity(attacker);
        bool? baseValid = attackerValid == true ? false : queries.ReadNativeValidity(attackerBase);
        if (attackerValid is null || baseValid is null)
            return new(null, null, false, null, "current.destruction_attacker_native_validity");
        if (!attackerValid.Value && !baseValid.Value)
        {
            attacker = new(NativeArmyDestructionReceiverKind.Army, null);
            attackerBase = new(NativeArmyDestructionReceiverKind.City, null);
        }
        var payload = new NativeArmyDestructionQueuePayload(victim,
            new(NativeArmyDestructionReceiverKind.City, null), attacker, attackerBase, 0, 1, 0, null);
        var admission = OriginalArmyDestructionQueue.Admit31F160(queue, payload, queries);
        if (admission.NativeReturn is null) return new(null, null, false, null, admission.UnresolvedOwner);
        var result = state with { NativeArmyLifecycle = state.NativeArmyLifecycle with { DeferredDestructionQueue = admission.State } };
        return new(result, admission.NativeReturn, admission.Duplicate, admission.AllocatedNode, null);
    }

    public static IOriginalArmyDestructionQueueCurrentQueries BindCurrent(GameState state, OriginalEventInitializationContext current)
        => new CurrentQueries(state, current);

    private static NativeArmyDestructionReceiverReference? BaseReference(int location) => location switch
    {
        >= 0 and <= 51 => new(NativeArmyDestructionReceiverKind.City, location),
        >= 52 and <= 61 => new(NativeArmyDestructionReceiverKind.Gate, location - 51),
        _ => null
    };

    private static NativeArmyDestructionReceiverReference[]? ReadPending70(NativeArmyLifecycleState arena)
    {
        if (arena.Pending70Descriptor is not { } handle) return null;
        if (handle.Identity is null) return [];
        if (!arena.Pool.Descriptors.TryGetValue(handle.Identity.Value, out var descriptor)) return null;
        var result = new List<NativeArmyDestructionReceiverReference>();
        for (int? at = descriptor.Head; at is int node; at = arena.Pool.Nodes[node].Next)
        {
            if (!arena.Pool.Nodes.TryGetValue(node, out var value) || value.Payload is not { Kind: "Registry:Army" } payload)
                return null;
            result.Add(new(NativeArmyDestructionReceiverKind.Army, payload.Identity));
        }
        return result.ToArray();
    }

    private sealed class CurrentQueries(GameState state, OriginalEventInitializationContext current)
        : IOriginalArmyDestructionQueueCurrentQueries
    {
        public bool? ReadNativeValidity(NativeArmyDestructionReceiverReference reference)
        {
            if (reference.PhysicalSlot is not int physical) return false;
            if (reference.Kind != NativeArmyDestructionReceiverKind.Army)
                return current.ReadRegistryObjectNativeValidity(OriginalArmyDestructionQueue.ToOriginalRegistryReference(reference));
            if (state.NativeArmyLifecycle is not { Slots.Length: 501 } arena || physical is < 0 or > 500) return null;
            var slot = arena.Slots[physical];
            return slot.Commander12 is ushort commander ? commander == 0 ? false : slot.Status10 is byte status ? status != 0 : null : null;
        }
        public ushort? ReadBaseObjectLink4E(NativeArmyDestructionReceiverReference reference) =>
            current.ReadBaseObjectLink(new(reference.Kind == NativeArmyDestructionReceiverKind.City
                ? OriginalBaseKind.City : OriginalBaseKind.Gate, reference.PhysicalSlot));
        public byte? ReadArmyRawForce22CD80(NativeArmyDestructionReceiverReference reference)
        {
            if (reference.PhysicalSlot is not int physical || state.NativeArmyLifecycle is not { Slots.Length: 501 } arena ||
                physical is < 0 or > 500 || arena.Slots[physical].Commander12 is not ushort commander) return null;
            var person = commander <= 6000 ? current.ReadPersonLookupReference(commander) : current.ReadTransferPersonTableReference(0);
            if (person?.PhysicalSlot is not int identity || current.ReadPersonNativeValidity(identity) is not bool valid) return null;
            if (valid) return current.ReadPersonOldForceSlot(identity);
            var district = current.ReadDistrictTableReference(0);
            if (district?.PhysicalSlot is not int physicalDistrict || current.ReadDistrictNativeValidity(physicalDistrict) is not bool districtValid)
                return null;
            return districtValid ? current.ReadDistrictRawByte(physicalDistrict, 0x10) : (byte)0;
        }
    }
}
