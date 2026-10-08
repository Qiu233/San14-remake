using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record OriginalSkyFogPacket(int RendererFogMode, int Profile,
    bool FogEnabled, uint SourceFogMode, Vector4 Value, byte[] Bytes,
    bool RequiresMode7NeutralScattering,
    OriginalSkyScatteringPacket? ScatteringOverride)
{
    public OriginalSkyDirectionalMode4FogReceipt? SourceMode4Proof { get; init; }
}

/// <summary>Final fog float4 from the original97C1F0 writer for explicit mode7.
/// Profiles are compared exactly as native code: profile1 versus every other
/// explicit integer. No current renderer state or source fog mode is inferred.</summary>
public static class OriginalSkyFogMath
{
    public static OriginalSkyFogPacket Generate(int explicitRendererFogMode,
        int explicitProfile, bool explicitFogEnabled, ReadOnlySpan<byte> fogRecord96)
    {
        if (explicitRendererFogMode != 7)
            throw new NotSupportedException("Original sky fog supports only explicit renderer fog mode7.");
        if (fogRecord96.Length != 96)
            throw new InvalidDataException("Original sky fog requires the complete96-byte source record.");
        uint sourceMode = BinaryPrimitives.ReadUInt32LittleEndian(fogRecord96.Slice(0x10, 4));
        bool active = explicitFogEnabled && sourceMode != 0;
        Vector4 value = Vector4.Zero;
        if (explicitProfile != 1 || active)
        {
            float pivot = Read(fogRecord96, 0x40);
            float minimum = BitConverter.UInt32BitsToSingle(0x37480000);
            // MAXSS selects its second operand on equality, preserving native
            // signed-zero behavior; the finite input contract excludes NaNs.
            float x = pivot > minimum ? pivot : minimum;
            float y = Divide(1, x);
            float z = Read(fogRecord96, 0x44), w = Read(fogRecord96, 0x1C);
            value = new(x, y, z, w);
        }
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(0, 4), value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(4, 4), value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(8, 4), value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12, 4), value.W);
        bool neutral = explicitProfile != 1 && !active;
        OriginalSkyScatteringPacket? scatteringOverride = null;
        if (neutral)
        {
            Vector4[] rows = [new(0, 0, 0, Read(fogRecord96, 0x14)),
                new(0, 0, 0, Read(fogRecord96, 0x18)), Vector4.Zero, new(0, 0, 0, 1)];
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

    private static float Read(ReadOnlySpan<byte> record, int offset)
    {
        float value = BinaryPrimitives.ReadSingleLittleEndian(record.Slice(offset, 4));
        if (!float.IsFinite(value))
            throw new InvalidDataException($"Nonfinite original mode7 fog input at record+{offset:X}.");
        return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Divide(float left, float right) => left / right;
}
