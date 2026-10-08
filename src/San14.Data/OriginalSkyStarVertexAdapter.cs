using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalSkyStarAdaptedVertex(byte[] Bytes, string Sha256,
    string VertexTranslationSha256, string GeometryTranslationSha256, int SelectedEmitSites,
    int RetainedVertexBodyInstructions, int RetainedGeometryBodyInstructions);

/// <summary>Finite backend composition of the verified Sky2Star VS and GS.
/// The original arithmetic remains in its original order. A four-vertex strip
/// instance runs both programs, selecting one original EmitVertex by VertexIndex.
/// A GS invocation that emits nothing becomes a fully clipped degenerate strip.
/// Only the final raster Y is changed for Godot's positive-height viewport.</summary>
public static class OriginalSkyStarVertexAdapter
{
    public const string VertexSourceSha = "B35F8B7BF6844AE8D586CC680D6395B41C7268BD60060FED655C82B9DCCBC334";
    public const string GeometrySourceSha = "44D01DEDF65D5F1814F6C08BD38A11BFA8FEA498FDBF9478C330C9FFB10BE3DF";
    public const string VertexTranslationSha = "D787E0238BD67D78FED979397AB0DDF10BDC1BF6DA74447CE8F0327E28FBC93B";
    public const string GeometryTranslationSha = "CED8BFFCF397B6953CD25A8D7C251694D27595D728D47FF03E64015EC105A9D5";
    private sealed record Instruction(ushort Opcode, uint[] Words);
    private static uint[] Op(ushort opcode, params uint[] operands)
        => new[] { checked((uint)(operands.Length + 1) << 16) | opcode }.Concat(operands).ToArray();

    public static OriginalSkyStarAdaptedVertex Adapt(OriginalShaderSpirv vertex, OriginalShaderSpirv geometry)
    {
        ArgumentNullException.ThrowIfNull(vertex); ArgumentNullException.ThrowIfNull(geometry);
        if (vertex.SourceProgramSha256 != VertexSourceSha || geometry.SourceProgramSha256 != GeometrySourceSha ||
            vertex.SpirvSha256 != VertexTranslationSha || geometry.SpirvSha256 != GeometryTranslationSha ||
            Convert.ToHexString(SHA256.HashData(vertex.Bytes)) != vertex.SpirvSha256 ||
            Convert.ToHexString(SHA256.HashData(geometry.Bytes)) != geometry.SpirvSha256)
            throw new InvalidDataException("Star adapter requires the exact original default VS and GS translations.");
        var vi = SpirvShaderInterfaceReader.Read(vertex.Bytes); var gi = SpirvShaderInterfaceReader.Read(geometry.Bytes);
        if (vi.EntryPoints.Single().ExecutionModel != 0 || gi.EntryPoints.Single().ExecutionModel != 3 ||
            vi.Descriptors.Count != 1 || gi.Descriptors.Count != 1 ||
            vi.Descriptors[0] is not { DescriptorSet: 1, Binding: 0, RequiredBufferBytes: 352 } ||
            gi.Descriptors[0] is not { DescriptorSet: 2, Binding: 0, RequiredBufferBytes: 16 } ||
            !gi.ExecutionModes.Any(m => m.Mode == 19) || !gi.ExecutionModes.Any(m => m.Mode == 29) ||
            !gi.ExecutionModes.Any(m => m.Mode == 26 && m.Operands.SequenceEqual(new uint[] { 4 })) ||
            !gi.ExecutionModes.Any(m => m.Mode == 0 && m.Operands.SequenceEqual(new uint[] { 1 })))
            throw new InvalidDataException("Star adapter requires native point input, four triangle-strip outputs and full CBs.");
        var vs = Parse(vertex.Bytes); var gs = Parse(geometry.Bytes);
        var vOutputs = vi.StageVariables.Where(v => v.StorageClass == 3).Select(v => v.VariableId).ToHashSet();
        var gInputs = gi.StageVariables.Where(v => v.StorageClass == 1).ToDictionary(v => v.VariableId,
            v => vi.StageVariables.Single(o => o.StorageClass == 3 && o.Location == v.Location).VariableId);
        if (vi.StageVariables.Count(v => v.StorageClass == 1) != 3 || vOutputs.Count != 4 || gInputs.Count != 3 ||
            vi.StageVariables.Single(v => v.BuiltIn == 1).StorageClass != 3 ||
            vi.StageVariables.Single(v => v.Location == 0 && v.StorageClass == 3).Type.ComponentCount != 4 ||
            vi.StageVariables.Single(v => v.Location == 1 && v.StorageClass == 3).Type.ComponentCount != 4 ||
            vi.StageVariables.Single(v => v.Location == 2 && v.StorageClass == 3).Type.ComponentCount != 2 ||
            gi.StageVariables.Any(v => v.InterpolationDecorations.Count != 0))
            throw new InvalidDataException("Star VS/GS varying contract differs from the verified source.");

        // Original VS output pointer types become Private, retaining all stores
        // and component access chains. Their former interface decorations vanish.
        var vGlobals = new List<uint[]>(); var vBody = new List<Instruction>(); bool inFunction = false;
        foreach (var i in vs)
        {
            uint[] w = i.Words.ToArray();
            if (i.Opcode == 54) inFunction = true;
            if (inFunction) { vBody.Add(i); continue; }
            if (i.Opcode == 15) continue;
            if (i.Opcode == 71 && vOutputs.Contains(w[1])) continue;
            if (i.Opcode == 32 && w[2] == 3) w[2] = 6;
            if (i.Opcode == 59 && vOutputs.Contains(w[2])) w[3] = 6;
            vGlobals.Add(w);
        }
        if (vBody.Count(i => i.Opcode == 253) != 1 || vBody.Count(i => i.Opcode == 54) != 1)
            throw new InvalidDataException("Star VS needs exactly one function and return.");
        uint next = vi.IdBound; uint Allocate() => checked(next++);
        var map = new Dictionary<uint, uint>();
        var declarations = new List<uint[]>(); var gAnnotations = new List<uint[]>();
        var gDebug = new List<uint[]>(); var gImports = new List<uint[]>();
        var types = new Dictionary<string, uint>();
        static string Key(uint[] w) => w[0] % 65536 + ":" + string.Join(",", w.Skip(2));
        foreach (var w in vGlobals.Where(w => (ushort)w[0] is >= 19 and <= 33))
            types.TryAdd(Key(w), w[1]);
        uint Map(uint id) => map.TryGetValue(id, out uint value) ? value : map[id] = Allocate();
        bool gFunction = false; var gBody = new List<Instruction>();
        foreach (var i in gs)
        {
            if (i.Opcode == 54) gFunction = true;
            if (gFunction) { gBody.Add(i); continue; }
            uint[] w = i.Words;
            if (i.Opcode is 14 or 15 or 16 or 17) continue;
            if (i.Opcode == 5 && (gInputs.ContainsKey(w[1]) || w[1] == gi.EntryPoints[0].FunctionId)) continue;
            if (i.Opcode == 71 && (gInputs.ContainsKey(w[1]) || gi.StageVariables.Any(v => v.VariableId == w[1] && v.StorageClass == 3))) continue;
            if (i.Opcode == 59 && gInputs.ContainsKey(w[2])) { Map(w[2]); continue; }
            var remapped = Remap(i, Map);
            if (i.Opcode == 32 && remapped[2] is 1 or 3) remapped[2] = 6;
            if (i.Opcode == 59 && remapped[3] == 3) remapped[3] = 6;
            if (i.Opcode is >= 19 and <= 33)
            {
                // Deduplicate only scalar/vector/pointer/function types. Array
                // and struct types retain original decorations and identities.
                if (i.Opcode is 19 or 20 or 21 or 22 or 23 or 32 or 33 && types.TryGetValue(Key(remapped), out uint common))
                    map[w[1]] = common;
                else { declarations.Add(remapped); if (i.Opcode is 19 or 20 or 21 or 22 or 23 or 32 or 33) types.TryAdd(Key(remapped), remapped[1]); }
            }
            else if (i.Opcode is 71 or 72) gAnnotations.Add(remapped);
            else if (i.Opcode == 11) gImports.Add(remapped);
            else if (i.Opcode is 3 or 5 or 6 or 7) gDebug.Add(remapped);
            else declarations.Add(remapped);
        }
        if (gBody.Count(i => i.Opcode == 218) != 4 || gBody.Count(i => i.Opcode == 219) != 1 ||
            gBody.Count(i => i.Opcode == 253) != 1 || gBody.Count(i => i.Opcode == 54) != 1)
            throw new InvalidDataException("Star GS needs the exact four EmitVertex sites and one EndPrimitive.");

        uint Type(ushort opcode, params uint[] operands)
        {
            uint[] w = Op(opcode, new uint[] { 0 }.Concat(operands).ToArray());
            if (types.TryGetValue(Key(w), out uint known)) return known;
            uint id = Allocate(); w[1] = id; types.Add(Key(w), id); declarations.Add(w); return id;
        }
        uint f32 = Type(22, 32), i32 = Type(21, 32, 1), b = Type(20);
        uint f3 = Type(23, f32, 3), f4 = Type(23, f32, 4), b3 = Type(23, b, 3), b4 = Type(23, b, 4);
        uint outputF3 = Type(32, 3, f3), outputF4 = Type(32, 3, f4), inputI32 = Type(32, 1, i32);
        uint position = Allocate(), color = Allocate(), uv = Allocate(), index = Allocate();
        declarations.Add(Op(59, outputF4, position, 3)); declarations.Add(Op(59, outputF4, color, 3));
        declarations.Add(Op(59, outputF3, uv, 3)); declarations.Add(Op(59, inputI32, index, 1));
        gAnnotations.Add(Op(71, position, 11, 0)); gAnnotations.Add(Op(71, color, 30, 1));
        gAnnotations.Add(Op(71, uv, 30, 2)); gAnnotations.Add(Op(71, index, 11, 42));
        uint Constant(uint type, uint literal) { uint id = Allocate(); declarations.Add(Op(43, type, id, literal)); return id; }
        uint zero = Constant(f32, 0), one = Constant(f32, 0x3F800000), two = Constant(f32, 0x40000000);
        uint clipped = Allocate(), black = Allocate(), uvZero = Allocate();
        declarations.Add(Op(44, f4, clipped, zero, zero, two, one));
        declarations.Add(Op(44, f4, black, zero, zero, zero, zero)); declarations.Add(Op(44, f3, uvZero, zero, zero, zero));
        uint[] ordinals = Enumerable.Range(0, 4).Select(n => Constant(i32, (uint)n)).ToArray();
        var inline = new List<uint[]> { Op(62, position, clipped), Op(62, color, black), Op(62, uv, uvZero) };
        uint gPosition = Map(gi.StageVariables.Single(v => v.BuiltIn == 0).VariableId);
        uint gColor = Map(gi.StageVariables.Single(v => v.StorageClass == 3 && v.Location == 1).VariableId);
        uint gUv = Map(gi.StageVariables.Single(v => v.StorageClass == 3 && v.Location == 2).VariableId);
        int emit = 0, inputChains = 0, retained = 0; bool initialLabel = false;
        foreach (var i in gBody)
        {
            if (i.Opcode is 54 or 56 or 253 or 219) continue;
            if (i.Opcode == 248 && !initialLabel) { initialLabel = true; continue; }
            if (i.Opcode == 218)
            {
                uint loadedIndex = Allocate(), select = Allocate();
                inline.Add(Op(61, i32, loadedIndex, index)); inline.Add(Op(170, b, select, loadedIndex, ordinals[emit++]));
                // This retained source module is SPIR-V 1.0: vector OpSelect
                // needs a same-width boolean vector condition.
                uint select3 = Allocate(), select4 = Allocate();
                inline.Add(Op(80, b3, select3, select, select, select));
                inline.Add(Op(80, b4, select4, select, select, select, select));
                foreach (var item in new[] { (Type: f4, Input: gPosition, Output: position), (Type: f4, Input: gColor, Output: color), (Type: f3, Input: gUv, Output: uv) })
                {
                    uint candidate = Allocate(), previous = Allocate(), selected = Allocate();
                    inline.Add(Op(61, item.Type, candidate, item.Input)); inline.Add(Op(61, item.Type, previous, item.Output));
                    inline.Add(Op(169, item.Type, selected, item.Type == f3 ? select3 : select4, candidate, previous)); inline.Add(Op(62, item.Output, selected));
                }
                continue;
            }
            if (i.Opcode is 65 or 66 && gInputs.TryGetValue(i.Words[3], out uint originalOutput))
            {
                if (i.Words.Length != 5 || gs.Single(c => c.Opcode == 43 && c.Words[2] == i.Words[4]).Words[3] != 0)
                    throw new InvalidDataException("Star GS input needs the original zero point-array index.");
                // OpCopyObject requires the identical pointer type ID, not only
                // an equivalent Private pointer declaration from the GS module.
                uint originalPointer = vGlobals.Single(w => (ushort)w[0] == 59 && w[2] == originalOutput)[1];
                inline.Add(Op(83, originalPointer, Map(i.Words[2]), originalOutput)); inputChains++; continue;
            }
            inline.Add(Remap(i, Map)); retained++;
        }
        if (emit != 4 || inputChains != 15) throw new InvalidDataException("Star GS input or emit schedule differs.");
        uint loaded = Allocate(), y = Allocate(), negative = Allocate(), changed = Allocate();
        inline.Add(Op(61, f4, loaded, position)); inline.Add(Op(81, f32, y, loaded, 1));
        inline.Add(Op(127, f32, negative, y)); inline.Add(Op(82, f4, changed, negative, loaded, 1)); inline.Add(Op(62, position, changed));

        // SPIR-V logical ordering: imports, memory, entry, debug, annotations,
        // declarations, then the original VS function with the GS inlined.
        var output = new List<uint>(); uint[] header = Words(vertex.Bytes).Take(5).ToArray(); header[3] = next; output.AddRange(header);
        foreach (var w in vGlobals.Where(w => (ushort)w[0] == 17)) output.AddRange(w);
        foreach (var w in vGlobals.Where(w => (ushort)w[0] is 10 or 11)) output.AddRange(w);
        foreach (var w in gImports) output.AddRange(w);
        output.AddRange(vGlobals.Single(w => (ushort)w[0] == 14));
        uint[] entryPrefix = vs.Single(i => i.Opcode == 15).Words.Take(5).ToArray();
        uint[] interfaces = vi.EntryPoints[0].InterfaceIds.Where(id => !vOutputs.Contains(id)).Concat(new[] { position, color, uv, index }).ToArray();
        output.AddRange(Op(15, entryPrefix.Skip(1).Concat(interfaces).ToArray()));
        // OpString/OpSource precede all OpName instructions even when composing
        // two modules. Keep their original operands and separate debug order.
        var debug = vGlobals.Where(w => (ushort)w[0] is 3 or 5 or 6 or 7).Concat(gDebug).ToArray();
        foreach (var w in debug.Where(w => (ushort)w[0] == 7)) output.AddRange(w);
        foreach (var w in debug.Where(w => (ushort)w[0] == 3)) output.AddRange(w);
        foreach (var w in debug.Where(w => (ushort)w[0] is 5 or 6)) output.AddRange(w);
        foreach (var w in vGlobals.Where(w => (ushort)w[0] is 71 or 72)) output.AddRange(w);
        foreach (var w in gAnnotations) output.AddRange(w);
        foreach (var w in vGlobals.Where(w => (ushort)w[0] is >= 19 and <= 52 or 59)) output.AddRange(w);
        foreach (var w in declarations) output.AddRange(w);
        foreach (var i in vBody) { if (i.Opcode == 253) foreach (var w in inline) output.AddRange(w); output.AddRange(i.Words); }
        byte[] bytes = new byte[checked(output.Count * 4)];
        for (int at = 0; at < output.Count; at++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at * 4, 4), output[at]);
        var result = SpirvShaderInterfaceReader.Read(bytes);
        if (result.EntryPoints[0].ExecutionModel != 0 || result.Capabilities.Contains(2u) || result.Descriptors.Count != 2 ||
            result.StageVariables.Count(v => v.StorageClass == 3) != 3 ||
            result.StageVariables.Single(v => v.BuiltIn == 42).StorageClass != 1)
            throw new InvalidDataException("Star adapter changed the bounded final vertex interface.");
        return new(bytes, result.Sha256, vertex.SpirvSha256, geometry.SpirvSha256, emit, vBody.Count, retained);
    }

    private static uint[] Words(ReadOnlySpan<byte> bytes)
    { var words = new uint[bytes.Length / 4]; for (int i = 0; i < words.Length; i++) words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(i * 4)..]); return words; }
    private static List<Instruction> Parse(ReadOnlySpan<byte> bytes)
    { var words = Words(bytes); var result = new List<Instruction>(); for (int at = 5; at < words.Length;) { int n = (int)(words[at] >> 16); if (n < 1 || n > words.Length - at) throw new InvalidDataException("Truncated star SPIR-V."); result.Add(new((ushort)words[at], words.AsSpan(at, n).ToArray())); at += n; } return result; }

    private static uint[] Remap(Instruction instruction, Func<uint, uint> map)
    {
        uint[] w = instruction.Words.ToArray();
        void Id(int at) { w[at] = map(w[at]); }
        void Ids(int start, int end) { for (int i = start; i < end; i++) Id(i); }
        switch (instruction.Opcode)
        {
            case 3: if (w.Length > 3) Id(3); break; // language/version are literals
            case 5: case 6: Id(1); break;
            case 7: case 11: case 19: case 20: case 22: Id(1); break;
            case 21: Id(1); break;
            case 23: Ids(1, 3); break;
            case 28: case 30: case 33: Ids(1, w.Length); break;
            case 32: Id(1); Id(3); break;
            case 43: Ids(1, 3); break;
            case 44: Ids(1, w.Length); break;
            case 59: Ids(1, 3); if (w.Length > 4) Id(4); break;
            case 71: case 72: Id(1); break;
            case 8: Id(1); break;
            case 12: Ids(1, 4); Ids(5, w.Length); break;
            case 79: Ids(1, 5); break;
            case 81: Ids(1, 4); break;
            case 82: Ids(1, 5); break;
            case 247: Id(1); break;
            case 248: case 249: Id(1); break;
            case 250: Ids(1, 4); break;
            case 61: case 62: case 63: case 65: case 66: case 83: case 124: case 127:
            case 129: case 133: case 169: case 170: case 171: case 184: case 197: case 199: Ids(1, w.Length); break;
            default: throw new InvalidDataException("Unsupported star GS translation instruction: " + instruction.Opcode);
        }
        return w;
    }
}

public sealed record OriginalSkyStarAnalyticPixel(byte[] Bytes, string Sha256,
    string SourceTranslationSha256, int RetainedAnalyticBodyInstructions, int RemovedConditionalInstructions);

/// <summary>The verified original PS's finite g_vStarInfo.w &lt;= 0 branch.
/// Removes its unreachable selector/texture block and unreferenced sampler/t0
/// variables. Every analytic and output instruction keeps its original bytes.
/// The draw must enforce the branch domain on each complete source packet.</summary>
public static class OriginalSkyStarPixelAdapter
{
    public const string PixelSourceSha = "069F085CE59681A57144B04C878211C40EE4B62F90503B31331E731A872B40D3";
    public const string PixelTranslationSha = "1E462D832D085C07F813FF5E835BE64EC3BDA25CCCA1C60F89605B6F14BF3B64";

    public static void ValidateAnalyticPacket(ReadOnlySpan<byte> pixel)
    {
        if (pixel.Length != 16 || !float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(pixel[12..])) ||
            BinaryPrimitives.ReadSingleLittleEndian(pixel[12..]) > 0)
            throw new InvalidDataException("Textureless Star draw requires every complete g_vStarInfo.w <= 0 packet.");
    }

    public static OriginalSkyStarAnalyticPixel AdaptAnalytic(OriginalShaderSpirv source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SourceProgramSha256 != PixelSourceSha || source.SpirvSha256 != PixelTranslationSha ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != PixelTranslationSha)
            throw new InvalidDataException("Analytic Star adapter requires the pinned original PS and translation.");
        var original = SpirvShaderInterfaceReader.Read(source.Bytes);
        if (original.EntryPoints.Single().ExecutionModel != 4 || original.Descriptors.Count != 3 ||
            original.Descriptors.Single(v => v.Binding == 2) is not { DescriptorSet: 0, RequiredBufferBytes: 16 })
            throw new InvalidDataException("Analytic Star source pixel interface differs.");
        uint[] words = new uint[source.Bytes.Length / 4];
        for (int i = 0; i < words.Length; i++) words[i] = BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan(i * 4, 4));
        var instructions = new List<uint[]>();
        for (int at = 5; at < words.Length;)
        { int count = (int)(words[at] >> 16); instructions.Add(words.AsSpan(at, count).ToArray()); at += count; }
        static ushort Code(uint[] w) => (ushort)w[0];
        uint[] branch = instructions.Single(w => Code(w) == 250);
        uint trueLabel = branch[2], falseLabel = branch[3];
        int initialAt = instructions.FindIndex(w => Code(w) == 248),
            falseAt = instructions.FindIndex(w => Code(w) == 248 && w[1] == falseLabel);
        int trueAt = instructions.FindIndex(w => Code(w) == 248 && w[1] == trueLabel);
        if (initialAt < 0 || trueAt <= initialAt || falseAt <= trueAt ||
            instructions.Count(w => Code(w) == 54) != 1 || instructions.Count(w => Code(w) == 253) != 1 ||
            instructions.Count(w => Code(w) == 247) != 1 || instructions.Count(w => Code(w) == 248) != 4 ||
            !instructions.Skip(trueAt).Take(falseAt - trueAt).Any(w => Code(w) == 87))
            throw new InvalidDataException("Analytic Star source branch structure differs.");
        var resources = original.Descriptors.Where(v => v.Binding is 0 or 1).Select(v => v.VariableId).ToHashSet();
        var removedNames = resources.Concat(new[] { trueLabel, falseLabel }).ToHashSet();
        var result = new List<uint>(words.Length); result.AddRange(words.Take(5));
        for (int at = 0; at < instructions.Count; at++)
        {
            uint[] w = instructions[at];
            if (at > initialAt && at <= falseAt) continue;
            if (Code(w) is 5 or 71 && removedNames.Contains(w[1])) continue;
            if (Code(w) == 59 && resources.Contains(w[2])) continue;
            result.AddRange(w);
        }
        byte[] adapted = new byte[checked(result.Count * 4)];
        for (int i = 0; i < result.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(adapted.AsSpan(i * 4, 4), result[i]);
        var final = SpirvShaderInterfaceReader.Read(adapted);
        if (final.Descriptors.Count != 1 || final.Descriptors[0] is not { DescriptorSet: 0, Binding: 2, RequiredBufferBytes: 16 } ||
            final.StageVariables.Count != original.StageVariables.Count)
            throw new InvalidDataException("Analytic Star specialization changed the bounded output/CB interface.");
        int analyticCount = instructions.Skip(falseAt + 1).TakeWhile(w => Code(w) != 249).Count();
        return new(adapted, final.Sha256, source.SpirvSha256, analyticCount, falseAt - initialAt);
    }
}
