using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The controller dispatch used by the current sprite, mesh and nested
/// emitter particles. All writes target the actual mutable particle slot.</summary>
public static class G1EffectParticleControllers
{
    public static void Apply(ReadOnlyMemory<byte> controller, Memory<byte> particle,
        float dt, G1EffectContext context) => ApplyCore(controller, particle, dt, context, null);

    /// <summary>Type 27 reads the current generic's opacity binding and
    /// configuration. Supply its mutable declaration clone, not a static copy.</summary>
    public static void Apply(ReadOnlyMemory<byte> controller, Memory<byte> particle,
        float dt, G1EffectContext context, G1EffectRecord generic) =>
        ApplyCore(controller, particle, dt, context, generic);

    private static void ApplyCore(ReadOnlyMemory<byte> c, Memory<byte> p, float dt,
        G1EffectContext context, G1EffectRecord? generic)
    {
        switch (I(c, 4))
        {
            case 2:
            case 3:
            {
                int target = I(c, 8);
                float time = F(p, I(c, 0x10)), step = dt;
                if (I(c, 4) == 3)
                {
                    float inverse = 1f / F(p, I(c, 0x14));
                    time = inverse * time;
                    step = inverse * dt;
                }
                W(p, target, Scalar(c[0x20..], F(p, target), time, F(p, I(c, 0x18)), step));
                break;
            }
            case 4:
            {
                int cursor = 0x20;
                // This scalar AST's source context is the same current slot.
                W(p, I(c, 8), G1EffectBirthExpression.Evaluate(c, ref cursor, p, context.Random));
                break;
            }
            case 8:
                ApplyCore(Nested(c), p, dt, context, generic);
                Byte(p, I(c, 8), F(p, I(c, 0x18)) * 255f);
                break;
            case 13:
            {
                ApplyCore(Nested(c), p, dt, context, generic);
                int field = I(c, 0x18);
                float turns = F(p, field) / 6.2831855f + 0.5f;
                W(p, field, (turns - MathF.Floor(turns)) * 6.2831855f - 3.1415927f);
                break;
            }
            case 14:
            {
                float time = F(p, I(c, 0x10)), end = time + dt, sum = 0;
                int count = I(c, 0x1C);
                for (int index = 0; index < count; index++)
                {
                    float keyTime = F(c, checked(0x20 + index * 8));
                    if (end <= keyTime) break;
                    if (time <= keyTime) sum += F(c, checked(0x24 + index * 8));
                }
                W(p, I(c, 8), sum * F(p, I(c, 0x14)));
                break;
            }
            case 22:
            {
                int field = I(c, 0x18);
                float before = F(p, field);
                ApplyCore(Nested(c), p, dt, context, generic);
                float difference = F(p, field) - before;
                int matrix = I(c, 8);
                for (int lane = 0; lane < 4; lane++)
                {
                    int destination = checked(matrix + 0x30 + lane * 4);
                    W(p, destination, difference * F(p, checked(matrix + 0x10 + lane * 4)) + F(p, destination));
                }
                break;
            }
            case 27:
            {
                if (generic is null)
                    throw new InvalidOperationException("Particle opacity controller requires its current generic owner.");
                var source = generic.Bytes;
                int configuration = checked(0x18 + I(source, 0x18));
                float value = F(p, I(source, 0xBC)) * F(source, checked(configuration + 0x34));
                if ((I(c, 0xC) & 1) != 0) W(p, I(c, 8), value);
                else Byte(p, I(c, 8), value * 255f);
                break;
            }
            default:
                throw new NotSupportedException($"Particle controller {I(c, 4)} is not implemented.");
        }
    }

    public static bool CanContinue(ReadOnlyMemory<byte> controller, ReadOnlyMemory<byte> particle)
    {
        var c = controller;
        switch (I(c, 4))
        {
            case 2: return ScalarCanContinue(c[0x20..], F(particle, I(c, 0x10)));
            case 3:
                return ScalarCanContinue(c[0x20..],
                    (1f / F(particle, I(c, 0x14))) * F(particle, I(c, 0x10)));
            case 4: return true;
            case 8:
            case 13:
            case 22: return CanContinue(Nested(c), particle);
            case 14:
            {
                int count = I(c, 0x1C);
                if (count < 1) throw new InvalidDataException("Particle event controller has no final key.");
                return F(c, checked(0x20 + (count - 1) * 8)) >= F(particle, I(c, 0x10));
            }
            case 27: return false;
            default: throw new NotSupportedException($"Particle controller {I(c, 4)} lifetime is not implemented.");
        }
    }

    private static float Scalar(ReadOnlyMemory<byte> s, float current, float time, float scale, float dt)
    {
        switch (I(s, 4))
        {
            case 0:
                return (Clamp(time * F(s, 0x14) + F(s, 0x10)) * F(s, 0x1C) + F(s, 0x18)) * scale;
            case 1:
            {
                float elapsed = time - F(s, 0x10);
                if (elapsed <= dt)
                {
                    current = scale * F(s, 0x18);
                    dt = elapsed;
                    if (elapsed <= 0) return current;
                }
                float target = scale * F(s, 0x1C);
                return (current - target) / (dt * F(s, 0x14) + 1f) + target;
            }
            case 3:
            case 5:
            {
                float first = Clamp(time * F(s, 0x14) + F(s, 0x10));
                float second = Clamp(time * F(s, 0x1C) + F(s, 0x18));
                float value = first * F(s, 0x24) + F(s, 0x20);
                if (I(s, 4) == 5) second = (3f - (second + second)) * (second * second);
                return (value + second * F(s, 0x28)) * scale;
            }
            default: throw new NotSupportedException($"Particle scalar controller expression {I(s, 4)} is not implemented.");
        }
    }

    private static bool ScalarCanContinue(ReadOnlyMemory<byte> s, float time) => I(s, 4) switch
    {
        0 => 1f > time * F(s, 0x14) + F(s, 0x10) || F(s, 0x1C) + F(s, 0x18) > 0.0011920928955078125f,
        1 => 8f / F(s, 0x14) + F(s, 0x10) > time || F(s, 0x1C) > 0.0011920928955078125f,
        3 or 5 => 1f > time * F(s, 0x1C) + F(s, 0x18) ||
            (F(s, 0x24) + F(s, 0x20)) + F(s, 0x28) > 0.0011920928955078125f,
        _ => throw new NotSupportedException($"Particle scalar controller expression {I(s, 4)} lifetime is not implemented.")
    };

    private static ReadOnlyMemory<byte> Nested(ReadOnlyMemory<byte> c)
    {
        int length = I(c, 0x10);
        if (length < 16 || length > c.Length - 0x10)
            throw new InvalidDataException("Nested particle controller escapes its declaration.");
        return c.Slice(0x10, length);
    }

    private static float Clamp(float value) => MathF.Min(1f, MathF.Max(0f, value));
    private static int I(ReadOnlyMemory<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(Range(bytes, offset, 4));
    private static float F(ReadOnlyMemory<byte> bytes, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(Range(bytes, offset, 4));
    private static void W(Memory<byte> bytes, int offset, float value)
    {
        _ = Range(bytes, offset, 4);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.Span[offset..], value);
    }
    private static void Byte(Memory<byte> bytes, int offset, float value)
    {
        _ = Range(bytes, offset, 1);
        bytes.Span[offset] = unchecked((byte)(long)value);
    }
    private static ReadOnlySpan<byte> Range(ReadOnlyMemory<byte> bytes, int offset, int count)
    {
        if (offset < 0 || offset > bytes.Length - count)
            throw new InvalidDataException("Particle controller field escapes its owning declaration or slot.");
        return bytes.Span.Slice(offset, count);
    }
}
