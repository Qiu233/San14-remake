namespace San14.Core;

public enum CityMerchantMode { Sell = 0, Buy = 1 }

/// <summary>Ordered selection inputs replayed by the current-world merchant
/// rules. The limits retain signed arithmetic; no reward result is supplied.</summary>
public sealed record CityMerchantAutomaticSelection(int GoldLimit, int FoodLimit,
    IReadOnlyList<int> OfficerPriority);

/// <summary>Amount is food, in both directions. Prices and affordability are
/// read by the execution provider from the current city at submission.</summary>
public sealed record CityMerchantOrder(int ForceId, int CityId, IReadOnlyList<int> OfficerIds,
    CityMerchantMode Mode, int Amount, CityMerchantAutomaticSelection? AutomaticSelection = null) : GameOrder(ForceId);

public interface ICityMerchantExecution
{
    CommandResult Trade(GameState state, ForceState force, CityMerchantOrder order, CommandContext context);
}

public sealed class CityMerchantOrders(IOrderRules otherCommands, ICityMerchantExecution? execution) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not CityMerchantOrder trade) return otherCommands.Apply(state, force, order, context);
        if (!Enum.IsDefined(trade.Mode) || trade.Amount <= 0 || trade.OfficerIds is not { Count: > 0 } ||
            trade.OfficerIds.Any(id => id <= 0) || trade.OfficerIds.Distinct().Count() != trade.OfficerIds.Count)
            return CommandResult.Reject("Merchant trade input is invalid.", "merchant.input");
        if (trade.AutomaticSelection is { } automatic && (automatic.OfficerPriority is not { Count: > 0 } ||
            automatic.OfficerPriority.Any(id => id is < 0 or > 6000)))
            return CommandResult.Reject("Merchant automatic selection requires an ordered officer priority.", "merchant.automatic_selection");
        if (!state.Cities.TryGetValue(trade.CityId, out var city) || city.ForceId != force.Id)
            return CommandResult.Reject("City is not controlled by this force.", "merchant.city_control");
        if (city.DistrictId is not int districtId || !state.Districts.TryGetValue(districtId, out var district))
            return CommandResult.RuleUnavailable("rules.merchant_district", "City district is unresolved.");
        if (context.DelegatedDistrictId is int delegated ? delegated != districtId : district.Number != 1)
            return CommandResult.Reject("City is outside the authorized district.", "merchant.district_scope");
        if (execution is null)
            return CommandResult.RuleUnavailable("rules.city_merchant", "Merchant execution is unavailable.");
        return execution.Trade(state, force, trade, context);
    }
}

public sealed partial record CityState
{
    /// <summary>Current food units priced at ten gold. Null is unavailable
    /// legacy knowledge; querying or loading a city never installs a price.</summary>
    public byte? MerchantFoodPerTenGold { get; init; }
}
