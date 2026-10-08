using System.Buffers.Binary;
using System.IO.Compression;

namespace San14.Data.Formats;

public static class PngWriter
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static void WriteRgba(string path, RgbaTexture texture)
    {
        if (texture.Width < 1 || texture.Height < 1 ||
            texture.Pixels.Length != (long)texture.Width * texture.Height * 4)
            throw new InvalidDataException("RGBA texture dimensions and data length differ.");
        using var output = File.Create(path);
        output.Write(Signature);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, texture.Width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], texture.Height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(output, "IHDR"u8, header);

        using var raw = new MemoryStream();
        int rowLength = checked(texture.Width * 4);
        for (int y = 0; y < texture.Height; y++)
        {
            raw.WriteByte(0); // PNG filter None
            raw.Write(texture.Pixels, y * rowLength, rowLength);
        }
        raw.Position = 0;
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            raw.CopyTo(zlib);
        WriteChunk(output, "IDAT"u8, compressed.ToArray());
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> payload)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, payload.Length);
        output.Write(word);
        output.Write(type);
        output.Write(payload);
        uint crc = 0xFFFF_FFFF;
        foreach (byte value in type) crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
        foreach (byte value in payload) crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
        BinaryPrimitives.WriteUInt32BigEndian(word, ~crc);
        output.Write(word);
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            uint value = n;
            for (int k = 0; k < 8; k++)
                value = (value & 1) == 0 ? value >> 1 : 0xEDB8_8320 ^ (value >> 1);
            table[n] = value;
        }
        return table;
    }
}
