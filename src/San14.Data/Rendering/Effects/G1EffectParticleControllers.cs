using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The controller dispatch used by the current sprite, mesh and nested
/// emitter particles. All writes target the actual mutable particle slot.</summary>
public static class G1EffectParticleControllers
{
    public static void Apply(ReadOnlyMemory<byte> controller, Memory<byte> particle,
        float dt, G1EffectContext context) => ApplyCore(controller, particle, dt, context, null, null);

    /// <summary>Type 27 reads the current generic's opacity binding and
    /// configuration. Supply its mutable declaration clone, not a static copy.</summary>
    public static void Apply(ReadOnlyMemory<byte> controller, Memory<byte> particle,
        float dt, G1EffectContext context, G1EffectRecord generic) =>
        ApplyCore(controller, particle, dt, context, generic, null);

    public static void Apply(ReadOnlyMemory<byte> controller, Memory<byte> particle,
        float dt, G1EffectContext context, G1EffectRecord generic, float? moduleStrength) =>
        ApplyCore(controller, particle, dt, context, generic, moduleStrength);

    public static void Apply(ReadOnlyMemory<byte> controller, Memory<byte> particle,
        float dt, G1EffectContext context, G1EffectRecord generic, float? moduleStrength,
        float moduleOpacity) => ApplyCore(controller, particle, dt, context, generic, moduleStrength, moduleOpacity);

    private static void ApplyCore(ReadOnlyMemory<byte> c, Memory<byte> p, float dt,
        G1EffectContext context, G1EffectRecord? generic, float? moduleStrength, float? moduleOpacity = null)
    {
        switch (I(c, 4))
        {
            case 0:
            {
                int target = I(c, 8);
                W(p, target, Scalar(c[0x20..], F(p, target),
                    F(p, I(c, 0x10)), 1f, dt));
                break;
            }
            case 1:
            {
                // Normalized time, without the baseline scale used by type 3.
                int target = I(c, 8);
                float inverse = 1f / F(p, I(c, 0x14));
                W(p, target, Scalar(c[0x20..], F(p, target),
                    inverse * F(p, I(c, 0x10)), 1f, inverse * dt));
                break;
            }
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
                // Native dispatch reads the opcode at 1C and passes an operand
                // cursor at 20. The shared prefix evaluator consumes both.
                int cursor = 0x1C;
                W(p, I(c, 8), G1EffectBirthExpression.Evaluate(c, ref cursor, p, context.Random, context));
                break;
            }
            case 7:
            case 8:
                ApplyCore(Nested(c), p, dt, context, generic, moduleStrength, moduleOpacity);
                Byte(p, I(c, 8), F(p, I(c, 0x18)) * 255f);
                break;
            case 11:
            case 12:
            {
                int target = I(c, 8);
                float baseline = F(p, I(c, 0x18));
                float normalized = F(p, target) / baseline;
                float time = F(p, I(c, 0x10)), period = F(c, 0x1C);
                if (I(c, 4) == 12)
                {
                    float inverse = 1f / F(p, I(c, 0x14));
                    time = inverse * time;
                    dt = inverse * dt;
                }
                time -= MathF.Floor(time / period) * period;
                W(p, target, Scalar(c[0x20..], normalized, time, 1f, dt) * baseline);
                break;
            }
            case 13:
            {
                ApplyCore(Nested(c), p, dt, context, generic, moduleStrength, moduleOpacity);
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
            case 16:
            {
                float period = F(c, 0x18);
                float time = F(p, I(c, 0x10));
                time -= MathF.Floor(time / period) * period;
                float end = time + dt, sum = 0;
                int count = I(c, 0x1C);
                for (int index = 0; index < count; index++)
                {
                    float key = F(c, checked(0x20 + index * 8));
                    if (end <= key) break;
                    if (time <= key) sum += F(c, checked(0x24 + index * 8));
                }
                // The source processes only the one wrapped tail, even when
                // a long update crosses more than one declared period.
                if (period < end)
                {
                    end -= period;
                    for (int index = 0; index < count; index++)
                    {
                        float key = F(c, checked(0x20 + index * 8));
                        if (end <= key) break;
                        if (0 <= key) sum += F(c, checked(0x24 + index * 8));
                    }
                }
                W(p, I(c, 8), sum * F(p, I(c, 0x14)));
                break;
            }
            case 21:
            case 22:
            {
                int field = I(c, 0x18);
                float before = F(p, field);
                ApplyCore(Nested(c), p, dt, context, generic, moduleStrength, moduleOpacity);
                float difference = F(p, field) - before;
                int matrix = I(c, 8);
                for (int lane = 0; lane < 4; lane++)
                {
                    int destination = checked(matrix + 0x30 + lane * 4);
                    int axis = (I(c, 4) - 21) * 0x10;
                    W(p, destination, difference * F(p, checked(matrix + axis + lane * 4)) + F(p, destination));
                }
                break;
            }
            case 34:
            case 36:
            {
                if (generic is null || moduleStrength is null)
                    throw new InvalidOperationException("Particle secondary opacity controller requires its current owner.");
                float value = F(p, I(generic.Bytes, 0xBC));
                if (I(c, 4) == 36)
                    value *= moduleOpacity ?? throw new InvalidOperationException("Particle opacity controller requires its module declaration opacity.");
                value *= moduleStrength.Value;
                if ((I(c, 0xC) & 1) != 0) W(p, I(c, 8), value);
                else Byte(p, I(c, 8), value * 255f);
                break;
            }
            case 27:
            {
                if (generic is null)
                    throw new InvalidOperationException("Particle opacity controller requires its current generic owner.");
                var source = generic.Bytes;
                float strength = moduleStrength ??
                    throw new InvalidOperationException("Particle opacity controller requires its current module strength.");
                float value = F(p, I(source, 0xBC)) * strength;
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
            case 0: return ScalarCanContinue(c[0x20..], F(particle, I(c, 0x10)));
            case 1:
                return ScalarCanContinue(c[0x20..],
                    (1f / F(particle, I(c, 0x14))) * F(particle, I(c, 0x10)));
            case 2: return ScalarCanContinue(c[0x20..], F(particle, I(c, 0x10)));
            case 3:
                return ScalarCanContinue(c[0x20..],
                    (1f / F(particle, I(c, 0x14))) * F(particle, I(c, 0x10)));
            case 4: return true;
            case 11: return true;
            case 12: return true;
            case 7:
            case 8:
            case 13:
            case 21:
            case 22: return CanContinue(Nested(c), particle);
            case 16: return true;
            case 14:
            {
                int count = I(c, 0x1C);
                if (count < 1) throw new InvalidDataException("Particle event controller has no final key.");
                return F(c, checked(0x20 + (count - 1) * 8)) >= F(particle, I(c, 0x10));
            }
            case 27:
            case 34:
            case 36: return false;
            default: throw new NotSupportedException($"Particle controller {I(c, 4)} lifetime is not implemented.");
        }
    }

    private static float Scalar(ReadOnlyMemory<byte> s, float current, float time, float scale, float dt)
    {
        switch (I(s, 4))
        {
            case 0:
            case 2:
            {
                float phase = Clamp(time * F(s, 0x14) + F(s, 0x10));
                if (I(s, 4) == 2) phase = Smooth(phase);
                return (phase * F(s, 0x1C) + F(s, 0x18)) * scale;
            }
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
            case 6:
            case 8:
            {
                float elapsed = time - F(s, 0x10);
                if (elapsed <= dt)
                {
                    current = scale * F(s, 0x20);
                    dt = elapsed;
                    if (elapsed <= 0) return current;
                }
                float phase = time * F(s, 0x1C) + F(s, 0x18);
                if (phase > 0)
                {
                    phase = Clamp(phase);
                    if (I(s, 4) == 8) phase = Smooth(phase);
                    return (phase * F(s, 0x28) + F(s, 0x24)) * scale;
                }
                float target = F(s, 0x24) * scale;
                return (current - target) / (F(s, 0x14) * dt + 1f) + target;
            }
            case 4:
            {
                float elapsed = time - F(s, 0x18);
                if (elapsed <= dt)
                {
                    current = (Clamp(time * F(s, 0x14) + F(s, 0x10)) * F(s, 0x24) + F(s, 0x20)) * scale;
                    dt = elapsed;
                    if (elapsed <= 0) return current;
                }
                float target = F(s, 0x28) * scale;
                return (current - target) / (F(s, 0x1C) * dt + 1f) + target;
            }
            case 7:
            {
                float elapsed = time - F(s, 0x10);
                if (elapsed <= dt)
                {
                    current = scale * F(s, 0x20);
                    if (elapsed <= 0) return current;
                    dt = elapsed;
                }
                float secondElapsed = time - F(s, 0x18);
                if (secondElapsed <= dt)
                {
                    float middle = F(s, 0x24) * scale;
                    current = (current - middle) / (F(s, 0x14) * dt + 1f) + middle;
                    if (secondElapsed <= 0) return current;
                    dt = secondElapsed;
                }
                float target = F(s, 0x28) * scale;
                return (current - target) / (F(s, 0x1C) * dt + 1f) + target;
            }
            case 3:
            case 5:
            case 9:
            case 11:
            {
                float first = Clamp(time * F(s, 0x14) + F(s, 0x10));
                float second = Clamp(time * F(s, 0x1C) + F(s, 0x18));
                // Type 11 smooths both clamped transitions; type 5 smooths
                // only the second. Both have an empty initialization slot.
                if (I(s, 4) is 9 or 11) first = Smooth(first);
                float value = first * F(s, 0x24) + F(s, 0x20);
                if (I(s, 4) is 5 or 11) second = Smooth(second);
                float secondValue = second * F(s, 0x28);
                return (I(s, 4) == 9 ? secondValue + value : value + secondValue) * scale;
            }
            default: throw new NotSupportedException($"Particle scalar controller expression {I(s, 4)} is not implemented.");
        }
    }

    private static bool ScalarCanContinue(ReadOnlyMemory<byte> s, float time) => I(s, 4) switch
    {
        0 or 2 => 1f > time * F(s, 0x14) + F(s, 0x10) || F(s, 0x1C) + F(s, 0x18) > 0.0011920928955078125f,
        1 => 8f / F(s, 0x14) + F(s, 0x10) > time || F(s, 0x1C) > 0.0011920928955078125f,
        4 or 7 => 8f / F(s, 0x1C) + F(s, 0x18) > time || F(s, 0x28) > 0.0011920928955078125f,
        3 or 5 or 6 or 8 or 9 or 11 => 1f > time * F(s, 0x1C) + F(s, 0x18) ||
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
    private static float Smooth(float value) => (3f - (value + value)) * (value * value);
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
