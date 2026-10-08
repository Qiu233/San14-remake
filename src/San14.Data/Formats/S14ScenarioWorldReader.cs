using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace San14.Data.Formats;

/// <summary>World fields at the static-source stage, not final first-player initialization.</summary>
public sealed record S14ScenarioWorldDocument(int Offset, ushort RawEmperorPersonId,
    short RawCurrentYear, int RawModeValue, int RawPastGameTime,
    byte[] RawBytes, string DecodedSha256, string WorldSha256)
{
    // RawCurrentYear is retained as the serialized legacy field name. The
    // original +2E date has a distinct use from the season-consumed +34 date.
    [JsonIgnore]
    public short RawWorldField2E => RawCurrentYear;
    [JsonIgnore]
    public S14ScenarioSourceDate RawSecondDate => S14ScenarioSourceDate.Read(RawBytes, 33);
    [JsonIgnore]
    public S14ScenarioSourceDate RawThirdDate => S14ScenarioSourceDate.Read(RawBytes, 38);
    public int? RawField48 { get; init; }
    public S14ScenarioWorldExtension? Mode3Extension { get; init; }
    public S14ScenarioWorldBoundary? SourceBoundary { get; init; }
}

public sealed record S14ScenarioStateTable(int Section, int MarkerOffset, int RecordOffset,
    int RawCount, int RecordBytes, byte[] RawBytes, string RawSha256);
public sealed record S14ScenarioWorldBoundary(int ItemEndOffset,
    IReadOnlyList<S14ScenarioStateTable> StateTables, int SettingsOffset,
    byte[] RawSettingsBytes, string SettingsSha256, int WorldMarkerOffset)
{
    // Null retains compatibility with earlier source snapshots. Raw scenario
    // settings are distinct from the final options selected by the player.
    public S14ScenarioSettingsDocument? Settings { get; init; }
}

public sealed record S14ScenarioWorldRow(int Index, int Offset, byte[] RawBytes);
public sealed record S14ScenarioWorldGroup(int Index, int MajorIndex, int MinorIndex,
    int Offset, uint RawCount, IReadOnlyList<S14ScenarioWorldRow> Rows);
public sealed record S14ScenarioWorldExtension(int Offset, byte[] RawFixedBytes,
    IReadOnlyList<S14ScenarioWorldGroup> Groups)
{
    public bool ValuesInterpreted => false;
}

/// <summary>
/// Reads the ordinary CE static world section validated against CWorldData's
/// serializer and the following section markers. Runtime/save event bitmaps
/// are omitted by the static serializer and are not read or fabricated here.
/// </summary>
public static class S14ScenarioWorldReader
{
    public const int WorldBytes = 373;
    public const int ExtensionFixedBytes = 110;
    public const int ExtensionGroupCount = 36;
    public const int ExtensionRowBytes = 36;
    private static readonly int[] FollowingSections = [101, 102, 104, 105];

    /// <summary>Reads only the ordinary terminal block, without a preceding-section identity proof.</summary>
    public static S14ScenarioWorldDocument ReadOrdinaryTail(S14Document document)
    {
        int suffixOffset = ValidateSource(document);
        ReadOnlySpan<byte> source = document.DecodedPayload;
        int offset = suffixOffset - WorldBytes;
        if (I32(source, offset - sizeof(int)) != 100)
            throw new InvalidDataException("Static world section 100 boundary is missing or uses an unsupported layout.");
        int mode = I32(source, offset + 51);
        if (mode == 3)
            throw new InvalidDataException("Static world mode 3 requires its preceding section cursor; the ordinary tail boundary is not sufficient.");
        return ReadAtOffset(document, offset);
    }

    /// <summary>Reads the CE world using a forward cursor anchored at the verified scenario item table.</summary>
    public static S14ScenarioWorldDocument Read(S14Document document, string locale)
    {
        ValidateSource(document);
        S14ScenarioWorldBoundary boundary = S14ScenarioTailReader.Read(document, locale);
        return ReadAtOffset(document, boundary.WorldMarkerOffset + sizeof(int)) with
        { SourceBoundary = boundary };
    }

    /// <summary>
    /// Reads a complete world body at a caller-supplied section cursor. The caller
    /// must establish this cursor from the preceding sections; this method checks
    /// section 100 and the complete body/suffix, without searching raw data for a
    /// candidate. Mode 3 extension values remain uninterpreted source bytes.
    /// </summary>
    public static S14ScenarioWorldDocument ReadAtOffset(S14Document document, int offset)
    {
        int suffixOffset = ValidateSource(document);
        ReadOnlySpan<byte> source = document.DecodedPayload;
        if (offset < sizeof(int) * 2 || offset > suffixOffset - WorldBytes ||
            I32(source, offset - sizeof(int)) != 100)
            throw new InvalidDataException("Static world section 100 cursor is outside the source or has a missing boundary.");

        int mode = I32(source, offset + 51);
        int cursor = offset + WorldBytes;
        int[] groupOffsets = new int[ExtensionGroupCount];
        uint[] counts = new uint[ExtensionGroupCount];
        if (mode == 3)
        {
            if (suffixOffset - cursor < ExtensionFixedBytes + ExtensionGroupCount * sizeof(int))
                throw new InvalidDataException("Static world mode 3 fixed block or group directory is truncated.");
            cursor += ExtensionFixedBytes;
            for (int index = 0; index < ExtensionGroupCount; index++)
            {
                if (cursor > suffixOffset - sizeof(int))
                    throw new InvalidDataException($"Static world mode 3 group {index} count is truncated.");
                groupOffsets[index] = cursor;
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(cursor, sizeof(int)));
                cursor += sizeof(int);
                long rowBytes = (long)count * ExtensionRowBytes;
                int remainingCounts = (ExtensionGroupCount - index - 1) * sizeof(int);
                if (rowBytes > (long)suffixOffset - cursor - remainingCounts)
                    throw new InvalidDataException($"Static world mode 3 group {index} count {count} exceeds its source window.");
                counts[index] = count;
                cursor += (int)rowBytes;
            }
        }
        if (cursor != suffixOffset)
            throw new InvalidDataException($"Static world body ends at {cursor}, but its following sections begin at {suffixOffset}.");

        // Allocate only after the entire body and every count have been checked.
        S14ScenarioWorldExtension? extension = null;
        if (mode == 3)
        {
            var groups = new S14ScenarioWorldGroup[ExtensionGroupCount];
            for (int index = 0; index < groups.Length; index++)
            {
                var rows = new S14ScenarioWorldRow[(int)counts[index]];
                for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
                {
                    int rowOffset = groupOffsets[index] + sizeof(int) + rowIndex * ExtensionRowBytes;
                    rows[rowIndex] = new S14ScenarioWorldRow(rowIndex, rowOffset,
                        source.Slice(rowOffset, ExtensionRowBytes).ToArray());
                }
                groups[index] = new S14ScenarioWorldGroup(index, index / 12, index % 12,
                    groupOffsets[index], counts[index], Array.AsReadOnly(rows));
            }
            extension = new S14ScenarioWorldExtension(offset + WorldBytes,
                source.Slice(offset + WorldBytes, ExtensionFixedBytes).ToArray(), Array.AsReadOnly(groups));
        }

        ReadOnlySpan<byte> world = source.Slice(offset, suffixOffset - offset);
        return new S14ScenarioWorldDocument(offset,
            BinaryPrimitives.ReadUInt16LittleEndian(world[22..]),
            BinaryPrimitives.ReadInt16LittleEndian(world[33..]), mode, I32(world, 84), world.ToArray(),
            Convert.ToHexString(SHA256.HashData(source)), Convert.ToHexString(SHA256.HashData(world)))
        { Mode3Extension = extension, RawField48 = I32(world,55) };
    }

    private static int ValidateSource(S14Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ReadOnlySpan<byte> source = document.DecodedPayload;
        if (document.FormatMarker != 0x66)
            throw new InvalidDataException($"Unsupported static world edition {document.FormatMarker:X8}.");
        if (source.Length < sizeof(int) * 2 + WorldBytes + FollowingSections.Length * sizeof(int) ||
            BinaryPrimitives.ReadUInt32LittleEndian(source) != document.FormatMarker)
            throw new InvalidDataException("Static world source is truncated or has a mismatched edition marker.");

        int suffixOffset = source.Length - FollowingSections.Length * sizeof(int);
        for (int index = 0; index < FollowingSections.Length; index++)
            if (I32(source, suffixOffset + index * sizeof(int)) != FollowingSections[index])
                throw new InvalidDataException($"Static world following section {FollowingSections[index]} is missing or misplaced.");
        return suffixOffset;
    }

    private static int I32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset, sizeof(int)));
}
