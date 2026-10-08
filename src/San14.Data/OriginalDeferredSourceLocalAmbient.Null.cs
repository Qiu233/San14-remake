using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalDeferredSourceLocalAmbientNullSpirv(byte[] Bytes, string Sha256,
    string OriginalTranslationSha256, OriginalDeferredSourceLocalAmbient Source,
    OriginalDeferredNullImageSpirv? Preceding2D,
    int ReplacedCubeSamples, int ReplacedDataLoads, int ReplacedIndexLoads,
    int RetainedInstructions);

/// <summary>Exact PS1 specialization for genuinely null LAS SRVs. D3D11 null
/// reads return zero. Replaces only t22 samples/t23 float loads/t24 uint loads,
/// removes those three SRVs and keeps all42 original RDEF rows. Source s7 is
/// retained: the nonnull global ambient t7 uses the same sampler.
/// Optional original t6/t10 null proofs are applied by their existing adapter.
/// No fabricated buffer, cube, sampler or zero device handle is produced.</summary>
public static class OriginalDeferredSourceLocalAmbientNullAdapter
{
    public static OriginalDeferredSourceLocalAmbientNullSpirv Adapt(OriginalShaderSpirv original,
        OriginalTranslatedShaderBindings sourceBindings, OriginalDeferredSourceLocalAmbient explicitFreshSource,
        IReadOnlyList<OriginalDeferredSourceNull2D>? explicitFresh2DNulls = null)
    {
        ArgumentNullException.ThrowIfNull(original); ArgumentNullException.ThrowIfNull(sourceBindings);
        ArgumentNullException.ThrowIfNull(explicitFreshSource); explicitFreshSource.Validate();
        if (original.SourceProgramSha256 != OriginalDeferredBufferSources.PixelSourceSha256 ||
            original.SpirvSha256 != OriginalDeferredNullImageAdapter.PixelTranslationSha256 ||
            Convert.ToHexString(SHA256.HashData(original.Bytes)) != OriginalDeferredNullImageAdapter.PixelTranslationSha256 ||
            sourceBindings.Interface.Sha256 != OriginalDeferredNullImageAdapter.PixelTranslationSha256 ||
            sourceBindings.Bindings.Count != 42 || sourceBindings.Interface.EntryPoints.Single().ExecutionModel != 4)
            throw Error("requires the complete pinned original PS1 translation");
        OriginalDeferredNullImageSpirv? preceding = explicitFresh2DNulls is { Count: > 0 }
            ? OriginalDeferredNullImageAdapter.Adapt(original, sourceBindings, explicitFresh2DNulls) : null;
        byte[] input = preceding?.Bytes ?? original.Bytes;
        var bindings = preceding is null ? sourceBindings : OriginalDeferredNullImageAdapter.ReadDeviceBindings(sourceBindings, preceding);
        var resources = new HashSet<uint>(); var imageVariables = new Dictionary<uint, uint>();
        foreach (uint slot in new uint[] { 22, 23, 24 })
        {
            var resource = bindings.Bindings.Single(b => b.Source.InputType == 2 && b.Source.BindPoint == slot);
            if (resource.DescriptorKind != (slot == 22 ? "Texture" : "TextureBuffer") || resource.Device.DescriptorSet != 0)
                throw Error("source LAS descriptor differs from its original image or typed-buffer kind");
            imageVariables.Add(resource.Device.VariableId, slot); resources.Add(resource.Device.VariableId);
        }
        var sampler = bindings.Bindings.Single(b => b.DescriptorKind == "Sampler" && b.Source.BindPoint == 7);
        if (sampler.Source.Name.Text != "__smpsAmbSpc" || sampler.Device.DescriptorSet != 0)
            throw Error("local cube-array does not use the original shared ambient s7");
        var instructions = Parse(input); var removed = new HashSet<uint>(resources);
        var loadedImages = new Dictionary<uint, uint>(); var sampledImages = new Dictionary<uint, uint>();
        var samplerLoads = new HashSet<uint>();
        var counts = new Dictionary<uint, int> { [22] = 0, [23] = 0, [24] = 0 };
        var zeros = new Dictionary<uint, uint>(); uint next = bindings.Interface.IdBound;
        var body = new List<uint[]>(); int retained = 0;
        foreach (uint[] w in instructions)
        {
            ushort op = (ushort)w[0];
            if (op == 61 && w.Length == 4 && w[3] == sampler.Device.VariableId) samplerLoads.Add(w[2]);
            if (op == 61 && w.Length == 4 && resources.Contains(w[3]))
            {
                removed.Add(w[2]);
                if (imageVariables.TryGetValue(w[3], out uint slot)) loadedImages.Add(w[2], slot);
                continue;
            }
            if (op == 86 && loadedImages.TryGetValue(w[3], out uint imageSlot))
            {
                if (imageSlot != 22 || !samplerLoads.Contains(w[4])) throw Error("LAS sample uses a different source sampler");
                sampledImages.Add(w[2], imageSlot); removed.Add(w[2]); continue;
            }
            uint sourceSlot = 0;
            bool replacement = op == 88 && sampledImages.TryGetValue(w[3], out sourceSlot) ||
                op == 95 && loadedImages.TryGetValue(w[3], out sourceSlot);
            if (replacement)
            {
                if ((op == 88) != (sourceSlot == 22)) throw Error("LAS consumer operation differs from its original sample/load schedule");
                var vector = instructions.Single(t => (ushort)t[0] == 23 && t[1] == w[1]);
                bool component = sourceSlot == 24
                    ? instructions.Any(t => (ushort)t[0] == 21 && t[1] == vector[2] && t[2] == 32 && t[3] == 0)
                    : instructions.Any(t => (ushort)t[0] == 22 && t[1] == vector[2] && t[2] == 32);
                if (vector[3] != 4 || !component) throw Error("LAS read differs from original float4/uint4 result type");
                if (!zeros.TryGetValue(w[1], out uint zero)) zeros.Add(w[1], zero = next++);
                body.Add(Op(83, w[1], w[2], zero)); counts[sourceSlot]++; continue;
            }
            body.Add(w); retained++;
        }
        if (counts[22] != 3 || counts[23] != 12 || counts[24] != 12)
            throw Error("LAS read schedule differs from three cube samples and twelve loads per buffer");
        var final = new List<uint>(); uint[] header = Words(input).Take(5).ToArray(); header[3] = next; final.AddRange(header);
        bool inserted = false;
        foreach (uint[] w in body)
        {
            ushort op = (ushort)w[0];
            if (op is 5 or 71 && removed.Contains(w[1]) || op == 59 && resources.Contains(w[2])) continue;
            if (op == 54 && !inserted)
            {
                foreach (var zero in zeros) final.AddRange(Op(46, zero.Key, zero.Value));
                inserted = true;
            }
            final.AddRange(w);
        }
        byte[] bytes = new byte[final.Count * 4];
        for (int i = 0; i < final.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), final[i]);
        var result = SpirvShaderInterfaceReader.Read(bytes);
        if (result.Descriptors.Count != bindings.Bindings.Count - 3 ||
            result.StageVariables.Count != bindings.Interface.StageVariables.Count ||
            result.Descriptors.Any(d => resources.Contains(d.VariableId)))
            throw Error("LAS null specialization changed the remaining original interface");
        return new(bytes, result.Sha256, original.SpirvSha256, explicitFreshSource, preceding,
            counts[22], counts[23], counts[24], retained);
    }

    public static OriginalTranslatedShaderBindings ReadDeviceBindings(OriginalTranslatedShaderBindings original,
        OriginalDeferredSourceLocalAmbientNullSpirv specialized)
    {
        ArgumentNullException.ThrowIfNull(original); ArgumentNullException.ThrowIfNull(specialized);
        specialized.Source.Validate();
        if (original.Interface.Sha256 != OriginalDeferredNullImageAdapter.PixelTranslationSha256 ||
            specialized.OriginalTranslationSha256 != OriginalDeferredNullImageAdapter.PixelTranslationSha256 ||
            Convert.ToHexString(SHA256.HashData(specialized.Bytes)) != specialized.Sha256 ||
            specialized.ReplacedCubeSamples != 3 || specialized.ReplacedDataLoads != 12 || specialized.ReplacedIndexLoads != 12)
            throw Error("device declarations require the exact LAS null specialization identity");
        var preceding = specialized.Preceding2D;
        if (preceding is not null) _ = OriginalDeferredNullImageAdapter.ReadDeviceBindings(original, preceding);
        var pairs = preceding?.NullPixelRegisters.ToHashSet() ?? [];
        var subset = original.OriginalDefinitions with { Bindings = Array.AsReadOnly(original.OriginalDefinitions.Bindings
            .Where(b => !(b.InputType == 2 && b.BindPoint is 22 or 23 or 24) &&
                !(b.InputType is 2 or 3 && pairs.Contains(b.BindPoint))).ToArray()) };
        var device = OriginalTranslatedShaderBindingReader.Read(subset, specialized.Bytes);
        return device with { OriginalDefinitions = original.OriginalDefinitions };
    }

    private static InvalidDataException Error(string message) => new("Deferred source LocalAmbient null: " + message + ".");
    private static uint[] Op(ushort code, params uint[] words)
        => new[] { (uint)((words.Length + 1) << 16) | code }.Concat(words).ToArray();
    private static uint[] Words(ReadOnlySpan<byte> bytes)
    {
        var words = new uint[bytes.Length / 4];
        for (int i = 0; i < words.Length; i++) words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(4 * i)..]);
        return words;
    }
    private static List<uint[]> Parse(ReadOnlySpan<byte> bytes)
    {
        var words = Words(bytes); var list = new List<uint[]>();
        for (int at = 5; at < words.Length;)
        {
            int count = (int)(words[at] >> 16);
            if (count == 0 || count > words.Length - at) throw Error("truncated SPIR-V");
            list.Add(words.AsSpan(at, count).ToArray()); at += count;
        }
        return list;
    }
}
