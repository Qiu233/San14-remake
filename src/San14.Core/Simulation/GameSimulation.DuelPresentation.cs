namespace San14.Core;

public sealed partial class GameSimulation
{
    public CampaignRegisteredDuel? PendingDuelPresentation => _progress is null
        ? CampaignDuelPresentationPlaybackRules.Pending(_normalPeriod?.Candidate ?? State) : null;

    /// <summary>The host completes a real duel presentation against its exact
    /// world. Its prepared aftermath handoff and queue removal publish together.</summary>
    public bool TryCompleteDuelPresentation(GameState expectedOwner, long expectedSequence,
        GameState? preparedState = null)
    {
        var pending = _normalPeriod;
        var owner = pending?.Candidate ?? State;
        if (_progress is not null || !ReferenceEquals(owner, expectedOwner) ||
            pending is not null && !NormalPeriodOperation.HasContinuation(pending.Candidate) ||
            !CampaignDuelPresentationPlaybackRules.TryComplete(owner, expectedSequence,
                out var completed, preparedState)) return false;
        if (pending is not null) _normalPeriod = pending with { Candidate = completed };
        else State = completed;
        return true;
    }
}
