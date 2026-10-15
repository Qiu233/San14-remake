using System.Buffers.Binary;

namespace San14.Data;

/// <summary>An imported, immutable controller point stream. Device draws and
/// media exporters borrow the same owner and copy bytes only for upload.</summary>
public sealed class SkyStarGeometry
{
    public const uint Stride = 20;
    private readonly byte[] vertices;
    public uint PointCount { get; }
    public int ByteLength => vertices.Length;
    public string SourceResourceUri { get; }
    public string SourceSha256 { get; }

    private SkyStarGeometry(uint pointCount, byte[] vertices, string sourceResourceUri, string sourceSha256)
        => (PointCount, this.vertices, SourceResourceUri, SourceSha256) =
            (pointCount, vertices, sourceResourceUri, sourceSha256);

    public byte[] CopyVertexBytes() => vertices.ToArray();

    /// <summary>Original 11771B0 _DVS2000 reader. Retains the entire point
    /// stream; the proven layout is XYZ float32, PSIZE float32, RGBA8 UNorm.</summary>
    public static SkyStarGeometry Read(LoadedRdbAsset source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Bytes.Length < 12 || !source.Bytes.AsSpan(0, 8).SequenceEqual("_DVS2000"u8))
            throw new InvalidDataException("Star source signature/version is invalid.");
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
        return new(pointCount, retained, sourceResourceUri, sourceSha256.ToUpperInvariant());
    }
}
