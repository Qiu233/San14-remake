namespace San14.Core;

public sealed partial class GameSimulation
{
    public CampaignRegisteredDuel? PendingDuelPresentation => _progress is { ArmyProgress: null }
        ? null : CampaignDuelPresentationPlaybackRules.Pending(SourcePresentationOwner);

    /// <summary>The host completes a real duel presentation against its exact
    /// world. Its prepared aftermath handoff and queue removal publish together.</summary>
    public bool TryCompleteDuelPresentation(GameState expectedOwner, long expectedSequence,
        GameState? preparedState = null)
    {
        var pending = _normalPeriod;
        var owner = SourcePresentationOwner;
        if (_progress is { ArmyProgress: null } || !ReferenceEquals(owner, expectedOwner) ||
            pending is not null && !NormalPeriodOperation.HasContinuation(pending.Candidate) ||
            !CampaignDuelPresentationPlaybackRules.TryComplete(owner, expectedSequence,
                out var completed, preparedState)) return false;
        PublishSourcePresentationOwner(completed, pending);
        return true;
    }
}
