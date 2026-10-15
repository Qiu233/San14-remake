using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record SkyScatteringPacket(int RendererFogMode,
    IReadOnlyList<Vector4> Values, byte[] Bytes);

/// <summary>The original 97CCE0 shared Scat writer for an explicitly selected
/// renderer fog mode7. Inputs are the original96-byte fog record and an explicit
/// directional RGB; neither an active mode nor a current light is inferred.</summary>
public static class SkyScatteringMath
{
    public static SkyScatteringPacket Generate(int explicitRendererFogMode,
        ReadOnlySpan<byte> fogRecord96, Vector3 explicitDirectionalRgb)
    {
        if (explicitRendererFogMode != 7)
            throw new NotSupportedException("Original sky scattering supports only explicit renderer fog mode7.");
        if (fogRecord96.Length != 96)
            throw new InvalidDataException("Original sky scattering requires the complete96-byte fog record.");
        RequireFiniteDirectionalColor(explicitDirectionalRgb);
        float start = Read(fogRecord96, 0x14), end = Read(fogRecord96, 0x18);
        float ax = Read(fogRecord96, 0x20), ay = Read(fogRecord96, 0x24), az = Read(fogRecord96, 0x28),
            scale = Read(fogRecord96, 0x2C);
        float bx = Read(fogRecord96, 0x30), by = Read(fogRecord96, 0x34), bz = Read(fogRecord96, 0x38),
            g = Read(fogRecord96, 0x3C);

        return GenerateResolved(start, end, ax, ay, az, scale, bx, by, bz, g, explicitDirectionalRgb);
    }

    public static SkyScatteringPacket GenerateWeather(SkyWeatherFogValues fog, Vector3 explicitDirectionalRgb)
    {
        ArgumentNullException.ThrowIfNull(fog);
        RequireFiniteDirectionalColor(explicitDirectionalRgb);
        return GenerateResolved(Finite(fog.Start), Finite(fog.End),
            Finite(fog.RayleighCoefficients.X), Finite(fog.RayleighCoefficients.Y), Finite(fog.RayleighCoefficients.Z),
            Finite(fog.Density), Finite(fog.MieCoefficients.X), Finite(fog.MieCoefficients.Y),
            Finite(fog.MieCoefficients.Z), Finite(fog.Anisotropy), explicitDirectionalRgb);
    }

    private static SkyScatteringPacket GenerateResolved(float start, float end,
        float ax, float ay, float az, float scale, float bx, float by, float bz, float g, Vector3 explicitDirectionalRgb)
    {
        const int explicitRendererFogMode = 7;

        // Native branch tests each first coefficient against zero, then divides
        // by that component's combined coefficient. Keep these as separate
        // binary32 operations; do not contract the later products into FMA.
        float x = Add(ax, bx), y = Add(by, ay), z = Add(bz, az);
        if (ax > 0) x = Div(scale, x);
        if (ay > 0) y = Div(scale, y);
        if (az > 0) z = Div(scale, z);
        y = Mul(y, explicitDirectionalRgb.Y);
        x = Mul(x, explicitDirectionalRgb.X);
        z = Mul(z, explicitDirectionalRgb.Z);

        float gSquared = Mul(g, g);
        float negativeTwiceG = Mul(g, F(0xC0000000));
        float phase = Mul(Sub(F(0x3F800000), gSquared), F(0x3DA2F983));
        float phaseW = Add(gSquared, F(0x3F800000));
        float secondX = Mul(phase, bx), secondY = Mul(phase, by), secondZ = Mul(phase, bz);
        secondX = Mul(x, secondX);
        secondY = Mul(y, secondY);
        secondZ = Mul(z, secondZ);
        float firstX = Mul(ax, F(0x3D747645)), firstY = Mul(ay, F(0x3D747645)),
            firstZ = Mul(az, F(0x3D747645));
        firstX = Mul(x, firstX);
        firstY = Mul(y, firstY);
        firstZ = Mul(z, firstZ);

        Vector4[] rows = [new(ax, ay, az, start), new(bx, by, bz, end),
            new(firstX, firstY, firstZ, negativeTwiceG), new(secondX, secondY, secondZ, phaseW)];
        byte[] bytes = new byte[64];
        for (int row = 0; row < rows.Length; row++)
        {
            Vector4 value = rows[row];
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(row * 16, 4), value.X);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(row * 16 + 4, 4), value.Y);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(row * 16 + 8, 4), value.Z);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(row * 16 + 12, 4), value.W);
        }
        return new(explicitRendererFogMode, Array.AsReadOnly(rows), bytes);
    }

    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    private static void RequireFiniteDirectionalColor(Vector3 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            throw new InvalidDataException("Original sky scattering requires explicit finite directional RGB.");
    }
    private static float Finite(float value)
    {
        if (!float.IsFinite(value)) throw new InvalidDataException("Nonfinite named sky scattering input.");
        return value;
    }
    private static float Read(ReadOnlySpan<byte> record, int offset)
    {
        float value = BinaryPrimitives.ReadSingleLittleEndian(record.Slice(offset, 4));
        if (!float.IsFinite(value))
            throw new InvalidDataException($"Nonfinite original sky scattering input at record+{offset:X}.");
        return value;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Sub(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Mul(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Div(float a, float b) => a / b;
}
