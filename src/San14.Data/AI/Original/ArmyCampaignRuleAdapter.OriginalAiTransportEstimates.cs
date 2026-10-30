using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Computes the selected receivers' transport troop ceilings.
    /// Inputs are source coefficients and locations, never caller-provided
    /// planning results. This does not spend or transfer actual troop stocks.</summary>
    public CommandResult EstimateTransportTroopCeilings(GameState state, ForceState force,
        EstimateTransportTroopCeilingsOrder order, CommandContext context)
    {
        try
        {
            if (order.LocationIds.Count == 0) return CommandResult.Accept(state);
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var memory = state.OriginalAiStrategyMemory;
            var rows = memory?.Footholds.ToBuilder();
            var needs = new Dictionary<int, AiBaseNeedsOperation>();
            foreach (int location in order.LocationIds)
            {
                var at = BaseLookup.Find(location, current);
                if (!TransportEstimateKnown(current.ReadBaseNativeValidity(at), "Base_validity")) continue;
                int actualLocation = TransportEstimateKnown(current.ReadBaseObjectLink(at), "Base_location");
                if (rows is null) throw TransportEstimateMissing("memory_owner");
                if (!rows.TryGetValue(actualLocation, out var before))
                    throw TransportEstimateMissing("memory_receiver." + actualLocation);
                int owner = TransportEstimateKnown(current.ReadBaseForceSlot(at), "Base_Force");
                if (!needs.TryGetValue(owner, out var operation))
                    needs[owner] = operation = new(this, state, owner, current);
                var incoming = operation.Incoming(at, food: true);
                int foodNeed = OriginalAiBaseNeedsMath.CalculateFood(new(operation.UsableTroops(at), incoming.Troops,
                    before.FoodForecast ?? throw TransportEstimateMissing("food_forecast." + actualLocation),
                    operation.CurrentFood(at), incoming.Cargo), order.TroopConsumptionPercent,
                    order.SupplyHorizon, order.IncomeHorizon);
                // Base virtual C8 is the ordinary troop CAPACITY output of
                // 268230. Reading current Base3C here changes the algorithm.
                int capacity = BasePostLoad.ReadResourceCapacities(at, current).Troops;
                int ceiling = foodNeed > 0 ? Math.Min(0, capacity) : Math.Max(0, capacity);
                rows[actualLocation] = before with { TransportTroopCeiling = ceiling };
            }
            return rows is null ? CommandResult.Accept(state) : CommandResult.Accept(state with
                { OriginalAiStrategyMemory = memory! with { Footholds = rows.ToImmutable() } });
        }
        catch (InvalidDataException missing)
        { return CommandResult.RuleUnavailable(missing.Message, missing.Message); }
    }

    internal int ReadOriginalAiTransportTroopCeiling(GameState state, int location) =>
        state.OriginalAiStrategyMemory?.Footholds.GetValueOrDefault(location)?.TransportTroopCeiling
            ?? throw TransportEstimateMissing("unproduced_ceiling." + location);

    private static T TransportEstimateKnown<T>(T? value, string field) where T : struct =>
        value ?? throw TransportEstimateMissing(field);
    private static InvalidDataException TransportEstimateMissing(string field) =>
        new("original_ai.transport_troop_ceiling." + field);
}
