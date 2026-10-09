namespace San14.Core;

/// <summary>The flag records the matching-doctrine branch, including a cost clamped to one.</summary>
public readonly record struct PolicyCost(int BeforeDiscount, int Total, bool DoctrineDiscountApplied);

/// <summary>
/// CE policy cost arithmetic verified against the original consumer. This is not an
/// appointment, active-policy, policy-level or force-wide budget calculation.
/// </summary>
public static class PolicyCosts
{
    /// <summary>All inputs are resolved values; neither doctrine comparison is an unknown/null comparison.</summary>
    public static PolicyCost Calculate(int personalBaseCost, int policyCostAddition,
        bool sameDoctrine, bool isRuler, bool ignoreDoctrineDiscount = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(personalBaseCost);
        ArgumentOutOfRangeException.ThrowIfNegative(policyCostAddition);
        long sum = (long)personalBaseCost + policyCostAddition;
        if (sum > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(policyCostAddition), "Combined policy cost exceeds the supported integer range.");
        int before = Math.Max(1, (int)sum);
        bool discount = !ignoreDoctrineDiscount && !isRuler && sameDoctrine;
        return new PolicyCost(before, Math.Max(1, before - (discount ? 3 : 0)), discount);
    }

    /// <summary>
    /// Resolves the original relationship through the officer's current district and its force.
    /// Missing legacy/source data is unavailable, never the original binary's invalid-slot sentinel.
    /// The current force's independent doctrine is not the ruler's personal doctrine.
    /// </summary>
    public static bool TryCalculate(GameState state, int officerId, out PolicyCost cost,
        bool ignoreDoctrineDiscount = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        cost = default;
        if (!state.Officers.TryGetValue(officerId, out OfficerState? officer) ||
            officer.PolicyId is not int policyId || officer.PolicyBaseCost is not int personalCost ||
            personalCost < 0 || !state.Policies.TryGetValue(policyId, out PolicyDefinition? policy) ||
            policy.CostAddition < 0 || (long)personalCost + policy.CostAddition > int.MaxValue)
            return false;
        if (ignoreDoctrineDiscount)
        {
            cost = Calculate(personalCost, policy.CostAddition, false, false, true);
            return true;
        }
        if (officer.DistrictId is not int districtId ||
            !state.Districts.TryGetValue(districtId, out DistrictState? district) ||
            !state.Forces.TryGetValue(district.ForceId, out ForceState? force) ||
            force.RulerId is not int rulerId || !state.Officers.TryGetValue(rulerId, out OfficerState? ruler))
            return false;
        bool isRuler = rulerId == officer.Id;
        if (!isRuler && (officer.DoctrineId is not int personalDoctrine ||
            !state.Doctrines.ContainsKey(personalDoctrine) || ruler.DoctrineId is not int rulerDoctrine ||
            !state.Doctrines.ContainsKey(rulerDoctrine)))
            return false;
        cost = Calculate(personalCost, policy.CostAddition,
            !isRuler && officer.DoctrineId == ruler.DoctrineId, isRuler);
        return true;
    }
}
