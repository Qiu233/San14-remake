namespace San14.Core;

/// <summary>
/// An explicit turn-start hook for reporting rules. AdvancePeriod never invokes it implicitly:
/// the original ordering relative to reports, income and budget recovery still needs calibration.
/// </summary>
public static class GovernanceActivation
{
    public static CommandResult ApplyDueChanges(GameState state, IGovernanceActivationRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (state.Phase != TurnPhase.Reporting)
            return CommandResult.Reject("Governance activation requires the reporting/turn-start boundary.", "governance.activation_phase");
        StateValidator.Validate(state);
        var forces = new Dictionary<int, ForceState>(state.Forces);
        var districts = new Dictionary<int, DistrictState>(state.Districts);
        foreach (ForceState force in state.Forces.Values.OrderBy(f => f.Id))
        {
            if (force.Governance is not { Pending: { } pending } governance || pending.ActivationPeriod != state.PeriodNumber) continue;
            GovernanceActivationEvaluation evaluation = rules.Evaluate(state, force);
            if (evaluation.Status == GovernanceDecisionStatus.Denied)
                return CommandResult.Reject(evaluation.Explanation ?? "Governance activation was denied.", "governance.activation_denied");
            if (evaluation.Status != GovernanceDecisionStatus.Allowed)
                return CommandResult.RuleUnavailable(evaluation.MissingRuleKey ?? "rules.governance_activation",
                    evaluation.Explanation ?? "Governance activation is unresolved.");
            if (evaluation.Configuration is not { } configuration ||
                !GovernanceValidation.TryCopy(configuration.Assignments, out GovernanceConfiguration target) ||
                !GovernanceValidation.IsSubset(target, governance.Configured))
                return CommandResult.RuleUnavailable("rules.governance_activation", "Activation must resolve a subset of the submitted appointments.");
            var slots = governance.OpenSlots.ToDictionary(slot => slot.Id);
            foreach (GovernanceAssignment row in target.Assignments)
            {
                if (!state.Officers.TryGetValue(row.OfficerId, out OfficerState? officer) || officer.ForceId != force.Id ||
                    officer.Presence is not (null or OfficerPresence.Present))
                    return CommandResult.RuleUnavailable("rules.governance_activation", "An ineligible departed officer remains in the activation result.");
                // Succession may happen after submission. Keep the old state intact until
                // the provider explicitly resolves eligibility and the associated bill.
                if (slots[row.SlotId].Kind == GovernanceSlotKind.Advisor &&
                    (force.RulerId is null || row.OfficerId == force.RulerId))
                    return CommandResult.RuleUnavailable("rules.governance_activation", "Military adviser eligibility conflicts with the current or unresolved ruler.");
            }
            GovernanceBill bill = pending.Bill;
            if (evaluation.DeferredCommandBooksCharged is not int charge || evaluation.PaidCommandBooksRefunded is not int refund ||
                charge < 0 || refund < 0 ||
                charge > (bill.Timing == GovernanceDebitTiming.Activation ? bill.CommandBooks : 0) ||
                refund > (bill.Timing == GovernanceDebitTiming.Submission ? bill.CommandBooks : 0))
                return CommandResult.RuleUnavailable("rules.governance_resources", "Activation charges or refunds exceed the recorded bill.");
            DistrictState district = districts[bill.DistrictId];
            if (district.OrdersRemaining is not int remaining)
                return CommandResult.RuleUnavailable("rules.command_budget", "Command budget is unresolved.");
            long next = (long)remaining - charge + refund;
            if (next < CommandBookAvailability.Reserved(state, district.Id, force.Id))
                return CommandResult.Reject("Activation would spend another command's reservation.", "governance.insufficient_orders");
            if (next > int.MaxValue)
                return CommandResult.RuleUnavailable("rules.governance_resources", "Activation accounting exceeds the known budget range.");
            districts[district.Id] = district with { OrdersRemaining = (int)next };
            forces[force.Id] = force with
            {
                Governance = governance with
                {
                    Configured = target,
                    Active = new GovernanceConfiguration(target.Assignments.ToArray()),
                    Pending = null
                }
            };
        }
        GameState result = state with { Forces = forces, Districts = districts };
        StateValidator.Validate(result);
        return CommandResult.Accept(result);
    }
}
