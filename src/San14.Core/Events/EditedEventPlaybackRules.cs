namespace San14.Core;

public sealed record EditedEventPlaybackPreparationResult(CampaignStoryRequest? Story,
    string? UnresolvedOwner)
{
    public GameState? State { get; init; }
}
public sealed record EditedEventPlaybackCompletionResult(GameState? State, string? UnresolvedOwner)
{
    public bool PendingBusiness { get; init; }
    //Null is the source-known return-to-title branch, not an empty-text skip.
    public EditedEventResultSummary? Summary { get; init; }
}

/// <summary>One actual post-summary business step. A source-known return to
/// title may bypass only the untouched tail; it never acknowledges a request.</summary>
public sealed record EditedEventPostResultContinuationResult(GameState? State, bool IsCompleted, string? UnresolvedOwner)
{
    public bool BypassedForReturnTitle { get; init; }
}

public interface IEditedEventPostResultRules
{
    EditedEventPostResultContinuationResult AdvanceEditedEventPostResult(GameState candidate, EditedEventPlayback playback);
}

/// <summary>Data owns the actual title-library binding, eligibility, compiled
/// article staging and result handlers. Core never opens the title archive.</summary>
public interface IEditedEventPlaybackRules
{
    // Preparation receives a private candidate for the actual manual-start
    // flag write. Its successful State and Story are adopted together.
    EditedEventPlaybackPreparationResult PrepareEditedEventPlayback(GameState candidate,
        int slot, long sequence);
    CampaignStoryPreparationResult PrepareEditedEventPlaybackStep(GameState candidate,
        EditedEventPlayback playback);
    // Completion receives one private transaction. Return a state only after
    // all real result handlers and source-time result-page preparation succeed.
    // Core records Occurred only after the actual summary receipt. Unknown/failure
    // returns no state, preserving the visible pending playback unchanged.
    EditedEventPlaybackCompletionResult CompleteEditedEventPlayback(GameState candidate,
        EditedEventPlayback playback);
}
