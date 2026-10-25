using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class G1EffectMotionRuntime
{
    private Vector4 ExpressionVector(ReadOnlyMemory<byte> bytes, ref int cursor,
        G1EffectRecord generic, Memory<byte> particle, ReadOnlyMemory<byte> source)
    {
        int opcode = Word(bytes, ref cursor);
        switch (opcode)
        {
            case 0: return ExpressionVector(bytes, ref cursor, generic, particle, source);
            case 1:
            {
                int field = cursor, at = checked(field + Word(bytes, ref cursor));
                return V(bytes, at);
            }
            case 2: return V(source, Word(bytes, ref cursor));
            case 3:
            {
                Vector4 value = ExpressionVector(bytes, ref cursor, generic, particle, source);
                float inverse = 1f / MathF.Sqrt(value.X * value.X + value.Y * value.Y + value.Z * value.Z);
                return new(value.X * inverse, value.Y * inverse, value.Z * inverse, value.W);
            }
            case 8:
            {
                Vector4 value = ExpressionVector(bytes, ref cursor, generic, particle, source);
                float scale = ExpressionScalar(bytes, ref cursor, generic, particle, source);
                return new(scale * value.X, scale * value.Y, scale * value.Z, scale * value.W);
            }
            case 9:
            {
                Vector4 value = ExpressionVector(bytes, ref cursor, generic, particle, source);
                float scale = BitConverter.Int32BitsToSingle(Word(bytes, ref cursor));
                return new(scale * value.X, scale * value.Y, scale * value.Z, scale * value.W);
            }
            case 39:
            {
                int pointerField = Word(bytes, ref cursor);
                if (pointerField != I(generic.Bytes, 0x8C))
                    throw new NotSupportedException("Particle force vector references an unbound indirect state owner.");
                return ExpressionVector(bytes, ref cursor, generic, particle, Parent(generic, particle));
            }
            default: throw new NotSupportedException($"Particle force vector expression {opcode} is not implemented.");
        }
    }

    private float ExpressionScalar(ReadOnlyMemory<byte> bytes, ref int cursor,
        G1EffectRecord generic, Memory<byte> particle, ReadOnlyMemory<byte> source)
    {
        int opcode = Word(bytes, ref cursor);
        switch (opcode)
        {
            case 0: return BitConverter.Int32BitsToSingle(Word(bytes, ref cursor));
            case 1: return F(source, Word(bytes, ref cursor));
            case 2:
            {
                float center = BitConverter.Int32BitsToSingle(Word(bytes, ref cursor)),
                    spread = BitConverter.Int32BitsToSingle(Word(bytes, ref cursor));
                return context.Random.NextSigned() * spread + center;
            }
            case 8:
            {
                float value = ExpressionScalar(bytes, ref cursor, generic, particle, source);
                float divisor = MathF.Abs(ExpressionScalar(bytes, ref cursor, generic, particle, source));
                value -= unchecked((int)(value / divisor)) * divisor;
                return value < 0 ? value + divisor : value;
            }
            case 13:
            {
                float left = ExpressionScalar(bytes, ref cursor, generic, particle, source);
                return ExpressionScalar(bytes, ref cursor, generic, particle, source) * left;
            }
            case 14:
            {
                float value = ExpressionScalar(bytes, ref cursor, generic, particle, source);
                return BitConverter.Int32BitsToSingle(Word(bytes, ref cursor)) * value;
            }
            case 15:
            {
                float numerator = ExpressionScalar(bytes, ref cursor, generic, particle, source),
                    denominator = ExpressionScalar(bytes, ref cursor, generic, particle, source);
                return numerator / (denominator == 0 ? 1.1754944e-38f : denominator);
            }
            case 39:
            {
                Vector4 value = ExpressionVector(bytes, ref cursor, generic, particle, source);
                return MathF.Sqrt(value.X * value.X + value.Y * value.Y + value.Z * value.Z);
            }
            case 47:
            {
                int pointerField = Word(bytes, ref cursor);
                if (pointerField != I(generic.Bytes, 0x8C))
                    throw new NotSupportedException("Particle force expression references an unbound indirect state owner.");
                return ExpressionScalar(bytes, ref cursor, generic, particle, Parent(generic, particle));
            }
            default: throw new NotSupportedException($"Particle force scalar expression {opcode} is not implemented.");
        }
    }

    private static int Word(ReadOnlyMemory<byte> bytes, ref int cursor)
    { int value = I(bytes, cursor); cursor = checked(cursor + 4); return value; }
}
