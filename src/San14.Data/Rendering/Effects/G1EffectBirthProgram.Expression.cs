using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The four real birth inputs plus the actual emitter/parent-particle
/// expression context. The current context is provided by its mutable owner.</summary>
public readonly record struct G1EffectParticleBirthInputs(Matrix4x4 Orientation, Vector4 Position,
    Matrix4x4 ParentWorld, Vector4 InheritedVelocity, ReadOnlyMemory<byte> SourceContext,
    int MaterialBase = 0, G1EffectParticleSlot? ParentSlot = null);

/// <summary>The prefix scalar AST used by particle definitions. Cursor points
/// at an opcode and advances through the actual ordered expression operands.</summary>
public static class G1EffectBirthExpression
{
    public static float Evaluate(ReadOnlyMemory<byte> bytes, ref int cursor,
        ReadOnlyMemory<byte> sourceContext, G1EffectRandom random, G1EffectContext? effectContext = null)
    {
        ArgumentNullException.ThrowIfNull(random);
        int opcode = ReadInt(bytes, ref cursor);
        switch (opcode)
        {
            case 0: return ReadFloat(bytes, ref cursor);
            case 1:
            {
                int offset = ReadInt(bytes, ref cursor);
                if (offset < 0 || offset > sourceContext.Length - 4)
                    throw new InvalidDataException("Particle expression reads outside its current owner context.");
                return BinaryPrimitives.ReadSingleLittleEndian(sourceContext.Span[offset..]);
            }
            case 2:
            {
                float center = ReadFloat(bytes, ref cursor), spread = ReadFloat(bytes, ref cursor);
                return random.NextSigned() * spread + center;
            }
            case 3:
            {
                float value = Evaluate(bytes, ref cursor, sourceContext, random, effectContext);
                float frequency = ReadFloat(bytes, ref cursor);
                uint octaves = unchecked((uint)ReadInt(bytes, ref cursor));
                if (effectContext is null || effectContext.ScalarNoise.Length != 256)
                    throw new InvalidOperationException("Scalar noise expression requires the initialized effect context.");
                float coordinate = frequency * value, result = 0, amplitude = 1;
                var samples = effectContext.ScalarNoise.Span;
                var permutation = effectContext.Permutation.Span;
                for (uint octave = 0; octave < octaves; octave++)
                {
                    float cell = MathF.Floor(coordinate * 16f);
                    float phase = coordinate * 16f - cell;
                    coordinate += coordinate;
                    phase = (3f - (phase + phase)) * phase * phase;
                    int index = unchecked((int)cell);
                    float sample = ((1f - phase) * samples[permutation[index & 255]] +
                        phase * samples[permutation[(index + 1) & 255]]) * amplitude;
                    amplitude *= 0.5f;
                    result += sample;
                }
                return result;
            }
            case 11:
            {
                float value = Evaluate(bytes, ref cursor, sourceContext, random, effectContext);
                return ReadFloat(bytes, ref cursor) + value;
            }
            case 12:
            {
                float left = Evaluate(bytes, ref cursor, sourceContext, random, effectContext);
                return left - Evaluate(bytes, ref cursor, sourceContext, random, effectContext);
            }
            case 13:
            {
                float left = Evaluate(bytes, ref cursor, sourceContext, random, effectContext);
                return Evaluate(bytes, ref cursor, sourceContext, random, effectContext) * left;
            }
            case 14:
            {
                float value = Evaluate(bytes, ref cursor, sourceContext, random, effectContext);
                return ReadFloat(bytes, ref cursor) * value;
            }
            case 15:
            {
                float numerator = Evaluate(bytes, ref cursor, sourceContext, random, effectContext);
                float denominator = Evaluate(bytes, ref cursor, sourceContext, random, effectContext);
                if (denominator == 0f) denominator = 1.1754944e-38f;
                return numerator / denominator;
            }
            case 22:
            {
                float angle = Evaluate(bytes, ref cursor, sourceContext, random, effectContext);
                const float tau = 6.2831855f;
                if (MathF.Abs(angle) > tau) angle -= (int)(angle / tau) * tau;
                return G1EffectShapeMath.SinCos(angle).Sine;
            }
            case 23:
            {
                float angle = Evaluate(bytes, ref cursor, sourceContext, random, effectContext);
                const float tau = 6.2831855f;
                if (MathF.Abs(angle) > tau) angle -= (int)(angle / tau) * tau;
                return G1EffectShapeMath.SinCos(angle).Cosine;
            }
            case 32:
            {
                int first = ReadInt(bytes, ref cursor), second = ReadInt(bytes, ref cursor),
                    divisor = ReadInt(bytes, ref cursor);
                float Field(int offset)
                {
                    if (offset < 0 || offset > sourceContext.Length - 4)
                        throw new InvalidDataException("Particle expression reads outside its current owner context.");
                    return BinaryPrimitives.ReadSingleLittleEndian(sourceContext.Span[offset..]);
                }
                return Field(second) * Field(first) / Field(divisor);
            }
            case 46:
            {
                float value = Evaluate(bytes, ref cursor, sourceContext, random, effectContext);
                float angle = ReadFloat(bytes, ref cursor) * value;
                int harmonics = ReadInt(bytes, ref cursor);
                float sineScale = ReadFloat(bytes, ref cursor), cosineScale = ReadFloat(bytes, ref cursor);
                if (harmonics < 1) throw new InvalidDataException("Scalar harmonic expression has no terms.");
                const float tau = 6.2831855f;
                if (MathF.Abs(angle) > tau) angle -= (int)(angle / tau) * tau;
                var (sine, cosine) = G1EffectShapeMath.SinCos(angle);
                float result = cosine * cosineScale + sine * sineScale, amplitude = 1;
                for (int term = 1; term < harmonics; term++)
                {
                    amplitude *= 0.5f;
                    sine = sine * cosine * 2f;
                    cosine = cosine * 2f * cosine - 1f;
                    result += (cosine * cosineScale + sine * sineScale) * amplitude;
                }
                return result;
            }
            default: throw new NotSupportedException($"Particle scalar AST opcode {opcode} is not implemented.");
        }
    }

    internal static int ReadInt(ReadOnlyMemory<byte> bytes, ref int cursor)
    {
        if (cursor < 0 || cursor > bytes.Length - 4)
            throw new InvalidDataException("Incomplete particle expression operand.");
        int result = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[cursor..]);
        cursor += 4; return result;
    }
    private static float ReadFloat(ReadOnlyMemory<byte> bytes, ref int cursor) =>
        BitConverter.Int32BitsToSingle(ReadInt(bytes, ref cursor));
}
