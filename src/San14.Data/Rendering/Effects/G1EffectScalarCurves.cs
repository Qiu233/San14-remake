using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The resource scalar AST31 table shared by birth and motion.
/// The caller evaluates its child once before consuming this complete table.</summary>
internal static class G1EffectScalarCurves
{
    internal static float Cubic(ReadOnlyMemory<byte> bytes, ref int cursor, float value)
    {
        int table = cursor;
        if (table < 0 || table > bytes.Length - 4)
            throw new InvalidDataException("Incomplete particle cubic curve.");
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[table..]);
        if (count < 1) throw new InvalidDataException("Particle cubic curve has no intervals.");
        int length = checked((5 * count + 3) * 4);
        if (table > bytes.Length - length)
            throw new InvalidDataException("Incomplete particle cubic curve.");
        // Even a clamped endpoint consumes all encoded intervals and terminalY.
        cursor = checked(table + length);
        float F(int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Span[at..]);
        float start = F(table + 4);
        if (value < start) return F(table + 12);
        for (int index = 0; index < count; index++)
        {
            int at = checked(table + 8 + index * 20);
            float end = F(at);
            if (value < end)
            {
                float time = NativeFloat.Subtract(value, start);
                float square = NativeFloat.Multiply(time, time);
                float linear = NativeFloat.Add(F(at + 4), NativeFloat.Multiply(time, F(at + 8)));
                float cubic = NativeFloat.Multiply(NativeFloat.Multiply(square, F(at + 16)), time);
                return NativeFloat.Add(NativeFloat.Add(NativeFloat.Multiply(square, F(at + 12)), linear), cubic);
            }
            start = end;
        }
        return F(checked(table + 8 + count * 20));
    }
}
