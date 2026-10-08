namespace San14.Data;

/// <summary>The reached Army→Army16B670 arithmetic inputs, after candidate
/// filtering/ordering and all current native queries. This is not an automatic
/// selection permission or an effect application transaction.</summary>
public sealed record OriginalTacticAutomaticActivationRateInput(ushort FormationRate88,
    int CurrentDifficultyBaseline, ushort TerrainRate2E, float CurrentHexMultiplier,
    int SourceHexPhysicalSlot, int TargetHexPhysicalSlot, int CandidateOrdinal);

public sealed record OriginalTacticAutomaticActivationTrial(float UncappedPercent,
    float CappedPercent, int? Sample, int? Threshold, int FirstSalt, int SecondSalt,
    bool Accepted);

/// <summary>SC16B670/TC16B910's ordinary tutorial-header0 rate trial. Float32
/// instruction order and pure salted sampler are preserved. The caller owns
/// actual candidate order, world and the separate tutorial override branch.</summary>
public static class OriginalTacticAutomaticActivationSourceMath
{
    public static float CalculateRate(OriginalTacticAutomaticActivationRateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        float value = input.FormationRate88;
        value = value * (float)input.CurrentDifficultyBaseline;
        value = value / 100f;
        value = value * (float)input.TerrainRate2E;
        value = value / 100f;
        return value * input.CurrentHexMultiplier;
    }

    //MINSS takes its second operand for equality or unordered inputs. The
    //following COMISS zero,value;JAE also samples on unordered, not only>0.
    public static float Cap(float percent) => 100f < percent ? 100f : percent;
    public static bool RequiresSample(float percent) => !(Cap(percent) <= 0f);

    public static OriginalTacticAutomaticActivationTrial Trial(
        OriginalTacticAutomaticActivationRateInput input, OriginalTacticSourceSamplerWorld? knownWorld)
    {
        float raw = CalculateRate(input), capped = Cap(raw);
        int first = unchecked(input.CandidateOrdinal * input.SourceHexPhysicalSlot);
        int second = unchecked(2 * input.TargetHexPhysicalSlot);
        if (!RequiresSample(raw)) return new(raw, capped, null, null, first, second, false);
        int sample = OriginalTacticEffectSourceSamplerMath.Sample(10000, 1, first, second, 0, 0, knownWorld);
        float thresholdValue = capped * 10000f;
        thresholdValue = thresholdValue / 100f;
        int threshold = !float.IsFinite(thresholdValue) || thresholdValue < int.MinValue ||
            thresholdValue >= 2147483648f ? int.MinValue : (int)thresholdValue;
        return new(raw, capped, sample, threshold, first, second, sample < threshold);
    }
}
