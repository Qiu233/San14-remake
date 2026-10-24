namespace San14.Core;

/// <summary>Owns waiting and every reached Army stage inside the caller's
/// private period. The provider returns an actual completed native clock and
/// calendar; Core retains the economy, date rollover and reporting boundary.</summary>
public interface IPlayableNativeArmyPeriodRules
{
    GameState CompleteArmyPeriod(GameState state, IPlayableNativeSubdayRules waitingRules);

    /// <summary>Actual post-Progress Turn food, after the month's income and
    /// before development/training/recruitment. Owns its ordered current bills.</summary>
    GameState CompleteArmyFoodTurn(GameState state);
}

/// <summary>Completes native owners around the coarse playable movement pass.
/// Waiting/calendar and the final post-movement view have separate boundaries.</summary>
public interface IPlayableNativePeriodContinuationRules
{
    GameState CompleteWaiting(GameState state);
    bool UsesSourceFacilityWork => false;

    /// <summary>Actual source-qualified Armies still waiting for a Base exit.
    /// Null means the provider does not own that native qualification.</summary>
    IReadOnlySet<int>? CaptureArmiesAwaitingAppearance(GameState state) => null;

    /// <summary>After movement, contacts, arrivals and retirement, before the
    /// completed period is published. Legacy providers have no additional work.</summary>
    GameState CompleteMovement(GameState state) => state;
}
