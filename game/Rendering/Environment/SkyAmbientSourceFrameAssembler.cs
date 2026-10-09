using San14.Data;
using Godot;

public enum SkyAmbientSourceState { FreshConstructor, RetainedSource, ResolvedCurrent }

/// <summary>Unwritten native cache/template bytes, exact ordinary-light owner
/// selection and pass/branch policy remain explicit construction inputs.</summary>
public sealed record SkyAmbientSourceConstruction(string RetainedProvenance,
    IReadOnlyList<SkySceneSky2FaceCache> SixRetainedFaces,
    SkySceneLights RetainedLightCaches, SkySceneLightRows SelectedLightRows,
    ReadOnlyMemory<byte> VertexTemplate, ReadOnlyMemory<byte> PixelTemplate,
    int NativeClipProfile, SkySceneProgramSelection ProgramSelection);

/// <summary>An already accepted source-created lighting frame. Season is the
/// explicit map-calendar input; it is not inferred from the Camera auto-clock.
/// ResolvedCurrent requires complete Sky2/controller state. RetainedSource
/// explicitly keeps the last accepted state, including its source phase fields.</summary>
public sealed record SkyAmbientSourceFrameInputs(MapSourceCreatedLightingPacket Lighting,
    int SourceSeasonMonth, string SeasonSourceProvenance,
    SkySceneSharedAtmosphere SharedAtmosphere, SkyAmbientSourceState SkyState,
    SkySceneProducerFrame Producers,
    SkySceneControllerSourceProfile? CompleteCurrentController = null,
    SkySceneSky2Parameters? CompleteCurrentParameters = null,
    SkySeasonAppliedFrameInputs? AppliedSeason = null)
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
    SkySceneAmbientFrame SkyUpdate,
    DeferredDeviceBinding DefaultStaticAmbientTexture,
    DeferredDeviceBinding DefaultStaticAmbientSampler);

public sealed record SkyAmbientFrameResourceBorrow(long FrameNumber,
    MapSourceCreatedLightingIdentity LightingIdentity,
    DeferredDeviceBinding Texture,DeferredDeviceBinding Sampler,
    ReadOnlyMemory<byte> FinalSamplerKey,ReadOnlyMemory<byte> SamplerDescriptor,
    string SourceProvenance,bool GeneratedSky);

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
    public SkySceneCameraSourceProfile CameraSource { get; }
    public SkySceneAmbientProvider AmbientProvider { get; }
    public SkyStarProducer? Stars { get; }
    public SkyCloudColorProducer? Cloud { get; }
    private readonly SkySceneTextures textures;
    private readonly SkyAmbientSourceConstruction construction;
    private readonly byte[] generatedSampler;
    private readonly byte[] ambientSamplerKey;
    private readonly Rid staticAmbientSampler;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object lifetimeGate=new();
    private readonly SkySceneControllerSourceProfile constructorController;
    private readonly SkySceneSky2Parameters constructorParameters;
    private SkySceneControllerSourceProfile? acceptedController;
    private SkySceneSky2Parameters? acceptedParameters;
    private SkyAmbientCombinedFrame? current;
    private volatile bool retiring;
    private Task? disposal;
    private readonly bool appliedSeasonScope;

    private SkyAmbientSourceFrameAssembler(SkyScene scene,SkySceneTextures textures,
        SourceResourceRegistrationDevice registration,MapSourceCreatedLightingSource lighting,
        SkySceneCameraSourceProfile camera,SkyAmbientSourceConstruction construction,
        byte[] generatedSampler,byte[] ambientSamplerKey,Rid staticAmbientSampler,MapLightingBinding binding,
        SkyStarProducer? stars,SkyCloudColorProducer? cloud,bool appliedSeasonScope=false)
    {
        Scene=scene;this.textures=textures;Registration=registration;LightingSource=lighting;
        CameraSource=camera;this.construction=construction;this.generatedSampler=generatedSampler;
        this.appliedSeasonScope=appliedSeasonScope;
        this.ambientSamplerKey=ambientSamplerKey;this.staticAmbientSampler=staticAmbientSampler;
        Binding=binding;
        Stars=stars;Cloud=cloud;
        constructorParameters=SkySceneSourceDefaults.CreateSky2Parameters(scene.SpecularTargets[0].Source.PixelFormat);
        constructorController=SkySceneSourceDefaults.ApplySourceCameraControllerInputs(
            SkySceneSourceDefaults.CreateControllerConstructor(),camera);
        AmbientProvider=new(new SkySceneFrameUpdater(scene,construction.SixRetainedFaces,stars,cloud));
    }

    /// <summary>Same accepted render-attempt frame combines owned World season,
    /// Main package/current phase and actual selected View record inputs. Fog
    /// and package+10 light owner are explicit independent source producers.</summary>
    public static SkyAmbientSourceFrameInputs CreateAppliedOwnedSeasonInputs(
        MapSourceCreatedLightingPacket acceptedLighting,SkySeasonMainCameraRgbFrame sourceCamera,
        SkySceneSelectedViewLightPacket selectedViewLight,
        System.Numerics.Vector4 actualPackageLightPosition,string actualPackageLightOwner,
        SkySceneSharedAtmosphere currentAtmosphere,SkySceneProducerFrame producers)
    {
        ArgumentNullException.ThrowIfNull(acceptedLighting);ArgumentNullException.ThrowIfNull(sourceCamera);
        ArgumentNullException.ThrowIfNull(selectedViewLight);ArgumentNullException.ThrowIfNull(currentAtmosphere);
        ArgumentNullException.ThrowIfNull(producers);
        if(acceptedLighting.FrameNumber!=sourceCamera.Inputs.FrameNumber||
            selectedViewLight.Identity.FrameNumber!=acceptedLighting.FrameNumber)
            throw new InvalidDataException("Applied owned season, selected View record and accepted lighting require the same render attempt.");
        var source=sourceCamera.Inputs.Source;
        var lights=SkySceneSelectedViewLightSource.PrepareSkyLights(selectedViewLight,
            actualPackageLightPosition,actualPackageLightOwner);
        return new(acceptedLighting,source.Season.Calendar.State.ThirdDate.Month,source.SourceProvenance,
            currentAtmosphere,SkyAmbientSourceState.ResolvedCurrent,producers,
            AppliedSeason:new(source.Selection,sourceCamera.CurrentSkyCamera,lights));
    }

    /// <summary>Source-created selected kind1 light and actual applied seasonal
    /// Camera, including the native nonnull Moon zero-cache branch. Source
    /// textures and both default programs are resolved automatically; current
    /// unknown edits cannot acquire this source-only capability.</summary>
    public static SkyAmbientSourceFrameInputs CreateAppliedOwnedSeasonSourceInputs(
        GameResourceRepository repository, MapSourceCreatedLightingPacket acceptedLighting,
        SkySeasonMainCameraRgbFrame sourceCamera,
        MapSourceCreatedF103FeaturePacket acceptedOwnedView, string ownedSelectedRecordIdentity,
        System.Numerics.Vector4 actualPackageLightPosition, string actualPackageLightOwner,
        SkySceneSharedAtmosphere currentAtmosphere, SkySceneProducerFrame producers)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(sourceCamera);
        var packet = SkySceneSelectedViewLightSource.CaptureOwnedKind1(acceptedOwnedView,
            acceptedLighting, ownedSelectedRecordIdentity);
        if (acceptedLighting.FrameNumber != sourceCamera.Inputs.FrameNumber)
            throw new InvalidDataException("Source season Camera and owned View publication require the same accepted attempt.");
        if (!acceptedOwnedView.AcceptedCamera.Prefix.Span[..0x1D0]
            .SequenceEqual(sourceCamera.Inputs.MainFrame.Camera.Prefix.Span[..0x1D0]))
            throw new InvalidDataException("Owned View and seasonal Sky Camera require the exact same accepted Main pose, optics and post fields.");
        var source = sourceCamera.Inputs.Source;
        var moon = Sky2SourceNull2D.FromAppliedSeasonOnFreshShader(source.Selection.FirstPackage,
            SkySamplingRole.Moon, SkyTextureStateScope.AppliedSourceSeasonOnFreshShader);
        var proof = Sky2SourceZeroMoon.Create(repository.LoadShaderContainer("system", 0x340AC335),
            moon, acceptedLighting, packet);
        var lights = SkySceneSelectedViewLightSource.PrepareSourceZeroMoonLights(packet,
            actualPackageLightPosition, actualPackageLightOwner, proof);
        return new(acceptedLighting, source.Season.Calendar.State.ThirdDate.Month, source.SourceProvenance,
            currentAtmosphere, SkyAmbientSourceState.ResolvedCurrent, producers,
            AppliedSeason: new(source.Selection, sourceCamera.CurrentSkyCamera, lights));
    }

    /// <summary>Actual source-season application on a fresh owned Sky2 shader:
    /// Earth is the original Static622 source, Milky remains constructor-null
    /// because the selected closed source family skips its null setter. All
    /// current Camera/package/light producers are explicit and same-frame.</summary>
    public static async Task<SkyAmbientSourceFrameAssembler> CreateAppliedSeasonAsync(
        GameResourceRepository repository,Vkd3dShaderCompiler compiler,SourceResourceRegistrationDevice registration,
        MapSourceCreatedLightingSource lighting,SkyAmbientSourceConstruction explicitRetained,
        SkyAmbientSourceFrameInputs firstAcceptedFrame,
        Func<SkyScene,Task<(SkyStarProducer? Stars,SkyCloudColorProducer? Cloud)>>? producerFactory=null,
        bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(registration);ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(explicitRetained);ArgumentNullException.ThrowIfNull(firstAcceptedFrame);
        var applied=firstAcceptedFrame.AppliedSeason??throw new InvalidDataException("Applied season construction requires its actual selected source/current Camera producer.");
        applied.Selection.Validate();
        if(explicitRetained.ProgramSelection is null||explicitRetained.ProgramSelection.FreshNullTextures is not null||
            string.IsNullOrWhiteSpace(explicitRetained.RetainedProvenance))
            throw new InvalidDataException("Applied source season requires explicit retained provenance and a separately proved texture lifetime.");
        ValidateAppliedStageSelection(explicitRetained.ProgramSelection,firstAcceptedFrame,applied);
        await registration.ValidateAsync();registration.ValidateConstructionSource(lighting);
        var camera=SkySceneSourceDefaults.ReadPerspectiveCamera(repository.LoadObjects("system",0x908AB6E2),lighting.Identity.CameraObjectId);
        var ambient=SkyAmbientResources.Load(repository,repository.LoadObjects("system",0xDAB638B2),0x910603AC,
            repository.LoadObjects("system",0x718CCB8B),repository.LoadObjects("system",0x93FB3280));
        var shader=repository.LoadShaderContainer("system",0x340AC335);
        var sta=StaticTextureResources.Load(repository,ambient.Resource,"system",[0xF81F0562,0xF8ED83E3,0xF9BC0264]);
        var earth=SkySeasonPackageTextures.LoadEarth(repository,applied.Selection.FirstPackage);
        if(new[]{ambient.Resource.Source,ambient.ShaderObjects.Source,shader.Source}.Any(s=>s.OverrideSource is not null)||
            sta.Any(s=>s.Resource.Source.OverrideSource is not null))
            throw new InvalidDataException("Applied source season cannot mix edited constructor resources.");
        var nullProof=Sky2SourceNull2D.FromAppliedSeasonOnFreshShader(applied.Selection.FirstPackage,
            SkySamplingRole.MilkyWay,SkyTextureStateScope.AppliedSourceSeasonOnFreshShader);
        var selectedCombination=shader.ReadG1ResourceBindings(explicitRetained.ProgramSelection.Sky2Combination).Combination;
        var selectedPixel=selectedCombination.Stages.Single(s=>s.StageCode==0&&s.Entry is not null).Entry!;
        var selectedVertex=selectedCombination.Stages.Single(s=>s.StageCode==1&&s.Entry is not null).Entry!;
        var selectedDefinitions=shader.ReadResourceDefinitions(selectedPixel.ProgramIndex);
        uint[] actualTextureRegisters=selectedDefinitions.Bindings.Where(b=>b.InputType==2).Select(b=>b.BindPoint).Order().ToArray();
        if(selectedVertex.ProgramIndex!=0 || selectedPixel.ProgramIndex is not(3 or 4) ||
            selectedDefinitions.Buffers.Count!=1 || selectedDefinitions.Buffers[0].ByteSize!=256 ||
            !actualTextureRegisters.SequenceEqual(selectedPixel.ProgramIndex==4 ? new uint[]{1,4} : new uint[]{0,1,2,3,4}) ||
            !selectedDefinitions.Bindings.Where(b=>b.InputType==3).Select(b=>b.BindPoint).Order().SequenceEqual(actualTextureRegisters))
            throw new InvalidDataException("Applied source Sky2 requires its exact selected VS0/PS3-or-PS4 complete texture interface.");
        var copied=explicitRetained with{SixRetainedFaces=SnapshotCaches(explicitRetained.SixRetainedFaces),
            VertexTemplate=explicitRetained.VertexTemplate.ToArray(),PixelTemplate=explicitRetained.PixelTemplate.ToArray(),
            ProgramSelection=explicitRetained.ProgramSelection with{FreshNullTextures=Array.AsReadOnly(new[]{nullProof})}};
        var native=RenderSamplerSourceAdapters.Load(repository.SourceDirectory,"TC");
        byte[] sourceSamplerKey=SourceResourceRegistrationAmbientCube.GetSamplerKey(registration.Source.AmbientCube);
        if(!RenderSamplerSourceAdapters.DecodeKey(native,sourceSamplerKey).EncodeD3D11Descriptor().AsSpan()
            .SequenceEqual(SourceResourceRegistrationAmbientCube.GetSamplerDescriptor(registration.Source.AmbientCube)))
            throw new InvalidDataException("Applied source Static Cube sampler key differs from its exact native conversion.");
        byte[] ambientKey=RenderSamplerSourceAdapters.AmbientSpecularKey(sourceSamplerKey);
        if(!ambientKey.AsSpan().SequenceEqual(RenderSamplerSourceAdapters.AmbientSpecularKey(
            RenderSamplerSourceAdapters.CubeTextureKey(native,(int)ambient.SpecularTargets[0].LevelNum))))
            throw new InvalidDataException("Applied source Static and generated Cube final consumer recipe differs.");
        byte[] sampler=RenderSamplerSourceAdapters.DecodeKey(native,ambientKey).EncodeD3D11Descriptor();
        var initial=Prepare(camera,lighting,copied,ambient.SpecularTargets[0],firstAcceptedFrame,
            SkySceneSourceDefaults.CreateSky2Parameters(ambient.SpecularTargets[0].PixelFormat),
            SkySceneSourceDefaults.CreateControllerConstructor(),null,null,true);
        var faces=SkySceneFramePreparation.Prepare(copied.SixRetainedFaces,initial.Source.SourceFrame);
        var selections=sta.Select((s,i)=>SkySceneTextureSelection.FromStatic("source-sta"+i,s,10))
            .Append(SkySceneTextureSelection.FromSampling(earth)).ToArray();
        var textures=new SkySceneTextures(selections);
        SkyScene? scene=null;SkyStarProducer? stars=null;SkyCloudColorProducer? cloud=null;Rid consumerSampler=default;
        try
        {
            var inputs=new List<Sky2DeviceInput>();
            foreach(uint register in actualTextureRegisters.Where(r=>r!=nullProof.PixelRegister))
            {
                if(register<3)inputs.Add(await textures.GetRetainedInputAsync(register,"source-sta"+register));
                else if(register==3)inputs.Add(await textures.GetRetainedInputAsync(register,earth.SourceName));
                else throw new InvalidDataException("Applied source Sky2 texture register has no actual retained provider.");
            }
            scene=await SkyScene.BuildAsync(ambient,shader,copied.ProgramSelection,compiler,faces.DrawPackets,
                copied.VertexTemplate,copied.PixelTemplate,inputs,sampler,sampler,
                NativeLogTables.Load(repository.SourceDirectory,"TC"),NativeLogDispatch.AvxFma,allowReadback);
            if(producerFactory is not null)(stars,cloud)=await producerFactory(scene);
            consumerSampler=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>DeviceSampler.Create(rd,sampler));
            return new(scene,textures,registration,lighting,camera,copied,sampler,ambientKey,consumerSampler,
                firstAcceptedFrame.Lighting.Binding,stars,cloud,true);
        }
        catch(Exception buildFailure)
        {
            var partial=new NativeMapBorrowerRetirementPackage(
                firstAcceptedFrame.Lighting.Binding.ViewIdentity+"/applied-Sky-partial/"+Guid.NewGuid().ToString("N"),
                buildFailure is NativeMapBorrowerConstructionException nested?nested.RetainedPackage:null,
                cloud,stars,new NativeMapOwnedSamplerRetirement(consumerSampler),scene,textures);
            try{await partial.DisposeAsync();}
            catch(Exception retirementFailure)
            {
                partial.RetainFailure();
                throw new NativeMapBorrowerConstructionException(buildFailure,retirementFailure,partial);
            }
            throw;
        }
    }

    /// <summary>Fresh resource creation derives all proved defaults and exact
    /// source images. The first packet is prepared for shader construction but
    /// submits no draws or clock work; call SubmitFrameAsync with that accepted
    /// lighting frame. Optional real producer factory receives this WORK owner.
    /// Created producers become owned and retire before the scene.</summary>
    public static async Task<SkyAmbientSourceFrameAssembler> CreateFreshAsync(
        GameResourceRepository repository,Vkd3dShaderCompiler compiler,
        SourceResourceRegistrationDevice registration,MapSourceCreatedLightingSource lighting,
        SkyAmbientSourceConstruction explicitRetained,
        SkyAmbientSourceFrameInputs firstAcceptedFrame,
        Func<SkyScene,Task<(SkyStarProducer? Stars,SkyCloudColorProducer? Cloud)>>? producerFactory=null,
        bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(registration);ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(explicitRetained);ArgumentNullException.ThrowIfNull(firstAcceptedFrame);
        if(string.IsNullOrWhiteSpace(explicitRetained.RetainedProvenance)||explicitRetained.ProgramSelection is null||
            explicitRetained.ProgramSelection.Sky2Combination!=0||
            explicitRetained.ProgramSelection.Sky2Operation!=Sky2Operation.Atmosphere||
            explicitRetained.ProgramSelection.FreshNullTextures is not null)
            throw new InvalidDataException("Fresh source assembler requires the explicit original default PS3 pass and unassigned source-null selection.");
        await registration.ValidateAsync();registration.ValidateConstructionSource(lighting);
        var cameraDefinitions=repository.LoadObjects("system",0x908AB6E2);
        var camera=SkySceneSourceDefaults.ReadPerspectiveCamera(cameraDefinitions,lighting.Identity.CameraObjectId);
        var ambient=SkyAmbientResources.Load(repository,repository.LoadObjects("system",0xDAB638B2),0x910603AC,
            repository.LoadObjects("system",0x718CCB8B),repository.LoadObjects("system",0x93FB3280));
        var skySource=repository.LoadShaderContainer("system",0x340AC335);
        var sta=StaticTextureResources.Load(repository,ambient.Resource,"system",[0xF81F0562,0xF8ED83E3,0xF9BC0264]);
        if(camera.IsOverride||new[]{ambient.Resource.Source,ambient.ShaderObjects.Source,
            ambient.SpecularTargets[0].Source.Source,skySource.Source}.Any(s=>s.OverrideSource is not null)||
            sta.Any(s=>s.Resource.Source.OverrideSource is not null))
            throw new InvalidDataException("Fresh source assembler cannot infer constructor resources from edited source overrides.");
        foreach(var role in new[]{SkySamplingRole.Earth,SkySamplingRole.Moon,SkySamplingRole.MilkyWay})
            _=Sky2SourceNull2D.FromFreshConstruction(SkySamplingResources.ReadCameraReference(
                cameraDefinitions,camera.ObjectId,role),SkyTextureStateScope.FreshSourceConstruction);
        var nulls=new[]{SkySamplingRole.Earth,SkySamplingRole.MilkyWay}.Select(role=>
            Sky2SourceNull2D.FromFreshConstruction(SkySamplingResources.ReadCameraReference(
                cameraDefinitions,camera.ObjectId,role),SkyTextureStateScope.FreshSourceConstruction)).ToArray();
        var copied=explicitRetained with{SixRetainedFaces=SnapshotCaches(explicitRetained.SixRetainedFaces),
            VertexTemplate=explicitRetained.VertexTemplate.ToArray(),PixelTemplate=explicitRetained.PixelTemplate.ToArray(),
            ProgramSelection=explicitRetained.ProgramSelection with{FreshNullTextures=Array.AsReadOnly(nulls)}};
        var native=RenderSamplerSourceAdapters.Load(repository.SourceDirectory,"TC");
        byte[] staticSourceKey=SourceResourceRegistrationAmbientCube.GetSamplerKey(registration.Source.AmbientCube);
        if(!RenderSamplerSourceAdapters.DecodeKey(native,staticSourceKey).EncodeD3D11Descriptor().AsSpan()
            .SequenceEqual(SourceResourceRegistrationAmbientCube.GetSamplerDescriptor(registration.Source.AmbientCube)))
            throw new InvalidDataException("Static Cube source sampler key and original native conversion disagree.");
        byte[] ambientKey=RenderSamplerSourceAdapters.AmbientSpecularKey(staticSourceKey);
        if(!ambientKey.AsSpan().SequenceEqual(RenderSamplerSourceAdapters.AmbientSpecularKey(
            RenderSamplerSourceAdapters.CubeTextureKey(native,(int)ambient.SpecularTargets[0].LevelNum))))
            throw new InvalidDataException("This exact Static and generated Cube final consumer recipe changed.");
        byte[] sampler=RenderSamplerSourceAdapters.DecodeKey(native,ambientKey).EncodeD3D11Descriptor();
        var parameters=SkySceneSourceDefaults.CreateSky2Parameters(ambient.SpecularTargets[0].PixelFormat);
        var controller=SkySceneSourceDefaults.ApplySourceCameraControllerInputs(
            SkySceneSourceDefaults.CreateControllerConstructor(),camera);
        var initial=Prepare(camera,lighting,copied,ambient.SpecularTargets[0],firstAcceptedFrame,parameters,controller,null,null);
        var faces=SkySceneFramePreparation.Prepare(copied.SixRetainedFaces,initial.Source.SourceFrame);
        var textures=new SkySceneTextures(sta.Select((s,i)=>SkySceneTextureSelection.FromStatic("source-sta"+i,s,10)).ToArray());
        SkyScene? scene=null;SkyStarProducer? stars=null;SkyCloudColorProducer? cloud=null;Rid consumerSampler=default;
        try
        {
            var inputs=new List<Sky2DeviceInput>();
            for(uint i=0;i<3;i++)inputs.Add(await textures.GetRetainedInputAsync(i,"source-sta"+i));
            scene=await SkyScene.BuildAsync(ambient,skySource,copied.ProgramSelection,compiler,
                faces.DrawPackets,copied.VertexTemplate,copied.PixelTemplate,inputs,sampler,sampler,
                NativeLogTables.Load(repository.SourceDirectory,"TC"),NativeLogDispatch.AvxFma,allowReadback);
            if(producerFactory is not null)(stars,cloud)=await producerFactory(scene);
            consumerSampler=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>DeviceSampler.Create(rd,sampler));
            return new(scene,textures,registration,lighting,camera,copied,sampler,ambientKey,consumerSampler,
                firstAcceptedFrame.Lighting.Binding,stars,cloud);
        }
        catch
        {
            if(cloud is not null)await cloud.DisposeAsync();if(stars is not null)await stars.DisposeAsync();
            if(consumerSampler.IsValid)await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(consumerSampler);return true;});
            if(scene is not null)await scene.DisposeAsync();await textures.DisposeAsync();throw;
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
            ObjectDisposedException.ThrowIf(retiring,this);await Registration.ValidateAsync();
            Registration.ValidateConstructionSource(LightingSource);
            if(input.Lighting is null||input.Lighting.Binding!=Binding)
                throw new InvalidDataException("Combined source frame belongs to another View, Camera owner or shader binding.");
            if(current is not null && input.Lighting.FrameNumber<=current.FrameNumber)
                throw new InvalidDataException("Combined source frames require strictly increasing accepted lighting identities.");
            var progress=await Scene.GetProgressAsync();
            if(progress.Phase is not (SkyScenePhase.Idle or SkyScenePhase.Faulted))
                throw new InvalidOperationException("Finish the combined source sky generation before staging another frame.");
            var prepared=Prepare(CameraSource,LightingSource,construction,Scene.SpecularTargets[(int)explicitGenerationIndex].Source,
                input,constructorParameters,constructorController,acceptedParameters,acceptedController,appliedSeasonScope);
            var selection=SkyAmbientCubeSourceSelection.FromDeclaredCube(Scene.SpecularTargets[(int)explicitGenerationIndex].Source,
                $"same-source-created-frame={input.Lighting.FrameNumber};{construction.RetainedProvenance}");
            var sky=await AmbientProvider.SubmitSourceFrameAsync(explicitGenerationIndex,prepared.Source,
                input.Producers,maximumWorkUnits,selection,generatedSampler,optionalLight);
            var bindings=DefaultAmbientBindings();
            current=new(input.Lighting.FrameNumber,input.Lighting,input.Lighting.Clock,
                MapSeasonMath.SelectMonth(input.SourceSeasonMonth),input.SeasonSourceProvenance,
                prepared.ControllerSource,prepared.Controller,prepared.Source,sky,bindings.Texture,bindings.Sampler);
            acceptedParameters=prepared.Parameters;acceptedController=prepared.ControllerSource;
            return current;
        }
        finally{gate.Release();}
    }

    /// <summary>Owned native World third-date adapter. Season state supplies
    /// the exact independently advanced source month; renderer clock and Sky
    /// controller phase remain separate accepted producers for this frame.</summary>
    public Task<SkyAmbientCombinedFrame> SubmitOwnedSeasonFrameAsync(uint explicitGenerationIndex,
        MapSourceCreatedLightingPacket acceptedLighting,MapSeasonRuntimeFrame ownedSeason,
        SkySceneSharedAtmosphere atmosphere,SkyAmbientSourceState skyState,
        SkySceneProducerFrame producers,uint maximumWorkUnits,
        SkySceneControllerSourceProfile? completeCurrentController=null,
        SkySceneSky2Parameters? completeCurrentParameters=null,SkySceneOptionalLightInputs? optionalLight=null)
        =>SubmitFrameAsync(explicitGenerationIndex,CreateOwnedSeasonInputs(acceptedLighting,ownedSeason,
            atmosphere,skyState,producers,completeCurrentController,completeCurrentParameters),maximumWorkUnits,optionalLight);

    /// <summary>Use the same owned calendar adapter for initial construction
    /// and later submissions; callers do not repeat the source month or its
    /// provenance. It does not derive current Sky phase from either clock.</summary>
    public static SkyAmbientSourceFrameInputs CreateOwnedSeasonInputs(
        MapSourceCreatedLightingPacket acceptedLighting,MapSeasonRuntimeFrame ownedSeason,
        SkySceneSharedAtmosphere atmosphere,SkyAmbientSourceState skyState,
        SkySceneProducerFrame producers,SkySceneControllerSourceProfile? completeCurrentController=null,
        SkySceneSky2Parameters? completeCurrentParameters=null)
    {
        ArgumentNullException.ThrowIfNull(acceptedLighting);ArgumentNullException.ThrowIfNull(ownedSeason);
        MapSeasonRuntime.ValidateOwned(ownedSeason.State);
        if(ownedSeason.FrameNumber!=acceptedLighting.FrameNumber||ownedSeason.Selection!=
            MapSeasonMath.SelectMonth(ownedSeason.State.ThirdDate.Month)||string.IsNullOrWhiteSpace(ownedSeason.SourceProvenance))
            throw new InvalidDataException("Owned native season and accepted lighting must name the same frame.");
        return new(acceptedLighting,ownedSeason.State.ThirdDate.Month,
            ownedSeason.SourceProvenance,atmosphere,skyState,producers,completeCurrentController,completeCurrentParameters);
    }

    public async Task<SkyAmbientCombinedFrame> AdvanceAsync(uint maximumWorkUnits)
    {
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(current is null)throw new InvalidOperationException("No combined source frame has been submitted.");
            await Registration.ValidateAsync();Registration.ValidateConstructionSource(LightingSource);
            current=current with{SkyUpdate=await AmbientProvider.AdvanceAsync(maximumWorkUnits)};
            return current;
        }
        finally{gate.Release();}
    }

    private (DeferredDeviceBinding Texture,DeferredDeviceBinding Sampler) DefaultAmbientBindings()
    {
        var resource=Registration.Entries[SourceResourceRegistrationAmbientCube.ObjectKey];
        return(new("Texture",7,resource.TextureRid,resource.TextureRid,resource.OwnerProvenance),
            new("Sampler",7,staticAmbientSampler,null,resource.OwnerProvenance+";D189D0-final-consumer-key="+Convert.ToHexString(ambientSamplerKey)));
    }

    public Task<SkyAmbientFrameResourceBorrow> BorrowDefaultAmbientAsync(long frameNumber,
        MapSourceCreatedLightingIdentity expectedLightingIdentity)
        =>BorrowAsync(frameNumber,expectedLightingIdentity,false);

    public Task<SkyAmbientFrameResourceBorrow> BorrowCompletedGeneratedAmbientAsync(long frameNumber,
        MapSourceCreatedLightingIdentity expectedLightingIdentity)
        =>BorrowAsync(frameNumber,expectedLightingIdentity,true);

    private async Task<SkyAmbientFrameResourceBorrow> BorrowAsync(long frameNumber,
        MapSourceCreatedLightingIdentity identity,bool generated)
    {
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(current is null||current.FrameNumber!=frameNumber||identity!=LightingSource.Identity||
                current.Lighting.SourceIdentity!=identity)
                throw new InvalidDataException("Ambient borrowing requires this same accepted source lighting frame.");
            await Registration.ValidateAsync();Registration.ValidateConstructionSource(LightingSource);
            if(generated)
            {
                var ready=current.SkyUpdate.Ambient??throw new InvalidOperationException("This source frame's generated cube has not finished filtering.");
                return new(frameNumber,identity,ready.Texture,ready.Sampler,ambientSamplerKey.ToArray(),generatedSampler.ToArray(),
                    current.SkySource.SourceProvenance+";"+ready.Texture.SourceResourceUri,true);
            }
            return new(frameNumber,identity,current.DefaultStaticAmbientTexture,current.DefaultStaticAmbientSampler,
                ambientSamplerKey.ToArray(),generatedSampler.ToArray(),current.DefaultStaticAmbientSampler.SourceResourceUri,false);
        }
        finally{gate.Release();}
    }

    private static (SkySceneSourceFramePacket Source,SkySceneSky2Parameters Parameters,
        SkySceneControllerSourceProfile ControllerSource,SkyScenePreparedController Controller) Prepare(
        SkySceneCameraSourceProfile camera,MapSourceCreatedLightingSource source,
        SkyAmbientSourceConstruction construction,CubeRenderTextureDefinition target,
        SkyAmbientSourceFrameInputs input,SkySceneSky2Parameters initialParameters,
        SkySceneControllerSourceProfile initialController,SkySceneSky2Parameters? previousParameters,
        SkySceneControllerSourceProfile? previousController,bool appliedSeasonScope=false)
    {
        ArgumentNullException.ThrowIfNull(input.Lighting);ArgumentNullException.ThrowIfNull(input.SharedAtmosphere);
        ArgumentNullException.ThrowIfNull(input.Producers);
        if(input.Lighting.SourceIdentity!=source.Identity||string.IsNullOrWhiteSpace(input.SeasonSourceProvenance))
            throw new InvalidDataException("Combined sky needs this constructed lighting generation and explicit source season provenance.");
        _=MapSeasonMath.SelectMonth(input.SourceSeasonMonth);
        if(appliedSeasonScope)
        {
            var applied=input.AppliedSeason??throw new InvalidDataException("This owned shader requires its actual current applied seasonal source.");
            applied.Selection.Validate();
            ValidateAppliedStageSelection(construction.ProgramSelection,input,applied);
            if(input.SkyState!=SkyAmbientSourceState.ResolvedCurrent||input.CompleteCurrentController is not null||
                input.CompleteCurrentParameters is not null||applied.CurrentCamera.FrameNumber!=input.Lighting.FrameNumber||
                applied.Selection.Provider.Database.Action.Selection!=MapSeasonMath.SelectMonth(input.SourceSeasonMonth))
                throw new InvalidDataException("Applied source season/current View Camera and lighting require one accepted frame and season scope.");
            var current=applied.CurrentCamera;
            applied.CurrentCameraLights.SourceZeroMoon?.ValidateLighting(input.Lighting);
            var appliedController=SkySceneSourceDefaults.PrepareController(current.Controller,current.Parameters,
                applied.CurrentCameraLights.SunPresent,applied.CurrentCameraLights.MoonPresent);
            var appliedPacket=SkySceneSourceFrameProvider.PrepareAppliedSeason(applied.Selection,current,input.Lighting,target,
                applied.CurrentCameraLights,input.SharedAtmosphere,appliedController,construction.NativeClipProfile);
            return(appliedPacket,current.Parameters,current.Controller,appliedController);
        }
        if(input.AppliedSeason is not null)throw new InvalidDataException("Fresh source-only shader cannot accept an external seasonal package scope.");
        SkySceneSky2Parameters parameters;SkySceneControllerSourceProfile controller;
        switch(input.SkyState)
        {
            case SkyAmbientSourceState.FreshConstructor when previousParameters is null && previousController is null:
                if(input.CompleteCurrentController is not null||input.CompleteCurrentParameters is not null)
                    throw new InvalidDataException("Fresh constructor state cannot silently combine edited Sky inputs.");
                parameters=initialParameters;controller=initialController;break;
            case SkyAmbientSourceState.RetainedSource when previousParameters is not null && previousController is not null:
                if(input.CompleteCurrentController is not null||input.CompleteCurrentParameters is not null)
                    throw new InvalidDataException("Retained source state does not accept competing current field writers.");
                parameters=previousParameters;controller=previousController;break;
            case SkyAmbientSourceState.ResolvedCurrent:
                parameters=input.CompleteCurrentParameters??throw new InvalidDataException("Current Sky2 parameters must be complete.");
                controller=input.CompleteCurrentController??throw new InvalidDataException("Current Sky controller phase/state must be complete.");
                controller=SkySceneSourceDefaults.ApplyPreparedMainControllerInputs(controller,input.Lighting.PreparedCameraPrefix.Span);
                break;
            default:throw new InvalidDataException("Sky state must explicitly select fresh, retained or fully resolved current inputs.");
        }
        if(parameters.NativeTargetFormat!=target.PixelFormat)throw new InvalidDataException("Current Sky parameters have another source target format.");
        var lights=SkySceneLightData.ApplyOrdinaryRows(construction.RetainedLightCaches,
            input.Lighting.LocalLightData.Span,construction.SelectedLightRows);
        var prepared=SkySceneSourceDefaults.PrepareController(controller,parameters,lights.SunPresent,lights.MoonPresent);
        parameters=parameters with{MilkyStrength1484=prepared.MilkyStrength1484};
        var packet=SkySceneSourceFrameProvider.PrepareResolved(camera,input.Lighting,target,
            construction.RetainedLightCaches,construction.SelectedLightRows,input.SharedAtmosphere,new(false,false,false),
            prepared.Controller,construction.NativeClipProfile,parameters);
        packet=packet with{UsesSourceConstructorDefaults=input.SkyState==SkyAmbientSourceState.FreshConstructor,
            SourceProvenance=packet.SourceProvenance+$";source-season-month={input.SourceSeasonMonth};{input.SeasonSourceProvenance};"+
                $"sky-state={input.SkyState};sky-controller={controller.SourceProvenance};source-camera-clock-tail={input.Lighting.Clock.AutoStepExecuted}"};
        return(packet,parameters,controller,prepared);
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
    {lock(lifetimeGate){retiring=true;return new(disposal??=DisposeCoreAsync());}}
    private async Task DisposeCoreAsync()
    {
        await gate.WaitAsync();
        try
        {
            await AmbientProvider.DisposeAsync();if(Cloud is not null)await Cloud.DisposeAsync();
            if(Stars is not null)await Stars.DisposeAsync();
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(staticAmbientSampler);return true;});
            await Scene.DisposeAsync();await textures.DisposeAsync();
        }
        finally{gate.Release();}
    }
}
