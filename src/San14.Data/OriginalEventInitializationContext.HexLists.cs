namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalHexReferenceListState
{
    public const int NativeNullHexListKey = -1;
    private readonly Dictionary<OriginalBaseReference, OriginalHexListHandle?> _baseHexListHandles = [];
    private CurrentOrderedLists _orderedLists = new();
    internal WorldIndexContext? OwnedIndexes { get; set; }
    internal bool HasCurrentList(int key) => _orderedLists.Contains(key);
    internal void MakeCurrentListUnavailable(OriginalHexListHandle handle)
    { if (handle.Identity is int key) _orderedLists.Release(key); }
    internal IReadOnlyList<object?>? ReadIndexValues(OriginalHexListHandle? handle)
        => handle?.Identity is int key && _orderedLists.Contains(key) ? _orderedLists.ReadAll(key) : null;
    internal IReadOnlyList<object?> ReadCurrentListValues(OriginalHexListHandle handle)
        => _orderedLists.ReadAll(HexListKey(handle));

    public void SetBaseHexListHandle(OriginalBaseReference reference, OriginalHexListHandle? handle)
        => _baseHexListHandles[reference] = handle;
    public OriginalHexListHandle? ReadBaseHexListHandle(OriginalBaseReference reference)
        => _baseHexListHandles.GetValueOrDefault(reference);
    public void SetKnownEmptyHexList(OriginalHexListHandle handle)
    {
        if (handle.Identity is int key) { _orderedLists.Ensure(key); _orderedLists.Clear(key); }
    }
    public void SetHexListNodePayload(OriginalHexListNodeReference node, OriginalHexReference? hex)
        => WriteCurrentListPayload(NodeIdentity(node), hex);
    public OriginalHexListNodeReference? ReadBaseHexListHead(OriginalBaseReference reference)
        => ReadBaseHexListHandle(reference) is { } handle ? ReadHexListHead(handle) : null;
    public OriginalHexListNodeReference? ReadHexListHead(OriginalHexListHandle handle)
        => handle.Identity is int key ? _orderedLists.Contains(key) ? new(_orderedLists.Head(key)) : null : new(null);
    public OriginalHexReference? ReadHexListNodePayload(OriginalHexListNodeReference node)
        => ReadNativeListPayload<OriginalHexReference>(NodeIdentity(node));
    private T? ReadNativeListPayload<T>(int cursor) where T : class
    {
        object? value = ReadCurrentListPayload(cursor);
        return value is null ? null : value as T ?? throw new InvalidDataException("Current ordered list has another value type.");
    }
    internal object? ReadCurrentListPayload(int cursor) => OwnedIndexes?.ReadValue(_orderedLists.Read(cursor)) ?? _orderedLists.Read(cursor);
    internal void WriteCurrentListPayload(int cursor, object? value) => _orderedLists.Write(cursor, OwnedIndexes?.StoreValue(value) ?? value);
    public OriginalHexListNodeReference? ReadHexListNodeNext(OriginalHexListNodeReference node)
        => new(_orderedLists.Next(NodeIdentity(node)));
    public OriginalHexListNodeReference? ReadHexListNodePrevious(OriginalHexListNodeReference node)
        => new(_orderedLists.Previous(NodeIdentity(node)));
    public void ReorderCurrentList(OriginalHexListHandle handle, IReadOnlyList<OriginalHexListNodeReference> order)
        => _orderedLists.Reorder(HexListKey(handle), order.Select(NodeIdentity).ToArray());
    public void ClearBaseHexReferences(OriginalBaseReference reference)
        => ClearHexList(KnownHandle(ReadBaseHexListHandle(reference)));
    public bool AppendBaseHexReference(OriginalBaseReference reference, OriginalHexReference hex)
        => AppendHexReference(KnownHandle(ReadBaseHexListHandle(reference)), hex);
    public OriginalHexListHandle? CreateTemporaryHexList() => new(_orderedLists.Create());
    public void ClearTemporaryHexList(OriginalHexListHandle handle) => ClearHexList(handle);
    public bool AppendTemporaryHexReference(OriginalHexListHandle handle, OriginalHexReference hex)
        => AppendHexReference(handle, hex);
    public int? ReadTemporaryHexListCount(OriginalHexListHandle handle)
        => handle.Identity is int key ? _orderedLists.Contains(key) ? _orderedLists.Count(key) : null : 0;
    public void ReleaseTemporaryHexList(OriginalHexListHandle handle)
    {
        if (handle.Identity is int key) _orderedLists.Release(key);
    }
    public OriginalHexListNodeReference AllocateHexListNode(OriginalHexListHandle handle)
        => AllocateNativeListNode(HexListKey(handle));
    private OriginalHexListNodeReference AllocateNativeListNode(int key)
        => NativeListKeyInRange(key) ? new(_orderedLists.Append(key)) : new(null);
    private bool AppendHexReference(OriginalHexListHandle handle, OriginalHexReference hex)
    {
        var node = AllocateHexListNode(handle);
        if (node.Identity is not int cursor) return false;
        WriteCurrentListPayload(cursor, hex); return true;
    }
    private void ClearHexList(OriginalHexListHandle handle)
    {
        if (handle.Identity is int key && NativeListKeyInRange(key)) ClearAddressableHexList(key);
    }
    private void ClearAddressableHexList(int key) => _orderedLists.Clear(key);
    private bool NativeListKeyInRange(int key) => _orderedLists.Contains(key);
    private bool KnownPoolPresent() => OwnedIndexes is not null;
    private static int HexListKey(OriginalHexListHandle handle) => handle.Identity ?? NativeNullHexListKey;
    private static int NodeIdentity(OriginalHexListNodeReference node)
        => node.Identity ?? throw new InvalidDataException("A null cursor has no current value.");
    private static void CheckOpaque(int id, string field)
    { if (id < 0) throw new InvalidDataException($"Negative {field}."); }
    private static OriginalHexListHandle KnownHandle(OriginalHexListHandle? handle)
        => handle ?? throw new InvalidDataException("Current base index is unavailable.");
    private static OriginalHexListNodeReference KnownNode(OriginalHexListNodeReference? node, string field)
        => node ?? throw new InvalidDataException($"Current list {field} is unavailable.");
}
