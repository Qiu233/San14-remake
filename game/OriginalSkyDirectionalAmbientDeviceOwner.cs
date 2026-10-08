using Godot;
using San14.Data;
using System.Security.Cryptography;

public sealed record OriginalSkyDirectionalDeviceProducers(OriginalSkyStarProducer Stars,
    OriginalSkyCloudColorProducer Cloud);

/// <summary>The exact currently selected original Q80 wrapper. Allocation and
/// generated contents are separate facts: Native Sky168 does not test the
/// backend's completed-generation flag before selecting a wrapper.</summary>
public sealed record OriginalSkyDirectionalAmbientDeviceBinding(OriginalCubeRenderTextureDevice Cube,
    OriginalDeferredDeviceBinding Texture,OriginalDeferredDeviceBinding Sampler,
    OriginalSkyCpuState168Snapshot State,bool HasCompleteGeneratedContents,string SourceProvenance);

public sealed record OriginalSkyDirectionalDeviceFrame(OriginalSkySceneSourceFramePacket Source,
    OriginalSkySceneFrameUpdateResult? Work,OriginalSkyCpuState168Snapshot State,
    OriginalSkyCpuState168CompletionPlan Completion);

/// <summary>Actual GPU preparation of four original wrappers. This is an
/// explicit backend initialization, not a native startup/Clock/Host receipt.</summary>
public sealed class OriginalSkyDirectionalBackendTargetPreparation
{
    internal OriginalSkyDirectionalBackendTargetPreparation(OriginalSkySceneSourceFramePacket source,
        IReadOnlyList<OriginalSkySceneFrameUpdateResult> generations,uint before,uint after,string provenance)
    {Source=source;Generations=generations;State168Before=before;State168After=after;SourceProvenance=provenance;}
    public OriginalSkySceneSourceFramePacket Source { get; }
    public IReadOnlyList<OriginalSkySceneFrameUpdateResult> Generations { get; }
    public uint State168Before { get; }
    public uint State168After { get; }
    public uint BlendGenerationIndex=>0;
    public float BlendInterpolation=>0;
    public string SourceProvenance { get; }
}

/// <summary>Independent actual Directional group80 Sky/WORK device owner.
/// No HDRI construction identity, static5A fallback or inferred output slot is
/// used. Optional source producers belong to this scene's real WORK resource.</summary>
public sealed partial class OriginalSkyDirectionalAmbientDeviceOwner : IAsyncDisposable
{
    private readonly OriginalGameRepository repository;
    private readonly LoadedShaderContainer shader;
    private readonly LoadedOriginalSkyDirectionalAtmosphereNativeSource atmosphere;
    private readonly OriginalSkySceneTextures textures;
    private readonly OriginalSkySceneFrameUpdater updater;
    private readonly OriginalSkyDirectionalDeviceProducers optional;
    private readonly OriginalSkyCpuState168Owner state;
    private readonly byte[] ambientSamplerKey,ambientSampler;
    private readonly Rid samplerRid;
    private readonly bool[] generated=new bool[4];
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object lifetimeGate=new();
    private Task? disposal;
    private volatile bool retiring;
    private volatile Exception? sourceFault;
    private OriginalSkySceneSourceFramePacket? preparedCurrent;
    public OriginalSkyScene Scene { get; }
    public OriginalSkySceneSourceFramePacket InitialSourceFrame { get; }
    public OriginalMapLightingBinding Binding { get; }
    public string OwnedSkyIdentity => state.OwnedSkyIdentity;
    public int DesiredAtmosphereCombination { get; }
    public int DesiredAtmospherePixelProgram { get; }
    public int ActualContributionCombination => Scene.Selection.Sky2Combination;
    public int ActualContributionPixelProgram { get; }
    public OriginalSkyDirectionalDesiredSelectionReceipt DesiredSelectionReceipt { get; private set; }
    public OriginalSkyAmbientCurrentFilterSelection CurrentFilterSelection { get; private set; }
    public bool RequiresRetirement => sourceFault is not null;
    public IReadOnlyList<OriginalNativeMapStageTiming> LastCurrentTimings { get; private set; } =
        Array.Empty<OriginalNativeMapStageTiming>();
    private OriginalSkyDirectionalAmbientDeviceOwner(OriginalGameRepository repository,
        LoadedShaderContainer shader,LoadedOriginalSkyDirectionalAtmosphereNativeSource atmosphere,
        OriginalSkyScene scene,OriginalSkySceneTextures textures,OriginalSkySceneFrameUpdater updater,
        OriginalSkyDirectionalDeviceProducers optional,OriginalSkyCpuState168Owner state,
        OriginalMapLightingBinding binding,OriginalSkySceneSourceFramePacket initialFrame,OriginalSkyDirectionalDesiredSelectionReceipt desiredSelection,
        OriginalSkyAmbientCurrentFilterSelection filterSelection,byte[] key,byte[] sampler,Rid samplerRid)
    {this.repository=repository;this.shader=shader;this.atmosphere=atmosphere;Scene=scene;this.textures=textures;
      this.updater=updater;this.optional=optional;this.state=state;Binding=binding;InitialSourceFrame=initialFrame;preparedCurrent=initialFrame;
      DesiredSelectionReceipt=desiredSelection;DesiredAtmosphereCombination=desiredSelection.GetSelection(true).Combination.Index;
      CurrentFilterSelection=filterSelection;
      DesiredAtmospherePixelProgram=Pixel(desiredSelection.GetSelection(true));ActualContributionPixelProgram=Pixel(desiredSelection.GetSelection(false));
      ambientSamplerKey=key;ambientSampler=sampler;this.samplerRid=samplerRid;}

    public static async Task<OriginalSkyDirectionalAmbientDeviceOwner> BuildAsync(
        OriginalGameRepository repository,string locale,Vkd3dShaderCompiler compiler,
        OriginalSkyDirectionalSelectedLightSource initialSource,OriginalSkyAppliedSourceStageFrame initialStages,
        uint actualSourceColorWriteMask,string ownedSkyIdentity,string actualClock254OwnerIdentity,
        Func<OriginalSkyScene,OriginalSkySceneSourceFramePacket,Task<OriginalSkyDirectionalDeviceProducers>> producerFactory,
        bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(initialSource);ArgumentNullException.ThrowIfNull(producerFactory);
        if(actualSourceColorWriteMask>15)throw new InvalidDataException("Directional Sky needs the actual original color write mask.");
        var present=RequireSourceStages(initialSource,initialStages);
        if(present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale!=locale)
            throw new InvalidDataException("Directional Sky executable locale differs from its actual selected Camera.");
        var ambient=OriginalSkyAmbientResources.Load(repository,repository.LoadObjects("system",0xDAB638B2),0x910603AC,
            repository.LoadObjects("system",0x718CCB8B),repository.LoadObjects("system",0x93FB3280));
        var filterSelection=OriginalSkyAmbientCurrentFilterSelection.CaptureCurrent(ambient.Shader,present);
        var shader=repository.LoadShaderContainer("system",0x340AC335);
        var nativeAtmosphere=OriginalSkyDirectionalAtmosphereNativeSource.Load(repository,locale,OriginalNativeLogDispatch.Sse);
        var package=present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage;
        var nullMoon=OriginalSky2SourceNull2D.FromAppliedSeasonOnFreshShader(package,OriginalSkySamplingRole.Moon,
            OriginalSkyTextureStateScope.AppliedSourceSeasonOnFreshShader);
        var nullMilky=OriginalSky2SourceNull2D.FromAppliedSeasonOnFreshShader(package,OriginalSkySamplingRole.MilkyWay,
            OriginalSkyTextureStateScope.AppliedSourceSeasonOnFreshShader);
        initialSource.PrepareLights(shader,nullMoon);
        var sourceFrame=initialSource.PrepareSourceSelectedSeasonFrame(ambient.SpecularTargets[0],nativeAtmosphere,2,shader,initialStages);
        var prepared=OriginalSkySceneFramePreparation.PrepareFreshDirectionalFullyWritten(sourceFrame.SourceFrame,initialSource);
        var desiredSelection=CaptureDesiredSelection(shader,initialSource,sourceFrame);
        var desired=desiredSelection.GetSelection(false);var atmosphereSelection=desiredSelection.GetSelection(true);
        RequireSelectedFamily(desired,atmosphereSelection);
        var selections=OriginalStaticTextureResources.Load(repository,ambient.Resource,"system",[0xF81F0562,0xF8ED83E3,0xF9BC0264])
            .Select((s,i)=>OriginalSkySceneTextureSelection.FromStatic("source-sta"+i,s,10)).ToList();
        var earth=OriginalSkySeasonPackageTextures.LoadEarth(repository,package);
        selections.Add(OriginalSkySceneTextureSelection.FromSampling(earth));
        var textures=new OriginalSkySceneTextures(selections);
        var samplers=OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale);
        byte[] key=OriginalDeferredSamplerSources.AmbientSpecularKey(OriginalDeferredSamplerSources.CubeTextureKey(samplers,ambient.SpecularTargets[0].LevelNum));
        byte[] sampler=OriginalDeferredSamplerSources.DecodeKey(samplers,key).EncodeD3D11Descriptor();
        OriginalSkyScene? scene=null;OriginalSkyDirectionalDeviceProducers? optional=null;Rid samplerRid=default;
        try
        {
            var definitions=shader.ReadResourceDefinitions(Pixel(desired));
            if(!definitions.Bindings.Where(b=>b.InputType==2).Select(b=>b.BindPoint).Order().SequenceEqual(new uint[]{1,4,5})||
                definitions.Buffers.Single().ByteSize!=336)
                throw new InvalidDataException("Actual Directional source-null Contribution texture interface changed.");
            // The selected PS6/15 consumes Ttable1 and null Milky4/Moon5. Earth and all
            // three real STA selections stay owned for their source roles.
            var inputs=new[]{await textures.GetRetainedInputAsync(1,"source-sta1")};
            var selectedFilter=filterSelection.Selection;
            var selection=new OriginalSkySceneProgramSelection(desired.Combination.Index,OriginalSky2Operation.Contribution,
                selectedFilter.CopyCombination.Index,selectedFilter.FilterCombination.Index,selectedFilter.BlendCombination.Index)
                {Sky2ColorWriteMask=actualSourceColorWriteMask,FreshNullTextures=[nullMilky,nullMoon]};
            scene=await OriginalSkyScene.BuildAsync(ambient,shader,selection,compiler,prepared.DrawPackets,
                new byte[shader.ReadResourceDefinitions(Vertex(desired)).Buffers.Single().ByteSize],
                new byte[definitions.Buffers.Single().ByteSize],inputs,sampler,sampler,
                OriginalNativeLogTables.Load(repository.SourceDirectory,locale),OriginalNativeLogDispatch.AvxFma,allowReadback);
            optional=await producerFactory(scene,sourceFrame);
            ArgumentNullException.ThrowIfNull(optional);ArgumentNullException.ThrowIfNull(optional.Stars);ArgumentNullException.ThrowIfNull(optional.Cloud);
            var updater=new OriginalSkySceneFrameUpdater(scene,prepared.Faces,optional.Stars,optional.Cloud);
            samplerRid=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>OriginalDeviceSampler.Create(rd,sampler));
            if(!samplerRid.IsValid)throw new InvalidDataException("Device rejected the actual Directional ambient source sampler.");
            var state=OriginalSkyCpuState168Owner.CreateFresh(repository,locale,ownedSkyIdentity,actualClock254OwnerIdentity);
            return new(repository,shader,nativeAtmosphere,scene,textures,updater,optional,state,
                initialSource.AcceptedLighting.Binding,sourceFrame,desiredSelection,filterSelection,key,sampler,samplerRid);
        }
        catch(Exception buildFailure)
        {
            var partial=new OriginalNativeMapBorrowerRetirementPackage(
                ownedSkyIdentity+"/Directional-Sky-partial/"+Guid.NewGuid().ToString("N"),
                buildFailure is OriginalNativeMapBorrowerConstructionException nested?nested.RetainedPackage:null,
                optional?.Cloud,optional?.Stars,new OriginalNativeMapOwnedSamplerRetirement(samplerRid),scene,textures);
            try{await partial.DisposeAsync();}
            catch(Exception retirementFailure){partial.RetainFailure();throw new OriginalNativeMapBorrowerConstructionException(buildFailure,retirementFailure,partial);}
            throw;
        }
    }

    /// <summary>Explicit backend startup only. Reuses one factory-created
    /// source/optional packet through three genuine 6+1+36 WORK generations
    /// and the original blend program. No168, Clock, time or Host writer is
    /// called. The native current selection remains unchanged.</summary>
    public async Task<OriginalSkyDirectionalBackendTargetPreparation> PrepareInitialGeneratedTargetsAsync(
        OriginalSkyDirectionalSelectedLightSource source,OriginalSkyAppliedSourceStageFrame stages,
        OriginalSkySceneSourceFramePacket actualSameSourceFrame,OriginalSkySceneProducerFrame actualProducerFrame)
    {
        await gate.WaitAsync();bool started=false;
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(sourceFault is not null)throw new InvalidOperationException("Failed Sky owner must be retired/recreated.",sourceFault);
            var present=RequireSourceStages(source,stages);CurrentFilterSelection.ValidateCurrent(present);
            CurrentFilterSelection.RequireSameProgramSampleCount(Scene.FilterProgram.SourceSampleSelection);
            if(!ReferenceEquals(actualSameSourceFrame,InitialSourceFrame)||!ReferenceEquals(preparedCurrent,actualSameSourceFrame)||
                source.AcceptedLighting.Binding!=Binding||actualSameSourceFrame.Binding!=Binding||
                actualSameSourceFrame.FrameNumber!=source.AcceptedLighting.FrameNumber||
                actualProducerFrame.Sky2Enabled!=stages.Sky2Enabled||actualProducerFrame.Scene is not null||actualProducerFrame.Contribution is not null||
                actualProducerFrame.StarFaces?.Count!=6||actualProducerFrame.StarGate is null||actualProducerFrame.CloudFaces?.Count!=6)
                throw new InvalidDataException("Backend startup requires this exact untouched actual factory source and all actual optional packets.");
            source.ValidatePreparedLights(actualSameSourceFrame.SourceFrame.Lights);
            _=OriginalSkySceneFramePreparation.PrepareFreshDirectionalFullyWritten(actualSameSourceFrame.SourceFrame,source);
            var before=state.CaptureForConsumer(actualSameSourceFrame.FrameNumber);
            var progress=await Scene.GetProgressAsync();
            if(before.State168!=0||generated.Any(x=>x)||progress.Phase!=OriginalSkyScenePhase.Idle||progress.CompletedGenerations!=0)
                throw new InvalidOperationException("Backend target startup must precede every native/source WORK call and run once on fresh wrappers.");
            started=true;
            var runs=new List<OriginalSkySceneFrameUpdateResult>();
            for(uint slot=0;slot<3;slot++)
            {
                var run=await updater.SubmitFrameAsync(slot,actualSameSourceFrame.SourceFrame,actualProducerFrame,43);
                if(run.CompletedSpecular is null||run.Progress.Phase!=OriginalSkyScenePhase.Idle||run.Progress.WorkUnits!=43||
                    run.Progress.LastCompletedIndex!=slot||run.Progress.CompletedGenerations!=slot+1)
                    throw new InvalidDataException("Backend startup's actual original WORK generation did not complete.");
                generated[slot]=true;runs.Add(run);
            }
            uint levels=Scene.SpecularTargets[3].Source.LevelNum;
            var surfaces=Scene.SpecularTargets[3].Surfaces.Select(s=>new OriginalSkySceneBlendSurfaceInputs(s.SurfaceId,
                OriginalSkyCubeBlendFaceMath.EncodeNativeFaceMatrix(s.SurfaceId/levels),ambientSampler,ambientSampler)).ToArray();
            await Scene.BlendAsync(0,0,surfaces);generated[3]=true;
            var after=state.CaptureForConsumer(actualSameSourceFrame.FrameNumber);
            before.ValidateCurrent(OwnedSkyIdentity,actualSameSourceFrame.FrameNumber);
            if(after.State168!=before.State168||after.SelectedSpecularIndex!=before.SelectedSpecularIndex)
                throw new InvalidDataException("Backend GPU target preparation changed native CPU168 selection.");
            return new(actualSameSourceFrame,runs.AsReadOnly(),before.State168,after.State168,
                actualSameSourceFrame.SourceProvenance+";backend-target-initialization-only;three-original-WORK6+1+36;"+
                "original-Blend0/t0;all-four-actual-declared-wrappers-generated;native-startup-identity-unproved;no168/Clock/time/Host-write");
        }
        catch(Exception failure){if(started)sourceFault=failure;throw;}
        finally{gate.Release();}
    }

    /// <summary>Complete-generation backend scope (43 original work units).
    /// Its actual acknowledged return drives168; the budget is explicitly a
    /// backend batch, separate from native Clock254 scheduling policy.</summary>
    public async Task<OriginalSkyDirectionalDeviceFrame> SubmitCompleteCurrentAsync(
        OriginalSkyDirectionalSelectedLightSource source,OriginalSkyAppliedSourceStageFrame stages,
        OriginalSkySceneProducerFrame producers,OriginalSkyAmbientSourceEnableInputs enable,
        OriginalSkyAmbientCurrentClock254 actualClock254,OriginalSkySceneSourceFramePacket? preparedSource=null)
    {
        await gate.WaitAsync();
        bool callStarted=false;
        try
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var timings=new List<OriginalNativeMapStageTiming>();
            double previous=0;
            void Mark(string name)
            {
                double elapsed=timer.Elapsed.TotalMilliseconds;
                timings.Add(new(name,elapsed-previous,elapsed));previous=elapsed;
            }
            ObjectDisposedException.ThrowIf(retiring,this);
            if(sourceFault is not null)throw new InvalidOperationException("Failed Directional Sky WORK must be retired and recreated; pending168 is retained.",sourceFault);
            var present=RequireSourceStages(source,stages);
            if(source.AcceptedLighting.Binding!=Binding||enable.FrameNumber!=source.AcceptedLighting.FrameNumber||
                producers.Sky2Enabled!=stages.Sky2Enabled||producers.Scene is not null||producers.Contribution is not null||
                producers.StarFaces?.Count!=6||producers.StarGate is null||producers.CloudFaces?.Count!=6)
                throw new InvalidDataException("Directional GPU WORK requires its same owned View, actual optional source packets and typed stage gates.");
            if(preparedSource is not null&&!ReferenceEquals(preparedCurrent,preparedSource))
                throw new InvalidDataException("Directional optional producers must use this device owner's actual prepared source frame.");
            var frame=preparedSource??PrepareCurrentSourceFrame(source,stages);
            CurrentFilterSelection.ValidateCurrent(present);
            CurrentFilterSelection.RequireSameProgramSampleCount(Scene.FilterProgram.SourceSampleSelection);
            source.ValidatePreparedLights(frame.SourceFrame.Lights);
            if(frame.FrameNumber!=source.AcceptedLighting.FrameNumber||frame.Binding!=Binding)
                throw new InvalidDataException("Directional optional/current Sky packets belong to different source frames.");
            _=OriginalSkySceneFramePreparation.PrepareFreshDirectionalFullyWritten(frame.SourceFrame,source);
            var call=state.BeginCurrentBackendBatch(present,enable,actualClock254,43,"actual-complete-GPU-batch6+1+36");
            callStarted=true;
            Mark("current-source-and-work-plan");
            OriginalSkySceneFrameUpdateResult? work=null;OriginalSkyCpuState168CompletionPlan completion;
            if(!enable.AllowsWorkInitialization)completion=state.PrepareCompletion(call,new(0,false,0));
            else
            {
                var progress=await Scene.GetProgressAsync();
                if(progress.Phase!=OriginalSkyScenePhase.Idle||call.FirstFlat!=0||call.PreparationSubphase!=0)
                    throw new NotSupportedException("Directional thin adapter currently accepts complete source generations; partial WORK needs its current cache continuation.");
                state.RecordWorkInitialization(call,true,true);
                work=await updater.SubmitFrameAsync(call.GenerationIndex,frame.SourceFrame,producers,43);
                if(work.Progress.Phase!=OriginalSkyScenePhase.Idle||work.Progress.WorkUnits!=43||
                    work.Progress.LastCompletedIndex!=call.GenerationIndex||work.CompletedSpecular is null)
                    throw new InvalidDataException("Actual Directional GPU draws/copy/filter did not return a complete source generation.");
                generated[call.GenerationIndex]=true;
                completion=state.PrepareCompletion(call,new(6,true,36));
            }
            Mark("current-work-submission");
            uint blendReturn=0;
            if(completion.RequiresBlend)
            {
                var indices=completion.BlendIndices;
                if(!generated[indices.InputA]||!generated[indices.InputB])
                    throw new NotSupportedException("Actual native blend selects an original wrapper without generated contents; its initialization content contract must be closed before this backend blend.");
                uint levels=Scene.SpecularTargets[3].Source.LevelNum;
                var surfaces=Scene.SpecularTargets[3].Surfaces.Select(s=>new OriginalSkySceneBlendSurfaceInputs(s.SurfaceId,
                    OriginalSkyCubeBlendFaceMath.EncodeNativeFaceMatrix(s.SurfaceId/levels),ambientSampler,ambientSampler)).ToArray();
                await Scene.BlendAsync(completion.NextGenerationIndex,completion.BlendFraction,surfaces);
                generated[3]=true;blendReturn=6;
            }
            Mark("current-blend-submission");
            var snapshot=state.CompleteCurrentCall(completion,blendReturn);
            Mark("current-state-completion");
            LastCurrentTimings=timings.AsReadOnly();
            return new(frame,work,snapshot,completion);
        }
        catch(Exception failure)
        {
            if(callStarted)sourceFault=failure;
            throw;
        }
        finally{gate.Release();}
    }

    /// <summary>Prepare the actual packet once before current Star/Cloud
    /// writers. Submission may consume that exact private current packet;
    /// it need not clone/reprepare their controller or light receipt.</summary>
    public OriginalSkySceneSourceFramePacket PrepareCurrentSourceFrame(
        OriginalSkyDirectionalSelectedLightSource source,OriginalSkyAppliedSourceStageFrame stages)
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        if(sourceFault is not null)throw new InvalidOperationException("Failed Sky owner must be retired/recreated.",sourceFault);
        var present=RequireSourceStages(source,stages);
        if(source.AcceptedLighting.Binding!=Binding)throw new InvalidDataException("Directional current packet belongs to another producer View/light owner.");
        var filterSelection=OriginalSkyAmbientCurrentFilterSelection.CaptureCurrent(Scene.FilterProgram.Source,present);
        filterSelection.RequireSameProgramSampleCount(Scene.FilterProgram.SourceSampleSelection);
        var package=present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage;
        var nullMoon=OriginalSky2SourceNull2D.FromAppliedSeasonOnFreshShader(package,OriginalSkySamplingRole.Moon,
            OriginalSkyTextureStateScope.AppliedSourceSeasonOnFreshShader);
        _=OriginalSky2SourceNull2D.FromAppliedSeasonOnFreshShader(package,OriginalSkySamplingRole.MilkyWay,
            OriginalSkyTextureStateScope.AppliedSourceSeasonOnFreshShader);
        source.PrepareLights(shader,nullMoon);
        var currentFrame=source.PrepareSourceSelectedSeasonFrame(Scene.SpecularTargets[0].Source,atmosphere,2,shader,stages);
        var desiredSelection=CaptureDesiredSelection(shader,source,currentFrame);
        var desired=desiredSelection.GetSelection(false);var desiredAtmosphere=desiredSelection.GetSelection(true);
        RequireSelectedFamily(desired,desiredAtmosphere);
        if(desired.Combination.Index!=ActualContributionCombination||desiredAtmosphere.Combination.Index!=DesiredAtmosphereCombination||
            Pixel(desired)!=ActualContributionPixelProgram||Pixel(desiredAtmosphere)!=DesiredAtmospherePixelProgram)
            throw new NotSupportedException("Changed Directional source variant needs its own actual shader lifecycle.");
        DesiredSelectionReceipt=desiredSelection;
        CurrentFilterSelection=filterSelection;
        return preparedCurrent=currentFrame;
    }

    /// <summary>Borrow the actual allocated native wrapper, preserving the
    /// distinct generated-content flag. Callers requiring completed pixels
    /// must check that flag; a different completed slot is never substituted.</summary>
    public OriginalSkyDirectionalAmbientDeviceBinding BorrowCurrentQ80Wrapper(
        OriginalDeferredSelected383SkyProjection projection,OriginalSkyCpuState168Snapshot snapshot,
        OriginalDeferredF103SelectedSource selected,OriginalMapSourceCreatedLightingPacket lighting)
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        if(sourceFault is not null)throw new InvalidOperationException("Failed Directional Sky WORK cannot publish Q80; retire and recreate the owner.",sourceFault);
        snapshot.ValidateCurrent(OwnedSkyIdentity,lighting.FrameNumber);
        projection.ValidateCurrent(selected,lighting);
        if(snapshot.RetainsEarlierEnvmap||projection.OwnedSkyIdentity!=OwnedSkyIdentity||
            projection.NativeSkyState168!=snapshot.State168||lighting.Binding.ViewIdentity!=Binding.ViewIdentity||
            lighting.Binding.CameraOwnerIdentity!=Binding.CameraOwnerIdentity)
            throw new InvalidDataException("Directional Q80 binding requires this actual selected source state and View owner.");
        var cube=Scene.SpecularTargets[(int)snapshot.SelectedSpecularIndex];projection.ValidateBorrowedCubeSource(cube.Source);
        string uri=cube.Source.Source.Source.ResourceUri+"@"+cube.Source.Source.Source.DecodedSha256+
            $"#object={cube.Source.Object.ObjectId:X8};actual-Q80-state168={snapshot.State168:X8};allocated-wrapper;generated={generated[snapshot.SelectedSpecularIndex]}";
        return new(cube,new("Texture",projection.PixelRegister,cube.TextureRid,cube.TextureRid,uri),
            new("Sampler",projection.PixelRegister,samplerRid,null,uri+";sampler="+Convert.ToHexString(SHA256.HashData(ambientSampler))),
            snapshot,generated[snapshot.SelectedSpecularIndex],snapshot.SourceProvenance+";"+uri);
    }
    public OriginalSkyCpuState168Snapshot CaptureCurrentState(long consumerFrame)=>state.CaptureForConsumer(consumerFrame);
    public byte[] CopyAmbientSamplerKey()=>ambientSamplerKey.ToArray();
    public byte[] CopyAmbientSamplerDescriptor()=>ambientSampler.ToArray();
    private static OriginalMapSourceCreatedSeasonLightingFrame RequireSourceStages(
        OriginalSkyDirectionalSelectedLightSource source,OriginalSkyAppliedSourceStageFrame stages)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(stages);source.ValidateCurrent();
        var present=source.AcceptedLighting.AcceptedInputs.PresentSeasonCamera??throw new InvalidDataException("Directional Sky needs its actual current seasonal Camera.");
        stages.ValidateCurrent(source.AcceptedLighting,present.AcceptedSeasonCamera.Inputs.Source.Selection,present.CurrentSkyCamera);
        if(stages.ScenePresent||stages.WorkContributionEnabled)
            throw new NotSupportedException("Directional thin adapter closes actual source-null Scene/no-1F2 WORK only.");
        return present;
    }
    private static OriginalSkyDirectionalDesiredSelectionReceipt CaptureDesiredSelection(LoadedShaderContainer shader,
        OriginalSkyDirectionalSelectedLightSource source,OriginalSkySceneSourceFramePacket actualFrame)
    {
        var receipt=source.CaptureDesiredSelection(shader,actualFrame);
        receipt.ValidateAppliedFrame(actualFrame);receipt.ValidateSourceSlotSelector(source.SelectedPacket);
        receipt.ValidateSelectedMoonProgramScope();
        return receipt;
    }
    private static int Pixel(OriginalSky2SelectionResult s)=>s.Combination.Stages.Single(x=>x.StageCode==0&&x.Entry is not null).Entry!.ProgramIndex;
    private static int Vertex(OriginalSky2SelectionResult s)=>s.Combination.Stages.Single(x=>x.StageCode==1&&x.Entry is not null).Entry!.ProgramIndex;
    private static void RequireSelectedFamily(OriginalSky2SelectionResult contribution,OriginalSky2SelectionResult atmosphere)
    {
        var feature=contribution.RendererFeature.Feature.Words;
        bool mode4=feature[6]==4&&feature[7]==1;
        bool mode7=feature[6]==7&&feature[7]==2;
        if(!contribution.RendererFeature.Feature.RawBytes.AsSpan().SequenceEqual(atmosphere.RendererFeature.Feature.RawBytes)||
            !(mode4&&Vertex(contribution)==0&&Vertex(atmosphere)==0&&Pixel(contribution)==6&&(Pixel(atmosphere) is 5 or 11)||
              mode7&&Vertex(contribution)==2&&Vertex(atmosphere)==2&&Pixel(contribution)==15&&Pixel(atmosphere)==14))
            throw new NotSupportedException("Directional WORK requires its source-selected mode4/profile1 or mode7/profile2 nonnull Sun/Moon shader pair.");
    }
    public ValueTask DisposeAsync(){lock(lifetimeGate){retiring=true;return new(disposal??=DisposeCoreAsync());}}
    private async Task DisposeCoreAsync()
    {
        await gate.WaitAsync();
        try
        {
            var package=new OriginalNativeMapBorrowerRetirementPackage(OwnedSkyIdentity+"/Directional-Sky-retire",
                optional.Cloud,optional.Stars,new OriginalNativeMapOwnedSamplerRetirement(samplerRid),Scene,textures);
            try{await package.DisposeAsync();}catch{package.RetainFailure();throw;}
        }
        finally{gate.Release();}
    }
}
