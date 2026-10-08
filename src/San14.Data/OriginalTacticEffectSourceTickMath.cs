namespace San14.Data;

/// <summary>Pure ordinary Army status update leaves. Source timing is four
/// invocations per day, but these methods consume one manager invocation only.</summary>
public static class OriginalTacticEffectSourceTickMath
{
    /// <summary>SC224020: one fresh current limit, two signed clamps, and final
    /// low-byte conversion. Decay has no provocation-owner write.</summary>
    public static byte DecayAbnormal(byte current, int currentUpperBound)
    {
        int value = unchecked(current - 1);
        value = value < 0 ? 0 : Math.Min(value, currentUpperBound);
        value = value < 0 ? 0 : Math.Min(value, currentUpperBound);
        return unchecked((byte)value);
    }

    /// <summary>SC223920 uses immediate240, not a mutable parameter global.
    /// The caller clears the matching modifier value whenever the result is zero.</summary>
    public static byte DecayModifierDuration(byte current)
    {
        int value = current - 1;
        value = value < 0 ? 0 : Math.Min(value, 240);
        return (byte)(value < 0 ? 0 : Math.Min(value, 240));
    }
}
