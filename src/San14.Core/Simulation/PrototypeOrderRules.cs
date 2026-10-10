namespace San14.Core;

/// <summary>
/// Structural prototype for command plumbing. Its costs, availability checks and
/// initial unit morale have not been compared with the installed game.
/// </summary>
public sealed class PrototypeOrderRules : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (force.OrdersRemaining is null or < 1)
            return CommandResult.Reject("No orders remain.");
        return order switch
        {
            AssignOverseerOrder assignment => AssignOverseer(state, force, assignment),
            MarchOrder march => March(state, force, march),
            _ => CommandResult.Reject("Order type is unsupported.")
        };
    }

    private static CommandResult AssignOverseer(GameState state, ForceState force,
        AssignOverseerOrder order)
    {
        if (!state.Cities.TryGetValue(order.CityId, out CityState? city) || city.ForceId != force.Id)
            return CommandResult.Reject("City is not controlled by this force.");
        if (!state.Areas.TryGetValue(order.AreaId, out AreaState? area) || area.CityId != city.Id)
            return CommandResult.Reject("Area does not belong to the city.");
        if (!state.Officers.TryGetValue(order.OfficerId, out OfficerState? officer) ||
            officer.LocationId != city.Id || officer.ForceId != force.Id || officer.Deployed)
            return CommandResult.Reject("Officer is unavailable in this city.");
        if (state.Areas.Values.Any(a => a.Id != area.Id && a.Administration?.OverseerId == officer.Id))
            return CommandResult.Reject("Officer already oversees another area.");

        var forces = new Dictionary<int, ForceState>(state.Forces)
        {
            [force.Id] = force with { OrdersRemaining = force.OrdersRemaining - 1 }
        };
        var areas = new Dictionary<int, AreaState>(state.Areas)
        {
            [area.Id] = area with { Administration = new AreaAdministrationState(officer.Id,
                area.Administration?.DevelopmentFocus) }
        };
        return CommandResult.Accept(state with { Forces = forces, Areas = areas });
    }

    private static CommandResult March(GameState state, ForceState force, MarchOrder order)
    {
        if (!state.Cities.TryGetValue(order.CityId, out CityState? city) || city.ForceId != force.Id)
            return CommandResult.Reject("City is not controlled by this force.");
        if (!state.Officers.TryGetValue(order.OfficerId, out OfficerState? officer) ||
            officer.LocationId != city.Id || officer.ForceId != force.Id || officer.Deployed)
            return CommandResult.Reject("Officer is unavailable in this city.");
        if (order.Troops < 1 || order.Troops > city.Troops || order.Supplies < 0 || order.Supplies > city.Supplies)
            return CommandResult.Reject("The city lacks the requested troops or supplies.");
        if (city.HexId is not int cityHex)
            return CommandResult.Reject("City map position is unavailable.");
        if (order.DestinationHexId < 0 || order.DestinationHexId >= state.MapWidth * state.MapHeight ||
            order.DestinationHexId == city.HexId)
            return CommandResult.Reject("Destination is invalid.");

        state = ArmyIdentityAllocation.Initialize(state);
        int unitId = state.ArmyIdentities!.Candidate;
        var forces = new Dictionary<int, ForceState>(state.Forces)
        {
            [force.Id] = force with { OrdersRemaining = force.OrdersRemaining - 1 }
        };
        var cities = new Dictionary<int, CityState>(state.Cities)
        {
            [city.Id] = city with { Troops = city.Troops - order.Troops,
                Supplies = city.Supplies - order.Supplies }
        };
        var officers = new Dictionary<int, OfficerState>(state.Officers)
        {
            [officer.Id] = officer with { Deployed = true }
        };
        var units = new Dictionary<int, UnitState>(state.Units)
        {
            [unitId] = new UnitState(unitId, force.Id, officer.Id, cityHex,
                order.DestinationHexId, order.Troops, order.Supplies, new(100, null))
        };
        return CommandResult.Accept(state with
        {
            Forces = forces, Cities = cities, Officers = officers, Units = units,
            ArmyIdentities = state.ArmyIdentities.Advance()
        });
    }
}
