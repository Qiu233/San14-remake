namespace San14.Data;

/// <summary>Owned manager+60 descriptor, shared list nodes and allocator. The
/// inherited Army validity must read the current pooled receiver header. The
/// unlink operation implements018920: head/tail and adjacent links, node return
/// to the free chain, descriptor count and global active count. This interface
/// supplies container operations, not death/disband admission or save ownership.</summary>
public interface IOriginalArmyMovementManagerLifecycleState : IOriginalObjectRegistrationState
{
    void UnlinkArmyManagerNode(OriginalRegistryListHandle descriptor, OriginalRegistryListNodeReference node);
}

public sealed record OriginalArmyMovementManagerRemoval(bool NativeReturnValue, int RemovedNodeCount);

/// <summary>TC31F430 registrar and32B450→3DFB50 remover. Pointer identity is
/// represented by the pinned Army pool slot, never Core UnitId or creation
/// order. No persistent owner is attached, reconstructed or repaired here.</summary>
public static class OriginalArmyMovementManagerLifecycle
{
    public const string Revision = "san14:ce-army-manager60-container-lifecycle-20261003";

    /// <summary>Tail insertion after current native validity and pointer dedup.
    /// Native true also includes known allocation failure; it is not a receipt
    /// that the new Army belongs to manager+60. Query the owned chain separately.</summary>
    public static bool Register(OriginalRegistryObjectReference? army,
        IOriginalArmyMovementManagerLifecycleState state)
    {
        RequireArmy(army); ArgumentNullException.ThrowIfNull(state);
        return OriginalObjectRegistration.Register(army, state);
    }

    /// <summary>No native validity check: cleanup resets the header before
    /// calling this remover. The wrapper first finds a match, then the generic
    /// container removes every equal pointer, preserving every other node.</summary>
    public static OriginalArmyMovementManagerRemoval Remove(OriginalRegistryObjectReference? army,
        IOriginalArmyMovementManagerLifecycleState state)
    {
        RequireArmy(army); ArgumentNullException.ThrowIfNull(state);
        var descriptor = Known(state.ReadRegistryDescriptor(OriginalRegistryKind.Army), "descriptor60");
        if (!Contains(army!, descriptor, state)) return new(false, 0);
        if (LockFlag(state)) state.EnterRegistryLock();
        //3DFB50 reads the container descriptor after taking its current lock.
        descriptor = Known(state.ReadRegistryDescriptor(OriginalRegistryKind.Army), "remove descriptor60");
        var node = Known(state.ReadRegistryHead(descriptor), "remove head");
        var visited = new HashSet<int>();
        int removed = 0;
        while (node.Identity is int identity)
        {
            Visit(visited, identity);
            var payload = Known(state.ReadRegistryNodePayload(node), "remove payload");
            //The next pointer is retained before018920 returns this node to
            //the allocator free chain (where node+8 has a different meaning).
            var next = Known(state.ReadRegistryNodeNext(node), "remove next");
            if (payload == army)
            {
                state.UnlinkArmyManagerNode(descriptor, node);
                removed++;
            }
            node = next;
        }
        if (LockFlag(state)) state.ExitRegistryLock();
        return new(true, removed);
    }

    /// <summary>The actual container order only. A caller still needs the
    /// same-stage current slot→Unit mapping and complete membership validation
    /// before satisfying IOriginalArmyMarchCurrentQueries. Header reset does
    /// not by itself remove a pointer from this chain.</summary>
    public static IReadOnlyList<OriginalRegistryObjectReference> ReadCurrentPointerOrder(
        IOriginalArmyMovementManagerLifecycleState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var descriptor = Known(state.ReadRegistryDescriptor(OriginalRegistryKind.Army), "descriptor60");
        var node = Known(state.ReadRegistryHead(descriptor), "head");
        var visited = new HashSet<int>();
        var result = new List<OriginalRegistryObjectReference>();
        while (node.Identity is int identity)
        {
            Visit(visited, identity);
            var payload = Known(state.ReadRegistryNodePayload(node), "payload");
            RequireArmy(payload);
            result.Add(payload);
            node = Known(state.ReadRegistryNodeNext(node), "next");
        }
        return result.AsReadOnly();
    }

    private static bool Contains(OriginalRegistryObjectReference army, OriginalRegistryListHandle descriptor,
        IOriginalArmyMovementManagerLifecycleState state)
    {
        var node = Known(state.ReadRegistryHead(descriptor), "dedup head");
        var visited = new HashSet<int>();
        while (node.Identity is int identity)
        {
            Visit(visited, identity);
            if (Known(state.ReadRegistryNodePayload(node), "dedup payload") == army) return true;
            node = Known(state.ReadRegistryNodeNext(node), "dedup next");
        }
        return false;
    }
    private static void RequireArmy(OriginalRegistryObjectReference? army)
    {
        if (army is null) throw new InvalidDataException("Current Army pointer is unknown.");
        if (army.Kind != OriginalRegistryKind.Army)
            throw new InvalidDataException("Movement manager accepts only Army receiver pointers.");
    }
    private static bool LockFlag(IOriginalArmyMovementManagerLifecycleState state)
        => state.ReadRegistryLockEnabled() ?? throw new InvalidDataException("Current manager lock flag is unknown.");
    private static T Known<T>(T? value, string field) where T : class
        => value ?? throw new InvalidDataException($"Current Army manager {field} is unknown.");
    private static void Visit(HashSet<int> visited, int identity)
    {
        if (!visited.Add(identity)) throw new InvalidDataException("Army manager chain contains a node cycle.");
    }
}
