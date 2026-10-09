using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current execution/order gates supplied by the owner of the
/// accepted geometry submission. Unknown gates cannot create a terrain
/// raster recipe. The final GPU receipt remains that owner's responsibility.</summary>
public sealed record OriginalTerrainPreviousModelRasterPublication(long FrameNumber,
    string ViewIdentity, string ContextOwnerIdentity, string LastPrimitiveIdentity,
    uint CurrentContextFlags3E4, int CurrentRasterOrdinal, bool? SourceGroupType10Selected,
    bool? NoPrimitiveOrContextRasterOverrides, bool? LastActualModelSubmittedBeforeTerrain,
    bool? NoInterveningDepthOrRasterAssignments, string CurrentProducerProvenance);

public sealed class OriginalTerrainInheritedModelRasterRecipe
{
    internal OriginalTerrainInheritedModelRasterRecipe(OriginalTerrainModelRasterInheritanceSource source,
        OriginalTerrainPreviousModelRasterPublication current, OriginalModelOrdinaryNativeStencilReceipt previous,
        OriginalModelQueueDepthState queueDepth, OriginalGeneralSceneTerrainCullTransition cull)
    { Source=source; CurrentPublication=current; PreviousModelStencil=previous; PreviousQueueDepth=queueDepth; CallerCull=cull; }
    public OriginalTerrainModelRasterInheritanceSource Source { get; }
    public OriginalTerrainPreviousModelRasterPublication CurrentPublication { get; }
    public OriginalModelOrdinaryNativeStencilReceipt PreviousModelStencil { get; }
    public OriginalModelQueueDepthState PreviousQueueDepth { get; }
    public OriginalGeneralSceneTerrainCullTransition CallerCull { get; }
    public long FrameNumber => CurrentPublication.FrameNumber;
    public string ViewIdentity => CurrentPublication.ViewIdentity;
    public string LastPrimitiveIdentity => CurrentPublication.LastPrimitiveIdentity;
    public uint NativeFillMode => Source.GroupRaster.NativeFillMode;
    /// <summary>Actual last-model state saved by D31D30 and restored after
    /// its Terrain invocation. This is distinct from the F8 draw state.</summary>
    public uint SavedSourceCullMode => CallerCull.SavedSourceCull;
    public uint SavedNativeCullMode => CallerCull.SavedNativeCull;
    public uint SourceDrawCullMode => CallerCull.DrawSourceCull;
    public uint NativeCullMode => CallerCull.DrawNativeCull;
    public uint RestoredSourceCullMode => CallerCull.RestoredSourceCull;
    public bool FrontCounterClockwise => Source.GroupRaster.FrontCounterClockwise;
    // Actual F8 D2D9A3 writes opcode15:DepthEnable, not opcode6:DepthWrite.
    public bool DepthTest => true;
    public bool DepthWrite => PreviousQueueDepth.DepthWrite;
    public uint NativeDepthCompare => PreviousQueueDepth.CompareCode;
    public string SourceProvenance => Source.SourceProvenance + ";" + CurrentPublication.CurrentProducerProvenance +
        ";F8-opcode15-DepthEnable1;DepthWrite/Compare-inherited;"+
        $"D31-saveCull{SavedSourceCullMode}/drawCull{SourceDrawCullMode}/restore{RestoredSourceCullMode};"+
        "terrain-stencil-separate;not-GPU-receipt";
}

/// <summary>Finite source53/54-or55/class18 primitive to GeneralScene56/F8
/// inheritance. Actual class18 cleanup uses1460/BA8060, so the generic
/// 9954B0 saved-Cull restoration is not substituted. No-model and imported
/// raster branches have no inferred constructor fallback.</summary>
public sealed class OriginalTerrainModelRasterInheritanceSource
{
    private readonly OriginalModelQueueStateSource queue;
    private readonly OriginalModelOrdinaryNativeStencilSource modelSource;
    private bool retained=true;
    private OriginalTerrainModelRasterInheritanceSource(OriginalModelQueueStateSource actualQueue,
        OriginalModelOrdinaryNativeStencilSource selected, OriginalModelGroupRasterSource group,
        LoadedOriginalGeneralSceneTerrainCallerSource caller)
    {
        queue=actualQueue; modelSource=selected; GroupRaster=group; CallerSource=caller;
    }
    public OriginalModelGroupRasterSource GroupRaster { get; }
    public LoadedOriginalGeneralSceneTerrainCallerSource CallerSource { get; }
    public int SourceDrawIndex => GroupRaster.Draw.Draw.Index;
    public string ModelUri => GroupRaster.Model.Model.Source.ResourceUri;
    public string ModelSha256 => GroupRaster.Model.Model.Source.DecodedSha256;
    public string SelectedModelSourceKey => modelSource.SourceKey;
    public string SourceProvenance => SelectedModelSourceKey + $";model={ModelUri}@{ModelSha256}/draw{SourceDrawIndex};"+
        "53/54-or55-D43577-D71500;D27BC0-type10-D27FE4-D71710;"+
        "actual170ABB0/BA8060-1460-GBuffer-v38-return/97F0B0;"+
        "D4D880-group-cleanup-preserves-depthwrite/compare/cull;"+
        "GeneralScene56-F8-D2D9A3-opcode15-DepthEnable;"+CallerSource.SourceProvenance;

    public static OriginalTerrainModelRasterInheritanceSource ReadFreshLastModelSource(
        LoadedModelShaderBindings actualModel, int drawIndex,
        OriginalModelRenderGraphShaderSource actualSelected, LoadedShaderContainer actualShader,
        LoadedOriginalGeneralSceneTerrainCallerSource? callerSource = null)
    {
        ArgumentNullException.ThrowIfNull(actualModel); ArgumentNullException.ThrowIfNull(actualSelected);
        ArgumentNullException.ThrowIfNull(actualShader);
        foreach(var source in new[]{actualModel.Dictionary.Source,actualModel.Model.Source,
            actualModel.BindingSource,actualSelected.Interface.Owner.Objects.Source,
            actualSelected.Graph.Source,actualShader.Source}) RequireCanonical(source);
        var queue=OriginalModelRendererStateSourceReader.ReadQueue(actualSelected.Graph,actualSelected.Node.Index);
        if(queue.Node.Index is not (54 or 55) || queue.ShaderStateOffset!=18 || queue.Flag2!=0 ||
            queue.Graph.Source.ResourceUri!="san14://rdb/system/024496EE")
            throw new InvalidDataException("Terrain raster inheritance requires the actual node53's ordinary54/55 source queue.");
        var geometry=G1ModelGeometryReader.Read(actualModel.Model.Source.Bytes,actualModel.Model.Source.Bytes.LongLength);
        var canonicalModel=actualModel with
        {
            Dictionary=actualModel.Dictionary with{Document=DokReader.Read(actualModel.Dictionary.Source.Bytes)},
            Model=actualModel.Model with{Geometry=geometry},
            Bindings=OriginalShaderBindingCsvReader.Read(actualModel.BindingSource.Bytes)
        };
        var dictionary=actualSelected.Interface.Owner.Objects with
            {Document=DokReader.Read(actualSelected.Interface.Owner.Objects.Source.Bytes)};
        var graph=actualSelected.Graph with{Document=RenderGraphReader.Read(actualSelected.Graph.Source.Bytes)};
        var selected=OriginalModelShaderResources.ResolveRenderGraphNodeBaseSource(canonicalModel,drawIndex,dictionary,graph,queue.Node.Index);
        if(actualSelected.Draw.Identifier.Draw.Index!=drawIndex ||
            actualSelected.Draw.Binding.Model.Model.Source.ResourceUri!=actualModel.Model.Source.ResourceUri ||
            actualSelected.Draw.Binding.Model.Model.Source.DecodedSha256!=actualModel.Model.Source.DecodedSha256 ||
            selected.Interface.Owner.Object.ObjectId!=actualSelected.Interface.Owner.Object.ObjectId ||
            selected.Interface.Provider?.Object.ObjectId!=actualSelected.Interface.Provider?.Object.ObjectId)
            throw new InvalidDataException("Previous selected model source is not the actual canonical draw/provider generation.");
        var modelSource=OriginalModelOrdinaryNativeStencilSource.ReadFreshSelected(queue,selected.Interface,actualShader);
        var group=OriginalModelRendererStateSourceReader.ReadGroupRaster(canonicalModel,drawIndex) ??
            throw new InvalidDataException("Previous source model has no proved type10 group raster; current inherited raster is required.");
        // The previous four-argument reader retains its original TC source
        // contract. A loaded SC/TC GeneralScene owner supplies its same caller
        // here; importing native executable windows belongs to that owner load.
        callerSource ??= OriginalGeneralSceneTerrainCallerSource.Create("TC");
        return new(queue,modelSource,group,callerSource);
    }

    public OriginalTerrainInheritedModelRasterRecipe PrepareCurrent(
        OriginalModelOrdinaryNativeStencilReceipt previousActualModel,
        OriginalTerrainPreviousModelRasterPublication current)
    {
        ArgumentNullException.ThrowIfNull(previousActualModel); ArgumentNullException.ThrowIfNull(current);
        if(!retained)
            throw new InvalidDataException("Previous model raster owner was edited/imported/replaced.");
        var previous=previousActualModel.CurrentPublication;
        if(!previousActualModel.ShouldSubmitPrimitive || previousActualModel.Source.SourceKey!=SelectedModelSourceKey ||
            current.FrameNumber<1 || current.FrameNumber!=previous.FrameNumber || current.ViewIdentity!=previous.ViewIdentity ||
            current.LastPrimitiveIdentity!=previous.PrimitiveIdentity || current.CurrentRasterOrdinal!=0 ||
            current.SourceGroupType10Selected!=true || current.NoPrimitiveOrContextRasterOverrides!=true ||
            current.LastActualModelSubmittedBeforeTerrain!=true || current.NoInterveningDepthOrRasterAssignments!=true ||
            new[]{current.ContextOwnerIdentity,current.LastPrimitiveIdentity,current.CurrentProducerProvenance}.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Terrain raster inheritance needs the exact last submitted same-frame model, retained type10 ordinal0 and complete current order/override gates.");
        var depth=OriginalModelRendererStateSourceReader.PrepareQueueDepth(queue,(current.CurrentContextFlags3E4&0x20000000)!=0);
        var cull = CallerSource.PrepareTerrainCull(GroupRaster.GroupRasterCode);
        return new(this,current,previousActualModel,depth,cull);
    }
    public void MarkImportedOrEdited(string actualWriter)
    {ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter);retained=false;}
    private static void RequireCanonical(LoadedRdbAsset source)
    {
        if(source.OverrideSource is not null || source.IndexSource is null || source.IndexSource.IsOverride ||
            Convert.ToHexString(SHA256.HashData(source.Bytes))!=source.DecodedSha256)
            throw new InvalidDataException("Terrain raster inheritance requires canonical unchanged source bytes and index.");
    }
}
