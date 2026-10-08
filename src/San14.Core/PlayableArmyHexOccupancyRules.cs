namespace San14.Core;

/// <summary>A detached occupancy draft for a decided ordinary traversal.
/// Null State keeps a reached source input unknown. Search, position previews
/// and controlled relocation do not invoke this writer.</summary>
public sealed record PlayableArmyHexOccupancyResult(GameState? State, string? UnresolvedOwner = null);

public interface IPlayableArmyHexOccupancyRules
{
    PlayableArmyHexOccupancyResult TryApplyTraversal(GameState state, UnitState actor, ReadOnlySpan<int> traversed);
}
