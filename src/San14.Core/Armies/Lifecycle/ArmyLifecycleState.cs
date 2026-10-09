namespace San14.Core;

/// <summary>Campaign army ownership and its ordered domain indexes. Resource
/// encodings and source-table associations belong to the resource adapter.</summary>
public sealed partial record ArmyLifecycleState(ArmyReceiverRoster Receivers, WorldIndexesState Indexes)
{
    public ArmyGroupsState? ArmyGroups { get; init; }
    public ArmyCautionsState? ArmyCautions { get; init; }
    public ArmyLifecycleState Copy() => GameStateCopy.Copy(this)!;
}

public sealed partial record GameState
{
    public ArmyLifecycleState? ArmyLifecycle { get; init; }
}
