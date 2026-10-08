using System.Buffers.Binary;
using System.IO.Compression;

namespace San14.Data.Formats;

/// <summary>Reads one asset from the installed game's RDB containers without writing to them.</summary>
public static class RdbArchiveReader
{
    private const int EntryHeaderSize = 48;
    private const long MaximumAssetSize = 512L * 1024 * 1024;

    public static string ContainerPath(string rdbPath, RdbIndex index, RdbRecord record)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(rdbPath))!;
        if ((record.Flags & 0x0001_0000) != 0)
        {
            string filename = record.Address?.ExternalPath ?? $"0x{record.FileId:x8}.file";
            string dataDirectory = index.DataDirectory.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(filename) || filename.Contains("..", StringComparison.Ordinal) ||
                Path.IsPathRooted(dataDirectory) || dataDirectory.Contains("..", StringComparison.Ordinal))
                throw new InvalidDataException("RDB external path is unsafe.");
            return Path.Combine(directory, dataDirectory, filename);
        }
        if ((record.Flags & 0x0002_0000) == 0 || record.Address is null)
            throw new InvalidDataException($"RDB entry {record.Index} has no supported container.");
        string path = rdbPath + ".bin";
        if (record.Address.BinId is int binId) path += binId.ToString();
        if (record.Address.BinSubId is int subId) path += $"_{subId}";
        return path;
    }

    public static byte[] ReadPayload(string rdbPath, RdbIndex index, RdbRecord record)
    {
        string container = ContainerPath(rdbPath, index, record);
        if (!File.Exists(container))
            throw new FileNotFoundException($"RDB container for asset {record.FileId:X8} is missing.", container);
        byte[] blob;
        using (var input = new FileStream(container, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            RdbAddress? address = record.Address;
            long offset = address?.Offset ?? 0;
            long length = address?.Length ?? input.Length;
            if (length < EntryHeaderSize || length > int.MaxValue || offset < 0 ||
                offset > input.Length || length > input.Length - offset)
                throw new InvalidDataException($"RDB asset {record.FileId:X8} points outside its container.");
            input.Position = offset;
            blob = new byte[(int)length];
            input.ReadExactly(blob);
        }

        ReadOnlySpan<byte> span = blob;
        if (span.Length < EntryHeaderSize || !span[..8].SequenceEqual("IDRK0000"u8))
            throw new InvalidDataException($"RDB asset {record.FileId:X8} has an invalid inner header.");
        long entrySize = BinaryPrimitives.ReadInt64LittleEndian(span[8..16]);
        long contentSize = BinaryPrimitives.ReadInt64LittleEndian(span[16..24]);
        long decodedSize = BinaryPrimitives.ReadInt64LittleEndian(span[24..32]);
        uint fileId = BinaryPrimitives.ReadUInt32LittleEndian(span[36..40]);
        if (fileId != record.FileId || entrySize < EntryHeaderSize || entrySize > span.Length ||
            contentSize < 0 || contentSize > entrySize - EntryHeaderSize ||
            decodedSize != record.DecodedSize || decodedSize > MaximumAssetSize)
            throw new InvalidDataException($"RDB asset {record.FileId:X8} disagrees with its index.");
        ReadOnlySpan<byte> content = span.Slice((int)(entrySize - contentSize), (int)contentSize);

        uint compression = record.Flags & 0x00F0_0000;
        return compression switch
        {
            0 => ReadPlain(content, decodedSize),
            0x0010_0000 => InflateChunks(content, decodedSize),
            _ => throw new InvalidDataException($"Unsupported RDB compression flags {record.Flags:X8}.")
        };
    }

    private static byte[] ReadPlain(ReadOnlySpan<byte> bytes, long expected)
    {
        if (bytes.Length != expected)
            throw new InvalidDataException("Plain RDB payload length differs from the index.");
        return bytes.ToArray();
    }

    private static byte[] InflateChunks(ReadOnlySpan<byte> compressed, long expected)
    {
        using var output = new MemoryStream((int)expected);
        int position = 0;
        bool terminated = false;
        byte[] buffer = new byte[16 * 1024];
        while (position + 4 <= compressed.Length)
        {
            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(compressed.Slice(position, 4));
            position += 4;
            if (chunkSize == 0)
            {
                terminated = true;
                break;
            }
            if (chunkSize < 2 || chunkSize > compressed.Length - position)
                throw new InvalidDataException("RDB zlib chunk extends beyond the payload.");
            using var input = new MemoryStream(compressed.Slice(position, chunkSize).ToArray());
            using var inflater = new ZLibStream(input, CompressionMode.Decompress);
            int read;
            while ((read = inflater.Read(buffer)) != 0)
            {
                if (read > expected - output.Length)
                    throw new InvalidDataException("RDB decompressed data exceeds the indexed size.");
                output.Write(buffer, 0, read);
            }
            position += chunkSize;
        }
        if (!terminated || position != compressed.Length || output.Length != expected)
            throw new InvalidDataException("RDB zlib stream length differs from the index.");
        return output.ToArray();
    }
}
