namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>The actual ordinary result page confirms its prepared snapshot.
    /// Occurrence/profile and event-manager completion remain with the worker.</summary>
    public bool TryCompleteCampaignEventCommandResult(GameState expectedOwner, long sequence,
        NormalTurnStoryResultSummary expectedSummary)
    {
        if (!EventCommandOwner(expectedOwner) || State.CampaignEventCommandPlayback?.Invocation is not { } playback ||
            playback.Phase != NormalTurnStoryResultPhase.Result ||
            !ReferenceEquals(playback.ResultSummary, expectedSummary) || playback.Story.Sequence != sequence ||
            playback.Story.NextStep != playback.Story.Steps.Length || playback.ExecutionCursor is { IsCompleted: false })
            return false;
        StoreEventCommandInvocation(playback with { Phase = NormalTurnStoryResultPhase.Confirmed });
        return true;
    }
}
