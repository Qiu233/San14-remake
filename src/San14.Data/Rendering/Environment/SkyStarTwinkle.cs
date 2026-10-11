using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>The periodic brightness table shared by Main and environment stars.
/// Each owner retains its own phase; constructing this table advances no clock.</summary>
public static class SkyStarTwinkle
{
    public static float[] CreateTable()
    {
        var samples = new float[48];
        for (int n = 0; n < samples.Length; n++)
        {
            float angle = Divide(Multiply(n, MathF.PI), 48);
            float amplitude = Multiply(Add(ArmyAnimationSampler.SourceSinCos(angle).Sin, 2), .06f);
            float value = .8f;
            for (int harmonic = 0; harmonic < 8; harmonic++)
            {
                angle = Add(angle, angle);
                amplitude = Multiply(amplitude, .85f);
                value = Add(value, Multiply(ArmyAnimationSampler.SourceSinCos(angle).Sin, amplitude));
            }
            samples[n] = value;
        }
        return Enumerable.Range(0, 64).Select(index => samples[((index / 4) * 3 + index % 4) % 48]).ToArray();
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float left, float right) => left + right;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float left, float right) => left * right;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float left, float right) => left / right;
}
