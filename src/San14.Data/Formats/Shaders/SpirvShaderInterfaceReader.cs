using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data.Formats;

public sealed record SpirvBlockMember(uint Index, uint? Offset, SpirvInterfaceType Type);
public sealed record SpirvInterfaceType(string Kind, uint? Bits = null, bool? Signed = null,
    uint? ComponentCount = null, SpirvInterfaceType? ComponentType = null,
    SpirvInterfaceType? ElementType = null, uint? Count = null, uint? ArrayStride = null,
    bool Block = false, IReadOnlyList<SpirvBlockMember>? Members = null,
    uint? Dimension = null, uint? DepthCode = null, bool? Arrayed = null,
    bool? Multisampled = null, uint? SampledCode = null, uint? ImageFormat = null,
    bool BufferBlock = false);
public sealed record SpirvInterfaceVariable(uint VariableId, string? Name, uint StorageClass,
    SpirvInterfaceType Type, uint? DescriptorSet, uint? Binding, uint? Location, uint? BuiltIn,
    IReadOnlyList<uint> InterpolationDecorations, uint? RequiredBufferBytes);
public sealed record SpirvEntryPoint(uint ExecutionModel, uint FunctionId, string Name,
    IReadOnlyList<uint> InterfaceIds);
public sealed record SpirvExecutionMode(uint FunctionId, uint Mode, IReadOnlyList<uint> Operands);
public sealed record SpirvShaderInterface(string Sha256, uint VersionWord, uint GeneratorWord,
    uint IdBound, IReadOnlyList<uint> Capabilities, IReadOnlyList<string> Extensions,
    IReadOnlyList<SpirvEntryPoint> EntryPoints, IReadOnlyList<SpirvExecutionMode> ExecutionModes,
    IReadOnlyList<SpirvInterfaceVariable> Descriptors, IReadOnlyList<SpirvInterfaceVariable> StageVariables);

/// <summary>Reads declared interfaces of the verified vkd3d SPIR-V output.
/// This is a bounded interface reader, not a semantic validator or execution proof.
/// Unsupported interface types and missing layouts are rejected explicitly.</summary>
public static class SpirvShaderInterfaceReader
{
    public static SpirvShaderInterface Read(ReadOnlySpan<byte> source)
    {
        try { return ReadCore(source); }
        catch (OverflowException error) { throw new InvalidDataException("SPIR-V interface byte layout exceeds its supported range.", error); }
    }

    private static SpirvShaderInterface ReadCore(ReadOnlySpan<byte> source)
    {
        if (source.Length < 20 || source.Length % 4 != 0 || source.Length > 64 * 1024 * 1024)
            throw new InvalidDataException("SPIR-V byte range is invalid.");
        uint[] words = new uint[source.Length / 4];
        for (int i = 0; i < words.Length; i++) words[i] = BinaryPrimitives.ReadUInt32LittleEndian(source[(i * 4)..]);
        if (words[0] != 0x07230203 || words[1] != 0x00010000 || words[3] == 0 || words[4] != 0)
            throw new InvalidDataException("Interface reader requires little-endian SPIR-V 1.0 with a zero schema word.");
        uint bound = words[3];
        var names = new Dictionary<uint, string>();
        var types = new Dictionary<uint, (ushort Opcode, uint[] Operands)>();
        var constants = new Dictionary<uint, (uint Type, uint[] Words)>();
        var variables = new Dictionary<uint, (uint PointerType, uint Storage)>();
        var decorations = new Dictionary<uint, Dictionary<uint, uint[]>>();
        var memberDecorations = new Dictionary<(uint Type, uint Member), Dictionary<uint, uint[]>>();
        var capabilities = new List<uint>(); var extensions = new List<string>();
        var entries = new List<SpirvEntryPoint>(); var modes = new List<SpirvExecutionMode>();
        var declarationIds = new HashSet<uint>();
        void Id(uint id) { if (id == 0 || id >= bound) throw new InvalidDataException("SPIR-V identifier is outside its bound."); }
        void Declare(uint id) { Id(id); if (!declarationIds.Add(id)) throw new InvalidDataException("Duplicate SPIR-V interface declaration ID."); }
        static void Require(ReadOnlySpan<uint> args, int count)
        { if (args.Length < count) throw new InvalidDataException("Truncated SPIR-V interface instruction."); }
        static (string Text, int Words) String(ReadOnlySpan<uint> args)
        {
            byte[] bytes = new byte[args.Length * 4];
            for (int i = 0; i < args.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), args[i]);
            int end = Array.IndexOf(bytes, (byte)0);
            if (end < 0) throw new InvalidDataException("Unterminated SPIR-V string.");
            int count = end / 4 + 1;
            for (int i = end; i < count * 4; i++)
                if (bytes[i] != 0) throw new InvalidDataException("SPIR-V string padding is not zero.");
            try { return (new UTF8Encoding(false, true).GetString(bytes, 0, end), count); }
            catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid SPIR-V UTF-8 string.", error); }
        }
        for (int at = 5; at < words.Length;)
        {
            int count = (int)(words[at] >> 16); ushort opcode = (ushort)words[at];
            if (count == 0 || count > words.Length - at) throw new InvalidDataException("SPIR-V instruction exceeds its source.");
            ReadOnlySpan<uint> a = words.AsSpan(at + 1, count - 1);
            switch (opcode)
            {
                case 5: // OpName
                    Require(a, 2); Id(a[0]);
                    if (!names.TryAdd(a[0], String(a[1..]).Text)) throw new InvalidDataException("Duplicate SPIR-V interface name.");
                    break;
                case 10: Require(a, 1); extensions.Add(String(a).Text); break;
                case 15:
                    Require(a, 3); Id(a[1]); var name = String(a[2..]);
                    uint[] ids = a[(2 + name.Words)..].ToArray(); foreach (uint id in ids) Id(id);
                    entries.Add(new(a[0], a[1], name.Text, ids)); break;
                case 16: Require(a, 2); Id(a[0]); modes.Add(new(a[0], a[1], a[2..].ToArray())); break;
                case 17: Require(a, 1); capabilities.Add(a[0]); break;
                case >= 19 and <= 33: // Type declarations, only reachable interface types are decoded below.
                    Require(a, 1); Declare(a[0]); types.Add(a[0], (opcode, a[1..].ToArray())); break;
                case 43: // OpConstant
                    Require(a, 3); Id(a[0]); Declare(a[1]); constants.Add(a[1], (a[0], a[2..].ToArray())); break;
                case 59:
                    Require(a, 3); Id(a[0]); Declare(a[1]); variables.Add(a[1], (a[0], a[2])); break;
                case 71:
                    Require(a, 2); Id(a[0]);
                    if (!decorations.TryGetValue(a[0], out var d)) decorations.Add(a[0], d = []);
                    if (!d.TryAdd(a[1], a[2..].ToArray())) throw new InvalidDataException("Duplicate SPIR-V decoration.");
                    break;
                case 72:
                    Require(a, 3); Id(a[0]);
                    if (!memberDecorations.TryGetValue((a[0], a[1]), out var m)) memberDecorations.Add((a[0], a[1]), m = []);
                    if (!m.TryAdd(a[2], a[3..].ToArray())) throw new InvalidDataException("Duplicate SPIR-V member decoration.");
                    break;
            }
            at += count;
        }
        if (entries.Count != 1) throw new InvalidDataException("Interface reader requires one explicit entry point.");
        static uint? One(Dictionary<uint, uint[]>? d, uint key)
        {
            if (d is null || !d.TryGetValue(key, out uint[]? values)) return null;
            if (values.Length != 1) throw new InvalidDataException("SPIR-V numeric decoration has invalid width.");
            return values[0];
        }
        uint ArrayLength(uint id)
        {
            if (!constants.TryGetValue(id, out var c) || c.Words.Length != 1 ||
                !types.TryGetValue(c.Type, out var t) || t.Opcode != 21 ||
                !t.Operands.SequenceEqual(new uint[] { 32, 0 }) || c.Words[0] == 0)
                throw new InvalidDataException("SPIR-V interface array needs a nonzero uint32 constant length.");
            return c.Words[0];
        }
        var cache = new Dictionary<uint, SpirvInterfaceType>(); var visiting = new HashSet<uint>();
        SpirvInterfaceType Describe(uint id)
        {
            Id(id);
            if (cache.TryGetValue(id, out var known)) return known;
            if (!types.TryGetValue(id, out var t) || !visiting.Add(id) || visiting.Count > 32)
                throw new InvalidDataException("Missing, recursive or excessively nested SPIR-V interface type.");
            uint[] a = t.Operands; decorations.TryGetValue(id, out var d);
            SpirvInterfaceType result;
            switch (t.Opcode)
            {
                case 21:
                    Require(a, 2); if (a[0] != 32 || a[1] > 1) throw new InvalidDataException("Unsupported interface integer type.");
                    result = new("int", Bits: a[0], Signed: a[1] != 0); break;
                case 22:
                    Require(a, 1); if (a[0] != 32) throw new InvalidDataException("Unsupported interface float width.");
                    result = new("float", Bits: a[0]); break;
                case 23:
                    Require(a, 2); if (a[1] is < 2 or > 4) throw new InvalidDataException("Unsupported vector width.");
                    result = new("vector", ComponentCount: a[1], ComponentType: Describe(a[0])); break;
                case 25:
                    Require(a, 7); if (a[2] > 2 || a[3] > 1 || a[4] > 1) throw new InvalidDataException("Invalid image interface flags.");
                    result = new("image", ComponentType: Describe(a[0]), Dimension: a[1], DepthCode: a[2],
                        Arrayed: a[3] != 0, Multisampled: a[4] != 0, SampledCode: a[5], ImageFormat: a[6]); break;
                case 26: result = new("sampler"); break;
                case 28:
                    Require(a, 2); result = new("array", ElementType: Describe(a[0]), Count: ArrayLength(a[1]), ArrayStride: One(d, 6)); break;
                case 29:
                    Require(a,1);var element=Describe(a[0]);uint? runtimeStride=One(d,6);
                    if(element is not{Kind:"int",Bits:32,Signed:false}||runtimeStride!=4)
                        throw new NotSupportedException("Only explicit uint32/stride4 storage runtime arrays are supported.");
                    result=new("runtime-array",ElementType:element,ArrayStride:runtimeStride);break;
                case 30:
                    var members = new List<SpirvBlockMember>();
                    for (uint i = 0; i < a.Length; i++)
                    {
                        memberDecorations.TryGetValue((id, i), out var md);
                        members.Add(new(i, One(md, 35), Describe(a[i])));
                    }
                    bool block = d?.ContainsKey(2) == true;
                    if (block && d![2].Length != 0) throw new InvalidDataException("Invalid Block decoration.");
                    bool bufferBlock=d?.ContainsKey(3)==true;
                    if(bufferBlock&&(block||d![3].Length!=0||members.Count!=1||members[0].Offset!=0||members[0].Type.Kind!="runtime-array"))
                        throw new InvalidDataException("Unsupported storage BufferBlock layout.");
                    result = new("struct", Block: block, Members: members.AsReadOnly(),BufferBlock:bufferBlock); break;
                default: throw new InvalidDataException($"SPIR-V interface type opcode {t.Opcode} is outside the supported contract.");
            }
            visiting.Remove(id); cache.Add(id, result); return result;
        }
        uint Bytes(SpirvInterfaceType t) => t.Kind switch
        {
            "int" or "float" => t.Bits!.Value / 8,
            "vector" => checked(t.ComponentCount!.Value * Bytes(t.ComponentType!)),
            "array" when t.ArrayStride is uint stride && stride >= Bytes(t.ElementType!) => checked(stride * t.Count!.Value),
            "struct" when t.Members!.Count != 0 => t.Members.Max(m => checked((m.Offset ??
                throw new InvalidDataException("Uniform member lacks its source Offset.")) + Bytes(m.Type))),
            _ => throw new InvalidDataException("Uniform block has an unsupported or missing source byte layout.")
        };
        var descriptors = new List<SpirvInterfaceVariable>(); var stageVariables = new List<SpirvInterfaceVariable>();
        var slots = new HashSet<(uint Set, uint Binding)>();
        foreach (var (id, variable) in variables)
        {
            if (variable.Storage is 9 or 10 or 11 or 12)
                throw new InvalidDataException("Additional SPIR-V resource storage is outside the supported interface contract.");
            if (variable.Storage is not (0 or 1 or 2 or 3)) continue;
            if (!types.TryGetValue(variable.PointerType, out var pointer) || pointer.Opcode != 32 ||
                pointer.Operands.Length != 2 || pointer.Operands[0] != variable.Storage)
                throw new InvalidDataException("SPIR-V interface variable has an inconsistent pointer storage class.");
            SpirvInterfaceType type = Describe(pointer.Operands[1]); decorations.TryGetValue(id, out var d);
            uint? set = One(d, 34), binding = One(d, 33), location = One(d, 30), builtin = One(d, 11);
            uint? bytes = variable.Storage == 2 && type.Kind == "struct" && type.Block ? Bytes(type) : null;
            var item = new SpirvInterfaceVariable(id, names.GetValueOrDefault(id), variable.Storage, type,
                set, binding, location, builtin, new uint[] { 13, 14, 16, 17 }.Where(x => d?.ContainsKey(x) == true).ToArray(), bytes);
            if (variable.Storage is 0 or 2)
            {
                if (set is null || binding is null || !slots.Add((set.Value, binding.Value)) ||
                    (variable.Storage == 2 && bytes is null&&!type.BufferBlock))
                    throw new InvalidDataException("Descriptor needs a unique explicit set/binding and a supported block layout.");
                descriptors.Add(item);
            }
            else
            {
                if (!entries[0].InterfaceIds.Contains(id) || (location is null && builtin is null))
                    throw new InvalidDataException("Stage variable lacks its explicit entry-point location or builtin.");
                stageVariables.Add(item);
            }
        }
        return new(Convert.ToHexString(SHA256.HashData(source)), words[1], words[2], bound,
            capabilities.AsReadOnly(), extensions.AsReadOnly(), entries.AsReadOnly(), modes.AsReadOnly(),
            descriptors.OrderBy(x => x.DescriptorSet).ThenBy(x => x.Binding).ToArray(), stageVariables.AsReadOnly());
    }
}
