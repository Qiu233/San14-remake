using System.Collections.Immutable;

namespace San14.Core;

/// <summary>The actual Strategy parent's retained Force queue and cursor.
/// The queue is copied once at the source Strategy entry. It is not derived
/// again when returning from the player's command stage or loading a save.</summary>
public sealed record OriginalAiStrategySchedule(ImmutableArray<int> ForceIds,
    int NextForceIndex, bool AwaitingPlayerCompletion);

public sealed partial record GameState
{
    /// <summary>Null means no known installed original-policy Strategy
    /// schedule. Restore never manufactures an empty queue.</summary>
    public OriginalAiStrategySchedule? OriginalAiStrategySchedule { get; init; }
}
