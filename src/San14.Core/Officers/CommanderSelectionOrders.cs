namespace San14.Core;

/// <summary>The ordered selection returned by the commander selector. Vacant
/// entries are implicit; the current capacity belongs to the world.</summary>
public sealed record SetCommanderSelectionOrder(int ForceId,
    IReadOnlyList<int> OfficerIds) : GameOrder(ForceId);

public interface ICommanderSelectionCommandRules
{
    CommandResult ApplyCommanderSelection(GameState state, ForceState force,
        IReadOnlyList<int> officerIds, CommandContext context);
}

internal static class CommanderSelectionOrders
{
    internal static CommandResult Apply(GameState state, ForceState force,
        SetCommanderSelectionOrder order, CommandContext context, IPersonnelCommandRules personnel)
    {
        if (context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Commander selection requires direct force control.", "commander-selection.scope");
        if (order.OfficerIds is null || order.OfficerIds.Count > 5 ||
            order.OfficerIds.Any(id => id is < 1 or > 6000) ||
            order.OfficerIds.Distinct().Count() != order.OfficerIds.Count)
            return CommandResult.Reject("Commander selection contains invalid or duplicate identities.", "commander-selection.identities");
        return personnel is ICommanderSelectionCommandRules rules
            ? rules.ApplyCommanderSelection(state, force, order.OfficerIds, context)
            : CommandResult.RuleUnavailable("commander-selection.rules", "Current commander selection rules are unavailable.");
    }
}
