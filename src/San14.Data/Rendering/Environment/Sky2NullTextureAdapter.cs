using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed class Sky2NullTextureSpirv
{
    private readonly byte[] bytes;
    internal Sky2NullTextureSpirv(byte[] bytes, SpirvShaderInterface shaderInterface,
        ShaderSpirv source, uint[] slots, IReadOnlyList<Sky2NullTextureBinding> bindings,
        Sky2LoadedNullTexturePacketInterface packetInterface, DxbcResourceDefinitions definitions,
        int replaced, int retained)
    {
        this.bytes = bytes; Interface = shaderInterface; Sha256 = shaderInterface.Sha256;
        UnspecializedTranslationSha256 = source.SpirvSha256;
        SourcePixelSha256 = source.SourceProgramSha256;
        NullPixelRegisters = Array.AsReadOnly(slots);
        NullTextureBindings = Array.AsReadOnly(bindings.ToArray());
        SourceEvidence = Array.AsReadOnly(bindings.Select(b => b.Role.ToString()).ToArray());
        PacketInterface = packetInterface; SourceDefinitions = definitions;
        ReplacedSamples = replaced; RetainedInstructions = retained;
    }
    public ReadOnlyMemory<byte> Bytes => bytes;
    public byte[] CopyBytes() => bytes.ToArray();
    public SpirvShaderInterface Interface { get; }
    public string Sha256 { get; }
    public string UnspecializedTranslationSha256 { get; }
    public string SourcePixelSha256 { get; }
    public IReadOnlyList<uint> NullPixelRegisters { get; }
    public IReadOnlyList<string> SourceEvidence { get; }
    public IReadOnlyList<Sky2NullTextureBinding> NullTextureBindings { get; }
    public int ReplacedSamples { get; }
    public int RetainedInstructions { get; }
    internal Sky2LoadedNullTexturePacketInterface PacketInterface { get; }
    internal DxbcResourceDefinitions SourceDefinitions { get; }
}

/// <summary>Adapt floating Texture2D sampling to the D3D11 null result.
/// Samples become float4(+0) only for captured null slots of the current binding owner.
/// Non-null programs and all other instructions remain unchanged. No bitmap
/// or GPU texture is allocated. Binding changes invalidate the captured program generation.</summary>
public static partial class Sky2NullTextureAdapter
{
    public const string DefaultPixelSourceSha = "890FB3C9182BAE2D52C20BA993471769F8BD8DBFB959997474827ACAFCDAB11A";
    // Source identities still used by the separate directional-light loader.
    // Null sampling compatibility below is derived from actual declarations.
    public const string FiniteMoonAtmosphere5SourceSha = "E0F26D96BC871BA23979AE4DE3996C7638AAD2DFE526CDDD907D0FBBBA296719";
    public const string FiniteMoonAtmosphere11SourceSha = "B0A176C05E2476DAA787BB75028185CDB59CCC7F344AFC3C782F2CDCECE6C4FF";
    public const string FiniteMoonContribution6SourceSha = "0B6E8D02E2CB98706826E7935AC9249429D758FFCE2BDD502821F469F0A3DBF2";

    public static Sky2NullTextureSpirv Adapt(ShaderSpirv pixel,
        TranslatedShaderBindings originalBindings, IReadOnlyList<Sky2NullTextureBinding> explicitNulls)
    {
        ArgumentNullException.ThrowIfNull(pixel); ArgumentNullException.ThrowIfNull(originalBindings);
        ArgumentNullException.ThrowIfNull(explicitNulls);
        if (originalBindings.Interface.EntryPoints.Single().ExecutionModel != 4 || explicitNulls.Count == 0)
            throw new InvalidDataException("Sky null sampling requires a fragment shader and captured null bindings.");
        var bindingOwner = explicitNulls[0].Snapshot;
        foreach (var binding in explicitNulls)
        {
            ArgumentNullException.ThrowIfNull(binding);
            binding.Validate();
            if (!ReferenceEquals(binding.Snapshot.Owner, bindingOwner.Owner) ||
                binding.Snapshot.Revision != bindingOwner.Revision)
                throw new InvalidDataException("Sky2 null bindings must belong to one program binding generation.");
        }
        uint[] slots = explicitNulls.Select(p => p.PixelRegister).Order().ToArray();
        if (slots.Distinct().Count() != slots.Length)
            throw new InvalidDataException("Sky null sampling requires distinct captured texture slots.");
        var packetInterface = PreparePacketInterface(explicitNulls, originalBindings.SourceDefinitions);
        var resources = new HashSet<uint>(); var textures = new HashSet<uint>();
        foreach (uint slot in slots)
        {
            var image = originalBindings.Bindings.Single(b => b.DescriptorKind == "Texture" && b.Source.BindPoint == slot);
            var sampler = originalBindings.Bindings.Single(b => b.DescriptorKind == "Sampler" && b.Source.BindPoint == slot);
            if (image.Source.Dimension != 4 || image.Source.ReturnType != 5 || image.Device.DescriptorSet != 0 || sampler.Device.DescriptorSet != 0)
                throw new InvalidDataException("Null2D source declaration differs from floating Texture2D and its original sampler.");
            textures.Add(image.Device.VariableId); resources.Add(image.Device.VariableId); resources.Add(sampler.Device.VariableId);
        }
        var instructions = Parse(pixel.Bytes); var removed = new HashSet<uint>(resources);
        var imageLoads = new HashSet<uint>(); var sampledImages = new HashSet<uint>();
        var loadedTextures = new HashSet<uint>(); var pairedImageLoads = new HashSet<uint>();
        var sampledImageUses = new HashSet<uint>();
        var zeros = new Dictionary<uint, uint>(); uint next = originalBindings.Interface.IdBound;
        var body = new List<uint[]>(); int replaced = 0, retained = 0;
        foreach (uint[] w in instructions)
        {
            ushort op = (ushort)w[0];
            if (op == 61 && resources.Contains(w[3]))
            {
                removed.Add(w[2]);
                if (textures.Contains(w[3])) { imageLoads.Add(w[2]); loadedTextures.Add(w[3]); }
                continue;
            }
            if (op == 86 && imageLoads.Contains(w[3]))
            {
                if (!removed.Contains(w[4])) throw new InvalidDataException("Source null image must use its exact paired null sampler.");
                pairedImageLoads.Add(w[3]); sampledImages.Add(w[2]); removed.Add(w[2]); continue;
            }
            if (op is 87 or 88 && sampledImages.Contains(w[3]))
            {
                // The result's vec4<float32> type is verified from declarations,
                // independent of whether a stage variable uses that type.
                uint[] vector = instructions.Single(t => (ushort)t[0] == 23 && t[1] == w[1]);
                if (vector[3] != 4 || !instructions.Any(t => (ushort)t[0] == 22 && t[1] == vector[2] && t[2] == 32))
                    throw new InvalidDataException("Sky null sampling requires a float4 sample result.");
                if (!zeros.TryGetValue(w[1], out uint zero)) zeros.Add(w[1], zero = next++);
                sampledImageUses.Add(w[3]); body.Add(Op(83, w[1], w[2], zero)); replaced++; continue;
            }
            body.Add(w); retained++;
        }
        if (!loadedTextures.SetEquals(textures) || !pairedImageLoads.SetEquals(imageLoads) ||
            !sampledImageUses.SetEquals(sampledImages))
            throw new InvalidDataException("Sky null sampling requires direct image/sampler loads with all sampled uses replaced.");
        var final = new List<uint>(); uint[] header = Words(pixel.Bytes).Take(5).ToArray(); header[3] = next; final.AddRange(header);
        bool inserted = false;
        foreach (uint[] w in body)
        {
            ushort op = (ushort)w[0];
            if (op is 5 or 71 && removed.Contains(w[1])) { retained--; continue; }
            if (op == 59 && resources.Contains(w[2])) { retained--; continue; }
            if (op == 54 && !inserted)
            { foreach (var zero in zeros) final.AddRange(Op(46, zero.Key, zero.Value)); inserted = true; }
            final.AddRange(w);
        }
        byte[] bytes = new byte[final.Count * 4];
        for (int i = 0; i < final.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), final[i]);
        var result = SpirvShaderInterfaceReader.Read(bytes);
        if (result.Descriptors.Count != originalBindings.Bindings.Count - 2 * slots.Length ||
            result.StageVariables.Count != originalBindings.Interface.StageVariables.Count ||
            result.Descriptors.Any(d => resources.Contains(d.VariableId)))
            throw new InvalidDataException("Null2D adapter changed the remaining source interface.");
        return new(bytes, result, pixel, slots, explicitNulls, packetInterface,
            originalBindings.SourceDefinitions, replaced, retained);
    }

    private static uint[] Op(ushort code, params uint[] words)
        => new[] { (uint)((words.Length + 1) << 16) | code }.Concat(words).ToArray();
    private static uint[] Words(ReadOnlySpan<byte> bytes)
    { var words = new uint[bytes.Length / 4]; for (int i = 0; i < words.Length; i++) words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(4 * i)..]); return words; }
    private static List<uint[]> Parse(ReadOnlySpan<byte> bytes)
    {
        var words = Words(bytes); var list = new List<uint[]>();
        for (int at = 5; at < words.Length;)
        { int count = (int)(words[at] >> 16); if (count == 0 || count > words.Length - at) throw new InvalidDataException("Truncated Sky2 SPIR-V."); list.Add(words.AsSpan(at, count).ToArray()); at += count; }
        return list;
    }
}


