namespace San14.Data;

/// <summary>Operation-local adapter for resource-facing traversal APIs.
/// Lists use ordinary owned collections; cursor tokens have no save identity.</summary>
public sealed partial class WorldIndexContext
{
    private readonly SourceWorldContext _state;
    private readonly EntitySourceBindings _armies = new();
    private readonly Dictionary<int, RegistryListHandle[]> _traitHandles = [];
    private readonly Dictionary<int, HexListHandle> _cautionHandles = [];
    internal HexListHandle? AssociatedTaskHandle { get; private set; }
    internal HexListHandle? ChangedWorkingHandle { get; private set; }
    internal HexListHandle? ChangedPendingHandle { get; private set; }
    internal bool? ChangedProcessing { get; set; }

    public WorldIndexContext(SourceWorldContext state)
    {
        _state = state; state.OwnedIndexes = this;
        state.RegistryHeadTablePresent = true; state.RegistryLockEnabled = false;
    }
    public HexListHandle AllocateDescriptor() => _state.CreateTemporaryHexList()!;
    public void BindArmyIdentity(int physical, int unitId)
    {
        if (_armies.Bind(physical, unitId) is int displaced) ReleaseTraitIndexes(displaced);
    }
    public void RemoveArmyIdentity(int physical)
    {
        if (_armies.RemoveRecord(physical) is int id) ReleaseTraitIndexes(id);
    }
    private void ReleaseTraitIndexes(int unitId)
    {
        if (_traitHandles.Remove(unitId, out var handles))
            foreach (var handle in handles)
                if (handle.Identity is int key && _state.HasCurrentList(key)) _state.ReleaseTemporaryHexList(new(key));
    }
    internal RegistryListHandle[] TraitHandles(int physical)
    {
        int unitId = ArmyId(physical);
        if (!_traitHandles.TryGetValue(unitId, out var handles))
        {
            handles = Enumerable.Range(0, 9).Select(_ => new RegistryListHandle(AllocateDescriptor().Identity)).ToArray();
            _traitHandles.Add(unitId, handles);
        }
        return handles;
    }
    internal HexListHandle CautionGroupHandle(int group)
    {
        if (!_cautionHandles.TryGetValue(group, out var handle))
        { handle = AllocateDescriptor(); _cautionHandles.Add(group, handle); }
        return handle;
    }
    internal void ReplaceCautionGroupMembers(int group, IReadOnlyList<int?> members)
    {
        var handle = CautionGroupHandle(group);
        _state.ClearTemporaryHexList(handle);
        foreach (int? id in members)
            _state.AppendStoredIndexValue(handle, new UnitIndexValue(id));
    }
    internal HexListHandle CreateAssociatedTaskIndex()
    {
        AssociatedTaskHandle ??= AllocateDescriptor();
        _state.SetCurrentAssociatedContainer(new(0));
        _state.SetAssociatedChainDescriptor(0, new(AssociatedTaskHandle.Identity));
        return AssociatedTaskHandle;
    }
    internal void CreateChangedIndexes(bool processing = false)
    {
        ChangedWorkingHandle ??= AllocateDescriptor(); ChangedPendingHandle ??= AllocateDescriptor();
        ChangedProcessing = processing;
    }
    internal Dictionary<int, San14.Core.ArmyIndexState> ExportArmyIndexes(Func<HexListHandle?, List<int?>?> read)
        => _traitHandles.Where(row => _armies.ContainsEntity(row.Key)).ToDictionary(row => row.Key, row =>
            new San14.Core.ArmyIndexState(row.Key, row.Value.Select(handle => read(new(handle.Identity))).ToArray()));
    internal Dictionary<int, List<int?>?> ExportCautionIndexes(Func<HexListHandle?, List<int?>?> read)
        => _cautionHandles.ToDictionary(row => row.Key, row => read(row.Value));
    internal IEnumerable<int> BoundListKeys => _traitHandles.Where(row => _armies.ContainsEntity(row.Key)).SelectMany(row => row.Value).Select(handle => handle.Identity)
        .Concat(_cautionHandles.Values.Select(handle => handle.Identity))
        .Concat([AssociatedTaskHandle?.Identity, ChangedWorkingHandle?.Identity, ChangedPendingHandle?.Identity])
        .OfType<int>();
}
