namespace San14.Core;

/// <summary>An ordered overseas caravan and its actual offered cargo.</summary>
public sealed record DispatchForeignTradeOrder(int ForceId, int CountryId,
    int[] ExecutorOfficerIds, int Gold, int Supplies, int Troops, int? ItemId) : GameOrder(ForceId);

/// <summary>Confirms the actual returned caravan report, never its departure.</summary>
public sealed record CompleteForeignTradeReportOrder(int ForceId, int TaskId) : GameOrder(ForceId);

public interface IForeignTradeCommandRules
{
    CommandResult DispatchForeignTrade(GameState state, ForceState force,
        DispatchForeignTradeOrder order, CommandContext context);
    CommandResult CompleteForeignTradeReport(GameState state, ForceState force,
        CompleteForeignTradeReportOrder order, CommandContext context);
}

public sealed class ForeignTradeOrders(IOrderRules next, IForeignTradeCommandRules? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not (DispatchForeignTradeOrder or CompleteForeignTradeReportOrder))
            return next.Apply(state, force, order, context);
        if (rules is null) return CommandResult.RuleUnavailable("foreign-trade.rules", "Foreign trade rules are unavailable.");
        if (!force.IsActive)
            return CommandResult.Reject("Foreign trade requires an active force.", "foreign-trade.scope");
        if (context.DelegatedDistrictId is int delegated)
        {
            if (!state.Districts.TryGetValue(delegated, out var actorDistrict) || actorDistrict.ForceId != force.Id)
                return CommandResult.Reject("Foreign trade district does not belong to this force.", "foreign-trade.scope");
            var indexes = state.ArmyLifecycle?.Indexes;
            if (indexes is null || !indexes.Registries.TryGetValue(WorldEntityKind.District, out var districts) ||
                districts is null)
                return CommandResult.RuleUnavailable("foreign-trade.primary_district", "The force primary district is unresolved.");
            int? primary = null;
            // Primary selection follows the actual registered district order.
            // A ruler-led district can precede the district numbered one.
            foreach (var entry in districts)
            {
                if (entry is not { Kind: WorldEntityKind.District, Id: int id } ||
                    !state.Districts.TryGetValue(id, out var district))
                    return CommandResult.RuleUnavailable("foreign-trade.primary_district", "A registered district is unresolved.");
                if (district.ForceId != force.Id) continue;
                if (district.Number == 1)
                {
                    primary = district.Id;
                    break;
                }
                int? leader = district.LeaderId;
                // A vacant domain appointment alone does not establish a
                // known empty source store. Consume only its saved empty word.
                if (leader is null && state.NativeForceUnregisterCurrent?.DistrictWindows10
                    .TryGetValue(id, out var fields) == true && fields is { Length: >= 4 } &&
                    fields[2] == 0 && fields[3] == 0)
                    leader = 0;
                if (leader is null || force.RulerId is not int ruler)
                    return CommandResult.RuleUnavailable("foreign-trade.primary_district", "A registered district leader or ruler is unresolved.");
                if (leader == ruler)
                {
                    primary = district.Id;
                    break;
                }
            }
            if (primary != delegated)
                return CommandResult.Reject("Foreign trade requires control of the force primary district.", "foreign-trade.scope");
        }
        return order switch
        {
            DispatchForeignTradeOrder dispatch when state.Phase == TurnPhase.Strategy =>
                rules.DispatchForeignTrade(state, force, dispatch, context),
            CompleteForeignTradeReportOrder report => rules.CompleteForeignTradeReport(state, force, report, context),
            _ => CommandResult.Reject("Foreign trade cannot be dispatched in this phase.", "foreign-trade.phase")
        };
    }
}
