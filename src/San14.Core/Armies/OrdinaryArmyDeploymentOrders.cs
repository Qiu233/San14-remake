namespace San14.Core;

public sealed record OrdinaryArmyDeployment(int SourceLocationId, DeploymentUnitRequest Unit);
public sealed record DeployArmiesOrder(int ForceId, int DistrictId,
    IReadOnlyList<OrdinaryArmyDeployment> Armies) : GameOrder(ForceId);

/// <summary>Executes ordinary source packets in their selected order on the
/// one owned command candidate. Only complete success returns a published
/// candidate; each packet retains its normal command-book debit.</summary>
public interface IGeneralUnitTargetExecution
{
    CommandResult Deploy(GameState state, ForceState force, DeployArmiesOrder order, CommandContext context);
}

public sealed class OrdinaryArmyDeploymentOrders(IOrderRules next, IGeneralUnitTargetExecution? execution) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not DeployArmiesOrder deployment) return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || !force.IsActive)
            return CommandResult.Reject("Armies can only deploy during strategy control.", "deployment.phase");
        if (!state.Districts.TryGetValue(deployment.DistrictId, out var district) || district.ForceId != force.Id ||
            context.DelegatedDistrictId is int delegated && district.Id != delegated)
            return CommandResult.Reject("The deployment district is outside this command's authority.", "deployment.district_scope");
        if (deployment.Armies is not { Count: > 0 and <= 500 })
            return CommandResult.Reject("The ordinary deployment selection is empty or too large.", "deployment.batch_size");

        var officers = new HashSet<int>();
        var packets = new List<OrdinaryArmyDeployment>(deployment.Armies.Count);
        foreach (var row in deployment.Armies)
        {
            if (row is null || row.Unit is not { } unit || !officers.Add(unit.OfficerId))
                return CommandResult.Reject("Each selected officer may lead only one Army.", "deployment.duplicate_officer");
            int? sourceForce, sourceDistrict;
            if (state.Cities.TryGetValue(row.SourceLocationId, out var city))
                (sourceForce, sourceDistrict) = (city.ForceId, city.DistrictId);
            else if (state.Gates.TryGetValue(row.SourceLocationId, out var gate))
                (sourceForce, sourceDistrict) = (gate.ForceId, gate.DistrictId);
            else return CommandResult.Reject("A selected deployment source no longer exists.", "deployment.source_missing");
            if (sourceForce != force.Id || sourceDistrict != district.Id ||
                !state.Officers.TryGetValue(unit.OfficerId, out var officer) || officer.ForceId != force.Id ||
                officer.LocationId != row.SourceLocationId)
                return CommandResult.Reject("A selected Army no longer belongs to its source district.", "deployment.source_control");
            if (unit.OfficerId is < 1 or > 6000 || unit.FormationId < 0 || unit.Troops <= 0 ||
                unit.TacticIds is null || unit.TacticIds.Count > 3 || unit.TacticIds.Any(id => id < 0) ||
                unit.Actions is not { Count: > 0 } || unit.Actions.Any(action => !DeploymentStateValidation.IsActionTargetValid(state, action)) ||
                unit.WaypointHexIds is null || unit.WaypointHexIds.Count > 5 ||
                unit.WaypointHexIds.Any(hex => hex < 0 || hex >= checked(state.MapWidth * state.MapHeight)) ||
                unit.NavalFormationId < 0 || unit.Behavior is { } behavior && !Enum.IsDefined(behavior.AfterAction))
                return CommandResult.Reject("An ordinary Army deployment packet is invalid.", "deployment.unit_configuration");
            if (unit.NavalFormationId is null || unit.Behavior is null)
                return CommandResult.RuleUnavailable("deployment.packet_defaults", "The selected Army's actual naval formation or behavior is unavailable.");

            packets.Add(row with { Unit = unit with
            {
                TacticIds = Array.AsReadOnly(unit.TacticIds.ToArray()),
                Actions = Array.AsReadOnly(unit.Actions.ToArray()),
                WaypointHexIds = Array.AsReadOnly(unit.WaypointHexIds.ToArray())
            } });
        }
        //Current officer eligibility, capacities, all action rules and per-row
        //debits belong to the ordinary deployment provider. It never issues
        //nested orders or publishes an intermediate source group.
        return execution?.Deploy(state, force, deployment with { Armies = packets.AsReadOnly() }, context) ??
            CommandResult.RuleUnavailable("deployment.general_execution", "Ordinary multi-source deployment rules are unavailable.");
    }
}
