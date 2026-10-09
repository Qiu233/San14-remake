using San14.Core;

namespace San14.Data;

/// <summary>Person reward numbers after the current-source queries have been
/// resolved. Keep unsigned XP arithmetic, signed merit arithmetic and float
/// trait rounding distinct; their overflow and division semantics differ.</summary>
internal static class PersonRewardArithmetic
{
    internal readonly record struct AbilityAdvance(uint Experience, byte BaseAbility);
    internal readonly record struct PendingMeritAdjustment(int TraitCorrection, int PanelCorrection,
        int DoctrineCorrection, int StageCorrection, int BeforeScaling, int Earned, int PendingMerit);

    internal static PersonNormalArmyRewardRequests CalculateRequests(int kind, int amount, int growth, int merit)
        => kind switch
        {
            0 or 1 => new(kind, amount, SignedPercent(growth, amount),
                unchecked(unchecked(merit * amount) * 10) / 100),
            2 => new(kind, amount, growth, unchecked(merit * 10)),
            3 => new(kind, amount, growth, 0),
            _ => throw new InvalidDataException("Normal reward kind is outside the source switch.")
        };

    internal static int CalculateGrowth(int requested, int trait, int traitScale,
        int advantage, int advantageScale, bool forceValid, int policy, int policyScale)
    {
        uint quantity = unchecked((uint)requested);
        uint traitBonus = Math.Max(trait != 0 ? 1u : 0u,
            ExperiencePercent(unchecked(traitScale * trait), quantity));
        uint bonus = unchecked(traitBonus + ExperiencePercent(unchecked(advantageScale * advantage), quantity));
        uint primary = forceValid ? ExperiencePercent(unchecked(policyScale * policy + 100), quantity) : quantity;
        return unchecked((int)(bonus + primary));
    }

    internal static AbilityAdvance AdvanceAbility(uint experience, byte baseAbility, int applied,
        Func<int, uint> nextThreshold)
    {
        uint after = unchecked(experience + (uint)applied);
        byte afterBase = baseAbility;
        while (afterBase < 100 && nextThreshold(afterBase + 1) <= after) afterBase++;
        return new(after, afterBase);
    }

    internal static PendingMeritAdjustment CalculatePendingMerit(int requested, int pending,
        int trait, int traitScale, int panel, int doctrine, bool stageOne, int stageScale, int? percent)
    {
        float product = (float)unchecked(traitScale * trait) * (float)requested;
        float scaled = product / 100f;
        int traitCorrection = scaled < 0f && scaled > -1f ? -1 :
            scaled > 0f && scaled < 1f ? 1 : Truncate(scaled);
        int panelCorrection = SignedPercent(panel, requested);
        int doctrineCorrection = SignedPercent(doctrine, requested);
        int stageCorrection = stageOne ? SignedPercent(stageScale, requested) : 0;
        int total = Math.Max(0, unchecked(unchecked(unchecked(unchecked(stageCorrection + doctrineCorrection) +
            panelCorrection) + traitCorrection) + requested));
        int earned = percent.HasValue ? SignedPercent(percent.Value, total) : total;
        int sum = unchecked(pending + earned);
        int after = Math.Clamp(sum, 0, OfficerMeritRules.MaximumPendingMerit);
        return new(traitCorrection, panelCorrection, doctrineCorrection, stageCorrection, total, earned, after);
    }

    private static int SignedPercent(int coefficient, int amount) => unchecked(coefficient * amount) / 100;
    private static uint ExperiencePercent(int coefficient, uint quantity) => unchecked((uint)coefficient * quantity) / 100;
    private static int Truncate(float value) => !float.IsFinite(value) || value >= 2147483648f || value < -2147483648f
        ? int.MinValue : (int)value;
}
