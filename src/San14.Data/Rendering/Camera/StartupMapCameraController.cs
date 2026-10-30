using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record StartupMapCameraTransition(int DurationMilliseconds, int ElapsedMilliseconds,
    int LastClock, Vector4 StartFocus, Vector4 TargetFocus, double StartDistance, double TargetDistance);

public sealed record StartupMapCameraState(MainMapCameraControllerState Pose,
    StartupMapCameraTransition? Transition, uint DirtyCountdownD8);

/// <summary>CB60/11F40 and the force page's11A70 request. The Startup controller
/// retains generic CCameraAttitude bounds; it never runs Main's seven-band
/// clamps, terrain qualification, inertia or eye-candidate rejection.</summary>
public static class StartupMapCameraController
{
    public static StartupMapCameraState ConstructTitleMap() => new(
        new(32400, 0, 144400, 1, 270000,
            (double)3.1415927410125732f, (double)1.5707963705062866f), null, 2);

    /// <summary>4F8140 calls11A70(controller,0,0,0). The source preserves yaw
    /// and pitch through62.83185308/-1 sentinels and requests500ms.</summary>
    public static StartupMapCameraState EnterForceSelection(StartupMapCameraState current, int sourceClock)
    {
        ArgumentNullException.ThrowIfNull(current);
        var p = current.Pose;
        return current with { Transition = new(500, 0, sourceClock,
            new((float)p.FocusX, (float)p.FocusY, (float)p.FocusZ, (float)p.FocusW),
            new(-7600, 0, 144400, 1), p.Distance90, 270000) };
    }

    /// <summary>16550 uses15640 elapsed clock, quadratic ease-out and E340
    /// binary32 focus interpolation. It does not consume yaw/pitch event fields.</summary>
    public static StartupMapCameraState Tick(StartupMapCameraState current, int sourceClock)
    {
        ArgumentNullException.ThrowIfNull(current);
        uint dirty = current.DirtyCountdownD8 == 0 ? 0 : current.DirtyCountdownD8 - 1;
        if (current.Transition is not { } t || t.ElapsedMilliseconds >= t.DurationMilliseconds)
            return current with { DirtyCountdownD8 = dirty };
        int elapsed = Math.Min(t.DurationMilliseconds,
            unchecked(t.ElapsedMilliseconds + sourceClock - t.LastClock));
        float fraction = t.DurationMilliseconds < 1 ? 1 : Math.Min(D(elapsed, t.DurationMilliseconds), 1);
        float ease = S(1, M(S(1, fraction), S(1, fraction)));
        float blend = ease >= 0 ? Math.Min(ease, 1) : 0;
        double distance = current.Pose.Distance90;
        if (t.StartDistance >= 0 && t.TargetDistance >= 0)
            distance = DA(DM((double)S(1, blend), t.StartDistance), DM((double)blend, t.TargetDistance));
        var p = current.Pose with { Distance90 = distance };
        if (MathF.Abs(t.StartFocus.W) >= 1.1920928955078125e-5f &&
            MathF.Abs(t.TargetFocus.W) >= 1.1920928955078125e-5f)
            p = p with { FocusX = L(t.StartFocus.X, t.TargetFocus.X, blend),
                FocusY = L(t.StartFocus.Y, t.TargetFocus.Y, blend),
                FocusZ = L(t.StartFocus.Z, t.TargetFocus.Z, blend), FocusW = 1 };
        return new(p, elapsed >= t.DurationMilliseconds ? null :
            t with { ElapsedMilliseconds = elapsed, LastClock = sourceClock }, 2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static float M(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float S(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float A(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a, float b) => a / b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double DM(double a, double b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double DA(double a, double b) => a + b;
    private static float L(float start, float end, float fraction) =>
        A(M(S(1, fraction), start), M(fraction, end));
}
