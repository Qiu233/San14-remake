using System.Buffers.Binary;

namespace San14.Data;

/// <summary>G1FX scalar type 0: a clamped linear transition. Its declaration is
/// already its complete evaluation data; the native initialization slot is a
/// no-op. This is distinct from the particle-birth expression bytecode.</summary>
public sealed record G1EffectScalarLinear(float TimeOffset, float TimeScale,
    float ValueOffset, float ValueScale)
{
    public static G1EffectScalarLinear Read(ReadOnlyMemory<byte> declaration)
    {
        if (declaration.Length < 0x20 ||
            BinaryPrimitives.ReadUInt32LittleEndian(declaration.Span) != 0x20 ||
            BinaryPrimitives.ReadUInt32LittleEndian(declaration.Span.Slice(4, 4)) != 0)
            throw new NotSupportedException("G1FX scalar declaration is not a complete linear type 0.");
        float F(int at) => BinaryPrimitives.ReadSingleLittleEndian(declaration.Span.Slice(at, 4));
        return new(F(0x10), F(0x14), F(0x18), F(0x1C));
    }

    public float Evaluate(float time, float scale)
    {
        float phase = NativeFloat.Add(NativeFloat.Multiply(time, TimeScale), TimeOffset);
        float bounded = MathF.Min(1, MathF.Max(0, phase));
        return NativeFloat.Multiply(
            NativeFloat.Add(NativeFloat.Multiply(bounded, ValueScale), ValueOffset), scale);
    }

    public bool CanContinue(float time)
    {
        float phase = NativeFloat.Add(NativeFloat.Multiply(time, TimeScale), TimeOffset);
        return 1 > phase || NativeFloat.Add(ValueScale, ValueOffset) > 0.0011920928955078125f;
    }
}
