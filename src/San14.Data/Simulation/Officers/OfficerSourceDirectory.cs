namespace San14.Data;

/// <summary>
/// Identity and admission for one source-world capture. Logical officer IDs,
/// serialized record identities and table references are separate knowledge.
/// Neither a missing lookup nor an explicitly empty lookup invents a receiver.
/// Format widths and capacities belong to the callers decoding those tables.
/// </summary>
internal sealed class OfficerSourceDirectory
{
    private readonly Dictionary<int, int?> _recordIdentities = [];
    private readonly Dictionary<int, int?> _logicalReferences = [];
    private readonly Dictionary<int, int?> _tableReferences = [];
    private InitialOfficerSourceDefinitions? _initialRecords, _initialLogicalReferences;
    private int? _tableIdentityCount;
    private bool _logicalTableCleared;
    private SourceOfficerReference? _placeholder;

    internal void BindInitialRecords(InitialOfficerSourceDefinitions definitions)
    { _recordIdentities.Clear(); _initialRecords = definitions; }
    internal void BindInitialLogicalReferences(InitialOfficerSourceDefinitions definitions)
    {
        _logicalReferences.Clear(); _initialLogicalReferences = definitions;
        _logicalTableCleared = true;
    }
    internal void InitializeTableReferences(int count)
    { _tableReferences.Clear(); _tableIdentityCount = count; }

    public int? IdentityAt(int record) => _recordIdentities.TryGetValue(record,out var current)
        ? current : _initialRecords?.IdentityAt(record);
    public void SetRecordIdentity(int record, int? officerId)
    {
        if (_initialRecords?.ContainsRecord(record) == true && _initialRecords.IdentityAt(record) == officerId)
            _recordIdentities.Remove(record);
        else _recordIdentities[record] = officerId;
    }
    public void SetLogicalReference(int officerId, int? record)
    {
        int? initial = null;
        bool known = _initialLogicalReferences?.TryLogicalReference(officerId,out initial) == true;
        if ((known || _logicalTableCleared) && initial == record) _logicalReferences.Remove(officerId);
        else _logicalReferences[officerId] = record;
    }
    public void SetTableReference(int index, int? record)
    {
        if (_tableIdentityCount is int count && (uint)index < (uint)count && record == index)
            _tableReferences.Remove(index);
        else _tableReferences[index] = record;
    }
    public SourceOfficerReference? LogicalReference(int officerId)
    {
        if (_logicalReferences.TryGetValue(officerId,out int? record)) return new(record);
        if (_initialLogicalReferences?.TryLogicalReference(officerId,out record) == true) return new(record);
        return _logicalTableCleared ? new(null) : null;
    }
    public SourceOfficerReference? TableReference(int index) =>
        _tableReferences.TryGetValue(index,out int? record) ? new(record) :
        _tableIdentityCount is int count && (uint)index < (uint)count ? new(index) : null;
    public SourceOfficerReference? Placeholder => _placeholder;
    public void SetPlaceholder(int? record) => _placeholder = new(record);
    public void ClearLogicalReferences()
    { _logicalReferences.Clear(); _initialLogicalReferences = null; _logicalTableCleared = true; }

    // A table can deliberately alias a record whose embedded identity differs.
    // Source table readers preserve that alias; ordinary officer consumers need
    // an exact binding and never admit the placeholder as a playable officer.
    public int? ResolveOfficer(int officerId) => officerId > 0 &&
        LogicalReference(officerId)?.Record is int record && record != 0 &&
        IdentityAt(record) == officerId ? record : null;

    public void CopyTo(OfficerSourceDirectory target)
    {
        Copy(_recordIdentities, target._recordIdentities);
        Copy(_logicalReferences, target._logicalReferences);
        Copy(_tableReferences, target._tableReferences);
        target._initialRecords = _initialRecords;
        target._initialLogicalReferences = _initialLogicalReferences;
        target._tableIdentityCount = _tableIdentityCount;
        target._logicalTableCleared = _logicalTableCleared;
        target._placeholder = _placeholder;
        static void Copy(Dictionary<int, int?> source, Dictionary<int, int?> destination)
        { destination.Clear(); foreach (var entry in source) destination.Add(entry.Key, entry.Value); }
    }
}

internal sealed record SourceOfficerReference(int? Record);
