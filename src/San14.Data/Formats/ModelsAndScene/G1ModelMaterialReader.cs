using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public sealed record G1TextureEntry(int Index, ushort RawValue0, ushort RawValue2,
    ushort RawValue4, ushort RawValue6, ushort RawValue8, ushort RawValue10, byte[] RawBytes);
public sealed record G1TextureRecord(int Index, uint UnknownValue0, uint UnknownValue8,
    uint UnknownValue12, IReadOnlyList<G1TextureEntry> TextureEntries, byte[] RawHeader);
public sealed record G1NamedParameter(int Index, int Length, string Name, byte[] NameBytes,
    uint UnknownValue8, ushort TypeCode, ushort ElementCount, int? VerifiedElementByteWidth,
    byte[] RawValueBytes, byte[] RawHeader);
public sealed record G1ParameterBlock(int Index, IReadOnlyList<G1NamedParameter> Parameters);
public sealed record G1Section10001Record(int Index, byte[] RawBytes);
public sealed record G1ModelMaterialRecords(IReadOnlyList<G1TextureRecord> TextureRecords,
    IReadOnlyList<G1ParameterBlock> ParameterBlocks,
    IReadOnlyList<G1Section10001Record> Section10001Records, byte[] RawSection10001Tail)
{
    /// <summary>Original parser AB90E0 reads this four-byte word after the item count,
    /// before the 64-byte records. Its higher-level meaning remains raw.</summary>
    public byte[] RawSection10001ExtraHeader { get; init; } = [];
}

/// <summary>
/// Reads verified boundaries of GM1G4400 sections 0x10001, 0x10002 and 0x10003.
/// Texture entry codes, parameter values and 0x10001 records remain raw. This does
/// not establish draw-to-material, texture-to-RDB or shader execution semantics.
/// </summary>
public static class G1ModelMaterialReader
{
    public static G1ModelMaterialRecords Read(G1ModelGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        G1GeometrySection s1 = Section(geometry, 0x10001);
        G1GeometrySection s2 = Section(geometry, 0x10002);
        G1GeometrySection s3 = Section(geometry, 0x10003);
        return new(ReadTextures(s2), ReadParameters(s3), ReadRawRecords(s1), [])
        { RawSection10001ExtraHeader = s1.RawBytes.AsSpan(12, 4).ToArray() };
    }

    private static IReadOnlyList<G1TextureRecord> ReadTextures(G1GeometrySection section)
    {
        ReadOnlySpan<byte> data = section.RawBytes;
        if (section.ItemCount > (data.Length - 12) / 16)
            throw Failure(section.Id, "record count exceeds the section");
        var records = new List<G1TextureRecord>(section.ItemCount);
        int position = 12;
        for (int i = 0; i < section.ItemCount; i++)
        {
            Require(data, position, 16, section.Id, $"record {i} header");
            ReadOnlySpan<byte> header = data.Slice(position, 16);
            int count = Number(header, 4, section.Id, $"record {i} entry count");
            position += 16;
            if (count > (data.Length - position) / 12)
                throw Failure(section.Id, $"record {i} entry count exceeds the section");
            var entries = new List<G1TextureEntry>(count);
            for (int j = 0; j < count; j++)
            {
                ReadOnlySpan<byte> entry = data.Slice(position, 12);
                entries.Add(new(j, Short(entry, 0), Short(entry, 2), Short(entry, 4),
                    Short(entry, 6), Short(entry, 8), Short(entry, 10), entry.ToArray()));
                position += 12;
            }
            records.Add(new(i, UInt(header, 0), UInt(header, 8), UInt(header, 12), entries, header.ToArray()));
        }
        if (position != data.Length) throw Failure(section.Id, "unindexed trailing bytes");
        return records;
    }

    private static IReadOnlyList<G1ParameterBlock> ReadParameters(G1GeometrySection section)
    {
        ReadOnlySpan<byte> data = section.RawBytes;
        if (section.ItemCount > (data.Length - 12) / 4)
            throw Failure(section.Id, "block count exceeds the section");
        var blocks = new List<G1ParameterBlock>(section.ItemCount);
        int position = 12;
        for (int i = 0; i < section.ItemCount; i++)
        {
            Require(data, position, 4, section.Id, $"block {i} count");
            int count = Number(data, position, section.Id, $"block {i} parameter count");
            position += 4;
            if (count > (data.Length - position) / 16)
                throw Failure(section.Id, $"block {i} parameter count exceeds the section");
            var parameters = new List<G1NamedParameter>(count);
            for (int j = 0; j < count; j++)
            {
                string context = $"block {i} parameter {j}";
                Require(data, position, 16, section.Id, context + " header");
                ReadOnlySpan<byte> header = data.Slice(position, 16);
                int length = Number(header, 0, section.Id, context + " length");
                int nameLength = Number(header, 4, section.Id, context + " name length");
                if (length < 16 || nameLength <= 0 || nameLength % 4 != 0 || nameLength > length - 16)
                    throw Failure(section.Id, context + " has invalid record/name boundaries");
                Require(data, position, length, section.Id, context);
                ReadOnlySpan<byte> nameBytes = data.Slice(position + 16, nameLength);
                string name = ReadName(nameBytes, section.Id, context);
                ushort type = Short(header, 12), elements = Short(header, 14);
                int valueLength = length - 16 - nameLength;
                // Only byte widths observed in all 285 local sources are asserted.
                // Other codes keep their explicit sized raw payload without interpretation.
                int? width = type switch { 1 => 4, 2 => 8, 4 => 16, 5 => 4, _ => null };
                if (width.HasValue && (long)elements * width.Value != valueLength)
                    throw Failure(section.Id, context + " element count does not match its value length");
                parameters.Add(new(j, length, name, nameBytes.ToArray(), UInt(header, 8), type,
                    elements, width, data.Slice(position + 16 + nameLength, valueLength).ToArray(), header.ToArray()));
                position += length;
            }
            blocks.Add(new(i, parameters));
        }
        if (position != data.Length) throw Failure(section.Id, "unindexed trailing bytes");
        return blocks;
    }

    private static IReadOnlyList<G1Section10001Record> ReadRawRecords(G1GeometrySection section)
    {
        if (section.RawBytes.Length != 16L + section.ItemCount * 64L)
            throw Failure(section.Id, "unsupported raw record size or extra header length");
        var result = new List<G1Section10001Record>(section.ItemCount);
        for (int i = 0; i < section.ItemCount; i++)
            result.Add(new(i, section.RawBytes.AsSpan(16 + i * 64, 64).ToArray()));
        return result;
    }

    private static G1GeometrySection Section(G1ModelGeometry geometry, uint id)
    {
        G1GeometrySection[] matches = geometry.Sections.Where(s => s.Id == id).ToArray();
        if (matches.Length != 1) throw Failure(id, "missing or repeated section");
        G1GeometrySection section = matches[0];
        ReadOnlySpan<byte> bytes = section.RawBytes;
        if (bytes.Length < 12 || bytes.Length != section.Length || section.ItemCount < 0 ||
            UInt(bytes, 0) != id || UInt(bytes, 4) != section.Length || UInt(bytes, 8) != section.ItemCount)
            throw Failure(id, "section metadata disagrees with its original header");
        return section;
    }

    private static string ReadName(ReadOnlySpan<byte> bytes, uint id, string context)
    {
        int end = bytes.IndexOf((byte)0);
        if (end <= 0) throw Failure(id, context + " name is empty or lacks a terminator");
        foreach (byte value in bytes[..end])
            if (value is < 33 or > 126) throw Failure(id, context + " name is not printable ASCII");
        foreach (byte value in bytes[end..])
            if (value != 0) throw Failure(id, context + " name contains data after its terminator");
        return Encoding.ASCII.GetString(bytes[..end]);
    }

    private static int Number(ReadOnlySpan<byte> data, int offset, uint id, string context)
    {
        uint value = UInt(data, offset);
        if (value > int.MaxValue) throw Failure(id, context + " exceeds the supported integer range");
        return (int)value;
    }
    private static void Require(ReadOnlySpan<byte> data, int offset, int length, uint id, string context)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw Failure(id, context + " is truncated");
    }
    private static uint UInt(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    private static ushort Short(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
    private static InvalidDataException Failure(uint id, string message) => new($"G1M section 0x{id:X5}: {message}.");
}
