namespace San14.Core;

/// <summary>The actual report-to-strategy entry, distinct from loading or
/// command-budget refresh. Numerical and proposal source rules own its work.</summary>
public interface IPlayableStrategyEntryRules
{
    GameState EnterStrategyAfterReport(GameState state);
}
