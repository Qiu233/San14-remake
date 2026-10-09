namespace San14.Core;

/// <summary>Original preparation/cancellation contracts. Unknown rules have no permissive defaults.</summary>
public sealed class DeploymentCommandRules(IDeploymentEligibility eligibility, IDeploymentResourceRules resources) : IOrderRules
{
    public const int MaximumUnitsPerBatch = 9;
    private readonly IDeploymentEligibility _eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
    private readonly IDeploymentResourceRules _resources = resources ?? throw new ArgumentNullException(nameof(resources));

    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context) => order switch
    {
        PrepareDeploymentOrder prepare => Prepare(state, force, prepare, context),
        CancelPreparedDeploymentOrder cancel => Cancel(state, force, cancel, context),
        _ => CommandResult.Reject("Order type is unsupported.", "command.unsupported")
    };

    private static (DeploymentSource? Source, CommandResult? Error) ResolveSource(GameState state,
        int forceId, int locationId, CommandContext context)
    {
        int? owner, districtId, hex;
        int troops;
        if (state.Cities.TryGetValue(locationId, out CityState? city))
            (owner, districtId, hex, troops) = (city.ForceId, city.DistrictId, city.HexId, city.Troops);
        else if (state.Gates.TryGetValue(locationId, out GateState? gate))
            (owner, districtId, hex, troops) = (gate.ForceId, gate.DistrictId, gate.HexId, gate.Troops);
        else return (null, CommandResult.Reject("Deployment source does not exist.", "deployment.source_missing"));
        if (owner != forceId)
            return (null, CommandResult.Reject("Deployment source belongs to another force.", "deployment.source_control"));
        if (districtId is not int id || !state.Districts.TryGetValue(id, out DistrictState? district) || hex is null)
            return (null, CommandResult.RuleUnavailable("rules.deployment_source", "Source district or position is unresolved."));
        if (context.DelegatedDistrictId is int delegated ? delegated != id : district.Number != 1)
            return (null, CommandResult.Reject("Deployment source is outside the authorized district.", "deployment.district_scope"));
        return (new DeploymentSource(locationId, forceId, id, hex.Value, troops), null);
    }

    private CommandResult Prepare(GameState state, ForceState force, PrepareDeploymentOrder order, CommandContext context)
    {
        (DeploymentSource? source, CommandResult? error) = ResolveSource(state, force.Id, order.SourceLocationId, context);
        if (error is not null) return error;
        if (order.Units is null || order.Units.Count < 1 || order.Units.Count > MaximumUnitsPerBatch)
            return CommandResult.Reject("Prepare between one and nine units at once.", "deployment.batch_size");
        var officers = new HashSet<int>();
        var units = new List<DeploymentUnitRequest>();
        long requestedTroops = 0;
        foreach (DeploymentUnitRequest unit in order.Units)
        {
            if (unit is null || !officers.Add(unit.OfficerId))
                return CommandResult.Reject("Each officer may lead only one unit.", "deployment.duplicate_officer");
            if (!state.Officers.TryGetValue(unit.OfficerId, out OfficerState? officer) ||
                officer.ForceId != force.Id || officer.LocationId != source!.LocationId)
                return CommandResult.Reject("Officer is not in this force's source location.", "deployment.officer_location");
            if (officer.Presence is not (null or OfficerPresence.Present))
                return CommandResult.Reject("Officer has not appeared, is undiscovered or is dead.", "deployment.ineligible");
            if (officer.Deployed || state.Units.Values.Any(u => u.OfficerId == officer.Id) ||
                state.PreparedDeployments.Values.Any(p => p.OfficerId == officer.Id))
                return CommandResult.Reject("Officer already leads an active or prepared unit.", "deployment.officer_busy");
            if (unit.Troops < 1 || unit.FormationId < 0 || unit.TacticIds is null ||
                unit.TacticIds.Count > 3 || unit.TacticIds.Any(t => t < 0))
                return CommandResult.Reject("Unit troops, formation or tactic selection is invalid.", "deployment.unit_configuration");
            if (unit.Actions is null || unit.Actions.Count == 0 || unit.Actions.Any(action => !DeploymentStateValidation.IsActionTargetValid(state, action)))
                return CommandResult.Reject("A deployment action or target is invalid.", "deployment.target");
            if (unit.Actions.Any(action => action.Target.Kind == DeploymentTargetKind.Structure))
                return CommandResult.RuleUnavailable("rules.deployment_structures", "Structure target state has not been implemented.");
            if (unit.WaypointHexIds is null || unit.WaypointHexIds.Count > 5 ||
                unit.WaypointHexIds.Any(hex => hex < 0 || hex >= checked(state.MapWidth * state.MapHeight)))
                return CommandResult.Reject("Deployment waypoints are invalid.", "deployment.waypoints");
            if (unit.Behavior is null || !Enum.IsDefined(unit.Behavior.AfterAction))
                return CommandResult.Reject("Deployment behavior settings are missing or invalid.", "deployment.behavior");
            requestedTroops += unit.Troops;
            // Keep a snapshot separate from the caller's mutable lists.
            units.Add(unit with { TacticIds = Array.AsReadOnly(unit.TacticIds.ToArray()),
                Actions = Array.AsReadOnly(unit.Actions.ToArray()), WaypointHexIds = Array.AsReadOnly(unit.WaypointHexIds.ToArray()) });
        }
        if (requestedTroops > source!.Troops - DeploymentAvailability.ReservedTroops(state, source.LocationId))
            return CommandResult.Reject("Source lacks uncommitted soldiers.", "deployment.insufficient_troops");
        DistrictState district = state.Districts[source.DistrictId];
        if (district.OrdersRemaining is not int remaining)
            return CommandResult.RuleUnavailable("rules.command_budget", "District command books are unresolved.");
        if (units.Count > remaining - CommandBookAvailability.Reserved(state, district.Id))
            return CommandResult.Reject("District lacks uncommitted command books.", "deployment.insufficient_orders");
        DeploymentEligibility eligibility = _eligibility.Evaluate(state, source, units.AsReadOnly(), context);
        if (eligibility.Status == DeploymentDecisionStatus.Denied)
            return CommandResult.Reject(eligibility.Explanation ?? "Deployment eligibility rejected this batch.", "deployment.ineligible");
        if (eligibility.Status != DeploymentDecisionStatus.Allowed)
            return CommandResult.RuleUnavailable(eligibility.MissingRuleKey ?? "rules.deployment_eligibility",
                eligibility.Explanation ?? "Deployment eligibility has not been verified.");
        DeploymentPreparationResources resourceRules = _resources.Prepare(state, source, units.AsReadOnly(), context);
        if (!KnownTiming(resourceRules.Troops) || !KnownTiming(resourceRules.CommandBooks))
            return CommandResult.RuleUnavailable("rules.deployment_resource_timing", "Preparation resource timing has not been verified.");

        bool debitTroops = resourceRules.Troops == DeploymentDebitTiming.Preparation;
        bool debitBooks = resourceRules.CommandBooks == DeploymentDebitTiming.Preparation;
        var pending = new Dictionary<int, PreparedDeployment>(state.PreparedDeployments);
        int nextId = state.NextDeploymentId;
        foreach (DeploymentUnitRequest unit in units)
        {
            int id = nextId;
            nextId = checked(nextId + 1);
            pending.Add(id, new PreparedDeployment(id, state.NextDeploymentId, force.Id, source.DistrictId, source.LocationId,
                unit.OfficerId, unit.FormationId, unit.Troops, unit.TacticIds.ToArray(), unit.Actions.ToArray(),
                unit.WaypointHexIds.ToArray(), unit.Behavior!, debitTroops, debitBooks));
        }
        GameState next = state with { PreparedDeployments = pending, NextDeploymentId = nextId };
        if (debitTroops) next = WithSourceTroops(next, source.LocationId, checked(source.Troops - (int)requestedTroops));
        if (debitBooks) next = WithOrders(next, district, remaining - units.Count);
        return CommandResult.Accept(next);
    }

    private CommandResult Cancel(GameState state, ForceState force, CancelPreparedDeploymentOrder order, CommandContext context)
    {
        if (!state.PreparedDeployments.TryGetValue(order.DeploymentId, out PreparedDeployment? deployment) || deployment.ForceId != force.Id)
            return CommandResult.Reject("Prepared unit does not belong to this force.", "deployment.plan_missing");
        (DeploymentSource? source, CommandResult? error) = ResolveSource(state, force.Id, deployment.SourceLocationId, context);
        if (error is not null) return error;
        if (source!.DistrictId != deployment.DistrictId)
            return CommandResult.RuleUnavailable("rules.deployment_district_change", "Source district changed after preparation.");
        DeploymentEligibility cancellation = _eligibility.EvaluateCancellation(state, deployment, context);
        if (cancellation.Status == DeploymentDecisionStatus.Denied)
            return CommandResult.Reject(cancellation.Explanation ?? "Cancellation is not permitted.", "deployment.cancellation_ineligible");
        if (cancellation.Status != DeploymentDecisionStatus.Allowed)
            return CommandResult.RuleUnavailable(cancellation.MissingRuleKey ?? "rules.deployment_cancellation_eligibility",
                cancellation.Explanation ?? "Cancellation scope has not been verified.");
        DeploymentCancellationResources refunds = deployment.TroopsDebited || deployment.CommandBookDebited
            ? _resources.Cancel(state, deployment, context) : new(0, 0);
        if (refunds.TroopsReturned is not int returnedTroops || refunds.CommandBooksReturned is not int returnedBooks)
            return CommandResult.RuleUnavailable("rules.deployment_cancellation", "Cancellation accounting has not been verified.");
        if (returnedTroops < 0 || returnedTroops > (deployment.TroopsDebited ? deployment.Troops : 0) ||
            returnedBooks < 0 || returnedBooks > (deployment.CommandBookDebited ? 1 : 0))
            throw new InvalidDataException("Cancellation rules returned more than the recorded preparation debits.");
        DistrictState district = state.Districts[deployment.DistrictId];
        if (returnedBooks > 0 && district.OrdersRemaining is null)
            return CommandResult.RuleUnavailable("rules.command_budget", "District command books are unresolved.");
        var pending = new Dictionary<int, PreparedDeployment>(state.PreparedDeployments);
        pending.Remove(deployment.Id);
        GameState next = state with { PreparedDeployments = pending };
        if (returnedTroops > 0) next = WithSourceTroops(next, source.LocationId, checked(source.Troops + returnedTroops));
        if (returnedBooks > 0) next = WithOrders(next, district, checked(district.OrdersRemaining!.Value + returnedBooks));
        return CommandResult.Accept(next);
    }

    private static bool KnownTiming(DeploymentDebitTiming timing) => timing is
        DeploymentDebitTiming.Preparation or DeploymentDebitTiming.Execution;

    private static GameState WithSourceTroops(GameState state, int location, int troops) =>
        state.Cities.TryGetValue(location, out CityState? city)
            ? state with { Cities = new(state.Cities) { [location] = city with { Troops = troops } } }
            : state with { Gates = new(state.Gates) { [location] = state.Gates[location] with { Troops = troops } } };

    private static GameState WithOrders(GameState state, DistrictState district, int orders) =>
        state with { Districts = new(state.Districts) { [district.Id] = district with { OrdersRemaining = orders } } };
}
