using San14.Data;

/// <summary>Owned six-face caches, constant templates and the selected
/// program used to create the seasonal sky scene.</summary>
public sealed record SkyAmbientSourceConstruction(string RetainedProvenance,
    IReadOnlyList<SkySceneSky2FaceCache> SixRetainedFaces,
    ReadOnlyMemory<byte> VertexTemplate, ReadOnlyMemory<byte> PixelTemplate,
    int NativeClipProfile, SkySceneProgramSelection ProgramSelection);

/// <summary>An already accepted source-created lighting frame. Season is the
/// explicit map-calendar input; it is not inferred from the Camera auto-clock.
/// The applied season supplies the current Camera, controller and lights.</summary>
public sealed record SkyAmbientSourceFrameInputs(MapSourceCreatedLightingPacket Lighting,
    int SourceSeasonMonth, string SeasonSourceProvenance,
    SkySceneSharedAtmosphere SharedAtmosphere, SkySceneProducerFrame Producers,
    SkySeasonAppliedFrameInputs AppliedSeason)
{
    /// <summary>Actual fresh owned Camera/WORK stage gate. Required for the
    /// source-null Scene nativepass1 route; a caller-selected combination alone
    /// is not permission to switch source programs.</summary>
    public SkyAppliedSourceStageFrame? SourceStages { get; init; }
}

/// <summary>Selected source package and current View Camera are independent
/// of HDRI setter Camera3/4. Current lights retain their actual producer scope.</summary>
public sealed record SkySeasonAppliedFrameInputs(MapSeasonCameraSelection Selection,
    SkySceneCurrentCameraParameters CurrentCamera,SkySceneLights CurrentCameraLights);

public sealed record SkyAmbientCombinedFrame(long FrameNumber,
    MapSourceCreatedLightingPacket Lighting, SkylightClockResult Clock,
    MapSeasonSelection Season, string SeasonSourceProvenance,
    SkySceneControllerSourceProfile ControllerSource,
    SkyScenePreparedController PreparedController,
    SkySceneSourceFramePacket SkySource,
    SkySceneAmbientFrame SkyUpdate);

/// <summary>Production source-created lighting/registry→Sky2 construction,
/// controller, six native cameras→source WORK/copy/filter lifecycle. It owns
/// its scene, STA images and consumer samplers; registry/static HDRI cube are
/// borrowed. Completed generated sky and default Static5A remain distinct.
/// Use this owner for every sky mutation while it is attached to a map frame.</summary>
public sealed class SkyAmbientSourceFrameAssembler : IAsyncDisposable
{
    public SkyScene Scene { get; }
    public SourceResourceRegistrationDevice Registration { get; }
    public MapSourceCreatedLightingSource LightingSource { get; }
    public MapLightingBinding Binding { get; }
    public SkySceneAmbientProvider AmbientProvider { get; }
    public SkyStarProducer? Stars { get; }
    public SkyCloudColorProducer? Cloud { get; }
    private readonly SkySceneTextures textures;
    private readonly Sky2TextureBindings textureBindings;
    private readonly SkyAmbientSourceConstruction construction;
    private readonly byte[] generatedSampler;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object lifetimeGate=new();
    private SkyAmbientCombinedFrame? current;
    private volatile bool retiring;
    private Task? disposal;
    private NativeMapBorrowerRetirementPackage? finalRetirement;

    private SkyAmbientSourceFrameAssembler(SkyScene scene,SkySceneTextures textures,
        SourceResourceRegistrationDevice registration,MapSourceCreatedLightingSource lighting,
        SkyAmbientSourceConstruction construction,
        byte[] generatedSampler,MapLightingBinding binding,
        SkyStarProducer? stars,SkyCloudColorProducer? cloud,Sky2TextureBindings textureBindings)
    {
        this.textureBindings=textureBindings;Scene=scene;this.textures=textures;Registration=registration;LightingSource=lighting;
        this.construction=construction;this.generatedSampler=generatedSampler;
        Binding=binding;
        Stars=stars;Cloud=cloud;
        AmbientProvider=new(new SkySceneFrameUpdater(scene,construction.SixRetainedFaces,stars,cloud));
    }

    /// <summary>Build the shader against the texture bindings that already
    /// produced the accepted source frame. Source-zero assignments preserve
    /// this owner's current descriptor state.</summary>
    public static async Task<SkyAmbientSourceFrameAssembler> CreateAppliedSeasonAsync(
        GameResourceRepository repository,LoadedSkyAmbientResources ambient,LoadedShaderContainer shader,
        Vkd3dShaderCompiler compiler,SourceResourceRegistrationDevice registration,
        MapSourceCreatedLightingSource lighting,SkyAmbientSourceConstruction explicitRetained,
        SkyAmbientSourceFrameInputs firstAcceptedFrame,Sky2TextureBindings textureBindings,
        Func<SkyScene,Task<(SkyStarProducer? Stars,SkyCloudColorProducer? Cloud)>>? producerFactory=null,
        bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(ambient);
        ArgumentNullException.ThrowIfNull(shader);ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(registration);ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(explicitRetained);ArgumentNullException.ThrowIfNull(firstAcceptedFrame);
        var applied=firstAcceptedFrame.AppliedSeason??throw new InvalidDataException("Applied season construction requires its actual selected source/current Camera producer.");
        applied.Selection.Validate();
        ArgumentNullException.ThrowIfNull(textureBindings);
        var textureSnapshot=explicitRetained.ProgramSelection?.TextureBindings??textureBindings.Capture();
        textureSnapshot.RequireCurrent();
        textureBindings.RequireCompatiblePackage(SkySeasonPackageTextures.Read(applied.Selection.FirstPackage),
            reference=>SkySeasonPackageTextures.LoadSampling(repository,reference));
        if(explicitRetained.ProgramSelection is null||
            string.IsNullOrWhiteSpace(explicitRetained.RetainedProvenance))
            throw new InvalidDataException("Applied source season requires retained current state and its actual texture binding owner.");
        if(explicitRetained.ProgramSelection.TextureBindings is { } prior&&
            !ReferenceEquals(prior.Owner,textureBindings))
            throw new InvalidDataException("Applied Sky construction belongs to another texture binding owner.");
        ValidateAppliedStageSelection(explicitRetained.ProgramSelection,firstAcceptedFrame,applied);
        registration.ValidateConstructionSource(lighting);
        var sta=StaticTextureResources.Load(repository,ambient.Resource,"system",[0xF81F0562,0xF8ED83E3,0xF9BC0264]);
        var assignedTextures=new[]{SkySamplingRole.Earth,SkySamplingRole.Moon,SkySamplingRole.MilkyWay}
            .Select(textureSnapshot.GetTexture).OfType<LoadedSkySamplingResource>().ToArray();
        var selectedCombination=shader.ReadG1ResourceBindings(explicitRetained.ProgramSelection.Sky2Combination).Combination;
        var selectedPixel=selectedCombination.Stages.Single(s=>s.StageCode==0&&s.Entry is not null).Entry!;
        var selectedDefinitions=shader.ReadResourceDefinitions(selectedPixel.ProgramIndex);
        uint[] actualTextureRegisters=selectedDefinitions.Bindings.Where(b=>b.InputType==2).Select(b=>b.BindPoint).Order().ToArray();
        var samplingRegisters = new[]{SkySamplingRole.Earth,SkySamplingRole.Moon,SkySamplingRole.MilkyWay}
            .Select(SkySamplingResources.PixelRegister).ToHashSet();
        if(actualTextureRegisters.Any(register=>register>=sta.Count&&!samplingRegisters.Contains(register)))
            throw new NotSupportedException("Applied Sky2 declares a texture register without a static or package sampling provider.");
        var copied=explicitRetained with{SixRetainedFaces=SnapshotCaches(explicitRetained.SixRetainedFaces),
            VertexTemplate=explicitRetained.VertexTemplate.ToArray(),PixelTemplate=explicitRetained.PixelTemplate.ToArray(),
            ProgramSelection=explicitRetained.ProgramSelection with{TextureBindings=textureSnapshot}};
        var native=RenderSamplerSourceAdapters.Load(repository.SourceDirectory,"TC");
        byte[] sourceSamplerKey=SourceResourceRegistrationAmbientCube.GetSamplerKey(registration.Source.AmbientCube);
        byte[] ambientKey=RenderSamplerSourceAdapters.AmbientSpecularKey(sourceSamplerKey);
        if(!ambientKey.AsSpan().SequenceEqual(RenderSamplerSourceAdapters.AmbientSpecularKey(
            RenderSamplerSourceAdapters.CubeTextureKey(native,(int)ambient.SpecularTargets[0].LevelNum))))
            throw new InvalidDataException("Applied source Static and generated Cube final consumer recipe differs.");
        byte[] sampler=RenderSamplerSourceAdapters.DecodeKey(native,ambientKey).EncodeD3D11Descriptor();
        var initial=Prepare(lighting,copied,ambient.SpecularTargets[0],firstAcceptedFrame);
        var faces=SkySceneFramePreparation.Prepare(copied.SixRetainedFaces,initial.Source.SourceFrame,
            shader.ReadResourceDefinitions(selectedCombination.Stages.Single(stage=>stage.StageCode==0).Entry!.ProgramIndex));
        var selections=sta.Select((s,i)=>SkySceneTextureSelection.FromStatic("source-sta"+i,s,10))
            .Concat(assignedTextures.Select(SkySceneTextureSelection.FromSampling)).ToArray();
        var textures=new SkySceneTextures(selections);
        SkyScene? scene=null;SkyStarProducer? stars=null;SkyCloudColorProducer? cloud=null;
        try
        {
            var inputs=new List<Sky2DeviceInput>();
            foreach(uint register in actualTextureRegisters.Where(r=>r<3||assignedTextures.Any(t=>t.PixelRegister==r)))
            {
                if(register<3)inputs.Add(await textures.GetRetainedInputAsync(register,"source-sta"+register));
                else if(assignedTextures.SingleOrDefault(t=>t.PixelRegister==register) is { } assigned)
                    inputs.Add(await textures.GetInputAsync(register,assigned.SourceName));
                else throw new InvalidDataException("Applied source Sky2 texture register has no actual retained provider.");
            }
            scene=await SkyScene.BuildAsync(ambient,shader,copied.ProgramSelection,compiler,faces.DrawPackets,
                copied.VertexTemplate,copied.PixelTemplate,inputs,sampler,sampler,
                NativeLogTables.Load(repository.SourceDirectory,"TC"),NativeLogDispatch.AvxFma,allowReadback);
            if(producerFactory is not null)(stars,cloud)=await producerFactory(scene);
            return new(scene,textures,registration,lighting,copied,sampler,
                firstAcceptedFrame.Lighting.Binding,stars,cloud,textureBindings);
        }
        catch(Exception buildFailure)
        {
            textureBindings.Dispose();
            var partial=new NativeMapBorrowerRetirementPackage(
                firstAcceptedFrame.Lighting.Binding.ViewIdentity+"/applied-Sky-partial/"+Guid.NewGuid().ToString("N"),
                buildFailure is NativeMapBorrowerConstructionException nested?nested.RetainedPackage:null,
                cloud,stars,scene,textures);
            try{await partial.DisposeAsync();}
            catch(Exception retirementFailure)
            {
                partial.RetainFailure();
                throw new NativeMapBorrowerConstructionException(buildFailure,retirementFailure,partial);
            }
            throw;
        }
    }

    public async Task<SkyAmbientCombinedFrame> SubmitFrameAsync(uint explicitGenerationIndex,
        SkyAmbientSourceFrameInputs input,uint maximumWorkUnits,SkySceneOptionalLightInputs? optionalLight=null)
    {
        ArgumentNullException.ThrowIfNull(input);
        if(explicitGenerationIndex>=3)throw new ArgumentOutOfRangeException(nameof(explicitGenerationIndex));
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);Registration.RequireAvailable();
            if(input.Lighting is null||input.Lighting.Binding!=Binding)
                throw new InvalidDataException("Combined source frame belongs to another View, Camera owner or shader binding.");
            if(current is not null && input.Lighting.FrameNumber<=current.FrameNumber)
                throw new InvalidDataException("Combined source frames require strictly increasing accepted lighting identities.");
            var progress=await Scene.GetProgressAsync();
            if(progress.Phase is not (SkyScenePhase.Idle or SkyScenePhase.Faulted))
                throw new InvalidOperationException("Finish the combined source sky generation before staging another frame.");
            var prepared=Prepare(LightingSource,construction,Scene.SpecularTargets[(int)explicitGenerationIndex].Source,input);
            var selection=SkyAmbientCubeSourceSelection.FromDeclaredCube(Scene.SpecularTargets[(int)explicitGenerationIndex].Source,
                $"same-source-created-frame={input.Lighting.FrameNumber};{construction.RetainedProvenance}");
            var sky=await AmbientProvider.SubmitSourceFrameAsync(explicitGenerationIndex,prepared.Source,
                input.Producers,maximumWorkUnits,selection,generatedSampler,optionalLight);
            current=new(input.Lighting.FrameNumber,input.Lighting,input.Lighting.Clock,
                MapSeasonMath.SelectMonth(input.SourceSeasonMonth),input.SeasonSourceProvenance,
                prepared.ControllerSource,prepared.Controller,prepared.Source,sky);
            return current;
        }
        finally{gate.Release();}
    }

    private static (SkySceneSourceFramePacket Source,
        SkySceneControllerSourceProfile ControllerSource,SkyScenePreparedController Controller) Prepare(
        MapSourceCreatedLightingSource source,SkyAmbientSourceConstruction construction,
        CubeRenderTextureDefinition target,SkyAmbientSourceFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(input.Lighting);ArgumentNullException.ThrowIfNull(input.SharedAtmosphere);
        ArgumentNullException.ThrowIfNull(input.Producers);
        if(input.Lighting.SourceIdentity!=source.Identity||string.IsNullOrWhiteSpace(input.SeasonSourceProvenance))
            throw new InvalidDataException("Combined sky needs this constructed lighting generation and explicit source season provenance.");
        construction.ProgramSelection.TextureBindings.RequireCurrent();
        var season=MapSeasonMath.SelectMonth(input.SourceSeasonMonth);
        var applied=input.AppliedSeason??throw new InvalidDataException("This owned shader requires its actual current applied seasonal source.");
        applied.Selection.Validate();
        ValidateAppliedStageSelection(construction.ProgramSelection,input,applied);
        if(applied.CurrentCamera.FrameNumber!=input.Lighting.FrameNumber||
            applied.Selection.Provider.Database.Action.Selection!=season)
            throw new InvalidDataException("Applied source season/current View Camera and lighting require one accepted frame and season scope.");
        var current=applied.CurrentCamera;
        applied.CurrentCameraLights.SourceZeroMoon?.ValidateLighting(input.Lighting);
        var appliedController=SkySceneSourceDefaults.PrepareController(current.Controller,current.Parameters,
            applied.CurrentCameraLights.SunPresent,applied.CurrentCameraLights.MoonPresent);
        var appliedPacket=SkySceneSourceFrameProvider.PrepareAppliedSeason(applied.Selection,current,input.Lighting,target,
            applied.CurrentCameraLights,input.SharedAtmosphere,appliedController,construction.NativeClipProfile,construction.ProgramSelection.TextureBindings);
        return(appliedPacket,current.Controller,appliedController);
    }

    /// <summary>Historical explicit combo0 scope remains available without a
    /// source-stage receipt. The actual source-created null Scene branch may
    /// use combo1 only with its same-frame private owned producer receipt.</summary>
    public static void ValidateAppliedStageSelection(SkySceneProgramSelection selection,
        SkyAmbientSourceFrameInputs input,SkySeasonAppliedFrameInputs applied)
    {
        ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(applied);
        if(input.SourceStages is { } stages)
        {
            stages.ValidateCurrent(input.Lighting,applied.Selection,applied.CurrentCamera);
            if(selection.Sky2Combination!=stages.Sky2Combination||
                selection.Sky2Operation!=Sky2Operation.Contribution||stages.ScenePresent||
                stages.WorkContributionEnabled||input.Producers.Scene is not null||input.Producers.Contribution is not null||
                input.Producers.Sky2Enabled!=stages.Sky2Enabled)
                throw new InvalidDataException("Actual applied source stage gates disagree with the selected program or optional producers.");
            return;
        }
        if(selection.Sky2Combination!=0||selection.Sky2Operation!=Sky2Operation.Atmosphere)
            throw new InvalidDataException("Applied nativepass1 requires the actual source-null Scene owned receipt; arbitrary caller combo1 is not a source gate.");
    }

    private static IReadOnlyList<SkySceneSky2FaceCache> SnapshotCaches(IReadOnlyList<SkySceneSky2FaceCache> caches)
    {
        ArgumentNullException.ThrowIfNull(caches);
        if(caches.Count!=6||caches.Any(c=>c is null||c.Draw is null||c.Draw.Pixel.Scattering is null))
            throw new InvalidDataException("Source sky construction requires six complete retained native caches.");
        return Array.AsReadOnly(caches.Select(c=>c with{Draw=c.Draw with{Pixel=c.Draw.Pixel with{
            Scattering=Array.AsReadOnly(c.Draw.Pixel.Scattering.ToArray())}}}).ToArray());
    }

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
            finalRetirement??=new(Binding.ViewIdentity+"/source-Sky-retire",
                AmbientProvider,Cloud,Stars,Scene,textures);
            await finalRetirement.DisposeAsync();
        }
        finally{gate.Release();}
    }
}
