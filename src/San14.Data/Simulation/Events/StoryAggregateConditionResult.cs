using San14.Data.Formats;

namespace San14.Data;

/// <summary>The predicate and the separate manager result written by an
/// ordinary Scenery comparison. A null kind means that leaf made no write.</summary>
public sealed record StoryAggregateConditionResult(bool Result,
    int? CurrentPlayerCityIncreaseKind = null);

/// <summary>Reached current-world inputs for the ordinary aggregate leaves.
/// Evaluating a predicate never publishes a world or a story outcome.</summary>
public interface IS14StoryAggregateConditionQueries
{
    StoryAggregateConditionResult EvaluateStoryAggregateConditionCurrent(int code,
        IReadOnlyList<int> arguments, ForceReference? force);
}
