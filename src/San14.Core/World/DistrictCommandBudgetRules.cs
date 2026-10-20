namespace San14.Core;

/// <summary>Replaces each reached district's current command books at the
/// campaign period boundary. Providers publish all changes together.</summary>
public interface IPlayableDistrictCommandBudgetRules
{
    GameState RefreshDistrictCommandBudgets(GameState state);
    /// <summary>Current cancellation ceiling; null is unresolved. A stored
    /// period quote is not a substitute for the current leader's abilities.</summary>
    int? ReadDistrictCommandReturnLimit(GameState state, int districtId);
}
