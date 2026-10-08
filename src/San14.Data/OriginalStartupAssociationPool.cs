namespace San14.Data;

/// <summary>The secondary node stores four pointers at0/8/10/18. Its next
/// link at30 and head/count tables belong to21467A0, separate from the primary
/// registry/hex pool. A copied payload remains a snapshot of all four pointers.</summary>
public sealed record OriginalStartupAssociationPayload(
    OriginalRegistryObjectReference? Field0, OriginalRegistryObjectReference? Field8,
    OriginalRegistryObjectReference? Field10, OriginalRegistryObjectReference? Field18);

public sealed record OriginalStartupAssociationNode(int? Identity);

/// <summary>Explicitly allocated independent startup association storage.
/// Capacities are provided by the host. Null return means unknown; a reference
/// with null identity means a known null pointer.</summary>
public sealed class OriginalStartupAssociationPool
{
    private readonly object _synchronization = new();
    private readonly OriginalStartupAssociationNode?[] _heads, _tails;
    private readonly int?[] _counts;
    private readonly Dictionary<int, OriginalStartupAssociationNode?> _next = [];
    private readonly Dictionary<int, OriginalStartupAssociationPayload?> _payloads = [];
    private OriginalStartupAssociationNode _freeHead = new(null);
    private int _createdNodes;
    public int DescriptorCapacity { get; }
    public int NodeCapacity { get; }
    public int ActiveNodeCount { get; private set; }
    public bool? Present { get; set; } = true;
    public bool? HeadTablePresent { get; set; } = true;

    public OriginalStartupAssociationPool(int descriptorCapacity, int nodeCapacity)
    {
        if (descriptorCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(descriptorCapacity));
        if (nodeCapacity < 0) throw new ArgumentOutOfRangeException(nameof(nodeCapacity));
        DescriptorCapacity = descriptorCapacity;
        NodeCapacity = nodeCapacity;
        _heads = new OriginalStartupAssociationNode?[descriptorCapacity];
        _tails = new OriginalStartupAssociationNode?[descriptorCapacity];
        _counts = new int?[descriptorCapacity];
        Array.Fill(_heads, new(null));
        Array.Fill(_tails, new(null));
        Array.Fill(_counts, 0);
    }

    public void EnterLock() => Monitor.Enter(_synchronization);
    public void ExitLock() => Monitor.Exit(_synchronization);
    public OriginalStartupAssociationNode? ReadHead(int key)
    {
        //232860 does not test secondary pool presence before this head lookup.
        if (!Known(HeadTablePresent, "head table presence")) return new(null);
        return (uint)key < (uint)DescriptorCapacity ? _heads[key] : new(null);
    }
    public OriginalStartupAssociationNode? ReadNext(OriginalStartupAssociationNode node)
        => _next.GetValueOrDefault(Node(node));
    public OriginalStartupAssociationPayload? ReadPayload(OriginalStartupAssociationNode node)
        => _payloads.GetValueOrDefault(Node(node));
    public int? ReadCount(int key) { Key(key); return _counts[key]; }
    public void SetHead(int key, OriginalStartupAssociationNode? node) { Key(key); _heads[key] = node; }
    public void SetCount(int key, int? count) { Key(key); _counts[key] = count; }
    public void SetNext(OriginalStartupAssociationNode node, OriginalStartupAssociationNode? next) => _next[Node(node)] = next;
    public void SetPayload(OriginalStartupAssociationNode node, OriginalStartupAssociationPayload? payload) => _payloads[Node(node)] = payload;

    public OriginalStartupAssociationNode Append(int key, OriginalStartupAssociationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Key(key);
        if (!Known(Present, "pool presence")) return new(null);
        OriginalStartupAssociationNode node;
        if (_freeHead.Identity.HasValue)
        {
            node = _freeHead;
            _freeHead = KnownReference(ReadNext(node), "free node next");
        }
        else
        {
            if (_createdNodes >= NodeCapacity) return new(null);
            node = new(++_createdNodes);
        }
        OriginalStartupAssociationNode head = KnownReference(_heads[key], "append head");
        _next[Node(node)] = new(null);
        if (head.Identity.HasValue)
            _next[Node(KnownReference(_tails[key], "append tail"))] = node;
        else _heads[key] = node;
        _tails[key] = node;
        int count = Known(_counts[key], "append count");
        _counts[key] = checked(count + 1);
        ActiveNodeCount = checked(ActiveNodeCount + 1);
        _payloads[Node(node)] = payload;
        return node;
    }

    /// <summary>The caller already saved/validated the key and holds the lock.
    /// Return the whole chain without traversing it or erasing any payload.</summary>
    public void Clear(int key)
    {
        Key(key);
        OriginalStartupAssociationNode head = KnownReference(_heads[key], "clear head");
        if (!head.Identity.HasValue) return;
        OriginalStartupAssociationNode tail = KnownReference(_tails[key], "clear tail");
        _next[Node(tail)] = _freeHead;
        _freeHead = head;
        _heads[key] = new(null);
        _tails[key] = new(null);
        int count = Known(_counts[key], "old count after head/tail clear");
        if (count < 0 || count > ActiveNodeCount) throw new InvalidDataException("Association count exceeds the owned active node count.");
        ActiveNodeCount -= count;
        _counts[key] = 0;
    }

    private void Key(int key)
    { if ((uint)key >= (uint)DescriptorCapacity) throw new InvalidDataException("Association key is outside the allocated capacity."); }
    private static int Node(OriginalStartupAssociationNode node)
        => node.Identity ?? throw new InvalidDataException("A null association node cannot provide fields.");
    private static T Known<T>(T? value, string field) where T : struct
        => value ?? throw new InvalidDataException($"Current association {field} is unknown.");
    private static T KnownReference<T>(T? value, string field) where T : class
        => value ?? throw new InvalidDataException($"Current association {field} is unknown.");
}
