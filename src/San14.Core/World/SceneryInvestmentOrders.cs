namespace San14.Core;

public sealed record InvestSceneryOrder(int ForceId, int SceneryId) : GameOrder(ForceId);

public interface IPlayableSceneryInvestmentRules
{
    CommandResult PrepareSceneryInvestment(GameState state, ForceState force,
        InvestSceneryOrder order, CommandContext context);
}

/// <summary>The normal strategy command delegates costs and the actual scenery
/// lifecycle to its source provider, without inventing a personnel task.</summary>
public sealed class SceneryInvestmentOrders(IOrderRules next, IPlayableSceneryInvestmentRules? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not InvestSceneryOrder investment) return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || !force.IsActive || context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Scenery investment requires direct strategy control.", "scenery.investment_scope");
        if (investment.SceneryId is < 0 or > 100)
            return CommandResult.Reject("The scenery identity is invalid.", "scenery.investment_identity");
        return rules?.PrepareSceneryInvestment(state, force, investment, context) ??
            CommandResult.RuleUnavailable("scenery.investment_rules", "Scenery investment rules are unavailable.");
    }
}
