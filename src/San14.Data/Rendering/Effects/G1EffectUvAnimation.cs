using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Installed UV animation declarations and their four-word live state.
/// The owner supplies elapsed time and lifetime; this consumer neither advances
/// an effect clock nor draws from the effect random stream.</summary>
public static class G1EffectUvAnimation
{
    public static ReadOnlyMemory<byte> Read(G1EffectParticleDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return Relative(declaration.Record.Bytes, 0x200);
    }

    public static void Initialize(ReadOnlySpan<byte> expression, Span<byte> state, uint start)
    {
        Require(state.Length, 0, 16);
        uint type = U(expression, 0), count = Count(expression);
        uint frame = type switch
        {
            0 or 1 or 2 or 4 => start % count,
            3 => unchecked((uint)(long)((float)start * F(expression, 8) %
                ((float)count * F(expression, 8)))),
            _ => throw Unsupported(type)
        };
        PutU(state, 0, frame);
        PutF(state, 4, frame);
    }

    /// <summary>Writes the selected rectangle, including type four's independent
    /// U/V scrolling. Frame conversion belongs to the caller's real live state.</summary>
    public static void Evaluate(ReadOnlySpan<byte> expression, Span<byte> rectangle,
        uint frame, float elapsed)
    {
        Require(rectangle.Length, 0, 16);
        uint type = U(expression, 0);
        _ = Count(expression);
        uint index = type switch
        {
            0 or 1 or 2 or 4 => frame,
            3 => unchecked((uint)(long)((float)frame / F(expression, 8))),
            _ => throw Unsupported(type)
        };
        int at = checked(16 + checked((int)index) * 16);
        Require(expression.Length, at, 16);
        expression.Slice(at, 16).CopyTo(rectangle);
        if (type != 4) return;
        float left = F(rectangle, 0), top = F(rectangle, 4),
            right = F(rectangle, 8), bottom = F(rectangle, 12);
        float u = (S(expression, 8) * elapsed * 0.0009765625f) %
            MathF.Ceiling(MathF.Abs(right - left));
        float v = (S(expression, 10) * elapsed * 0.0009765625f) %
            MathF.Ceiling(MathF.Abs(bottom - top));
        PutF(rectangle, 0, left + u); PutF(rectangle, 4, top + v);
        PutF(rectangle, 8, right + u); PutF(rectangle, 12, bottom + v);
    }

    /// <summary>Updates only the current-frame word. State words two and three
    /// are the owner's lifetime and elapsed time. Type four has no update entry
    /// in this dispatch family and is evaluated directly with its elapsed time.</summary>
    public static void Update(ReadOnlySpan<byte> expression, Span<byte> state)
    {
        Require(state.Length, 0, 16);
        uint type = U(expression, 0), count = Count(expression);
        float value;
        switch (type)
        {
            case 0: value = (F(state, 12) * F(expression, 8) + U(state, 0)) % count; break;
            case 1: value = (F(state, 12) * F(expression, 8) / F(state, 8) + U(state, 0)) % count; break;
            case 2: value = (F(state, 4) + 1f) % ((float)count * F(expression, 8)); break;
            case 3: return;
            default: throw Unsupported(type);
        }
        PutF(state, 4, value);
    }

    public static uint WrapFrame(ReadOnlySpan<byte> expression, uint frame)
    {
        uint count = Count(expression), type = U(expression, 0);
        return type switch
        {
            0 or 1 or 2 or 4 => frame % count,
            3 => unchecked((uint)(long)((float)(frame % count) * F(expression, 8))),
            _ => throw Unsupported(type)
        };
    }

    public static float FrameAtTime(ReadOnlySpan<byte> expression, uint initialFrame,
        float currentFrame, float elapsed, float lifetime)
    {
        uint type = U(expression, 0), count = Count(expression);
        return type switch
        {
            0 => (elapsed * F(expression, 8) + initialFrame) % count,
            1 => (elapsed * F(expression, 8) / lifetime + initialFrame) % count,
            2 => (currentFrame + 1f) % ((float)count * F(expression, 8)),
            3 => currentFrame,
            _ => throw Unsupported(type)
        };
    }

    /// <summary>Initializes a standalone UV controller's rectangle and two frame
    /// words. The returned declaration is its actual managed binding, replacing
    /// the original process pointer without storing an address in product state.</summary>
    public static ReadOnlyMemory<byte> InitializeController(ReadOnlyMemory<byte> declaration,
        Span<byte> instance, float parameter, uint sampledStart)
    {
        Require(instance.Length, 0, 0x28);
        ReadOnlyMemory<byte> expression = Relative(declaration, 0x28);
        Span<byte> state = stackalloc byte[16];
        state.Clear();
        uint previous = U(instance, 0x20);
        PutU(state, 0, previous); PutF(state, 4, previous);
        PutF(state, 8, parameter);
        Initialize(expression.Span, state, sampledStart);
        Evaluate(expression.Span, instance.Slice(0x10, 16),
            unchecked((uint)(long)F(state, 4)), F(state, 12));
        PutF(instance, 0x24, F(state, 4)); PutU(instance, 0x20, U(state, 0));
        return expression;
    }

    private static ReadOnlyMemory<byte> Relative(ReadOnlyMemory<byte> bytes, int slot)
    {
        Require(bytes.Length, slot, 4);
        int at = checked(slot + BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[slot..]));
        Require(bytes.Length, at, 16);
        return bytes[at..];
    }
    private static uint Count(ReadOnlySpan<byte> expression)
    {
        uint count = U(expression, 12);
        if (count == 0) throw new InvalidDataException("UV animation has no frames.");
        return count;
    }
    private static NotSupportedException Unsupported(uint type) =>
        new($"UV animation dispatch {type} is not implemented for this operation.");
    private static void Require(int size, int at, int length)
    {
        if (at < 0 || length < 0 || at > size - length)
            throw new InvalidDataException("UV animation escapes its resource or live state.");
    }
    private static uint U(ReadOnlySpan<byte> bytes, int at)
    { Require(bytes.Length, at, 4); return BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]); }
    private static short S(ReadOnlySpan<byte> bytes, int at)
    { Require(bytes.Length, at, 2); return BinaryPrimitives.ReadInt16LittleEndian(bytes[at..]); }
    private static float F(ReadOnlySpan<byte> bytes, int at)
    { Require(bytes.Length, at, 4); return BinaryPrimitives.ReadSingleLittleEndian(bytes[at..]); }
    private static void PutU(Span<byte> bytes, int at, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], value);
    private static void PutF(Span<byte> bytes, int at, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(bytes[at..], value);
}
