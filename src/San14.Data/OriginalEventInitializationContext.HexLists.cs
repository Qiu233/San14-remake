namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalHexReferenceListState
{
    public const int NativeNullHexListKey = 0x14000;
    private readonly Dictionary<OriginalBaseReference, OriginalHexListHandle?> _baseHexListHandles = [];
    private readonly Dictionary<int, OriginalHexListNodeReference?> _hexListHeads = [], _hexListTails = [];
    private readonly Dictionary<int, int?> _hexListCounts = [];
    // All lists use the same native node+0 payload. Reusing a node for another
    // object kind overwrites that pointer; typed stores must not preserve aliases.
    private readonly Dictionary<int, object?> _nativeListPayloads = [];
    private readonly Dictionary<int, OriginalHexListNodeReference?> _hexListNext = [], _hexListPrevious = [];
    public OriginalHexListAllocationProfile? HexListAllocationProfile { get; set; }

    public void SetBaseHexListHandle(OriginalBaseReference baseReference, OriginalHexListHandle? handle)
    { _ = BasePhysical(baseReference); if (handle?.Identity is int id) CheckOpaque(id, "list handle"); _baseHexListHandles[baseReference] = handle; }
    public OriginalHexListHandle? ReadBaseHexListHandle(OriginalBaseReference baseReference)
    { _ = BasePhysical(baseReference); return _baseHexListHandles.GetValueOrDefault(baseReference); }
    public void SetHexListHead(OriginalHexListHandle handle, OriginalHexListNodeReference? node)
    { int key = HexListKey(handle); CheckNode(node); _hexListHeads[key] = node; }
    public void SetHexListTail(OriginalHexListHandle handle, OriginalHexListNodeReference? node)
    { int key = HexListKey(handle); CheckNode(node); _hexListTails[key] = node; }
    public void SetHexListCount(OriginalHexListHandle handle, int? count)
    { if (count is < 0) throw new InvalidDataException("Negative count is outside normal logical list profile."); _hexListCounts[HexListKey(handle)] = count; }
    /// <summary>Explicit already-known empty current list; never called implicitly by construction or allocation.</summary>
    public void SetKnownEmptyHexList(OriginalHexListHandle handle)
    { SetHexListHead(handle, new(null)); SetHexListTail(handle, new(null)); SetHexListCount(handle, 0); }
    public void SetHexListNodePayload(OriginalHexListNodeReference node, OriginalHexReference? hex)
    { int id = NodeIdentity(node); if (hex?.PhysicalSlot is int physical) Check(physical, 48400, "list hex payload"); _nativeListPayloads[id] = hex; }
    public void SetHexListNodeNext(OriginalHexListNodeReference node, OriginalHexListNodeReference? next)
    { CheckNode(next); _hexListNext[NodeIdentity(node)] = next; }
    public void SetHexListNodePrevious(OriginalHexListNodeReference node, OriginalHexListNodeReference? previous)
    { CheckNode(previous); _hexListPrevious[NodeIdentity(node)] = previous; }
    public OriginalHexListNodeReference? ReadBaseHexListHead(OriginalBaseReference baseReference)
    {
        OriginalHexListHandle? handle = ReadBaseHexListHandle(baseReference);
        return handle is null ? null : ReadHexListHead(handle);
    }
    public OriginalHexListNodeReference? ReadHexListHead(OriginalHexListHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!handle.Identity.HasValue) return new(null);
        return ListKeyAddressable(handle) ? _hexListHeads.GetValueOrDefault(HexListKey(handle)) : new(null);
    }
    public OriginalHexReference? ReadHexListNodePayload(OriginalHexListNodeReference node) => ReadNativeListPayload<OriginalHexReference>(NodeIdentity(node));
    private T? ReadNativeListPayload<T>(int identity) where T : class
    {
        object? payload = _nativeListPayloads.GetValueOrDefault(identity);
        if (payload is null) return null;
        return payload as T ?? throw new InvalidDataException("Current list node payload has another object kind; native typed access would be unsafe.");
    }
    public OriginalHexListNodeReference? ReadHexListNodeNext(OriginalHexListNodeReference node) => _hexListNext.GetValueOrDefault(NodeIdentity(node));
    public OriginalHexListNodeReference? ReadHexListNodePrevious(OriginalHexListNodeReference node) => _hexListPrevious.GetValueOrDefault(NodeIdentity(node));
    public void ClearBaseHexReferences(OriginalBaseReference baseReference) => ClearHexList(KnownHandle(ReadBaseHexListHandle(baseReference)));
    public bool AppendBaseHexReference(OriginalBaseReference baseReference, OriginalHexReference hex) => AppendHexReference(KnownHandle(ReadBaseHexListHandle(baseReference)), hex);
    public OriginalHexListHandle? CreateTemporaryHexList()
    {
        OriginalHexListAllocationProfile profile = Profile();
        if (profile.TemporaryDescriptorAllocator is null) throw new InvalidDataException("Current temporary descriptor allocation is unknown.");
        OriginalHexListHandle result = profile.TemporaryDescriptorAllocator()
            ?? throw new InvalidDataException("Current temporary descriptor allocation outcome is unknown.");
        if (result.Identity is int id) CheckOpaque(id, "temporary list handle");
        // 018A00 does not clear the descriptor's fixed key or its associated node-table row.
        return result;
    }
    public void ClearTemporaryHexList(OriginalHexListHandle handle) => ClearHexList(handle);
    public bool AppendTemporaryHexReference(OriginalHexListHandle handle, OriginalHexReference hex) => AppendHexReference(handle, hex);
    public int? ReadTemporaryHexListCount(OriginalHexListHandle handle)
    {
        // 2A70AF/BA uses the sentinel key for a null descriptor and still reads the
        // current count table. A null pointer does not establish a zero count.
        return _hexListCounts.GetValueOrDefault(HexListKey(handle));
    }
    public void ReleaseTemporaryHexList(OriginalHexListHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!handle.Identity.HasValue) return;
        if (!KnownPoolPresent()) return;
        OriginalHexListAllocationProfile profile = Profile();
        if (profile.TemporaryDescriptorReleaser is null) throw new InvalidDataException("Current temporary descriptor release is unknown.");
        profile.TemporaryDescriptorReleaser(handle);
        // Backing fields survive; release itself does not clear node rows or rebind aliases.
    }

    /// <summary>
    /// Successful 018C90 tail allocation followed by logical current head/tail/count effects.
    /// The caller performs the original payload store separately. New payload is unknown;
    /// a recycled node retains its old payload until overwritten, exactly as +0 is not cleared.
    /// </summary>
    public OriginalHexListNodeReference AllocateHexListNode(OriginalHexListHandle handle)
        => AllocateNativeListNode(HexListKey(handle));
    private OriginalHexListNodeReference AllocateNativeListNode(int key)
    {
        if (!NativeListKeyInRange(key)) return new(null);
        var handle = new OriginalHexListHandle(key);
        OriginalHexListAllocationProfile profile = Profile();
        if (profile.NodeAllocator is null) throw new InvalidDataException("Current hex-list node allocation is unknown.");
        OriginalHexListNodeReference node = profile.NodeAllocator(handle)
            ?? throw new InvalidDataException("Current hex-list node allocation outcome is unknown.");
        if (!node.Identity.HasValue) return node;
        int id = NodeIdentity(node);
        _hexListNext[id] = new(null); _hexListPrevious[id] = new(null);
        OriginalHexListNodeReference head = KnownNode(_hexListHeads.GetValueOrDefault(key), "append head");
        OriginalHexListNodeReference? tail = head.Identity.HasValue ? KnownNode(_hexListTails.GetValueOrDefault(key), "append tail") : new(null);
        if (head.Identity.HasValue && tail!.Identity.HasValue)
        {
            _hexListNext[NodeIdentity(tail)] = node; _hexListPrevious[id] = tail;
        }
        else _hexListHeads[key] = node;
        _hexListTails[key] = node;
        int count = _hexListCounts.GetValueOrDefault(key) ?? throw new InvalidDataException("Current hex-list count is unknown at increment.");
        if (count == int.MaxValue) throw new InvalidDataException("Hex-list count exceeds the supported normal allocation profile.");
        _hexListCounts[key] = count + 1;
        if (profile.NodeCountIncrementer is null) throw new InvalidDataException("Current hex-list global count increment is unknown.");
        profile.NodeCountIncrementer(handle);
        return node;
    }
    private bool AppendHexReference(OriginalHexListHandle handle, OriginalHexReference hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        if (hex.PhysicalSlot is int physical) Check(physical, 48400, "list hex payload");
        OriginalHexListNodeReference node = AllocateHexListNode(handle);
        if (!node.Identity.HasValue) return false;
        SetHexListNodePayload(node, hex);
        return true;
    }
    private void ClearHexList(OriginalHexListHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!handle.Identity.HasValue || !ListKeyAddressable(handle)) return;
        int key = HexListKey(handle);
        ClearAddressableHexList(key);
    }
    private void ClearAddressableHexList(int key)
    {
        var handle=new OriginalHexListHandle(key);
        OriginalHexListNodeReference head = KnownNode(_hexListHeads.GetValueOrDefault(key), "clear head");
        if (!head.Identity.HasValue) return; // Native018610 does not repair tail/count for an empty head.
        OriginalHexListNodeReference tail = KnownNode(_hexListTails.GetValueOrDefault(key), "clear tail");
        if (!tail.Identity.HasValue) throw new InvalidDataException("Nonempty list has a null tail in normal pool profile.");
        OriginalHexListAllocationProfile profile = Profile();
        if (profile.NodeReturner is null) throw new InvalidDataException("Current hex-list node return effect is unknown.");
        profile.NodeReturner(handle, head, tail);
        _hexListHeads[key] = new(null); _hexListTails[key] = new(null);
        int oldCount = _hexListCounts.GetValueOrDefault(key) ?? throw new InvalidDataException("Current hex-list old count is unknown after head/tail clear.");
        if (profile.NodeCountReturner is null) throw new InvalidDataException("Current hex-list pool count return effect is unknown.");
        profile.NodeCountReturner(handle, oldCount);
        _hexListCounts[key] = 0;
        // Freed nodes retain payload and caller-supplied free-chain effects; no guessed erasure.
    }
    private bool ListKeyAddressable(OriginalHexListHandle handle)
    {
        // Head resolution/clear guard the backing pool before its key/window check.
        if (!KnownPoolPresent()) return false;
        return ListKeyInRange(handle);
    }
    private bool ListKeyInRange(OriginalHexListHandle handle)
        => NativeListKeyInRange(HexListKey(handle));
    private bool NativeListKeyInRange(int key)
    {
        int capacity = Profile().ListKeyCapacity ?? throw new InvalidDataException("Current hex-list key capacity is unknown.");
        if (capacity < 0) throw new InvalidDataException("Negative hex-list key capacity.");
        // Native018C90 rejects an invalid unsigned key before its lock/free-list reads.
        return unchecked((uint)key) < (uint)capacity;
    }
    private bool KnownPoolPresent() => Profile().NodePoolPresent ?? throw new InvalidDataException("Current hex-list node pool presence is unknown.");
    private OriginalHexListAllocationProfile Profile() => HexListAllocationProfile ?? throw new InvalidDataException("Current hex-list allocation profile is unknown.");
    private static int HexListKey(OriginalHexListHandle handle)
    { ArgumentNullException.ThrowIfNull(handle); int key = handle.Identity ?? NativeNullHexListKey; CheckOpaque(key, "list handle key"); return key; }
    private static int NodeIdentity(OriginalHexListNodeReference node)
    { ArgumentNullException.ThrowIfNull(node); int id = node.Identity ?? throw new InvalidDataException("Null logical hex-list node cannot provide fields."); CheckOpaque(id, "list node token"); return id; }
    private static void CheckOpaque(int id, string field) { if (id < 0) throw new InvalidDataException($"Negative {field} outside stable logical pool profile."); }
    private static void CheckNode(OriginalHexListNodeReference? node) { if (node?.Identity is int id) CheckOpaque(id, "list node token"); }
    private static OriginalHexListHandle KnownHandle(OriginalHexListHandle? handle) => handle ?? throw new InvalidDataException("Current base hex-list handle is unknown.");
    private static OriginalHexListNodeReference KnownNode(OriginalHexListNodeReference? node, string field) => node ?? throw new InvalidDataException($"Current hex-list {field} is unknown.");
}
