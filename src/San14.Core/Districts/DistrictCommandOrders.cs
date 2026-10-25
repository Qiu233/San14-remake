namespace San14.Core;

public sealed record CreateDistrictOrder(int ForceId, IReadOnlyList<int> CityIds,
    int LeaderId, DistrictSettings Settings) : GameOrder(ForceId)
{
    public int MilitaryIntelligenceTransfer { get; init; }
}

public sealed record ReorganizeDistrictOrder(int ForceId, int DistrictId, IReadOnlyList<int> CityIds,
    int LeaderId, DistrictSettings Settings) : GameOrder(ForceId)
{
    public int MilitaryIntelligenceTransfer { get; init; }
}

public sealed record DissolveDistrictOrder(int ForceId, int DistrictId) : GameOrder(ForceId);

public interface IDistrictCommandRules
{
    CommandResult ApplyCreate(GameState state, ForceState force, CreateDistrictOrder order, CommandContext context);
    CommandResult ApplyReorganize(GameState state, ForceState force, ReorganizeDistrictOrder order, CommandContext context);
    CommandResult ApplyDissolve(GameState state, ForceState force, DissolveDistrictOrder order, CommandContext context);
}

/// <summary>Shared command authority and input shape. The source provider owns
/// allocation, reassignment, budgets, settings publication and personnel tails.</summary>
public sealed class DistrictCommandOrders(IOrderRules next, IDistrictCommandRules? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not (CreateDistrictOrder or ReorganizeDistrictOrder or DissolveDistrictOrder))
            return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || !force.IsActive || context.DelegatedDistrictId is not null)
            return CommandResult.Reject("District organization requires direct strategy control.", "district.scope");
        if (rules is null)
            return CommandResult.RuleUnavailable("district.command_rules", "District organization rules are unavailable.");

        if (order is DissolveDistrictOrder dissolve)
        {
            if (!state.Districts.TryGetValue(dissolve.DistrictId, out var dissolved) || dissolved.ForceId != force.Id ||
                dissolved.Number == 1)
                return CommandResult.Reject("Only a controlled secondary district can be dissolved.", "district.dissolve.selection");
            return rules.ApplyDissolve(state, force, dissolve, context);
        }

        var cities = order is CreateDistrictOrder create ? create.CityIds : ((ReorganizeDistrictOrder)order).CityIds;
        int leaderId = order is CreateDistrictOrder c ? c.LeaderId : ((ReorganizeDistrictOrder)order).LeaderId;
        var settings = order is CreateDistrictOrder s ? s.Settings : ((ReorganizeDistrictOrder)order).Settings;
        int transfer = order is CreateDistrictOrder t ? t.MilitaryIntelligenceTransfer : ((ReorganizeDistrictOrder)order).MilitaryIntelligenceTransfer;
        if (cities is not { Count: > 0 and <= 51 } || cities.Distinct().Count() != cities.Count ||
            cities.Any(id => !state.Cities.TryGetValue(id, out var city) || city.ForceId != force.Id) ||
            !state.Officers.TryGetValue(leaderId, out var leader) || leader.ForceId != force.Id ||
            transfer < 0 || !ValidSettings(settings))
            return CommandResult.Reject("District cities, leader or settings are invalid.", "district.input");
        if (order is CreateDistrictOrder newDistrict)
            return rules.ApplyCreate(state, force, newDistrict, context);
        var reorganize = (ReorganizeDistrictOrder)order;
        if (!state.Districts.TryGetValue(reorganize.DistrictId, out var selected) || selected.ForceId != force.Id)
            return CommandResult.Reject("The district is outside this force's control.", "district.control");
        return rules.ApplyReorganize(state, force, reorganize, context);
    }

    private static bool ValidSettings(DistrictSettings? settings)
    {
        if (settings is null || settings.PolicyId is < 0 or > 5 || settings.PriorityId is < 0 or > 4 ||
            settings.AllowedFacilityIds is null || settings.AllowedFacilityIds.Any(id => id is < 0 or > 30) ||
            settings.Resources is not { GoldUsageId: >= 0 and <= 2, FoodUsageId: >= 0 and <= 2, TroopsUsageId: >= 0 and <= 2 } ||
            settings.Military?.AllowedMissionIds is not { } missions || missions.Any(id => id is not (0 or 1 or >= 3 and <= 7))) return false;
        return settings.PolicyId switch
        {
            2 => settings.Target is { Kind: DistrictTargetKind.Force, Id: >= 1 and <= 51 },
            3 => settings.Target is { Kind: DistrictTargetKind.Foothold, Id: >= 1 and <= 61 },
            5 => settings.Target is { Kind: DistrictTargetKind.District, Id: >= 1 and <= 51 },
            _ => settings.Target is null
        };
    }
}
