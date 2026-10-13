using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

// RDEF ranges and type offsets are relative to PayloadRangeInProgram, not the DXBC/S2G start.
public sealed record DxbcRdefString(string Text, DxbcRange EncodedRange);
public sealed record DxbcRdefBinding(int Index, DxbcRange RowRange, DxbcRdefString Name,
    uint InputType, uint ReturnType, uint Dimension, uint NumSamples, uint BindPoint, uint BindCount, uint Flags);
public sealed record DxbcRdefVariable(int Index, DxbcRange RowRange, DxbcRdefString Name,
    int StartOffset, int ByteSize, uint Flags, int TypeOffset, DxbcRange? DefaultValueRange,
    uint StartTexture, uint TextureSize, uint StartSampler, uint SamplerSize);
public sealed record DxbcRdefBuffer(int Index, DxbcRange RowRange, DxbcRdefString Name,
    int ByteSize, uint Flags, uint BufferType, DxbcRange VariableTableRange,
    IReadOnlyList<DxbcRdefVariable> Variables);
public sealed record DxbcRdefMember(int Index, DxbcRange RowRange, DxbcRdefString Name,
    int TypeOffset, uint ByteOffset);
public sealed record DxbcRdefType(int Offset, DxbcRange RowRange, ushort Class, ushort BaseType,
    ushort Rows, ushort Columns, ushort Elements, DxbcRange MemberTableRange,
    IReadOnlyList<DxbcRdefMember> Members, ReadOnlyMemory<byte> RawTypeExtension, DxbcRdefString? Name);
public sealed record DxbcResourceDefinitions(int ProgramByteLength, DxbcRange PayloadRangeInProgram,
    byte MajorVersion, byte MinorVersion, ushort RawShaderType, uint CompileFlags,
    uint InterfaceSlotCount, ReadOnlyMemory<byte> RawHeader, DxbcRdefString Creator,
    DxbcRange BufferTableRange, DxbcRange BindingTableRange,
    IReadOnlyList<DxbcRdefBuffer> Buffers, IReadOnlyList<DxbcRdefBinding> Bindings,
    IReadOnlyList<DxbcRdefType> Types);

/// <summary>
/// Reads Shader Model 5.0 RD11 resource definitions. Keeps numeric enum/flag values,
/// ordered declarations, shared type references and original defaults. It does not
/// choose a model shader, upload constants, infer sampler states, or execute code.
/// </summary>
public static class DxbcResourceDefinitionReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static DxbcResourceDefinitions Read(ReadOnlySpan<byte> program)
    {
        DxbcContainer container = DxbcContainerReader.Read(program);
        DxbcPart[] definitions = container.Parts.Where(part => part.FourCc == "RDEF").ToArray();
        if (definitions.Length != 1) throw Error("expected exactly one RDEF part");
        DxbcRange payloadRange = definitions[0].PayloadRange;
        ReadOnlySpan<byte> data = payloadRange.Slice(program);
        if (data.Length < 60 || data[16] != 0 || data[17] != 5 || !data.Slice(28, 4).SequenceEqual("RD11"u8))
            throw Error("unsupported or truncated SM5.0/RD11 header");
        int[] strides = [60, 24, 32, 40, 36, 12];
        for (int i = 0; i < strides.Length; i++)
            if (UInt(data, 32 + i * 4) != strides[i]) throw Error("unsupported declared record size");

        int bufferCount = Number(data, 0), bindingCount = Number(data, 8);
        DxbcRange bufferRange = Table(data, UInt(data, 4), bufferCount, 24);
        DxbcRange bindingRange = Table(data, UInt(data, 12), bindingCount, 32);
        var budget = new DecodeBudget(data.Length);
        budget.Rows(bufferCount);
        budget.Rows(bindingCount);
        var strings = new Dictionary<int, DxbcRdefString>();
        DxbcRdefString creator = ReadString(data, UInt(data, 24), strings, budget);
        var bindings = new List<DxbcRdefBinding>(bindingCount);
        for (int i = 0; i < bindingCount; i++)
        {
            int row = bindingRange.Offset + i * 32;
            bindings.Add(new(i, new(row, 32), ReadString(data, UInt(data, row), strings, budget),
                UInt(data, row + 4), UInt(data, row + 8), UInt(data, row + 12), UInt(data, row + 16),
                UInt(data, row + 20), UInt(data, row + 24), UInt(data, row + 28)));
        }

        var buffers = new List<DxbcRdefBuffer>(bufferCount);
        var typeOffsets = new Queue<int>();
        var enqueuedTypes = new HashSet<int>();
        for (int i = 0; i < bufferCount; i++)
        {
            int row = bufferRange.Offset + i * 24;
            DxbcRdefString name = ReadString(data, UInt(data, row), strings, budget);
            int variableCount = Number(data, row + 4), size = Number(data, row + 12);
            DxbcRange variableRange = Table(data, UInt(data, row + 8), variableCount, 40);
            budget.Rows(variableCount);
            var variables = new List<DxbcRdefVariable>(variableCount);
            for (int j = 0; j < variableCount; j++)
            {
                int variable = variableRange.Offset + j * 40;
                int start = Number(data, variable + 4), length = Number(data, variable + 8);
                if ((long)start + length > size) throw Error($"buffer {i} variable {j} exceeds its declared buffer size");
                int type = Number(data, variable + 16);
                Require(data, type, 36);
                if (enqueuedTypes.Add(type)) typeOffsets.Enqueue(type);
                uint defaultOffset = UInt(data, variable + 20);
                DxbcRange? defaultRange = defaultOffset == 0 ? null : Table(data, defaultOffset, length, 1);
                variables.Add(new(j, new(variable, 40), ReadString(data, UInt(data, variable), strings, budget),
                    start, length, UInt(data, variable + 12), type, defaultRange,
                    UInt(data, variable + 24), UInt(data, variable + 28), UInt(data, variable + 32), UInt(data, variable + 36)));
            }
            buffers.Add(new(i, new(row, 24), name, size, UInt(data, row + 16), UInt(data, row + 20),
                variableRange, variables.AsReadOnly()));
        }

        // Queue each original type offset once; shared/nested types never expand exponentially.
        var types = new List<DxbcRdefType>();
        while (typeOffsets.TryDequeue(out int offset))
        {
            int memberCount = UShort(data, offset + 10);
            DxbcRange memberRange = Table(data, UInt(data, offset + 12), memberCount, 12);
            budget.Rows(1 + memberCount);
            var members = new List<DxbcRdefMember>(memberCount);
            for (int i = 0; i < memberCount; i++)
            {
                int row = memberRange.Offset + i * 12, type = Number(data, row + 4);
                Require(data, type, 36);
                if (enqueuedTypes.Add(type)) typeOffsets.Enqueue(type);
                members.Add(new(i, new(row, 12), ReadString(data, UInt(data, row), strings, budget), type, UInt(data, row + 8)));
            }
            uint nameOffset = UInt(data, offset + 32);
            types.Add(new(offset, new(offset, 36), UShort(data, offset), UShort(data, offset + 2),
                UShort(data, offset + 4), UShort(data, offset + 6), UShort(data, offset + 8), memberRange,
                members.AsReadOnly(), data.Slice(offset + 16, 16).ToArray(), nameOffset == 0 ? null : ReadString(data, nameOffset, strings, budget)));
        }
        RejectTypeCycles(types);
        return new(program.Length, payloadRange, data[17], data[16], UShort(data, 18), UInt(data, 20),
            UInt(data, 56), data[..60].ToArray(), creator, bufferRange, bindingRange,
            buffers.AsReadOnly(), bindings.AsReadOnly(), types.AsReadOnly());
    }

    private static void RejectTypeCycles(IReadOnlyList<DxbcRdefType> types)
    {
        var byOffset = types.ToDictionary(type => type.Offset);
        var inbound = types.ToDictionary(type => type.Offset, _ => 0);
        foreach (DxbcRdefType type in types)
            foreach (DxbcRdefMember member in type.Members) inbound[member.TypeOffset]++;
        var queue = new Queue<int>(inbound.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        int visited = 0;
        while (queue.TryDequeue(out int offset))
        {
            visited++;
            foreach (DxbcRdefMember member in byOffset[offset].Members)
                if (--inbound[member.TypeOffset] == 0) queue.Enqueue(member.TypeOffset);
        }
        if (visited != types.Count) throw Error("recursive type member references");
    }

    private static DxbcRdefString ReadString(ReadOnlySpan<byte> data, uint rawOffset,
        Dictionary<int, DxbcRdefString> strings, DecodeBudget budget)
    {
        if (rawOffset > int.MaxValue) throw Error("string offset exceeds supported range");
        int offset = (int)rawOffset;
        Require(data, offset, 1);
        if (strings.TryGetValue(offset, out DxbcRdefString? known)) return known;
        int length = data[offset..].IndexOf((byte)0);
        if (length < 0) throw Error($"unterminated string at {offset}");
        budget.Text(length + 1);
        string text;
        try { text = StrictUtf8.GetString(data.Slice(offset, length)); }
        catch (DecoderFallbackException error) { throw new InvalidDataException($"RDEF string at {offset} is not valid UTF-8.", error); }
        var result = new DxbcRdefString(text, new(offset, length + 1));
        strings.Add(offset, result);
        return result;
    }

    // Shared tables and overlapping string suffixes must not expand a small override
    // into quadratic allocations. These are explicit decoder limits, not file semantics.
    private sealed class DecodeBudget(int sourceLength)
    {
        private int rows = sourceLength;
        private int text = sourceLength;
        public void Rows(int count)
        {
            if (count > rows) throw Error("decoded record budget exceeded");
            rows -= count;
        }
        public void Text(int bytes)
        {
            if (bytes > text) throw Error("decoded string budget exceeded");
            text -= bytes;
        }
    }

    private static DxbcRange Table(ReadOnlySpan<byte> data, uint rawOffset, int count, int stride)
    {
        if (rawOffset > int.MaxValue || count < 0 || (long)count * stride > int.MaxValue)
            throw Error("table exceeds supported range");
        int offset = (int)rawOffset, length = count * stride;
        // Empty tables may have a zero offset; never dereference it or invent an entry.
        if (length == 0 && offset == 0) return new(0, 0);
        Require(data, offset, length);
        return new(offset, length);
    }
    private static void Require(ReadOnlySpan<byte> data, int offset, int length)
    {
        if (offset < 60 || length < 0 || (long)offset + length > data.Length)
            throw Error($"range {offset}+{length} exceeds RDEF data");
    }
    private static uint UInt(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
    private static ushort UShort(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
    private static int Number(ReadOnlySpan<byte> data, int offset)
    {
        uint value = UInt(data, offset);
        return value <= int.MaxValue ? (int)value : throw Error($"field at {offset} exceeds supported range");
    }
    private static InvalidDataException Error(string message) => new($"RDEF resource definitions: {message}.");
}
