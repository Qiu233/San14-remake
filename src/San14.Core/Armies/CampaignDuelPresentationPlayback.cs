namespace San14.Core;

/// <summary>Consumes only a completed duel's real presentation head. Numerical
/// registration and campaign aftermath have their own business lifecycle.</summary>
public static class CampaignDuelPresentationPlaybackRules
{
    public static CampaignRegisteredDuel? Pending(GameState state) =>
        state.CampaignDuelService is { CompletedPresentations.Length: > 0 } service
            ? service.CompletedPresentations[0] : null;

    /// <summary>Prepared completion hands actual aftermath notices to their
    /// existing presentation owners before removing this same duel atomically.</summary>
    public static bool TryComplete(GameState state, long expectedSequence,
        out GameState next, GameState? preparedState = null)
    {
        next = state;
        if (state.CampaignDuelService is not { } service ||
            Pending(state) is not { AftermathPresentation: { } aftermath } head ||
            head.Sequence != expectedSequence ||
            preparedState is null && (aftermath.Injuries.Length != 0 || aftermath.ArmyNotices.Length != 0))
            return false;
        var prepared = preparedState ?? state;
        //The trusted Data completion may update actual speech/RNG owners, but
        //must leave this completed-result queue owned by this exact source.
        if (!ReferenceEquals(prepared.CampaignDuelService, service)) return false;
        var remaining = new CampaignRegisteredDuel[service.CompletedPresentations.Length - 1];
        Array.Copy(service.CompletedPresentations, 1, remaining, 0, remaining.Length);
        next = prepared with { CampaignDuelService = service with { CompletedPresentations = remaining } };
        return true;
    }
}
