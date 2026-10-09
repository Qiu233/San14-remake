using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record SkylightBaseColors(Vector4 Sun, Vector4 Moon);

/// <summary>Original 1176CF0 base colors and B4BDB0/B4BBE0 HSV operations,
/// before any transmittance table, weather profile or directional modifier.</summary>
public static class SkylightColorMath
{
    public static SkylightBaseColors Prepare(Vector2 sunHueSaturation, Vector2 moonHueSaturation)
    {
        Finite(sunHueSaturation.X); Finite(sunHueSaturation.Y);
        Finite(moonHueSaturation.X); Finite(moonHueSaturation.Y);
        float sun = M(0.009999999776482582f, 123384.234375f);
        float moon = M(0.009999999776482582f, 0.2718055248260498f);
        Vector4 sunHsv = RgbToHsv(new(sun, M(sun, 0.8718000054359436f), M(sun, 0.8129799962043762f), 1));
        Vector4 moonHsv = RgbToHsv(new(moon, M(moon, 0.8847000002861023f), M(moon, 0.8424000144004822f), 1));
        sunHsv.X = Clamp01(sunHueSaturation.X); sunHsv.Y = Clamp01(sunHueSaturation.Y);
        moonHsv.X = Clamp01(moonHueSaturation.X); moonHsv.Y = Clamp01(moonHueSaturation.Y);
        return new(HsvToRgb(sunHsv), HsvToRgb(moonHsv));
    }

    public static Vector4 RgbToHsv(Vector4 rgb)
    {
        Finite(rgb.X); Finite(rgb.Y); Finite(rgb.Z); // W is copied as an opaque source word.
        float maximum, range, hue = 0;
        if (rgb.Y > rgb.X)
        {
            if (rgb.Z > rgb.Y)
            {
                maximum = rgb.Z; range = S(rgb.Z, rgb.X);
                if (range > 0) hue = A(D(M(S(rgb.X, rgb.Y), 60), range), 240);
            }
            else
            {
                maximum = rgb.Y; range = S(rgb.Y, rgb.Z > rgb.X ? rgb.X : rgb.Z);
                if (range > 0) hue = A(D(M(S(rgb.Z, rgb.X), 60), range), 120);
            }
        }
        else if (rgb.Z > rgb.X)
        {
            maximum = rgb.Z; range = S(rgb.Z, rgb.Y);
            if (range > 0) hue = A(D(M(S(rgb.X, rgb.Y), 60), range), 240);
        }
        else
        {
            maximum = rgb.X; range = S(rgb.X, rgb.Z > rgb.Y ? rgb.Y : rgb.Z);
            if (range > 0) hue = D(M(S(rgb.Y, rgb.Z), 60), range);
        }
        if (hue < 0) hue = A(hue, 360);
        else if (hue > 360) hue = A(hue, -360);
        return new(D(hue, 360), maximum == 0 ? 0 : D(range, maximum), maximum, rgb.W);
    }

    /// <summary>Finite original HSV input with hue in [0,1]. No framework color conversion.</summary>
    public static Vector4 HsvToRgb(Vector4 hsv)
    {
        Finite(hsv.X); Finite(hsv.Y); Finite(hsv.Z);
        if (hsv.X < 0 || hsv.X > 1)
            throw new InvalidDataException("Original bounded HSV conversion requires hue in [0,1].");
        if (hsv.Y == 0) return new(hsv.Z, hsv.Z, hsv.Z, hsv.W);
        float sixHue = M(hsv.X, 6);
        uint sector = (uint)(ulong)sixHue % 6;
        float fraction = hsv.X < 1 ? S(sixHue, sector) : 0;
        float p = M(S(1, hsv.Y), hsv.Z);
        float q = M(S(1, M(hsv.Y, fraction)), hsv.Z);
        float t = M(S(1, M(S(1, fraction), hsv.Y)), hsv.Z);
        return sector switch
        {
            0 => new(hsv.Z, t, p, hsv.W), 1 => new(q, hsv.Z, p, hsv.W),
            2 => new(p, hsv.Z, t, hsv.W), 3 => new(p, q, hsv.Z, hsv.W),
            4 => new(t, p, hsv.Z, hsv.W), _ => new(hsv.Z, p, q, hsv.W)
        };
    }

    internal static float Clamp01(float value) => value >= 1 ? 1 : value <= 0 ? 0 : value;
    private static void Finite(float value)
    { if (!float.IsFinite(value)) throw new InvalidDataException("Original Skylight RGB/HSV input is not finite."); }
    private static float A(float a, float b) => NativeFloat.Add(a, b);
    private static float S(float a, float b) => NativeFloat.Subtract(a, b);
    private static float M(float a, float b) => NativeFloat.Multiply(a, b);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float D(float a, float b) => a / b;
}
