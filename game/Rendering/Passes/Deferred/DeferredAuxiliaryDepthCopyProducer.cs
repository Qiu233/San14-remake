using Godot;
using San14.Data;

/// <summary>Retained stock backend fields are explicit current inputs. The
/// bounded producer has no replacement for a custom VS or stencil program.</summary>
public sealed record DeferredAuxiliaryDepthCopyBackendState(Vector2 CurrentScreenScale29E0,
    Color CurrentColorScale29F0, bool CurrentCustomVertexShaderIsAbsent,
    bool? CurrentGlobalStencilIsDisabled, string CurrentBackendProducerProvenance);

/// <summary>Node97 completed its copy. Later source mode16 scene geometry is a
/// separate writer; this result does not certify that those draws were empty
/// or completed, and must not alone promote the whole refraction pass.</summary>
public sealed record DeferredAuxiliaryDepthCopyStage(
    DeferredF103SelectedSource AcceptedSelection,
    DeferredAuxiliaryProductionReceipt CopyDepthReceipt,
    DeferredAuxiliaryTargetDevice Target)
{
    public bool FollowingSourceGeometryRequired => true;
}

/// <summary>Executes the actual embedded integer-load PS of original node97.
/// Its main-depth input must already have been produced for the accepted frame;
/// allocation, an earlier frame or a cleared diagnostic image is insufficient.</summary>
public sealed class DeferredAuxiliaryDepthCopyProducer : IAsyncDisposable
{
    public LoadedDeferredAuxiliaryPixelSourceRecipes Source { get; }
    public LoadedDeferredAuxiliaryDepthCopySource EmbeddedSource { get; }
    public DeferredAuxiliaryPixelProgram Program { get; }
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object retirementGate=new();
    private volatile bool retiring;
    private Task? disposal;
    private IAsyncDisposable? pendingDraw;

    private DeferredAuxiliaryDepthCopyProducer(LoadedDeferredAuxiliaryPixelSourceRecipes source,
        LoadedDeferredAuxiliaryDepthCopySource embedded,DeferredAuxiliaryPixelProgram program)
        =>(Source,EmbeddedSource,Program)=(source,embedded,program);

    public static async Task<DeferredAuxiliaryDepthCopyProducer> BuildAsync(
        LoadedDeferredAuxiliaryPixelSourceRecipes source,
        LoadedDeferredAuxiliaryDepthCopySource embedded,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(embedded);
        ArgumentNullException.ThrowIfNull(compiler);
        if(source.Graph.Document.Nodes.Count<=97||source.Graph.Document.Nodes[97].Call.FactoryClassId!=0xFD1176C6||
            source.MainDepth.Object.ObjectId!=embedded.SourceDepthObjectId||
            source.Targets.Single(t=>t.Texture.Object.ObjectId==embedded.DestinationDepthObjectId).MipCount!=1)
            throw new InvalidDataException("Depth copy requires its actual node97 and source depth declarations.");
        return new(source,embedded,await DeferredAuxiliaryPixelProgram.BuildDepthCopyAsync(embedded,compiler));
    }

    public async Task<DeferredAuxiliaryDepthCopyStage> CopyDepthAsync(
        DeferredF103SelectedSource selection,RenderTargetDeviceTexture currentMainDepth,
        DeferredAuxiliaryTargetDevice destination,DeferredAuxiliaryDepthCopyBackendState backend,
        string currentMainDepthProducerProvenance)
    {
        ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(currentMainDepth);
        ArgumentNullException.ThrowIfNull(destination);ArgumentNullException.ThrowIfNull(backend);
        await gate.WaitAsync();
        try
        {
            await DrainPendingDrawCoreAsync();
            ObjectDisposedException.ThrowIf(retiring,this);Program.RequireAvailable();
            if(selection.Camera.ExecutableLocale!=EmbeddedSource.Locale||
                selection.SourcePlan.Graph.Source.ResourceUri!=Source.Graph.Source.ResourceUri||
                selection.SourcePlan.Graph.Source.DecodedSha256!=Source.Graph.Source.DecodedSha256||
                string.IsNullOrWhiteSpace(currentMainDepthProducerProvenance)||
                string.IsNullOrWhiteSpace(backend.CurrentBackendProducerProvenance)||
                !backend.CurrentCustomVertexShaderIsAbsent||
                !float.IsFinite(backend.CurrentScreenScale29E0.X)||!float.IsFinite(backend.CurrentScreenScale29E0.Y)||
                backend.CurrentScreenScale29E0.X<1||backend.CurrentScreenScale29E0.Y<1)
                throw new InvalidDataException("CopyDepth lacks its current source frame and proved full-coverage stock backend state.");
            if(currentMainDepth.Source.Object.ObjectId!=EmbeddedSource.SourceDepthObjectId||
                currentMainDepth.Source.Source.Source.ResourceUri!=Source.MainDepth.Source.Source.ResourceUri||
                currentMainDepth.Source.Source.Source.DecodedSha256!=Source.MainDepth.Source.Source.DecodedSha256||
                currentMainDepth.Source with{Source=Source.MainDepth.Source,Object=Source.MainDepth.Object}!=Source.MainDepth)
                throw new InvalidDataException("CopyDepth input is not the current original E13DDB4F main-depth owner.");
            var expected=Source.Targets.Single(t=>t.Texture.Object.ObjectId==EmbeddedSource.DestinationDepthObjectId);
            var nativeMainDepth=RenderTextureFormatMath.Resolve(Source.MainDepth);
            var mainDepthFormat=nativeMainDepth.TargetViewFormat switch
            {20=>RenderingDevice.DataFormat.D32SfloatS8Uint,40=>RenderingDevice.DataFormat.D32Sfloat,
                _=>throw new InvalidDataException("Source main depth has no exact supported depth-aspect adapter.")};
            DeferredAuxiliaryTargetSources.Validate(destination.Construction);
            if(destination.Source.Source.Source.ResourceUri!=expected.Texture.Source.Source.ResourceUri||
                destination.Source.Source.Source.DecodedSha256!=expected.Texture.Source.Source.DecodedSha256||
                destination.Source with{Source=expected.Texture.Source,Object=expected.Texture.Object}!=expected.Texture||
                destination.Construction.NativeFrameExtent!=expected.NativeFrameExtent||
                destination.ExplicitExtent!=currentMainDepth.ExplicitExtent||
                destination.ExplicitExtent!=new Vector2I(expected.Extent.Width,expected.Extent.Height)||
                destination.TargetFormat!=RenderingDevice.DataFormat.D32Sfloat||
                currentMainDepth.StorageFormat!=mainDepthFormat||
                currentMainDepth.ShaderFormat!=mainDepthFormat||
                currentMainDepth.TargetFormat!=mainDepthFormat)
                throw new InvalidDataException("CopyDepth requires the exact full-size native depth aspect, extent and target owner.");
            var stencilAdmission=DeferredAuxiliaryDepthCopyStencilAdmission.ForSourceTarget(
                expected.Texture,destination.TargetFormat,backend.CurrentGlobalStencilIsDisabled);
            var quad=DeferredAuxiliaryStockQuad.FromCurrentViewport(destination.ExplicitExtent,
                backend.CurrentScreenScale29E0,backend.CurrentColorScale29F0,backend.CurrentBackendProducerProvenance);
            var constants=new Dictionary<(uint,uint),ReadOnlyMemory<byte>>{{(1,0),quad.VertexConstantBytes}};
            var inputs=new DeferredDeviceBinding[]{new("Texture",0,currentMainDepth.ShaderViewRid,
                currentMainDepth.StorageRid,currentMainDepth.Source.Source.Source.ResourceUri+"#E13DDB4F/source-mip0")};
            DeferredAuxiliaryPixelDraw draw;
            try
            {
                draw=await DeferredAuxiliaryPixelDraw.BuildAsync(Program,destination,0,constants,inputs,
                    4,RenderingDevice.RenderPrimitive.Triangles,quad);
            }
            catch(NativeMapBorrowerConstructionException failure)
            {pendingDraw=failure.RetainedPackage;throw;}
            pendingDraw=draw;
            Exception? productionFailure=null;
            try
            {
                var receipt=DeferredAuxiliaryProductionReceipt.FromSourceWriter(Source.Graph,97,
                    selection.Current.FrameNumber,selection.Current.CurrentProducerProvenance,
                    "OriginalNode97CopyDepthOnly;ps="+Program.PixelTranslation.SourceProgramSha256+
                    ";mainDepthProducer="+currentMainDepthProducerProvenance+
                    ";backendProducer="+backend.CurrentBackendProducerProvenance+
                    ";destination-stencil-aspect="+(stencilAdmission.TargetHasNoStencilAspect?"absent":"present")+
                    ";global-stencil-disabled="+(backend.CurrentGlobalStencilIsDisabled?.ToString()??"unknown")+
                    ";followingMode16GeometryNotCertified",EmbeddedSource.DestinationDepthObjectId,[0]);
                await destination.ProduceAsync(receipt,(rd,surfaces)=>
                {
                    if(surfaces.Count!=1||surfaces[0].Source.MipLevel!=0)
                        throw new InvalidDataException("CopyDepth selected another source target surface.");
                    draw.Submit(rd);
                });
                return new(selection,receipt,destination);
            }
            catch(Exception failure){productionFailure=failure;throw;}
            finally
            {
                try{await DrainPendingDrawCoreAsync();}
                catch(Exception cleanup) when(productionFailure is not null)
                {throw new AggregateException("Auxiliary depth production and draw retirement failed.",productionFailure,cleanup);}
            }
        }
        finally{gate.Release();}
    }

    internal async Task DrainPendingDrawAsync()
    {
        await gate.WaitAsync();
        try{await DrainPendingDrawCoreAsync();}
        finally{gate.Release();}
    }
    private async Task DrainPendingDrawCoreAsync()
    {
        if(pendingDraw is not null)await pendingDraw.DisposeAsync();
        pendingDraw=null;
    }

    public ValueTask DisposeAsync()
    {
        lock(retirementGate)
        {
            retiring=true;
            return new(disposal is null||disposal.IsFaulted||disposal.IsCanceled?disposal=RetireAsync():disposal);
        }
    }
    private async Task RetireAsync()
    {
        await gate.WaitAsync();
        try{await DrainPendingDrawCoreAsync();await Program.DisposeAsync();}
        finally{gate.Release();}
    }
}
