namespace San14.Core;

public enum NativeArmyDestructionReceiverKind { Army, City, Gate }

/// <summary>A present reference with no physical slot is a known null pointer;
/// an absent reference is an unknown current pointer.</summary>
public sealed record NativeArmyDestructionReceiverReference(NativeArmyDestructionReceiverKind Kind,
    int? PhysicalSlot);

/// <summary>The actual 48-byte SAnnihilate request. Padding2C is not initialized
/// by the native constructor and therefore remains nullable.</summary>
public sealed record NativeArmyDestructionQueuePayload(
    NativeArmyDestructionReceiverReference? VictimArmy0, NativeArmyDestructionReceiverReference? VictimBase8,
    NativeArmyDestructionReceiverReference? AttackerArmy10, NativeArmyDestructionReceiverReference? AttackerBase18,
    uint? Raw20, uint? NativeMode24, uint? Cause28, uint? Padding2C);
public sealed record NativeArmyDestructionQueueNode(NativeArmyDestructionQueuePayload Payload48,
    int? Next30, int? Previous38);
public sealed record NativeArmyDestructionQueueDescriptor(int Key, int? Next8, int? Previous10);

/// <summary>Saved current secondary pool142145730. Its 128 preallocated nodes
/// and 64 descriptor wrappers are independent of the primary shared-node pool.
/// Array positions are physical node/key identities, never process pointers.</summary>
public sealed record NativeArmyDestructionQueueState(string Revision, string Language,
    string ExecutableSha256, string SourceScenarioUri, string SourceScenarioSha256,
    int? Manager140DescriptorKey, int? DescriptorFreeHead, int? DescriptorLiveHead,
    int? DescriptorLiveTail, int DescriptorLiveCount,
    NativeArmyDestructionQueueDescriptor[] Descriptors,
    int?[] Heads10, int?[] Tails18, long[] Counts28, int? FreeNodeHead20,
    long ActiveNodeCount30, NativeArmyDestructionQueueNode[] Nodes);

public sealed partial record NativeArmyLifecycleState
{
    public NativeArmyDestructionQueueState? DeferredDestructionQueue { get; init; }
}

public static class NativeArmyDestructionQueueStateValidation
{
    public static void Validate(NativeArmyLifecycleState lifecycle)
    {
        if (lifecycle.DeferredDestructionQueue is not { } queue) return;
        if (queue.Language != lifecycle.Language || queue.ExecutableSha256 != lifecycle.ExecutableSha256 ||
            queue.SourceScenarioUri != lifecycle.SourceScenarioUri || queue.SourceScenarioSha256 != lifecycle.SourceScenarioSha256)
            throw new InvalidDataException("Native destruction queue belongs to a different current world.");
        Validate(queue);
    }

    /// <summary>Save/load boundary validation; normal source operations use
    /// the existing owned pool without revalidating or hashing it.</summary>
    public static void Validate(NativeArmyDestructionQueueState value)
    {
        const int descriptorCapacity = 64, nodeCapacity = 128;
        if (string.IsNullOrWhiteSpace(value.Revision) || value.Language is not ("SC" or "TC") ||
            value.ExecutableSha256 is not { Length: 64 } || value.SourceScenarioSha256 is not { Length: 64 } ||
            string.IsNullOrWhiteSpace(value.SourceScenarioUri) ||
            value.Descriptors is not { Length: descriptorCapacity } || value.Heads10 is not { Length: descriptorCapacity } ||
            value.Tails18 is not { Length: descriptorCapacity } || value.Counts28 is not { Length: descriptorCapacity } ||
            value.Nodes is not { Length: nodeCapacity })
            throw new InvalidDataException("Native destruction queue physical tables are malformed.");
        var descriptorOwners = new HashSet<int>();
        var liveDescriptors = new HashSet<int>();
        WalkDescriptors(value.DescriptorFreeHead, false);
        WalkDescriptors(value.DescriptorLiveHead, true);
        if (descriptorOwners.Count != descriptorCapacity || value.Manager140DescriptorKey is int manager && !liveDescriptors.Contains(manager))
            throw new InvalidDataException("Native destruction descriptor ownership is incomplete.");
        var nodeOwners = new HashSet<int>();
        long active = 0;
        for (int key = 0; key < descriptorCapacity; key++)
        {
            int? previous = null;
            long count = 0;
            for (int? at = value.Heads10[key]; at is int node; at = value.Nodes[node].Next30)
            {
                CheckNode(node);
                if (value.Nodes[node].Previous38 != previous)
                    throw new InvalidDataException("Native destruction node previous link differs.");
                previous = node;
                count++;
            }
            if (previous != value.Tails18[key] || count != value.Counts28[key])
                throw new InvalidDataException("Native destruction list head, tail or count differs.");
            if (count != 0 && !liveDescriptors.Contains(key))
                throw new InvalidDataException("Native destruction nodes belong to a free descriptor.");
            active += count;
        }
        for (int? at = value.FreeNodeHead20; at is int node; at = value.Nodes[node].Next30) CheckNode(node);
        if (nodeOwners.Count != nodeCapacity || active != value.ActiveNodeCount30)
            throw new InvalidDataException("Native destruction free and live node ownership differs.");

        void CheckNode(int node)
        {
            if ((uint)node >= nodeCapacity || !nodeOwners.Add(node) || value.Nodes[node]?.Payload48 is not { } payload)
                throw new InvalidDataException("Native destruction node ownership is malformed.");
            CheckReceiver(payload.VictimArmy0, true);
            CheckReceiver(payload.AttackerArmy10, true);
            CheckReceiver(payload.VictimBase8, false);
            CheckReceiver(payload.AttackerBase18, false);
        }

        static void CheckReceiver(NativeArmyDestructionReceiverReference? reference, bool army)
        {
            if (reference is null) return;
            bool kind = army ? reference.Kind == NativeArmyDestructionReceiverKind.Army :
                reference.Kind is NativeArmyDestructionReceiverKind.City or NativeArmyDestructionReceiverKind.Gate;
            int capacity = reference.Kind switch
            { NativeArmyDestructionReceiverKind.Army => 501, NativeArmyDestructionReceiverKind.City => 52,
                NativeArmyDestructionReceiverKind.Gate => 11, _ => 0 };
            if (!kind || reference.PhysicalSlot is int physical && (uint)physical >= capacity)
                throw new InvalidDataException("Native destruction receiver identity is malformed.");
        }

        void WalkDescriptors(int? head, bool live)
        {
            int count = 0;
            int? previous = null;
            for (int? at = head; at is int key; at = value.Descriptors[key].Next8)
            {
                if ((uint)key >= descriptorCapacity || !descriptorOwners.Add(key) || value.Descriptors[key] is not { } descriptor ||
                    descriptor.Key != key || live && descriptor.Previous10 != previous)
                    throw new InvalidDataException("Native destruction descriptor chain is malformed.");
                if (live) liveDescriptors.Add(key);
                previous = key;
                count++;
            }
            if (live && (previous != value.DescriptorLiveTail || count != value.DescriptorLiveCount))
                throw new InvalidDataException("Native destruction live descriptor bookkeeping differs.");
        }
    }
}
