namespace San14.Core;

/// <summary>
/// Resolves a person's final current policy contribution level, including all applicable
/// modifiers and context. Null is unavailable. Base cost is not a fallback. Implementations
/// must not mutate state; contributor eligibility and activation timing are separate rules.
/// </summary>
public interface IEffectivePolicyLevels
{
    int? Resolve(GameState state, int officerId);
}

/// <summary>A capped aggregate level and its selected ten-level effect value. Level zero has value zero.</summary>
public readonly record struct PolicyEffectValue(int Level, int Value);

/// <summary>
/// CE effect aggregation over an explicitly resolved contributor sequence. Repeated people
/// and repeated policy effect references are consumed repeatedly, as in the verified original
/// iterator. This query does not derive contributors from configured/active governance,
/// appoint anyone, calculate final personal levels, or restore command books.
/// </summary>
public static class OriginalPolicyEffects
{
    /// <summary>
    /// The caller supplies the complete known sequence for this query, including any ruler
    /// contribution and original activation filtering. Unknown policy references or final
    /// levels remain unavailable. A known empty sequence has a known zero effect.
    /// </summary>
    public static bool TryCalculate(GameState state, int effectId,
        IReadOnlyList<int> contributorIds, IEffectivePolicyLevels levels,
        out PolicyEffectValue result)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(contributorIds);
        ArgumentNullException.ThrowIfNull(levels);
        result = default;
        if (state.PolicyEffects is null || state.Policies is null || state.Officers is null ||
            effectId < 0 || !state.PolicyEffects.TryGetValue(effectId, out PolicyEffectDefinition? effect) ||
            effect is null || effect.Id != effectId || effect.LevelValues is not { Length: 10 } values ||
            values.Any(value => value < 0))
            return false;

        // Providers may retain their input; snapshot the caller-owned sequence before invoking one.
        int[] contributors = contributorIds.ToArray();
        int[] effectValues = (int[])values.Clone();
        int aggregate = 0;
        foreach (int officerId in contributors)
        {
            if (officerId < 0 || !state.Officers.TryGetValue(officerId, out OfficerState? officer) ||
                officer is null || officer.Id != officerId || officer.PolicyId is not int policyId ||
                !state.Policies.TryGetValue(policyId, out PolicyDefinition? policy) || policy is null ||
                policy.Id != policyId || policy.EffectIds is not { } references)
                return false;
            int[] effectIds = (int[])references.Clone();
            foreach (int referencedId in effectIds)
                if (referencedId < 0 || !state.PolicyEffects.ContainsKey(referencedId) ||
                    state.PolicyEffects[referencedId] is null)
                    return false;

            int? resolved = levels.Resolve(state, officerId);
            if (resolved is not int level || level < 0) return false;
            int contribution = Math.Min(level, 10);
            foreach (int referencedId in effectIds)
                if (referencedId == effectId)
                    aggregate = Math.Min(aggregate + contribution, 10);
            // Continue even at ten: the complete supplied sequence must be known. Do not hide
            // a later legacy/null reference or unknown level behind an already saturated value.
        }
        result = new PolicyEffectValue(aggregate, aggregate == 0 ? 0 : effectValues[aggregate - 1]);
        return true;
    }
}
