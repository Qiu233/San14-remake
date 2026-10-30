using System.Numerics;

namespace San14.Data;

/// <summary>Source speaker coefficients, before the separate source LFE and output sends.</summary>
internal static class SourceSpatialPanning
{
    private const float QuarterPi = 0.7853982f;
    private const float HalfPi = 1.5707964f;
    private const float Pi = 3.1415927f;
    private const float RootTwo = 1.4142135f;

    internal static float[] Prepare(float azimuthRadians, float directionalBlend,
        int outputChannels, int outputProfileChannels, float centerGain, float spreadGain)
    {
        if (outputChannels is not (2 or 6 or 8))
            throw new ArgumentOutOfRangeException(nameof(outputChannels));
        Vector3 profile = outputProfileChannels switch
        {
            2 or 4 => new(FromBits(0x3f490fdb), FromBits(0x4016cbe4), -1f),
            6 => new(FromBits(0x3f060a92), FromBits(0x3ff5be0b), -1f),
            8 => new(FromBits(0x3f060a92), FromBits(0x3ff5be0b), FromBits(0x40278d37)),
            _ => throw new ArgumentOutOfRangeException(nameof(outputProfileChannels))
        };
        var result = new float[outputChannels];
        if (!float.IsFinite(azimuthRadians) || !float.IsFinite(directionalBlend) ||
            !float.IsFinite(centerGain) || !float.IsFinite(spreadGain)) return result;
        Vector2 position = Project(profile, azimuthRadians, directionalBlend);
        if (outputChannels == 2) Stereo(result, position.X, spreadGain);
        else if (outputChannels == 6) SurroundSix(result, position.X, position.Y, centerGain, spreadGain);
        else SurroundEight(result, position.X, position.Y, centerGain, spreadGain);
        return result;
    }

    private static float FromBits(int bits) => BitConverter.Int32BitsToSingle(bits);

    // The loaded mix profile owns these breakpoints, independently of the device fold.
    private static Vector2 Project(Vector3 profile, float angle, float blend)
    {
        float a = MathF.Abs(angle), x, y;
        if (a <= profile.X) { x = a / profile.X; y = -1f; }
        else if (profile.Z < 0f)
        {
            if (a <= profile.Y) { x = 1f; y = 2f * ((a - profile.X) / (profile.Y - profile.X)) - 1f; }
            else { x = (a - Pi) / (profile.Y - Pi); y = 1f; }
        }
        else if (a <= profile.Y) { x = 1f; y = (a - profile.X) / (profile.Y - profile.X) - 1f; }
        else if (a <= profile.Z) { x = 1f; y = (a - profile.Y) / (profile.Z - profile.Y); }
        else { x = (a - Pi) / (profile.Z - Pi); y = 1f; }
        x *= blend;
        y *= blend;
        return new(angle < 0f ? -x : x, y);
    }

    private static void Stereo(float[] output, float horizontal, float spread)
    {
        float upper = horizontal, lower = horizontal;
        if (spread > 0f)
        {
            float wide = (spread + 1f) * horizontal;
            upper = MathF.Min(wide + spread, 1f);
            lower = MathF.Max(wide - spread, -1f);
        }
        output[0] = MathF.Max(MathF.Cos((upper + 1f) * QuarterPi), 0f);
        output[1] = MathF.Sin((lower + 1f) * QuarterPi);
    }

    // Four lanes are left, right, front, back. The source widens only horizontal input.
    private static Vector4 Quad(float horizontal, float frontBack, float spread)
    {
        float upper = horizontal, lower = horizontal;
        if (spread > 0f)
        {
            float wide = (spread + 1f) * horizontal;
            upper = MathF.Min(wide + spread, 1f);
            lower = MathF.Max(wide - spread, -1f);
        }
        Vector4 angles = new Vector4(upper, lower, frontBack, frontBack) *
            new Vector4(-QuarterPi, QuarterPi, -QuarterPi, QuarterPi) + new Vector4(QuarterPi);
        angles = Vector4.Min(Vector4.Max(Vector4.Zero, angles), new Vector4(HalfPi));
        return new(MathF.Sin(angles.X), MathF.Sin(angles.Y), MathF.Sin(angles.Z), MathF.Sin(angles.W));
    }

    private static Vector4 Corners(Vector4 q) => Vector4.Max(Vector4.Zero,
        new Vector4(q.X * q.Z, q.Y * q.Z, q.X * q.W, q.Y * q.W));

    private static Vector4 SplitFront(Vector4 q, float horizontal, float center)
    {
        Vector4 aux = Quad(2f * horizontal + (horizontal > 0f ? -1f : 1f), 2f * center - 1f, 0f);
        return Vector4.Max(Vector4.Zero, new Vector4(aux.Z * q.X * q.Z, aux.Z * q.Y * q.Z,
            aux.W * q.Z * aux.X, aux.W * q.Z * aux.Y));
    }

    private static float Power(Vector4 q) => MathF.Sqrt(q.X * q.X + q.Y * q.Y);
    private static float Energy(float x, float y) => MathF.Sqrt(x * x + y * y);
    private static float Remainder(float value) => value > 0f ? MathF.Sqrt(value) : 0f;

    private static void SurroundSix(float[] output, float h, float b, float center, float spread)
    {
        Vector4 q = Quad(h, b, spread), corners = Corners(q);
        output[4] = corners.Z;
        output[5] = corners.W;
        if (center <= 0f)
        {
            output[0] = corners.X;
            output[1] = corners.Y;
        }
        else if (center >= 1f)
        {
            float angle = Math.Clamp((h > 0f ? h : h + 1f) * HalfPi, 0f, HalfPi);
            float first = MathF.Max(MathF.Cos(angle) * q.Z, 0f);
            float second = MathF.Max(MathF.Sin(angle) * q.Z, 0f);
            float power = Power(q);
            if (h > 0f) { output[1] = second * power; output[2] = first * power; }
            else { output[0] = first * power; output[2] = second * power; }
        }
        else
        {
            Vector4 v = SplitFront(q, h, center);
            if (h > 0f)
            {
                output[0] = v.X;
                output[1] = Energy(v.Y, v.W);
                output[2] = spread < 1f ? v.Z * (1f - spread) : 0f;
            }
            else
            {
                output[0] = Energy(v.X, v.Z);
                output[1] = v.Y;
                output[2] = spread < 1f ? v.W * (1f - spread) : 0f;
            }
        }
    }

    private static void SurroundEight(float[] output, float h, float b, float center, float spread)
    {
        Vector4 q = Quad(h, b, spread), corners = Corners(q);
        bool side = MathF.Abs(b) < MathF.Abs(h);
        bool right = h > 0f;
        output[6] = corners.Z;
        output[7] = corners.W;
        if (center <= 0f)
        {
            output[0] = corners.X;
            output[1] = corners.Y;
            if (!side) return;
            Vector4 aux = Quad(h, MathF.Abs(h) - MathF.Abs(b), 0f);
            if (right)
            {
                if (b >= 0f) { output[1] = aux.X * q.Y; output[7] = aux.Z * corners.W * RootTwo; }
                else { output[7] = aux.X * q.Y; output[1] = aux.Z * corners.Y * RootTwo; }
                output[5] = Remainder((q.Y * q.Y - output[1] * output[1]) - output[7] * output[7]);
            }
            else
            {
                if (b >= 0f) { output[0] = aux.Y * q.X; output[6] = aux.Z * corners.Z * RootTwo; }
                else { output[6] = aux.Y * q.X; output[0] = aux.Z * corners.X * RootTwo; }
                output[4] = Remainder((q.X * q.X - output[0] * output[0]) - output[6] * output[6]);
            }
            return;
        }
        if (center >= 1f)
        {
            EightFullCenter(output, q, corners, h, b, side, right);
            return;
        }
        Vector4 v = SplitFront(q, h, center);
        float powerSquared = q.X * q.X + q.Y * q.Y;
        float power = MathF.Sqrt(powerSquared);
        if (!side)
        {
            if (right)
            {
                output[0] = v.X;
                output[1] = Energy(v.Y, v.W);
                output[2] = spread < 1f ? v.Z * (1f - spread) : 0f;
            }
            else
            {
                // The source's left and right branches deliberately have different power factors.
                output[0] = MathF.Sqrt(v.X * v.X + v.Z * v.Z * powerSquared);
                output[1] = v.Y;
                output[2] = v.W * power;
            }
            return;
        }
        float turn = MathF.Abs(h) - MathF.Abs(b);
        float sideFactor = MathF.Cos((turn + 1f) * QuarterPi);
        if (right)
        {
            output[0] = v.X;
            output[2] = v.Z * power;
            float combined = MathF.Sqrt(v.Y * v.Y + v.W * v.W * powerSquared);
            if (b >= 0f) { output[1] = combined; output[7] = corners.W * sideFactor * RootTwo; }
            else { output[1] = combined * sideFactor * RootTwo; output[7] = q.X * q.Y; }
            float remaining = ((((powerSquared - output[0] * output[0]) - output[1] * output[1]) -
                output[2] * output[2]) - output[6] * output[6]) - output[7] * output[7];
            output[5] = Remainder(remaining);
        }
        else
        {
            output[1] = v.Y;
            output[2] = v.W * power;
            float combined = MathF.Sqrt(v.X * v.X + v.Z * v.Z * powerSquared);
            if (b >= 0f) { output[0] = combined; output[6] = corners.Z * sideFactor * RootTwo; }
            else { output[0] = combined * sideFactor * RootTwo; output[6] = q.X * q.Y; }
            float remaining = ((((powerSquared - output[0] * output[0]) - output[1] * output[1]) -
                output[2] * output[2]) - output[6] * output[6]) - output[7] * output[7];
            output[4] = Remainder(remaining);
        }
    }

    private static void EightFullCenter(float[] output, Vector4 q, Vector4 corners,
        float h, float b, bool side, bool right)
    {
        float angle = Math.Clamp((right ? h : h + 1f) * HalfPi, 0f, HalfPi);
        float first = MathF.Max(MathF.Cos(angle) * q.Z, 0f);
        float second = MathF.Max(MathF.Sin(angle) * q.Z, 0f);
        float powerSquared = q.X * q.X + q.Y * q.Y;
        float power = MathF.Sqrt(powerSquared);
        output[2] = (right ? first : second) * power;
        if (!side)
        {
            output[right ? 1 : 0] = (right ? second : first) * power;
            return;
        }
        Vector4 aux = Quad(h, MathF.Abs(h) - MathF.Abs(b), 0f);
        if (right)
        {
            if (b >= 0f)
            {
                output[1] = aux.X * MathF.Sin(angle) * power;
                output[7] = aux.Z * corners.W * RootTwo;
            }
            else
            {
                output[7] = aux.X * q.Y;
                output[1] = aux.Z * second * RootTwo * power;
            }
            float remaining = (((powerSquared - output[1] * output[1]) - output[2] * output[2]) -
                output[6] * output[6]) - output[7] * output[7];
            output[5] = Remainder(remaining);
        }
        else
        {
            if (b >= 0f)
            {
                output[0] = aux.Y * MathF.Cos(angle) * power;
                output[6] = aux.Z * corners.Z * RootTwo;
            }
            else
            {
                output[6] = aux.Y * q.X;
                output[0] = aux.Z * first * RootTwo * power;
            }
            float remaining = (((powerSquared - output[0] * output[0]) - output[2] * output[2]) -
                output[6] * output[6]) - output[7] * output[7];
            output[4] = Remainder(remaining);
        }
    }
}
