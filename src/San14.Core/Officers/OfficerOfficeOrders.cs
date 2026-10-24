namespace San14.Core;

/// <summary>One office slot in an appointment proposal. A null person leaves that slot vacant.</summary>
public sealed record OfficerOfficeAssignment(int OfficeId, int? OfficerId);

/// <summary>All edited slots are submitted as one command transaction.</summary>
public sealed record SetOfficerOfficesOrder(int ForceId,
    IReadOnlyList<OfficerOfficeAssignment> Assignments) : GameOrder(ForceId);

public interface IOfficerOfficeCommandRules
{
    CommandResult ApplyOffices(GameState state, ForceState force,
        IReadOnlyList<OfficerOfficeAssignment> assignments, CommandContext context);
}

internal static class OfficerOfficeOrders
{
    internal static CommandResult Apply(GameState state, ForceState force, GameOrder order,
        CommandContext context, IPersonnelCommandRules personnel)
    {
        if (context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Office appointments require direct force control.", "personnel.scope");
        IReadOnlyList<OfficerOfficeAssignment>? assignments = order switch
        {
            SetOfficerOfficeOrder single => [new(single.OfficeId, single.OfficerId)],
            SetOfficerOfficesOrder batch => batch.Assignments,
            _ => throw new ArgumentException("Not an office appointment order.", nameof(order))
        };
        if (assignments is null || assignments.Count > 256 ||
            assignments.Any(row => row is null || row.OfficeId is < 0 or > 255 || row.OfficerId < 0) ||
            assignments.Select(row => row.OfficeId).Distinct().Count() != assignments.Count ||
            assignments.Where(row => row.OfficerId is not null).Select(row => row.OfficerId).Distinct().Count() !=
                assignments.Count(row => row.OfficerId is not null))
            return CommandResult.Reject("Office assignments contain invalid or duplicate identities.", "personnel.office_assignments");
        if (personnel is not IOfficerOfficeCommandRules rules)
            return CommandResult.RuleUnavailable("personnel.office_policy", "Native office appointment rules are unavailable.");
        return rules.ApplyOffices(state, force, assignments, context);
    }
}
