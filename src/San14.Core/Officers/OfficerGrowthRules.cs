namespace San14.Core;

/// <summary>Resolved growth arithmetic. Experience additions and percentage
/// products preserve the game's unsigned 32-bit wrap and integer division.</summary>
public static class OfficerGrowthRules
{
    public const int MaximumBaseAbility = 100;
    public readonly record struct AbilityAdvance(uint Experience, int BaseAbility);

    public static int CalculateExperienceGain(int requested, int trait, int traitScale,
        int advantage, int advantageScale, bool forceValid, int policy, int policyScale)
    {
        uint quantity = unchecked((uint)requested);
        uint traitBonus = Math.Max(trait != 0 ? 1u : 0u,
            Percent(unchecked(traitScale * trait), quantity));
        uint bonus = unchecked(traitBonus + Percent(unchecked(advantageScale * advantage), quantity));
        uint primary = forceValid ? Percent(unchecked(policyScale * policy + 100), quantity) : quantity;
        return unchecked((int)(bonus + primary));
    }

    public static AbilityAdvance AdvanceAbility(uint experience, int baseAbility,
        int applied, Func<int, uint> nextThreshold)
    {
        uint after = unchecked(experience + (uint)applied);
        int afterBase = baseAbility;
        while (afterBase < MaximumBaseAbility && nextThreshold(afterBase + 1) <= after) afterBase++;
        return new(after, afterBase);
    }

    private static uint Percent(int coefficient, uint quantity) => unchecked((uint)coefficient * quantity) / 100;
}
