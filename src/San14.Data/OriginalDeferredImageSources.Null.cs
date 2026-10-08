using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

public enum OriginalDeferredImageStateScope { FreshSourceConstructionNoAssignments, CurrentOrEditedState }

/// <summary>Source-created default HDRI plus fresh Ramp/Hatching shader plugins.
/// This scope excludes subsequent resource setters and current modifier state.
/// It has no RID, pixels or generated image: D3D11 null sampling returns zero.</summary>
public sealed class OriginalDeferredSourceNull2D
{
    private readonly OriginalMainLightConstructionPacket? hdri;
    private readonly OriginalDirectionalLightConstructionPacket? directional;
    public uint PixelRegister { get; }
    public string SourceEvidence { get; }
    /// <summary>Legacy HDRI construction. A Directional receipt cannot be
    /// converted into an HDRI packet through this compatibility property.</summary>
    public OriginalMainLightConstructionPacket LightConstruction => hdri ??
        throw new InvalidDataException("This Hatching null receipt belongs to a Directional construction, not HDRI.");
    public OriginalDirectionalLightConstructionPacket? DirectionalConstruction => directional;
    public string LightSourceProvenance => hdri?.SourceProvenance ??
        OriginalMapSourceCreatedDirectionalLightingSource.DescribeConstruction(directional!);
    private OriginalDeferredSourceNull2D(OriginalMainLightConstructionPacket light, uint register)
    {
        hdri = light; PixelRegister = register;
        SourceEvidence = light.SourceProvenance + (register == 6
            ? ";LightData1008:hardcoded-source-zero;HatchingCtor9FBC10:descriptor60-zero;F103EAE0D8:null-skip;HatchingPrepare9FBFB9:copy-null"
            : ";IndexedRampTexture556972D8:source-zero;LightDataFF8:null;RampCtor9F82E0:descriptor40-zero;F103EAE09C:null-skip;RampPrepare9F8D90:copy-null")
            + ";fresh-shader-interface;no-provider-assignments";
    }
    private OriginalDeferredSourceNull2D(OriginalDirectionalLightConstructionPacket light)
    {
        directional=light;PixelRegister=6;
        SourceEvidence=LightSourceProvenance+
            ";Directional-CBFAB0:source-resource-slot5/LightData1008-declared0/effective0;"+
            "HatchingCtor9FBC10:descriptor60-zero;F103EAE0D8:null-skip;HatchingPrepare9FBFB9:copy-null;"+
            "fresh-shader-interface;no-provider-assignments;logical-Hatching6-not-selected-register";
    }

    public static IReadOnlyList<OriginalDeferredSourceNull2D> FromFreshConstruction(
        OriginalMainLightConstructionPacket light, OriginalDeferredImageStateScope explicitScope)
    {
        ArgumentNullException.ThrowIfNull(light);
        if (explicitScope != OriginalDeferredImageStateScope.FreshSourceConstructionNoAssignments)
            throw new InvalidDataException("Current deferred images require their actual applied source owners; fresh source-null proof does not cover them.");
        ValidateLight(light);
        return Array.AsReadOnly(new[] { new OriginalDeferredSourceNull2D(light, 6), new OriginalDeferredSourceNull2D(light, 10) });
    }

    /// <summary>The source declaration and complete owned constructor must
    /// independently prove slot5 null. Only Hatching is admitted on this route;
    /// it supplies no copied HDRI Ramp, IES or local-ambient authority.</summary>
    public static IReadOnlyList<OriginalDeferredSourceNull2D> FromFreshDirectionalConstruction(
        OriginalDirectionalLightConstructionPacket light, OriginalDeferredImageStateScope explicitScope)
    {
        ArgumentNullException.ThrowIfNull(light);
        if(explicitScope!=OriginalDeferredImageStateScope.FreshSourceConstructionNoAssignments)
            throw new InvalidDataException("Current or edited Directional Hatching requires its actual shader assignment owner.");
        ValidateDirectional(light);
        return Array.AsReadOnly(new[]{new OriginalDeferredSourceNull2D(light)});
    }
    public void Validate()
    { if(hdri is not null)ValidateLight(hdri);else ValidateDirectional(directional!); }
    internal void ValidateForLegacyPs1()
    {
        if(hdri is null)throw new InvalidDataException("Legacy PS1 specialization requires its original HDRI source receipt.");
        ValidateLight(hdri);
    }
    public void ValidateForSelection(OriginalDeferredF103SelectedSource selection)
    {
        Validate();
        if(directional is null)return;
        var feature=selection.OwnedFeatureSource;
        if(selection.Combination.Index!=510||selection.PixelProgramIndex!=383||feature is null||
            selection.PixelProgramSha256!=OriginalDeferredSelected383Constants.PixelProgramSha256||
            feature.LightingSourceIdentity.MainLightResourceUri!=directional.SourceUri||
            feature.LightingSourceIdentity.MainLightSha256!=directional.SourceSha256||
            feature.LightingSourceIdentity.MainLightObjectId!=directional.SourceObjectId||
            feature.LightingSourceIdentity.CreatedLightDataSha256!=directional.PreparedSha256||
            feature.LightingSourceIdentity.ConstructedOwnerSha256!=Convert.ToHexString(SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(LightSourceProvenance)))||
            BinaryPrimitives.ReadUInt64LittleEndian(feature.EffectiveLocalLightData.Span.Slice(0x1008,8))!=0)
            throw new InvalidDataException("Directional Hatching null receipt requires this actual PS383 outer source constructor and effective null1008 generation.");
    }
    private static void ValidateDirectional(OriginalDirectionalLightConstructionPacket light)
    {
        ArgumentNullException.ThrowIfNull(light);
        var slots=light.ResourceSlots.Where(slot=>slot.LightDataOffset==0x1008).ToArray();
        if(light.Bytes.Length!=OriginalDirectionalLightConstruction.LightDataBytes||slots.Length!=1||
            slots[0].Slot!=5||slots[0].DeclaredObjectKey!=0||slots[0].EffectiveDataHandle!=0||
            slots[0].RequiredInterfaceClassId!=0xB672FAA2||
            BinaryPrimitives.ReadUInt64LittleEndian(light.ConstructorBytes.Span.Slice(0x1008,8))!=0||
            BinaryPrimitives.ReadUInt64LittleEndian(light.Bytes.Span.Slice(0x1008,8))!=0)
            throw new InvalidDataException("Directional Hatching requires independently declared0 and effective null source slot5 at1008.");
    }
    private static void ValidateLight(OriginalMainLightConstructionPacket light)
    {
        var source = OriginalMainLightBindingSource.ReadGameDefault(new(
            San14.Data.Formats.DokReader.Read(light.Source.Resource.Source.Bytes), light.Source.Resource.Source));
        if (source.Declaration.ObjectId != light.Source.Declaration.ObjectId || light.CurrentModifiersApplied ||
            light.Bytes.Length != OriginalMainLightConstruction.LightDataBytes ||
            !Convert.ToHexString(SHA256.HashData(source.Resource.Source.Bytes)).Equals(
                OriginalMainLightBindingSource.DecodedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Deferred null images require the complete original Game HDRI construction packet.");
        var ramp = source.Declaration.Properties.Where(p => p.NameHash == 0x556972D8).ToArray();
        if (ramp.Length != 1 || ramp[0].TypeCode != 5 || ramp[0].ElementCount != 1 || ramp[0].RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(ramp[0].RawValue) != 0 ||
            BinaryPrimitives.ReadUInt64LittleEndian(light.Bytes.Span.Slice(0xFF8, 8)) != 0 ||
            BinaryPrimitives.ReadUInt64LittleEndian(light.Bytes.Span.Slice(0x1008, 8)) != 0)
            throw new InvalidDataException("Deferred Ramp/Hatching pointers differ from the proven source-created null state.");
    }
}

public sealed record OriginalDeferredNullImageSpirv(byte[] Bytes, string Sha256,
    string OriginalTranslationSha256, IReadOnlyList<uint> NullPixelRegisters,
    IReadOnlyList<string> SourceEvidence, int ReplacedSamples, int RetainedInstructions);

/// <summary>Bounded original9D8E811F PS1 vkd3d2.1 null-image specialization.
/// Replaces only the proven one Hatching and seven Ramp samples with float4(+0),
/// removing their exact image/sampler descriptors. No texture is allocated.</summary>
public static class OriginalDeferredNullImageAdapter
{
    public const string PixelTranslationSha256 = "0BA3DC56B68B8F7D43D7D0C743C3FB2231E48AA1C1B36A3E2F8BB911E17CFD8F";

    public static OriginalTranslatedShaderBindings ReadDeviceBindings(OriginalTranslatedShaderBindings original,
        OriginalDeferredNullImageSpirv specialized)
    {
        ArgumentNullException.ThrowIfNull(original); ArgumentNullException.ThrowIfNull(specialized);
        if (original.Interface.Sha256 != PixelTranslationSha256 ||
            specialized.OriginalTranslationSha256 != PixelTranslationSha256 ||
            Convert.ToHexString(SHA256.HashData(specialized.Bytes)) != specialized.Sha256 ||
            specialized.NullPixelRegisters.Count == 0 || specialized.NullPixelRegisters.Any(s => s is not (6 or 10)) ||
            specialized.NullPixelRegisters.Distinct().Count() != specialized.NullPixelRegisters.Count)
            throw new InvalidDataException("Deferred device declarations require the verified null specialization identity.");
        var slots = specialized.NullPixelRegisters.ToHashSet();
        var subset = original.OriginalDefinitions with { Bindings = Array.AsReadOnly(original.OriginalDefinitions.Bindings
            .Where(b => b.InputType is not (2 or 3) || !slots.Contains(b.BindPoint)).ToArray()) };
        var device = OriginalTranslatedShaderBindingReader.Read(subset, specialized.Bytes);
        return device with { OriginalDefinitions = original.OriginalDefinitions };
    }

    public static OriginalDeferredNullImageSpirv Adapt(OriginalShaderSpirv pixel,
        OriginalTranslatedShaderBindings sourceBindings, IReadOnlyList<OriginalDeferredSourceNull2D> explicitNulls)
    {
        ArgumentNullException.ThrowIfNull(pixel); ArgumentNullException.ThrowIfNull(sourceBindings);
        ArgumentNullException.ThrowIfNull(explicitNulls);
        if (pixel.SourceProgramSha256 != OriginalDeferredImageSources.PixelSourceSha256 ||
            pixel.SpirvSha256 != PixelTranslationSha256 || Convert.ToHexString(SHA256.HashData(pixel.Bytes)) != PixelTranslationSha256 ||
            sourceBindings.Interface.Sha256 != PixelTranslationSha256 || sourceBindings.Bindings.Count != 42 ||
            sourceBindings.Interface.EntryPoints.Single().ExecutionModel != 4 || explicitNulls.Count == 0)
            throw new InvalidDataException("Deferred null specialization requires the complete exact original PS1 translation and proven fresh source nulls.");
        foreach (var proof in explicitNulls) { ArgumentNullException.ThrowIfNull(proof); proof.ValidateForLegacyPs1(); }
        uint[] slots = explicitNulls.Select(p => p.PixelRegister).Order().ToArray();
        if (slots.Distinct().Count() != slots.Length || slots.Any(s => s is not (6 or 10)))
            throw new InvalidDataException("Deferred null specialization supports only source-created Hatching t6 and Ramp t10.");
        var resources = new HashSet<uint>(); var textures = new Dictionary<uint, uint>();
        foreach (uint slot in slots)
        {
            string name = slot == 6 ? "sHatchingShadow" : "sRamp";
            var image = sourceBindings.Bindings.Single(b => b.DescriptorKind == "Texture" && b.Source.BindPoint == slot);
            var sampler = sourceBindings.Bindings.Single(b => b.DescriptorKind == "Sampler" && b.Source.BindPoint == slot);
            if (image.Source.Name.Text != name || sampler.Source.Name.Text != "__smp" + name ||
                image.Source.Dimension != 4 || image.Source.ReturnType != 5 || image.Source.BindCount != 1 ||
                image.Device.DescriptorSet != 0 || sampler.Device.DescriptorSet != 0)
                throw new InvalidDataException("Deferred null image differs from its original float2D paired sampler declaration.");
            textures.Add(image.Device.VariableId, slot); resources.Add(image.Device.VariableId); resources.Add(sampler.Device.VariableId);
        }
        var instructions = Parse(pixel.Bytes); var removed = new HashSet<uint>(resources);
        var imageLoads = new Dictionary<uint, uint>(); var sampled = new Dictionary<uint, uint>();
        var counts = slots.ToDictionary(s => s, _ => 0); var zeros = new Dictionary<uint, uint>();
        uint next = sourceBindings.Interface.IdBound; var body = new List<uint[]>(); int retained = 0;
        foreach (uint[] w in instructions)
        {
            ushort op = (ushort)w[0];
            if (op == 61 && resources.Contains(w[3]))
            { removed.Add(w[2]); if (textures.TryGetValue(w[3], out uint slot)) imageLoads.Add(w[2], slot); continue; }
            if (op == 86 && imageLoads.TryGetValue(w[3], out uint imageSlot))
            {
                if (!removed.Contains(w[4])) throw new InvalidDataException("Deferred null image uses a different sampler.");
                sampled.Add(w[2], imageSlot); removed.Add(w[2]); continue;
            }
            if (op is 87 or 88 && sampled.TryGetValue(w[3], out uint sampledSlot))
            {
                var vector = instructions.Single(t => (ushort)t[0] == 23 && t[1] == w[1]);
                if (vector[3] != 4 || !instructions.Any(t => (ushort)t[0] == 22 && t[1] == vector[2] && t[2] == 32))
                    throw new InvalidDataException("Deferred null sample differs from source float4 output.");
                if (!zeros.TryGetValue(w[1], out uint zero)) zeros.Add(w[1], zero = next++);
                body.Add(Op(83, w[1], w[2], zero)); counts[sampledSlot]++; continue;
            }
            body.Add(w); retained++;
        }
        if (counts.Any(p => p.Value != (p.Key == 6 ? 1 : 7)))
            throw new InvalidDataException("Deferred null sample schedule differs from the pinned original PS1.");
        var final = new List<uint>(); uint[] header = Words(pixel.Bytes).Take(5).ToArray(); header[3] = next; final.AddRange(header);
        bool inserted = false;
        foreach (uint[] w in body)
        {
            ushort op = (ushort)w[0];
            if (op is 5 or 71 && removed.Contains(w[1])) continue;
            if (op == 59 && resources.Contains(w[2])) continue;
            if (op == 54 && !inserted)
            { foreach (var zero in zeros) final.AddRange(Op(46, zero.Key, zero.Value)); inserted = true; }
            final.AddRange(w);
        }
        byte[] bytes = new byte[final.Count * 4];
        for (int i = 0; i < final.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), final[i]);
        var result = San14.Data.Formats.SpirvShaderInterfaceReader.Read(bytes);
        if (result.Descriptors.Count != 42 - 2 * slots.Length ||
            result.StageVariables.Count != sourceBindings.Interface.StageVariables.Count ||
            result.Descriptors.Any(d => resources.Contains(d.VariableId)))
            throw new InvalidDataException("Deferred null adapter changed the remaining source interface.");
        return new(bytes, result.Sha256, pixel.SpirvSha256, Array.AsReadOnly(slots),
            Array.AsReadOnly(explicitNulls.Select(p => p.SourceEvidence).ToArray()), counts.Values.Sum(), retained);
    }

    private static uint[] Op(ushort code, params uint[] words)
        => new[] { (uint)((words.Length + 1) << 16) | code }.Concat(words).ToArray();
    private static uint[] Words(ReadOnlySpan<byte> bytes)
    { var words = new uint[bytes.Length / 4]; for (int i = 0; i < words.Length; i++) words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(4 * i)..]); return words; }
    private static List<uint[]> Parse(ReadOnlySpan<byte> bytes)
    {
        var words = Words(bytes); var list = new List<uint[]>();
        for (int at = 5; at < words.Length;)
        { int count = (int)(words[at] >> 16); if (count == 0 || count > words.Length - at) throw new InvalidDataException("Truncated deferred SPIR-V."); list.Add(words.AsSpan(at, count).ToArray()); at += count; }
        return list;
    }
}
