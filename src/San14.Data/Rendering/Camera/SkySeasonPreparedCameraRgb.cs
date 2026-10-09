using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Present source-season table Sun/Moon colors and exact common
/// C45D18 lighting transition. This RGB-only producer preserves every Fog and
/// weather row; its output is not a complete native V418 Camera packet.</summary>
public static class SkySeasonPreparedCameraRgb
{
    public const int PrefixByteLength = 0x3C0;

    /// <summary>Only the native Sun/Moon RGB/direction/transition fields are
    /// written. Full V418 Fog and weather fields remain the explicit caller's
    /// separate producer. Present source table dependency failure cannot enter
    /// the absent-table path. The currently bounded temperature branch accepts
    /// exact6500K: both native Blackbody evaluations are identical and have
    /// positive finite channels, so every source clamped ratio is exactly1.</summary>
    public static PreparedCameraLightingPacket PrepareWithSourceTransmittanceRgb(
        CameraLightingController controller, SkylightParameters resolvedParameters,
        ReadOnlySpan<byte> priorPrefix, float frameDelta,
        SkySeasonTransmittanceSource transmittanceSource,
        NativeCameraTangentDispatch dispatch, float currentColorTemperature21C)
    {
        ArgumentNullException.ThrowIfNull(controller); ArgumentNullException.ThrowIfNull(resolvedParameters);
        if (priorPrefix.Length != PrefixByteLength)
            throw new InvalidDataException("Prepared original Camera lighting requires an explicit 3C0-byte prior prefix.");
        if (BitConverter.SingleToUInt32Bits(currentColorTemperature21C) != 0x45CB2000)
            throw new NotSupportedException("Original present-table Camera RGB currently requires the proved6500K color-temperature ratio branch; other current temperatures need the source Blackbody producer.");
        foreach (float value in new[] { controller.TransitionSpan, controller.TransitionSuppression, controller.SunMargin,
            controller.SunHueSaturation.X, controller.SunHueSaturation.Y, controller.MoonHueSaturation.X,
            controller.MoonHueSaturation.Y, frameDelta, ReadFloat(priorPrefix, 0x3B4) }) Finite(value);
        SkylightDirections directions = SkylightDirectionMath.Calculate(resolvedParameters);
        var products = SkySeasonTransmittance.PrepareBaseColors(
            controller,resolvedParameters,directions,transmittanceSource,dispatch);
        SkylightBaseColors colors = products.TransmittedColors;
        bool below = controller.SunMargin >= directions.Sun.Y;
        var transition = AdvanceState(BinaryPrimitives.ReadUInt32LittleEndian(priorPrefix.Slice(0x3B0, 4)),
            ReadFloat(priorPrefix, 0x3B4), controller.TransitionSpan, frameDelta, below);
        uint state = transition.State; float elapsed = transition.Elapsed;
        bool primarySun = state is 0 or 1 or 5;
        Vector4 sun = colors.Sun, moon = colors.Moon;
        bool updateDirections = state is 0 or 3;
        if (!updateDirections)
        {
            float t = controller.TransitionSpan > 0 ? SkylightColorMath.Clamp01(D(elapsed, controller.TransitionSpan)) : 1;
            float u = SkylightColorMath.Clamp01(state < 3 ? A(t, t) : A(S(t, .5f), S(t, .5f)));
            float weight = state < 3 ? S(1, Curve(S(1, u), controller.TransitionSuppression)) : Curve(u, controller.TransitionSuppression);
            Vector3 oldSun = ReadRgb(priorPrefix, 0x2E4), oldMoon = ReadRgb(priorPrefix, 0x328);
            sun = new(Blend(colors.Sun.X, oldSun.X, weight), Blend(colors.Sun.Y, oldSun.Y, weight),
                Blend(colors.Sun.Z, oldSun.Z, weight), ReadFloat(priorPrefix, 0x2E0));
            moon = new(Blend(colors.Moon.X, oldMoon.X, t), Blend(colors.Moon.Y, oldMoon.Y, t),
                Blend(colors.Moon.Z, oldMoon.Z, t), ReadFloat(priorPrefix, 0x324));
            primarySun = Luma(sun) >= Luma(moon);
            updateDirections = (!primarySun && state < 3) || (primarySun && state > 3);
        }
        byte[] result = priorPrefix.ToArray();
        // C45081/C45094 store the untransmitted baseline before C458DF's
        // local RGB products. The common transition consumes those products;
        // the baseline storage remains the original1176CF0 output.
        WriteVector(result, 0x2C4, products.UnattenuatedBaseColors.Sun);
        WriteVector(result, 0x308, products.UnattenuatedBaseColors.Moon);
        WriteRgb(result, 0x2B8, directions.Sun); WriteRgb(result, 0x2FC, directions.Moon);
        if (updateDirections)
        { WriteRgb(result, 0x2AC, directions.Sun); WriteRgb(result, 0x2F0, directions.Moon); }
        WriteVector(result, 0x2D4, sun); WriteVector(result, 0x318, moon);
        if (state is 0 or 3)
        { WriteRgb(result, 0x2E4, colors.Sun); WriteRgb(result, 0x328, colors.Moon); }
        WriteFloat(result, 0x3B4, elapsed); WriteFloat(result, 0x3B8, 1);
        float strength = 1;
        if (controller.TransitionSpan > 0)
        {
            float centered = MathF.Abs(S(elapsed, M(controller.TransitionSpan, .5f)));
            strength = SkylightColorMath.Clamp01(D(A(centered, centered), controller.TransitionSpan));
        }
        WriteFloat(result, 0x3BC, strength);
        uint order = state | (primarySun ? 0x80000000u : 0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x3B0, 4), order);
        return new(result, directions, products.UnattenuatedBaseColors, order, elapsed);
    }

    private static (uint State, float Elapsed) AdvanceState(uint encodedState, float elapsed, float duration, float delta, bool below)
    {
        uint state = encodedState & 0x7FFFFFFF;
        switch (state)
        {
            case 0: if (!below) return (1, elapsed); break;
            case 3: if (below) return (4, elapsed); break;
            case 1:
                if (!below) { if (elapsed >= M(duration, .5f)) state = 2; }
                else { state = 5; float remaining = S(duration, elapsed); elapsed = duration;
                    if (remaining < duration) elapsed = Max(M(duration, .5f), remaining); }
                elapsed = Min(A(elapsed, delta), duration); break;
            case 2:
                if (!below) { if (elapsed >= duration) state = 3; }
                else { state = 4; float remaining = S(duration, elapsed); elapsed = M(duration, .5f);
                    if (remaining < elapsed) elapsed = Max(0, remaining); }
                elapsed = Min(A(elapsed, delta), duration); break;
            case 4:
                if (below) { if (elapsed >= M(duration, .5f)) state = 5; }
                else { state = 2; float remaining = S(duration, elapsed); elapsed = duration;
                    if (remaining < duration) elapsed = Max(M(duration, .5f), remaining); }
                elapsed = Min(A(elapsed, delta), duration); break;
            case 5:
                if (below) { if (elapsed >= duration) state = 0; }
                else { state = 1; float remaining = S(duration, elapsed); elapsed = M(duration, .5f);
                    if (remaining < elapsed) elapsed = Max(0, remaining); }
                elapsed = Min(A(elapsed, delta), duration); break;
            default: state = below ? 0u : 3u; break;
        }
        if (state is 0 or 3) elapsed = 0;
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
    private static Vector3 ReadRgb(ReadOnlySpan<byte> bytes, int offset)
    {
        Vector3 value = new(ReadFloat(bytes, offset), ReadFloat(bytes, offset + 4), ReadFloat(bytes, offset + 8));
        Finite(value.X); Finite(value.Y); Finite(value.Z); return value;
    }
    private static float ReadFloat(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset, 4));
    private static void WriteFloat(Span<byte> bytes, int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(bytes.Slice(offset, 4), value);
    private static void WriteRgb(Span<byte> bytes, int offset, Vector4 value)
    { WriteFloat(bytes, offset, value.X); WriteFloat(bytes, offset + 4, value.Y); WriteFloat(bytes, offset + 8, value.Z); }
    private static void WriteVector(Span<byte> bytes, int offset, Vector4 value)
    { WriteRgb(bytes, offset, value); WriteFloat(bytes, offset + 12, value.W); }
    private static void Finite(float value)
    { if (!float.IsFinite(value)) throw new InvalidDataException("Original prepared Camera lighting input/result is not finite."); }
    private static float A(float a, float b) => NativeFloat.Add(a, b);
    private static float S(float a, float b) => NativeFloat.Subtract(a, b);
    private static float M(float a, float b) => NativeFloat.Multiply(a, b);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a, float b) => a / b;
}
