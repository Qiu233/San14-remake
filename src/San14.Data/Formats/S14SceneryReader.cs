using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14SceneryDefinition(int Slot, string Name, string SecondaryText,
    ushort RawHex44, byte RawField46, byte RawField48, byte[] RawBytes);

public sealed record S14SceneryCatalog(uint FormatMarker, int RecordOffset, int RecordStride,
    string DecodedSha256, IReadOnlyList<S14SceneryDefinition> Sceneries);

public sealed record S14SceneryStateCatalog(int RecordOffset, string DecodedSha256,
    IReadOnlyList<byte> Flags47);

/// <summary>CE CSceneryData fixed fields and separate scenario flags. This does
/// not infer activation, current ownership, UI readiness or later startup state.</summary>
public static class S14SceneryReader
{
    public const int Count = 101, Stride = 56, Start = 355702;

    public static S14SceneryCatalog ReadFixed(S14Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ReadOnlySpan<byte> source = document.DecodedPayload;
        int end = Start + Count * Stride;
        if (document.FormatMarker != 0x66 || source.Length < end + 8 ||
            I32(source, 0) != 0x66 || I32(source, Start - 8) != 38 || I32(source, Start - 4) != Count ||
            I32(source, end) != 39 || I32(source, end + 4) != 7)
            throw new InvalidDataException("Scenery definitions require the supported CE fixed section38/101/56-byte profile.");
        var rows = new S14SceneryDefinition[Count];
        for (int slot = 0; slot < rows.Length; slot++)
        {
            ReadOnlySpan<byte> row = source.Slice(Start + slot * Stride, Stride);
            rows[slot] = new(slot, Text(row[..14]), Text(row.Slice(14,38)),
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(52,2)), row[54], row[55], row.ToArray());
        }
        return new(document.FormatMarker, Start, Stride,
            Convert.ToHexString(SHA256.HashData(source)), Array.AsReadOnly(rows));
    }

    public static S14SceneryStateCatalog ReadState(S14ScenarioWorldDocument world)
    {
        ArgumentNullException.ThrowIfNull(world);
        S14ScenarioStateTable[] tables = world.SourceBoundary?.StateTables.Where(table => table.Section == 38).ToArray()
            ?? throw new InvalidDataException("Scenario scenery flags require a forward world source boundary.");
        if (tables.Length != 1 || tables[0] is not { RawCount:Count, RecordBytes:1 } table ||
            table.RawBytes.Length != Count || table.RawSha256 != Convert.ToHexString(SHA256.HashData(table.RawBytes)))
            throw new InvalidDataException("Scenario scenery section38 requires exactly101 one-byte flags with retained source bytes.");
        return new(table.RecordOffset, world.DecodedSha256, Array.AsReadOnly((byte[])table.RawBytes.Clone()));
    }

    private static int I32(ReadOnlySpan<byte> source, int offset) => BinaryPrimitives.ReadInt32LittleEndian(source[offset..]);
    private static string Text(ReadOnlySpan<byte> source)
    {
        string text = Encoding.Unicode.GetString(source);
        int terminator = text.IndexOf('\0');
        return terminator < 0 ? text : text[..terminator];
    }
}
