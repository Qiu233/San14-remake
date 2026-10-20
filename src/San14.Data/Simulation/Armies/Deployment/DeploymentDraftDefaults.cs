using San14.Core;

namespace San14.Data;

/// <summary>CE ordinary UI's two-choice settings, independently owned by the world.
/// The source's additional numeric states have no proved Core behavior translation.</summary>
public static class DeploymentDraftDefaults
{
    // Offline inspected CE rule data: the minimum new-summary allocation, not a
    // scenario's troop default and not the formation's eligibility minimum.
    public const int CeMinimumNewUnitTroops = 1000;

    public static GameState InstallFresh(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.DefaultDeploymentBehavior is not null || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 })
            throw new InvalidDataException("Deployment preferences may only be installed in a fresh world.");
        // State reset, paired CE normal UI: 1 / 0 / 1 / 2.
        return DeploymentDraftFigureRules.InstallFresh(state with
            { DefaultDeploymentBehavior = new(false, false, true, DeploymentAfterAction.Stay) });
    }

    public static DeploymentBehavior? Decode(int enemyApproach, int retreat, int pursuit, int afterAction) =>
        enemyApproach is 1 or 2 && retreat is 0 or 1 && pursuit is 0 or 1 && afterAction is 1 or 2
            ? new(enemyApproach == 2, retreat == 1, pursuit == 1,
                afterAction == 1 ? DeploymentAfterAction.Retreat : DeploymentAfterAction.Stay)
            : null;

    public static DeploymentPreferenceEncoding Encode(DeploymentBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        if (!Enum.IsDefined(behavior.AfterAction)) throw new InvalidDataException("Deployment after-action setting is invalid.");
        return new(behavior.AttackNearbyEnemies ? 2 : 1, behavior.AllowRetreat ? 1 : 0,
            behavior.PursueRetreatingEnemies ? 1 : 0,
            behavior.AfterAction == DeploymentAfterAction.Retreat ? 1 : 2);
    }
}

/// <summary>The numeric packet boundary; domain preferences remain named behavior.</summary>
public sealed record DeploymentPreferenceEncoding(int EnemyApproach, int Retreat, int Pursuit, int AfterAction);
