using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record SkyFogPacket(int RendererFogMode, int Profile,
    bool FogEnabled, uint SourceFogMode, Vector4 Value, byte[] Bytes,
    bool RequiresMode7NeutralScattering,
    SkyScatteringPacket? ScatteringOverride)
{
    public SkyDirectionalMode4FogReceipt? SourceMode4Proof { get; init; }
}

/// <summary>Final fog parameters for explicit renderer modes. Numeric preparation
/// is independent of selected-light, camera and rendering owner lifetimes.</summary>
public static class SkyFogMath
{
    /// <summary>Directional renderer mode four, profile one. Disabled source
    /// fog ignores its interval. Active nonincreasing intervals use the supplied
    /// renderer fallback, including its original overflow behavior.</summary>
    public static SkyFogPacket GenerateDirectionalMode4(uint sourceMode,
        float start, float end, float intervalFallback)
    {
        Vector4 value = Vector4.Zero;
        if (sourceMode != 0)
        {
            if (!float.IsFinite(start) || !float.IsFinite(end))
                throw new InvalidDataException("Mode4 finalFog input is nonfinite.");
            float ratio = end > start ? Divide(1, Subtract(end, start)) : intervalFallback;
            float product = Multiply(1, start);
            float x = Multiply(1, ratio), y = Multiply(product, ratio);
            y = BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(y) ^ 0x80000000);
            value = new(x, y, 0, 0);
        }
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(0), value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(4), value.Y);
        return new(4, 1, true, sourceMode, value, bytes, false, null);
    }

    public static SkyFogPacket Generate(int explicitRendererFogMode,
        int explicitProfile, bool explicitFogEnabled, ReadOnlySpan<byte> fogRecord96)
    {
        if (explicitRendererFogMode != 7)
            throw new NotSupportedException("Original sky fog supports only explicit renderer fog mode7.");
        return GenerateResolved(explicitProfile,explicitFogEnabled,
            SkyWeatherFogEncoding.DecodeProjection(fogRecord96,explicitProfile,explicitFogEnabled));
    }

    public static SkyFogPacket GenerateWeather(SkyWeatherFogValues fog,int profile,bool enabled)
        => GenerateWeather(7, fog, profile, enabled);

    public static SkyFogPacket GenerateWeather(uint sourceMode, SkyWeatherFogValues fog, int profile, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(fog);
        bool active = enabled && sourceMode != 0;
        if (profile != 1 || active)
        { Finite(fog.Pivot); Finite(fog.Blend); Finite(fog.PlanetRadius); }
        if (profile != 1 && !active) { Finite(fog.Start); Finite(fog.End); }
        return GenerateResolved(profile,enabled,new(sourceMode,fog.Pivot,fog.Blend,fog.PlanetRadius,fog.Start,fog.End));
    }

    private static void Finite(float value)
    { if (!float.IsFinite(value)) throw new InvalidDataException("Nonfinite named mode7 fog input."); }

    private static SkyFogPacket GenerateResolved(int explicitProfile,bool explicitFogEnabled,SkyFogProjectionInputs input)
    {
        const int explicitRendererFogMode=7;
        uint sourceMode = input.Mode;
        bool active = explicitFogEnabled && sourceMode != 0;
        Vector4 value = Vector4.Zero;
        if (explicitProfile != 1 || active)
        {
            float pivot = input.Pivot;
            float minimum = BitConverter.UInt32BitsToSingle(0x37480000);
            // MAXSS selects its second operand on equality, preserving native
            // signed-zero behavior; the finite input contract excludes NaNs.
            float x = pivot > minimum ? pivot : minimum;
            float y = Divide(1, x);
            float z = input.Blend, w = input.PlanetRadius;
            value = new(x, y, z, w);
        }
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(0, 4), value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(4, 4), value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(8, 4), value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12, 4), value.W);
        bool neutral = explicitProfile != 1 && !active;
        SkyScatteringPacket? scatteringOverride = null;
        if (neutral)
        {
            Vector4[] rows = [new(0, 0, 0, input.Start),
                new(0, 0, 0, input.End), Vector4.Zero, new(0, 0, 0, 1)];
            byte[] neutralBytes = new byte[64];
            for (int row = 0; row < rows.Length; row++)
            {
                Vector4 v = rows[row];
                BinaryPrimitives.WriteSingleLittleEndian(neutralBytes.AsSpan(row * 16, 4), v.X);
                BinaryPrimitives.WriteSingleLittleEndian(neutralBytes.AsSpan(row * 16 + 4, 4), v.Y);
                BinaryPrimitives.WriteSingleLittleEndian(neutralBytes.AsSpan(row * 16 + 8, 4), v.Z);
                BinaryPrimitives.WriteSingleLittleEndian(neutralBytes.AsSpan(row * 16 + 12, 4), v.W);
            }
            scatteringOverride = new(7, Array.AsReadOnly(rows), neutralBytes);
        }
        // Native clears context+121 on this branch and marks Scat25 for upload.
        // The caller must use the override instead of Generate7 when requested,
        // and manage pending/dirty state. This pure packet does not reproduce
        // record caching, token2 color writes, or lazy coefficient initialization.
        return new(explicitRendererFogMode, explicitProfile, explicitFogEnabled,
            sourceMode, value, bytes, neutral, scatteringOverride);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Divide(float left, float right) => left / right;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Multiply(float left, float right) => left * right;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Subtract(float left, float right) => left - right;
}
