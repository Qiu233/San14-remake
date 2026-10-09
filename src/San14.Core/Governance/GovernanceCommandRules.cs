namespace San14.Core;

/// <summary>Verified original appointment constraints with explicit unresolved eligibility and accounting decisions.</summary>
public sealed class GovernanceCommandRules(IGovernanceCommandRules rules) : IOrderRules
{
    private readonly IGovernanceCommandRules _rules = rules ?? throw new ArgumentNullException(nameof(rules));

    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not SetGovernanceOrder request)
            return CommandResult.Reject("Order type is unsupported.", "command.unsupported");
        if (force.Governance is not { } governance)
            return Missing("rules.governance_state", "Current governance is unresolved.");
        if (!GovernanceValidation.TryCopy(request.Assignments, out GovernanceConfiguration target))
            return CommandResult.Reject("Governance assignments are missing, null or use duplicate positions.", "governance.assignments");
        var slots = governance.OpenSlots.ToDictionary(slot => slot.Id);
        foreach (GovernanceAssignment row in target.Assignments)
        {
            if (!slots.ContainsKey(row.SlotId))
                return CommandResult.Reject("Governance position is not open.", "governance.slot");
            if (!state.Officers.TryGetValue(row.OfficerId, out OfficerState? officer) || officer.ForceId != force.Id)
                return CommandResult.Reject("Officer does not belong to this force.", "governance.officer_force");
            if (officer.Presence is not (null or OfficerPresence.Present))
                return CommandResult.Reject("Officer is not currently serving.", "governance.ineligible");
            if (slots[row.SlotId].Kind == GovernanceSlotKind.Advisor)
            {
                if (force.RulerId is not int rulerId)
                    return Missing("rules.governance_ruler", "The current ruler is unresolved for military adviser eligibility.");
                if (row.OfficerId == rulerId)
                    return CommandResult.Reject("The ruler cannot be appointed military adviser.", "governance.advisor_ruler");
            }
        }
        GovernanceCommandEvaluation evaluation = _rules.Evaluate(state, force,
            new GovernanceConfiguration(target.Assignments.ToArray()), context);
        if (evaluation.Status == GovernanceDecisionStatus.Denied)
            return CommandResult.Reject(evaluation.Explanation ?? "Governance changes are unavailable.", "governance.ineligible");
        if (evaluation.Status != GovernanceDecisionStatus.Allowed)
            return Missing(evaluation.MissingRuleKey ?? "rules.governance_eligibility", evaluation.Explanation ?? "Governance eligibility is unresolved.");
        // Scope/eligibility must also authorize no-ops. Engine idempotence is not a claim
        // about the original UI's fee for pressing Confirm twice.
        if (GovernanceValidation.Equal(target, governance.Configured) &&
            (governance.Pending is not null || GovernanceValidation.Equal(target, governance.Active)))
            return CommandResult.Accept(state);
        if (evaluation.OfficerPolicies is not { } policies)
            return Missing("rules.governance_policies", "Applicable officer policies are unresolved.");
        long cost = 0;
        foreach (GovernanceAssignment row in target.Assignments)
        {
            if (!policies.TryGetValue(row.OfficerId, out GovernanceOfficerPolicy? policy) || policy is null ||
                policy.Cost < 0 || string.IsNullOrWhiteSpace(policy.DepartmentKey))
                return Missing("rules.governance_policies", "An officer's applicable policy category or cost is unresolved.");
            if (slots[row.SlotId].Kind == GovernanceSlotKind.Advisor)
            {
                if (policy.AdvisorIntelligence is not int intelligence)
                    return Missing("rules.governance_advisor_intelligence", "Applicable military adviser intelligence is unresolved.");
                if (intelligence < 70)
                    return CommandResult.Reject("Military adviser intelligence is below 70.", "governance.advisor_intelligence");
                // The adviser has no policy cost and may use a different department's policy.
            }
            else
            {
                if (policy.DepartmentKey != slots[row.SlotId].DepartmentKey)
                    return CommandResult.Reject("Policy belongs to another department.", "governance.department");
                cost += policy.Cost;
            }
        }
        if (cost > governance.PolicyCostLimit)
            return CommandResult.Reject("Policy cost exceeds the current limit.", "governance.policy_cost");
        if (evaluation.ExceedsLevelLimit is not bool exceeds)
            return Missing("rules.governance_levels", "Effective policy levels are unresolved.");
        if (exceeds && !request.AcceptLevelOverflow)
            return CommandResult.Reject("Effective policy levels exceed 10; confirmation is required.", "governance.level_confirmation");
        if (evaluation.ActiveAfterSubmission is not { } active ||
            !GovernanceValidation.TryCopy(active.Assignments, out GovernanceConfiguration activeCopy) ||
            !GovernanceValidation.IsSubset(activeCopy, governance.Active))
            return Missing("rules.governance_departure", "Departure effects are unresolved or would activate new policies immediately.");
        if (evaluation.Bill is not { } bill || bill.CommandBooks < 0 ||
            bill.Timing is not (GovernanceDebitTiming.Submission or GovernanceDebitTiming.Activation) ||
            !state.Districts.TryGetValue(bill.DistrictId, out DistrictState? chargedDistrict) || chargedDistrict.ForceId != force.Id)
            return Missing("rules.governance_resources", "Command-book fee, timing or district is unresolved.");
        if (evaluation.PreviousCommandBooksRefunded is not int refund || refund < 0)
            return Missing("rules.governance_refund", "The replacement's refund decision is unresolved.");
        GovernanceBill? previous = governance.Pending?.Bill;
        int refundable = previous?.Timing == GovernanceDebitTiming.Submission ? previous.CommandBooks : 0;
        if (refund > refundable)
            return Missing("rules.governance_refund", "Refund exceeds the previous change's actual debit.");
        if (state.PeriodNumber == int.MaxValue)
            return Missing("rules.governance_period", "The next governance activation period is outside the state range.");
        var adjustments = new Dictionary<int, long> { [bill.DistrictId] = bill.Timing == GovernanceDebitTiming.Submission ? -(long)bill.CommandBooks : 0 };
        if (previous is not null)
            adjustments[previous.DistrictId] = adjustments.GetValueOrDefault(previous.DistrictId) + refund;
        var districts = new Dictionary<int, DistrictState>(state.Districts);
        foreach ((int id, long adjustment) in adjustments)
        {
            DistrictState district = districts[id];
            if (district.OrdersRemaining is not int remaining)
                return Missing("rules.command_budget", "Command budget is unresolved.");
            long next = remaining + adjustment;
            long reserved = CommandBookAvailability.Reserved(state, id, force.Id) +
                (bill.DistrictId == id && bill.Timing == GovernanceDebitTiming.Activation ? bill.CommandBooks : 0L);
            if (next < reserved)
                return CommandResult.Reject("Not enough unreserved command books.", "governance.insufficient_orders");
            if (next > int.MaxValue || (district.OrderLimit is int limit && next > limit))
                return Missing("rules.governance_resources", "Accounting result exceeds the known budget range.");
            districts[id] = district with { OrdersRemaining = (int)next };
        }
        var pending = new PendingGovernanceChange(state.PeriodNumber, state.PeriodNumber + 1, bill);
        var forces = new Dictionary<int, ForceState>(state.Forces)
        {
            [force.Id] = force with { Governance = governance with { Configured = target, Active = activeCopy, Pending = pending } }
        };
        return CommandResult.Accept(state with { Forces = forces, Districts = districts });
    }

    private static CommandResult Missing(string key, string explanation) => CommandResult.RuleUnavailable(key, explanation);
}
