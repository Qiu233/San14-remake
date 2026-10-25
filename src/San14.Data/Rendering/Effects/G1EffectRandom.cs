namespace San14.Data;

/// <summary>The shared effect random stream, separate from campaign and App
/// randomness. The host supplies its actual owned initial state and preserves
/// one stream across the source-ordered effect updates.</summary>
public sealed class G1EffectRandom(uint state)
{
    private readonly object gate = new();
    private uint value = state;

    public uint State
    {
        get { lock (gate) return value; }
    }

    public uint NextUInt()
    {
        lock (gate)
        {
            value = unchecked(value * 0x10DCDu + 1u);
            return value;
        }
    }

    /// <summary>Inclusive zero-to-one sample; the original divides the upper
    /// sixteen bits by 65535, not 65536.</summary>
    public float NextUnit() => (NextUInt() >> 16) / 65535f;

    public float NextSigned()
    {
        float centered = NextUnit() - 0.5f;
        return centered + centered;
    }

    /// <summary>Definition count rounding always advances the stream, including
    /// integral counts. Call after the actual count expression has been evaluated.</summary>
    public int RoundCount(float expressionValue)
    {
        int count = unchecked((int)expressionValue);
        if (NextUnit() < expressionValue - count) count = unchecked(count + 1);
        return count;
    }
}
