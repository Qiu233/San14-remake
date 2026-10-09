using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record ArmyAnimationUnsupportedChannel(int JointIndex,
    int DescriptorOffsetDwords, ushort? ChannelType, string Reason);

public sealed record ArmyAnimationChannelSample(int JointIndex,
    int DescriptorOffsetDwords, ushort ChannelType, int KeyIndex,
    uint CoefficientIndex, ushort StartFrame, ushort EndFrame,
    float NormalizedSegmentTime, Vector3 Value);

/// <summary>Only channels actually present in the explicit clip are populated.</summary>
public sealed record ArmyAnimationJointSample(int JointIndex,
    Vector3? Scale, Quaternion? Rotation, Vector3? Translation,
    IReadOnlyList<ArmyAnimationChannelSample> Channels)
{
    public int PackedTableIndex { get; init; }
}

public sealed record ArmyAnimationSample(float TimeSeconds, uint FixedFrameTime,
    IReadOnlyList<ArmyAnimationJointSample> Joints,
    IReadOnlyList<ArmyAnimationUnsupportedChannel> UnsupportedChannels)
{
    public bool Complete => UnsupportedChannels.Count == 0;
    public bool AnimationStateFidelityVerified => false;
}

/// <summary>
/// Samples the explicitly supplied original packed model animation channels at an explicit
/// time. Ports TC D95990, D93640, ABD060, ABD3B0 and 14590 (paired with SC in the
/// sampling source contract). Outputs local channels; action selection, skeleton map,
/// missing-channel defaults, hierarchy and current/previous activity belong to callers.
/// </summary>
public static class ArmyAnimationSampler
{
    public static ArmyAnimationSample Sample(ArmyAnimationClip clip,
        float timeSeconds)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (!float.IsFinite(timeSeconds))
            throw new ArgumentOutOfRangeException(nameof(timeSeconds), "Sampling requires a finite explicit time.");
        if (!float.IsFinite(clip.FrameRate) || clip.FrameRate < 0)
            throw new ArgumentException("The explicit clip has an unsupported frame rate.", nameof(clip));

        uint fixedTime = ToFixedFrameTime(timeSeconds, clip.FrameRate, clip.FrameCount);
        var joints = new List<ArmyAnimationJointSample>();
        var unsupported = new List<ArmyAnimationUnsupportedChannel>();
        var descriptors = new Dictionary<int, ArmyAnimationChannel>();
        foreach (var channel in clip.Channels)
        {
            if (!descriptors.TryAdd(channel.DescriptorOffsetDwords, channel))
                throw new ArgumentException("The explicit clip has duplicate channel descriptor offsets.", nameof(clip));
        }

        for (int tableIndex = 0; tableIndex < clip.PackedJointChannels.Count; tableIndex++)
        {
            uint packed = clip.PackedJointChannels[tableIndex];
            int jointIndex = (int)((packed >> 4) & 0x3FF);
            int count = (int)(packed & 0xF);
            int offset = (int)(packed >> 14);
            Vector3? scale = null, translation = null;
            Quaternion? rotation = null;
            var samples = new List<ArmyAnimationChannelSample>();
            int previousType = -1;
            for (int i = 0; i < count; i++)
            {
                if (!descriptors.TryGetValue(offset, out var channel))
                {
                    unsupported.Add(new(jointIndex, offset, null, "No parsed original descriptor exists at this packed offset."));
                    break;
                }
                // The native model caller consumes an optional 0, then 1, then 2.
                // Other orders and extra types cannot be promoted to model pose channels.
                if (channel.Type > 2 || channel.Type <= previousType)
                {
                    unsupported.Add(new(jointIndex, offset, channel.Type,
                        "The original model sampler supports ordered optional rotation(0), translation(1), scale(2) channels."));
                    break;
                }
                if (!TrySampleChannel(clip, channel, jointIndex, fixedTime, out var sampled, out string? reason))
                {
                    unsupported.Add(new(jointIndex, offset, channel.Type, reason!));
                    break;
                }
                samples.Add(sampled!);
                switch (channel.Type)
                {
                    case 0:
                        if (!TryExponentialVectorQuaternion(sampled!.Value, out var q, out reason))
                            unsupported.Add(new(jointIndex, offset, channel.Type, reason!));
                        else rotation = q;
                        break;
                    case 1: translation = sampled!.Value; break;
                    case 2: scale = sampled!.Value; break;
                }
                previousType = channel.Type;
                offset = checked(offset + 2 + ((channel.KeyTimes.Count + 1) >> 1));
            }
            joints.Add(new(jointIndex, scale, rotation, translation, samples) { PackedTableIndex = tableIndex });
        }
        return new(timeSeconds, fixedTime, joints, unsupported);
    }

    /// <summary>The native seconds-to-16.16-frame conversion and unsigned clamp.</summary>
    public static uint ToFixedFrameTime(float timeSeconds, float frameRate, ushort frameCount)
    {
        if (!float.IsFinite(timeSeconds) || !float.IsFinite(frameRate) || frameRate < 0)
            throw new ArgumentOutOfRangeException(nameof(timeSeconds), "Time and nonnegative frame rate must be finite.");
        float nonnegative = timeSeconds > 0 ? timeSeconds : 0;
        float scaled = Multiply(Multiply(nonnegative, frameRate), 65536f);
        // Native cvttss2si rax requires a representable signed 64-bit value. Keep
        // undefined/indefinite inputs outside this explicit safe sampling interface.
        if (!float.IsFinite(scaled) || (double)scaled >= 9223372036854775808d)
            throw new ArgumentOutOfRangeException(nameof(timeSeconds), "The native fixed-frame conversion overflows.");
        uint converted = unchecked((uint)(long)scaled);
        uint end = (uint)frameCount << 16;
        return converted <= end ? converted : end;
    }

    private static bool TrySampleChannel(ArmyAnimationClip clip,
        ArmyAnimationChannel channel, int jointIndex, uint fixedTime,
        out ArmyAnimationChannelSample? sample, out string? reason)
    {
        sample = null; reason = null;
        var times = channel.KeyTimes;
        for (int i = 1; i < times.Count; i++)
        {
            if (times[i] <= times[i - 1])
            {
                reason = "Original key times are not strictly increasing; the native segment search is unsupported for this input.";
                return false;
            }
        }
        int first = 0, remaining = times.Count;
        uint integerFrame = fixedTime >> 16;
        while (remaining > 1)
        {
            int half = remaining >> 1;
            int midpoint = first + half;
            if (integerFrame < times[midpoint]) remaining = half;
            else { first = midpoint; remaining = (remaining + 1) >> 1; }
        }
        ushort start = 0, end = clip.FrameCount;
        if (remaining == 1)
        {
            start = times[first];
            if (first + 1 != times.Count) end = times[first + 1];
        }
        int signedDuration = unchecked((end - start) << 16);
        if (signedDuration == 0)
        {
            reason = "The selected original segment has zero duration.";
            return false;
        }
        uint coefficientIndex = unchecked(channel.FirstCoefficientIndex + (uint)first);
        long coefficientOffset = (long)coefficientIndex * 32;
        if (coefficientOffset + 32 > clip.PackedCoefficients.Length)
        {
            reason = "The selected original 32-byte coefficient block is absent.";
            return false;
        }
        uint timeOffset = unchecked(fixedTime - ((uint)start << 16));
        float t = Divide((float)timeOffset, (float)signedDuration);
        Vector3 value = EvaluatePackedCubic(clip.PackedCoefficients.AsSpan((int)coefficientOffset, 32), t);
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
        {
            reason = "The selected original coefficient/time arithmetic is nonfinite.";
            return false;
        }
        sample = new(jointIndex, channel.DescriptorOffsetDwords, channel.Type,
            first, coefficientIndex, start, end, t, value);
        return true;
    }

    /// <summary>Original four packed u64 vector coefficients, in constant-to-cubic order.</summary>
    public static Vector3 EvaluatePackedCubic(ReadOnlySpan<byte> coefficientBytes, float normalizedTime)
    {
        if (coefficientBytes.Length != 32)
            throw new ArgumentException("The original packed vector cubic requires exactly 32 bytes.", nameof(coefficientBytes));
        Vector3 c0 = DecodePackedVector(BinaryPrimitives.ReadUInt64LittleEndian(coefficientBytes));
        Vector3 c1 = DecodePackedVector(BinaryPrimitives.ReadUInt64LittleEndian(coefficientBytes[8..]));
        Vector3 c2 = DecodePackedVector(BinaryPrimitives.ReadUInt64LittleEndian(coefficientBytes[16..]));
        Vector3 c3 = DecodePackedVector(BinaryPrimitives.ReadUInt64LittleEndian(coefficientBytes[24..]));
        float square = Multiply(normalizedTime, normalizedTime);
        float cube = Multiply(square, normalizedTime);
        return new(EvaluateComponent(c0.X, c1.X, c2.X, c3.X, normalizedTime, square, cube),
            EvaluateComponent(c0.Y, c1.Y, c2.Y, c3.Y, normalizedTime, square, cube),
            EvaluateComponent(c0.Z, c1.Z, c2.Z, c3.Z, normalizedTime, square, cube));
    }

    /// <summary>Three signed 20-bit values with the original shared 4-bit exponent.</summary>
    public static Vector3 DecodePackedVector(ulong packed)
    {
        int exponentBits = unchecked((int)(((packed >> 37) & 0x07800000) + 0x32000000));
        float multiplier = BitConverter.Int32BitsToSingle(exponentBits);
        int x = unchecked((int)((packed >> 28) & 0xFFFFF000));
        int y = unchecked((int)((packed >> 8) & 0xFFFFF000));
        int z = unchecked((int)(packed << 12)) & unchecked((int)0xFFFFF000);
        return new(Multiply((float)x, multiplier), Multiply((float)y, multiplier), Multiply((float)z, multiplier));
    }

    private static float EvaluateComponent(float c0, float c1, float c2, float c3,
        float time, float square, float cube) =>
        Add(Multiply(c3, cube), Add(Add(c0, Multiply(c1, time)), Multiply(c2, square)));

    private static bool TryExponentialVectorQuaternion(Vector3 value,
        out Quaternion rotation, out string? reason)
    {
        rotation = default; reason = null;
        float squared = Add(Add(Multiply(value.X, value.X), Multiply(value.Y, value.Y)), Multiply(value.Z, value.Z));
        float length = MathF.Sqrt(squared); // Original finite path is sqrtss (101FB6C).
        if (!float.IsFinite(length)) { reason = "The original rotation magnitude is nonfinite."; return false; }
        if (!TrySourceSinCos(Multiply(length, 0.5f), out float sin, out float cos, out reason)) return false;
        float factor = length > BitConverter.Int32BitsToSingle(0x37480000) ? Divide(sin, length) : 0.5f;
        rotation = new(Multiply(value.X, factor), Multiply(value.Y, factor), Multiply(value.Z, factor), cos);
        return true;
    }

    /// <summary>The native scalar piecewise sin/cos polynomials, without normalization.</summary>
    public static (float Sin, float Cos) SourceSinCos(float angle)
    {
        if (!TrySourceSinCos(angle, out float sin, out float cos, out string? reason))
            throw new ArgumentOutOfRangeException(nameof(angle), reason);
        return (sin, cos);
    }

    private static bool TrySourceSinCos(float angle, out float sin, out float cos, out string? reason)
    {
        sin = cos = 0; reason = null;
        if (!float.IsFinite(angle)) { reason = "The original sin/cos input is nonfinite."; return false; }
        if (angle == 0) { cos = 1; return true; }
        bool positiveSin = angle >= 0, positiveCos = true;
        float a = positiveSin ? angle : -angle;
        float twoPi = BitConverter.Int32BitsToSingle(0x40C90FDB);
        // The source repeatedly adds -2*pi. Bounded inputs keep the explicit
        // interface finite; larger angles retain an unsupported outcome.
        if (a > Multiply(twoPi, 4096))
        { reason = "The original iterative angle reduction exceeds the explicit sampling bound of 4096 periods."; return false; }
        while (a >= twoPi) a = Add(a, -twoPi);
        float pi = BitConverter.Int32BitsToSingle(0x40490FDB);
        float halfPi = BitConverter.Int32BitsToSingle(0x3FC90FDB);
        if (a >= pi) { a = Add(a, -pi); positiveSin = !positiveSin; positiveCos = false; }
        if (a >= halfPi) { a = Subtract(pi, a); positiveCos = !positiveCos; }
        if (a < BitConverter.Int32BitsToSingle(0x3F2B16EC))
        {
            sin = SinPolynomial(a, positiveSin);
            cos = CosPolynomial(a, positiveCos);
        }
        else if (a < BitConverter.Int32BitsToSingle(0x3F688790))
        {
            cos = CosPolynomial(a, positiveCos);
            sin = CosPolynomial(Subtract(halfPi, a), positiveSin);
        }
        else
        {
            float complement = Subtract(halfPi, a);
            cos = SinPolynomial(complement, positiveCos);
            sin = CosPolynomial(complement, positiveSin);
        }
        return true;
    }

    private static float SinPolynomial(float angle, bool positive)
    {
        float square = Multiply(angle, angle);
        float fourth = Multiply(square, square);
        float first = positive ? Subtract(6, square) : Subtract(square, 6);
        float sum = positive ? Add(Multiply(first, 20), fourth) : Subtract(Multiply(first, 20), fourth);
        return Multiply(Multiply(sum, angle), BitConverter.Int32BitsToSingle(0x3C088889));
    }

    private static float CosPolynomial(float angle, bool positive)
    {
        float square = Multiply(angle, angle);
        float fourthPlus = Add(Multiply(square, square), 360);
        float first = positive ? Subtract(30, square) : Subtract(square, 30);
        float product = Multiply(fourthPlus, first);
        float sum = positive ? Subtract(product, 10080) : Add(product, 10080);
        return Multiply(sum, BitConverter.Int32BitsToSingle(0x3AB60B61));
    }

    // Keep the original SSE operations separately rounded; no FMA substitution.
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Subtract(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float a, float b) => a / b;
}
