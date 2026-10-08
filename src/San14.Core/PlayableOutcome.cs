namespace San14.Core;

public enum PlayableResult { Ongoing, Unified, Defeated }

/// <summary>A saved source terminal result takes precedence. Legacy/provisional
/// sessions retain their explicit manual city, force and date fallback.</summary>
public static class PlayableOutcome
{
    public static PlayableResult Evaluate(GameState state, int playerForceId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!PlayableSkeleton.IsPlayableRuleset(state.RulesetId) ||
            !state.Forces.ContainsKey(playerForceId))
            throw new InvalidOperationException("A playable player force is required.");
        if (state.NativePlayableTerminal is { } terminal)
            return terminal.PlayerForcePhysical == playerForceId
                ? terminal.Kind == NativePlayableTerminalKind.OrdinaryUnified ? PlayableResult.Unified : PlayableResult.Defeated
                : PlayableResult.Ongoing;
        bool hasOfficer = state.Officers.Values.Any(person => person.ForceId == playerForceId && person.Presence != OfficerPresence.Dead);
        if (!state.Forces[playerForceId].IsActive || state.Date.Year >= 351 ||
            !PlayableForceDefeat.OwnsCity(state, playerForceId) || !hasOfficer) return PlayableResult.Defeated;
        CityState[] cities = state.Cities.Values.Where(city => PlayableForceDefeat.IsConquestCity(city.Id)).ToArray();
        bool allCitiesOwned = cities.Length > 0 && cities.All(city => city.ForceId == playerForceId);
        bool noOtherOrdinaryForce = !state.Forces.Values.Any(force => force.Id != playerForceId &&
            force.IsActive && PlayableForceDefeat.IsOrdinaryForce(force.Id));
        return allCitiesOwned || noOtherOrdinaryForce ? PlayableResult.Unified : PlayableResult.Ongoing;
    }
}
