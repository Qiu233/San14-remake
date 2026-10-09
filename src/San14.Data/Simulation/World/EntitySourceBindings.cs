namespace San14.Data;

internal readonly record struct EntitySourceBinding(int SourceRecord, int EntityId);

/// <summary>Current one-to-one entity bindings for an operation's source records.</summary>
internal sealed class EntitySourceBindings
{
    private Dictionary<int, int> _entities = [];
    private Dictionary<int, int> _records = [];

    public int? EntityAt(int record) => _entities.TryGetValue(record, out int id) ? id : null;
    public int? RecordOf(int? id) => id is int entity && _records.TryGetValue(entity, out int record) ? record : null;
    public bool ContainsEntity(int id) => _records.ContainsKey(id);

    /// <summary>Binding a new receiver retires the displaced entity, not the entity moving to this record.</summary>
    public int? Bind(int record, int id)
    {
        int? displaced = EntityAt(record);
        if (displaced is int old && old != id) _records.Remove(old);
        if (RecordOf(id) is int previous && previous != record) _entities.Remove(previous);
        _entities[record] = id;
        _records[id] = record;
        return displaced != id ? displaced : null;
    }

    public int? RemoveRecord(int record)
    {
        if (!_entities.Remove(record, out int id)) return null;
        _records.Remove(id);
        return id;
    }

    /// <summary>A complete publication replaces current membership, including known absence.</summary>
    public void Replace(IEnumerable<EntitySourceBinding> bindings)
    {
        EntitySourceBinding[] rows = bindings.ToArray();
        var entities = rows.ToDictionary(row => row.SourceRecord, row => row.EntityId);
        var records = rows.ToDictionary(row => row.EntityId, row => row.SourceRecord);
        _entities = entities;
        _records = records;
    }

    public void CopyFrom(EntitySourceBindings source)
    {
        _entities = new(source._entities);
        _records = new(source._records);
    }
}
