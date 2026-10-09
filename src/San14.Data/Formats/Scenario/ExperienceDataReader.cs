using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record ExperienceDataRecord(int Index, byte[] RawBytes);

public sealed record ExperienceDataTable(int Index, int Offset, int Length,
    int RecordStride, IReadOnlyList<ExperienceDataRecord> Records, byte[] LeadingPadding);

public sealed record ExperienceDataDocument(IReadOnlyList<ExperienceDataTable> Tables);

/// <summary>
/// Reads the verified containers in 0000/experience_data.bin and
/// experience_data_exce.bin. Record values retain their original bytes:
/// table zero and table one have separate verified projections in
/// ExperienceDoctrineReader and ExperienceGrowthReader. Other tables and
/// complete runtime behavior remain separate work.
/// </summary>
public static class ExperienceDataReader
{
    public static ExperienceDataDocument Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < 4)
            throw new InvalidDataException("Experience data table directory is missing.");
        uint tableCount = BinaryPrimitives.ReadUInt32LittleEndian(source);
        if (tableCount is not (2 or 4))
            throw new InvalidDataException($"Unsupported experience data table count {tableCount}.");
        int[] counts = [11, 101, 7, 16];
        int[] strides = [10, 24, 15, 6];
        int previousEnd = 4 + (int)tableCount * 8;
        if (source.Length < previousEnd)
            throw new InvalidDataException("Experience data table directory is truncated.");
        var tables = new List<ExperienceDataTable>((int)tableCount);
        for (int index = 0; index < tableCount; index++)
        {
            int entry = 4 + index * 8;
            uint offsetValue = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(entry, 4));
            uint lengthValue = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(entry + 4, 4));
            int expectedOffset = (previousEnd + 3) & ~3;
            int expectedLength = 4 + counts[index] * strides[index];
            if (offsetValue != expectedOffset || lengthValue != expectedLength ||
                offsetValue > (uint)source.Length || lengthValue > source.Length - offsetValue)
                throw new InvalidDataException($"Experience data table {index} has an invalid offset or length.");
            int offset = (int)offsetValue;
            int length = (int)lengthValue;
            ReadOnlySpan<byte> padding = source.Slice(previousEnd, offset - previousEnd);
            if (padding.IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException($"Experience data table {index} has nonzero alignment padding.");
            if (BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4)) != counts[index])
                throw new InvalidDataException($"Experience data table {index} has an unsupported record count.");
            var records = new List<ExperienceDataRecord>(counts[index]);
            for (int record = 0; record < counts[index]; record++)
            {
                ReadOnlySpan<byte> row = source.Slice(offset + 4 + record * strides[index], strides[index]);
                if (BinaryPrimitives.ReadUInt32LittleEndian(row) != record)
                    throw new InvalidDataException($"Experience data table {index} record {record} has an invalid index.");
                records.Add(new ExperienceDataRecord(record, row.ToArray()));
            }
            tables.Add(new ExperienceDataTable(index, offset, length, strides[index], records, padding.ToArray()));
            previousEnd = offset + length;
        }
        if (previousEnd != source.Length)
            throw new InvalidDataException("Experience data contains trailing bytes after the final table.");
        return new ExperienceDataDocument(tables);
    }
}
