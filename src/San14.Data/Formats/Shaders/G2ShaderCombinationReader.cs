using System.Buffers.Binary;

namespace San14.Data.Formats;

/// <summary>Stage code is the DXBC code, distinct from native D3D command stages.</summary>
public sealed record G2ShaderCombinationStage(uint StageCode, int RowFieldOffset,
    short StageTableEntryIndex, G2ShaderStageEntry? Entry);
public sealed record G2ShaderCombination(int Index, int SourceOffset, ReadOnlyMemory<byte> RawRow,
    uint RawValue4, IReadOnlyList<G2ShaderCombinationStage> Stages);
public sealed record G2ShaderCombinations(DxbcRange SourceRange, int OffsetField,
    IReadOnlyList<G2ShaderCombination> Rows);

/// <summary>
/// Reads the verified S2G 16-byte combination range: header+94 to the first stage
/// table. Resolves original table ordinals to physical programs. Does not choose
/// an active combination, infer the +4 dword, or discard aliases/unknown bytes.
/// </summary>
public static class G2ShaderCombinationReader
{
    private static readonly (int Offset, uint Stage)[] Fields =
        [(0, 1), (2, 0), (8, 3), (10, 4), (12, 2), (14, 5)];

    public static G2ShaderCombinations Read(ReadOnlySpan<byte> source)
        => Read(source, G2ShaderContainerReader.Read(source));

    internal static G2ShaderCombinations Read(ReadOnlySpan<byte> source, G2ShaderContainer container)
    {
        if (source.Length != container.SourceByteLength)
            throw Error("source length differs from the imported container");
        uint rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(0x94, 4));
        int end = container.StageTables.Min(table => table.TableOffset);
        if (rawOffset > int.MaxValue || rawOffset < container.MetadataRange.Offset || rawOffset > end ||
            (end - rawOffset) % 16 != 0)
            throw Error("combination range is unsupported, unaligned or overlaps the stage tables");
        int start = (int)rawOffset;
        var tables = container.StageTables.ToDictionary(table => table.StageCode);
        var rows = new List<G2ShaderCombination>((end - start) / 16);
        for (int at = start; at < end; at += 16)
        {
            var stages = new G2ShaderCombinationStage[Fields.Length];
            for (int field = 0; field < Fields.Length; field++)
            {
                (int offset, uint stage) = Fields[field];
                short entryIndex = BinaryPrimitives.ReadInt16LittleEndian(source.Slice(at + offset, 2));
                G2ShaderStageTable table = tables[stage];
                if (entryIndex < -1 || entryIndex >= table.Entries.Count)
                    throw Error($"combination {rows.Count} stage {stage} entry {entryIndex} is unsupported or out of range");
                stages[field] = new(stage, offset, entryIndex, entryIndex < 0 ? null : table.Entries[entryIndex]);
            }
            rows.Add(new(rows.Count, at, source.Slice(at, 16).ToArray(),
                BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(at + 4, 4)), Array.AsReadOnly(stages)));
        }
        return new(new(start, end - start), 0x94, rows.AsReadOnly());
    }

    private static InvalidDataException Error(string detail) => new($"S2G shader combinations: {detail}.");
}
