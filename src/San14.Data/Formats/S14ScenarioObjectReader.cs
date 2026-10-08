using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data.Formats;

/// <summary>Static source fields of CObjectData; physical slot is not the raw +10 value.</summary>
public sealed record S14ScenarioObjectRecord(int PhysicalSlot, int Offset, ushort RawField10,
    byte RawField12, byte RawField13, ushort RawField14, byte[] RawBytes);

/// <summary>
/// SourceSha256 hashes the decoded scenario payload; ObjectsSha256 hashes only
/// the retained six-byte rows. These records do not describe first-player state.
/// </summary>
public sealed record S14ScenarioObjectCatalog(int MarkerOffset, int RecordOffset,
    int RecordStride, IReadOnlyList<S14ScenarioObjectRecord> Objects,
    byte[] RawBytes, string SourceSha256, string ObjectsSha256);

/// <summary>
/// Reads CE section 7 by advancing from the verified scenario area-table end.
/// The canonical count is a supported source profile, not a Core capacity or
/// an original native count-equality check. Unknown raw values are retained.
/// </summary>
public static class S14ScenarioObjectReader
{
    public const int ObjectRecordBytes = 6;
    public const int SupportedObjectCount = 3001;
    public const int SupportedLeadingIdCount = 1852;

    public static S14ScenarioObjectCatalog Read(S14Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.FormatMarker != 0x66)
            throw new InvalidDataException("Scenario object source requires the inspected CE profile.");
        ReadOnlySpan<byte> source = document.DecodedPayload;
        if (source.Length < 8 || U32(source, 0) != document.FormatMarker ||
            document.LeadingRecordCount is not int count || count != SupportedLeadingIdCount ||
            BinaryPrimitives.ReadInt32LittleEndian(source[4..]) != count ||
            8L + count * 2L > source.Length)
            throw new InvalidDataException("Scenario object source has an invalid decoded scenario header.");

        // Reuse the established area parser's bounds, markers and references.
        // The end is derived from that same CE layout, never located by search.
        S14ScenarioTables.ReadScenarioAreas(document);
        int markerOffset = S14ScenarioTables.ScenarioAreaEndOffset(document);
        long recordOffset = (long)markerOffset + 8;
        long end = recordOffset + (long)SupportedObjectCount * ObjectRecordBytes;
        if (end + 4 > source.Length ||
            I32(source, markerOffset) != 7 || I32(source, markerOffset + 4) != SupportedObjectCount)
            throw new InvalidDataException("Scenario object section 7 count or source window differs from the supported CE profile.");
        if (I32(source, (int)end) != 8)
            throw new InvalidDataException("Scenario object source is missing its section 8 boundary.");

        var objects = new S14ScenarioObjectRecord[SupportedObjectCount];
        for (int slot = 0; slot < objects.Length; slot++)
        {
            int offset = (int)recordOffset + slot * ObjectRecordBytes;
            ReadOnlySpan<byte> row = source.Slice(offset, ObjectRecordBytes);
            objects[slot] = new(slot, offset, BinaryPrimitives.ReadUInt16LittleEndian(row),
                row[2], row[3], BinaryPrimitives.ReadUInt16LittleEndian(row[4..]), row.ToArray());
        }
        byte[] rows = source.Slice((int)recordOffset, (int)(end - recordOffset)).ToArray();
        return new(markerOffset, (int)recordOffset, ObjectRecordBytes, Array.AsReadOnly(objects),
            rows, Sha(source), Sha(rows));
    }

    private static int I32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
    private static uint U32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
