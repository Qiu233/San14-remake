using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual query results and context fields at the F103 preparation
/// boundary. These belong to one owned accepted frame; a source Camera or its
/// constructor does not establish later View resource registrations.</summary>
public sealed record OriginalDeferredF103CurrentVariantInputs(
    long FrameNumber, uint ContextFlags3F0, uint NativeColorMode,
    uint CommonViewFlags8, bool QueryD5BeA0,
    int ReflectionQueryD57E20, bool SelectedCamera1808Present,
    bool CommonViewResource788Registered, bool CommonViewResource78CRegistered,
    bool CommonViewResource8B8Registered, string CurrentProducerProvenance);

public sealed record OriginalDeferredF103VariantWrite(int WordIndex, uint? RequestedValue,
    uint PreviousValue, uint NextValue, bool? CapabilityAccepted, string NativeOrigin);

/// <summary>The original full48/96-byte selection inputs and exact selected
/// original programs. It supplies no images, descriptors or null specialization.
/// Its frame identity is the actual serial scene attempt, not a predicted count.</summary>
public sealed class OriginalDeferredF103SelectedSource
{
    private readonly byte[] featureBytes, variantBytes;
    internal OriginalDeferredF103SelectedSource(LoadedOriginalDeferredTexturePlan plan,
        OriginalMapCameraConstructionPacket camera, OriginalDeferredF103CurrentVariantInputs current,
        byte[] feature, byte[] variant, G1ShaderFeatureSelection featureMatch,
        G1ShaderVariantRecord variantRow, G2ShaderCombination combination,
        IReadOnlyList<OriginalDeferredF103VariantWrite> writes,
        OriginalMapSourceCreatedF103FeaturePacket? ownedFeature = null)
    {
        SourcePlan = plan; Camera = camera.CopyAcceptedSnapshot(); Current = current; featureBytes = feature;
        variantBytes = variant; FeatureMatch = featureMatch; BindingVariant = variantRow;
        Combination = combination; Writes = writes; OwnedFeatureSource = ownedFeature;
        var pixel = combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null);
        PixelProgramIndex = pixel.Entry!.ProgramIndex;
        var declarations=OriginalMapSourceCreatedShaderSelection.ReadLoadedProgram(plan.Shader,combination.Index,PixelProgramIndex);
        PixelProgramSha256 = declarations.PixelSha256;
        PixelDefinitions = declarations.PixelDefinitions;
        CanonicalPixelDefinitions = declarations.CanonicalPixelDefinitions;
        ResourceBindings = declarations.ResourceBindings;
    }
    public LoadedOriginalDeferredTexturePlan SourcePlan { get; }
    public OriginalMapCameraConstructionPacket Camera { get; }
    public OriginalDeferredF103CurrentVariantInputs Current { get; }
    public ReadOnlyMemory<byte> CurrentFeature48 => featureBytes;
    public ReadOnlyMemory<byte> RequestedVariant96 => variantBytes;
    public G1ShaderFeatureSelection FeatureMatch { get; }
    public G1ShaderVariantRecord BindingVariant { get; }
    public G2ShaderCombination Combination { get; }
    public int PixelProgramIndex { get; }
    public string PixelProgramSha256 { get; }
    public DxbcResourceDefinitions PixelDefinitions { get; }
    internal DxbcResourceDefinitions CanonicalPixelDefinitions { get; }
    public G1ShaderResourceBindings ResourceBindings { get; }
    public IReadOnlyList<OriginalDeferredF103VariantWrite> Writes { get; }
    public OriginalMapSourceCreatedF103FeaturePacket? OwnedFeatureSource { get; }
    public bool HasVolumetricFogImage => PixelDefinitions.Bindings.Any(b => b.Name.Text == "sVolumetricFogMap");

    public void Validate()
    {
        if (OwnedFeatureSource is { } feature &&
            (feature.FrameNumber != Current.FrameNumber || !feature.CurrentFeature48.Span.SequenceEqual(featureBytes) ||
                !feature.MatchesAcceptedCameraPrefix(Camera.Prefix.Span) ||
                feature.CurrentView.CurrentProducerProvenance != Current.CurrentProducerProvenance))
            throw new InvalidDataException("Selected original feature differs from its owned current Camera/light/View producer.");
    }

    /// <summary>Carry the actual same five source target identities into the
    /// selected original reflection/register mapping. Resource allocation and
    /// produced contents remain with their existing graph owners.</summary>
    public LoadedOriginalDeferredTexturePlan ReadSelectedTexturePlan()
    {
        Validate();
        var declarations = PixelDefinitions;
        var resources = ResourceBindings;
        var inputs = new List<OriginalDeferredTextureInput>();
        foreach (var original in SourcePlan.Inputs)
        {
            var mappings = resources.Entries.Where(r => r.PrimaryEncoded == original.EncodedParameter).ToArray();
            if (mappings.Length != 1 || mappings[0].SlotIndex < 0)
                throw new InvalidDataException("Selected original shader lacks an exact named G-buffer mapping.");
            var rows = declarations.Bindings.Where(b => b.InputType == 2 && b.Name.Text == mappings[0].Name).ToArray();
            if (rows.Length != 1 || rows[0].BindPoint != (uint)mappings[0].SlotIndex ||
                rows[0].Dimension != original.PixelBinding.Dimension || rows[0].ReturnType != 5 || rows[0].BindCount != 1)
                throw new InvalidDataException("Selected original G-buffer declaration differs from its same source target type.");
            inputs.Add(original with { ResourceMapping = mappings[0], PixelBinding = rows[0] });
        }
        return SourcePlan with { ResourceBindings = resources, PixelDefinitions = declarations, Inputs = inputs.AsReadOnly() };
    }
}

/// <summary>Exact finite node166 fresh shader selection. The complete initial
/// variant comes from native row0 copying, and the first21 capability masks come
/// from native997580's source table loop. Every active F103 variant writer is
/// projected before the feature matcher/whole96-byte equality selection. Source
/// inputs do not certify later Camera or shader edits, producer success or a draw.</summary>
public static partial class OriginalMapSourceCreatedShaderSelection
{
    public const string ShaderSha256 = "95575321D097F29696A35F377B68DD91F37BB7F4EAE3061EB3CA501006021E38";
    public const string GraphSha256 = "B9AE1EC8209C067DBC73084CDC0EB149A65E282C25D4B8AD398F43071072971D";

    /// <summary>Production feature selection from the exact owned kind1
    /// Camera/light/View producer. The raw48-byte overload remains available
    /// for explicitly resolved current producers and diagnostic source checks.</summary>
    public static OriginalDeferredF103SelectedSource PrepareFreshShader(
        LoadedOriginalDeferredTexturePlan source, OriginalMapSourceCreatedF103FeaturePacket ownedFeature,
        OriginalMapSourceCreatedLightingPacket lighting, OriginalDeferredF103CurrentVariantInputs current)
    {
        ArgumentNullException.ThrowIfNull(ownedFeature); ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(current);
        var camera = ownedFeature.AcceptedCamera; ownedFeature.ValidateFrame(camera, lighting);
        if (ownedFeature.CurrentView.CurrentProducerProvenance != current.CurrentProducerProvenance)
            throw new InvalidDataException("F103 feature and variant fields belong to different current producer boundaries.");
        var prepared = PrepareFreshShader(source, camera, current, ownedFeature.CurrentFeature48.Span);
        return new(prepared.SourcePlan, prepared.Camera, prepared.Current, prepared.CurrentFeature48.ToArray(),
            prepared.RequestedVariant96.ToArray(), prepared.FeatureMatch, prepared.BindingVariant,
            prepared.Combination, prepared.Writes, ownedFeature);
    }

    public static OriginalDeferredF103SelectedSource PrepareFreshShader(
        LoadedOriginalDeferredTexturePlan source,
        OriginalMapCameraConstructionPacket acceptedSourceCamera,
        OriginalDeferredF103CurrentVariantInputs current,
        ReadOnlySpan<byte> actualCurrentFeature48)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(acceptedSourceCamera);
        ArgumentNullException.ThrowIfNull(current);
        if (current.FrameNumber <= 0 || current.FrameNumber != acceptedSourceCamera.FrameNumber ||
            string.IsNullOrWhiteSpace(current.CurrentProducerProvenance) || actualCurrentFeature48.Length != 48)
            throw new InvalidDataException("F103 selection requires the accepted source Camera, actual serial frame identity, current producers and complete48-byte feature.");
        var loaded=ReadLoadedSource(source);
        source=loaded.Plan;
        var tables=loaded.Tables;
        byte[] desired = tables.Variants.Records[0].RawBytes.ToArray();
        var masks=loaded.Capabilities;
        var writes = new List<OriginalDeferredF103VariantWrite>();
        void Write(int word, uint value, string origin)
        {
            uint previous = BinaryPrimitives.ReadUInt32LittleEndian(desired.AsSpan(word * 4, 4));
            bool accepted = (masks[word] & (1u << (int)(value & 31))) != 0;
            uint next = accepted ? value : previous;
            BinaryPrimitives.WriteUInt32LittleEndian(desired.AsSpan(word * 4, 4), next);
            writes.Add(new(word, value, previous, next, accepted, origin));
        }
        // Registered Refraction sets aux bit2. ThinSurfaceBSDF TransSpecSrc=0
        // and AtDistance=0 leave the constructor bit4 branch clear. With the
        // exact non-ocean source, F103 calls9971D0 in EAD5B0.
        uint flags = current.ContextFlags3F0;
        Write(16, (flags & 0x40000000) == 0 ? 1u : 0u, "TC:EAD59A..EAD5B5/9971D0");
        //997190 word20 is unsupported for any nonzero value in this source;
        // native preserves the constructor word regardless of CommonView+14.
        if (masks[20] != 1 || BinaryPrimitives.ReadUInt32LittleEndian(desired.AsSpan(80, 4)) != 0)
            throw new InvalidDataException("F103 fixed word20 capability differs from the original source.");
        bool specular = current.NativeColorMode == 0x60
            ? acceptedSourceCamera.SourceSpecularOcclusionType3C0 == 1
            : acceptedSourceCamera.SourceSpecularOcclusionType3C0 != 0;
        Write(1, specular ? 1u : 0u, "TC:EAD6E7..EAD722/997350");
        uint bit14Inverse = (flags & 0x4000) == 0 ? 1u : 0u;
        Write(2, bit14Inverse, "TC:EAD769..EAD78E/997110");
        Write(5, bit14Inverse, "TC:EAD78E..EAD79A/997210");
        Write(17, bit14Inverse, "TC:EAD79A..EAD7A8/997010");
        uint lightFlagsInverse = (flags & 0x1800) == 0 ? 1u : 0u;
        Write(9, lightFlagsInverse, "TC:EAD7C5..EAD7E7/997290");
        Write(10, lightFlagsInverse, "TC:EAD7E9..EAD7F1/9972D0");
        Write(11, (flags & 0xC0000) == 0 ? 0u : 3u, "TC:EAD7F1..EAD80F/997860");
        Write(3, (flags & 0x8000) == 0 ? 1u : 0u, "TC:EAD80F..EAD826/997090");
        Write(4, (flags & 0x10000) == 0 ? 1u : 0u, "TC:EAD826..EAD83B/996FD0");
        // Both point/spot query outcomes request0 or1 at word12. This source
        // supports only0, which is already retained: neither result can change
        // the key. No later query value is invented or demanded from callers.
        uint retained12 = BinaryPrimitives.ReadUInt32LittleEndian(desired.AsSpan(48, 4));
        if (masks[12] != 1 || retained12 != 0)
            throw new InvalidDataException("F103 fixed word12 capability differs from the original source.");
        writes.Add(new(12, null, retained12, retained12, null,
            "TC:EAD83B..EAD86A/997150;source-mask1;both-query-outcomes-retain0"));
        if ((flags & 0x20000) == 0)
            Write(13, 1, "TC:EAD86A..EAD8A1/997310;registered-VolumetricFog");
        bool reflection = current.ReflectionQueryD57E20 != 0 && current.SelectedCamera1808Present;
        bool mode = (flags & 0x100000) != 0 && current.NativeColorMode != 0x60;
        Write(15, reflection ? (mode ? 2u : 0u) : (mode ? 2u : 1u), "TC:EAD8A1..EAD8FC/9970D0");
        bool registered = (current.CommonViewFlags8 & 0x200) != 0 && current.QueryD5BeA0 &&
            (current.CommonViewResource788Registered || current.CommonViewResource78CRegistered || current.CommonViewResource8B8Registered);
        Write(14, registered ? 0u : 1u, "TC:EAD900..EAD976/997050");
        var feature = G1ShaderFeatureRowReader.ReadRow(actualCurrentFeature48);
        var match = G1ShaderFeatureMatcher.Select(feature.Words.ToArray(), tables.Features.Select(r => r.Feature).ToArray());
        if (match.RowIndex < 0) throw new InvalidDataException("F103 current source feature matcher has no result.");
        var variant = tables.Variants.Match(desired) ??
            throw new InvalidDataException("F103 complete requested96-byte variant has no exact original source row; native fallback remains required.");
        uint[] indices = [(uint)match.RowIndex, 0, 0, 0, 0, 0, (uint)variant.Index];
        var combination = tables.ResolveCombination(indices, 0);
        return new(source, acceptedSourceCamera, current, actualCurrentFeature48.ToArray(), desired, match,
            variant, combination, Array.AsReadOnly(writes.ToArray()));
    }
}
