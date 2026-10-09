using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data.Formats;

/// <summary>
/// Advances through the verified CE scenario state serializers from the item
/// table to world section 100. This is a source-layout adapter, not a limit on
/// the independent game's maps or entity collections. The supported profile
/// checks canonical source counts; native serializers themselves read counts
/// from the stream without an equality check against their preset values.
/// </summary>
public static class S14ScenarioTailReader
{
    public const int SettingsBytes = S14ScenarioSettingsReader.SerializedBytes;
    private sealed record Profile(int Section, int Count, int RecordBytes);
    private static readonly Profile[] Profiles =
    [
        new(14, 101, 0), new(15, 13, 0), new(16, 256, 0), new(17, 11, 0),
        new(18, 121, 0), new(19, 31, 15), new(20, 400, 0), new(21, 2001, 0),
        new(22, 356, 60), new(23, 501, 0),
        new(24, 31, 0), new(25, 501, 0), new(26, 101, 0), new(27, 501, 0),
        new(28, 49, 0), new(29, 501, 0), new(30, 51, 0), new(31, 8, 213),
        new(32, 50, 0), new(33, 201, 0), new(34, 31, 0), new(35, 18, 0),
        new(36, 5, 5), new(37, 9, 0), new(38, 101, 1), new(39, 7, 0),
        new(40, 31, 0), new(41, 80, 0), new(42, 7, 0)
    ];

    public static S14ScenarioWorldBoundary Read(S14Document document, string locale)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.FormatMarker != 0x66)
            throw new InvalidDataException("Scenario state tail requires the inspected CE source profile.");
        S14ScenarioItemCatalog items = S14ItemReader.ReadScenarioItems(document, locale);
        ReadOnlySpan<byte> source = document.DecodedPayload;
        int itemEndOffset = checked(items.RecordOffset + items.Items.Count * items.RecordStride);
        int cursor = itemEndOffset;
        var frames = new S14ScenarioStateTable[Profiles.Length];
        for (int index = 0; index < Profiles.Length; index++)
        {
            Profile profile = Profiles[index];
            RequireWord(source, cursor, profile.Section, $"section {profile.Section}");
            RequireWord(source, cursor + 4, profile.Count,
                $"section {profile.Section} count for the supported CE profile");
            int recordOffset = cursor + 8;
            long recordBytes = (long)profile.Count * profile.RecordBytes;
            if (recordBytes > (long)source.Length - recordOffset - sizeof(int))
                throw new InvalidDataException($"Scenario state section {profile.Section} rows are truncated.");
            int next = recordOffset + (int)recordBytes;
            RequireWord(source, next, profile.Section + 1, $"section {profile.Section + 1}");
            ReadOnlySpan<byte> rows = source.Slice(recordOffset, (int)recordBytes);
            frames[index] = new S14ScenarioStateTable(profile.Section, cursor, recordOffset,
                profile.Count, profile.RecordBytes, rows.ToArray(), Sha(rows));
            // Adjacent tables share this boundary marker. Do not consume it twice.
            cursor = next;
        }

        int settingsOffset = cursor + sizeof(int);
        if (settingsOffset > source.Length - SettingsBytes - sizeof(int))
            throw new InvalidDataException("CE scenario environment settings are truncated.");
        int worldMarkerOffset = settingsOffset + SettingsBytes;
        RequireWord(source, worldMarkerOffset, 100, "world section 100");
        ReadOnlySpan<byte> settings = source.Slice(settingsOffset, SettingsBytes);
        return new S14ScenarioWorldBoundary(itemEndOffset, Array.AsReadOnly(frames),
            settingsOffset, settings.ToArray(), Sha(settings), worldMarkerOffset)
        { Settings = S14ScenarioSettingsReader.Read(settings, settingsOffset) };
    }

    private static void RequireWord(ReadOnlySpan<byte> source, int offset, int value, string field)
    {
        if (offset < 0 || offset > source.Length - sizeof(int) ||
            BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset, sizeof(int))) != value)
            throw new InvalidDataException($"Scenario state tail {field} differs at {offset}.");
    }

    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
