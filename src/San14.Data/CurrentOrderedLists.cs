namespace San14.Data;

/// <summary>Operation-local cursors over ordinary ordered lists. Cursor IDs
/// are never saved or reused; membership and order live in the lists.</summary>
internal sealed class CurrentOrderedLists
{
    private sealed class Entry(int cursor)
    {
        public int Cursor { get; } = cursor;
        public object? Payload { get; set; }
        public bool HasPayload { get; set; }
    }
    private readonly Dictionary<int, List<Entry>> _lists = [];
    private readonly Dictionary<int, (int List, Entry Entry)> _entries = [];
    private int _nextList;
    private int _nextCursor = 1;
    public int ListCount => _lists.Count;
    public int EntryCount => _entries.Count;
    public IEnumerable<int> NonemptyLists => _lists.Where(row => row.Value.Count != 0).Select(row => row.Key);
    public bool Contains(int key) => _lists.ContainsKey(key);
    public int Create()
    {
        while (_lists.ContainsKey(_nextList)) _nextList++;
        int key = _nextList++;
        _lists.Add(key, []);
        return key;
    }
    public void Ensure(int key)
    {
        _lists.TryAdd(key, []);
    }
    public void Release(int key)
    {
        Clear(key); _lists.Remove(key);
    }
    public int? Head(int key) => _lists.TryGetValue(key, out var list) && list.Count > 0 ? list[0].Cursor : null;
    public int Count(int key) => _lists[key].Count;
    public int Append(int key)
    {
        var entry = new Entry(_nextCursor++);
        _lists[key].Add(entry); _entries.Add(entry.Cursor, (key, entry));
        return entry.Cursor;
    }
    public object? Read(int cursor) => _entries[cursor].Entry.HasPayload ? _entries[cursor].Entry.Payload :
        throw new InvalidDataException("A current list entry has not been assigned its value.");
    public void Write(int cursor, object? value)
    {
        var entry = _entries[cursor].Entry; entry.Payload = value; entry.HasPayload = true;
    }
    public int? Next(int cursor)
    {
        var row = _entries[cursor]; var list = _lists[row.List]; int index = list.IndexOf(row.Entry);
        return index + 1 < list.Count ? list[index + 1].Cursor : null;
    }
    public int? Previous(int cursor)
    {
        var row = _entries[cursor]; var list = _lists[row.List]; int index = list.IndexOf(row.Entry);
        return index > 0 ? list[index - 1].Cursor : null;
    }
    public void Remove(int key, int cursor)
    {
        var row = _entries[cursor];
        if (row.List != key) throw new InvalidOperationException("A cursor belongs to another ordered list.");
        _lists[key].Remove(row.Entry); _entries.Remove(cursor);
    }
    public void Clear(int key)
    {
        var list = _lists[key]; foreach (var entry in list) _entries.Remove(entry.Cursor);
        list.Clear();
    }
    public void Reorder(int key, IReadOnlyList<int> cursors)
    {
        var list = _lists[key];
        if (cursors.Count != list.Count || cursors.Distinct().Count() != list.Count)
            throw new InvalidOperationException("Reordering requires each current entry once.");
        var order = cursors.Select(cursor => _entries[cursor]).ToArray();
        if (order.Any(row => row.List != key)) throw new InvalidOperationException("Reordering cannot move entries between lists.");
        list.Clear(); list.AddRange(order.Select(row => row.Entry));
    }
    public IReadOnlyList<object?> ReadAll(int key) => _lists[key].Select(entry => Read(entry.Cursor)).ToArray();
    public CurrentOrderedLists Copy()
    {
        var copy = new CurrentOrderedLists { _nextList = _nextList, _nextCursor = _nextCursor };
        foreach (var row in _lists)
        {
            var list = new List<Entry>(); copy._lists.Add(row.Key, list);
            foreach (var entry in row.Value)
            {
                var cloned = new Entry(entry.Cursor) { Payload = entry.Payload, HasPayload = entry.HasPayload };
                list.Add(cloned); copy._entries.Add(cloned.Cursor, (row.Key, cloned));
            }
        }
        return copy;
    }
}
