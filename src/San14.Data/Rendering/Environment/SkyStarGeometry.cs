using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

public sealed record SkyStarGeometry(uint PointCount, ReadOnlyMemory<byte> VertexBytes,
    string SourceResourceUri, string SourceSha256, string VertexPayloadSha256)
{
    public const uint Stride = 20;

    /// <summary>Original 11771B0 _DVS2000 reader. Retains the entire point
    /// stream; the proven layout is XYZ float32, PSIZE float32, RGBA8 UNorm.</summary>
    public static SkyStarGeometry Read(LoadedRdbAsset source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Bytes.Length < 12 || !source.Bytes.AsSpan(0, 8).SequenceEqual("_DVS2000"u8) ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != source.DecodedSha256)
            throw new InvalidDataException("Star source signature/version or retained hash differs.");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan(8, 4));
        if (12L + (long)count * Stride != source.Bytes.Length)
            throw new InvalidDataException("Star source count does not cover the complete 20-byte stream.");
        return ReadExplicit(source.Bytes.AsMemory(12), count, source.ResourceUri, source.DecodedSha256);
    }

    /// <summary>Explicit point stream using the proven source layout, including
    /// diagnostic streams. The caller must identify its provenance.</summary>
    public static SkyStarGeometry ReadExplicit(ReadOnlyMemory<byte> bytes, uint pointCount,
        string sourceResourceUri, string sourceSha256)
    {
        if ((long)pointCount * Stride != bytes.Length || pointCount > int.MaxValue / Stride ||
            string.IsNullOrWhiteSpace(sourceResourceUri) || sourceSha256.Length != 64 ||
            sourceSha256.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("Star point count/layout or explicit provenance is invalid.");
        byte[] retained = bytes.ToArray();
        for (int at = 0; at < retained.Length; at += (int)Stride)
            for (int component = 0; component < 4; component++)
                if (!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(retained.AsSpan(at + component * 4, 4))))
                    throw new InvalidDataException("Star position or PSIZE is outside the supported finite domain.");
        return new(pointCount, retained, sourceResourceUri, sourceSha256.ToUpperInvariant(),
            Convert.ToHexString(SHA256.HashData(retained)));
    }
}
