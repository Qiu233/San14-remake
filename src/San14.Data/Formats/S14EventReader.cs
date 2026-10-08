using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data.Formats;

public sealed record S14EventRecord(int Index, int Id, int RawField34,
    int Offset, byte[] RawBytes);

public sealed record S14EventOperation(int Index, int Offset, int Code,
    short RawField04, short RawField06, byte[] RawBytes);

public sealed record S14EventStream(int Index, int Offset, int Length,
    IReadOnlyList<S14EventOperation> Operations);

public sealed record S14EventOpaqueRange(int Offset, byte[] RawBytes);

public sealed record S14EventDocument(int RecordOffset, int NativeBaseId,
    IReadOnlyList<S14EventRecord> Events,
    IReadOnlyList<S14EventStream> ConditionStreams,
    IReadOnlyList<S14EventStream> ExecutionStreams,
    IReadOnlyList<S14EventOpaqueRange> UninterpretedRanges,
    byte[] RawBytes, string SourceSha256);

/// <summary>
/// Reads the native event resource layout verified in the CE loader. These are
/// uncompressed resources, distinct from the fixed/scenario S14 containers.
/// The two offset directories share their middle endpoint. Stream indices are
/// native directory slots, not event IDs or the event record's raw +34 field.
/// Operations and unknown fields are retained without evaluating bytecode.
/// </summary>
public static class S14EventReader
{
    public const int HeaderBytes = 0x44;
    public const int EventRecordBytes = 0x44;
    public const int OperationBytes = 0x2C;

    // Exact 48-byte signature compared by original loader 3D67C0 against
    // 1A41B78. Its individual words are not assumed to be format versions.
    private static readonly byte[] Signature = Convert.FromHexString(
        "030000000500000009000000040000000E000000000000000000000006000000" +
        "01000000000000000000000000000000");

    public static S14EventDocument Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < HeaderBytes)
            throw new InvalidDataException("Event resource header is truncated.");
        if (!source[..Signature.Length].SequenceEqual(Signature))
            throw new InvalidDataException("Unsupported event resource signature.");

        int count = I32(source, 0x30);
        if (count <= 0)
            throw new InvalidDataException($"Invalid event record count {count}.");

        // N conditions and N executions use 2N+1 offsets, not two independent
        // N+1 directories. Validate before allocating count-sized collections.
        long directoryEnd = HeaderBytes + (2L * count + 1) * sizeof(int);
        if (directoryEnd > source.Length)
            throw new InvalidDataException("Event stream directory is truncated.");

        int recordOffset = I32(source, 0x40);
        long recordEnd = (long)recordOffset + (long)count * EventRecordBytes;
        if (recordOffset < directoryEnd || recordEnd > source.Length)
            throw new InvalidDataException("Event record table overlaps the directory or exceeds the resource.");

        int offsetCount = checked(count * 2 + 1);
        var offsets = new int[offsetCount];
        for (int index = 0; index < offsetCount; index++)
        {
            int offset = I32(source, HeaderBytes + index * sizeof(int));
            if (offset < recordEnd || offset > source.Length)
                throw new InvalidDataException($"Event stream offset {index} is outside the operation data.");
            if (index > 0 && (offset < offsets[index - 1] ||
                (offset - offsets[index - 1]) % OperationBytes != 0))
                throw new InvalidDataException($"Event stream interval {index - 1} is reversed or has a partial operation.");
            offsets[index] = offset;
        }

        var events = new List<S14EventRecord>(count);
        for (int index = 0; index < count; index++)
        {
            int offset = recordOffset + index * EventRecordBytes;
            ReadOnlySpan<byte> row = source.Slice(offset, EventRecordBytes);
            events.Add(new S14EventRecord(index, I32(row, 0), I32(row, 0x34), offset, row.ToArray()));
        }

        var conditions = new List<S14EventStream>(count);
        var executions = new List<S14EventStream>(count);
        for (int index = 0; index < count; index++)
        {
            conditions.Add(ReadStream(source, index, offsets[index], offsets[index + 1]));
            executions.Add(ReadStream(source, index, offsets[count + index], offsets[count + index + 1]));
        }

        // Preserve bytes outside the verified structural regions. The native
        // samples contain a 4-byte gap before rows and a 4-byte tail; neither is
        // assigned a sentinel meaning or required to have a particular value.
        var opaque = new List<S14EventOpaqueRange>();
        Preserve(source, opaque, (int)directoryEnd, recordOffset);
        Preserve(source, opaque, (int)recordEnd, offsets[0]);
        Preserve(source, opaque, offsets[^1], source.Length);
        return new S14EventDocument(recordOffset, events[0].Id,
            events.AsReadOnly(), conditions.AsReadOnly(), executions.AsReadOnly(), opaque.AsReadOnly(),
            source.ToArray(), Convert.ToHexString(SHA256.HashData(source)));
    }

    private static S14EventStream ReadStream(ReadOnlySpan<byte> source, int index, int begin, int end)
    {
        int count = (end - begin) / OperationBytes;
        var operations = new List<S14EventOperation>(count);
        for (int operationIndex = 0; operationIndex < count; operationIndex++)
        {
            int offset = begin + operationIndex * OperationBytes;
            ReadOnlySpan<byte> row = source.Slice(offset, OperationBytes);
            operations.Add(new S14EventOperation(operationIndex, offset, I32(row, 8),
                BinaryPrimitives.ReadInt16LittleEndian(row[4..]),
                BinaryPrimitives.ReadInt16LittleEndian(row[6..]), row.ToArray()));
        }
        return new S14EventStream(index, begin, end - begin, operations.AsReadOnly());
    }

    private static void Preserve(ReadOnlySpan<byte> source, List<S14EventOpaqueRange> ranges, int begin, int end)
    {
        if (begin != end)
            ranges.Add(new S14EventOpaqueRange(begin, source[begin..end].ToArray()));
    }

    private static int I32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset, sizeof(int)));
}
