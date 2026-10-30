namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>The actual ordinary result page confirms its prepared snapshot.
    /// Occurrence/profile and event-manager completion remain with the worker.</summary>
    public bool TryCompleteNormalTurnStoryResult(GameState expectedOwner, long sequence,
        NormalTurnStoryResultSummary expectedSummary)
    {
        if (_progress is not null || _normalPeriod is not { } pending ||
            !ReferenceEquals(pending.Candidate, expectedOwner) ||
            pending.Candidate.NativeNormalTurnContinuation is not { NextAction: 48, StoryPlayback: { } playback } turn ||
            playback.Phase != NormalTurnStoryResultPhase.Result ||
            !ReferenceEquals(playback.ResultSummary, expectedSummary) || playback.Story.Sequence != sequence ||
            playback.Story.NextStep != playback.Story.Steps.Length || playback.ExecutionCursor is { IsCompleted: false })
            return false;
        _normalPeriod = pending with { Candidate = pending.Candidate with
        {
            NativeNormalTurnContinuation = turn with
            { StoryPlayback = playback with { Phase = NormalTurnStoryResultPhase.Confirmed } }
        } };
        return true;
    }
}
