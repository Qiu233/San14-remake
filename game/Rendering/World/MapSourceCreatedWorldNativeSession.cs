using System.Buffers.Binary;
using San14.Data;

/// <summary>Actual five-root World session with ordered source510/PS383
/// submissions. Auxiliary and final stages can consume its first pass's shared
/// View/Camera; each late pass retains its own independent modifier and constants.</summary>
public sealed partial class MapSourceCreatedWorldNativeSession : IAsyncDisposable
{
    public DeferredPixelProgram Program { get; }
    public GgxAmbientLut Ggx { get; }
    public NativeDeferredResources Resources { get; }
    public MapSelectedSourceSamplerOwner Samplers { get; }
    public MapSelectedSourceImageOwner Images { get; }
    public DeferredSelected383PluginOwner Plugins { get; }
    public MapSourceCreatedWorldDeferredFrameAssembler Assembler { get; }
    public NativeMapModelPopulationRenderer Population => Assembler.Population;
    public NativeMapArmyPopulationRenderer Army { get; }
    public MapLightingBinding Binding { get; }
    public MapSourceCreatedNativeSessionPreparedFrame? CurrentFrame { get; private set; }
    public SceneViewNativeMapWorldF103SourceOwner.CurrentFrame? CurrentWorld { get; private set; }
    public IReadOnlyList<MapSourceCreatedNativeSessionPreparedFrame> CurrentPasses { get; private set; }=[];
    public MapSourceCreatedNativeSessionPreparedFrame? GeometryFrame { get; private set; }
    public SceneViewNativeMapWorldF103SourceOwner.CurrentFrame? GeometryWorld { get; private set; }
    public IReadOnlyList<MapSourceCreatedNativeSessionPreparedFrame> GeometryPasses { get; private set; }=[];
    public NativeMapArmyPreparedFrame? CurrentArmyCpuFrame { get; private set; }
    public IReadOnlyList<NativeMapStageTiming> LastBeforeFrameTimings { get; private set; }=[];
    private IReadOnlyList<NativeDeferredPacket>? preparedLatePackets;
    private NativeMapWorldDeferredSubmittedFrame? acceptedLateSubmission;
    private NativeDeferredPacket[]? appliedLatePackets;
    private readonly LoadedDeferredSamplerNativeSource native;
    private readonly LoadedSkyAmbientResources skySource;
    private readonly MapCommonViewFilterSource filterSource;
    private readonly Func<NativeMapFrameContext,NativeMapArmyPreparedFrame,
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame> current;
    private readonly Func<MapSourceCreatedNativeSessionPreparedFrame,NativeMapModelPopulationAcceptedFrame,
        Task<MapSourceCreatedNativeSessionGpuInputs>> gpu;
    private readonly Func<MapSourceCreatedNativeSessionPreparedFrame,SkyDirectionalAmbientDeviceBinding> ambient;
    private readonly object lifetime=new();
    private bool retiring;
    private Task? disposal;

    private MapSourceCreatedWorldNativeSession(DeferredPixelProgram program,GgxAmbientLut ggx,
        NativeDeferredResources resources,MapSelectedSourceSamplerOwner samplers,
        MapSelectedSourceImageOwner images,MapSourceCreatedWorldDeferredFrameAssembler assembler,
        NativeMapArmyPopulationRenderer army,DeferredSelected383PluginOwner plugins,
        LoadedDeferredSamplerNativeSource native,LoadedSkyAmbientResources skySource,
        MapCommonViewFilterSource filterSource,MapLightingBinding binding,
        Func<NativeMapFrameContext,NativeMapArmyPreparedFrame,SceneViewNativeMapWorldF103SourceOwner.CurrentFrame> current,
        Func<MapSourceCreatedNativeSessionPreparedFrame,NativeMapModelPopulationAcceptedFrame,Task<MapSourceCreatedNativeSessionGpuInputs>> gpu,
        Func<MapSourceCreatedNativeSessionPreparedFrame,SkyDirectionalAmbientDeviceBinding> ambient)
        =>(Program,Ggx,Resources,Samplers,Images,Assembler,Army,Plugins,this.native,this.skySource,this.filterSource,Binding,this.current,this.gpu,this.ambient)=
          (program,ggx,resources,samplers,images,assembler,army,plugins,native,skySource,filterSource,binding,current,gpu,ambient);

    public static MapLightingBinding CreateExpectedBinding(LoadedDeferredTexturePlan plan,string view,string camera,string light)
    {
        var actual=MapSourceCreatedNativeSession.CreateExpectedDefault1022Binding(plan,view,camera,light);
        return actual with {DeferredCombination=510};
    }

    public static async Task<MapSourceCreatedWorldNativeSession> BuildAsync(GameResourceRepository repo,Vkd3dShaderCompiler compiler,
        MapSourceCreatedDirectionalLightingDeviceOwner lightOwner,NativeMapModelPopulationRenderer population,
        NativeMapArmyPopulationRenderer army,MapMaterialCachePrepared materials,
        MapSourceCreatedF103CurrentFrame bootstrap,
        Func<NativeMapFrameContext,NativeMapArmyPreparedFrame,SceneViewNativeMapWorldF103SourceOwner.CurrentFrame> current,
        Func<MapSourceCreatedNativeSessionPreparedFrame,NativeMapModelPopulationAcceptedFrame,Task<MapSourceCreatedNativeSessionGpuInputs>> gpu,
        Func<MapSourceCreatedNativeSessionPreparedFrame,SkyDirectionalAmbientDeviceBinding> ambient)
    {
        ArgumentNullException.ThrowIfNull(repo);ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(current);ArgumentNullException.ThrowIfNull(gpu);ArgumentNullException.ThrowIfNull(ambient);
        var selected=bootstrap.Selection;selected.Validate();
        var source=lightOwner.Sources.Single(s=>s.Identity==bootstrap.Lighting.SourceIdentity);
        var pairs=DeferredSourceNull2D.FromFreshDirectionalConstruction(source.Construction,
            DeferredImageStateScope.FreshSourceConstructionNoAssignments);
        var program=await DeferredPixelProgram.BuildAsync(selected,compiler,pairs);
        GgxAmbientLut? ggx=null;NativeDeferredResources? resources=null;
        MapSelectedSourceSamplerOwner? samplers=null;MapSelectedSourceImageOwner? images=null;
        MapSourceCreatedWorldDeferredFrameAssembler? assembler=null;
        try
        {
            var native=RenderSamplerSourceAdapters.Load(repo.SourceDirectory,selected.Camera.ExecutableLocale);
            var sky=SkyAmbientResources.Load(repo,repo.LoadObjects("system",0xDAB638B2),0x910603AC,
                repo.LoadObjects("system",0x718CCB8B),repo.LoadObjects("system",0x93FB3280));
            ggx=await GgxAmbientLut.BuildAsync(GgxShaderCompiler.Load(selected.Camera.ExecutableLocale),compiler);
            resources=await NativeDeferredResources.BuildSelectedAsync(program,materials.Table,ggx);
            var imagePlan=MapSelectedSourceImagePlan.Read(selected);
            var filters=MapCommonViewFilterSource.LoadGameDefault(repo);
            var transparency=PrepareTransparency(bootstrap,filters);
            var samplerPlan=PrepareSamplers(program,native,sky,imagePlan,transparency.Selection,pairs);
            samplers=await MapSelectedSourceSamplerOwner.BuildAsync(samplerPlan);images=MapSelectedSourceImageOwner.FromSource(imagePlan);
            var provider=new MapSourceCreatedDirectionalDeferredProvider(program,lightOwner,source);
            assembler=new(provider,resources,samplers,images,materials,population);
            var plugins=DeferredSelected383PluginOwner.CreateAfterSuccessfulGraphicsInitialization(
                RenderSamplerSourceAdapters.LoadSelected767PluginSource(repo.SourceDirectory,selected.Camera.ExecutableLocale),selected.SourcePlan);
            return new(program,ggx,resources,samplers,images,assembler,army,plugins,native,sky,filters,bootstrap.Lighting.Binding,current,gpu,ambient);
        }
        catch
        {
            if(assembler is not null)await assembler.DisposeAsync();
            if(images is not null)await images.DisposeAsync();if(samplers is not null)await samplers.DisposeAsync();
            if(resources is not null)await resources.DisposeAsync();if(ggx is not null)await ggx.DisposeAsync();await program.DisposeAsync();throw;
        }
    }

    public NativeMapSceneStages Stages(NativeMapSceneStages? preceding=null,
        Func<MapSourceCreatedNativeSessionPreparedFrame,Task>? afterCurrentPrepared=null,
        Func<MapSourceCreatedNativeSessionPreparedFrame,NativeMapModelPopulationRenderer,Task>? submitStaticGeometry=null,
        Func<MapSourceCreatedNativeSessionPreparedFrame,NativeMapArmyPreparedFrame,Task>? submitWorldGeometryBeforeTerrain=null)
        =>(preceding??new NativeMapSceneStages()) with
        {
            BeforeFrame=async context=>
            {
                var watch=System.Diagnostics.Stopwatch.StartNew();
                var timings=new List<NativeMapStageTiming>(5);
                double previous=0;
                void Mark(string stage)
                {
                    double elapsed=watch.Elapsed.TotalMilliseconds;
                    timings.Add(new(stage,elapsed-previous,elapsed));previous=elapsed;
                }
                Assembler.CompletePreviousReads(context);
                Mark("previous-read-retirement");
                if(preceding?.BeforeFrame is { } before)await before(context);
                Mark("preceding-source-stage");
                CurrentArmyCpuFrame=await Army.CaptureBeforeFrameAsync(context);
                Mark("current-army-cpu-capture");
                PrepareCurrent(context,CurrentArmyCpuFrame);
                Mark("current-world-source-preparation");
                if(afterCurrentPrepared is not null)await afterCurrentPrepared(RequireCurrent(context));
                Mark("current-consumers-and-sky");
                LastBeforeFrameTimings=timings.AsReadOnly();
            },
            BeforeTerrain=async context=>
            {
                var frame=RequireCurrent(context);
                if(submitWorldGeometryBeforeTerrain is { } submitWorld)
                    await submitWorld(frame,CurrentArmyCpuFrame??throw new InvalidDataException("World geometry lacks its captured Army CPU roster."));
                else
                {
                    if(submitStaticGeometry is { } submit)await submit(frame,Population);
                    else await Population.PrepareAndDrawAsync(context);
                    await Army.PrepareAndDrawAsync(context);
                }
                if(preceding?.BeforeTerrain is { } before)await before(context);
            },
            AfterDeferred=async context=>
            {
                try{return preceding?.AfterDeferred is { } after?await after(context):null;}
                finally{await Assembler.CompleteDeferredReadsAsync(context);}
            },
            BeforeTargetRetirement=async context=>
            {
                AdvanceAcceptedSubmittedWorldRevision();
                CurrentFrame=null;CurrentWorld=null;CurrentPasses=[];GeometryFrame=null;GeometryWorld=null;GeometryPasses=[];CurrentArmyCpuFrame=null;preparedLatePackets=null;
                await Assembler.RetireTargetDependenciesAsync();await Army.BeforeTargetRetirementAsync(context);await Population.BeforeTargetRetirementAsync(context);
                if(preceding?.BeforeTargetRetirement is { } retire)await retire(context);
            }
        };

    private void PrepareCurrent(NativeMapFrameContext context,NativeMapArmyPreparedFrame army)
    {
        lock(lifetime)ObjectDisposedException.ThrowIf(retiring,this);
        AdvanceAcceptedSubmittedWorldRevision();
        CurrentFrame=null;CurrentWorld=null;CurrentPasses=[];GeometryFrame=null;GeometryWorld=null;GeometryPasses=[];preparedLatePackets=null;
        var world=current(context,army);
        if(world.World.View.Binding!=Binding||world.World.Passes.Count==0||world.First.FrameNumber!=context.FrameNumber)
            throw new InvalidDataException("World session requires its actual current counted View and ordered passes.");
        var frames=PreparePasses(context,world);
        CurrentWorld=GeometryWorld=world;CurrentPasses=GeometryPasses=frames;CurrentFrame=GeometryFrame=frames[0];
    }

    private IReadOnlyList<MapSourceCreatedNativeSessionPreparedFrame> PreparePasses(
        NativeMapFrameContext context, SceneViewNativeMapWorldF103SourceOwner.CurrentFrame world)
    {
        var frames=world.World.Passes.Select((p,index)=>
        {
            var f=p.Frame;f.Feature.ValidateFrame(f.Camera,f.Lighting);Program.ValidateCompatibleCurrentSelection(f.Selection);
            if(f.Lighting.SceneInputs.Camera!=context.PreparedCamera||f.Lighting.Binding!=Binding)
                throw new InvalidDataException("World pass differs from the accepted geometry Camera or primary owner.");
            var transparency=PrepareTransparency(f,filterSource);
            var proofs=Assembler.Lighting.CaptureCurrentSourceNullProofs(f.Selection);
            var plan=PrepareSamplers(Program,native,skySource,MapSelectedSourceImagePlan.Read(f.Selection),transparency.Selection,proofs);
            _=Samplers.GetBindings(plan);
            return new MapSourceCreatedNativeSessionPreparedFrame(context,f.Camera.CopyAcceptedSnapshot(),f.Lighting,f.Feature,f.Selection,plan)
                {PreparedSourceFrame=f,GeometrySourceFrame=world.GeometryWorld.Passes[index].Frame};
        }).ToArray();
        return Array.AsReadOnly(frames);
    }

    /// <summary>Consume the real later CPU writer on the same geometry
    /// roster. Existing admissions and device receipts keep their early Q.</summary>
    public void PrepareCurrentShading(NativeMapFrameContext context,
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame actualPostGeometry)
    {
        var geometry=GeometryWorld??throw new InvalidDataException("Current shading has no prepared geometry roster.");
        _=RequireCurrent(context);
        if (!ReferenceEquals(actualPostGeometry.GeometryWorld,geometry.World) ||
            !ReferenceEquals(actualPostGeometry.Admission,geometry.Admission) ||
            !ReferenceEquals(actualPostGeometry.CountedQueue,geometry.CountedQueue) ||
            !ReferenceEquals(actualPostGeometry.World.Plan,geometry.World.Plan) ||
            actualPostGeometry.World.Passes.Count!=geometry.World.Passes.Count)
            throw new InvalidDataException("Post-geometry shading belongs to another World/View admission.");
        for(int i=0;i<geometry.World.Passes.Count;i++)
            WorldGeometryShadingPhase.RequireSameGeometrySnapshot(
                geometry.World.Passes[i].Frame,actualPostGeometry.World.Passes[i].Frame);
        var passes=PreparePasses(context,actualPostGeometry);
        CurrentWorld=actualPostGeometry;CurrentPasses=passes;CurrentFrame=passes[0];preparedLatePackets=null;
    }

    public async Task<IReadOnlyList<NativeDeferredPacket>> PrepareOrderedDeferredAsync(NativeMapFrameContext context)
    {
        var first=RequireCurrent(context);var population=Population.LastAcceptedFrame;
        if(population is null||!ReferenceEquals(population.Context,context)||Army.LastAcceptedFrame is not { } army||
            !ReferenceEquals(army.Prepared.Context,context)||!ReferenceEquals(population.Frame.Visibility,first.CpuPopulation!.Visibility))
            throw new InvalidDataException("World session needs the actual current model and Army submissions that supplied its counters.");
        var produced=await gpu(first,population);var packets=new List<NativeDeferredPacket>();
        foreach(var frame in CurrentPasses)
        {
            var plugin=Plugins.Prepare(frame.Selection,frame.Lighting);
            var additional=plugin.Complete(produced.Refraction,produced.TranslucencyViewport,produced.Projection,produced.CameraProducerProvenance);
            var constants=DeferredSelected383Constants.Create(frame.Selection,frame.Lighting,additional);
            var transparency=PrepareTransparency(frame.PreparedSourceFrame!,filterSource);
            var geometry=MapSourceCreatedDeferredFrameInputs.FromAcceptedPopulation(population,produced.TerrainMaterials,
                constants.Constants,[],transparency.Selection,[],produced.Raster);
            var packet=await Assembler.PrepareAsync(context,frame.Lighting,
                new(frame.Selection,frame.Samplers,geometry,produced.ProducedAuxiliaryDevices),ambient(frame),additional,
                frame,produced.SharedCameraReady??throw new InvalidDataException("World passes require their actual shared Camera/Aux generation."),
                transparency.Camera,transparency.Common);
            packets.Add(packet.Deferred);
        }
        return preparedLatePackets=Array.AsReadOnly(packets.ToArray());
    }

    /// <summary>Join the scene's real completed late draws to this session's
    /// privately prepared complete World roster. RenderOrderedAsync applies the
    /// same source Camera writer after preparation; compare that resulting packet.</summary>
    public void RequireSubmittedWorld(NativeMapWorldDeferredSubmittedFrame submission)
        =>_=RequireSubmittedWorldAndReadAppliedPackets(submission);
    private NativeDeferredPacket[] RequireSubmittedWorldAndReadAppliedPackets(NativeMapWorldDeferredSubmittedFrame submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        lock(lifetime)ObjectDisposedException.ThrowIf(retiring,this);
        var context=submission.Context;
        var first=RequireCurrent(context);
        submission.RequireCurrent(context);
        if(ReferenceEquals(acceptedLateSubmission,submission)&&appliedLatePackets is { } accepted)
            return accepted;
        if(preparedLatePackets is not { } packets||CurrentWorld is null||
            packets.Count!=CurrentPasses.Count||packets.Count!=CurrentWorld.World.Passes.Count||
            CurrentPasses.Any(p=>!ReferenceEquals(p.Context,context))||context.PreparedCamera is null||
            first.Lighting.SceneInputs.Camera!=context.PreparedCamera)
            throw new InvalidDataException("Late World submission has no exact privately prepared current source roster.");
        var actual=packets.Select(p=>p.ApplySceneFields(Program,new(Camera:context.PreparedCamera),out _)).ToArray();
        submission.RequireExactOrderedPackets(Program,actual);
        acceptedLateSubmission=submission;appliedLatePackets=actual;
        return actual;
    }

    private MapSourceCreatedNativeSessionPreparedFrame RequireCurrent(NativeMapFrameContext context)
        =>CurrentFrame is { } frame&&ReferenceEquals(frame.Context,context)?frame:
            throw new InvalidDataException("Current World session preparation is unavailable.");

    private static MapSelectedSourceSamplerPlan PrepareSamplers(DeferredPixelProgram program,
        LoadedDeferredSamplerNativeSource native,LoadedSkyAmbientResources sky,MapSelectedSourceImagePlan images,
        DeferredTransparencyImageSelection transparency,IReadOnlyList<DeferredSourceNull2D> actualCurrentPairs)
    {
        var proofs=program.SelectedSourceNullSpecialization??throw new InvalidDataException("World program requires its actual Directional Hatching proof.");
        var keys=new List<MapSelectedSamplerSourceInput>();
        foreach(var target in new[]{images.GraphTargets.Single(t=>t.Role=="RefractionDepth"),images.SelectSpecular(transparency),
            images.GraphTargets.Single(t=>t.Role=="BlurredDepth")})
            keys.Add(MapSelectedSourceSamplerPlan.FromRenderTexture(images,native,target.EncodedParameter,target.Texture));
        keys.Add(MapSelectedSourceSamplerPlan.FromProducerKey(images,native,27,sky.Resource.Source,
            "object/910603AC/Q80/original-CubeTexture-v28",RenderSamplerSourceAdapters.AmbientSpecularKey(
                RenderSamplerSourceAdapters.CubeTextureKey(native,sky.SpecularTargets[0].LevelNum))));
        return MapSelectedSourceSamplerPlan.ResolveWithCurrentDirectionalProofs(images,native,
            program.UnspecializedTranslation,keys,proofs,actualCurrentPairs);
    }

    private static (DeferredAuxiliaryCameraFilterSource Camera,MapCommonViewFilterCurrent Common,
        DeferredTransparencyImageSelection Selection) PrepareTransparency(
        MapSourceCreatedF103CurrentFrame frame,MapCommonViewFilterSource source)
    {
        if(frame.Common.Scope!=MapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication)
            throw new NotSupportedException("World transparency requires its actual fresh source Common filter owner.");
        var main=frame.Lighting.AcceptedInputs.PresentSeasonCamera?.AcceptedSeasonCamera.Inputs.MainFrame
            ??throw new InvalidDataException("World transparency needs the actual current Main/season source.");
        var camera=DeferredAuxiliaryCameraFilterSource.CaptureSourceLoadedMain(main,
            AuxiliaryCameraFilterScope.SourceLoadedCameraNoLaterFilterWriter);
        var common=source.PrepareAppliedCurrent(frame.FrameNumber,frame.Common.CommonTableOwnerIdentity,frame.Lighting.Binding.ViewIdentity);
        return (camera,common,camera.ReadTransparencySelection(
            frame.Common.CommonViewResource240Present?common.TransparencyResolution8C:null));
    }
    public ValueTask DisposeAsync(){lock(lifetime){retiring=true;return new(disposal??=RetireAsync());}}
    private async Task RetireAsync()
    {AdvanceAcceptedSubmittedWorldRevision();CurrentFrame=null;CurrentWorld=null;CurrentPasses=[];GeometryFrame=null;GeometryWorld=null;GeometryPasses=[];preparedLatePackets=null;await Assembler.DisposeAsync();await Images.DisposeAsync();await Samplers.DisposeAsync();
        await Resources.DisposeAsync();await Ggx.DisposeAsync();await Program.DisposeAsync();}
}
