using San14.Data.Formats;

namespace San14.Data;

public sealed record DeferredEarlyF103CurrentVariantInputs(DeferredF103CurrentVariantInputs Common,
    PreparedViewLocalLightQueries LocalLightQueries);

public sealed class DeferredEarlyF103SelectedSource
{
    private readonly HdrCompatibilitySelectionSource source;
    internal DeferredEarlyF103SelectedSource(HdrCompatibilitySelectionSource source,
        MapCameraConstructionPacket camera,DeferredEarlyF103CurrentVariantInputs current,
        HdrProgramSelection selection,MapSourceCreatedF103FeaturePacket? feature=null)
    {
        this.source=source;Camera=camera.CopyAcceptedSnapshot();EarlyCurrent=current;
        Selection=selection;OwnedFeatureSource=feature;
    }
    public HdrProgramSelection Selection {get;}
    public LoadedDeferredTexturePlan SourcePlan=>source.Source;
    public MapCameraConstructionPacket Camera {get;}
    public DeferredEarlyF103CurrentVariantInputs EarlyCurrent {get;}
    public DeferredF103CurrentVariantInputs Current=>EarlyCurrent.Common;
    public ReadOnlyMemory<byte> CurrentFeature48=>HdrShaderCodec.WriteFeature(Selection.Feature);
    public ReadOnlyMemory<byte> RequestedVariant96=>HdrShaderCodec.WriteVariant(Selection.Variant);
    public G1ShaderFeatureSelection FeatureMatch=>Selection.FeatureMatch;
    public G1ShaderVariantRecord BindingVariant=>source.Bank.CompatibilityVariant(Selection);
    public G2ShaderCombination Combination=>Selection.Program.CompatibilityCombination;
    public int PixelProgramIndex=>Selection.Program.PixelProgramIndex;
    public int VertexProgramIndex=>Selection.Program.VertexProgramIndex;
    public string PixelProgramSha256=>Selection.Program.Prepared.PixelSha256;
    public string VertexProgramSha256=>Selection.Program.Prepared.VertexSha256;
    public DxbcResourceDefinitions PixelDefinitions=>Selection.Program.PixelDefinitions;
    public G1ShaderResourceBindings ResourceBindings=>Selection.Program.ResourceBindings;
    public MapSourceCreatedF103FeaturePacket? OwnedFeatureSource {get;}
    public void ValidateParameterFrame(MapCameraConstructionPacket camera,MapSourceCreatedLightingPacket lighting)
    {
        if(camera.FrameNumber!=Current.FrameNumber || lighting.FrameNumber!=Current.FrameNumber)
            throw new InvalidDataException("HDR parameters belong to a different frame.");
    }
    public LoadedDeferredTexturePlan ReadSelectedTexturePlan()=>source.TexturePlan(Selection.Program);
}

public static partial class DeferredEarlyF103SourceSelection
{
    public const string GraphSha256="B9AE1EC8209C067DBC73084CDC0EB149A65E282C25D4B8AD398F43071072971D";
    public const string TextureObjectsSha256="4082FCB18D13F992407B2168F0105B9C86F53D095E2BA630802AD0BCDF19FF12";
    public const string FullscreenVertexSha256="C51D71A59DC89E4B23B68395180A41473B35914AD33BCE80C92AF636A379757D";
    public static DeferredEarlyF103SelectedSource PrepareFreshShader(LoadedDeferredTexturePlan source,
        MapSourceCreatedF103FeaturePacket ownedFeature,MapSourceCreatedLightingPacket lighting,
        DeferredEarlyF103CurrentVariantInputs current)
        =>Select(source,ownedFeature.AcceptedCamera,current,ownedFeature.CurrentFeature48.Span,ownedFeature);
    public static DeferredEarlyF103SelectedSource PrepareFreshShader(LoadedDeferredTexturePlan source,
        MapCameraConstructionPacket acceptedCamera,DeferredEarlyF103CurrentVariantInputs current,
        ReadOnlySpan<byte> actualCurrentFeature48)
        =>Select(source,acceptedCamera,current,actualCurrentFeature48,null);
    private static DeferredEarlyF103SelectedSource Select(LoadedDeferredTexturePlan source,
        MapCameraConstructionPacket camera,DeferredEarlyF103CurrentVariantInputs current,
        ReadOnlySpan<byte> feature,MapSourceCreatedF103FeaturePacket? ownedFeature)
    {
        var imported=HdrCompatibilitySelectionSource.Read(source);
        var selected=imported.Bank.Select(HdrShaderCodec.ReadFeature(feature),
            HdrShaderCodec.ReadSettings(current.Common,camera.SourceSpecularOcclusionType3C0,
                current.LocalLightQueries.AreEmpty));
        return new(imported,camera,current,selected,ownedFeature);
    }
}
