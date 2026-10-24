using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14Document(
    uint FormatMarker,
    string Signature,
    string? Title,
    string Description,
    byte[] DecodedPayload,
    int? LeadingRecordCount)
{
    public int? StartYear { get; init; }
    public int? StartMonth { get; init; }
    public int? StartDay { get; init; }
    public S14ScenarioSelectionMetadata? SelectionMetadata { get; init; }
}

public sealed record S14Header(uint FormatMarker, string Signature, string? Title,
    string Description, int PayloadOffset, int? StartYear, int? StartMonth, int? StartDay)
{
    public S14ScenarioSelectionMetadata? SelectionMetadata { get; init; }
}

public static class S14Reader
{
    private static readonly byte[] LwcMagic = [0x4c, 0x57, 0x43, 0x1a];

    public static S14Document Read(ReadOnlySpan<byte> source, bool isScenario)
    {
        S14Header header = ReadHeader(source, isScenario);
        byte[] decoded = LwcDecoder.Decode(source[header.PayloadOffset..]);
        if (decoded.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(decoded) != header.FormatMarker)
            throw new InvalidDataException("S14 decoded payload does not match the outer format marker.");

        int? recordCount = null;
        int candidate = BinaryPrimitives.ReadInt32LittleEndian(decoded.AsSpan(4, 4));
        if (candidate is >= 0 and < 100_000 && decoded.Length >= 8L + candidate * 2L)
            recordCount = candidate;
        if (isScenario)
        {
            if (recordCount is null or < 1 || candidate > ushort.MaxValue)
                throw new InvalidDataException("S14 scenario ID table is missing.");
            for (int i = 0; i < candidate; i++)
            {
                ushort value = BinaryPrimitives.ReadUInt16LittleEndian(
                    decoded.AsSpan(8 + i * 2, 2));
                ushort expected = (ushort)(i == candidate - 1 ? 0 : i + 1);
                if (value != expected)
                    throw new InvalidDataException($"S14 scenario ID table differs at slot {i}.");
            }
        }

        return new S14Document(header.FormatMarker, header.Signature, header.Title,
            header.Description, decoded, recordCount)
        {
            StartYear = header.StartYear,
            StartMonth = header.StartMonth,
            StartDay = header.StartDay,
            SelectionMetadata = header.SelectionMetadata
        };
    }

    /// <summary>Reads the small uncompressed header so scenario lists need no payload cache.</summary>
    public static S14Header ReadHeader(ReadOnlySpan<byte> source, bool isScenario)
    {
        if (source.Length < 480)
            throw new InvalidDataException("S14 file is too short.");

        uint marker = BinaryPrimitives.ReadUInt32LittleEndian(source);
        int expectedOffset = marker switch
        {
            0x33 => 480,
            0x5c => 754,
            0x66 => 754,
            _ => throw new InvalidDataException($"Unsupported S14 format marker {marker:X8}.")
        };
        if (source.Length < expectedOffset + 268 || !source.Slice(expectedOffset, 4).SequenceEqual(LwcMagic))
            throw new InvalidDataException($"LWC payload missing at expected S14 offset {expectedOffset}.");

        string signature = Encoding.ASCII.GetString(source.Slice(4, 16)).TrimEnd('\0');
        string description = ReadUtf16Z(source.Slice(20, isScenario ? 402 : 384));
        string? title = isScenario ? ReadUtf16Z(source.Slice(0x1a6, 18)) : null;
        int? year = null;
        int? month = null;
        int? day = null;
        if (isScenario)
        {
            year = BinaryPrimitives.ReadInt16LittleEndian(source.Slice(0x1c8, 2));
            day = source[0x1cc];
            month = source[0x1cd];
            if (year is < 1 or > 999 || month is < 1 or > 12 ||
                day is not (1 or 11 or 21) || source[0x1cb] != marker)
                throw new InvalidDataException("S14 scenario date or repeated format marker is invalid.");
        }
        return new S14Header(marker, signature, title, description,
            expectedOffset, year, month, day)
        {
            SelectionMetadata = isScenario ? S14ScenarioSelectionMetadata.Read(source, marker) : null
        };
    }

    private static string ReadUtf16Z(ReadOnlySpan<byte> data)
    {
        int end = 0;
        while (end + 1 < data.Length && (data[end] != 0 || data[end + 1] != 0)) end += 2;
        return Encoding.Unicode.GetString(data[..end]);
    }
}
