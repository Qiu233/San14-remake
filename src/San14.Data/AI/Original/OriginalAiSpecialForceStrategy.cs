using San14.Core;

namespace San14.Data.AI.Original;

internal sealed record OriginalAiSpecialForceStrategyOrder(int ForceId) : GameOrder(ForceId);

internal static class OriginalAiSpecialForceStrategy
{
    internal static void Dispatch(CommandPlanningContext planning, ArmyCampaignRuleAdapter adapter, int force)
    {
        string actor = "original-ai-special:" + force;
        var result = planning.Apply(new OriginalAiSpecialForceStrategyOrder(force),
            new OriginalAiSpecialForceStrategyRules(adapter), new(actor), new SingleForceAuthority(force, actor));
        if (!result.Accepted)
            throw new InvalidDataException(result.MissingRuleKey ?? result.ErrorCode ?? result.Error);
    }
}

internal sealed class OriginalAiSpecialForceStrategyRules(ArmyCampaignRuleAdapter adapter) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not OriginalAiSpecialForceStrategyOrder special || special.ForceId != force.Id ||
            force.Id is not (>= 46 and <= 50) || state.Phase != TurnPhase.Strategy || context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Special Strategy requires its actual special Force owner.", "original-ai.special.scope");
        try { return CommandResult.Accept(adapter.ApplyOriginalAiSpecialForceStrategyCurrent(state, force.Id)); }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }
}
