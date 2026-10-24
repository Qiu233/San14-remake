namespace San14.Core;

/// <summary>Hires from the force's actual offered templates into the selected
/// destination city. This is distinct from recruiting an existing person.</summary>
public sealed record HireOfficersOrder(int ForceId, int DistrictId, int DestinationCityId,
    IReadOnlyList<int> TemplateOfficerIds) : GameOrder(ForceId);
/// <summary>Consumes a force-owned tactic book. Replacing a tactic is an
/// explicit player choice, never an automatic truncation of the learned set.</summary>
public sealed record LearnOfficerTacticOrder(int ForceId, int OfficerId, int GemId,
    int? ReplacedTacticId = null) : GameOrder(ForceId);
/// <summary>The actual ordered two-person marriage or two/three-person sworn
/// relationship proposal. The native gender rules determine the relationship.</summary>
public sealed record MediationOrder(int ForceId, int DistrictId,
    IReadOnlyList<int> OfficerIds) : GameOrder(ForceId);

public interface IPersonnelDevelopmentCommandRules
{
    CommandResult ApplyHire(GameState state, ForceState force, int districtId, int destinationCityId,
        IReadOnlyList<int> templateOfficerIds, CommandContext context);
    CommandResult ApplyLearn(GameState state, ForceState force, int officerId, int gemId,
        int? replacedTacticId, CommandContext context);
    CommandResult ApplyMediation(GameState state, ForceState force, int districtId,
        IReadOnlyList<int> officerIds, CommandContext context);
}

internal static class PersonnelDevelopmentOrders
{
    internal static CommandResult Apply(GameState state, ForceState force, GameOrder order,
        CommandContext context, IPersonnelCommandRules provider)
    {
        if (state.Phase != TurnPhase.Strategy || !force.IsActive || context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Personnel development requires direct strategy control.", "personnel.scope");
        if (provider is not IPersonnelDevelopmentCommandRules rules)
            return CommandResult.RuleUnavailable("personnel.development_rules", "Native personnel development is unavailable.");
        switch (order)
        {
            case HireOfficersOrder hire:
                if (!state.Districts.TryGetValue(hire.DistrictId, out var hireDistrict) || hireDistrict.ForceId != force.Id ||
                    !state.Cities.TryGetValue(hire.DestinationCityId, out var city) || city.ForceId != force.Id)
                    return CommandResult.Reject("The destination city and district must be controlled by this force.", "personnel.hire.control");
                var templates = hire.TemplateOfficerIds;
                if (templates is null || templates.Count is < 1 or > 200 || templates.Any(id => id is < 1 or > 6000) ||
                    templates.Distinct().Count() != templates.Count)
                    return CommandResult.Reject("Select distinct offered hire templates.", "personnel.hire.selection");
                return rules.ApplyHire(state, force, hire.DistrictId, hire.DestinationCityId, templates, context);
            case LearnOfficerTacticOrder learn:
                if (learn.OfficerId is < 1 or > 6000 || learn.GemId is < 1 or > 30 || learn.ReplacedTacticId < 1)
                    return CommandResult.Reject("The selected officer or tactic book is invalid.", "personnel.learn.selection");
                return rules.ApplyLearn(state, force, learn.OfficerId, learn.GemId, learn.ReplacedTacticId, context);
            case MediationOrder mediation:
                if (!state.Districts.TryGetValue(mediation.DistrictId, out var district) || district.ForceId != force.Id)
                    return CommandResult.Reject("The district is not controlled by this force.", "personnel.mediation.district");
                var ids = mediation.OfficerIds;
                if (ids is null || ids.Count is < 2 or > 3 || ids.Any(id => id is < 1 or > 6000) ||
                    ids.Distinct().Count() != ids.Count)
                    return CommandResult.Reject("Select two or three distinct officers.", "personnel.mediation.selection");
                return rules.ApplyMediation(state, force, mediation.DistrictId, ids, context);
            default: throw new ArgumentException("Not a personnel development order.", nameof(order));
        }
    }
}
