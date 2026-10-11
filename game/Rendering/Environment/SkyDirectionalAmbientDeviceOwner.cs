using Godot;
using San14.Data;
using System.Security.Cryptography;

public sealed record SkyDirectionalDeviceProducers(SkyStarProducer Stars,
    SkyCloudColorProducer Cloud);

/// <summary>The exact currently selected original Q80 wrapper. Allocation and
/// generated contents are separate facts: Native Sky168 does not test the
/// backend's completed-generation flag before selecting a wrapper.</summary>
public sealed record SkyDirectionalAmbientDeviceBinding(CubeRenderTextureDevice Cube,
    DeferredDeviceBinding Texture,DeferredDeviceBinding Sampler,
    SkyCpuState168Snapshot State,bool HasCompleteGeneratedContents,string SourceProvenance);

public sealed record SkyDirectionalDeviceFrame(SkySceneSourceFramePacket Source,
    SkySceneFrameUpdateResult? Work,SkyCpuState168Snapshot State,
    SkyCpuState168CompletionPlan Completion);

/// <summary>Actual GPU preparation of four original wrappers. This is an
/// explicit backend initialization, not a native startup/Clock/Host receipt.</summary>
public sealed class SkyDirectionalBackendTargetPreparation
{
    internal SkyDirectionalBackendTargetPreparation(SkySceneSourceFramePacket source,
        IReadOnlyList<SkySceneFrameUpdateResult> generations,uint before,uint after,string provenance)
    {Source=source;Generations=generations;State168Before=before;State168After=after;SourceProvenance=provenance;}
    public SkySceneSourceFramePacket Source { get; }
    public IReadOnlyList<SkySceneFrameUpdateResult> Generations { get; }
    public uint State168Before { get; }
    public uint State168After { get; }
    public uint BlendGenerationIndex=>0;
    public float BlendInterpolation=>0;
    public string SourceProvenance { get; }
}

/// <summary>Independent actual Directional group80 Sky/WORK device owner.
/// No HDRI construction identity, static5A fallback or inferred output slot is
/// used. Optional source producers belong to this scene's real WORK resource.</summary>
public sealed partial class SkyDirectionalAmbientDeviceOwner : IAsyncDisposable
{
    private readonly GameResourceRepository repository;
    private readonly LoadedShaderContainer shader;
    private readonly LoadedSkyDirectionalAtmosphereNativeSource atmosphere;
    private SkySceneTextures textures;
    private readonly Vkd3dShaderCompiler compiler;
    private Sky2TextureBindings textureBindings;
    private Sky2TextureBindingSnapshot textureSnapshot;
    private readonly SkySceneFrameUpdater updater;
    private readonly SkyDirectionalDeviceProducers optional;
    private readonly SkyCpuState168Owner state;
    private readonly byte[] ambientSamplerKey,ambientSampler;
    private readonly Rid samplerRid;
    private readonly bool[] generated=new bool[4];
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object lifetimeGate=new();
    private Task? disposal;
    private volatile bool retiring;
    private volatile Exception? sourceFault;
    private SkySceneSourceFramePacket? preparedCurrent;
    public SkyScene Scene { get; }
    public SkySceneSourceFramePacket InitialSourceFrame { get; }
    public MapLightingBinding Binding { get; }
    public string OwnedSkyIdentity => state.OwnedSkyIdentity;
    public int DesiredAtmosphereCombination { get; }
    public int DesiredAtmospherePixelProgram { get; }
    public int ActualContributionCombination => Scene.Selection.Sky2Combination;
    public int ActualContributionPixelProgram { get; }
    public SkyDirectionalDesiredSelectionReceipt DesiredSelectionReceipt { get; private set; }
    public SkyAmbientCurrentFilterSelection CurrentFilterSelection { get; private set; }
    public bool RequiresRetirement => sourceFault is not null;
    public IReadOnlyList<NativeMapStageTiming> LastCurrentTimings { get; private set; } =
        Array.Empty<NativeMapStageTiming>();
    private SkyDirectionalAmbientDeviceOwner(GameResourceRepository repository,
        LoadedShaderContainer shader,LoadedSkyDirectionalAtmosphereNativeSource atmosphere,
        SkyScene scene,SkySceneTextures textures,SkySceneFrameUpdater updater,
        SkyDirectionalDeviceProducers optional,SkyCpuState168Owner state,
        MapLightingBinding binding,SkySceneSourceFramePacket initialFrame,SkyDirectionalDesiredSelectionReceipt desiredSelection,
        SkyAmbientCurrentFilterSelection filterSelection,byte[] key,byte[] sampler,Rid samplerRid,
        Sky2TextureBindings textureBindings,Sky2TextureBindingSnapshot textureSnapshot,Vkd3dShaderCompiler compiler)
    {this.compiler=compiler;this.textureBindings=textureBindings;this.textureSnapshot=textureSnapshot;this.repository=repository;this.shader=shader;this.atmosphere=atmosphere;Scene=scene;this.textures=textures;
      this.updater=updater;this.optional=optional;this.state=state;Binding=binding;InitialSourceFrame=initialFrame;preparedCurrent=initialFrame;
      DesiredSelectionReceipt=desiredSelection;DesiredAtmosphereCombination=desiredSelection.GetSelection(true).Combination.Index;
      CurrentFilterSelection=filterSelection;
      DesiredAtmospherePixelProgram=Pixel(desiredSelection.GetSelection(true));ActualContributionPixelProgram=Pixel(desiredSelection.GetSelection(false));
      ambientSamplerKey=key;ambientSampler=sampler;this.samplerRid=samplerRid;}

    public static async Task<SkyDirectionalAmbientDeviceOwner> BuildAsync(
        GameResourceRepository repository,string locale,Vkd3dShaderCompiler compiler,
        SkyDirectionalSelectedLightSource initialSource,SkyAppliedSourceStageFrame initialStages,
        uint actualSourceColorWriteMask,string ownedSkyIdentity,string actualClock254OwnerIdentity,
        Func<SkyScene,SkySceneSourceFramePacket,Task<SkyDirectionalDeviceProducers>> producerFactory,
        bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(initialSource);ArgumentNullException.ThrowIfNull(producerFactory);
        if(actualSourceColorWriteMask>15)throw new InvalidDataException("Directional Sky needs the actual original color write mask.");
        var present=RequireSourceStages(initialSource,initialStages);
        if(present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale!=locale)
            throw new InvalidDataException("Directional Sky executable locale differs from its actual selected Camera.");
        var package=present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage;
        var textureBindings=new Sky2TextureBindings();
        textureBindings.ApplyPackage(SkySeasonPackageTextures.Read(package),reference=>SkySeasonPackageTextures.LoadSampling(repository,reference));
        var textureSnapshot=textureBindings.Capture();
        var ambient=SkyAmbientResources.Load(repository,repository.LoadObjects("system",0xDAB638B2),0x910603AC,
            repository.LoadObjects("system",0x718CCB8B),repository.LoadObjects("system",0x93FB3280));
        var filterSelection=SkyAmbientCurrentFilterSelection.CaptureCurrent(ambient.Shader,present);
        var shader=repository.LoadShaderContainer("system",0x340AC335);
        var nativeAtmosphere=SkyDirectionalAtmosphereNativeSource.Load(repository,locale,NativeLogDispatch.Sse);
        initialSource.PrepareLights(shader,textureSnapshot);
        var sourceFrame=initialSource.PrepareSourceSelectedSeasonFrame(ambient.SpecularTargets[0],nativeAtmosphere,2,shader,initialStages,textureSnapshot);
        var prepared=SkySceneFramePreparation.PrepareFreshDirectionalFullyWritten(sourceFrame.SourceFrame,initialSource);
        var desiredSelection=CaptureDesiredSelection(shader,initialSource,sourceFrame);
        var desired=desiredSelection.GetSelection(false);var atmosphereSelection=desiredSelection.GetSelection(true);
        RequireSelectedFamily(desired,atmosphereSelection);
        var selections=StaticTextureResources.Load(repository,ambient.Resource,"system",[0xF81F0562,0xF8ED83E3,0xF9BC0264])
            .Select((s,i)=>SkySceneTextureSelection.FromStatic("source-sta"+i,s,10)).ToList();
        foreach(var role in new[]{SkySamplingRole.Earth,SkySamplingRole.Moon,SkySamplingRole.MilkyWay})
            if(textureSnapshot.GetTexture(role) is { } assigned)
                selections.Add(SkySceneTextureSelection.FromSampling(assigned));
        var textures=new SkySceneTextures(selections);
        var samplers=RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale);
        byte[] key=RenderSamplerSourceAdapters.AmbientSpecularKey(RenderSamplerSourceAdapters.CubeTextureKey(samplers,ambient.SpecularTargets[0].LevelNum));
        byte[] sampler=RenderSamplerSourceAdapters.DecodeKey(samplers,key).EncodeD3D11Descriptor();
        SkyScene? scene=null;SkyDirectionalDeviceProducers? optional=null;Rid samplerRid=default;
        try
        {
            var definitions=shader.ReadResourceDefinitions(Pixel(desired));
            if(!definitions.Bindings.Where(b=>b.InputType==2).Select(b=>b.BindPoint).Order().SequenceEqual(new uint[]{1,4,5})||
                definitions.Buffers.Single().ByteSize!=336)
                throw new InvalidDataException("Actual Directional Contribution texture interface changed.");
            // The selected PS6/15 consumes Ttable1 plus installed Milky4/Moon5.
            // Earth and all three STA selections retain their own source roles.
            var inputs=new List<Sky2DeviceInput>{await textures.GetRetainedInputAsync(1,"source-sta1")};
            foreach(var role in new[]{SkySamplingRole.MilkyWay,SkySamplingRole.Moon})
                if(textureSnapshot.GetTexture(role) is { } assigned)
                    inputs.Add(await textures.GetInputAsync(assigned.PixelRegister,assigned.SourceName));
            var selectedFilter=filterSelection.Selection;
            var selection=new SkySceneProgramSelection(desired.Combination.Index,Sky2Operation.Contribution,
                selectedFilter.CopyCombination.Index,selectedFilter.FilterCombination.Index,selectedFilter.BlendCombination.Index)
                {Sky2ColorWriteMask=actualSourceColorWriteMask,TextureBindings=textureSnapshot};
            scene=await SkyScene.BuildAsync(ambient,shader,selection,compiler,prepared.DrawPackets,
                new byte[shader.ReadResourceDefinitions(Vertex(desired)).Buffers.Single().ByteSize],
                new byte[definitions.Buffers.Single().ByteSize],inputs,sampler,sampler,
                NativeLogTables.Load(repository.SourceDirectory,locale),NativeLogDispatch.AvxFma,allowReadback);
            optional=await producerFactory(scene,sourceFrame);
            ArgumentNullException.ThrowIfNull(optional);ArgumentNullException.ThrowIfNull(optional.Stars);ArgumentNullException.ThrowIfNull(optional.Cloud);
            var updater=new SkySceneFrameUpdater(scene,prepared.Faces,optional.Stars,optional.Cloud);
            samplerRid=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>DeviceSampler.Create(rd,sampler));
            if(!samplerRid.IsValid)throw new InvalidDataException("Device rejected the actual Directional ambient source sampler.");
            var state=SkyCpuState168Owner.CreateFresh(repository,locale,ownedSkyIdentity,actualClock254OwnerIdentity);
            return new(repository,shader,nativeAtmosphere,scene,textures,updater,optional,state,
                initialSource.AcceptedLighting.Binding,sourceFrame,desiredSelection,filterSelection,key,sampler,samplerRid,textureBindings,textureSnapshot,compiler);
        }
        catch(Exception buildFailure)
        {
            textureBindings.Dispose();
            var partial=new NativeMapBorrowerRetirementPackage(
                ownedSkyIdentity+"/Directional-Sky-partial/"+Guid.NewGuid().ToString("N"),
                buildFailure is NativeMapBorrowerConstructionException nested?nested.RetainedPackage:null,
                optional?.Cloud,optional?.Stars,new NativeMapOwnedSamplerRetirement(samplerRid),scene,textures);
            try{await partial.DisposeAsync();}
            catch(Exception retirementFailure){partial.RetainFailure();throw new NativeMapBorrowerConstructionException(buildFailure,retirementFailure,partial);}
            throw;
        }
    }

    /// <summary>Explicit backend startup only. Reuses one factory-created
    /// source/optional packet through three genuine 6+1+36 WORK generations
    /// and the original blend program. No168, Clock, time or Host writer is
    /// called. The native current selection remains unchanged.</summary>
    public async Task<SkyDirectionalBackendTargetPreparation> PrepareInitialGeneratedTargetsAsync(
        SkyDirectionalSelectedLightSource source,SkyAppliedSourceStageFrame stages,
        SkySceneSourceFramePacket actualSameSourceFrame,SkySceneProducerFrame actualProducerFrame)
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
            RequireTextureFrame(actualSameSourceFrame);
            source.ValidatePreparedLights(actualSameSourceFrame.SourceFrame.Lights);
            var before=state.CaptureForConsumer(actualSameSourceFrame.FrameNumber);
            var progress=await Scene.GetProgressAsync();
            if(before.State168!=0||generated.Any(x=>x)||progress.Phase!=SkyScenePhase.Idle||progress.CompletedGenerations!=0)
                throw new InvalidOperationException("Backend target startup must precede every native/source WORK call and run once on fresh wrappers.");
            started=true;
            var runs=new List<SkySceneFrameUpdateResult>();
            for(uint slot=0;slot<3;slot++)
            {
                var run=await updater.SubmitFrameAsync(slot,actualSameSourceFrame.SourceFrame,actualProducerFrame,43);
                if(run.CompletedSpecular is null||run.Progress.Phase!=SkyScenePhase.Idle||run.Progress.WorkUnits!=43||
                    run.Progress.LastCompletedIndex!=slot||run.Progress.CompletedGenerations!=slot+1)
                    throw new InvalidDataException("Backend startup's actual original WORK generation did not complete.");
                generated[slot]=true;runs.Add(run);
            }
            uint levels=Scene.SpecularTargets[3].Source.LevelNum;
            var surfaces=Scene.SpecularTargets[3].Surfaces.Select(s=>new SkySceneBlendSurfaceInputs(s.SurfaceId,
                SkyCubeBlendFaceMath.EncodeNativeFaceMatrix(s.SurfaceId/levels),ambientSampler,ambientSampler)).ToArray();
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
    public async Task<SkyDirectionalDeviceFrame> SubmitCompleteCurrentAsync(
        SkyDirectionalSelectedLightSource source,SkyAppliedSourceStageFrame stages,
        SkySceneProducerFrame producers,SkyAmbientSourceEnableInputs enable,
        SkyAmbientCurrentClock254 actualClock254,SkySceneSourceFramePacket? preparedSource=null)
    {
        await gate.WaitAsync();
        bool callStarted=false;
        try
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var timings=new List<NativeMapStageTiming>();
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
            RequireTextureFrame(frame);
            CurrentFilterSelection.ValidateCurrent(present);
            CurrentFilterSelection.RequireSameProgramSampleCount(Scene.FilterProgram.SourceSampleSelection);
            source.ValidatePreparedLights(frame.SourceFrame.Lights);
            if(frame.FrameNumber!=source.AcceptedLighting.FrameNumber||frame.Binding!=Binding)
                throw new InvalidDataException("Directional optional/current Sky packets belong to different source frames.");
            var call=state.BeginCurrentBackendBatch(present,enable,actualClock254,43,"actual-complete-GPU-batch6+1+36");
            callStarted=true;
            Mark("current-source-and-work-plan");
            SkySceneFrameUpdateResult? work=null;SkyCpuState168CompletionPlan completion;
            if(!enable.AllowsWorkInitialization)completion=state.PrepareCompletion(call,new(0,false,0));
            else
            {
                var progress=await Scene.GetProgressAsync();
                if(progress.Phase!=SkyScenePhase.Idle||call.FirstFlat!=0||call.PreparationSubphase!=0)
                    throw new NotSupportedException("Directional thin adapter currently accepts complete source generations; partial WORK needs its current cache continuation.");
                state.RecordWorkInitialization(call,true,true);
                work=await updater.SubmitFrameAsync(call.GenerationIndex,frame.SourceFrame,producers,43);
                if(work.Progress.Phase!=SkyScenePhase.Idle||work.Progress.WorkUnits!=43||
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
                var surfaces=Scene.SpecularTargets[3].Surfaces.Select(s=>new SkySceneBlendSurfaceInputs(s.SurfaceId,
                    SkyCubeBlendFaceMath.EncodeNativeFaceMatrix(s.SurfaceId/levels),ambientSampler,ambientSampler)).ToArray();
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
    public SkySceneSourceFramePacket PrepareCurrentSourceFrame(
        SkyDirectionalSelectedLightSource source,SkyAppliedSourceStageFrame stages)
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        if(sourceFault is not null)throw new InvalidOperationException("Failed Sky owner must be retired/recreated.",sourceFault);
        var present=RequireSourceStages(source,stages);
        if(source.AcceptedLighting.Binding!=Binding)throw new InvalidDataException("Directional current packet belongs to another producer View/light owner.");
        var filterSelection=SkyAmbientCurrentFilterSelection.CaptureCurrent(Scene.FilterProgram.Source,present);
        filterSelection.RequireSameProgramSampleCount(Scene.FilterProgram.SourceSampleSelection);
        textureSnapshot.RequireCurrent();
        source.PrepareLights(shader,textureSnapshot);
        var currentFrame=source.PrepareSourceSelectedSeasonFrame(Scene.SpecularTargets[0].Source,atmosphere,2,shader,stages,textureSnapshot);
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
    public SkyDirectionalAmbientDeviceBinding BorrowCurrentQ80Wrapper(
        DeferredSelected383SkyProjection projection,SkyCpuState168Snapshot snapshot,
        DeferredF103SelectedSource selected,MapSourceCreatedLightingPacket lighting)
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
    public SkyCpuState168Snapshot CaptureCurrentState(long consumerFrame)=>state.CaptureForConsumer(consumerFrame);
    public byte[] CopyAmbientSamplerKey()=>ambientSamplerKey.ToArray();
    public byte[] CopyAmbientSamplerDescriptor()=>ambientSampler.ToArray();
    private static MapSourceCreatedSeasonLightingFrame RequireSourceStages(
        SkyDirectionalSelectedLightSource source,SkyAppliedSourceStageFrame stages)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(stages);source.ValidateCurrent();
        var present=source.AcceptedLighting.AcceptedInputs.PresentSeasonCamera??throw new InvalidDataException("Directional Sky needs its actual current seasonal Camera.");
        stages.ValidateCurrent(source.AcceptedLighting,present.AcceptedSeasonCamera.Inputs.Source.Selection,present.CurrentSkyCamera);
        if(stages.ScenePresent||stages.WorkContributionEnabled)
            throw new NotSupportedException("Directional thin adapter closes actual source-null Scene/no-1F2 WORK only.");
        return present;
    }
    private void RequireTextureFrame(SkySceneSourceFramePacket frame)
    {
        textureSnapshot.RequireCurrent();
        if(frame.TextureBindings is not { } bound||!ReferenceEquals(bound.Owner,textureBindings))
            throw new InvalidDataException("Sky frame belongs to another texture binding owner.");
        bound.RequireCurrent();
    }
    private static SkyDirectionalDesiredSelectionReceipt CaptureDesiredSelection(LoadedShaderContainer shader,
        SkyDirectionalSelectedLightSource source,SkySceneSourceFramePacket actualFrame)
    {
        var receipt=source.CaptureDesiredSelection(shader,actualFrame);
        receipt.ValidateAppliedFrame(actualFrame);receipt.ValidateSourceSlotSelector(source.SelectedPacket);
        receipt.ValidateSelectedMoonProgramScope();
        return receipt;
    }
    private static int Pixel(Sky2SelectionResult s)=>s.Combination.Stages.Single(x=>x.StageCode==0&&x.Entry is not null).Entry!.ProgramIndex;
    private static int Vertex(Sky2SelectionResult s)=>s.Combination.Stages.Single(x=>x.StageCode==1&&x.Entry is not null).Entry!.ProgramIndex;
    private static void RequireSelectedFamily(Sky2SelectionResult contribution,Sky2SelectionResult atmosphere)
    {
        var feature=contribution.RendererFeature.Feature.Words;
        bool mode4=feature[6]==4&&feature[7]==1;
        bool mode7=feature[6]==7&&feature[7]==2;
        if(!contribution.RendererFeature.Feature.RawBytes.AsSpan().SequenceEqual(atmosphere.RendererFeature.Feature.RawBytes)||
            !(mode4&&Vertex(contribution)==0&&Vertex(atmosphere)==0&&Pixel(contribution)==6&&(Pixel(atmosphere) is 5 or 11)||
              mode7&&Vertex(contribution)==2&&Vertex(atmosphere)==2&&Pixel(contribution)==15&&Pixel(atmosphere)==14))
            throw new NotSupportedException("Directional WORK requires its source-selected mode4/profile1 or mode7/profile2 nonnull Sun/Moon shader pair.");
    }
    private NativeMapBorrowerRetirementPackage? finalRetirement;
    private bool finalRetirementRegistered;
    public ValueTask DisposeAsync()
    {
        lock(lifetimeGate)
        {
            retiring=true;textureBindings.Dispose();
            if(disposal is null||disposal.IsFaulted||disposal.IsCanceled)disposal=DisposeCoreAsync();
            return new(disposal);
        }
    }
    private async Task DisposeCoreAsync()
    {
        await gate.WaitAsync();
        try
        {
            await RetireSeasonResourcesCoreAsync();
            finalRetirement??=new(OwnedSkyIdentity+"/Directional-Sky-retire",
                optional.Cloud,optional.Stars,new NativeMapOwnedSamplerRetirement(samplerRid),Scene,textures);
            try{await finalRetirement.DisposeAsync();}
            catch
            {
                if(!finalRetirementRegistered){finalRetirement.RetainFailure();finalRetirementRegistered=true;}
                throw;
            }
        }
        finally{gate.Release();}
    }
}
