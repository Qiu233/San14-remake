namespace San14.Data;

/// <summary>268690's signed32 arithmetic. Callers retain native early-return
/// gates and lazy input reads. Presentation clamps only after this result.</summary>
internal static class ArmyFireCommandProbabilityMath
{
    internal static int Baseline(int attackerIntelligence, int defenderIntelligence,
        int terrainBonus, int abnormalBonus, int minimum) =>
        Math.Max(unchecked(attackerIntelligence / 2 - defenderIntelligence / 2 +
            terrainBonus + abnormalBonus), minimum);

    internal static int TraitCorrection(int baseline, int contribution, int scale) =>
        unchecked(contribution * scale * baseline) / 100;

    internal static int ApplyModifiers(int baseline, int traitCorrection, int forcePanel) =>
        unchecked(baseline + traitCorrection + forcePanel);
}
