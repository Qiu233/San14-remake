namespace San14.Core;

public sealed record ConfirmCoalitionInvitationOrder(int ForceId, int TargetForceId, bool Accepted) : GameOrder(ForceId);

public interface INormalTurnCoalitionInvitationRules
{
    CommandResult ConfirmNormalTurnCoalitionInvitation(GameState state,
        ConfirmCoalitionInvitationOrder order, CommandContext context);
}

public sealed class CoalitionInvitationOrders(IOrderRules next, INormalTurnCoalitionInvitationRules? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not ConfirmCoalitionInvitationOrder confirm) return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || context.DelegatedDistrictId is not null ||
            state.NativeNormalTurnContinuation is not { NextAction: 25, PendingCoalitionInvitation: { } request } ||
            request.PlayerForceId != force.Id || request.TargetForceId != confirm.TargetForceId)
            return CommandResult.Reject("This coalition invitation is not awaiting a decision.", "coalition.invitation_pending");
        return rules?.ConfirmNormalTurnCoalitionInvitation(state, confirm, context) ??
            CommandResult.RuleUnavailable("coalition.invitation_rules", "Coalition invitation rules are unavailable.");
    }
}
