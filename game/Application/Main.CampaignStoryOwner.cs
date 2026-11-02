using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    // Each stopped operation retains its actual presentation candidate.
    // Direct command events continue to use their session world.
    private static GameState StoryOwner(GameplaySession session) =>
        session.PendingNormalPeriod?.Candidate ?? session.PendingManualTacticCandidate ?? session.PendingArmyProgressCandidate ?? session.PendingProgressCandidate ?? session.World;
    private static NormalTurnStoryPlayback? StoryPlayback(GameplaySession session) =>
        session.PendingNormalPeriod?.Candidate.NativeNormalTurnContinuation?.StoryPlayback ??
        session.PendingCampaignEventCommand?.Invocation;
    private static CampaignStoryRequest? PendingCampaignStory(GameplaySession session) =>
        session.PendingNormalPeriod?.PendingStory ?? session.PendingCampaignEventCommand?.PendingStory ?? session.PendingLegacyStory;
    private static NormalTurnStoryResultSummary? PendingCampaignStoryResult(GameplaySession session) =>
        session.PendingNormalPeriod?.PendingStoryResult ?? session.PendingCampaignEventCommand?.PendingResult;
    private static CampaignStoryForceRankContinuation? PendingCampaignStoryForceRank(GameplaySession session) =>
        session.PendingNormalPeriod?.PendingStoryForceRank ?? session.PendingCampaignEventCommand?.PendingForceRank;

    private CampaignEventCommandPlayback? _automaticCampaignEventCommandAttempt;

    private bool RefreshCampaignEventCommandContinuation(GameplaySession session,
        CampaignComposition composition, MapSceneView map, int player)
    {
        if (session.PendingCampaignEventCommand is not { } pending) return false;
        _campaignEventCommandReturnToList = true;
        if (TryOpenRulerDecision(session, StoryOwner(session), composition, map, player)) return true;
        if (TryHandleNormalStoryAudio(session, null, composition, map)) return true;
        if (TryOpenNormalStoryForceRank(session, null, composition, map)) return true;
        if (TryOpenNormalStoryResult(session, null, composition, map)) return true;
        if (pending.PendingStory is not null)
            return TryOpenStoryPresentation(session, null, composition, map);
        if (!pending.RequiresAutomaticContinuation(StoryOwner(session))) return true;
        // A failed worker stays available for an explicit retry or save/load.
        // Refresh does not spin the same immutable operation indefinitely.
        if (ReferenceEquals(_automaticCampaignEventCommandAttempt, pending)) return true;
        _automaticCampaignEventCommandAttempt = pending;
        ContinueCampaignStory(session, composition);
        return true;
    }

    private void ContinueCampaignStory(GameplaySession session, CampaignComposition composition,
        GameOrder? choice = null)
    {
        if (_advancingPeriod) return;
        if (choice is null && session.PendingNormalPeriod is null &&
            (session.PendingArmyProgressCandidate is not null || session.PendingManualTacticCandidate is not null))
        {
            if (session.CanRetryLegacyProgress) TryContinueLegacyManualProgress(session, composition);
            return;
        }
        _advancingPeriod = true;
        _periodAdvanceTask = ContinueCampaignStoryAsync(session, composition, choice);
    }

    private Task ContinueCampaignStoryAsync(GameplaySession session, CampaignComposition composition,
        GameOrder? choice = null) => session.PendingCampaignEventCommand is not null
        ? RunCampaignEventCommandAsync(session, composition, confirmation: choice switch
        {
            null => null,
            ConfirmRulerSuccessionOrder succession => succession,
            _ => throw new InvalidOperationException("The selected event does not own this decision.")
        })
        : AdvancePlayableAsync(session, composition, campaignChoice: choice);
}
