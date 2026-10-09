using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Specializes only the one original PS95 Caustics sample proved to
/// read a fresh source NULL image. The original source and owner survive.</summary>
public sealed class DeferredEarlyCausticsNullSpirv
{
    private readonly byte[] bytes;
    internal DeferredEarlyCausticsNullSpirv(byte[] bytes,
        DeferredEarlyF103SelectedSource selected, DeferredEarlyCausticsSourceNull proof)
    {
        this.bytes = bytes.ToArray(); Selection = selected; SourceNull = proof;
        Sha256 = Convert.ToHexString(SHA256.HashData(bytes));
    }
    public byte[] Bytes => bytes.ToArray();
    public string Sha256 { get; }
    public DeferredEarlyF103SelectedSource Selection { get; }
    public DeferredEarlyCausticsSourceNull SourceNull { get; }
    public int ReplacedSamples => 1;
}

public static class DeferredEarlyCausticsNullAdapter
{
    public const string PixelSha256 = "19103754FF90ADC50E3CC7269C1AA93013D0FC3EA8341D3EE7BB2396D9F3030C";
    public const string SpirvSha256 = "534FAFD41CEF7FBB3E281B93530AF40CCC41B0FBA945A3DFCC9F7AB0EEEC65F7";
    private const string CompilerSha256 = "37885422AD210365C8FFCDA9F40A7F321842139698C6E1E6134DB20BA5D65780";

    public static DeferredEarlyCausticsNullSpirv Adapt(ShaderSpirv original,
        TranslatedShaderBindings bindings, DeferredEarlyF103SelectedSource selected,
        DeferredEarlyCausticsSourceNull proof)
    {
        ArgumentNullException.ThrowIfNull(original); ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(selected); ArgumentNullException.ThrowIfNull(proof);
        selected.Validate(); proof.Validate(selected);
        if (selected.SourcePlan.Node.Index != 78 || selected.Combination.Index != 126 || selected.PixelProgramIndex != 95 ||
            selected.PixelProgramSha256 != PixelSha256 || original.SourceProgramSha256 != PixelSha256 ||
            original.CompilerLibrarySha256 != CompilerSha256 || original.SpirvSha256 != SpirvSha256 ||
            Hash(original.Bytes) != SpirvSha256 || bindings.Interface.Sha256 != SpirvSha256 ||
            bindings.Interface.EntryPoints.Single().ExecutionModel != 4)
            throw Error("requires the exact source node78/row126/PS95 translation");

        var texture = bindings.Bindings.Single(binding => binding.Source.Name.Text == "sCausticsSampler");
        var sampler = bindings.Bindings.Single(binding => binding.Source.Name.Text == "__smpsCausticsSampler");
        if (texture.DescriptorKind != "Texture" || texture.Source.InputType != 2 || texture.Source.BindPoint != 1 ||
            texture.Source.Dimension != 4 || texture.Source.ReturnType != 5 || texture.Source.BindCount != 1 ||
            sampler.DescriptorKind != "Sampler" || sampler.Source.InputType != 3 || sampler.Source.BindPoint != 1 ||
            sampler.Source.BindCount != 1 || texture.Device.DescriptorSet != 0 || sampler.Device.DescriptorSet != 0)
            throw Error("Caustics role is no longer the pinned float4 t1/s1 pair");

        uint textureId = texture.Device.VariableId, samplerId = sampler.Device.VariableId;
        var instructions = Parse(original.Bytes);
        var removed = new HashSet<uint> { textureId, samplerId };
        var imageLoads = new HashSet<uint>(); var samplerLoads = new HashSet<uint>();
        var sampled = new HashSet<uint>(); var body = new List<uint[]>();
        uint zeroType = 0, zeroId = bindings.Interface.IdBound; int samples = 0;
        foreach (var words in instructions)
        {
            ushort op = (ushort)words[0];
            if (op == 61 && words.Length == 4 && (words[3] == textureId || words[3] == samplerId))
            {
                (words[3] == textureId ? imageLoads : samplerLoads).Add(words[2]);
                removed.Add(words[2]); continue;
            }
            if (op == 86 && words.Length == 5 && imageLoads.Contains(words[3]))
            {
                if (!samplerLoads.Contains(words[4])) throw Error("Caustics sampled a different sampler");
                sampled.Add(words[2]); removed.Add(words[2]); continue;
            }
            if (op is 87 or 88 && words.Length >= 5 && sampled.Contains(words[3]))
            {
                // Original D3D NULL Sample returns float4 zero. No image
                // dimensions, fetch or comparison operation exists here.
                if (++samples != 1) throw Error("Caustics sample schedule changed");
                zeroType = words[1];
                var vector = instructions.Single(word => (ushort)word[0] == 23 && word[1] == zeroType);
                if (vector[3] != 4 || !instructions.Any(word => (ushort)word[0] == 22 && word[1] == vector[2] && word[2] == 32))
                    throw Error("Caustics sample result is not the original float4");
                body.Add(Op(83, words[1], words[2], zeroId)); continue;
            }
            body.Add(words);
        }
        if (samples != 1 || imageLoads.Count != 1 || samplerLoads.Count != 1 || sampled.Count != 1)
            throw Error("Caustics resource read schedule changed");

        var header = Words(original.Bytes).Take(5).ToArray(); header[3] = checked(zeroId + 1);
        var encoded = new List<uint>(header); bool inserted = false;
        foreach (var words in body)
        {
            ushort op = (ushort)words[0];
            if (op is 5 or 71 && removed.Contains(words[1]) || op == 59 && (words[2] == textureId || words[2] == samplerId)) continue;
            if (op == 54 && !inserted) { encoded.AddRange(Op(46, zeroType, zeroId)); inserted = true; }
            encoded.AddRange(words);
        }
        byte[] bytes = new byte[checked(encoded.Count * 4)];
        for (int index = 0; index < encoded.Count; index++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), encoded[index]);
        var result = SpirvShaderInterfaceReader.Read(bytes);
        if (!inserted || result.Descriptors.Count != bindings.Bindings.Count - 2 ||
            result.Descriptors.Any(descriptor => descriptor.VariableId == textureId || descriptor.VariableId == samplerId) ||
            result.StageVariables.Count != bindings.Interface.StageVariables.Count)
            throw Error("specialization changed a surviving original interface");
        return new(bytes, selected, proof);
    }

    public static TranslatedShaderBindings ReadDeviceBindings(TranslatedShaderBindings original,
        DeferredEarlyCausticsNullSpirv specialized)
    {
        ArgumentNullException.ThrowIfNull(original); ArgumentNullException.ThrowIfNull(specialized);
        specialized.SourceNull.Validate(specialized.Selection);
        if (original.Interface.Sha256 != SpirvSha256 || Hash(specialized.Bytes) != specialized.Sha256)
            throw Error("specialized translation identity changed");
        var subset = original.SourceDefinitions with { Bindings = Array.AsReadOnly(original.SourceDefinitions.Bindings.Where(binding =>
            !(binding.InputType == 2 && binding.BindPoint == 1) && !(binding.InputType == 3 && binding.BindPoint == 1)).ToArray()) };
        return TranslatedShaderBindingReader.Read(subset, specialized.Bytes) with { SourceDefinitions = original.SourceDefinitions };
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static InvalidDataException Error(string message) => new("Early source Caustics NULL: " + message + ".");
    private static uint[] Op(ushort code, params uint[] words) => new[] { (uint)((words.Length + 1) << 16) | code }.Concat(words).ToArray();
    private static uint[] Words(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 20 || bytes.Length % 4 != 0) throw Error("truncated SPIR-V header");
        var words = new uint[bytes.Length / 4];
        for (int index = 0; index < words.Length; index++) words[index] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(index * 4)..]);
        return words;
    }
    private static List<uint[]> Parse(ReadOnlySpan<byte> bytes)
    {
        var words = Words(bytes); var result = new List<uint[]>();
        for (int index = 5; index < words.Length;)
        {
            int length = (int)(words[index] >> 16);
            if (length == 0 || length > words.Length - index) throw Error("truncated SPIR-V instruction");
            result.Add(words.AsSpan(index, length).ToArray()); index += length;
        }
        return result;
    }
}
