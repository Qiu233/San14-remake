namespace San14.Core;

/// <summary>One actual automatically prepared Army make packet and its home Base.</summary>
public sealed record CampaignArmyDeployment(int SourceLocationId, DeploymentUnitRequest Unit);

/// <summary>Launches one expedition from the selected district's ordered Base packets.</summary>
public sealed record LaunchCampaignOrder(int ForceId, int ProvinceId, int DistrictId,
    IReadOnlyList<CampaignArmyDeployment> Armies) : GameOrder(ForceId);

public sealed record RetreatCampaignsOrder(int ForceId, IReadOnlyList<int> CampaignIds) : GameOrder(ForceId);

public interface ICampaignExpeditionExecution
{
    CommandResult Launch(GameState state, ForceState force, LaunchCampaignOrder order, CommandContext context);
    CommandResult Retreat(GameState state, ForceState force, RetreatCampaignsOrder order, CommandContext context);
}

public sealed class CampaignExpeditionOrders(IOrderRules next, ICampaignExpeditionExecution? execution) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is LaunchCampaignOrder launch)
        {
            if (launch.ProvinceId is < 1 or > 22 || launch.DistrictId is < 1 or > 51 ||
                launch.Armies is not { Count: > 0 and <= 500 } ||
                launch.Armies.Any(row => row is null || row.Unit is null || row.SourceLocationId is < 1 or > 61) ||
                launch.Armies.Select(row => row.Unit.OfficerId).Distinct().Count() != launch.Armies.Count)
                return CommandResult.Reject("Invalid expedition deployment packets.", "campaign.expedition.input");
            if (!state.Districts.TryGetValue(launch.DistrictId, out var district) || district.ForceId != force.Id ||
                context.DelegatedDistrictId is int delegated && delegated != district.Id)
                return CommandResult.Reject("The expedition district is outside this command's authority.", "campaign.expedition.scope");
            foreach (var row in launch.Armies)
            {
                var unit = row.Unit;
                if (unit.OfficerId is < 1 or > 6000 || unit.FormationId < 0 || unit.Troops <= 0 ||
                    unit.TacticIds is null || unit.TacticIds.Count > 3 ||
                    unit.TacticIds.Any(id => id < 0) || unit.TacticIds.Where(id => id != 0).Distinct().Count() != unit.TacticIds.Count(id => id != 0) ||
                    unit.Actions is not { Count: 1 } ||
                    unit.WaypointHexIds is null || unit.WaypointHexIds.Count > 5 ||
                    unit.WaypointHexIds.Any(id => id is < 0 or >= 48400) || unit.NavalFormationId < 0)
                    return CommandResult.Reject("An expedition Army make packet is malformed.", "campaign.expedition.packet_format");
                var action = unit.Actions[0];
                if (action is null || action.Target is not { } target || string.IsNullOrWhiteSpace(action.ActionKey) ||
                    !Enum.IsDefined(target.Kind) || target.Id < 0)
                    return CommandResult.Reject("An expedition Army action is malformed.", "campaign.expedition.packet_format");
                if (unit.Behavior is null || unit.NavalFormationId is null)
                    return CommandResult.RuleUnavailable("campaign.expedition.packet_defaults", "The prepared Army packet is missing its actual behavior or naval formation.");
                if (!Enum.IsDefined(unit.Behavior.AfterAction))
                    return CommandResult.Reject("An expedition behavior is invalid.", "campaign.expedition.packet_format");
                int? sourceForce = row.SourceLocationId <= 51
                    ? state.Cities.GetValueOrDefault(row.SourceLocationId)?.ForceId
                    : state.Gates.GetValueOrDefault(row.SourceLocationId)?.ForceId;
                int? sourceDistrict = row.SourceLocationId <= 51
                    ? state.Cities.GetValueOrDefault(row.SourceLocationId)?.DistrictId
                    : state.Gates.GetValueOrDefault(row.SourceLocationId)?.DistrictId;
                if (sourceForce != force.Id || sourceDistrict != district.Id ||
                    !state.Officers.TryGetValue(row.Unit.OfficerId, out var person) || person.ForceId != force.Id)
                    return CommandResult.Reject("An expedition packet no longer belongs to the selected district.", "campaign.expedition.packet_control");
            }
            return execution?.Launch(state, force, launch, context) ??
                CommandResult.RuleUnavailable("campaign.expedition.execution", "Expedition execution is unavailable.");
        }
        if (order is RetreatCampaignsOrder retreat)
        {
            if (retreat.CampaignIds is not { Count: > 0 and <= 50 } ||
                retreat.CampaignIds.Any(id => id is < 1 or > 50) ||
                retreat.CampaignIds.Distinct().Count() != retreat.CampaignIds.Count)
                return CommandResult.Reject("Invalid expedition withdrawal selection.", "campaign.expedition.selection");
            return execution?.Retreat(state, force, retreat, context) ??
                CommandResult.RuleUnavailable("campaign.expedition.execution", "Expedition execution is unavailable.");
        }
        return next.Apply(state, force, order, context);
    }
}
