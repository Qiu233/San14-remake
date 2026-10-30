namespace San14.Core;

/// <summary>Execute the ordered offered-template hires at the selected base.
/// Empty and repeated templates retain their actual batch effects. Base identity
/// is the shared City/Gate resource key, not a map-object receiver.</summary>
public sealed record ExecutePersonnelHireOrder(int ForceId, int DistrictId, int DestinationBaseId,
    IReadOnlyList<int> TemplateIds) : GameOrder(ForceId);

/// <summary>Transient batch completion. An incomplete accepted batch retains
/// its already-created prefix; only a complete batch reaches final accounting.</summary>
public sealed record PersonnelHireExecution(bool Completed);

public interface IPersonnelHireExecutionRules
{
    CommandResult ExecutePersonnelHire(GameState state, ForceState force,
        ExecutePersonnelHireOrder order, CommandContext context);
}

public sealed class PersonnelHireExecutionOrders(IOrderRules next, IPersonnelHireExecutionRules? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not ExecutePersonnelHireOrder hire) return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || !force.IsActive || hire.ForceId != force.Id)
            return CommandResult.Reject("Personnel hire execution requires active strategy control.", "personnel.hire_execution.scope");
        if (!state.Districts.TryGetValue(hire.DistrictId, out var district) || district.ForceId != force.Id ||
            context.DelegatedDistrictId is int delegated && delegated != district.Id)
            return CommandResult.Reject("Personnel hire execution is outside the authorized district.", "personnel.hire_execution.district_scope");
        if (hire.DestinationBaseId is < 0 or > 61 || hire.TemplateIds is null ||
            hire.TemplateIds.Any(id => id is < 1 or > 520))
            return CommandResult.Reject("Personnel hire execution selection is invalid.", "personnel.hire_execution.selection");
        // Current offers, base binding, creation, partial prefixes and final
        // accounting are one provider operation. Acceptance and completion are
        // distinct so an incomplete batch never discards its real mutations.
        return rules?.ExecutePersonnelHire(state, force, hire, context) ??
            CommandResult.RuleUnavailable("personnel.hire_execution.rules", "Personnel hire execution rules are unavailable.");
    }
}
