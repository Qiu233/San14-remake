namespace San14.Core;

public enum TalentOrderKind { Search, Recruit }

public sealed record PreparedTalentOrder(int ExecutorOfficerId, int ForceId, int CityId,
    int DistrictId, TalentOrderKind Kind, int? TargetOfficerId);

public sealed record PrepareTalentSearchOrder(int ForceId, int CityId,
    int ExecutorOfficerId) : GameOrder(ForceId);

public sealed record PrepareTalentRecruitOrder(int ForceId, int CityId,
    int ExecutorOfficerId, int TargetOfficerId) : GameOrder(ForceId);

/// <summary>
/// Playable talent planning. One book per executor comes from installed command help;
/// exact success, travel duration and enemy-target eligibility remain separate rules.
/// </summary>
public sealed class PlayableTalentOrders(IOrderRules otherCommands) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order,
        CommandContext context)
    {
        int cityId, executorId;
        int? targetId;
        TalentOrderKind kind;
        switch (order)
        {
            case PrepareTalentSearchOrder search:
                (cityId, executorId, targetId, kind) =
                    (search.CityId, search.ExecutorOfficerId, null, TalentOrderKind.Search);
                break;
            case PrepareTalentRecruitOrder recruit:
                (cityId, executorId, targetId, kind) =
                    (recruit.CityId, recruit.ExecutorOfficerId, recruit.TargetOfficerId,
                        TalentOrderKind.Recruit);
                break;
            default:
                return otherCommands.Apply(state, force, order, context);
        }
        if (!state.Cities.TryGetValue(cityId, out CityState? city) || city.ForceId != force.Id)
            return CommandResult.Reject("Talent command requires an owned city.", "talent.city");
        if (city.DistrictId is not int districtId ||
            !state.Districts.TryGetValue(districtId, out DistrictState? district))
            return CommandResult.RuleUnavailable("rules.city_district",
                "Talent command district is unresolved.");
        if (context.DelegatedDistrictId is int delegated ? delegated != district.Id : district.Number != 1)
            return CommandResult.Reject("Talent command is outside the authorized district.",
                "talent.district_scope");
        if (state.PreparedTalentOrders is null)
            return CommandResult.RuleUnavailable("rules.talent_planning",
                "Talent planning state is unresolved.");
        if (!state.Officers.TryGetValue(executorId, out OfficerState? executor) ||
            executor.ForceId != force.Id || executor.LocationId != city.Id ||
            executor.Presence != OfficerPresence.Present || executor.Deployed || PlayableOfficerAvailability.IsSourceBusy(state, executor) ||
            state.PreparedTalentOrders.ContainsKey(executorId) ||
            state.PreparedOfficerMoves?.ContainsKey(executorId) == true ||
            state.PreparedDeployments.Values.Any(plan => plan.OfficerId == executorId) ||
            state.Cities.Values.Any(active => active.Duties?.RecruiterId == executorId ||
                active.Duties?.TrainerId == executorId) ||
            state.Areas.Values.Any(area => area.Administration?.OverseerId == executorId))
            return CommandResult.Reject("Talent command executor is unavailable.", "talent.executor");
        if (kind == TalentOrderKind.Recruit &&
            (!state.Officers.TryGetValue(targetId!.Value, out OfficerState? target) ||
             target.Role != OfficerRole.Free || target.Presence != OfficerPresence.Present ||
             target.ForceId is not null || target.LocationId != city.Id ||
             state.PreparedTalentOrders.Values.Any(plan => plan.TargetOfficerId == targetId)))
            return CommandResult.Reject("Known free target is unavailable in this city.",
                "talent.target");
        if (district.OrdersRemaining is not int remaining)
            return CommandResult.RuleUnavailable("rules.command_budget",
                "District command books are unresolved.");
        if (remaining - CommandBookAvailability.Reserved(state, district.Id) < 1)
            return CommandResult.Reject("District lacks an uncommitted command book.",
                "talent.insufficient_orders");
        var plans = new Dictionary<int, PreparedTalentOrder>(state.PreparedTalentOrders)
        {
            [executorId] = new PreparedTalentOrder(executorId, force.Id, city.Id,
                district.Id, kind, targetId)
        };
        return CommandResult.Accept(state with
        {
            PreparedTalentOrders = plans,
            Districts = new Dictionary<int, DistrictState>(state.Districts)
            {
                [district.Id] = district with { OrdersRemaining = remaining - 1 }
            }
        });
    }
}
