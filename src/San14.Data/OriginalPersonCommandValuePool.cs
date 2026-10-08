using San14.Core;

namespace San14.Data;

/// <summary>Operation-local numeric template pool with the same opaque saved
/// descriptor/node image as the primary pool, and independent value payloads.
/// A recycled node retains its old payload until the actual append store.</summary>
internal sealed class OriginalPersonCommandValuePool
{
    private readonly int descriptorCapacity, nodeCapacity;
    private readonly Stack<int> freeDescriptors;
    private readonly HashSet<int> live;
    private readonly Dictionary<int, NativeSharedListDescriptor> rows;
    private readonly Dictionary<int, NativeSharedListNode> nodes;
    private int? freeNode;
    private int created, active;
    public OriginalPersonCommandValuePool(NativeSharedListPoolState image)
    {
        NativeArmyLifecycleStateValidation.ValidatePool(image);
        descriptorCapacity = image.DescriptorCapacity; nodeCapacity = image.NodeCapacity;
        freeDescriptors = new(image.FreeDescriptors.Reverse()); live = image.LiveDescriptors.ToHashSet();
        rows = new(image.Descriptors); nodes = new(image.Nodes);
        freeNode = image.FreeNodeHead; created = image.CreatedNodeCount; active = image.ActiveNodeCount;
    }
    public static NativeSharedListPoolState Fresh(int descriptors, int nodeCapacity) => new(descriptors, nodeCapacity,
        0, 0, null, null, Enumerable.Range(0, descriptors).ToArray(), [],
        Enumerable.Range(0, descriptors).ToDictionary(x => x, _ => new NativeSharedListDescriptor(null, null, 0)), []);
    public NativeSharedListHandle AllocateDescriptor()
    {
        while (freeDescriptors.TryPop(out int key)) if (live.Add(key)) return new(key);
        return new(null);
    }
    public int? Append(NativeSharedListHandle descriptor, NativeSharedListPayload payload)
    {
        if (descriptor.Identity is not int key || key < 0 || key >= descriptorCapacity) return null;
        int id;
        if (freeNode is int reused) { id = reused; freeNode = nodes[id].Next; }
        else { if (created == nodeCapacity) return null; id = ++created; }
        var row = rows[key];
        nodes[id] = new(null, row.Head.HasValue ? row.Tail : null, payload);
        if (row.Head is not null && row.Tail is int tail) nodes[tail] = nodes[tail] with { Next = id };
        rows[key] = new(row.Head ?? id, id, (row.Count ?? throw new InvalidDataException("Current scalar count is unknown.")) + 1);
        active++; return id;
    }
    public void Clear(NativeSharedListHandle descriptor)
    {
        if (descriptor.Identity is not int key) return;
        var row = rows[key]; if (row.Head is not int head) return;
        if (row.Tail is not int tail || row.Count is not int count) throw new InvalidDataException("Current scalar chain tail/count is unknown.");
        nodes[tail] = nodes[tail] with { Next = freeNode }; freeNode = head;
        rows[key] = new(null, null, 0); active -= count;
    }
    //202D80: unlink the current head, return that node to the independent
    //value-pool free list, and retain its previous payload.
    public int? PopFirst(NativeSharedListHandle descriptor)
    {
        if (descriptor.Identity is not int key || rows[key].Head is not int head) return null;
        var row = rows[key]; var removed = nodes[head];
        int count = row.Count ?? throw new InvalidDataException("Current scalar chain count is unknown.");
        if (removed.Next is int next) nodes[next] = nodes[next] with { Previous = removed.Previous };
        rows[key] = new(removed.Next, row.Tail == head ? removed.Previous : row.Tail, count - 1);
        //These are opaque links: free.Next denotes the native free link;
        //the other native link was cleared before202D80's free-list push.
        nodes[head] = removed with { Next = freeNode, Previous = null };
        freeNode = head; active--; return head;
    }
    /// <summary>018810: unlink the actual selected node, keep its payload,
    /// and push it onto this value template's free chain.</summary>
    public void RemoveAt(NativeSharedListHandle descriptor, int node)
    {
        int key = descriptor.Identity ?? throw new InvalidDataException("Current selected value descriptor is unknown.");
        var row = rows[key]; var removed = nodes[node];
        int count = row.Count ?? throw new InvalidDataException("Current selected value count is unknown.");
        if (removed.Previous is int previous) nodes[previous] = nodes[previous] with { Next = removed.Next };
        if (removed.Next is int next) nodes[next] = nodes[next] with { Previous = removed.Previous };
        rows[key] = new(row.Head == node ? removed.Next : row.Head, row.Tail == node ? removed.Previous : row.Tail, count - 1);
        nodes[node] = removed with { Next = freeNode, Previous = null }; freeNode = node; active--;
    }
    public IEnumerable<NativeSharedListPayload?> Values(NativeSharedListHandle descriptor)
    {
        if (descriptor.Identity is not int key) yield break;
        for (int? at = rows[key].Head; at is int id; at = nodes[id].Next) yield return nodes[id].Payload;
    }
    public int? Count(NativeSharedListHandle descriptor) => descriptor.Identity is int key ? rows[key].Count : null;
    public void ReleaseDescriptor(NativeSharedListHandle descriptor)
    {
        if (descriptor.Identity is not int key) return;
        if (!live.Remove(key)) throw new InvalidDataException("Current command descriptor is not allocated.");
        freeDescriptors.Push(key); //Actual018700 LIFO; row/payload bytes survive.
    }
    public void SetPayload(int node, NativeSharedListPayload payload) => nodes[node] = nodes[node] with { Payload = payload };
    public NativeSharedListPoolState Export() => new(descriptorCapacity, nodeCapacity, active, created, freeNode, null,
        freeDescriptors.ToArray(), live.Order().ToArray(), new(rows), new(nodes));
}
