namespace San14.Core;

public enum GovernanceSlotKind { Department, Advisor }
/// <summary>Only currently open positions. IDs and department keys belong to the ruleset, not to source array offsets.</summary>
public sealed record GovernanceSlot(int Id, GovernanceSlotKind Kind, string? DepartmentKey);
public sealed record GovernanceAssignment(int SlotId, int OfficerId);
public sealed record GovernanceConfiguration(GovernanceAssignment[] Assignments);
public enum GovernanceDebitTiming { Unavailable, Submission, Activation }
public sealed record GovernanceBill(int DistrictId, int CommandBooks, GovernanceDebitTiming Timing);
public sealed record PendingGovernanceChange(int SubmittedPeriod, int ActivationPeriod, GovernanceBill Bill);

/// <summary>
/// Null ForceState.Governance is unknown. A known empty configuration has an empty array.
/// Configured is the submitted target; Active changes only by explicit rule decisions.
/// Open positions do not imply that all positions permitted by a doctrine have been expanded.
/// </summary>
public sealed record GovernanceState(GovernanceSlot[] OpenSlots, int PolicyCostLimit,
    GovernanceConfiguration Configured, GovernanceConfiguration Active, PendingGovernanceChange? Pending);
public sealed record SetGovernanceOrder(int ForceId, IReadOnlyList<GovernanceAssignment> Assignments,
    bool AcceptLevelOverflow = false) : GameOrder(ForceId);

public enum GovernanceDecisionStatus { Unavailable, Allowed, Denied }
public sealed record GovernanceOfficerPolicy(string DepartmentKey, int Cost, int? AdvisorIntelligence = null);

/// <summary>
/// One pure evaluation of the complete target. Every required value must be resolved;
/// zero fees/refunds and an empty active set must be explicit. No defaults infer original rules.
/// AdvisorIntelligence is the verified applicable value, not necessarily a base ability.
/// </summary>
public sealed record GovernanceCommandEvaluation(GovernanceDecisionStatus Status)
{
    public string? Explanation { get; init; }
    public string? MissingRuleKey { get; init; }
    public IReadOnlyDictionary<int, GovernanceOfficerPolicy>? OfficerPolicies { get; init; }
    public bool? ExceedsLevelLimit { get; init; }
    public GovernanceBill? Bill { get; init; }
    public int? PreviousCommandBooksRefunded { get; init; }
    public GovernanceConfiguration? ActiveAfterSubmission { get; init; }
}
public interface IGovernanceCommandRules
{
    // Includes force-wide/delegated scope, tasks/health, duplicate officer appointments,
    // policy costs/categories, effect-level aggregation, charge population and departure timing.
    // Must not mutate state or the supplied target. The caller never supplies these decisions.
    GovernanceCommandEvaluation Evaluate(GameState state, ForceState force,
        GovernanceConfiguration target, CommandContext context);
}

public sealed record GovernanceActivationEvaluation(GovernanceDecisionStatus Status)
{
    public string? Explanation { get; init; }
    public string? MissingRuleKey { get; init; }
    // May remove now-ineligible assignments from the submitted target, but cannot appoint new people.
    public GovernanceConfiguration? Configuration { get; init; }
    public int? DeferredCommandBooksCharged { get; init; }
    public int? PaidCommandBooksRefunded { get; init; }
}
public interface IGovernanceActivationRules
{
    // Explicitly rechecks departures/deaths and charge/refund outcomes at the chosen turn-start hook.
    // Previously agreed fees are in Pending.Bill; do not reprice them using a later policy table.
    GovernanceActivationEvaluation Evaluate(GameState state, ForceState force);
}

/// <summary>Shared availability for all commands competing for the same district's books.</summary>
public static class CommandBookAvailability
{
    public static long Reserved(GameState state, int districtId, int? excludingGovernanceForceId = null) =>
        DeploymentAvailability.ReservedCommandBooks(state, districtId) + state.Forces.Values
            .Where(f => f.Id != excludingGovernanceForceId &&
                f.Governance?.Pending?.Bill is { Timing: GovernanceDebitTiming.Activation } bill && bill.DistrictId == districtId)
            .Sum(f => (long)f.Governance!.Pending!.Bill.CommandBooks);
}

internal static class GovernanceValidation
{
    internal static bool TryCopy(IReadOnlyList<GovernanceAssignment>? assignments, out GovernanceConfiguration copy)
    {
        copy = new GovernanceConfiguration([]);
        if (assignments is null) return false;
        var seen = new HashSet<int>();
        var rows = new List<GovernanceAssignment>();
        foreach (GovernanceAssignment row in assignments)
        {
            if (row is null || row.SlotId < 0 || row.OfficerId < 0 || !seen.Add(row.SlotId)) return false;
            rows.Add(row);
        }
        copy = new GovernanceConfiguration(rows.OrderBy(row => row.SlotId).ToArray());
        return true;
    }

    internal static bool Equal(GovernanceConfiguration first, GovernanceConfiguration second) =>
        first.Assignments.Length == second.Assignments.Length &&
        first.Assignments.OrderBy(a => a.SlotId).SequenceEqual(second.Assignments.OrderBy(a => a.SlotId));

    internal static bool IsSubset(GovernanceConfiguration part, GovernanceConfiguration whole) =>
        part.Assignments.All(row => whole.Assignments.Contains(row));

    internal static void Validate(GameState state)
    {
        foreach (ForceState force in state.Forces.Values)
        {
            if (force.Governance is not { } governance) continue;
            if (governance.OpenSlots is null || governance.PolicyCostLimit < 0)
                throw new InvalidDataException($"Force {force.Id} governance positions or cost limit are invalid.");
            var ids = new HashSet<int>();
            foreach (GovernanceSlot slot in governance.OpenSlots)
                if (slot is null || slot.Id < 0 || !ids.Add(slot.Id) || !Enum.IsDefined(slot.Kind) ||
                    (slot.Kind == GovernanceSlotKind.Department ? string.IsNullOrWhiteSpace(slot.DepartmentKey) : slot.DepartmentKey is not null))
                    throw new InvalidDataException($"Force {force.Id} has invalid governance positions.");
            CheckConfiguration(governance.Configured);
            CheckConfiguration(governance.Active);
            if (governance.Pending is { } pending)
            {
                if (pending.SubmittedPeriod < 0 || pending.SubmittedPeriod > state.PeriodNumber ||
                    (long)pending.ActivationPeriod != (long)pending.SubmittedPeriod + 1 ||
                    pending.ActivationPeriod < state.PeriodNumber ||
                    (state.Phase == TurnPhase.Strategy && pending.ActivationPeriod == state.PeriodNumber) ||
                    pending.Bill is not { } bill || bill.CommandBooks < 0 ||
                    bill.Timing is not (GovernanceDebitTiming.Submission or GovernanceDebitTiming.Activation) ||
                    !state.Districts.TryGetValue(bill.DistrictId, out DistrictState? district) || district.ForceId != force.Id)
                    throw new InvalidDataException($"Force {force.Id} has invalid pending governance.");
            }
            void CheckConfiguration(GovernanceConfiguration? configuration)
            {
                if (configuration is null || !TryCopy(configuration.Assignments, out GovernanceConfiguration copy) ||
                    copy.Assignments.Any(row => !ids.Contains(row.SlotId) || !state.Officers.ContainsKey(row.OfficerId)))
                    throw new InvalidDataException($"Force {force.Id} has invalid governance assignments.");
                // Current affiliation/health and derived costs are rule decisions; a departure during
                // an action phase must be resolvable without making the stored state unreadable.
            }
        }
    }
}
