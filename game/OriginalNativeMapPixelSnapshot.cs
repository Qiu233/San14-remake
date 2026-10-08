using System.Buffers.Binary;
using Godot;

/// <summary>Detached bytes from one accepted presentation. BMP export writes
/// the original extent and BGRA channels without scaling or compression.</summary>
public sealed class OriginalNativeMapPixelSnapshot
{
    private readonly byte[] bgra;
    internal OriginalNativeMapPixelSnapshot(long frame, Vector2I extent, byte[] pixels, string provenance)
    {
        if (frame <= 0 || extent.X <= 0 || extent.Y <= 0 ||
            pixels.LongLength != checked((long)extent.X * extent.Y * 4))
            throw new InvalidDataException("Accepted pixel snapshot has an invalid frame or extent.");
        FrameNumber = frame; Extent = extent; bgra = pixels.ToArray(); SourceProvenance = provenance;
    }
    public long FrameNumber { get; }
    public Vector2I Extent { get; }
    public string SourceProvenance { get; }
    public ReadOnlyMemory<byte> Bgra32 => bgra.ToArray();

    public void WriteBmp(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        // BITMAPV5HEADER: top-down 32-bit pixels with explicit RGBA masks.
        // BI_BITFIELDS specifies channel layout; pixel rows remain uncompressed.
        byte[] header = new byte[138];
        void U(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(offset, 4), value);
        header[0] = (byte)'B'; header[1] = (byte)'M';
        U(2, checked((uint)(header.Length + bgra.Length))); U(10, 138); U(14, 124);
        U(18, checked((uint)Extent.X)); U(22, unchecked((uint)-Extent.Y));
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(28, 2), 32);
        U(30, 3); U(34, checked((uint)bgra.Length));
        U(54, 0x00FF0000); U(58, 0x0000FF00); U(62, 0x000000FF); U(66, 0xFF000000);
        U(70, 0x73524742); // sRGB; preserve the already presented channel bytes.
        using var output = new FileStream(Path.GetFullPath(path), FileMode.Create, System.IO.FileAccess.Write, FileShare.None);
        output.Write(header); output.Write(bgra);
    }
}
