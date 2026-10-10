using San14.Data.Formats;

namespace San14.Data;

// Compatibility input carriers. New callers use HdrSelectionSettings directly.
public sealed record DeferredF103CurrentVariantInputs(
    long FrameNumber, uint ContextFlags3F0, uint NativeColorMode,
    uint CommonViewFlags8, bool QueryD5BeA0,
    int ReflectionQueryD57E20, bool SelectedCamera1808Present,
    bool CommonViewResource788Registered, bool CommonViewResource78CRegistered,
    bool CommonViewResource8B8Registered, string CurrentProducerProvenance);

/// <summary>Compatibility view of a typed HDR selection. Original encoding is
/// produced only by its legacy getters; it is not retained as current state.</summary>
public sealed class DeferredF103SelectedSource
{
    private readonly HdrCompatibilitySelectionSource source;
    internal DeferredF103SelectedSource(HdrCompatibilitySelectionSource source,
        MapCameraConstructionPacket camera,DeferredF103CurrentVariantInputs current,
        HdrProgramSelection selection,MapSourceCreatedF103FeaturePacket? feature=null)
    {
        this.source=source;Camera=camera.CopyAcceptedSnapshot();Current=current;
        Selection=selection;OwnedFeatureSource=feature;
    }
    public HdrProgramSelection Selection {get;}
    public LoadedDeferredTexturePlan SourcePlan=>source.Source;
    public MapCameraConstructionPacket Camera {get;}
    public DeferredF103CurrentVariantInputs Current {get;}
    public ReadOnlyMemory<byte> CurrentFeature48=>HdrShaderCodec.WriteFeature(Selection.Feature);
    public ReadOnlyMemory<byte> RequestedVariant96=>HdrShaderCodec.WriteVariant(Selection.Variant);
    public G1ShaderFeatureSelection FeatureMatch=>Selection.FeatureMatch;
    public G1ShaderVariantRecord BindingVariant=>source.Bank.CompatibilityVariant(Selection);
    public G2ShaderCombination Combination=>Selection.Program.CompatibilityCombination;
    public int PixelProgramIndex=>Selection.Program.PixelProgramIndex;
    public string PixelProgramSha256=>Selection.Program.Prepared.PixelSha256;
    public DxbcResourceDefinitions PixelDefinitions=>Selection.Program.PixelDefinitions;
    internal DxbcResourceDefinitions CanonicalPixelDefinitions=>source.Bank.CanonicalPixelDefinitions;
    public G1ShaderResourceBindings ResourceBindings=>Selection.Program.ResourceBindings;
    public MapSourceCreatedF103FeaturePacket? OwnedFeatureSource {get;}
    public bool HasVolumetricFogImage=>PixelDefinitions.Bindings.Any(binding=>binding.Name.Text=="sVolumetricFogMap");
    public LoadedDeferredTexturePlan ReadSelectedTexturePlan()=>source.TexturePlan(Selection.Program);
}

public static partial class MapSourceCreatedShaderSelection
{
    // Legacy resource metadata retained for callers that still identify these assets.
    public const string ShaderSha256="95575321D097F29696A35F377B68DD91F37BB7F4EAE3061EB3CA501006021E38";
    public const string GraphSha256="B9AE1EC8209C067DBC73084CDC0EB149A65E282C25D4B8AD398F43071072971D";
    public static DeferredF103SelectedSource PrepareFreshShader(LoadedDeferredTexturePlan source,
        MapSourceCreatedF103FeaturePacket ownedFeature,MapSourceCreatedLightingPacket lighting,
        DeferredF103CurrentVariantInputs current)
        =>Select(source,ownedFeature.AcceptedCamera,current,ownedFeature.CurrentFeature48.Span,ownedFeature);
    public static DeferredF103SelectedSource PrepareFreshShader(LoadedDeferredTexturePlan source,
        MapCameraConstructionPacket acceptedSourceCamera,DeferredF103CurrentVariantInputs current,
        ReadOnlySpan<byte> actualCurrentFeature48)
        =>Select(source,acceptedSourceCamera,current,actualCurrentFeature48,null);
    private static DeferredF103SelectedSource Select(LoadedDeferredTexturePlan source,
        MapCameraConstructionPacket camera,DeferredF103CurrentVariantInputs current,
        ReadOnlySpan<byte> feature,MapSourceCreatedF103FeaturePacket? ownedFeature)
    {
        var imported=HdrCompatibilitySelectionSource.Read(source);
        var selected=imported.Bank.Select(HdrShaderCodec.ReadFeature(feature),
            HdrShaderCodec.ReadSettings(current,camera.SourceSpecularOcclusionType3C0));
        return new(imported,camera,current,selected,ownedFeature);
    }
}
