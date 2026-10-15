using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>CBC770's mode6/10B2=8 continuation, after its Camera color,
/// quaternion and fog copies. Does not classify lighting or publish a View.</summary>
public static class CameraDirectionalLightColorMixing
{
    public static byte[] ApplyPreparedMode6ThirdColor(ReadOnlySpan<byte> completePreparedLightData,
        ReadOnlySpan<byte> completePreparedCamera)
    {
        if (completePreparedLightData.Length != 0x10C0 || completePreparedCamera.Length != 0x3C0)
            throw new InvalidDataException("Third-light color requires complete prepared LightData and Camera snapshots.");
        if (completePreparedLightData[0x10B0] != 6 || completePreparedLightData[0x10B2] != 8)
            throw new NotSupportedException("This original color continuation requires mode6 and third-light selector8.");
        Vector4 color = ApplyPreparedMode6ThirdColor(
            ReadVector(completePreparedLightData, 0x10), ReadVector(completePreparedLightData, 0x50),
            Read(completePreparedCamera, 0x1F0), Read(completePreparedCamera, 0x1F4));
        byte[] result = completePreparedLightData.ToArray();
        Write(result, 0x50, color.X); Write(result, 0x54, color.Y);
        Write(result, 0x58, color.Z); Write(result, 0x5C, color.W);
        return result;
    }

    /// <summary>Current third-light color, preserving the fourth copied word
    /// according to the actual zero, direct-replacement and HSV branches.</summary>
    public static Vector4 ApplyPreparedMode6ThirdColor(
        Vector4 firstColor, Vector4 thirdColor, float intensity, float mix)
    {
        // COMISS/JBE also takes this branch for an unordered intensity.
        if (!(intensity > 0)) return new(0, 0, 0, thirdColor.W);
        // MAXSS(mix,+0) selects the second operand on equal or unordered.
        float weight = mix > 0 ? mix : 0;
        Rgb first = ReadRgb(firstColor);
        Rgb third = ReadRgb(thirdColor);
        if (weight >= 1 || (third.R == 0 && third.G == 0 && third.B == 0))
            return new(Mul(intensity, first.R), Mul(intensity, first.G),
                Mul(intensity, first.B), firstColor.W);
        Hsv a = ToHsv(first), b = ToHsv(third);
        float retained = Sub(1, weight);
        Hsv mixed = new(Add(Mul(a.H, weight), Mul(retained, b.H)),
            Add(Mul(a.S, weight), Mul(retained, b.S)), Mul(a.V, intensity));
        Rgb color = ToRgb(mixed);
        return new(color.R, color.G, color.B, thirdColor.W);
    }

    private readonly record struct Rgb(float R, float G, float B);
    private readonly record struct Hsv(float H, float S, float V);
    private static Hsv ToHsv(Rgb c)
    {
        float max, delta, hue = 0;
        if (c.G > c.R)
        {
            if (c.B > c.G)
            {
                max = c.B; delta = Sub(c.B, c.R);
                if (delta > 0) hue = Add(Div(Mul(Sub(c.R, c.G), 60), delta), 240);
            }
            else
            {
                max = c.G; delta = Sub(c.G, c.B > c.R ? c.R : c.B);
                if (delta > 0) hue = Add(Div(Mul(Sub(c.B, c.R), 60), delta), 120);
            }
        }
        else if (c.B > c.R)
        {
            max = c.B; delta = Sub(c.B, c.G);
            if (delta > 0) hue = Add(Div(Mul(Sub(c.R, c.G), 60), delta), 240);
        }
        else
        {
            max = c.R; delta = Sub(c.R, c.B > c.G ? c.G : c.B);
            if (delta > 0) hue = Div(Mul(Sub(c.G, c.B), 60), delta);
        }
        if (hue < 0) hue = Add(hue, 360);
        else if (hue > 360) hue = Add(hue, -360);
        return new(Div(hue, 360), max == 0 ? 0 : Div(delta, max), max);
    }

    private static Rgb ToRgb(Hsv c)
    {
        Finite(c.H); Finite(c.S); Finite(c.V);
        if (c.S == 0) return new(c.V, c.V, c.V);
        float sectors = Mul(c.H, 6);
        // The source truncates to64 bits, uses low32 unsigned division by6,
        // and obtains the remainder. Valid source-generated H is in[0,1].
        if (c.H < 0 || c.H > 1)
            throw new NotSupportedException("Third-light HSV hue outside the source-generated finite interval requires its native conversion overflow path.");
        uint sector = unchecked((uint)(long)sectors) % 6;
        float fraction = c.H < 1 ? Sub(sectors, sector) : 0;
        float p = Mul(Sub(1, c.S), c.V);
        float q = Mul(Sub(1, Mul(c.S, fraction)), c.V);
        float t = Mul(Sub(1, Mul(Sub(1, fraction), c.S)), c.V);
        return sector switch
        {
            0 => new(c.V, t, p), 1 => new(q, c.V, p), 2 => new(p, c.V, t),
            3 => new(p, q, c.V), 4 => new(t, p, c.V), 5 => new(c.V, p, q),
            _ => throw new InvalidDataException("Invalid original HSV sector.")
        };
    }

    private static Rgb ReadRgb(Vector4 value)
    {
        var c = new Rgb(value.X, value.Y, value.Z);
        Finite(c.R); Finite(c.G); Finite(c.B); return c;
    }
    private static Vector4 ReadVector(ReadOnlySpan<byte> bytes, int at) =>
        new(Read(bytes, at), Read(bytes, at + 4), Read(bytes, at + 8), Read(bytes, at + 12));
    private static float Read(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(at, 4));
    private static void Write(byte[] bytes, int at, float value) => BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at, 4), value);
    private static void Finite(float value)
    { if (!float.IsFinite(value)) throw new InvalidDataException("Original finite third-light RGB/HSV conversion requires finite consumed colors."); }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Mul(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Sub(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Div(float a, float b) => a / b;
}
