using System.Numerics;

namespace San14.Data;

/// <summary>The effect's interpolated lattice-vector noise. Samples and permutation
/// belong to the shared context; sampling never resets or advances its RNG.</summary>
public static class G1EffectVectorNoise
{
    public static Vector4 Sample(G1EffectContext context, ReadOnlySpan<float> coordinates,
        float amplitude, uint octaves)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (coordinates.Length is < 1 or > 4)
            throw new NotSupportedException("Effect vector noise requires one through four coordinates.");
        if (context.VectorNoise.Length != 256 || context.Permutation.Length != 256)
            throw new InvalidOperationException("Effect vector noise has no initialized shared tables.");
        Vector4 result = Lattice(context, coordinates) * amplitude;
        float frequency = 1;
        Span<float> next = stackalloc float[4];
        for (uint octave = 1; octave < octaves; octave++)
        {
            frequency += frequency;
            amplitude *= 0.5f;
            for (int axis = 0; axis < coordinates.Length; axis++)
                next[axis] = coordinates[axis] * frequency;
            Vector4 sample = Lattice(context, next[..coordinates.Length]);
            result = new(sample.X * amplitude + result.X, sample.Y * amplitude + result.Y,
                sample.Z * amplitude + result.Z, sample.W * amplitude + result.W);
        }
        return result;
    }

    private static Vector4 Lattice(G1EffectContext context, ReadOnlySpan<float> coordinates)
    {
        int dimensions = coordinates.Length;
        Span<int> initial = stackalloc int[4], current = stackalloc int[4];
        Span<float> weights = stackalloc float[4];
        for (int axis = 0; axis < dimensions; axis++)
        {
            float scaled = coordinates[axis] * 16f, floor = MathF.Floor(scaled);
            initial[axis] = current[axis] = unchecked((int)floor);
            weights[axis] = 1f - (scaled - floor);
        }
        ReadOnlySpan<byte> permutation = context.Permutation.Span;
        ReadOnlySpan<Vector4> samples = context.VectorNoise.Span;
        Vector4 result = default;
        for (int corner = 0; corner < 1 << dimensions; corner++)
        {
            float weight = weights[0];
            for (int axis = 1; axis < dimensions; axis++) weight *= weights[axis];
            int hash = current[0];
            for (int axis = 1; axis < dimensions; axis++) hash = permutation[hash & 255] + current[axis];
            Vector4 value = samples[permutation[hash & 255]];
            result = new(weight * value.X + result.X, weight * value.Y + result.Y,
                weight * value.Z + result.Z, weight * value.W + result.W);
            // Preserve the native binary-carry traversal and its repeated weight
            // subtraction, rather than recomputing independently rounded weights.
            for (int axis = 0; axis < dimensions; axis++)
            {
                weights[axis] = 1f - weights[axis];
                if ((corner >> axis & 1) == 0) { current[axis] = unchecked(initial[axis] + 1); break; }
                current[axis] = initial[axis];
            }
        }
        return result;
    }
}
