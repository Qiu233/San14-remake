using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public sealed record G2ShaderProgram(int Index, int Offset, int Length, DxbcContainer Container);
public sealed record G2ShaderStageEntry(int Index, uint RelativeBytecodeOffset, int BytecodeLength,
    int ProgramIndex, int TableRowOffset, ReadOnlyMemory<byte> RawTableRow);
public sealed record G2ShaderStageTable(uint StageCode, int CountFieldOffset, int TableFieldOffset,
    int TableOffset, IReadOnlyList<G2ShaderStageEntry> Entries);
public sealed record G2ShaderContainer(int SourceByteLength, uint RawValue12, string NamePrefix,
    bool NameTerminated, byte[] RawNameField, byte[] RawHeader, DxbcRange MetadataRange,
    DxbcRange BytecodeRange, IReadOnlyList<G2ShaderProgram> Programs,
    IReadOnlyList<G2ShaderStageTable> StageTables)
{
    public uint MaterialModeCount => BinaryPrimitives.ReadUInt32LittleEndian(RawHeader.AsSpan(0x54, 4));
    public ReadOnlySpan<byte> GetProgramBytes(ReadOnlySpan<byte> source, int programIndex)
    {
        if (source.Length != SourceByteLength) throw new InvalidDataException("S2G source length changed.");
        if ((uint)programIndex >= Programs.Count) throw new ArgumentOutOfRangeException(nameof(programIndex));
        G2ShaderProgram program = Programs[programIndex];
        return source.Slice(program.Offset, program.Length);
    }
}

/// <summary>
/// Reads the installed _S2G3000 profile: six explicit stage tables and the DXBC
/// bytecode region. Physical programs and ordered table entries are separate,
/// preserving aliases and unreferenced programs. Names can be truncated 32-byte
/// prefixes. Render-state metadata, technique selection and shader execution
/// remain uninterpreted; complete source bytes are retained by the repository.
/// </summary>
public static class G2ShaderContainerReader
{
    // Each stage code is independently present in its referenced SHDR/SHEX token.
    // Table order follows the observed header layout, not numeric stage order.
    private static readonly (uint Stage, int Count, int Table)[] Stages =
    [ (1, 0x68, 0x98), (0, 0x6C, 0x9C), (3, 0xC4, 0xD0),
      (4, 0xC8, 0xD4), (2, 0xCC, 0xD8), (5, 0xE0, 0xE4) ];

    public static G2ShaderContainer Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < 256 || !source[..8].SequenceEqual("_S2G3000"u8))
            throw Error("missing or unsupported container header");
        if (UInt(source, 8) != source.Length || UInt(source, 12) != 0x00100001)
            throw Error("declared length or header profile is unsupported");
        int bytecodeStart = Number(source, 0xB0), bytecodeLength = Number(source, 0xB4);
        if (bytecodeStart < 256 || bytecodeLength < 32 || (long)bytecodeStart + bytecodeLength != source.Length)
            throw Error("bytecode region does not exactly reach the declared container end");
        ReadOnlySpan<byte> nameField = source.Slice(16, 32);
        int terminator = nameField.IndexOf((byte)0);
        ReadOnlySpan<byte> nameBytes = terminator < 0 ? nameField : nameField[..terminator];
        if (nameBytes.ContainsAnyExceptInRange((byte)32, (byte)126) ||
            (terminator >= 0 && nameField[terminator..].ContainsAnyExcept((byte)0)))
            throw Error("unsupported name field encoding or padding");

        var programs = new List<G2ShaderProgram>();
        var programRanges = new Dictionary<(uint Offset, int Length), int>();
        int cursor = bytecodeStart;
        while (cursor < source.Length)
        {
            if (source.Length - cursor < 32 || !source.Slice(cursor, 4).SequenceEqual("DXBC"u8))
                throw Error($"bytecode at {cursor} has no complete DXBC header");
            int length = Number(source, cursor + 24);
            if (length < 32 || length > source.Length - cursor)
                throw Error($"program {programs.Count} exceeds the bytecode region");
            DxbcContainer parsed;
            try { parsed = DxbcContainerReader.Read(source.Slice(cursor, length)); }
            catch (InvalidDataException error)
            { throw new InvalidDataException($"S2G program {programs.Count} at {cursor}: {error.Message}", error); }
            programRanges.Add(((uint)(cursor - bytecodeStart), length), programs.Count);
            programs.Add(new(programs.Count, cursor, length, parsed));
            cursor += length;
        }

        var tables = new List<G2ShaderStageTable>();
        var occupiedTables = new List<(int Start, int End)>();
        foreach ((uint stage, int countField, int tableField) in Stages)
        {
            int count = Number(source, countField), tableOffset = Number(source, tableField);
            if (tableOffset < 256 || tableOffset > bytecodeStart || (long)count * 8 > bytecodeStart - tableOffset)
                throw Error($"stage {stage} table exceeds metadata boundaries");
            if (count > 0) occupiedTables.Add((tableOffset, tableOffset + count * 8));
            var entries = new List<G2ShaderStageEntry>(count);
            for (int i = 0; i < count; i++)
            {
                int row = tableOffset + i * 8;
                uint offset = UInt(source, row);
                int length = Number(source, row + 4);
                if (!programRanges.TryGetValue((offset, length), out int programIndex))
                    throw Error($"stage {stage} entry {i} does not reference an exact program boundary");
                DxbcPart[] code = programs[programIndex].Container.Parts.Where(part => part.TokenHeader is not null).ToArray();
                if (code.Length != 1 || code[0].TokenHeader!.RawVersionToken >> 16 != stage)
                    throw Error($"stage {stage} entry {i} disagrees with its tokenized shader stage");
                entries.Add(new(i, offset, length, programIndex, row, source.Slice(row, 8).ToArray()));
            }
            tables.Add(new(stage, countField, tableField, tableOffset, entries.AsReadOnly()));
        }
        var orderedTables = occupiedTables.OrderBy(range => range.Start).ToArray();
        for (int i = 1; i < orderedTables.Length; i++)
            if (orderedTables[i].Start < orderedTables[i - 1].End)
                throw Error("stage tables overlap");
        return new(source.Length, UInt(source, 12), Encoding.ASCII.GetString(nameBytes), terminator >= 0,
            nameField.ToArray(), source[..256].ToArray(), new(256, bytecodeStart - 256),
            new(bytecodeStart, bytecodeLength), programs.AsReadOnly(), tables.AsReadOnly());
    }

    private static uint UInt(ReadOnlySpan<byte> source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4));
    private static int Number(ReadOnlySpan<byte> source, int offset)
    {
        uint value = UInt(source, offset);
        return value <= int.MaxValue ? (int)value : throw Error($"field at {offset} exceeds the supported range");
    }
    private static InvalidDataException Error(string message) => new($"S2G shader container: {message}.");
}
