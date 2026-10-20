using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data;

internal enum MapEdgeScrollSpeed { Disabled, Slow, Normal, Fast }
internal enum MapKeyboardScrollSpeed { Slow, Normal, Fast }

/// <summary>Current frontend choices, separate from campaign saves. These are
/// the source fresh environment choices; a settings edit replaces this value.</summary>
internal sealed record MapCameraNavigationOptions(MapEdgeScrollSpeed Edge = MapEdgeScrollSpeed.Disabled,
    MapKeyboardScrollSpeed Keyboard = MapKeyboardScrollSpeed.Normal)
{
    internal void Validate()
    {
        if (!Enum.IsDefined(Edge) || !Enum.IsDefined(Keyboard))
            throw new ArgumentOutOfRangeException(nameof(MapCameraNavigationOptions));
    }
}

/// <summary>The source-created camera input tuning. No native code or retained
/// original execution is consulted at runtime. The current controller owns it.</summary>
internal sealed class MapCameraNavigation
{
    // Paired TC/SC 16D90 source initialization, independently retained in the
    // camera input proof. Band6 and out-of-range bands use base0.
    private readonly int[] bandSpeeds = [200, 300, 600, 900, 1500, 1800];
    internal int BorderPixels { get; init; } = 2;
    internal int AngleHundredths { get; init; } = 6;

    internal int Speed(int band, MapCameraNavigationOptions options, bool keyboard)
    {
        int speed = (uint)band < bandSpeeds.Length ? bandSpeeds[band] : 0;
        int option = keyboard ? (int)options.Keyboard : (int)options.Edge - 1;
        int percent = option switch { 0 => 80, 1 => 100, 2 => 150, _ => 100 };
        return unchecked(speed * percent) / 100;
    }

    internal (int Horizontal, int Vertical) EdgeDirection(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        int b = BorderPixels;
        int left = Math.Max(b, unchecked(b * 1920) / width);
        int top = Math.Max(b, unchecked(b * 1080) / height);
        int right = Math.Min(unchecked(1920 - b), unchecked((width - b) * 1920) / width);
        int bottom = Math.Min(unchecked(1080 - b), unchecked((height - b) * 1080) / height);
        return (x < left ? 1 : x >= right ? -1 : 0, y < top ? -1 : y >= bottom ? 1 : 0);
    }

    internal static MainMapCameraControllerRequest Planar(MainMapCameraControllerState state,
        int speed, int horizontal, int vertical)
    {
        var trig = NativeTrigonometry.SinCos014590((float)state.YawA8);
        float u = unchecked(speed * horizontal), v = unchecked(speed * vertical);
        float x = Add((float)state.FocusX, Sub(Mul(u, trig.Cos), Mul(v, trig.Sin)));
        float z = Sub((float)state.FocusZ, Add(Mul(v, trig.Cos), Mul(u, trig.Sin)));
        return new(0, new Vector4(x, (float)state.FocusY, z, (float)state.FocusW), -1, 62.83185308, -1, 0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Sub(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Mul(float a, float b) => a * b;
}
