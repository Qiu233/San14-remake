namespace San14.Data;

public sealed partial class WorldIndexContext
{
    internal SourceWorldContext ArmyReturnOwner => _state;
    internal bool OwnsArmyReturnDescriptor(RegistryListHandle descriptor)
        => descriptor.Identity is not int key || _state.HasCurrentList(key);
    internal bool OwnsArmyReturnProfile() => ReferenceEquals(_state.OwnedIndexes, this);
    internal WorldIndexContext DetachForArmyReturn(SourceWorldContext target)
    {
        var result = new WorldIndexContext(target);
        result.CopyBindingsFrom(this); return result;
    }
    internal void CommitArmyReturnPool(WorldIndexContext draft)
    {
        _state.CommitArmyReturnSharedListFields(draft._state); CopyBindingsFrom(draft);
    }
    private void CopyBindingsFrom(WorldIndexContext source)
    {
        _armies.CopyFrom(source._armies);
        _officers.CopyFrom(source._officers);
        _traitHandles.Clear(); foreach (var row in source._traitHandles) _traitHandles.Add(row.Key, row.Value.ToArray());
        _cautionHandles.Clear(); foreach (var row in source._cautionHandles) _cautionHandles.Add(row.Key, row.Value);
        AssociatedTaskHandle = source.AssociatedTaskHandle; ChangedWorkingHandle = source.ChangedWorkingHandle;
        ChangedPendingHandle = source.ChangedPendingHandle; ChangedProcessing = source.ChangedProcessing;
    }
}
