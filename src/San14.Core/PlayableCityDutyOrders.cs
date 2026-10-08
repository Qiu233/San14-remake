namespace San14.Core;

public enum CityDutyKind { Recruitment, Training }
public sealed record SetCityDutyOrder(int ForceId, int CityId, CityDutyKind Kind,
    int? OfficerId) : GameOrder(ForceId);

/// <summary>Provisional city-duty command path; exact original eligibility is still open.</summary>
public sealed class PlayableCityDutyOrders(IOrderRules otherCommands) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not SetCityDutyOrder dutyOrder)
            return otherCommands.Apply(state, force, order, context);
        if (!Enum.IsDefined(dutyOrder.Kind))
            return CommandResult.Reject("City duty kind is invalid.", "duty.kind");
        if (!state.Cities.TryGetValue(dutyOrder.CityId, out CityState? city) || city.ForceId != force.Id)
            return CommandResult.Reject("City is not controlled by this force.", "duty.city_control");
        if (city.DistrictId is not int districtId ||
            !state.Districts.TryGetValue(districtId, out DistrictState? district))
            return CommandResult.RuleUnavailable("rules.city_district", "City district is unresolved.");
        if (context.DelegatedDistrictId is int delegated ? delegated != district.Id : district.Number != 1)
            return CommandResult.Reject("City is outside the authorized district.", "duty.district_scope");
        if (city.Duties is not CityDutyState duties)
            return CommandResult.RuleUnavailable("rules.city_duties", "Current duty appointments are unresolved.");
        int? previous = dutyOrder.Kind == CityDutyKind.Recruitment ? duties.RecruiterId : duties.TrainerId;
        if (previous == dutyOrder.OfficerId) return CommandResult.Accept(state);
        if (dutyOrder.OfficerId is int officerId)
        {
            if (!state.Officers.TryGetValue(officerId, out OfficerState? officer) ||
                officer.ForceId != force.Id || officer.LocationId != city.Id ||
                officer.Presence != OfficerPresence.Present || officer.Deployed || PlayableOfficerAvailability.IsSourceBusy(state, officer) ||
                state.PreparedOfficerMoves?.ContainsKey(officerId) == true ||
                state.PreparedTalentOrders?.ContainsKey(officerId) == true ||
                state.PreparedDeployments.Values.Any(plan => plan.OfficerId == officerId))
                return CommandResult.Reject("Officer is unavailable for this city duty.", "duty.officer");
        }
        if (district.OrdersRemaining is not int remaining)
            return CommandResult.RuleUnavailable("rules.command_budget", "District command books are unresolved.");
        if (remaining - CommandBookAvailability.Reserved(state, district.Id) < 1)
            return CommandResult.Reject("District lacks an uncommitted command book.", "duty.insufficient_orders");
        CityDutyState nextDuties = dutyOrder.Kind switch
        {
            CityDutyKind.Recruitment => duties with
            {
                RecruiterId = dutyOrder.OfficerId, PreparationPeriods = 0
            },
            _ => duties with { TrainerId = dutyOrder.OfficerId }
        };
        return CommandResult.Accept(state with
        {
            Cities = new Dictionary<int, CityState>(state.Cities)
            {
                [city.Id] = city with { Duties = nextDuties }
            },
            Districts = new Dictionary<int, DistrictState>(state.Districts)
            {
                [district.Id] = district with { OrdersRemaining = remaining - 1 }
            }
        });
    }
}
