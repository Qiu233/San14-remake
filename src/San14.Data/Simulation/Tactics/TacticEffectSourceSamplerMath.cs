namespace San14.Data;

/// <summary>SC3FD130/TC3FCA40's pure source sampler. Its four explicit i32
/// salts and optional current World450/45C inputs determine the result. It
/// neither reads nor advances the separately owned mutable random stream.</summary>
public static class TacticEffectSourceSamplerMath
{
    public static int Sample(int bound, int divisor, int firstSalt, int secondSalt,
        int thirdSalt, int fourthSalt, TacticSourceSamplerWorld? world)
    {
        if (bound < 2) return 0;
        int seed = unchecked(firstSalt + secondSalt + thirdSalt + fourthSalt);
        if (world is not null)
            seed = unchecked(seed + world.Salt45C + world.Day450 / Math.Max(1, divisor));
        uint value = SourceRandom32Mix.Advance(unchecked((uint)seed)) & 0x7FFF_FFFFu;
        return (int)(value % (uint)bound);
    }
}

public sealed record TacticSourceSamplerWorld(int Day450, int Salt45C);
