using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data.Formats;

/// <summary>
/// Table zero's original threshold and two byte quantities. The threshold is
/// compared as signed int32 by the verified CE consumer; its unsigned bit view
/// is retained separately. Row zero is retained even though that consumer marks
/// it invalid. RawAdditionalSlots is an aggregate additional-slot quantity,
/// not an allocation to any particular department.
/// </summary>
public sealed record ExperienceDoctrineRecord(int Index, uint RawThreshold,
    int SignedThreshold, byte RawAdditionalSlots, byte RawCostLimitAddition,
    byte[] RawBytes);

public sealed record ExperienceDoctrineCatalog(int TableIndex, int TableOffset,
    int TableLength, int RecordStride, string RawTableSha256,
    IReadOnlyList<ExperienceDoctrineRecord> Records);

/// <summary>
/// Projects the verified table-zero profile without initializing force
/// experience, doctrine level, governance, or the optional-mode cost bonus.
/// Full source provenance remains the responsibility of the caller: an
/// ExperienceDataDocument contains directory and record bytes, not a file URI
/// or full-file hash.
/// </summary>
public static class ExperienceDoctrineReader
{
    public static ExperienceDoctrineCatalog Read(ExperienceDataDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Tables is null || document.Tables.Count is not (2 or 4))
            throw new InvalidDataException("Doctrine experience projection requires the verified two- or four-table container.");
        ExperienceDataTable table = document.Tables[0]
            ?? throw new InvalidDataException("Doctrine experience table zero is missing.");
        if (table.Index != 0 || table.Offset != 4 + document.Tables.Count * 8 ||
            table.Length != 114 || table.RecordStride != 10 ||
            table.LeadingPadding is null || table.LeadingPadding.Length != 0 ||
            table.Records is null || table.Records.Count != 11)
            throw new InvalidDataException("Doctrine experience table zero has an unsupported directory or record profile.");

        var rawTable = new byte[table.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(rawTable, (uint)table.Records.Count);
        var records = new List<ExperienceDoctrineRecord>(table.Records.Count);
        for (int index = 0; index < table.Records.Count; index++)
        {
            ExperienceDataRecord row = table.Records[index]
                ?? throw new InvalidDataException($"Doctrine experience table zero record {index} is missing.");
            if (row.Index != index || row.RawBytes is null || row.RawBytes.Length != table.RecordStride ||
                BinaryPrimitives.ReadUInt32LittleEndian(row.RawBytes) != index)
                throw new InvalidDataException($"Doctrine experience table zero record {index} has an invalid index or length.");
            byte[] raw = (byte[])row.RawBytes.Clone();
            raw.CopyTo(rawTable, 4 + index * table.RecordStride);
            records.Add(new ExperienceDoctrineRecord(index,
                BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4, 4)),
                BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(4, 4)),
                raw[8], raw[9], raw));
        }
        return new ExperienceDoctrineCatalog(table.Index, table.Offset, table.Length,
            table.RecordStride, Convert.ToHexString(SHA256.HashData(rawTable)), records);
    }
}
