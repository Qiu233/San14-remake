using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Common celestial transition math; state contains no camera layout.</summary>
internal static class SkyLightingTransition
{
    internal static SkyLightingState Prepare(CameraLightingController controller,
        SkylightDirections directions, SkylightBaseColors baseColors, SkylightBaseColors colors,
        SkyLightingState prior, float frameDelta)
    {
        bool below = controller.SunMargin >= directions.Sun.Y;
        var transition = AdvanceState(prior.Transition.Phase, prior.Transition.Elapsed,
            controller.TransitionSpan, frameDelta, below);
        var state = transition.State; float elapsed = transition.Elapsed;
        bool primarySun = state is SkyLightingTransitionPhase.SunPrimary or SkyLightingTransitionPhase.SunToMoonFirst or SkyLightingTransitionPhase.MoonToSunSecond;
        Vector4 sun = colors.Sun, moon = colors.Moon;
        bool updateDirections = state is SkyLightingTransitionPhase.SunPrimary or SkyLightingTransitionPhase.MoonPrimary;
        if (!updateDirections)
        {
            bool sunToMoon = state is SkyLightingTransitionPhase.SunToMoonFirst or SkyLightingTransitionPhase.SunToMoonSecond;
            float t = controller.TransitionSpan > 0 ? SkylightColorMath.Clamp01(D(elapsed, controller.TransitionSpan)) : 1;
            float u = SkylightColorMath.Clamp01(sunToMoon ? A(t, t) : A(S(t, .5f), S(t, .5f)));
            float weight = sunToMoon ? S(1, Curve(S(1, u), controller.TransitionSuppression)) : Curve(u, controller.TransitionSuppression);
            Vector3 oldSun = prior.Sun.PreviousColor, oldMoon = prior.Moon.PreviousColor;
            Finite(oldSun.X); Finite(oldSun.Y); Finite(oldSun.Z);
            Finite(oldMoon.X); Finite(oldMoon.Y); Finite(oldMoon.Z);
            sun = new(Blend(colors.Sun.X, oldSun.X, weight), Blend(colors.Sun.Y, oldSun.Y, weight),
                Blend(colors.Sun.Z, oldSun.Z, weight), prior.Sun.CurrentColor.W);
            moon = new(Blend(colors.Moon.X, oldMoon.X, t), Blend(colors.Moon.Y, oldMoon.Y, t),
                Blend(colors.Moon.Z, oldMoon.Z, t), prior.Moon.CurrentColor.W);
            primarySun = Luma(sun) >= Luma(moon);
            updateDirections = (!primarySun && sunToMoon) || (primarySun && !sunToMoon);
        }
        float strength = 1;
        if (controller.TransitionSpan > 0)
        {
            float centered = MathF.Abs(S(elapsed, M(controller.TransitionSpan, .5f)));
            strength = SkylightColorMath.Clamp01(D(A(centered, centered), controller.TransitionSpan));
        }
        bool settled = state is SkyLightingTransitionPhase.SunPrimary or SkyLightingTransitionPhase.MoonPrimary;
        return new(
            new(updateDirections ? Xyz(directions.Sun) : prior.Sun.SelectedDirection, directions.Sun,
                baseColors.Sun, sun, settled ? Xyz(colors.Sun) : prior.Sun.PreviousColor),
            new(updateDirections ? Xyz(directions.Moon) : prior.Moon.SelectedDirection, directions.Moon,
                baseColors.Moon, moon, settled ? Xyz(colors.Moon) : prior.Moon.PreviousColor),
            new(state, primarySun, elapsed, 1, strength));
    }

    internal static void ValidateInputs(CameraLightingController controller, SkyLightingState prior, float frameDelta)
    {
        foreach (float value in new[] { controller.TransitionSpan, controller.TransitionSuppression, controller.SunMargin,
            controller.SunHueSaturation.X, controller.SunHueSaturation.Y, controller.MoonHueSaturation.X,
            controller.MoonHueSaturation.Y, frameDelta, prior.Transition.Elapsed }) Finite(value);
    }

    private static Vector3 Xyz(Vector4 value) => new(value.X, value.Y, value.Z);

    private static (SkyLightingTransitionPhase State, float Elapsed) AdvanceState(SkyLightingTransitionPhase phase, float elapsed, float duration, float delta, bool below)
    {
        var state = phase;
        switch (state)
        {
            case SkyLightingTransitionPhase.SunPrimary: if (!below) return (SkyLightingTransitionPhase.SunToMoonFirst, elapsed); break;
            case SkyLightingTransitionPhase.MoonPrimary: if (below) return (SkyLightingTransitionPhase.MoonToSunFirst, elapsed); break;
            case SkyLightingTransitionPhase.SunToMoonFirst:
                if (!below) { if (elapsed >= M(duration, .5f)) state = SkyLightingTransitionPhase.SunToMoonSecond; }
                else { state = SkyLightingTransitionPhase.MoonToSunSecond; float remaining = S(duration, elapsed); elapsed = duration;
                    if (remaining < duration) elapsed = Max(M(duration, .5f), remaining); }
                elapsed = Min(A(elapsed, delta), duration); break;
            case SkyLightingTransitionPhase.SunToMoonSecond:
                if (!below) { if (elapsed >= duration) state = SkyLightingTransitionPhase.MoonPrimary; }
                else { state = SkyLightingTransitionPhase.MoonToSunFirst; float remaining = S(duration, elapsed); elapsed = M(duration, .5f);
                    if (remaining < elapsed) elapsed = Max(0, remaining); }
                elapsed = Min(A(elapsed, delta), duration); break;
            case SkyLightingTransitionPhase.MoonToSunFirst:
                if (below) { if (elapsed >= M(duration, .5f)) state = SkyLightingTransitionPhase.MoonToSunSecond; }
                else { state = SkyLightingTransitionPhase.SunToMoonSecond; float remaining = S(duration, elapsed); elapsed = duration;
                    if (remaining < duration) elapsed = Max(M(duration, .5f), remaining); }
                elapsed = Min(A(elapsed, delta), duration); break;
            case SkyLightingTransitionPhase.MoonToSunSecond:
                if (below) { if (elapsed >= duration) state = SkyLightingTransitionPhase.SunPrimary; }
                else { state = SkyLightingTransitionPhase.SunToMoonFirst; float remaining = S(duration, elapsed); elapsed = M(duration, .5f);
                    if (remaining < elapsed) elapsed = Max(0, remaining); }
                elapsed = Min(A(elapsed, delta), duration); break;
            default: state = below ? SkyLightingTransitionPhase.SunPrimary : SkyLightingTransitionPhase.MoonPrimary; break;
        }
        if (state is SkyLightingTransitionPhase.SunPrimary or SkyLightingTransitionPhase.MoonPrimary) elapsed = 0;
        Finite(elapsed);
        return (state, elapsed);
    }

    // Verified original 101EC10 finite special branches. General library pow
    // remains explicit until its polynomial/table path has been ported.
    private static float Curve(float value, float exponent)
    {
        if (exponent == 1) return value;
        if (exponent == 0 || value == 1) return 1;
        if (value == 0 && exponent > 0) return 0;
        throw new NotSupportedException("Original Skylight transition curve needs the unresolved 101EC10 general-power producer for this explicit input; no framework pow substitution is used.");
    }

    private static float Blend(float baseline, float previous, float weight) => A(M(S(1, weight), previous), M(weight, baseline));
    private static float Luma(Vector4 value) => A(A(M(value.X, 0.29899999499320984f), M(value.Y, 0.5870000123977661f)), M(value.Z, 0.11400000005960464f));
    private static float Min(float a, float b) => a < b ? a : b;
    private static float Max(float a, float b) => a > b ? a : b;
    private static void Finite(float value)
    { if (!float.IsFinite(value)) throw new InvalidDataException("Original prepared Camera lighting input/result is not finite."); }
    private static float A(float a, float b) => NativeFloat.Add(a, b);
    private static float S(float a, float b) => NativeFloat.Subtract(a, b);
    private static float M(float a, float b) => NativeFloat.Multiply(a, b);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a, float b) => a / b;
}
