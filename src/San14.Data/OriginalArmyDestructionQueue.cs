using San14.Core;
using OriginalArmyDestructionQueuePayload = San14.Core.NativeArmyDestructionQueuePayload;
using OriginalArmyDestructionQueueNode = San14.Core.NativeArmyDestructionQueueNode;
using OriginalArmyDestructionQueueDescriptor = San14.Core.NativeArmyDestructionQueueDescriptor;
using OriginalArmyDestructionQueueState = San14.Core.NativeArmyDestructionQueueState;

namespace San14.Data;

public sealed record OriginalArmyDestructionQueueAllocation(OriginalArmyDestructionQueueState State, int? Identity);
public sealed record OriginalArmyDestructionQueueAdmission(OriginalArmyDestructionQueueState State,
    int? NativeReturn, int? AllocatedNode, bool Duplicate, string? UnresolvedOwner);
public sealed record OriginalArmyDestructionQueueEntry(int Identity, OriginalArmyDestructionQueuePayload Payload);

public interface IOriginalArmyDestructionQueueCurrentQueries
{
    bool? ReadNativeValidity(NativeArmyDestructionReceiverReference reference);
    ushort? ReadBaseObjectLink4E(NativeArmyDestructionReceiverReference reference);
    byte? ReadArmyRawForce22CD80(NativeArmyDestructionReceiverReference reference) => null;
}

/// <summary>Independent secondary pool142145730, not the primary shared-node
/// arena.9365D0→935AB0 creates128x40 nodes and64x18 descriptor wrappers.
///31ADB0 allocates the manager140's first descriptor through018A30. Normal
///queue clear returns a whole chain through3261A0→33F450, preserving payloads
///and node identities. The deferred259360 business consumer is separate.</summary>
public static class OriginalArmyDestructionQueue
{
    public const string Revision = "san14:ce-owned-army-destruction-queue-20261007";
    public const int NodeCapacity = 128;
    public const int DescriptorCapacity = 64;

    public static GameState InstallFreshOwned(GameState state)
    {
        if (state.NativeArmyLifecycle?.DeferredDestructionQueue is not null)
            throw new InvalidDataException("Current destruction queue is already installed.");
        var queue = CreateFreshOwned(state);
        return state with { NativeArmyLifecycle = state.NativeArmyLifecycle! with { DeferredDestructionQueue = queue } };
    }

    /// <summary>Explicit fresh producer for the already installed CE numeric
    ///world. This result must be published into the host's saved owner once;
    ///current queue operations never call this producer.</summary>
    public static OriginalArmyDestructionQueueState CreateFreshOwned(GameState state)
    {
        var arena = state.NativeArmyLifecycle;
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            arena is null || state.NativeArmySimulation?.Parameters is not { } parameters ||
            arena.Language != parameters.Language || arena.ExecutableSha256 != parameters.ExecutableSha256 ||
            state.ContentHashes.GetValueOrDefault(arena.SourceScenarioUri) != arena.SourceScenarioSha256 ||
            !(parameters.Language == "SC" && parameters.ExecutableSha256 == OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256 ||
              parameters.Language == "TC" && parameters.ExecutableSha256 == OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256))
            throw new InvalidDataException("Destruction queue requires its explicit fresh current CE constructor owner.");
        var zero = new OriginalArmyDestructionQueuePayload(new(NativeArmyDestructionReceiverKind.Army, null),
            new(NativeArmyDestructionReceiverKind.City, null), new(NativeArmyDestructionReceiverKind.Army, null),
            new(NativeArmyDestructionReceiverKind.City, null), 0, 0, 4, null);
        var stateBeforeWrapper = new OriginalArmyDestructionQueueState(Revision, parameters.Language,
            parameters.ExecutableSha256, arena.SourceScenarioUri, arena.SourceScenarioSha256,
            null, 0, null, null, 0,
            Enumerable.Range(0, DescriptorCapacity).Select(i => new OriginalArmyDestructionQueueDescriptor(i,
                i + 1 < DescriptorCapacity ? i + 1 : null, null)).ToArray(),
            new int?[DescriptorCapacity], new int?[DescriptorCapacity], new long[DescriptorCapacity], 0, 0,
            Enumerable.Range(0, NodeCapacity).Select(i => new OriginalArmyDestructionQueueNode(zero,
                i + 1 < NodeCapacity ? i + 1 : null, null)).ToArray());
        var wrapper = AllocateDescriptor(stateBeforeWrapper, false);
        return wrapper.State with { Manager140DescriptorKey = wrapper.Identity };
    }

    /// <summary>018A30 on the independent secondary descriptor-wrapper pool.
    ///No descriptor means the actual native allocation failed.</summary>
    public static OriginalArmyDestructionQueueAllocation AllocateDescriptor(OriginalArmyDestructionQueueState state, bool tail)
    {
        if (state.DescriptorFreeHead is not int key) return new(state, null);
        var descriptors = state.Descriptors.ToArray(); var node = descriptors[key];
        int? head = state.DescriptorLiveHead, end = state.DescriptorLiveTail;
        descriptors[key] = node with { Next8 = null, Previous10 = null };
        if (head is null || end is null) head = end = key;
        else if (tail)
        {
            descriptors[end.Value] = descriptors[end.Value] with { Next8 = key };
            descriptors[key] = descriptors[key] with { Previous10 = end }; end = key;
        }
        else
        {
            descriptors[key] = descriptors[key] with { Next8 = head };
            descriptors[head.Value] = descriptors[head.Value] with { Previous10 = key }; head = key;
        }
        return new(state with { Descriptors = descriptors, DescriptorFreeHead = node.Next8,
            DescriptorLiveHead = head, DescriptorLiveTail = end,
            DescriptorLiveCount = state.DescriptorLiveCount + 1 }, key);
    }

    /// <summary>33FFE0. Allocation is from the saved128-node free chain; it
    ///does not grow on demand and does not erase the old48-byte payload.</summary>
    public static OriginalArmyDestructionQueueAllocation AllocateNode(OriginalArmyDestructionQueueState state, int key, bool tail)
    {
        if ((uint)key >= DescriptorCapacity || state.FreeNodeHead20 is not int identity) return new(state, null);
        var nodes = state.Nodes.ToArray(); var before = nodes[identity];
        var heads = state.Heads10.ToArray(); var tails = state.Tails18.ToArray(); var counts = state.Counts28.ToArray();
        nodes[identity] = before with { Next30 = null, Previous38 = null };
        if (heads[key] is null || tails[key] is null) heads[key] = tails[key] = identity;
        else if (tail)
        {
            nodes[tails[key]!.Value] = nodes[tails[key]!.Value] with { Next30 = identity };
            nodes[identity] = nodes[identity] with { Previous38 = tails[key] }; tails[key] = identity;
        }
        else
        {
            nodes[identity] = nodes[identity] with { Next30 = heads[key] };
            nodes[heads[key]!.Value] = nodes[heads[key]!.Value] with { Previous38 = identity }; heads[key] = identity;
        }
        counts[key]++;
        return new(state with { Nodes = nodes, Heads10 = heads, Tails18 = tails, Counts28 = counts,
            FreeNodeHead20 = before.Next30, ActiveNodeCount30 = state.ActiveNodeCount30 + 1 }, identity);
    }

    /// <summary>31F160. Native return1 means this event was handled, including
    ///a duplicate or failed node allocation. It does not prove a node exists.</summary>
    public static OriginalArmyDestructionQueueAdmission Admit31F160(OriginalArmyDestructionQueueState state,
        OriginalArmyDestructionQueuePayload payload, IOriginalArmyDestructionQueueCurrentQueries current)
    {
        int key = ManagerKey(state);
        if (state.Counts28[key] >= NodeCapacity) return new(state, 0, null, false, null);
        try
        {
            for (int? at = state.Heads10[key]; at is int identity; at = state.Nodes[identity].Next30)
            {
                var old = state.Nodes[identity].Payload48;
                bool same;
                //The Army getters are reached only when BOTH pointers are
                //currently valid. Otherwise compare currently valid Bases4E.
                if (Valid(old.VictimArmy0, "current.existing_victimArmy") && Valid(payload.VictimArmy0, "current.incoming_victimArmy"))
                    same = old.VictimArmy0!.PhysicalSlot == payload.VictimArmy0!.PhysicalSlot;
                else
                    same = Valid(old.VictimBase8, "current.existing_victimBase") && Valid(payload.VictimBase8, "current.incoming_victimBase") &&
                        Link(old.VictimBase8!) == Link(payload.VictimBase8!);
                if (same) return new(state, 1, null, true, null);
            }
            var allocated = AllocateNode(state, key, true);
            if (allocated.Identity is not int node) return new(state, 1, null, false, null);
            var nodes = allocated.State.Nodes.ToArray(); nodes[node] = nodes[node] with { Payload48 = payload };
            return new(allocated.State with { Nodes = nodes }, 1, node, false, null);

            bool Valid(NativeArmyDestructionReceiverReference? reference, string owner)
            {
                if (reference is null) throw new Missing(owner);
                if (reference.PhysicalSlot is null) return false;
                return current.ReadNativeValidity(reference) ?? throw new Missing(owner + ".native_validity");
            }
            ushort Link(NativeArmyDestructionReceiverReference reference) => current.ReadBaseObjectLink4E(reference)
                ?? throw new Missing("current.victimBase4E");
        }
        catch (Missing error) { return new(state, null, null, false, error.Message); }
    }

    /// <summary>233560's secondary manager140 branch only. The attacker
    /// branch reads current Base validity, Army validity and raw District10
    /// force in that order, before the valid victim-Army fallback. Forces
    ///46..51 cannot exclude the attacker. Missing reached inputs remain unknown.</summary>
    public static bool? ReadArmyExcludedByQueue233560(OriginalArmyDestructionQueueState state,
        NativeArmyDestructionReceiverReference army, IOriginalArmyDestructionQueueCurrentQueries current)
    {
        int key = ManagerKey(state);
        try
        {
            for (int? at = state.Heads10[key]; at is int identity; at = state.Nodes[identity].Next30)
            {
                var payload = state.Nodes[identity].Payload48;
                if (Valid(payload.VictimBase8) && Valid(payload.AttackerArmy10))
                {
                    byte force = current.ReadArmyRawForce22CD80(payload.AttackerArmy10!)
                        ?? throw new Missing("current.attackerArmy_raw_force22CD80");
                    if (force != 0x33 && unchecked((uint)(force - 0x2e)) > 4 && payload.AttackerArmy10 == army)
                        return true;
                }
                if (Valid(payload.VictimArmy0) && payload.VictimArmy0 == army) return true;
            }
            return false;

            bool Valid(NativeArmyDestructionReceiverReference? reference)
            {
                if (reference is null) throw new Missing("current.destruction_receiver_pointer");
                if (reference.PhysicalSlot is null) return false;
                return current.ReadNativeValidity(reference) ?? throw new Missing("current.destruction_receiver_native_validity");
            }
        }
        catch (Missing) { return null; }
    }

    /// <summary>2334B0: only current valid VictimBase8 pointers in the actual
    ///secondary manager140 list. No Army/attacker/raw-force query and no
    ///primary managerF0 membership are involved.</summary>
    public static bool? ReadBaseExcluded2334B0(OriginalArmyDestructionQueueState state,
        NativeArmyDestructionReceiverReference target, IOriginalArmyDestructionQueueCurrentQueries current)
    {
        int key = ManagerKey(state);
        for (int? at = state.Heads10[key]; at is int identity; at = state.Nodes[identity].Next30)
        {
            var victim = state.Nodes[identity].Payload48.VictimBase8;
            if (victim is null) return null;
            if (victim.PhysicalSlot is null) continue;
            bool? valid = current.ReadNativeValidity(victim);
            if (valid is null) return null;
            if (valid.Value && victim == target) return true;
        }
        return false;
    }

    /// <summary>Complete233560: current primary pending70 pointer order is
    /// checked first, without any validity query. It belongs to the primary
    /// lifecycle owner and is deliberately not duplicated in this queue.</summary>
    public static bool? ReadArmyExcluded233560(OriginalArmyDestructionQueueState state,
        IEnumerable<NativeArmyDestructionReceiverReference?> pending70PointerOrder,
        NativeArmyDestructionReceiverReference army, IOriginalArmyDestructionQueueCurrentQueries current)
    {
        foreach (var pending in pending70PointerOrder)
        {
            if (pending is null) return null;
            if (pending == army) return true;
        }
        return ReadArmyExcludedByQueue233560(state, army, current);
    }

    public static OriginalRegistryObjectReference ToOriginalRegistryReference(NativeArmyDestructionReceiverReference reference)
        => new(reference.Kind switch
        {
            NativeArmyDestructionReceiverKind.Army => OriginalRegistryKind.Army,
            NativeArmyDestructionReceiverKind.City => OriginalRegistryKind.City,
            NativeArmyDestructionReceiverKind.Gate => OriginalRegistryKind.Gate,
            _ => throw new ArgumentOutOfRangeException(nameof(reference))
        }, reference.PhysicalSlot);

    public static IOriginalArmyDestructionQueueCurrentQueries BindCurrent(OriginalEventInitializationContext current)
        => new CurrentQueries(current);

    public static OriginalArmyDestructionQueueEntry[] ReadPointerOrder(OriginalArmyDestructionQueueState state)
    {
        int key = ManagerKey(state); var entries = new List<OriginalArmyDestructionQueueEntry>();
        for (int? at = state.Heads10[key]; at is int identity; at = state.Nodes[identity].Next30)
            entries.Add(new(identity, state.Nodes[identity].Payload48));
        return entries.ToArray();
    }

    /// <summary>Only after450830's current linked traversal completed.33F450
    ///returns the chain at its head, preserving every payload and internal
    ///previous link. It never releases the manager's descriptor wrapper.</summary>
    public static OriginalArmyDestructionQueueState Clear3261A0(OriginalArmyDestructionQueueState state)
        => ClearDescriptor(state, ManagerKey(state));
    public static OriginalArmyDestructionQueueState ClearDescriptor(OriginalArmyDestructionQueueState state, int key)
    {
        if ((uint)key >= DescriptorCapacity) return state;
        if (state.Heads10[key] is not int head) return state;
        int tail = state.Tails18[key] ?? throw new InvalidDataException("Secondary queue tail is missing.");
        var nodes = state.Nodes.ToArray(); nodes[tail] = nodes[tail] with { Next30 = state.FreeNodeHead20 };
        var heads = state.Heads10.ToArray(); var tails = state.Tails18.ToArray(); var counts = state.Counts28.ToArray();
        long count = counts[key]; heads[key] = tails[key] = null; counts[key] = 0;
        return state with { Nodes = nodes, Heads10 = heads, Tails18 = tails, Counts28 = counts,
            FreeNodeHead20 = head, ActiveNodeCount30 = state.ActiveNodeCount30 - count };
    }

    /// <summary>Load/import validation. Normal appends do not revalidate or
    ///fingerprint an internal current pool.</summary>
    public static void ValidateSaved(OriginalArmyDestructionQueueState state)
    {
        if (state.Revision != Revision) throw new InvalidDataException("Saved secondary destruction queue revision differs.");
        NativeArmyDestructionQueueStateValidation.Validate(state);
    }
    private sealed class CurrentQueries(OriginalEventInitializationContext current) : IOriginalArmyDestructionQueueCurrentQueries
    {
        public bool? ReadNativeValidity(NativeArmyDestructionReceiverReference reference)
            => current.ReadRegistryObjectNativeValidity(ToOriginalRegistryReference(reference));
        public ushort? ReadBaseObjectLink4E(NativeArmyDestructionReceiverReference reference)
            => current.ReadBaseObjectLink(new(reference.Kind == NativeArmyDestructionReceiverKind.City
                ? OriginalBaseKind.City : OriginalBaseKind.Gate, reference.PhysicalSlot));
        public byte? ReadArmyRawForce22CD80(NativeArmyDestructionReceiverReference reference)
            => current.ReadArmyUnitRawForceSlot(new(reference.PhysicalSlot));
    }
    private static int ManagerKey(OriginalArmyDestructionQueueState state) => state.Manager140DescriptorKey is int key &&
        (uint)key < DescriptorCapacity ? key : throw new InvalidDataException("Current manager140 descriptor wrapper is unknown.");
    private sealed class Missing(string owner) : Exception(owner);
}
