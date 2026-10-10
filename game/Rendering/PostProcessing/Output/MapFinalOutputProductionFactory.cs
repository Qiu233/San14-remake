using Godot;
using San14.Data;

public sealed record MapFinalOutputProductionFrameInputs(
    MapCameraConstructionPacket Camera,MapSourceCreatedLightingPacket Lighting,
    MapSeasonRuntimeFrame CurrentSeason,NativeCameraTangentDispatch TangentMode,
    string SourceFrameOwner)
{
    public SkySeasonMainCameraRgbFrame? AppliedSeasonCamera { get; init; }
    public MapSourceCreatedF103FeaturePacket? OwnedSelectedFeature { get; init; }
    public SkySceneSelectedViewLightPacket? SelectedExposureLight { get; private init; }
    internal MapExposureSelectedLightReceipt? SelectedExposure { get; init; }
    internal NativeMapFrameContext? AcceptedContext { get; init; }

    /// <summary>Explicit owned source-frame boundary, also used by numeric
    /// fixtures. Retains its exact target context through asynchronous handoff.</summary>
    public static MapFinalOutputProductionFrameInputs FromOwnedSourceFrame(
        NativeMapFrameContext context,MapCameraConstructionPacket camera,
        MapSourceCreatedLightingPacket lighting,MapSourceCreatedF103FeaturePacket selectedFeature,
        MapSeasonRuntimeFrame season,NativeCameraTangentDispatch tangent,string actualFrameOwner,
        SkySeasonMainCameraRgbFrame? appliedSeasonCamera=null)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(selectedFeature);
        selectedFeature.ValidateFrame(camera,lighting);
        return new(camera,lighting,season,tangent,actualFrameOwner)
            {AppliedSeasonCamera=appliedSeasonCamera,OwnedSelectedFeature=selectedFeature,AcceptedContext=context};
    }

    /// <summary>Borrow the exact current owned source session generation. This
    /// selection is bounded to the remake's single constructed light record;
    /// it does not observe native D59120's independent original publisher.</summary>
    public static MapFinalOutputProductionFrameInputs FromSourceSession(
        MapSourceCreatedNativeSessionPreparedFrame current,NativeMapFrameContext context,
        MapSeasonRuntimeFrame season,NativeCameraTangentDispatch tangent,
        SkySeasonMainCameraRgbFrame? appliedSeasonCamera=null)
    {
        ArgumentNullException.ThrowIfNull(current);ArgumentNullException.ThrowIfNull(context);
        current.Feature.ValidateFrame(current.MainCamera,current.Lighting);
        if(!ReferenceEquals(current.Context,context)||!ReferenceEquals(current.Selection.OwnedFeatureSource,current.Feature)||
            current.Feature.CurrentView.SelectedRecordLightOwnerIdentity!=current.Lighting.Binding.LightOwnerIdentity||
            current.Feature.FrameNumber!=context.FrameNumber)
            throw new InvalidDataException("Final source input requires the exact callback context and selected owned kind1 generation.");
        return FromOwnedSourceFrame(context,current.MainCamera,current.Lighting,current.Feature,season,tangent,
            current.Feature.CurrentView.CurrentProducerProvenance+";owned-single-record-final-selection",appliedSeasonCamera);
    }

    /// <summary>Preserve the actual independently selected World record8
    /// payload for node1 Exposure. It is not the inner-modified F103 copy.</summary>
    public static MapFinalOutputProductionFrameInputs FromWorldSourceSession(
        MapSourceCreatedNativeSessionPreparedFrame current,NativeMapFrameContext context,
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame world,
        MapSeasonRuntimeFrame season,NativeCameraTangentDispatch tangent)
    {
        ArgumentNullException.ThrowIfNull(world);
        if(!ReferenceEquals(current.PreparedSourceFrame,world.First)||
            world.First.Common.ViewGroupSelector is not byte selector||selector is <1 or >32)
            throw new InvalidDataException("World Final requires its actual selected View and same prepared primary source.");
        uint mask=1u<<(selector-1);
        var selected=world.Queue.FirstOrDefault(record=>(record.CurrentWorldPQWord78&~mask)==0)
            ??throw new InvalidDataException("World Exposure selected record is absent from its actual current queue.");
        var payload=world.First.ViewFields.CurrentPublication.SelectedShadowRecord1BD0
            ??throw new InvalidDataException("World Exposure needs the independently prepared complete record8 payload.");
        if(selected.CurrentItemIndex!=payload.CurrentQueueIndex||selected.CurrentWorldPQOwnerIdentity!=payload.RecordOwnerIdentity||
            payload.FrameNumber!=context.FrameNumber||payload.LightOwnerIdentity!=current.Lighting.Binding.LightOwnerIdentity||
            !payload.CompleteLightData.Span.SequenceEqual(current.Lighting.LocalLightData.Span))
            throw new NotSupportedException("World Exposure requires its exact independently selected current primary record payload.");
        var light=SkySceneSelectedViewLightSource.Capture(payload.CompleteLightData.Span,
            new(context.FrameNumber,current.Lighting.Binding.ViewIdentity,payload.RecordOwnerIdentity,payload.PayloadOwnerIdentity));
        var input=FromSourceSession(current,context,season,tangent,world.Rgb);
        return input with{SelectedExposureLight=light,
            SourceFrameOwner=current.Feature.CurrentView.CurrentProducerProvenance+
                ";actual-World-selected-record8="+payload.RecordOwnerIdentity};
    }
}

/// <summary>Same accepted production node73 frame to the complete49 original
/// effect draws and PS28. Owns the source SPK current/shadow state and the GPU
/// chain. Scene/depth, Camera, lighting and World season stay borrowed inputs.</summary>
public sealed class MapFinalOutputProductionFactory:IAsyncDisposable
{
    public MapFinalOutputSceneChain Chain { get; }
    public MapFinalOutputProductionState Constants { get; }
    public MapExposureViewAux SelectedViewAux { get; }
    public MapFinalOutputProductionPrepared? LastAcceptedConstants { get; private set; }
    public RenderOutputPresentation? Presentation=>Chain.Presentation;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object lifetime=new();private int accepted;private bool retiring;
    private Task? disposal;private TaskCompletionSource? drained;
    private MapFinalOutputProductionFactory(MapFinalOutputSceneChain chain,MapExposureViewAux view)
    {Chain=chain;Constants=new(chain.Graph);SelectedViewAux=view;}
    public static async Task<MapFinalOutputProductionFactory> BuildAsync(
        GameResourceRepository repository,LoadedMapFinalOutputResources source,
        LoadedMapCameraConstructionSource sourceMainCamera,Vkd3dShaderCompiler compiler,
        MapExposureOwner? sharedExposure=null,bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if(sourceMainCamera.Camera.ObjectId!=MainMapCameraAdapter.MainCameraObjectId)
            throw new InvalidDataException("Production final factory requires its source Main38D Camera owner.");
        var view=MapExposureViewAux.ReadTemplate(repository.LoadObjects("KIDSSystemResource",0x6D011726),0x899F101B);
        var chain=await MapFinalOutputSceneChain.BuildCompleteSourceHostAsync(source,compiler,sourceMainCamera,sharedExposure,allowReadback);
        return new(chain,view);
    }
    public Task<RenderOutputPresentation> RenderAsync(NativeMapFrameContext context,
        MapFinalOutputProductionFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(input);
        if(!ReferenceEquals(input.AcceptedContext,context))
            throw new InvalidDataException("Production final source input belongs to its exact accepted target context.");
        var feature=input.OwnedSelectedFeature??throw new InvalidDataException("Production final requires its current owned selected kind1 LightData generation.");
        feature.ValidateFrame(input.Camera,input.Lighting);
        if(feature.CurrentView.SelectedRecordLightOwnerIdentity!=input.Lighting.Binding.LightOwnerIdentity)
            throw new InvalidDataException("Production final selected record differs from its actual primary lighting owner.");
        MapExposureSelectedLightReceipt? exposure=null;
        if(input.SelectedExposureLight is { } selected)
        {
            exposure=MapExposureSelectedLightReceipt.CaptureOwnedSelectedView(selected,input.Lighting,
                Constants.OwnedApplyLuminanceReferenceIdentity);
            exposure.ValidateFor(input.Lighting);
        }
        else if(feature.EffectiveLocalLightData.Span[0x10B0]!=2||(feature.EffectiveLocalLightData.Span[0xFDC]&1)!=0)
            throw new InvalidDataException("Production final cannot replace an unresolved external selected-light exposure branch with Camera luminance.");
        if(input.AppliedSeasonCamera is { } applied&&
            (applied.Inputs.FrameNumber!=context.FrameNumber||applied.Inputs.Source.Season.Calendar!=input.CurrentSeason||
             applied.Inputs.MainFrame.Camera.FrameNumber!=input.Camera.FrameNumber||
             applied.Inputs.Phase.Parameters!=input.Lighting.LightingSkylightParameters||
             applied.Inputs.Phase.LightingController!=input.Lighting.SelectedController||
             !applied.SunMoonRgb.Prefix.Span[..0x1D0].SequenceEqual(input.Lighting.PreparedCameraPrefix.Span[..0x1D0])))
            throw new InvalidDataException("Applied season post-output must consume the same Main phase/controller and preserved optical/effect fields as accepted lighting.");
        if(context.PreparedCamera is null||input.Camera.FrameNumber!=context.FrameNumber||
            input.Lighting.FrameNumber!=context.FrameNumber||input.CurrentSeason.FrameNumber!=context.FrameNumber||
            input.Lighting.SceneInputs.Camera!=context.PreparedCamera||
            input.Camera.SceneInputs!=context.PreparedCamera||
            !context.OutputFrame.Textures.TryGetValue(0x064B746B,out var scene))
            throw new InvalidDataException("Production final hook must consume the exact accepted scene Camera/lighting/season frame.");
        var camera=input.Camera.CopyAcceptedSnapshot();var lighting=input.Lighting.CopyAcceptedSnapshot();
        Accept();return RenderAcceptedAsync(context,input with{Camera=camera,Lighting=lighting,SelectedExposure=exposure},scene.ExplicitExtent);
    }
    private async Task<RenderOutputPresentation> RenderAcceptedAsync(NativeMapFrameContext context,
        MapFinalOutputProductionFrameInputs input,Vector2I extent)
    {
        await gate.WaitAsync();MapFinalOutputProductionPrepared? prepared=null;bool succeeded=false;
        try
        {
            bool srgb=RenderTextureFormatMath.Resolve(Chain.Source.OutputTexture).TargetViewFormat is 29 or 91;
            var scene=context.OutputFrame.Textures[0x064B746B];
            // This owned chain binds Scene/depth/Exposure and generated effects,
            // with no external PostEffect3 input. This is
            // its explicit construction contract, not an observation that the
            // original game lacks those independent inputs. With PostEffect3
            // absent the native format predicate is invariant to Common3C0.
            var effect=MapFinalOutputPostEffect3Owner.CreateAbsent(input.SourceFrameOwner+"/owned-PostEffect3",
                "owned-complete-source-chain:no-external-PostEffect3-input;Common3C0-invariant-when-absent");
            int commonMode=SelectedViewAux.CommonMode3C0??throw new InvalidDataException("Apply HDR needs its actual selected Common3C0 setting.");
            var hdr=MapFinalOutputHdrRoute.Capture(scene.EffectiveSource,extent.X,extent.Y,commonMode,effect,
                context.FrameNumber,input.SourceFrameOwner);
            hdr.ValidateSameWorkingTarget(scene.EffectiveSource,extent.X,extent.Y,context.FrameNumber,input.SourceFrameOwner);
            // The fresh source Camera's actual LDR key933 is zero and its
            // second slot is constructor-null. Their typed receipt follows
            // both loader/copy paths; unknown Camera1398 remains unread.
            var bloom=MapFinalOutputCameraBloomSource.ConstructFreshSource(input.Camera,
                input.SourceFrameOwner+"/fresh-source-Camera-Bloom-tail");
            bloom.ValidateSameCamera(input.Camera);
            prepared=Constants.Prepare(input.Camera,input.Lighting,input.CurrentSeason,SelectedViewAux,extent.X,extent.Y,srgb,input.TangentMode,input.SourceFrameOwner,
                input.SelectedExposure,hdr,bloom.CurrentMaxLum,bloom);
            var raster=SourceRaster();
            var effects=new MapFinalOutputEffectsContext(prepared.CurrentParameters,prepared.RetainedTemplatesByPass,
                new(0,0,ReadOnlyMemory<byte>.Empty),new(0,0,prepared.RadialUv),raster,Vector2.One,Colors.White,
                prepared.StockPixel,prepared.SourceEvidence);
            var final=new MapFinalOutputSceneDrawPacket(new Dictionary<uint,ReadOnlyMemory<byte>>{{0,prepared.FinalVertex}},
                new Dictionary<uint,ReadOnlyMemory<byte>>{{0,prepared.FinalPixel}},ReadOnlyMemory<byte>.Empty,
                new Dictionary<uint,ReadOnlyMemory<byte>>(),raster,prepared.SourceEvidence);
            var output=await Chain.RenderCompleteSourceAsync(context,new(prepared.Camera,prepared.Exposure,prepared.Weight,effects,final,prepared.SourceEvidence)
                {HdrRoute=hdr});
            succeeded=true;LastAcceptedConstants=prepared;return output;
        }
        finally{if(prepared is not null)Constants.Complete(prepared,succeeded);gate.Release();Complete();}
    }
    /// <summary>Attach before the selected Deferred assembler's Stages wrapper
    /// so its auxiliary read acknowledgement follows the final source draw.</summary>
    public NativeMapSceneStages Stages(
        Func<NativeMapFrameContext,MapFinalOutputProductionFrameInputs> sameFrameInput,
        NativeMapSceneStages? previous=null)
    {
        ArgumentNullException.ThrowIfNull(sameFrameInput);
        return (previous ?? new NativeMapSceneStages()) with
        {
            BeforeTargetRetirement = async targets=>
            {await Chain.RetireTargetAsync();if(previous?.BeforeTargetRetirement is { } prior)await prior(targets);},
            AfterDeferred = async context=>
            {if(previous?.AfterDeferred is { } prior)await prior(context);return await RenderAsync(context,sameFrameInput(context));}
        };
    }
    private static ModelRasterInputs SourceRaster()
    {
        var a=new ModelBlendAttachment(false,RenderingDevice.BlendFactor.One,RenderingDevice.BlendFactor.Zero,RenderingDevice.BlendOperation.Add,
            RenderingDevice.BlendFactor.One,RenderingDevice.BlendFactor.Zero,RenderingDevice.BlendOperation.Add,true,true,true,true);
        return MapFinalOutputRasterSnapshot.Capture(new(RenderingDevice.PolygonCullMode.Disabled,RenderingDevice.PolygonFrontFace.Clockwise,false,false,RenderingDevice.CompareOperator.Always,[a]));
    }
    private void Accept(){lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);accepted++;}}
    private void Complete(){lock(lifetime){if(--accepted==0&&retiring)drained?.TrySetResult();}}
    public ValueTask DisposeAsync(){lock(lifetime){retiring=true;return new(disposal??=DisposeCoreAsync());}}
    private async Task DisposeCoreAsync()
    {Task wait;lock(lifetime){wait=accepted==0?Task.CompletedTask:(drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;}await wait;await gate.WaitAsync();
        try{await Chain.DisposeAsync();LastAcceptedConstants=null;}finally{gate.Release();}}
}

/// <summary>Detach caller-owned attachment lists synchronously at frame
/// acceptance. Attachment records contain immutable scalar fields.</summary>
public static class MapFinalOutputRasterSnapshot
{
    public static ModelRasterInputs Capture(ModelRasterInputs raster)
    {
        ArgumentNullException.ThrowIfNull(raster);ArgumentNullException.ThrowIfNull(raster.Attachments);
        return raster with{Attachments=Array.AsReadOnly(raster.Attachments.ToArray())};
    }
}
