using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableTransportFoodWarningRules
{
    /// <summary>The Make decision's real22FE10 quote transaction. Permission
    /// and cargo legality are supplied by the common order dispatcher. It
    /// posts no cargo or deployment, while preserving reached quote1 cleanup.</summary>
    public CommandResult PrepareTransportFoodWarning(GameState state, PrepareTransportFoodWarningOrder order)
    {
        try
        {
            var prepared = PrepareTransportFoodWarningCurrent(state, order.SourceCityId, order.CargoSupplies);
            return CommandResult.Accept(prepared.State) with { TransportFoodWarning = prepared.Warning };
        }
        catch (InvalidDataException error)
        {
            return CommandResult.RuleUnavailable("rules.transport_food_warning", error.Message);
        }
    }

    private (GameState State, TransportFoodWarningResult Warning) PrepareTransportFoodWarningCurrent(
        GameState state, int sourceCityId, int cargoFood)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        var source = TransportSource(state, current, sourceCityId);
        var city = CurrentGateMapQueries.FindCity(source, current);
        var quote = ReadCityCaptureFoodQuoteCurrent(state, current, city);
        int expense = quote.Value ?? throw new InvalidDataException(quote.UnresolvedOwner);
        var completed = quote.State ?? state;
        int stock = TK(current.ReadBaseResource(source, BaseResourceKind.Supplies), "warning-current-food");
        return (completed, new(unchecked(stock - unchecked(expense * 3) - cargoFood) < 1, expense));
    }
}
