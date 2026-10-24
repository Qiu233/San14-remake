namespace San14.Data;

public sealed record TacticEffectGateSourceInput(int SourceAbility, int TargetAbility,
    int CurrentBaseline, int CurrentHigherCoefficient, int CurrentLowerDenominator,
    int SourceTrait99, int CurrentTraitCoefficient);

/// <summary>SC16F700/TC16F9A0's Army effect gate arithmetic, after the
/// independently queried commander effect98 early return. It uses the pure
/// 3FD130 sampler and leaves the mutable random stream unchanged.</summary>
public static class TacticEffectSourceGateMath
{
    public static float FailurePercent(TacticEffectGateSourceInput input) => FailurePercent(input, 0.5f);

    /// <summary>TC16F9A0's explicit negative lower-ability coefficient.
    /// The existing no-locale API retains its previous independent contract.</summary>
    public static float FailurePercentTc(TacticEffectGateSourceInput input) => FailurePercent(input, -1f);

    private static float FailurePercent(TacticEffectGateSourceInput input, float lowerCoefficient)
    {
        float change;
        if (input.SourceAbility > input.TargetAbility)
        {
            int product = unchecked(unchecked(input.SourceAbility - input.TargetAbility) * input.CurrentHigherCoefficient);
            change = (float)Math.Sqrt((double)product);
        }
        else
        {
            int differenceOfSquares = unchecked(unchecked(input.TargetAbility * input.TargetAbility) -
                unchecked(input.SourceAbility * input.SourceAbility));
            change = (float)differenceOfSquares;
            change *= lowerCoefficient;
            change /= (float)input.CurrentLowerDenominator;
        }
        float failure = 100f - (change + (float)input.CurrentBaseline);
        failure = MinSs(failure, 100f);
        failure = MaxSs(failure, 0f);
        int modifier = unchecked(input.SourceTrait99 * input.CurrentTraitCoefficient);
        float contribution = (float)modifier * (100f - failure);
        contribution /= 100f;
        failure += contribution;
        return MinSs(100f, failure);
    }

    public static bool? Allows(TacticEffectGateSourceInput input, int tacticPhysicalSlot,
        int capturedSourceHexPhysicalSlot, int targetHexPhysicalSlot, TacticSourceSamplerWorld? world)
        => AllowsFailure(FailurePercent(input), tacticPhysicalSlot,
            capturedSourceHexPhysicalSlot, targetHexPhysicalSlot, world);

    public static bool? AllowsTc(TacticEffectGateSourceInput input, int tacticPhysicalSlot,
        int sourceHexPhysicalSlot, int targetHexPhysicalSlot, TacticSourceSamplerWorld? world)
        => AllowsFailure(FailurePercentTc(input), tacticPhysicalSlot,
            sourceHexPhysicalSlot, targetHexPhysicalSlot, world);

    private static bool? AllowsFailure(float failure, int tacticPhysicalSlot,
        int capturedSourceHexPhysicalSlot, int targetHexPhysicalSlot, TacticSourceSamplerWorld? world)
    {
        if (failure <= 0f) return true;
        //The actual game route has a World instance. Null here preserves an
        //unknown current owner rather than silently choosing the no-World ABI.
        if (world is null) return null;
        int sample = TacticEffectSourceSamplerMath.Sample(10000, 1,
            unchecked(unchecked(tacticPhysicalSlot + 5) * capturedSourceHexPhysicalSlot),
            unchecked(targetHexPhysicalSlot * 2), 0, 0, world);
        float threshold = failure * 10000f;
        threshold /= 100f;
        int converted = !float.IsFinite(threshold) || threshold >= 2147483648f || threshold < -2147483648f
            ? int.MinValue : (int)threshold;
        return sample >= converted;
    }

    //MINSS/MAXSS return the second operand for unordered comparison (and
    //equal values), retaining the reached native NaN behavior.
    private static float MinSs(float first, float second) => first < second ? first : second;
    private static float MaxSs(float first, float second) => first > second ? first : second;
}
