using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14AbilityBonus(byte Leadership, byte Strength, byte Intelligence,
    byte Politics, byte Charm);

public sealed record S14RankDefinition(int Id, string Name, string Pinyin, string Description,
    ushort CommandLimit, S14AbilityBonus AbilityBonus, byte Raw156,
    string RawTailHex, string RawBytesHex);

public sealed record S14OfficeDefinition(int Id, string Name, string Pinyin,
    ushort CommandLimit, S14AbilityBonus AbilityBonus, byte Raw38, byte Raw39,
    byte Raw45, string RawBytesHex);

public sealed record S14RankCatalog(uint FormatMarker, int RankRecordOffset,
    int RankRecordStride, int OfficeRecordOffset, int OfficeRecordStride,
    IReadOnlyList<S14RankDefinition> Ranks, IReadOnlyList<S14OfficeDefinition> Offices);

public sealed record S14ForceRankReference(int ForceSlot, byte RankId);
public sealed record S14CharacterOfficeReference(int Slot, byte OfficeId);

/// <summary>
/// Reads the installed fixed-data rank and office tables without supplying runtime
/// ability formulas. Unverified parameters and every original row remain available.
/// </summary>
public static class S14RankReader
{
    private const int RankCount = 13;
    private const int OfficeCount = 256;
    private const int OfficeStride = 46;
    private static readonly Encoding StrictUnicode = new UnicodeEncoding(false, false, true);

    /// <summary>Preserves the source office reference, including invalid/reserved codes in templates.</summary>
    public static IReadOnlyList<S14CharacterOfficeReference> ReadCharacterOffices(
        S14Document scenario, string locale)
    {
        S14CharacterTable table = S14ScenarioTables.ReadCharacterNames(scenario, locale);
        int fieldOffset = locale == "SC" ? 218 : 162;
        var references = new S14CharacterOfficeReference[table.Names.Count];
        for (int slot = 0; slot < references.Length; slot++)
            references[slot] = new(slot, scenario.DecodedPayload[table.RecordOffset +
                slot * table.RecordStride + fieldOffset]);
        return references;
    }

    public static S14RankCatalog Read(S14Document fixedData)
    {
        (int start, int stride) = fixedData.FormatMarker switch
        {
            0x33 => (0x36876, 164),
            0x5C => (0x36D02, 164),
            0x66 => (0x36DF6, 166),
            _ => throw new InvalidDataException("Unsupported fixed-data edition for ranks.")
        };
        ReadOnlySpan<byte> data = fixedData.DecodedPayload;
        RequireHeader(data, start - 8, 15, RankCount);
        int officeHeader = checked(start + RankCount * stride);
        RequireHeader(data, officeHeader, 16, OfficeCount);
        int officeStart = officeHeader + 8;
        RequireHeader(data, checked(officeStart + OfficeCount * OfficeStride), 17, 11);

        var ranks = new List<S14RankDefinition>(RankCount);
        for (int id = 0; id < RankCount; id++)
        {
            ReadOnlySpan<byte> row = data.Slice(start + id * stride, stride);
            string name = ReadText(row[..26]);
            if (name.Length == 0)
                throw new InvalidDataException($"Empty rank name at {id}.");
            ranks.Add(new S14RankDefinition(id, name, ReadText(row.Slice(26, 26)),
                ReadText(row.Slice(52, 102)),
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(154, 2)),
                ReadBonus(row.Slice(157, 5)), row[156],
                Convert.ToHexString(row[162..]), Convert.ToHexString(row)));
        }

        var offices = new List<S14OfficeDefinition>(OfficeCount);
        for (int id = 0; id < OfficeCount; id++)
        {
            ReadOnlySpan<byte> row = data.Slice(officeStart + id * OfficeStride, OfficeStride);
            // Preserve every slot, including nonempty invalid-placeholder names.
            offices.Add(new S14OfficeDefinition(id, ReadText(row[..10]),
                ReadText(row.Slice(10, 26)),
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(36, 2)),
                ReadBonus(row.Slice(40, 5)), row[38], row[39], row[45],
                Convert.ToHexString(row)));
        }
        return new S14RankCatalog(fixedData.FormatMarker, start, stride, officeStart,
            OfficeStride, ranks, offices);
    }

    public static IReadOnlyList<S14ForceRankReference> ReadForceRanks(S14Document scenario)
    {
        (int countOffset, int stride) = scenario.FormatMarker switch
        {
            0x33 => (0x119F, 326),
            0x5C => (0x12A6, 326),
            0x66 => (0x1436, 329),
            _ => throw new InvalidDataException("Unsupported scenario edition for force ranks.")
        };
        ReadOnlySpan<byte> data = scenario.DecodedPayload;
        const int forceCount = 52;
        if (data.Length < countOffset + 4 + forceCount * stride ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(countOffset - 4, 4)) != 3 ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(countOffset, 4)) != forceCount)
            throw new InvalidDataException("Scenario force-rank table boundary or count differs.");
        var references = new List<S14ForceRankReference>(forceCount);
        for (int slot = 0; slot < forceCount; slot++)
        {
            byte rankId = data[countOffset + 4 + slot * stride + 54];
            if (rankId >= RankCount)
                throw new InvalidDataException($"Force {slot} references unsupported rank {rankId}.");
            references.Add(new S14ForceRankReference(slot, rankId));
        }
        return references;
    }

    private static void RequireHeader(ReadOnlySpan<byte> data, int offset, int marker, int count)
    {
        if (offset < 0 || offset > data.Length - 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4)) != marker ||
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset + 4, 4)) != count)
            throw new InvalidDataException($"Fixed-data section {marker} boundary or count differs.");
    }

    private static S14AbilityBonus ReadBonus(ReadOnlySpan<byte> data) =>
        new(data[0], data[1], data[2], data[3], data[4]);

    private static string ReadText(ReadOnlySpan<byte> data)
    {
        int end = 0;
        while (end < data.Length && BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(end, 2)) != 0)
            end += 2;
        try { return StrictUnicode.GetString(data[..end]); }
        catch (DecoderFallbackException error)
        { throw new InvalidDataException("Invalid UTF-16 in fixed rank or office text.", error); }
    }
}
