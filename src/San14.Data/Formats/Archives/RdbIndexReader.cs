using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace San14.Data.Formats;

public sealed record RdbAddress(long Offset, long Length, int? BinId, int? BinSubId,
    string? ExternalPath);

public sealed record RdbRecord(int Index, uint FileId, uint TypeId, uint Flags,
    long DecodedSize, RdbAddress? Address)
{
    // The variable header includes loader parameter descriptors and values.
    // Retain it even when the payload consumer does not interpret those options.
    public byte[] LoaderMetadata { get; init; } = [];
}

public sealed record RdbIndex(string DataDirectory, uint NameDatabaseId,
    IReadOnlyList<RdbRecord> Records);

/// <summary>Reads RDB index metadata. Payloads are handled separately and never inferred.</summary>
public static class RdbIndexReader
{
    private const int HeaderSize = 24;
    private const int RecordSize = 48;

    public static RdbIndex Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize || !data[..8].SequenceEqual("_DRK0000"u8))
            throw new InvalidDataException("RDB index header is missing.");
        int headerLength = ReadInt(data, 8);
        int count = ReadInt(data, 16);
        if (headerLength < HeaderSize || headerLength > data.Length || count < 0 ||
            count > (data.Length - headerLength) / RecordSize)
            throw new InvalidDataException("RDB header length or record count is invalid.");

        uint nameDatabaseId = ReadUInt(data, 20);
        ReadOnlySpan<byte> directoryBytes = data.Slice(HeaderSize, headerLength - HeaderSize);
        int nullAt = directoryBytes.IndexOf((byte)0);
        string directory = Encoding.UTF8.GetString(nullAt < 0 ? directoryBytes : directoryBytes[..nullAt]);
        var records = new List<RdbRecord>(count);
        int position = headerLength;
        var ids = new HashSet<uint>();
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> remaining = data[position..];
            if (remaining.Length < RecordSize || !remaining[..8].SequenceEqual("IDRK0000"u8))
                throw new InvalidDataException($"RDB record {index} has an invalid header.");
            long size = ReadLong(remaining, 8);
            long contentSize = ReadLong(remaining, 16);
            long decodedSize = ReadLong(remaining, 24);
            if (size < RecordSize || size > remaining.Length || contentSize < 0 ||
                contentSize > size - RecordSize || decodedSize < 0)
                throw new InvalidDataException($"RDB record {index} has invalid size fields.");
            uint id = ReadUInt(remaining, 36);
            if (!ids.Add(id))
                throw new InvalidDataException($"RDB file ID {id:X8} occurs more than once.");
            ReadOnlySpan<byte> content = remaining.Slice((int)(size - contentSize), (int)contentSize);
            RdbAddress? address = contentSize == 0 ? null : ParseAddress(content, index);
            records.Add(new RdbRecord(index, id, ReadUInt(remaining, 40),
                ReadUInt(remaining, 44), decodedSize, address)
            {
                LoaderMetadata = remaining.Slice(RecordSize, checked((int)(size - contentSize - RecordSize))).ToArray()
            });
            position = checked(position + (int)((size + 3) & ~3));
            if (position > data.Length)
                throw new InvalidDataException($"RDB record {index} exceeds the index file.");
        }
        if (position != data.Length)
            throw new InvalidDataException("RDB index has trailing or missing record bytes.");
        return new RdbIndex(directory, nameDatabaseId, records);
    }

    private static RdbAddress ParseAddress(ReadOnlySpan<byte> bytes, int index)
    {
        int nullAt = bytes.IndexOf((byte)0);
        if (nullAt >= 0) bytes = bytes[..nullAt];
        string value = Encoding.ASCII.GetString(bytes);
        int at = value.IndexOf('@');
        if (at < 1) throw new InvalidDataException($"RDB record {index} has an invalid address.");
        long offset = ParseHex(value[..at], index);
        int cursor = at + 1;
        long length = ParseHex(ReadPart(value, ref cursor), index);
        int? binId = null;
        int? subId = null;
        string? path = null;
        if (cursor < value.Length && value[cursor] == '#')
        {
            cursor++;
            binId = checked((int)ParseHex(ReadPart(value, ref cursor), index));
        }
        if (cursor < value.Length && value[cursor] == '&')
        {
            cursor++;
            subId = checked((int)ParseHex(ReadPart(value, ref cursor), index));
        }
        if (cursor < value.Length && value[cursor] == '?')
            path = value[(cursor + 1)..];
        else if (cursor != value.Length)
            throw new InvalidDataException($"RDB record {index} has an invalid address suffix.");
        return new RdbAddress(offset, length, binId, subId, path);
    }

    private static string ReadPart(string value, ref int cursor)
    {
        int start = cursor;
        while (cursor < value.Length && value[cursor] is not ('#' or '&' or '?')) cursor++;
        return value[start..cursor];
    }

    private static long ParseHex(string text, int index)
    {
        if (text.Length == 0 || !long.TryParse(text, NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out long value) || value < 0)
            throw new InvalidDataException($"RDB record {index} has an invalid hex address.");
        return value;
    }

    private static int ReadInt(ReadOnlySpan<byte> data, int at) =>
        BinaryPrimitives.ReadInt32LittleEndian(data.Slice(at, 4));
    private static uint ReadUInt(ReadOnlySpan<byte> data, int at) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(at, 4));
    private static long ReadLong(ReadOnlySpan<byte> data, int at) =>
        BinaryPrimitives.ReadInt64LittleEndian(data.Slice(at, 8));
}
