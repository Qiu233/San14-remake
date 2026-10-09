using System.Buffers.Binary;
using San14.Data;

/// <summary>Actual five-root World session with ordered source510/PS383
/// submissions. Auxiliary and final stages can consume its first pass's shared
/// View/Camera; each late pass retains its own independent modifier and constants.</summary>
public sealed partial class OriginalMapSourceCreatedWorldNativeSession : IAsyncDisposable
{
    public OriginalDeferredPixelProgram Program { get; }
    public OriginalGgxAmbientLut Ggx { get; }
    public OriginalNativeDeferredResources Resources { get; }
    public OriginalMapSelectedSourceSamplerOwner Samplers { get; }
    public OriginalMapSelectedSourceImageOwner Images { get; }
    public OriginalDeferredSelected383PluginOwner Plugins { get; }
    public OriginalMapSourceCreatedWorldDeferredFrameAssembler Assembler { get; }
    public OriginalNativeMapModelPopulationRenderer Population => Assembler.Population;
    public OriginalNativeMapArmyPopulationRenderer Army { get; }
    public OriginalMapLightingBinding Binding { get; }
    public OriginalMapSourceCreatedNativeSessionPreparedFrame? CurrentFrame { get; private set; }
    public OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame? CurrentWorld { get; private set; }
    public IReadOnlyList<OriginalMapSourceCreatedNativeSessionPreparedFrame> CurrentPasses { get; private set; }=[];
    public OriginalMapSourceCreatedNativeSessionPreparedFrame? GeometryFrame { get; private set; }
    public OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame? GeometryWorld { get; private set; }
    public IReadOnlyList<OriginalMapSourceCreatedNativeSessionPreparedFrame> GeometryPasses { get; private set; }=[];
    public OriginalNativeMapArmyPreparedFrame? CurrentArmyCpuFrame { get; private set; }
    public IReadOnlyList<OriginalNativeMapStageTiming> LastBeforeFrameTimings { get; private set; }=[];
    private IReadOnlyList<OriginalNativeDeferredPacket>? preparedLatePackets;
    private OriginalNativeMapWorldDeferredSubmittedFrame? acceptedLateSubmission;
    private OriginalNativeDeferredPacket[]? appliedLatePackets;
    private readonly LoadedOriginalDeferredSamplerNativeSource native;
    private readonly LoadedOriginalSkyAmbientResources skySource;
    private readonly OriginalMapCommonViewFilterSource filterSource;
    private readonly Func<OriginalNativeMapFrameContext,OriginalNativeMapArmyPreparedFrame,
        OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame> current;
    private readonly Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,OriginalNativeMapModelPopulationAcceptedFrame,
        Task<OriginalMapSourceCreatedNativeSessionGpuInputs>> gpu;
    private readonly Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,OriginalSkyDirectionalAmbientDeviceBinding> ambient;
    private readonly object lifetime=new();
    private bool retiring;
    private Task? disposal;

    private OriginalMapSourceCreatedWorldNativeSession(OriginalDeferredPixelProgram program,OriginalGgxAmbientLut ggx,
        OriginalNativeDeferredResources resources,OriginalMapSelectedSourceSamplerOwner samplers,
        OriginalMapSelectedSourceImageOwner images,OriginalMapSourceCreatedWorldDeferredFrameAssembler assembler,
        OriginalNativeMapArmyPopulationRenderer army,OriginalDeferredSelected383PluginOwner plugins,
        LoadedOriginalDeferredSamplerNativeSource native,LoadedOriginalSkyAmbientResources skySource,
        OriginalMapCommonViewFilterSource filterSource,OriginalMapLightingBinding binding,
        Func<OriginalNativeMapFrameContext,OriginalNativeMapArmyPreparedFrame,OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame> current,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,OriginalNativeMapModelPopulationAcceptedFrame,Task<OriginalMapSourceCreatedNativeSessionGpuInputs>> gpu,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,OriginalSkyDirectionalAmbientDeviceBinding> ambient)
        =>(Program,Ggx,Resources,Samplers,Images,Assembler,Army,Plugins,this.native,this.skySource,this.filterSource,Binding,this.current,this.gpu,this.ambient)=
          (program,ggx,resources,samplers,images,assembler,army,plugins,native,skySource,filterSource,binding,current,gpu,ambient);

    public static OriginalMapLightingBinding CreateExpectedBinding(LoadedOriginalDeferredTexturePlan plan,string view,string camera,string light)
    {
        var actual=OriginalMapSourceCreatedNativeSession.CreateExpectedDefault1022Binding(plan,view,camera,light);
        return actual with {DeferredCombination=510};
    }

    public static async Task<OriginalMapSourceCreatedWorldNativeSession> BuildAsync(OriginalGameRepository repo,Vkd3dShaderCompiler compiler,
        OriginalMapSourceCreatedDirectionalLightingDeviceOwner lightOwner,OriginalNativeMapModelPopulationRenderer population,
        OriginalNativeMapArmyPopulationRenderer army,OriginalMapMaterialCachePrepared materials,
        OriginalMapSourceCreatedF103CurrentFrame bootstrap,
        Func<OriginalNativeMapFrameContext,OriginalNativeMapArmyPreparedFrame,OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame> current,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,OriginalNativeMapModelPopulationAcceptedFrame,Task<OriginalMapSourceCreatedNativeSessionGpuInputs>> gpu,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,OriginalSkyDirectionalAmbientDeviceBinding> ambient)
    {
        ArgumentNullException.ThrowIfNull(repo);ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(current);ArgumentNullException.ThrowIfNull(gpu);ArgumentNullException.ThrowIfNull(ambient);
        var selected=bootstrap.Selection;selected.Validate();
        var source=lightOwner.Sources.Single(s=>s.Identity==bootstrap.Lighting.SourceIdentity);
        var pairs=OriginalDeferredSourceNull2D.FromFreshDirectionalConstruction(source.Construction,
            OriginalDeferredImageStateScope.FreshSourceConstructionNoAssignments);
        var program=await OriginalDeferredPixelProgram.BuildAsync(selected,compiler,pairs);
        OriginalGgxAmbientLut? ggx=null;OriginalNativeDeferredResources? resources=null;
        OriginalMapSelectedSourceSamplerOwner? samplers=null;OriginalMapSelectedSourceImageOwner? images=null;
        OriginalMapSourceCreatedWorldDeferredFrameAssembler? assembler=null;
        try
        {
            var native=OriginalDeferredSamplerSources.Load(repo.SourceDirectory,selected.Camera.ExecutableLocale);
            var sky=OriginalSkyAmbientResources.Load(repo,repo.LoadObjects("system",0xDAB638B2),0x910603AC,
                repo.LoadObjects("system",0x718CCB8B),repo.LoadObjects("system",0x93FB3280));
            ggx=await OriginalGgxAmbientLut.BuildAsync(OriginalEmbeddedGgxShader.Load(selected.Camera.ExecutableLocale),compiler);
            resources=await OriginalNativeDeferredResources.BuildSelectedAsync(program,materials.Table,ggx);
            var imagePlan=OriginalMapSelectedSourceImagePlan.Read(selected);
            var filters=OriginalMapCommonViewFilterSource.LoadGameDefault(repo);
            var transparency=PrepareTransparency(bootstrap,filters);
            var samplerPlan=PrepareSamplers(program,native,sky,imagePlan,transparency.Selection,pairs);
            samplers=await OriginalMapSelectedSourceSamplerOwner.BuildAsync(samplerPlan);images=OriginalMapSelectedSourceImageOwner.FromSource(imagePlan);
            var provider=new OriginalMapSourceCreatedDirectionalDeferredProvider(program,lightOwner,source);
            assembler=new(provider,resources,samplers,images,materials,population);
            var plugins=OriginalDeferredSelected383PluginOwner.CreateAfterSuccessfulGraphicsInitialization(
                OriginalDeferredSamplerSources.LoadSelected767PluginSource(repo.SourceDirectory,selected.Camera.ExecutableLocale),selected.SourcePlan);
            return new(program,ggx,resources,samplers,images,assembler,army,plugins,native,sky,filters,bootstrap.Lighting.Binding,current,gpu,ambient);
        }
        catch
        {
            if(assembler is not null)await assembler.DisposeAsync();
            if(images is not null)await images.DisposeAsync();if(samplers is not null)await samplers.DisposeAsync();
            if(resources is not null)await resources.DisposeAsync();if(ggx is not null)await ggx.DisposeAsync();await program.DisposeAsync();throw;
        }
    }

    public OriginalNativeMapSceneStages Stages(OriginalNativeMapSceneStages? preceding=null,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,Task>? afterCurrentPrepared=null,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,OriginalNativeMapModelPopulationRenderer,Task>? submitStaticGeometry=null,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,OriginalNativeMapArmyPreparedFrame,Task>? submitWorldGeometryBeforeTerrain=null)
        =>(preceding??new OriginalNativeMapSceneStages()) with
        {
            BeforeFrame=async context=>
            {
                var watch=System.Diagnostics.Stopwatch.StartNew();
                var timings=new List<OriginalNativeMapStageTiming>(5);
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

    private void PrepareCurrent(OriginalNativeMapFrameContext context,OriginalNativeMapArmyPreparedFrame army)
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

    private IReadOnlyList<OriginalMapSourceCreatedNativeSessionPreparedFrame> PreparePasses(
        OriginalNativeMapFrameContext context, OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame world)
    {
        var frames=world.World.Passes.Select((p,index)=>
        {
            var f=p.Frame;f.Feature.ValidateFrame(f.Camera,f.Lighting);Program.ValidateCompatibleCurrentSelection(f.Selection);
            if(f.Lighting.SceneInputs.Camera!=context.PreparedCamera||f.Lighting.Binding!=Binding)
                throw new InvalidDataException("World pass differs from the accepted geometry Camera or primary owner.");
            var transparency=PrepareTransparency(f,filterSource);
            var proofs=Assembler.Lighting.CaptureCurrentSourceNullProofs(f.Selection);
            var plan=PrepareSamplers(Program,native,skySource,OriginalMapSelectedSourceImagePlan.Read(f.Selection),transparency.Selection,proofs);
            _=Samplers.GetBindings(plan);
            return new OriginalMapSourceCreatedNativeSessionPreparedFrame(context,f.Camera.CopyAcceptedSnapshot(),f.Lighting,f.Feature,f.Selection,plan)
                {PreparedSourceFrame=f,GeometrySourceFrame=world.GeometryWorld.Passes[index].Frame};
        }).ToArray();
        return Array.AsReadOnly(frames);
    }

    /// <summary>Consume the real later CPU writer on the same geometry
    /// roster. Existing admissions and device receipts keep their early Q.</summary>
    public void PrepareCurrentShading(OriginalNativeMapFrameContext context,
        OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame actualPostGeometry)
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
            OriginalWorldGeometryShadingPhase.RequireSameGeometrySnapshot(
                geometry.World.Passes[i].Frame,actualPostGeometry.World.Passes[i].Frame);
        var passes=PreparePasses(context,actualPostGeometry);
        CurrentWorld=actualPostGeometry;CurrentPasses=passes;CurrentFrame=passes[0];preparedLatePackets=null;
    }

    public async Task<IReadOnlyList<OriginalNativeDeferredPacket>> PrepareOrderedDeferredAsync(OriginalNativeMapFrameContext context)
    {
        var first=RequireCurrent(context);var population=Population.LastAcceptedFrame;
        if(population is null||!ReferenceEquals(population.Context,context)||Army.LastAcceptedFrame is not { } army||
            !ReferenceEquals(army.Prepared.Context,context)||!ReferenceEquals(population.Frame.Visibility,first.CpuPopulation!.Visibility))
            throw new InvalidDataException("World session needs the actual current model and Army submissions that supplied its counters.");
        var produced=await gpu(first,population);var packets=new List<OriginalNativeDeferredPacket>();
        foreach(var frame in CurrentPasses)
        {
            var plugin=Plugins.Prepare(frame.Selection,frame.Lighting);
            var additional=plugin.Complete(produced.Refraction,produced.TranslucencyViewport,produced.Projection,produced.CameraProducerProvenance);
            var constants=OriginalDeferredSelected383Constants.Create(frame.Selection,frame.Lighting,additional);
            var transparency=PrepareTransparency(frame.PreparedSourceFrame!,filterSource);
            var geometry=OriginalMapSourceCreatedDeferredFrameInputs.FromAcceptedPopulation(population,produced.TerrainMaterials,
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
    public void RequireSubmittedWorld(OriginalNativeMapWorldDeferredSubmittedFrame submission)
        =>_=RequireSubmittedWorldAndReadAppliedPackets(submission);
    private OriginalNativeDeferredPacket[] RequireSubmittedWorldAndReadAppliedPackets(OriginalNativeMapWorldDeferredSubmittedFrame submission)
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

    private OriginalMapSourceCreatedNativeSessionPreparedFrame RequireCurrent(OriginalNativeMapFrameContext context)
        =>CurrentFrame is { } frame&&ReferenceEquals(frame.Context,context)?frame:
            throw new InvalidDataException("Current World session preparation is unavailable.");

    private static OriginalMapSelectedSourceSamplerPlan PrepareSamplers(OriginalDeferredPixelProgram program,
        LoadedOriginalDeferredSamplerNativeSource native,LoadedOriginalSkyAmbientResources sky,OriginalMapSelectedSourceImagePlan images,
        OriginalDeferredTransparencyImageSelection transparency,IReadOnlyList<OriginalDeferredSourceNull2D> actualCurrentPairs)
    {
        var proofs=program.SelectedSourceNullSpecialization??throw new InvalidDataException("World program requires its actual Directional Hatching proof.");
        var keys=new List<OriginalMapSelectedSamplerSourceInput>();
        foreach(var target in new[]{images.GraphTargets.Single(t=>t.Role=="RefractionDepth"),images.SelectSpecular(transparency),
            images.GraphTargets.Single(t=>t.Role=="BlurredDepth")})
            keys.Add(OriginalMapSelectedSourceSamplerPlan.FromRenderTexture(images,native,target.EncodedParameter,target.Texture));
        keys.Add(OriginalMapSelectedSourceSamplerPlan.FromProducerKey(images,native,27,sky.Resource.Source,
            "object/910603AC/Q80/original-CubeTexture-v28",OriginalDeferredSamplerSources.AmbientSpecularKey(
                OriginalDeferredSamplerSources.CubeTextureKey(native,sky.SpecularTargets[0].LevelNum))));
        return OriginalMapSelectedSourceSamplerPlan.ResolveWithCurrentDirectionalProofs(images,native,
            program.OriginalTranslation,keys,proofs,actualCurrentPairs);
    }

    private static (OriginalDeferredAuxiliaryCameraFilterSource Camera,OriginalMapCommonViewFilterCurrent Common,
        OriginalDeferredTransparencyImageSelection Selection) PrepareTransparency(
        OriginalMapSourceCreatedF103CurrentFrame frame,OriginalMapCommonViewFilterSource source)
    {
        if(frame.Common.Scope!=OriginalMapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication)
            throw new NotSupportedException("World transparency requires its actual fresh source Common filter owner.");
        var main=frame.Lighting.AcceptedInputs.PresentSeasonCamera?.AcceptedSeasonCamera.Inputs.MainFrame
            ??throw new InvalidDataException("World transparency needs the actual current Main/season source.");
        var camera=OriginalDeferredAuxiliaryCameraFilterSource.CaptureSourceLoadedMain(main,
            OriginalAuxiliaryCameraFilterScope.SourceLoadedCameraNoLaterFilterWriter);
        var common=source.PrepareAppliedCurrent(frame.FrameNumber,frame.Common.CommonTableOwnerIdentity,frame.Lighting.Binding.ViewIdentity);
        return (camera,common,camera.ReadTransparencySelection(
            frame.Common.CommonViewResource240Present?common.TransparencyResolution8C:null));
    }
    public ValueTask DisposeAsync(){lock(lifetime){retiring=true;return new(disposal??=RetireAsync());}}
    private async Task RetireAsync()
    {AdvanceAcceptedSubmittedWorldRevision();CurrentFrame=null;CurrentWorld=null;CurrentPasses=[];GeometryFrame=null;GeometryWorld=null;GeometryPasses=[];preparedLatePackets=null;await Assembler.DisposeAsync();await Images.DisposeAsync();await Samplers.DisposeAsync();
        await Resources.DisposeAsync();await Ggx.DisposeAsync();await Program.DisposeAsync();}
}
