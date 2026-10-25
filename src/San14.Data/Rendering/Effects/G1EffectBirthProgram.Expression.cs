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
        ReadOnlyMemory<byte> sourceContext, G1EffectRandom random)
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
            case 12:
            {
                float left = Evaluate(bytes, ref cursor, sourceContext, random);
                return left - Evaluate(bytes, ref cursor, sourceContext, random);
            }
            case 13:
            {
                float left = Evaluate(bytes, ref cursor, sourceContext, random);
                return Evaluate(bytes, ref cursor, sourceContext, random) * left;
            }
            case 14:
            {
                float value = Evaluate(bytes, ref cursor, sourceContext, random);
                return ReadFloat(bytes, ref cursor) * value;
            }
            case 15:
            {
                float numerator = Evaluate(bytes, ref cursor, sourceContext, random);
                float denominator = Evaluate(bytes, ref cursor, sourceContext, random);
                if (denominator == 0f) denominator = 1.1754944e-38f;
                return numerator / denominator;
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
