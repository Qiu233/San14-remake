using San14.Core;

namespace San14.Data;

public sealed partial class OriginalCurrentListPool
{
    /// <summary>Exact owned allocator image. Free descriptor order is stack
    /// pop order; reserved entries still in that stack are retained.</summary>
    public NativeSharedListPoolState ExportOwnedState()
    {
        if (!OwnsArmyReturnProfile()) throw new InvalidDataException("Only an actual owned pool can be saved.");
        var image = new NativeSharedListPoolState(DescriptorCapacity, NodeCapacity, ActiveNodeCount,
            _createdNodes, _freeNodeHead.Identity, _state.ReadTemporaryHexListCount(new(null)), _freeDescriptors.ToArray(), _liveDescriptors.Order().ToArray(),
            _state.ExportOwnedDescriptors(DescriptorCapacity), _state.ExportOwnedNodes(_createdNodes));
        NativeArmyLifecycleStateValidation.ValidatePool(image); return image;
    }

    /// <summary>Replaces a freshly built context's scratch arena with the
    /// independently saved current arena. No chain is reconstructed from units.</summary>
    public static OriginalCurrentListPool ImportOwnedState(OriginalEventInitializationContext context,
        NativeSharedListPoolState image)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(image);
        NativeArmyLifecycleStateValidation.ValidatePool(image);
        // Decode every payload before touching the destination.
        var payloads = image.Nodes.ToDictionary(x => x.Key, x => OriginalEventInitializationContext.DecodeOwnedPayload(x.Value.Payload));
        context.ReplaceOwnedSharedListStorage(image, payloads);
        context.HexListAllocationProfile = null;
        var pool = new OriginalCurrentListPool(context, image.DescriptorCapacity, image.NodeCapacity);
        // Constructor creates empty rows. Restore actual rows, not its defaults.
        context.ReplaceOwnedSharedListStorage(image, payloads);
        pool._freeDescriptors.Clear(); foreach (int key in image.FreeDescriptors.Reverse()) pool._freeDescriptors.Push(key);
        pool._liveDescriptors.Clear(); pool._liveDescriptors.UnionWith(image.LiveDescriptors);
        pool._createdNodes = image.CreatedNodeCount; pool.ActiveNodeCount = image.ActiveNodeCount;
        pool._freeNodeHead = new(image.FreeNodeHead);
        return pool;
    }
}

public sealed partial class OriginalEventInitializationContext
{
    public OriginalCurrentListPool? ReadOwnedCurrentListPool() =>
        HexListAllocationProfile?.TemporaryDescriptorAllocator?.Target as OriginalCurrentListPool;

    internal Dictionary<int, NativeSharedListDescriptor> ExportOwnedDescriptors(int capacity)
    {
        var result = new Dictionary<int, NativeSharedListDescriptor>();
        for (int i = 0; i < capacity; i++)
        {
            if (_hexListHeads.GetValueOrDefault(i) is not { } head || _hexListTails.GetValueOrDefault(i) is not { } tail)
                throw new InvalidDataException("Owned descriptor pointer field is unknown.");
            result.Add(i, new(head.Identity, tail.Identity, _hexListCounts.GetValueOrDefault(i)));
        }
        return result;
    }
    internal Dictionary<int, NativeSharedListNode> ExportOwnedNodes(int count)
    {
        var result = new Dictionary<int, NativeSharedListNode>();
        for (int i = 1; i <= count; i++)
        {
            if (_hexListNext.GetValueOrDefault(i) is not { } next || _hexListPrevious.GetValueOrDefault(i) is not { } previous)
                throw new InvalidDataException("Owned shared node pointer field is unknown.");
            result.Add(i, new(next.Identity, previous.Identity,
                _nativeListPayloads.TryGetValue(i, out var value) ? EncodeOwnedPayload(value) : null));
        }
        return result;
    }
    internal void ReplaceOwnedSharedListStorage(NativeSharedListPoolState image, Dictionary<int, object?> payloads)
    {
        _hexListHeads.Clear(); _hexListTails.Clear(); _hexListCounts.Clear();
        _hexListNext.Clear(); _hexListPrevious.Clear(); _nativeListPayloads.Clear();
        foreach (var row in image.Descriptors)
        { _hexListHeads[row.Key] = new(row.Value.Head); _hexListTails[row.Key] = new(row.Value.Tail); _hexListCounts[row.Key] = row.Value.Count; }
        _hexListCounts[NativeNullHexListKey] = image.NullDescriptorCount;
        foreach (var row in image.Nodes)
        {
            _hexListNext[row.Key] = new(row.Value.Next); _hexListPrevious[row.Key] = new(row.Value.Previous);
            if (row.Value.Payload is not null) _nativeListPayloads[row.Key] = payloads[row.Key];
        }
    }
    private static NativeSharedListPayload EncodeOwnedPayload(object? value) => value switch
    {
        null => new("Null", null),
        OriginalRegistryObjectReference x => new("Registry:" + x.Kind, x.PhysicalSlot),
        OriginalBaseReference x => new("Base:" + x.Kind, x.PhysicalSlot),
        OriginalHexReference x => new("Hex", x.PhysicalSlot),
        OriginalPersonalityReference x => new("Personality", x.PhysicalSlot),
        OriginalPersonLinkedEntityReference x => new("LinkedEntity", x.Identity),
        OriginalTransferPersonReference x => new("Person", x.PhysicalSlot),
        OriginalReturnStaticTablePayload { TableKind: "State" } x => new("StaticTable:State", x.PhysicalSlot),
        OriginalReturnStaticTablePayload { TableKind: "Region" } x => new("StaticTable:Region", x.PhysicalSlot),
        OriginalForcePolicyScratchReference x => new("ForcePolicyScratch", x.Identity),
        _ => throw new InvalidDataException("Unsupported owned shared-list payload: " + value.GetType().Name)
    };
    internal static object? DecodeOwnedPayload(NativeSharedListPayload? value)
    {
        if (value is null) return null;
        if (value.Kind.StartsWith("Registry:", StringComparison.Ordinal) &&
            Enum.TryParse<OriginalRegistryKind>(value.Kind[9..], out var registry) && Enum.IsDefined(registry))
            return new OriginalRegistryObjectReference(registry, value.Identity);
        if (value.Kind.StartsWith("Base:", StringComparison.Ordinal) &&
            Enum.TryParse<OriginalBaseKind>(value.Kind[5..], out var kind) && Enum.IsDefined(kind))
            return new OriginalBaseReference(kind, value.Identity);
        return value.Kind switch
        {
            "Null" when value.Identity is null => null,
            "Hex" => new OriginalHexReference(value.Identity),
            "Personality" => new OriginalPersonalityReference(value.Identity),
            "LinkedEntity" => new OriginalPersonLinkedEntityReference(value.Identity),
            "Person" => new OriginalTransferPersonReference(value.Identity),
            "StaticTable:State" => new OriginalReturnStaticTablePayload("State", value.Identity),
            "StaticTable:Region" => new OriginalReturnStaticTablePayload("Region", value.Identity),
            "ForcePolicyScratch" => new OriginalForcePolicyScratchReference(value.Identity),
            _ => throw new InvalidDataException("Unknown saved owned-list payload kind: " + value.Kind)
        };
    }
}
