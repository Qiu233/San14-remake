namespace San14.Data;

public sealed partial class SourceWorldContext : IHexReferenceListState
{
    public const int NativeNullHexListKey = -1;
    private readonly Dictionary<BaseReference, HexListHandle?> _baseHexListHandles = [];
    private CurrentOrderedLists _orderedLists = new();
    internal WorldIndexContext? OwnedIndexes { get; set; }
    internal bool HasCurrentList(int key) => _orderedLists.Contains(key);
    internal void MakeCurrentListUnavailable(HexListHandle handle)
    { if (handle.Identity is int key) _orderedLists.Release(key); }
    internal IReadOnlyList<object?>? ReadIndexValues(HexListHandle? handle)
        => handle?.Identity is int key && _orderedLists.Contains(key) ? _orderedLists.ReadAll(key) : null;
    internal IReadOnlyList<object?> ReadCurrentListValues(HexListHandle handle)
        => _orderedLists.ReadAll(HexListKey(handle));

    public void SetBaseHexListHandle(BaseReference reference, HexListHandle? handle)
        => _baseHexListHandles[reference] = handle;
    public HexListHandle? ReadBaseHexListHandle(BaseReference reference)
        => _baseHexListHandles.GetValueOrDefault(reference);
    public void SetKnownEmptyHexList(HexListHandle handle)
    {
        if (handle.Identity is int key) { _orderedLists.Ensure(key); _orderedLists.Clear(key); }
    }
    public void SetHexListNodePayload(HexListNodeReference node, HexReference? hex)
        => WriteCurrentListPayload(NodeIdentity(node), hex);
    public HexListNodeReference? ReadBaseHexListHead(BaseReference reference)
        => ReadBaseHexListHandle(reference) is { } handle ? ReadHexListHead(handle) : null;
    public HexListNodeReference? ReadHexListHead(HexListHandle handle)
        => handle.Identity is int key ? _orderedLists.Contains(key) ? new(_orderedLists.Head(key)) : null : new(null);
    public HexReference? ReadHexListNodePayload(HexListNodeReference node)
        => ReadNativeListPayload<HexReference>(NodeIdentity(node));
    private T? ReadNativeListPayload<T>(int cursor) where T : class
    {
        object? value = ReadCurrentListPayload(cursor);
        return value is null ? null : value as T ?? throw new InvalidDataException("Current ordered list has another value type.");
    }
    internal object? ReadCurrentListPayload(int cursor) => OwnedIndexes?.ReadValue(_orderedLists.Read(cursor)) ?? _orderedLists.Read(cursor);
    internal void WriteCurrentListPayload(int cursor, object? value) => _orderedLists.Write(cursor, OwnedIndexes?.StoreValue(value) ?? value);
    public HexListNodeReference? ReadHexListNodeNext(HexListNodeReference node)
        => new(_orderedLists.Next(NodeIdentity(node)));
    public HexListNodeReference? ReadHexListNodePrevious(HexListNodeReference node)
        => new(_orderedLists.Previous(NodeIdentity(node)));
    public void ReorderCurrentList(HexListHandle handle, IReadOnlyList<HexListNodeReference> order)
        => _orderedLists.Reorder(HexListKey(handle), order.Select(NodeIdentity).ToArray());
    public void ClearBaseHexReferences(BaseReference reference)
        => ClearHexList(KnownHandle(ReadBaseHexListHandle(reference)));
    public bool AppendBaseHexReference(BaseReference reference, HexReference hex)
        => AppendHexReference(KnownHandle(ReadBaseHexListHandle(reference)), hex);
    public HexListHandle? CreateTemporaryHexList() => new(_orderedLists.Create());
    public void ClearTemporaryHexList(HexListHandle handle) => ClearHexList(handle);
    public bool AppendTemporaryHexReference(HexListHandle handle, HexReference hex)
        => AppendHexReference(handle, hex);
    public int? ReadTemporaryHexListCount(HexListHandle handle)
        => handle.Identity is int key ? _orderedLists.Contains(key) ? _orderedLists.Count(key) : null : 0;
    public void ReleaseTemporaryHexList(HexListHandle handle)
    {
        if (handle.Identity is int key) _orderedLists.Release(key);
    }
    public HexListNodeReference AllocateHexListNode(HexListHandle handle)
        => AllocateNativeListNode(HexListKey(handle));
    private HexListNodeReference AllocateNativeListNode(int key)
        => NativeListKeyInRange(key) ? new(_orderedLists.Append(key)) : new(null);
    private bool AppendHexReference(HexListHandle handle, HexReference hex)
    {
        var node = AllocateHexListNode(handle);
        if (node.Identity is not int cursor) return false;
        WriteCurrentListPayload(cursor, hex); return true;
    }
    private void ClearHexList(HexListHandle handle)
    {
        if (handle.Identity is int key && NativeListKeyInRange(key)) ClearAddressableHexList(key);
    }
    private void ClearAddressableHexList(int key) => _orderedLists.Clear(key);
    private bool NativeListKeyInRange(int key) => _orderedLists.Contains(key);
    private bool KnownPoolPresent() => OwnedIndexes is not null;
    private static int HexListKey(HexListHandle handle) => handle.Identity ?? NativeNullHexListKey;
    private static int NodeIdentity(HexListNodeReference node)
        => node.Identity ?? throw new InvalidDataException("A null cursor has no current value.");
    private static void CheckOpaque(int id, string field)
    { if (id < 0) throw new InvalidDataException($"Negative {field}."); }
    private static HexListHandle KnownHandle(HexListHandle? handle)
        => handle ?? throw new InvalidDataException("Current base index is unavailable.");
    private static HexListNodeReference KnownNode(HexListNodeReference? node, string field)
        => node ?? throw new InvalidDataException($"Current list {field} is unavailable.");
}
