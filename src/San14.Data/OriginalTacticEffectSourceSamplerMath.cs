using System.Numerics;

namespace San14.Data;

/// <summary>SC3FD130/TC3FCA40's pure source sampler. Its four explicit i32
/// salts and optional current World450/45C inputs determine the result. It
/// neither reads nor advances the separately owned mutable random stream.</summary>
public static class OriginalTacticEffectSourceSamplerMath
{
    public static int Sample(int bound, int divisor, int firstSalt, int secondSalt,
        int thirdSalt, int fourthSalt, OriginalTacticSourceSamplerWorld? world)
    {
        if (bound < 2) return 0;
        int seed = unchecked(firstSalt + secondSalt + thirdSalt + fourthSalt);
        if (world is not null)
            seed = unchecked(seed + world.Salt45C + world.Day450 / Math.Max(1, divisor));
        int iterations = (seed & 15) + 1;
        uint bits = unchecked((uint)seed);
        for (int index = 0; index < iterations; index++)
            bits = BitOperations.RotateLeft(unchecked(bits * 0x0693D4B5u - 0x290550B7u), 16);
        uint value = BitOperations.RotateLeft(unchecked(bits * 0x41C64E6Du + 0x3039u), 16) & 0x7FFF_FFFFu;
        return (int)(value % (uint)bound);
    }
}

public sealed record OriginalTacticSourceSamplerWorld(int Day450, int Salt45C);
