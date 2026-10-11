using Godot;
using San14.Data;

public sealed class SkyDirectionalOptionalFrame
{
    internal SkyDirectionalOptionalFrame(SkyAppliedSourceStarFrame star,
        SkyAppliedSourceCloudFrame cloud,SkySceneSourceFramePacket source,
        MapSourceCreatedSeasonLightingFrame present,SkySceneProducerFrame producers)
    {Star=star;Cloud=cloud;Source=source;Present=present;Producers=producers;}
    public SkyAppliedSourceStarFrame Star { get; }
    public SkyAppliedSourceCloudFrame Cloud { get; }
    public SkySceneSourceFramePacket Source { get; }
    public MapSourceCreatedSeasonLightingFrame Present { get; }
    public SkySceneProducerFrame Producers { get; }
}

/// <summary>Actual Directional Star/Cloud source producers. This owner accepts
/// independently selected Sky80/Stage/View execution directly; no old HDRI
/// F103 frame or NativeSessionPreparedFrame is required.</summary>
public sealed class SkyDirectionalOptionalSourceOwner : IAsyncDisposable
{
    private readonly Vkd3dShaderCompiler compiler;
    private readonly SkyAppliedSourceStarOwner stars;
    private readonly SkyAppliedSourceCloudOwner cloud;
    private SceneViewNativeMapSkyCloudTextureOwner cloudTextures;
    private SkyCloudColorProducer? cloudProducer;
    internal SceneViewNativeMapSkyCloudTextureOwner TextureOwner=>cloudTextures;
    internal SkyCloudColorProducer CloudProducer=>cloudProducer??throw new InvalidOperationException("Cloud device has not been built.");
    private readonly SkyDirectionalSelectedLightSource initialSource;
    private readonly SkyAppliedSourceStageFrame initialStages;
    private readonly MainSceneNode51Execution initialExecution;
    private readonly uint writeMask;
    private SkyDirectionalOptionalFrame? current;
    private SkyDirectionalOptionalFrame? initialCandidate;
    private bool built;
    private volatile bool retiring;
    public SkyCloudSourceClipPlane ClipSource => cloud.ClipSource;
    public SkyCloudSourceTextureGroup StaPlaneSource => cloudTextures.StaPlaneSource;
    public void RequireCurrentResourceContext()
    {
        ObjectDisposedException.ThrowIf(retiring, this);
        if (!built) throw new InvalidOperationException("Directional optional resource preparation has not completed.");
    }
    public Task<IReadOnlyList<PatternCameraStaReadyPlaneEntry>> BorrowReadyStaPlaneEntriesAsync()
    {
        RequireCurrentResourceContext();
        return cloudTextures.BorrowReadyStaPlaneEntriesAsync();
    }
    internal SceneViewNativeMapSkyCloudTextureOwner? PrepareSeasonTextures(SkyAppliedSeasonCloudTextureSources groups)
    {
        RequireCurrentResourceContext();
        var imported=SkyAppliedCloudTextureSet.Import(groups);
        return cloudTextures.TextureSet.SamePhysical(imported)?null:SceneViewNativeMapSkyCloudTextureOwner.Create(groups,imported);
    }
    internal void RequirePreparedSeasonTextures(SceneViewNativeMapSkyCloudTextureOwner previous)
    {
        RequireCurrentResourceContext();
        if(!ReferenceEquals(previous,cloudTextures))throw new InvalidOperationException("Cloud textures changed after season preparation.");
    }
    internal SceneViewNativeMapSkyCloudTextureOwner InstallPreparedSeasonTextures(
        SkyAppliedSeasonCloudTextureSources groups,SceneViewNativeMapSkyCloudTextureOwner? replacement)
    {
        var previous=cloudTextures;
        if(replacement is not null)cloudTextures=replacement;
        cloudTextures.InstallPreparedSeasonSources(groups);
        current=null;initialCandidate=null;
        return previous;
    }
    private SkyDirectionalOptionalSourceOwner(Vkd3dShaderCompiler compiler,
        SkyAppliedSourceStarOwner stars,SkyAppliedSourceCloudOwner cloud,
        SceneViewNativeMapSkyCloudTextureOwner textures,
        SkyDirectionalSelectedLightSource source,SkyAppliedSourceStageFrame stages,
        MainSceneNode51Execution execution,uint mask)
    {this.compiler=compiler;this.stars=stars;this.cloud=cloud;cloudTextures=textures;
      initialSource=source;initialStages=stages;initialExecution=execution;writeMask=mask;}
    public static SkyDirectionalOptionalSourceOwner ReadSource(GameResourceRepository repository,string locale,
        Vkd3dShaderCompiler compiler,SkyDirectionalSelectedLightSource initialSource,
        SkyAppliedSourceStageFrame initialStages,MainSceneNode51Execution initialExecution,
        uint actualWriteMask,uint actualStarNativeCullMode,string ownedProducerIdentity,SkyCloudProgramResources cloudPrograms)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(compiler);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedProducerIdentity);initialSource.ValidateCurrent();
        var present=initialSource.AcceptedLighting.AcceptedInputs.PresentSeasonCamera??throw new InvalidDataException("Optional Directional source needs current accepted Main/season.");
        initialStages.ValidateCurrent(initialSource.AcceptedLighting,present.AcceptedSeasonCamera.Inputs.Source.Selection,present.CurrentSkyCamera);
        initialExecution.RequireSelectedView(present.FrameNumber,initialSource.AcceptedLighting.Binding);
        if(actualWriteMask>15||actualStarNativeCullMode!=1)
            throw new NotSupportedException("Optional Directional source requires the actual retained write mask and original Star cull1.");
        var groups=SkyAppliedSeasonCloudTextures.Load(repository,present.AcceptedSeasonCamera.Inputs.Source);
        var selection=SkyAppliedSourceCloudSelectionOwner.CreateFresh(repository,locale,cloudPrograms).PrepareCurrent(present,initialStages,groups);
        return new(compiler,SkyAppliedSourceStarOwner.CreateFresh(repository,locale,ownedProducerIdentity+"/Star",
                NamedViewRendererResources.Load(repository),present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage),
            SkyAppliedSourceCloudOwner.CreateFresh(repository,locale,ownedProducerIdentity+"/Cloud",cloudPrograms),
            SceneViewNativeMapSkyCloudTextureOwner.ReadSource(selection,groups),initialSource,initialStages,initialExecution,actualWriteMask);
    }

    /// <summary>Factory passed directly to the Directional device adapter.
    /// Constructor clip is a cached pipeline plan, never a submitted current
    /// Host clip/history acknowledgement.</summary>
    public async Task<SkyDirectionalDeviceProducers> BuildAsync(SkyScene scene,SkySceneSourceFramePacket initialFrame)
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        if(built)throw new InvalidOperationException("Directional optional devices already built.");
        var present=initialSource.AcceptedLighting.AcceptedInputs.PresentSeasonCamera!;
        var prepared=Prepare(initialSource,initialStages,initialExecution,initialFrame,present,null,0,true);
        var star=await SkyStarProducer.BuildAsync(stars.Shader,stars.Program.Combination.Index,compiler,scene.PrimaryWork,
            stars.Geometry,prepared.Producers.StarFaces!,null,writeMask,
            new(RenderingDevice.PolygonCullMode.Disabled,false,false,RenderingDevice.CompareOperator.Always),prepared.Producers.StarGate!);
        try
        {
            var inputs=await cloudTextures.GetCurrentInputsAsync(prepared.Cloud.Selection);
            var clouds=await SkyCloudColorProducer.BuildAsync(prepared.Cloud.Selection.ProgramSelection,
                compiler,scene.PrimaryWork,inputs,prepared.Producers.CloudFaces!,writeMask);
            cloudProducer=clouds;initialCandidate=prepared;built=true;return new(star,clouds);
        }
        catch(Exception failure)
        {
            var partial=new NativeMapBorrowerRetirementPackage("Directional-optional-Star-partial/"+Guid.NewGuid().ToString("N"),star);
            try{await partial.DisposeAsync();}catch(Exception retirement){partial.RetainFailure();throw new NativeMapBorrowerConstructionException(failure,retirement,partial);}
            throw;
        }
    }
    /// <summary>The exact factory packet, still private-current, for explicit
    /// backend target preparation only. Reading it acknowledges no Host/time.</summary>
    public SkyDirectionalOptionalFrame RequireInitialProducerFrame()
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        if(!built||initialCandidate is null||!ReferenceEquals(current,initialCandidate))
            throw new InvalidOperationException("The actual initial producer packet is absent or has been replaced.");
        ValidateCandidate(initialCandidate);return initialCandidate;
    }
    public SkyDirectionalOptionalFrame PrepareCurrent(SkyDirectionalSelectedLightSource directional,
        SkyAppliedSourceStageFrame stages,MainSceneNode51Execution actualExecution,
        SkySceneSourceFramePacket actualFrame,MapOwnedGraphicsMaskPrefix actualPrefix,
        float explicitOwnedGeometryDelta)
    {
        if(!built)throw new InvalidOperationException("Build the actual optional WORK devices before current preparation.");
        var present=directional.AcceptedLighting.AcceptedInputs.PresentSeasonCamera??throw new InvalidDataException("Directional optional source lacks current Camera.");
        actualPrefix.RequireCanonicalSkyDeclaredHookScope();
        if(actualPrefix.FrameNumber!=actualFrame.FrameNumber||actualPrefix.ColorWriteMask!=writeMask||actualPrefix.SkyStarNativeCullMode!=1)
            throw new InvalidDataException("Optional Directional source requires this current frame's actual cached-pipeline-compatible graphics prefix.");
        return Prepare(directional,stages,actualExecution,actualFrame,present,actualPrefix,explicitOwnedGeometryDelta,false);
    }
    private SkyDirectionalOptionalFrame Prepare(SkyDirectionalSelectedLightSource directional,
        SkyAppliedSourceStageFrame stages,MainSceneNode51Execution execution,
        SkySceneSourceFramePacket frame,MapSourceCreatedSeasonLightingFrame present,
        MapOwnedGraphicsMaskPrefix? prefix,float delta,bool constructorPlan)
    {
        ObjectDisposedException.ThrowIf(retiring,this);directional.ValidatePreparedLights(frame.SourceFrame.Lights);
        var star=stars.PrepareCurrent(frame,present,stages,execution,delta,
            "explicit-owned-geometry-delta;not-clock254;candidate-until-complete-Host");
        SkyAppliedSourceCloudFrame clouds;
        if(constructorPlan)clouds=cloud.PrepareDirectionalBootstrap(frame,present,stages,execution,delta,
            "cached-constructor-pipeline-plan-no-GPU-history",directional);
        else
        {
            cloud.BindCurrentClipScope(prefix!);
            clouds=cloud.PrepareDirectionalCurrent(frame,present,stages,execution,delta,
                "explicit-owned-geometry-delta;candidate-until-complete-Host",directional);
        }
        if(cloudProducer is { } producer&&producer.Program.Combination.Index!=clouds.Selection.ColorCombination)
            throw new NotSupportedException("Changed Cloud program selection requires its actual program lifecycle.");
        cloudTextures.ValidateCurrent(clouds.Selection);
        var starPackets=star.SixPackets.Select(p=>new SkyStarFacePacket(p.VertexBytes,p.GeometryBytes,p.PixelBytes)).ToArray();
        var cloudPackets=clouds.SixPackets.Select(p=>new SkyCloudColorFacePacket(p.VertexBytes,p.PixelBytes,p.MaximumRaySamples)).ToArray();
        var producers=new SkySceneProducerFrame(starPackets,new(true,star.NativeScalar15C0,star.NativeScalar15C4),
            cloudPackets,stages.Sky2Enabled,null,null);
        return current=new(star,clouds,frame,present,producers);
    }
    public void ValidateCandidate(SkyDirectionalOptionalFrame frame)
    {
        if(!ReferenceEquals(current,frame))throw new InvalidDataException("Directional optional candidate is stale or foreign.");
        frame.Star.ValidateCandidate(frame.Source,frame.Present);frame.Cloud.ValidateCandidate(frame.Source,frame.Present);
        cloudTextures.ValidateCurrent(frame.Cloud.Selection);
    }
    public SkyCloudSourceClipPlanePacket RequireCurrentClipPacket()
    {var frame=current??throw new InvalidDataException("No actual Directional optional candidate.");ValidateCandidate(frame);return cloud.RequireCurrentClipPacket();}
    public void CommitCompleteHost(SkyDirectionalOptionalFrame frame)
    {ValidateCandidate(frame);stars.CommitCompleteFrame(frame.Star);cloud.CommitCompleteFrame(frame.Cloud);current=null;}
    // Device adapter retires the borrowing Star/Cloud draws first. This source
    // owner then retires their separately owned original texture allocations.
    public ValueTask DisposeAsync(){retiring=true;return cloudTextures.DisposeAsync();}
}
