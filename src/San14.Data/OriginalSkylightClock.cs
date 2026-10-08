using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record OriginalSkylightClockResult(OriginalSkylightParameters Parameters,
    bool AutoStepExecuted, bool CalendarStepExecuted, float IntegerDays);

/// <summary>The original C4639B auto-clock tail. Its explicit Camera controller
/// clock is independent of the simulation calendar and is applied after lighting production.</summary>
public static class OriginalSkylightClock
{
    private static readonly int[] DaysPerMonth = [-1, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    public static OriginalSkylightClockResult Advance(OriginalSkylightParameters input,
        int autoDateTimeStepType, float timeSpanPerDay, float frameDelta)
    {
        ArgumentNullException.ThrowIfNull(input);
        foreach (float value in new[] { input.MonthDay, input.DateTime, input.Longitude, input.Latitude,
            input.Altitude, input.NorthDir, input.MoonAge, timeSpanPerDay, frameDelta }) Finite(value);
        if (autoDateTimeStepType == 0 || input.DateTime < 0) return new(input, false, false, 0);
        float denominator = Max(1.1920928955078125e-05f, timeSpanPerDay);
        float advancedTime = A(input.DateTime, D(frameDelta, denominator)); Finite(advancedTime);
        OriginalNativeFloatParts time = OriginalNativeFloatRounding.SplitFraction1033788(advancedTime);
        OriginalSkylightParameters output = input with { DateTime = time.Fraction };
        if (autoDateTimeStepType != 2 || time.Integral == 0) return new(output, true, false, time.Integral);
        OriginalNativeFloatParts month = OriginalNativeFloatRounding.SplitFraction1033788(input.MonthDay);
        int monthIndex = Math.Clamp(TruncInt32(month.Integral), 1, 12);
        int monthLength = DaysPerMonth[monthIndex];
        float dayZero = OriginalNativeFloatRounding.Floor101EB80(A(M(monthLength - 1, month.Fraction), .5f));
        float dayCount = OriginalNativeFloatRounding.Floor101EB80(A(A(A(dayZero, 1), time.Integral), .5f));
        int day = TruncInt32(dayCount), shifts = 0;
        while (day > monthLength)
        {
            if (++shifts > 65536) throw new InvalidDataException("Original Skylight calendar exceeds the bounded month-transition contract.");
            day -= monthLength; monthIndex++; if (monthIndex > 12) monthIndex = 1;
            monthLength = DaysPerMonth[monthIndex];
        }
        while (day < 1)
        {
            if (++shifts > 65536) throw new InvalidDataException("Original Skylight calendar exceeds the bounded month-transition contract.");
            monthIndex--; if (monthIndex < 1) monthIndex = 12;
            monthLength = DaysPerMonth[monthIndex]; day += monthLength;
        }
        float fraction = D(day - 1, monthLength - 1);
        fraction = fraction >= 0.9999880790710449f ? 0.9999880790710449f : Max(0, fraction);
        float moonAge = A(input.MoonAge, D(time.Integral, 30)); Finite(moonAge);
        output = output with { MonthDay = A(monthIndex, fraction), MoonAge = OriginalNativeFloatRounding.SplitFraction1033788(moonAge).Fraction };
        return new(output, true, true, time.Integral);
    }

    /// <summary>Only the three fields written by this original tail are applied.
    /// The caller supplies a prefix from its source/controller lighting producer.</summary>
    public static byte[] ApplyToPreparedPrefix(ReadOnlySpan<byte> explicitPrefix, OriginalSkylightClockResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (explicitPrefix.Length != OriginalPreparedCameraLighting.PrefixByteLength)
            throw new InvalidDataException("Original Skylight clock requires an explicit 3C0 Camera prefix.");
        byte[] bytes = explicitPrefix.ToArray();
        if (!result.AutoStepExecuted) return bytes;
        foreach (float value in new[] { result.Parameters.MonthDay, result.Parameters.DateTime, result.Parameters.MoonAge }) Finite(value);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(0x204, 4), result.Parameters.DateTime);
        if (result.CalendarStepExecuted)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(0x200, 4), result.Parameters.MonthDay);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(0x218, 4), result.Parameters.MoonAge);
        }
        return bytes;
    }

    private static int TruncInt32(float value)
    {
        // cvttss2si overflow is an invalid native sentinel; a caller may not use
        // it to silently manufacture a new source date or run an unbounded loop.
        if (!float.IsFinite(value) || value < int.MinValue || (double)value > int.MaxValue)
            throw new InvalidDataException("Original Skylight calendar conversion is outside the finite int32 contract.");
        return (int)value;
    }
    private static float Max(float a, float b) => a > b ? a : b;
    private static void Finite(float value)
    { if (!float.IsFinite(value)) throw new InvalidDataException("Original Skylight clock input/result is not finite."); }
    private static float A(float a, float b) => OriginalNativeFloat.Add(a, b);
    private static float M(float a, float b) => OriginalNativeFloat.Multiply(a, b);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a, float b) => a / b;
}

public readonly record struct OriginalNativeFloatParts(float Fraction, float Integral);

/// <summary>Finite original scalar float32 split/floor helpers used by Skylight.
/// Integer masks and signed-zero branches are preserved, without framework rounding.</summary>
public static class OriginalNativeFloatRounding
{
    public static OriginalNativeFloatParts SplitFraction1033788(float value)
    {
        if (!float.IsFinite(value)) throw new InvalidDataException("Original float fraction split requires a finite input.");
        uint bits = BitConverter.SingleToUInt32Bits(value), sign = bits & 0x80000000;
        int exponent = (int)((bits >> 23) & 255) - 127;
        if (exponent < 0) return new(value, BitConverter.UInt32BitsToSingle(sign));
        if (exponent >= 23) return new(BitConverter.UInt32BitsToSingle(sign), value);
        uint integralBits = bits & ~((1u << (23 - exponent)) - 1);
        float integral = BitConverter.UInt32BitsToSingle(integralBits);
        float fraction = value == integral && value < 0 ? BitConverter.UInt32BitsToSingle(0x80000000) : OriginalNativeFloat.Subtract(value, integral);
        return new(fraction, integral);
    }

    public static float Floor101EB80(float value)
    {
        if (!float.IsFinite(value)) throw new InvalidDataException("Original float floor requires a finite input.");
        uint bits = BitConverter.SingleToUInt32Bits(value), absolute = bits & 0x7FFFFFFF;
        bool negative = bits != absolute;
        if (absolute >= 0x4B800000 || absolute == 0) return value;
        if (absolute < 0x3F800000) return negative ? -1 : 0;
        int shift = 150 - (int)(absolute >> 23);
        uint integralBits = bits & ~((1u << shift) - 1);
        float integral = BitConverter.UInt32BitsToSingle(integralBits);
        return negative && bits != integralBits ? OriginalNativeFloat.Subtract(integral, 1) : integral;
    }
}
