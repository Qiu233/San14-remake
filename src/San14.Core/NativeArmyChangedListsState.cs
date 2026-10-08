namespace San14.Core;

/// <summary>0BD880's separate work8/pending18 descriptor identities in the
/// existing primary pool. It is not manager60 or a generated route.</summary>
public sealed record NativeArmyChangedListsState(string Revision, NativeSharedListHandle Work8,
    NativeSharedListHandle Pending18, byte? Busy90);

public sealed partial record NativeArmyLifecycleState
{
    public NativeArmyChangedListsState? ChangedLists { get; init; }
}
