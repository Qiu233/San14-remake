using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Lens values from an explicitly supplied, type-verified native Camera prefix.</summary>
public readonly record struct OriginalCameraLens(float Near, float Far, float VerticalFovRadians,
    float Aspect, float ViewOffset, float OrthographicScale, uint Flags)
{
    public const int PrefixBytes = 0x160;
    public bool IsOrthographic => (Flags & 1) != 0;
    public bool ReverseDepth => (Flags & 2) != 0;

    public static OriginalCameraLens DecodePrefix(ReadOnlySpan<byte> prefix)
    {
        if (prefix.Length != PrefixBytes)
            throw new InvalidDataException("Original Camera prefix must contain exactly 0x160 bytes.");
        return new(BinaryPrimitives.ReadSingleLittleEndian(prefix[0x140..]),
            BinaryPrimitives.ReadSingleLittleEndian(prefix[0x144..]),
            BinaryPrimitives.ReadSingleLittleEndian(prefix[0x148..]),
            BinaryPrimitives.ReadSingleLittleEndian(prefix[0x14C..]),
            BinaryPrimitives.ReadSingleLittleEndian(prefix[0x150..]),
            BinaryPrimitives.ReadSingleLittleEndian(prefix[0x154..]),
            BinaryPrimitives.ReadUInt32LittleEndian(prefix[0x158..]));
    }

    /// <summary>Native perspective uses literal scale 1; +154 is read only by the orthographic branch.</summary>
    public Matrix4x4 CreatePerspective()
    {
        if (IsOrthographic || (Flags & ~3u) != 0 || ViewOffset != 0)
            throw new InvalidDataException("Camera flags do not select the supported original perspective branch.");
        return TerrainFrustumMath.Perspective(VerticalFovRadians, Aspect, Near, Far,
            reverseDepth: ReverseDepth, projectionScale: 1);
    }
}
