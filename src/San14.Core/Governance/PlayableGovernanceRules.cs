namespace San14.Core;

/// <summary>
/// Temporary first-playable policy appointments. Source policy identities and groups are
/// retained, while slot capacity, command fee, fallback cost and activation timing are
/// deliberately confined to this ruleset.
/// </summary>
internal sealed class PlayableGovernanceCommandRules : IGovernanceCommandRules
{
    public GovernanceCommandEvaluation Evaluate(GameState state, ForceState force,
        GovernanceConfiguration target, CommandContext context)
    {
        if (context.DelegatedDistrictId is not null)
            return Denied("This provisional force-wide policy command requires direct control.");
        if (force.Governance is not { } governance)
            return Unavailable("rules.governance_state");
        if (target.Assignments.Select(row => row.OfficerId).Distinct().Count() != target.Assignments.Length)
            return Denied("An officer cannot occupy two provisional policy slots.");
        Dictionary<int, GovernanceSlot> slots = governance.OpenSlots.ToDictionary(slot => slot.Id);
        var policies = new Dictionary<int, GovernanceOfficerPolicy>();
        foreach (GovernanceAssignment row in target.Assignments)
        {
            if (!slots.TryGetValue(row.SlotId, out GovernanceSlot? slot) ||
                !state.Officers.TryGetValue(row.OfficerId, out OfficerState? officer) ||
                officer.ForceId != force.Id || officer.Presence != OfficerPresence.Present ||
                officer.Deployed || PlayableOfficerAvailability.IsSourceBusy(state, officer) ||
                PlayableDiplomacy.IsOfficerOnMission(state, officer.Id) || state.PreparedOfficerMoves?.ContainsKey(officer.Id) == true ||
                state.PreparedTalentOrders?.ContainsKey(officer.Id) == true ||
                state.PreparedDeployments.Values.Any(plan => plan.OfficerId == officer.Id))
                return Denied("Policy officer is unavailable.");
            if (officer.PolicyId is not int policyId || policyId <= 0 ||
                !state.Policies.TryGetValue(policyId, out PolicyDefinition? policy) ||
                policy.DepartmentCode is not int group)
                return Unavailable("rules.governance_policies");
            int cost;
            if (PolicyCosts.TryCalculate(state, officer.Id, out PolicyCost sourceCost))
                cost = sourceCost.Total;
            else if (officer.PolicyBaseCost is int baseCost && baseCost >= 0)
                cost = PolicyCosts.Calculate(baseCost, policy.CostAddition,
                    sameDoctrine: false, isRuler: false, ignoreDoctrineDiscount: true).Total;
            else return Unavailable("rules.governance_policies");
            policies.Add(officer.Id, new GovernanceOfficerPolicy($"group-{group}", cost,
                slot.Kind == GovernanceSlotKind.Advisor ? officer.BaseAbilities?.Intelligence : null));
        }
        int chargedDistrict = state.Districts.Values.Where(district => district.ForceId == force.Id)
            .OrderBy(district => district.Number == 1 ? 0 : 1)
            .ThenBy(district => district.Number).ThenBy(district => district.Id)
            .Select(district => district.Id).FirstOrDefault(-1);
        if (chargedDistrict < 0) return Unavailable("rules.command_budget");
        GovernanceConfiguration retained = new(governance.Active.Assignments
            .Where(active => target.Assignments.Contains(active)).ToArray());
        return new GovernanceCommandEvaluation(GovernanceDecisionStatus.Allowed)
        {
            OfficerPolicies = policies,
            ExceedsLevelLimit = false, // Effects are not calculated in this playable ruleset.
            Bill = new GovernanceBill(chargedDistrict, target.Assignments.Length,
                GovernanceDebitTiming.Submission),
            PreviousCommandBooksRefunded = governance.Pending?.Bill is
                { Timing: GovernanceDebitTiming.Submission } previous ? previous.CommandBooks : 0,
            ActiveAfterSubmission = retained
        };
    }

    private static GovernanceCommandEvaluation Unavailable(string key) =>
        new(GovernanceDecisionStatus.Unavailable) { MissingRuleKey = key };
    private static GovernanceCommandEvaluation Denied(string explanation) =>
        new(GovernanceDecisionStatus.Denied) { Explanation = explanation };
}

internal sealed class PlayableGovernanceActivationRules : IGovernanceActivationRules
{
    public GovernanceActivationEvaluation Evaluate(GameState state, ForceState force)
    {
        if (force.Governance is not { } governance)
            return new(GovernanceDecisionStatus.Unavailable) { MissingRuleKey = "rules.governance_state" };
        GovernanceAssignment[] eligible = governance.Configured.Assignments.Where(row =>
        {
            if (!state.Officers.TryGetValue(row.OfficerId, out OfficerState? officer) ||
                officer.ForceId != force.Id || officer.Presence != OfficerPresence.Present ||
                PlayableOfficerAvailability.IsSourceBusy(state, officer) || PlayableDiplomacy.IsOfficerOnMission(state, officer.Id))
                return false;
            GovernanceSlot slot = governance.OpenSlots.Single(item => item.Id == row.SlotId);
            return slot.Kind != GovernanceSlotKind.Advisor ||
                (force.RulerId is int rulerId && officer.Id != rulerId &&
                    officer.BaseAbilities?.Intelligence >= 70);
        }).ToArray();
        return new GovernanceActivationEvaluation(GovernanceDecisionStatus.Allowed)
        {
            Configuration = new GovernanceConfiguration(eligible),
            DeferredCommandBooksCharged = 0,
            PaidCommandBooksRefunded = 0
        };
    }
}
