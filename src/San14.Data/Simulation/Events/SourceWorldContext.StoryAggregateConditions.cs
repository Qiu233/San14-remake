using San14.Core;

namespace San14.Data;

public sealed partial class SourceWorldContext : IS14StoryAggregateConditionQueries
{
    internal StoryAggregateConditionCurrent? StoryAggregateConditions { get; set; }

    public StoryAggregateConditionResult EvaluateStoryAggregateConditionCurrent(int code,
        IReadOnlyList<int> arguments, ForceReference? force) =>
        (StoryAggregateConditions ?? throw new InvalidDataException("Current story aggregate inputs are unknown."))
            .Evaluate(code, arguments, force);
}

/// <summary>A single detached view of the reached state and current tables.
/// The normal query binder replaces it after each business mutation.</summary>
internal sealed class StoryAggregateConditionCurrent(ArmyCampaignRuleAdapter owner,
    GameState state, SourceWorldContext current)
{
    internal StoryAggregateConditionResult Evaluate(int code, IReadOnlyList<int> arguments,
        ForceReference? force) => owner.EvaluateStoryAggregateConditionCurrent(state, current, code, arguments, force);
}
