namespace San14.Core;

/// <summary>The reached strategy-to-progress source transition. It executes
/// pending proposals before creating this period's commands or summaries.</summary>
public interface IPlayableStrategyExitRules
{
    GameState ExitStrategyForProgress(GameState state);
}
