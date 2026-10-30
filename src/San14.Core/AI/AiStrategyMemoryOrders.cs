namespace San14.Core;

//These commands request computations, never caller-supplied memory snapshots.
//Their coefficients are signed source inputs; the provider owns the exact
//arithmetic and cannot replace missing inputs with forecast defaults.
public sealed record EstimateAiFoodNeedsOrder(int ForceId, int TroopConsumptionPercent,
    int SupplyHorizon, int IncomeHorizon) : GameOrder(ForceId);
public sealed record EstimateAiGoldNeedsOrder(int ForceId, int IncomeHorizon) : GameOrder(ForceId);
public sealed record EstimateTransportTroopCeilingsOrder(int ForceId, IReadOnlyList<int> LocationIds,
    int TroopConsumptionPercent, int SupplyHorizon, int IncomeHorizon) : GameOrder(ForceId);
public sealed record SetDistrictPurposeOrder(int ForceId, int DistrictId, sbyte Purpose) : GameOrder(ForceId);

public interface IAiStrategyMemoryExecution
{
    CommandResult EstimateFoodNeeds(GameState state, ForceState force,
        EstimateAiFoodNeedsOrder order, CommandContext context);
    CommandResult EstimateGoldNeeds(GameState state, ForceState force,
        EstimateAiGoldNeedsOrder order, CommandContext context);
    CommandResult EstimateTransportTroopCeilings(GameState state, ForceState force,
        EstimateTransportTroopCeilingsOrder order, CommandContext context);
}

public interface IDistrictPurposeExecution
{
    CommandResult ExecuteDistrictPurpose(GameState state, ForceState force,
        SetDistrictPurposeOrder order, CommandContext context);
}

/// <summary>Formal strategy commands for current planning facts. They pass
/// through the same GameEngine actor/force authority as player orders. No
/// policy identity grants a bypass and no order can inject estimated results.</summary>
public sealed class AiStrategyMemoryOrders(IOrderRules next,
    IAiStrategyMemoryExecution? memory, IDistrictPurposeExecution? purposes) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not (EstimateAiFoodNeedsOrder or EstimateAiGoldNeedsOrder or EstimateTransportTroopCeilingsOrder or SetDistrictPurposeOrder))
            return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || !force.IsActive || order.ForceId != force.Id)
            return CommandResult.Reject("Planning commands require active strategy control.", "planning.phase_or_force");
        //A delegated district can derive planning quantities for its own
        //Force: the actual producer traverses that Force's complete Base list.
        //This does not authorize spending another district's resources.
        if (context.DelegatedDistrictId is int delegated &&
            (!state.Districts.TryGetValue(delegated, out var actorDistrict) || actorDistrict.ForceId != force.Id))
            return CommandResult.Reject("Delegated district belongs to another force.", "planning.district_scope");

        if (order is SetDistrictPurposeOrder purpose)
        {
            if (!state.Districts.TryGetValue(purpose.DistrictId, out var district) || district.ForceId != force.Id)
                return CommandResult.Reject("District belongs to another force.", "planning.purpose_district");
            if (context.DelegatedDistrictId is int selected ? selected != district.Id : district.Number != 1)
                return CommandResult.Reject("District is outside the authorized scope.", "planning.purpose_scope");
            return purposes?.ExecuteDistrictPurpose(state, force, purpose, context) ??
                CommandResult.RuleUnavailable("planning.district_purpose_rules", "District purpose writer is unavailable.");
        }
        if (memory is null)
            return CommandResult.RuleUnavailable("planning.memory_rules", "Planning estimate rules are unavailable.");
        if (order is EstimateTransportTroopCeilingsOrder transportSelection && (transportSelection.LocationIds is null ||
            transportSelection.LocationIds.Any(id => id is < 0 or > 61)))
            return CommandResult.Reject("Transport capacity estimate locations are invalid.", "planning.transport_locations");
        //No coefficient clamp is imposed here. The exact typed producer
        //accepts signed inputs. Transport capacity reads the actual supplied
        //location sequence without introducing an ownership filter.
        return order switch
        {
            EstimateAiFoodNeedsOrder food => memory.EstimateFoodNeeds(state, force, food, context),
            EstimateAiGoldNeedsOrder gold => memory.EstimateGoldNeeds(state, force, gold, context),
            EstimateTransportTroopCeilingsOrder transport => memory.EstimateTransportTroopCeilings(state, force, transport, context),
            _ => throw new InvalidOperationException("Planning command changed during dispatch.")
        };
    }
}
