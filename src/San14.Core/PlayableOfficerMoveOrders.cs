namespace San14.Core;

public sealed record PrepareOfficerMoveOrder(int ForceId, int SourceCityId,
    int DestinationCityId, int OfficerId,
    OfficerMoveMode Mode = OfficerMoveMode.Dispatch) : GameOrder(ForceId);

/// <summary>Provisional between-city officer assignment; execution occurs at the next period.</summary>
public sealed class PlayableOfficerMoveOrders(IOrderRules otherCommands) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order,
        CommandContext context)
    {
        if (order is not PrepareOfficerMoveOrder move)
            return otherCommands.Apply(state, force, order, context);
        if (!Enum.IsDefined(move.Mode))
            return CommandResult.Reject("Officer move mode is invalid.", "officer_move.mode");
        if (!state.Cities.TryGetValue(move.SourceCityId, out CityState? source) ||
            source.ForceId != force.Id)
            return CommandResult.Reject("Officer move source city is unavailable.", "officer_move.source");
        if (!state.Cities.TryGetValue(move.DestinationCityId, out CityState? destination) ||
            destination.ForceId != force.Id || destination.Id == source.Id)
            return CommandResult.Reject("Officer move target must be another friendly city.",
                "officer_move.destination");
        CityState executingCity = move.Mode == OfficerMoveMode.Summon ? destination : source;
        if (executingCity.DistrictId is not int districtId ||
            !state.Districts.TryGetValue(districtId, out DistrictState? district))
            return CommandResult.RuleUnavailable("rules.city_district",
                "Officer move command district is unresolved.");
        if (context.DelegatedDistrictId is int delegated ? delegated != district.Id : district.Number != 1)
            return CommandResult.Reject("Officer move command is outside the authorized district.",
                "officer_move.district_scope");
        if (state.PreparedOfficerMoves is null)
            return CommandResult.RuleUnavailable("rules.officer_moves", "Officer planning state is unresolved.");
        int id = move.OfficerId;
        if (!state.Officers.TryGetValue(id, out OfficerState? officer) ||
            officer.ForceId != force.Id || officer.LocationId != source.Id ||
            officer.Presence != OfficerPresence.Present || officer.Deployed || PlayableOfficerAvailability.IsSourceBusy(state, officer) ||
            officer.Role != OfficerRole.Officer || state.PreparedOfficerMoves.ContainsKey(id) ||
            state.PreparedTalentOrders?.ContainsKey(id) == true ||
            state.PreparedDeployments.Values.Any(plan => plan.OfficerId == id) ||
            state.Cities.Values.Any(city => city.GovernorId == id ||
                city.Duties?.RecruiterId == id || city.Duties?.TrainerId == id) ||
            state.Districts.Values.Any(active => active.LeaderId == id) ||
            state.Areas.Values.Any(area => area.Administration?.OverseerId == id))
            return CommandResult.Reject("Officer is unavailable for a city move.",
                "officer_move.officer");
        if (district.OrdersRemaining is not int remaining)
            return CommandResult.RuleUnavailable("rules.command_budget", "District command books are unresolved.");
        if (remaining - CommandBookAvailability.Reserved(state, district.Id) < 1)
            return CommandResult.Reject("District lacks uncommitted command books.",
                "officer_move.insufficient_orders");
        var plans = new Dictionary<int, PreparedOfficerMove>(state.PreparedOfficerMoves);
        plans.Add(id, new PreparedOfficerMove(id, force.Id, source.Id, destination.Id,
            district.Id, move.Mode));
        return CommandResult.Accept(state with
        {
            PreparedOfficerMoves = plans,
            Districts = new Dictionary<int, DistrictState>(state.Districts)
            {
                [district.Id] = district with { OrdersRemaining = remaining - 1 }
            }
        });
    }
}
