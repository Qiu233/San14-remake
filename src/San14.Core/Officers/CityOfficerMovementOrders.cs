namespace San14.Core;

/// <summary>CityId is the executing source for Dispatch and the destination
/// for Summon. A batch is admitted and published once.</summary>
public sealed record PrepareOfficersMoveOrder(int ForceId, int CityId, int? DestinationCityId,
    IReadOnlyList<int> OfficerIds, OfficerMoveMode Mode) : GameOrder(ForceId);

public sealed record CancelOfficerMovementOrder(int ForceId, int TaskId) : GameOrder(ForceId);

public interface ICityOfficerMovementExecution
{
    CommandResult Move(GameState state, ForceState force, PrepareOfficersMoveOrder order, CommandContext context);
}

internal static class CityOfficerMovementOrders
{
    internal static CommandResult Cancel(GameState state, ForceState force, CancelOfficerMovementOrder order,
        CommandContext context)
    {
        if (state.PersonnelCommands is not { } owner || !owner.Returns.TryGetValue(order.TaskId, out var task) ||
            !task.IsReassignment || task.IsRetiring)
            return CommandResult.Reject("Personnel movement is no longer active.", "officer_move.task");
        if (!state.Districts.TryGetValue(task.DistrictId, out var district) || district.ForceId != force.Id ||
            (context.DelegatedDistrictId is int delegated ? delegated != district.Id : district.Number != 1) ||
            task.MemberIds.Any(id => !state.Officers.TryGetValue(id, out var person) || person.ForceId != force.Id))
            return CommandResult.Reject("Personnel movement is outside the authorized district.", "officer_move.district_scope");
        var updated = owner with { Returns = new(owner.Returns)
            { [task.Id] = task with { CancellationRequested = true } } };
        return CommandResult.Accept(state with { PersonnelCommands = updated });
    }

    internal static CommandResult Apply(GameState state, ForceState force, PrepareOfficersMoveOrder order,
        CommandContext context, ICityOfficerMovementExecution? execution)
    {
        if (!Enum.IsDefined(order.Mode) || order.OfficerIds is not { Count: > 0 } ||
            order.OfficerIds.Any(id => id is < 1 or > 6000) || order.OfficerIds.Distinct().Count() != order.OfficerIds.Count)
            return CommandResult.Reject("Invalid personnel movement selection.", "officer_move.input");
        if (!state.Cities.TryGetValue(order.CityId, out var city) || city.ForceId != force.Id)
            return CommandResult.Reject("Executing city is not controlled by this force.", "officer_move.city");
        if (city.DistrictId is not int districtId || !state.Districts.TryGetValue(districtId, out var district))
            return CommandResult.RuleUnavailable("rules.city_district", "Personnel command district is unresolved.");
        if (context.DelegatedDistrictId is int delegated ? delegated != districtId : district.Number != 1)
            return CommandResult.Reject("Personnel command is outside the authorized district.", "officer_move.district_scope");
        foreach (int id in order.OfficerIds)
            if (!state.Officers.TryGetValue(id, out var person) || person.ForceId != force.Id ||
                PlayableDiplomacy.IsOfficerOnMission(state, id) ||
                state.PreparedOfficerMoves?.ContainsKey(id) == true || state.PreparedTalentOrders?.ContainsKey(id) == true ||
                state.PreparedDeployments.Values.Any(plan => plan.OfficerId == id))
                return CommandResult.Reject("Officer is not available to this force.", "officer_move.officer");
        return execution?.Move(state, force, order, context) ??
            CommandResult.RuleUnavailable("rules.city_officer_movement", "Personnel movement execution is unavailable.");
    }
}
