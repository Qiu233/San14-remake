using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data.Formats;

/// <summary>
/// Original table-one cumulative experience thresholds, in the ability order
/// consumed by CGrowthExpData. Values retain all 32 bits; the original load
/// comparison is unsigned. Index is the source base-ability value (0..100).
/// </summary>
public sealed record ExperienceGrowthRecord(int Index, uint Leadership,
    uint Strength, uint Intelligence, uint Politics, uint Charm, byte[] RawBytes);

public sealed record ExperienceGrowthCatalog(int TableIndex, int TableOffset,
    int TableLength, int RecordStride, string RawTableSha256,
    IReadOnlyList<ExperienceGrowthRecord> Records);

/// <summary>
/// Reads the five original columns without substituting a growth formula or
/// initializing any officer. File provenance belongs to the repository owner.
/// </summary>
public static class ExperienceGrowthReader
{
    public static ExperienceGrowthCatalog Read(ExperienceDataDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Tables is null || document.Tables.Count is not (2 or 4))
            throw new InvalidDataException("Growth experience projection requires the verified two- or four-table container.");
        ExperienceDataTable table = document.Tables[1]
            ?? throw new InvalidDataException("Growth experience table one is missing.");
        int expectedOffset = (4 + document.Tables.Count * 8 + 114 + 3) & ~3;
        if (table.Index != 1 || table.Offset != expectedOffset ||
            table.Length != 2428 || table.RecordStride != 24 ||
            table.LeadingPadding is null || table.LeadingPadding.Length != 2 ||
            table.LeadingPadding.AsSpan().IndexOfAnyExcept((byte)0) >= 0 ||
            table.Records is null || table.Records.Count != 101)
            throw new InvalidDataException("Growth experience table one has an unsupported directory or record profile.");

        var rawTable = new byte[table.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(rawTable, (uint)table.Records.Count);
        var records = new List<ExperienceGrowthRecord>(table.Records.Count);
        for (int index = 0; index < table.Records.Count; index++)
        {
            ExperienceDataRecord row = table.Records[index]
                ?? throw new InvalidDataException($"Growth experience table one record {index} is missing.");
            if (row.Index != index || row.RawBytes is null || row.RawBytes.Length != table.RecordStride ||
                BinaryPrimitives.ReadUInt32LittleEndian(row.RawBytes) != index)
                throw new InvalidDataException($"Growth experience table one record {index} has an invalid index or length.");
            byte[] raw = (byte[])row.RawBytes.Clone();
            raw.CopyTo(rawTable, 4 + index * table.RecordStride);
            records.Add(new ExperienceGrowthRecord(index,
                BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(8, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(12, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(16, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(20, 4)), raw));
        }
        return new ExperienceGrowthCatalog(table.Index, table.Offset, table.Length,
            table.RecordStride, Convert.ToHexString(SHA256.HashData(rawTable)), records);
    }
}
