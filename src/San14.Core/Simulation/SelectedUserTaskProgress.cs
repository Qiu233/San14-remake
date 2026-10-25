namespace San14.Core;

/// <summary>The selected user task's actual deadline, condition states and scoring periods.</summary>
public sealed record SelectedUserTaskProgress(NativeMapSeasonDate? Deadline,
    byte VictoryConditionStatus, byte DefeatConditionStatus, byte ScorePeriods);

/// <summary>Application work requested by a reached user-task conclusion.
/// An absent game result means the source had no game scene to update.</summary>
public sealed record SelectedUserTaskConclusionRequest(int UserIndex, bool? GameSucceeded, bool CloseProgressScene);

public sealed partial record GameState
{
    public SelectedUserTaskProgress? SelectedUserTaskProgress { get; init; }
    public SelectedUserTaskConclusionRequest? PendingSelectedUserTaskConclusion { get; init; }
}
