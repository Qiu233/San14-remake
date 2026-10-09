using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public sealed record G2ShaderMetadataString(string Text, DxbcRange EncodedRange);
public sealed record G2ShaderMetadataRecord(int Index, int PointerFieldOffset, uint RelativeTableOffset,
    DxbcRange EncodedRange, IReadOnlyList<uint> RawWords, G2ShaderMetadataString? Name,
    G2ShaderMetadataString? Text32, G2ShaderMetadataString? Text36);
public sealed record G2ShaderMetadata(int SourceByteLength, DxbcRange BlockRange, byte[] RawHeader,
    G2ShaderMetadataString? Description, DxbcRange? RecordTableRange,
    IReadOnlyList<G2ShaderMetadataRecord> Records, IReadOnlyList<DxbcRange> UninterpretedBlockRanges);

/// <summary>
/// Reads the verified 5000 tail block's description and relative table of 44-byte
/// named records. All ranges are absolute offsets in the decoded S2G source.
/// Other tables and unknown fields are retained, without assigning register,
/// sampler, render-state, technique or material semantics to their values.
/// </summary>
public static class G2ShaderMetadataReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static G2ShaderMetadata Read(ReadOnlySpan<byte> source)
    {
        G2ShaderContainer container = G2ShaderContainerReader.Read(source);
        int start = Number(source, 0xAC), end = container.BytecodeRange.Offset;
        if (start < container.MetadataRange.Offset || start > end - 40 ||
            UInt(source, start) != end - start || !source.Slice(start + 4, 4).SequenceEqual("5000"u8))
            throw Error("unsupported tail block profile or boundaries");
        var block = new DxbcRange(start, end - start);
        foreach (G2ShaderStageTable table in container.StageTables)
            if (table.Entries.Count > 0 && table.TableOffset < end &&
                (long)table.TableOffset + table.Entries.Count * 8L > start)
                throw Error("tail block overlaps a stage table");

        int count = Number(source, start + 12);
        if (count > (block.Length - 40) / 4) throw Error("record count exceeds tail block");
        uint tablePointer = UInt(source, start + 24);
        if (count > 0 && tablePointer == 0) throw Error("nonempty record table has a null pointer");
        DxbcRange? directory = tablePointer == 0 ? null : RelativeRange(start, tablePointer, count * 4, block);
        var structures = new List<DxbcRange> { new(start, 40) };
        if (directory is { Length: > 0 }) structures.Add(directory);
        var rows = new List<(int Pointer, uint Relative, DxbcRange Range, uint[] Words)>(count);
        var physicalRows = new Dictionary<int, uint[]>();
        for (int i = 0; i < count; i++)
        {
            int pointer = directory!.Offset + i * 4;
            uint relative = UInt(source, pointer);
            DxbcRange row = RelativeRange(directory.Offset, relative, 44, block);
            if (!physicalRows.TryGetValue(row.Offset, out uint[]? words))
            {
                words = new uint[11];
                for (int word = 0; word < words.Length; word++) words[word] = UInt(source, row.Offset + word * 4);
                physicalRows.Add(row.Offset, words);
                structures.Add(row);
            }
            rows.Add((pointer, relative, row, words));
        }
        structures.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        for (int i = 1; i < structures.Count; i++)
            if (structures[i].Offset < structures[i - 1].Offset + structures[i - 1].Length)
                throw Error("header, record table or distinct record bodies overlap");

        var strings = new Dictionary<int, G2ShaderMetadataString>();
        int remainingTextBytes = block.Length;
        G2ShaderMetadataString? description = ReadString(source, start, UInt(source, start + 28), block,
            structures, strings, ref remainingTextBytes);
        var records = new List<G2ShaderMetadataRecord>(count);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            records.Add(new(i, row.Pointer, row.Relative, row.Range, row.Words,
                ReadString(source, row.Range.Offset, row.Words[7], block, structures, strings, ref remainingTextBytes),
                ReadString(source, row.Range.Offset, row.Words[8], block, structures, strings, ref remainingTextBytes),
                ReadString(source, row.Range.Offset, row.Words[9], block, structures, strings, ref remainingTextBytes)));
        }
        // Merge original ranges only for coverage; public records retain table order
        // and repeated references. Unparsed secondary tables remain in the gaps.
        var covered = structures.Concat(strings.Values.Select(text => text.EncodedRange)).OrderBy(range => range.Offset);
        var gaps = new List<DxbcRange>();
        int cursor = start;
        foreach (DxbcRange range in covered)
        {
            if (range.Offset > cursor) gaps.Add(new(cursor, range.Offset - cursor));
            cursor = Math.Max(cursor, range.Offset + range.Length);
        }
        if (cursor < end) gaps.Add(new(cursor, end - cursor));
        return new(source.Length, block, source.Slice(start, 40).ToArray(), description, directory, records, gaps);
    }

    private static G2ShaderMetadataString? ReadString(ReadOnlySpan<byte> source, int origin, uint relative,
        DxbcRange block, IReadOnlyList<DxbcRange> structures, Dictionary<int, G2ShaderMetadataString> strings,
        ref int remainingBytes)
    {
        if (relative == 0) return null;
        int offset = RelativeRange(origin, relative, 1, block).Offset;
        if (strings.TryGetValue(offset, out G2ShaderMetadataString? known)) return known;
        // Binary search avoids checking every record body for every string.
        int low = 0, high = structures.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (structures[middle].Offset <= offset) low = middle + 1; else high = middle;
        }
        if (low > 0 && offset < structures[low - 1].Offset + structures[low - 1].Length)
            throw Error($"string at {offset} intersects structural data");
        int end = low < structures.Count ? structures[low].Offset : block.Offset + block.Length;
        int limit = Math.Min(end - offset, remainingBytes);
        int length = source.Slice(offset, limit).IndexOf((byte)0);
        if (length < 0) throw Error($"string at {offset} is unterminated or exceeds the decoded text budget");
        string value;
        try { value = StrictUtf8.GetString(source.Slice(offset, length)); }
        catch (DecoderFallbackException error)
        { throw new InvalidDataException($"S2G metadata string at {offset} is not valid UTF-8.", error); }
        remainingBytes -= length + 1;
        var result = new G2ShaderMetadataString(value, new(offset, length + 1));
        strings.Add(offset, result);
        return result;
    }

    private static DxbcRange RelativeRange(int origin, uint relative, int length, DxbcRange block)
    {
        long offset = origin + (long)relative, end = block.Offset + (long)block.Length;
        if (offset < block.Offset + 40L || length < 0 || offset > end - length)
            throw Error($"relative range {origin}+{relative}+{length} exceeds tail data");
        return new((int)offset, length);
    }
    private static uint UInt(ReadOnlySpan<byte> source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4));
    private static int Number(ReadOnlySpan<byte> source, int offset)
    {
        uint value = UInt(source, offset);
        return value <= int.MaxValue ? (int)value : throw Error($"field at {offset} exceeds the supported range");
    }
    private static InvalidDataException Error(string message) => new($"S2G metadata: {message}.");
}
