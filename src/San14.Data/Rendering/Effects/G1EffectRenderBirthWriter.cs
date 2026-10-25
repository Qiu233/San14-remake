using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Source-owned inputs to one render birth instruction. Expressions are
/// deferred so their random draws occur at their original instruction position.
/// Color channels are requested in R, G, B, A order.</summary>
public sealed record G1EffectRenderBirthContext(int MaterialBase, uint MaterialRange,
    uint UvStart, uint UvRange, Func<int, float> ColorChannel,
    Func<float>? MeshPhaseMultiplier = null);

/// <summary>Initializes the installed billboard, oriented and mesh render payloads.
/// It consumes normal G1FX declarations and the real effect random stream; it
/// neither acquires a pool slot nor substitutes a captured GPU instance stream.</summary>
public static class G1EffectRenderBirthWriter
{
    public static void Write(int variant, G1EffectParticleDeclaration declaration,
        Memory<byte> particle, G1EffectRandom random, G1EffectRenderBirthContext context)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(context);
        if (variant == 9) return; // The installed dispatch entry is an empty operation.
        int stateAt = variant switch
        {
            0 => 0x60,
            1 => 0x90,
            6 => 0xA0,
            _ => throw new NotSupportedException($"Render birth variant {variant} is not implemented.")
        };
        ReadOnlySpan<byte> layout = declaration.Layout.Span;
        Span<byte> target = particle.Span;
        Require(layout.Length, 0x20, 0x20);
        Require(target.Length, stateAt, 16);
        int material = context.MaterialBase;
        if (context.MaterialRange != 0)
            material = unchecked(material + (int)((random.NextUInt() >> 16) % context.MaterialRange));
        layout.Slice(0x20, 16).CopyTo(target.Slice(0x20, 16));
        BinaryPrimitives.WriteInt16LittleEndian(target[0x2C..],
            unchecked((short)(material + S(layout, 0x2C))));
        uint start = context.UvStart;
        if (context.UvRange != 0)
            start = unchecked(start + (random.NextUInt() >> 16) % context.UvRange);
        ReadOnlySpan<byte> expression = G1EffectUvAnimation.Read(declaration).Span;
        Span<byte> state = target.Slice(stateAt, 16);
        PutU(state, 12, 0);
        G1EffectUvAnimation.Initialize(expression, state, start);
        G1EffectUvAnimation.Evaluate(expression, target.Slice(0x40, 16),
            unchecked((uint)(long)F(state, 4)), F(state, 12));
        // The expression getters can consume randomness and must stay R/G/B/A.
        float red = context.ColorChannel(0) * layout[0x32];
        float green = context.ColorChannel(1) * layout[0x31];
        float blue = context.ColorChannel(2) * layout[0x30];
        float alpha = context.ColorChannel(3) * layout[0x33];
        target[0x32] = Channel(red); target[0x31] = Channel(green);
        target[0x30] = Channel(blue); target[0x33] = Channel(alpha);
        if (variant == 0 && S(target, 0x20) == 0)
            layout.Slice(0x3C, 4).CopyTo(target.Slice(0x3C, 4));
        if (variant == 6) InitializeMesh(declaration, target, random, context);
    }

    private static void InitializeMesh(G1EffectParticleDeclaration declaration, Span<byte> target,
        G1EffectRandom random, G1EffectRenderBirthContext context)
    {
        ReadOnlySpan<byte> layout = declaration.Layout.Span, record = declaration.Record.Bytes.Span;
        Require(layout.Length, 0x98, 4);
        int shader = target[0x2A], first = shader - 0x96, second = shader - 0xAA;
        bool encoded = first is >= 0 and < 18 && (0x2F8CFu >> first & 1) != 0 ||
            second is >= 0 and < 64 && (0xCDB300B330CC92C3ul >> second & 1) != 0;
        if (encoded)
        {
            float phase;
            if ((declaration.Flags & (1u << 17)) == 0)
            {
                if (context.MeshPhaseMultiplier is null)
                    throw new InvalidOperationException("Mesh birth requires its source phase expression.");
                phase = context.MeshPhaseMultiplier() * F(layout, 0x98);
            }
            else phase = F(layout, 0x98);
            (double fraction, int exponent) = FractionAndExponent(phase);
            target[0x2F] = unchecked((byte)(long)(((float)fraction + (float)fraction - 1f) * 255f));
            PutI(target, 0x98, unchecked(exponent + 0x80));
        }
        else layout.Slice(0x98, 4).CopyTo(target.Slice(0x98, 4));
        int mode = S(record, 0x240);
        if (mode is not (1 or 3 or 5)) return;
        uint count = H(record, 0x242);
        float sample = random.NextUnit();
        int table = checked(0x244 + I(record, 0x244));
        if (mode == 1)
        {
            uint index = unchecked(count - 1);
            for (uint visited = 0; visited < count; visited++)
            {
                uint candidate = count - visited - 1;
                float weight = F(record, checked(table + (int)candidate * 4));
                if (sample < weight) { index = candidate; break; }
                sample -= weight;
            }
            PutU(target, 0x9C, index);
        }
        else
        {
            int choice = unchecked((int)((float)count * sample));
            PutU(target, 0x9C, choice == 0 ? 0 : U(record, checked(table + (choice - 1) * 4)));
        }
    }

    private static (double Fraction, int Exponent) FractionAndExponent(double value)
    {
        if (value == 0) return (value, 0);
        if (!double.IsFinite(value)) return (value, -1);
        int exponent = Math.ILogB(value) + 1;
        return (Math.ScaleB(value, -exponent), exponent);
    }
    private static byte Channel(float value) => value < 255f
        ? unchecked((byte)(long)(value >= 0 ? value : 0f)) : (byte)255;
    private static void Require(int size, int at, int length)
    {
        if (at < 0 || length < 0 || at > size - length)
            throw new InvalidDataException("Render birth escapes its declaration or particle slot.");
    }
    private static uint U(ReadOnlySpan<byte> bytes, int at)
    { Require(bytes.Length, at, 4); return BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]); }
    private static int I(ReadOnlySpan<byte> bytes, int at) => unchecked((int)U(bytes, at));
    private static short S(ReadOnlySpan<byte> bytes, int at)
    { Require(bytes.Length, at, 2); return BinaryPrimitives.ReadInt16LittleEndian(bytes[at..]); }
    private static ushort H(ReadOnlySpan<byte> bytes, int at) => unchecked((ushort)S(bytes, at));
    private static float F(ReadOnlySpan<byte> bytes, int at)
    { Require(bytes.Length, at, 4); return BinaryPrimitives.ReadSingleLittleEndian(bytes[at..]); }
    private static void PutU(Span<byte> bytes, int at, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], value);
    private static void PutI(Span<byte> bytes, int at, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(bytes[at..], value);
}
