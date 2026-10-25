namespace San14.Core;

public sealed record ConfirmSelfRecommendationOrder(int ForceId, int OfficerId, bool Accepted) : GameOrder(ForceId);

public interface INormalTurnSelfRecommendationRules
{
    CommandResult ConfirmNormalTurnSelfRecommendation(GameState state,
        ConfirmSelfRecommendationOrder order, CommandContext context);
}

public sealed class SelfRecommendationOrders(IOrderRules next, INormalTurnSelfRecommendationRules? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not ConfirmSelfRecommendationOrder confirm) return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || context.DelegatedDistrictId is not null ||
            state.NativeNormalTurnContinuation?.PendingSelfRecommendation is not { } request ||
            request.ForceId != force.Id || confirm.OfficerId != request.OfficerId)
            return CommandResult.Reject("This officer recommendation is not awaiting a decision.", "recommendation.pending");
        return rules?.ConfirmNormalTurnSelfRecommendation(state, confirm, context) ??
            CommandResult.RuleUnavailable("recommendation.rules", "Officer recommendation rules are unavailable.");
    }
}
