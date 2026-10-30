namespace San14.Core;

public sealed record IntelligenceAssignment(int TargetForceId, int OfficerId);
public sealed record SetIntelligenceAssignmentsOrder(int ForceId,
    IReadOnlyList<IntelligenceAssignment> Assignments) : GameOrder(ForceId);
public sealed record DispatchIntelligenceOrder(int ForceId, int TargetForceId,
    int OfficerId, int Duration) : GameOrder(ForceId)
{
    /// <summary>Explicit district execution. Null retains the direct primary
    /// command path; an actual district uses its own candidate and payment rules.</summary>
    public int? DistrictId { get; init; }
}
public sealed record InspectIntelligenceOrder(int ForceId, int TargetCityId,
    int OfficerId, int Level) : GameOrder(ForceId)
{
    /// <summary>Explicit district execution, distinct from the direct primary
    /// command path retained by null.</summary>
    public int? DistrictId { get; init; }
}

public interface IIntelligenceCommandRules
{
    CommandResult SetIntelligenceAssignments(GameState state, ForceState force,
        SetIntelligenceAssignmentsOrder order, CommandContext context);
    CommandResult DispatchIntelligence(GameState state, ForceState force,
        DispatchIntelligenceOrder order, CommandContext context);
    CommandResult InspectIntelligence(GameState state, ForceState force,
        InspectIntelligenceOrder order, CommandContext context);
}

/// <summary>Home assignments, foreign dispatch and inspection retain their
/// distinct normal source operations and share ordinary command authority.</summary>
public sealed class IntelligenceCommandOrders(IOrderRules next, IIntelligenceCommandRules? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not (SetIntelligenceAssignmentsOrder or DispatchIntelligenceOrder or InspectIntelligenceOrder))
            return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || !force.IsActive)
            return CommandResult.Reject("Intelligence commands require active strategy control.", "intelligence.scope");
        int? districtId = order switch
        {
            DispatchIntelligenceOrder away => away.DistrictId,
            InspectIntelligenceOrder inspect => inspect.DistrictId,
            _ => null
        };
        if (districtId is int selected && (!state.Districts.TryGetValue(selected, out var district) || district.ForceId != force.Id))
            return CommandResult.Reject("The intelligence district is not controlled by this force.", "intelligence.district_control");
        if (context.DelegatedDistrictId is int delegated && delegated != districtId)
            return CommandResult.Reject("Intelligence commands are outside the authorized district.", "intelligence.scope");
        bool valid = order switch
        {
            SetIntelligenceAssignmentsOrder home => home.Assignments is { Count: 3 } &&
                home.Assignments.All(assignment => assignment is not null && assignment.TargetForceId is >= 0 and <= 51 &&
                    assignment.OfficerId is >= 0 and <= 6000),
            DispatchIntelligenceOrder away => away.TargetForceId is >= 0 and <= 51 && away.OfficerId is >= 1 and <= 6000 &&
                away.Duration is 5 or 10 or 15,
            InspectIntelligenceOrder inspect => inspect.TargetCityId is >= 1 and <= 51 && inspect.OfficerId is >= 1 and <= 6000 &&
                inspect.Level is 10 or 20 or 30,
            _ => false
        };
        if (!valid) return CommandResult.Reject("The intelligence selection is invalid.", "intelligence.selection");
        if (rules is null) return CommandResult.RuleUnavailable("intelligence.command_rules", "Intelligence rules are unavailable.");
        return order switch
        {
            SetIntelligenceAssignmentsOrder home => rules.SetIntelligenceAssignments(state, force, home, context),
            DispatchIntelligenceOrder away => rules.DispatchIntelligence(state, force, away, context),
            InspectIntelligenceOrder inspect => rules.InspectIntelligence(state, force, inspect, context),
            _ => throw new InvalidOperationException("The intelligence command changed during dispatch.")
        };
    }
}
