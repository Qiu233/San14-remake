namespace San14.Core;

public sealed record RewardOfficersOrder(int ForceId, int DistrictId,
    IReadOnlyList<int> OfficerIds) : GameOrder(ForceId);
public sealed record GrantOfficerMeritOrder(int ForceId, int DistrictId,
    IReadOnlyList<int> OfficerIds) : GameOrder(ForceId);
public sealed record OfficerCreditChoice(int OfficerId, byte ChoiceCode);
public sealed record PromoteOfficerCreditOrder(int ForceId, int DistrictId,
    IReadOnlyList<OfficerCreditChoice> Choices) : GameOrder(ForceId);
public sealed record BanishOfficersOrder(int ForceId,
    IReadOnlyList<int> OfficerIds) : GameOrder(ForceId);

/// <summary>Native personnel administration. Candidate and accounting rules belong
/// to the same provider used by the command page, rather than the legacy item policy.</summary>
public interface IPersonnelAdministrationCommandRules
{
    CommandResult ApplyReward(GameState state, ForceState force, int districtId,
        IReadOnlyList<int> officerIds, CommandContext context);
    CommandResult ApplyCreditGive(GameState state, ForceState force, int districtId,
        IReadOnlyList<int> officerIds, CommandContext context);
    CommandResult ApplyCreditRankup(GameState state, ForceState force, int districtId,
        IReadOnlyList<OfficerCreditChoice> choices, CommandContext context);
    CommandResult ApplyBanish(GameState state, ForceState force,
        IReadOnlyList<int> officerIds, CommandContext context);
}

internal static class PersonnelAdministrationOrders
{
    internal static CommandResult Apply(GameState state, ForceState force, GameOrder order,
        CommandContext context, IPersonnelCommandRules provider)
    {
        if (state.Phase != TurnPhase.Strategy || !force.IsActive || context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Personnel administration requires direct strategy control.", "personnel.scope");
        IReadOnlyList<int>? ids = order switch
        {
            RewardOfficersOrder row => row.OfficerIds,
            GrantOfficerMeritOrder row => row.OfficerIds,
            PromoteOfficerCreditOrder row => row.Choices?.Select(choice => choice?.OfficerId ?? -1).ToArray(),
            BanishOfficersOrder row => row.OfficerIds,
            _ => throw new ArgumentException("Not a personnel administration order.", nameof(order))
        };
        if (ids is null || ids.Count is < 1 or > 1850 || ids.Any(id => id is < 1 or > 6000) ||
            ids.Distinct().Count() != ids.Count)
            return CommandResult.Reject("Personnel selection contains invalid or duplicate identities.", "personnel.selection");
        int? districtId = order switch
        {
            RewardOfficersOrder row => row.DistrictId,
            GrantOfficerMeritOrder row => row.DistrictId,
            PromoteOfficerCreditOrder row => row.DistrictId,
            _ => null
        };
        if (districtId is int district && (!state.Districts.TryGetValue(district, out var owner) || owner.ForceId != force.Id))
            return CommandResult.Reject("The district is not controlled by this force.", "personnel.district_control");
        if (provider is not IPersonnelAdministrationCommandRules rules)
            return CommandResult.RuleUnavailable("personnel.administration_rules", "Native personnel administration is unavailable.");
        return order switch
        {
            RewardOfficersOrder row => rules.ApplyReward(state, force, row.DistrictId, row.OfficerIds, context),
            GrantOfficerMeritOrder row => rules.ApplyCreditGive(state, force, row.DistrictId, row.OfficerIds, context),
            PromoteOfficerCreditOrder row => rules.ApplyCreditRankup(state, force, row.DistrictId, row.Choices, context),
            BanishOfficersOrder row => rules.ApplyBanish(state, force, row.OfficerIds, context),
            _ => throw new ArgumentException("Not a personnel administration order.", nameof(order))
        };
    }
}
