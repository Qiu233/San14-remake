namespace San14.Core;

/// <summary>A single radio pair. First=true selects attack, allow retreat,
/// pursue, or retreat after the action, respectively.</summary>
public enum UnitBehaviorSetting { AttackNearby, AllowRetreat, Pursue, AfterRetreat }

public readonly record struct UnitBehaviorEligibility(bool? Allowed, string? Reason = null,
    string? MissingRuleKey = null);

/// <summary>Current commander restrictions shared by player and AI commands.
/// Evaluation borrows the current world and does not execute or alter an order.</summary>
public interface IPlayableUnitBehaviorRules
{
    UnitBehaviorEligibility EvaluateUnitBehaviors(GameState state, IReadOnlyList<UnitState> units, DeploymentBehavior behavior);

    /// <summary>Validate only the selected setting for the entire selection.
    /// Each unit retains its other three saved settings. Implementations may
    /// capture current commander traits once for this batch.</summary>
    UnitBehaviorEligibility EvaluateUnitBehaviorSetting(GameState state, IReadOnlyList<UnitState> units,
        UnitBehaviorSetting setting, bool first) => new(null,
            "Current commander restrictions for a single behavior setting are unavailable.",
            "rules.unit_behavior_setting");
}
