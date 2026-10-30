using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>The normal event manager's selected batch. Completing a displayed
/// step does not mark an event as occurred or rerun its selection prefix.</summary>
public sealed record NormalTurnStoryPlayback(CampaignStoryRequest Story,
    ImmutableArray<int> RemainingEventIds, CampaignEventOccurrenceDate Date,
    CampaignCameraSnapshot? ReturnCamera)
{
    public bool IsReturningCamera { get; init; }
    public CampaignStoryExecutionCursor? ExecutionCursor { get; init; }
    public CampaignStoryOutcomes? Outcomes { get; init; }
    public NormalTurnStoryAudioReturn? AudioReturn { get; init; }
    public NormalTurnStoryResultPhase Phase { get; init; }
    public NormalTurnStoryResultSummary? ResultSummary { get; init; }
    public CampaignStoryChronicleProgress? ChronicleProgress { get; init; }
    [JsonRequired] public CampaignStoryStateControlProgress? StateControl { get; init; }

    internal NormalTurnStoryPlayback Copy() => this with
    {
        Story = Story.Copy(), ExecutionCursor = ExecutionCursor?.Copy()
    };

    internal void Validate()
    {
        if (Story is null || RemainingEventIds.IsDefault || RemainingEventIds.Any(id => id is < 0 or >= 1024) ||
            Date is null || Date.Year > 350 || Date.Month is < 1 or > 12 || Date.Day is < 1 or > 30)
            throw new InvalidDataException("The normal Turn story batch is incomplete.");
        ExecutionCursor?.Validate();
        Outcomes?.Validate();
        if (ExecutionCursor?.ForceRankContinuation is { } rank &&
            (rank.Source != Story.Source || rank.StorySequence != Story.Sequence))
            throw new InvalidDataException("The ordinary Story rank request does not belong to its playback.");
        AudioReturn?.Validate();
        ChronicleProgress?.Validate();
        StateControl?.Validate();
        bool entry = Phase is NormalTurnStoryResultPhase.Entry or NormalTurnStoryResultPhase.Entered;
        if (!IsReturningCamera && (entry ? ChronicleProgress is not null : ChronicleProgress is null) ||
            IsReturningCamera && ChronicleProgress is not null ||
            ChronicleProgress is { SourceTimeUnavailable: false } progress &&
            (Phase == NormalTurnStoryResultPhase.Presentation ? progress.Completed : !progress.Completed))
            throw new InvalidDataException("The story chronology work does not belong to its invocation phase.");
        bool result = Phase is NormalTurnStoryResultPhase.Result or NormalTurnStoryResultPhase.Confirmed;
        bool exiting = Phase is NormalTurnStoryResultPhase.Exiting or NormalTurnStoryResultPhase.Exited;
        bool postResult = Phase == NormalTurnStoryResultPhase.PostResult;
        if ((entry || IsReturningCamera) ? StateControl is not null : StateControl is null)
            throw new InvalidDataException("The Story province-control owner does not belong to its invocation phase.");
        if (exiting && StateControl is { IsSourceTimeUnavailable: false, IsCompleted: false })
            throw new InvalidDataException("The Story province-control work has not completed before exit.");
        if (!Enum.IsDefined(Phase) ||
            (Phase == NormalTurnStoryResultPhase.Presentation || entry) && ResultSummary is not null ||
            result && (ResultSummary is null || Outcomes is null) ||
            (result || exiting || postResult) && (Story.NextStep != Story.Steps.Length || ExecutionCursor is { IsCompleted: false }) ||
            postResult && (ExecutionCursor is not { IsCompleted: true } || ChronicleProgress is not { Completed: true }) ||
            entry && (Story.Steps.Length != 0 || ExecutionCursor is not null || Outcomes is not null) ||
            Phase == NormalTurnStoryResultPhase.Entry && AudioReturn is not null ||
            (Phase == NormalTurnStoryResultPhase.Entered || exiting || postResult) && AudioReturn is null ||
            ResultSummary is { } summary && (summary.Locale is not ("TC" or "SC") || summary.Title is null || summary.Body is null) ||
            IsReturningCamera && (AudioReturn is not null || Outcomes is not null || Phase != NormalTurnStoryResultPhase.Presentation || ResultSummary is not null))
            throw new InvalidDataException("The normal Story result/audio lifecycle is incomplete or invalid.");
        Story.Validate(allowEmpty: entry || ExecutionCursor?.IsCompleted == true ||
            ExecutionCursor?.PersonListContinuation?.AwaitingDeathCompletion == true || ExecutionCursor?.ForceRankContinuation is not null ||
            ExecutionCursor?.SinglePersonDeathContinuation is not null);
        if (IsReturningCamera && (ExecutionCursor is not null || !RemainingEventIds.IsEmpty || Story.Steps.Length != 1 ||
            Story.Steps[0] is not StoryRestoreCampaignCamera))
            throw new InvalidDataException("The normal Turn camera return is incomplete.");
    }
}
