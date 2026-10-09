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
    private bool _logicalTableCleared;
    private SourceOfficerReference? _placeholder;

    public int? IdentityAt(int record) => _recordIdentities.GetValueOrDefault(record);
    public void SetRecordIdentity(int record, int? officerId) => _recordIdentities[record] = officerId;
    public void SetLogicalReference(int officerId, int? record) => _logicalReferences[officerId] = record;
    public void SetTableReference(int index, int? record) => _tableReferences[index] = record;
    public SourceOfficerReference? LogicalReference(int officerId) =>
        _logicalReferences.TryGetValue(officerId, out int? record) ? new(record) :
        _logicalTableCleared ? new(null) : null;
    public SourceOfficerReference? TableReference(int index) =>
        _tableReferences.TryGetValue(index, out int? record) ? new(record) : null;
    public SourceOfficerReference? Placeholder => _placeholder;
    public void SetPlaceholder(int? record) => _placeholder = new(record);
    public void ClearLogicalReferences() { _logicalReferences.Clear(); _logicalTableCleared = true; }

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
        target._logicalTableCleared = _logicalTableCleared;
        target._placeholder = _placeholder;
        static void Copy(Dictionary<int, int?> source, Dictionary<int, int?> destination)
        { destination.Clear(); foreach (var entry in source) destination.Add(entry.Key, entry.Value); }
    }
}

internal sealed record SourceOfficerReference(int? Record);
