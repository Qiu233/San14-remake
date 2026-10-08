using San14.Data;
using Godot;

public enum OriginalSkyAmbientSourceState { FreshConstructor, RetainedSource, ResolvedCurrent }

/// <summary>Unwritten native cache/template bytes, exact ordinary-light owner
/// selection and pass/branch policy remain explicit construction inputs.</summary>
public sealed record OriginalSkyAmbientSourceConstruction(string RetainedProvenance,
    IReadOnlyList<OriginalSkySceneSky2FaceCache> SixRetainedFaces,
    OriginalSkySceneLights RetainedLightCaches, OriginalSkySceneLightRows SelectedLightRows,
    ReadOnlyMemory<byte> VertexTemplate, ReadOnlyMemory<byte> PixelTemplate,
    int NativeClipProfile, OriginalSkySceneProgramSelection ProgramSelection);

/// <summary>An already accepted source-created lighting frame. Season is the
/// explicit map-calendar input; it is not inferred from the Camera auto-clock.
/// ResolvedCurrent requires complete Sky2/controller state. RetainedSource
/// explicitly keeps the last accepted state, including its source phase fields.</summary>
public sealed record OriginalSkyAmbientSourceFrameInputs(OriginalMapSourceCreatedLightingPacket Lighting,
    int SourceSeasonMonth, string SeasonSourceProvenance,
    OriginalSkySceneSharedAtmosphere SharedAtmosphere, OriginalSkyAmbientSourceState SkyState,
    OriginalSkySceneProducerFrame Producers,
    OriginalSkySceneControllerSourceProfile? CompleteCurrentController = null,
    OriginalSkySceneSky2Parameters? CompleteCurrentParameters = null,
    OriginalSkySeasonAppliedFrameInputs? AppliedSeason = null)
{
    /// <summary>Actual fresh owned Camera/WORK stage gate. Required for the
    /// source-null Scene nativepass1 route; a caller-selected combination alone
    /// is not permission to switch source programs.</summary>
    public OriginalSkyAppliedSourceStageFrame? SourceStages { get; init; }
}

/// <summary>Selected source package and current View Camera are independent
/// of HDRI setter Camera3/4. Current lights retain their actual producer scope.</summary>
public sealed record OriginalSkySeasonAppliedFrameInputs(OriginalMapSeasonCameraSelection Selection,
    OriginalSkySceneCurrentCameraParameters CurrentCamera,OriginalSkySceneLights CurrentCameraLights);

public sealed record OriginalSkyAmbientCombinedFrame(long FrameNumber,
    OriginalMapSourceCreatedLightingPacket Lighting, OriginalSkylightClockResult Clock,
    OriginalMapSeasonSelection Season, string SeasonSourceProvenance,
    OriginalSkySceneControllerSourceProfile ControllerSource,
    OriginalSkyScenePreparedController PreparedController,
    OriginalSkySceneSourceFramePacket SkySource,
    OriginalSkySceneAmbientFrame SkyUpdate,
    OriginalDeferredDeviceBinding DefaultStaticAmbientTexture,
    OriginalDeferredDeviceBinding DefaultStaticAmbientSampler);

public sealed record OriginalSkyAmbientFrameResourceBorrow(long FrameNumber,
    OriginalMapSourceCreatedLightingIdentity LightingIdentity,
    OriginalDeferredDeviceBinding Texture,OriginalDeferredDeviceBinding Sampler,
    ReadOnlyMemory<byte> FinalSamplerKey,ReadOnlyMemory<byte> SamplerDescriptor,
    string SourceProvenance,bool GeneratedSky);

/// <summary>Production source-created lighting/registry→Sky2 construction,
/// controller, six native cameras→source WORK/copy/filter lifecycle. It owns
/// its scene, STA images and consumer samplers; registry/static HDRI cube are
/// borrowed. Completed generated sky and default Static5A remain distinct.
/// Use this owner for every sky mutation while it is attached to a map frame.</summary>
public sealed class OriginalSkyAmbientSourceFrameAssembler : IAsyncDisposable
{
    public OriginalSkyScene Scene { get; }
    public SourceResourceRegistrationDevice Registration { get; }
    public OriginalMapSourceCreatedLightingSource LightingSource { get; }
    public OriginalMapLightingBinding Binding { get; }
    public OriginalSkySceneCameraSourceProfile CameraSource { get; }
    public OriginalSkySceneAmbientProvider AmbientProvider { get; }
    public OriginalSkyStarProducer? Stars { get; }
    public OriginalSkyCloudColorProducer? Cloud { get; }
    private readonly OriginalSkySceneTextures textures;
    private readonly OriginalSkyAmbientSourceConstruction construction;
    private readonly byte[] generatedSampler;
    private readonly byte[] ambientSamplerKey;
    private readonly Rid staticAmbientSampler;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object lifetimeGate=new();
    private readonly OriginalSkySceneControllerSourceProfile constructorController;
    private readonly OriginalSkySceneSky2Parameters constructorParameters;
    private OriginalSkySceneControllerSourceProfile? acceptedController;
    private OriginalSkySceneSky2Parameters? acceptedParameters;
    private OriginalSkyAmbientCombinedFrame? current;
    private volatile bool retiring;
    private Task? disposal;
    private readonly bool appliedSeasonScope;

    private OriginalSkyAmbientSourceFrameAssembler(OriginalSkyScene scene,OriginalSkySceneTextures textures,
        SourceResourceRegistrationDevice registration,OriginalMapSourceCreatedLightingSource lighting,
        OriginalSkySceneCameraSourceProfile camera,OriginalSkyAmbientSourceConstruction construction,
        byte[] generatedSampler,byte[] ambientSamplerKey,Rid staticAmbientSampler,OriginalMapLightingBinding binding,
        OriginalSkyStarProducer? stars,OriginalSkyCloudColorProducer? cloud,bool appliedSeasonScope=false)
    {
        Scene=scene;this.textures=textures;Registration=registration;LightingSource=lighting;
        CameraSource=camera;this.construction=construction;this.generatedSampler=generatedSampler;
        this.appliedSeasonScope=appliedSeasonScope;
        this.ambientSamplerKey=ambientSamplerKey;this.staticAmbientSampler=staticAmbientSampler;
        Binding=binding;
        Stars=stars;Cloud=cloud;
        constructorParameters=OriginalSkySceneSourceDefaults.CreateSky2Parameters(scene.SpecularTargets[0].Source.PixelFormat);
        constructorController=OriginalSkySceneSourceDefaults.ApplySourceCameraControllerInputs(
            OriginalSkySceneSourceDefaults.CreateControllerConstructor(),camera);
        AmbientProvider=new(new OriginalSkySceneFrameUpdater(scene,construction.SixRetainedFaces,stars,cloud));
    }

    /// <summary>Same accepted render-attempt frame combines owned World season,
    /// Main package/current phase and actual selected View record inputs. Fog
    /// and package+10 light owner are explicit independent source producers.</summary>
    public static OriginalSkyAmbientSourceFrameInputs CreateAppliedOwnedSeasonInputs(
        OriginalMapSourceCreatedLightingPacket acceptedLighting,OriginalSkySeasonMainCameraRgbFrame sourceCamera,
        OriginalSkySceneSelectedViewLightPacket selectedViewLight,
        System.Numerics.Vector4 actualPackageLightPosition,string actualPackageLightOwner,
        OriginalSkySceneSharedAtmosphere currentAtmosphere,OriginalSkySceneProducerFrame producers)
    {
        ArgumentNullException.ThrowIfNull(acceptedLighting);ArgumentNullException.ThrowIfNull(sourceCamera);
        ArgumentNullException.ThrowIfNull(selectedViewLight);ArgumentNullException.ThrowIfNull(currentAtmosphere);
        ArgumentNullException.ThrowIfNull(producers);
        if(acceptedLighting.FrameNumber!=sourceCamera.Inputs.FrameNumber||
            selectedViewLight.Identity.FrameNumber!=acceptedLighting.FrameNumber)
            throw new InvalidDataException("Applied owned season, selected View record and accepted lighting require the same render attempt.");
        var source=sourceCamera.Inputs.Source;
        var lights=OriginalSkySceneSelectedViewLightSource.PrepareSkyLights(selectedViewLight,
            actualPackageLightPosition,actualPackageLightOwner);
        return new(acceptedLighting,source.Season.Calendar.State.ThirdDate.Month,source.SourceProvenance,
            currentAtmosphere,OriginalSkyAmbientSourceState.ResolvedCurrent,producers,
            AppliedSeason:new(source.Selection,sourceCamera.CurrentSkyCamera,lights));
    }

    /// <summary>Source-created selected kind1 light and actual applied seasonal
    /// Camera, including the native nonnull Moon zero-cache branch. Source
    /// textures and both default programs are resolved automatically; current
    /// unknown edits cannot acquire this source-only capability.</summary>
    public static OriginalSkyAmbientSourceFrameInputs CreateAppliedOwnedSeasonSourceInputs(
        OriginalGameRepository repository, OriginalMapSourceCreatedLightingPacket acceptedLighting,
        OriginalSkySeasonMainCameraRgbFrame sourceCamera,
        OriginalMapSourceCreatedF103FeaturePacket acceptedOwnedView, string ownedSelectedRecordIdentity,
        System.Numerics.Vector4 actualPackageLightPosition, string actualPackageLightOwner,
        OriginalSkySceneSharedAtmosphere currentAtmosphere, OriginalSkySceneProducerFrame producers)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(sourceCamera);
        var packet = OriginalSkySceneSelectedViewLightSource.CaptureOwnedKind1(acceptedOwnedView,
            acceptedLighting, ownedSelectedRecordIdentity);
        if (acceptedLighting.FrameNumber != sourceCamera.Inputs.FrameNumber)
            throw new InvalidDataException("Source season Camera and owned View publication require the same accepted attempt.");
        if (!acceptedOwnedView.AcceptedCamera.Prefix.Span[..0x1D0]
            .SequenceEqual(sourceCamera.Inputs.MainFrame.Camera.Prefix.Span[..0x1D0]))
            throw new InvalidDataException("Owned View and seasonal Sky Camera require the exact same accepted Main pose, optics and post fields.");
        var source = sourceCamera.Inputs.Source;
        var moon = OriginalSky2SourceNull2D.FromAppliedSeasonOnFreshShader(source.Selection.FirstPackage,
            OriginalSkySamplingRole.Moon, OriginalSkyTextureStateScope.AppliedSourceSeasonOnFreshShader);
        var proof = OriginalSky2SourceZeroMoon.Create(repository.LoadShaderContainer("system", 0x340AC335),
            moon, acceptedLighting, packet);
        var lights = OriginalSkySceneSelectedViewLightSource.PrepareSourceZeroMoonLights(packet,
            actualPackageLightPosition, actualPackageLightOwner, proof);
        return new(acceptedLighting, source.Season.Calendar.State.ThirdDate.Month, source.SourceProvenance,
            currentAtmosphere, OriginalSkyAmbientSourceState.ResolvedCurrent, producers,
            AppliedSeason: new(source.Selection, sourceCamera.CurrentSkyCamera, lights));
    }

    /// <summary>Actual source-season application on a fresh owned Sky2 shader:
    /// Earth is the original Static622 source, Milky remains constructor-null
    /// because the selected closed source family skips its null setter. All
    /// current Camera/package/light producers are explicit and same-frame.</summary>
    public static async Task<OriginalSkyAmbientSourceFrameAssembler> CreateAppliedSeasonAsync(
        OriginalGameRepository repository,Vkd3dShaderCompiler compiler,SourceResourceRegistrationDevice registration,
        OriginalMapSourceCreatedLightingSource lighting,OriginalSkyAmbientSourceConstruction explicitRetained,
        OriginalSkyAmbientSourceFrameInputs firstAcceptedFrame,
        Func<OriginalSkyScene,Task<(OriginalSkyStarProducer? Stars,OriginalSkyCloudColorProducer? Cloud)>>? producerFactory=null,
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
        var camera=OriginalSkySceneSourceDefaults.ReadPerspectiveCamera(repository.LoadObjects("system",0x908AB6E2),lighting.Identity.CameraObjectId);
        var ambient=OriginalSkyAmbientResources.Load(repository,repository.LoadObjects("system",0xDAB638B2),0x910603AC,
            repository.LoadObjects("system",0x718CCB8B),repository.LoadObjects("system",0x93FB3280));
        var shader=repository.LoadShaderContainer("system",0x340AC335);
        var sta=OriginalStaticTextureResources.Load(repository,ambient.Resource,"system",[0xF81F0562,0xF8ED83E3,0xF9BC0264]);
        var earth=OriginalSkySeasonPackageTextures.LoadEarth(repository,applied.Selection.FirstPackage);
        if(new[]{ambient.Resource.Source,ambient.ShaderObjects.Source,shader.Source}.Any(s=>s.OverrideSource is not null)||
            sta.Any(s=>s.Resource.Source.OverrideSource is not null))
            throw new InvalidDataException("Applied source season cannot mix edited constructor resources.");
        var nullProof=OriginalSky2SourceNull2D.FromAppliedSeasonOnFreshShader(applied.Selection.FirstPackage,
            OriginalSkySamplingRole.MilkyWay,OriginalSkyTextureStateScope.AppliedSourceSeasonOnFreshShader);
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
        var native=OriginalDeferredSamplerSources.Load(repository.SourceDirectory,"TC");
        byte[] sourceSamplerKey=SourceResourceRegistrationAmbientCube.GetSamplerKey(registration.Source.AmbientCube);
        if(!OriginalDeferredSamplerSources.DecodeKey(native,sourceSamplerKey).EncodeD3D11Descriptor().AsSpan()
            .SequenceEqual(SourceResourceRegistrationAmbientCube.GetSamplerDescriptor(registration.Source.AmbientCube)))
            throw new InvalidDataException("Applied source Static Cube sampler key differs from its exact native conversion.");
        byte[] ambientKey=OriginalDeferredSamplerSources.AmbientSpecularKey(sourceSamplerKey);
        if(!ambientKey.AsSpan().SequenceEqual(OriginalDeferredSamplerSources.AmbientSpecularKey(
            OriginalDeferredSamplerSources.CubeTextureKey(native,(int)ambient.SpecularTargets[0].LevelNum))))
            throw new InvalidDataException("Applied source Static and generated Cube final consumer recipe differs.");
        byte[] sampler=OriginalDeferredSamplerSources.DecodeKey(native,ambientKey).EncodeD3D11Descriptor();
        var initial=Prepare(camera,lighting,copied,ambient.SpecularTargets[0],firstAcceptedFrame,
            OriginalSkySceneSourceDefaults.CreateSky2Parameters(ambient.SpecularTargets[0].PixelFormat),
            OriginalSkySceneSourceDefaults.CreateControllerConstructor(),null,null,true);
        var faces=OriginalSkySceneFramePreparation.Prepare(copied.SixRetainedFaces,initial.Source.SourceFrame);
        var selections=sta.Select((s,i)=>OriginalSkySceneTextureSelection.FromStatic("source-sta"+i,s,10))
            .Append(OriginalSkySceneTextureSelection.FromSampling(earth)).ToArray();
        var textures=new OriginalSkySceneTextures(selections);
        OriginalSkyScene? scene=null;OriginalSkyStarProducer? stars=null;OriginalSkyCloudColorProducer? cloud=null;Rid consumerSampler=default;
        try
        {
            var inputs=new List<OriginalSky2DeviceInput>();
            foreach(uint register in actualTextureRegisters.Where(r=>r!=nullProof.PixelRegister))
            {
                if(register<3)inputs.Add(await textures.GetRetainedInputAsync(register,"source-sta"+register));
                else if(register==3)inputs.Add(await textures.GetRetainedInputAsync(register,earth.SourceName));
                else throw new InvalidDataException("Applied source Sky2 texture register has no actual retained provider.");
            }
            scene=await OriginalSkyScene.BuildAsync(ambient,shader,copied.ProgramSelection,compiler,faces.DrawPackets,
                copied.VertexTemplate,copied.PixelTemplate,inputs,sampler,sampler,
                OriginalNativeLogTables.Load(repository.SourceDirectory,"TC"),OriginalNativeLogDispatch.AvxFma,allowReadback);
            if(producerFactory is not null)(stars,cloud)=await producerFactory(scene);
            consumerSampler=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>OriginalDeviceSampler.Create(rd,sampler));
            return new(scene,textures,registration,lighting,camera,copied,sampler,ambientKey,consumerSampler,
                firstAcceptedFrame.Lighting.Binding,stars,cloud,true);
        }
        catch(Exception buildFailure)
        {
            var partial=new OriginalNativeMapBorrowerRetirementPackage(
                firstAcceptedFrame.Lighting.Binding.ViewIdentity+"/applied-Sky-partial/"+Guid.NewGuid().ToString("N"),
                buildFailure is OriginalNativeMapBorrowerConstructionException nested?nested.RetainedPackage:null,
                cloud,stars,new OriginalNativeMapOwnedSamplerRetirement(consumerSampler),scene,textures);
            try{await partial.DisposeAsync();}
            catch(Exception retirementFailure)
            {
                partial.RetainFailure();
                throw new OriginalNativeMapBorrowerConstructionException(buildFailure,retirementFailure,partial);
            }
            throw;
        }
    }

    /// <summary>Fresh resource creation derives all proved defaults and exact
    /// source images. The first packet is prepared for shader construction but
    /// submits no draws or clock work; call SubmitFrameAsync with that accepted
    /// lighting frame. Optional real producer factory receives this WORK owner.
    /// Created producers become owned and retire before the scene.</summary>
    public static async Task<OriginalSkyAmbientSourceFrameAssembler> CreateFreshAsync(
        OriginalGameRepository repository,Vkd3dShaderCompiler compiler,
        SourceResourceRegistrationDevice registration,OriginalMapSourceCreatedLightingSource lighting,
        OriginalSkyAmbientSourceConstruction explicitRetained,
        OriginalSkyAmbientSourceFrameInputs firstAcceptedFrame,
        Func<OriginalSkyScene,Task<(OriginalSkyStarProducer? Stars,OriginalSkyCloudColorProducer? Cloud)>>? producerFactory=null,
        bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(registration);ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(explicitRetained);ArgumentNullException.ThrowIfNull(firstAcceptedFrame);
        if(string.IsNullOrWhiteSpace(explicitRetained.RetainedProvenance)||explicitRetained.ProgramSelection is null||
            explicitRetained.ProgramSelection.Sky2Combination!=0||
            explicitRetained.ProgramSelection.Sky2Operation!=OriginalSky2Operation.Atmosphere||
            explicitRetained.ProgramSelection.FreshNullTextures is not null)
            throw new InvalidDataException("Fresh source assembler requires the explicit original default PS3 pass and unassigned source-null selection.");
        await registration.ValidateAsync();registration.ValidateConstructionSource(lighting);
        var cameraDefinitions=repository.LoadObjects("system",0x908AB6E2);
        var camera=OriginalSkySceneSourceDefaults.ReadPerspectiveCamera(cameraDefinitions,lighting.Identity.CameraObjectId);
        var ambient=OriginalSkyAmbientResources.Load(repository,repository.LoadObjects("system",0xDAB638B2),0x910603AC,
            repository.LoadObjects("system",0x718CCB8B),repository.LoadObjects("system",0x93FB3280));
        var skySource=repository.LoadShaderContainer("system",0x340AC335);
        var sta=OriginalStaticTextureResources.Load(repository,ambient.Resource,"system",[0xF81F0562,0xF8ED83E3,0xF9BC0264]);
        if(camera.IsOverride||new[]{ambient.Resource.Source,ambient.ShaderObjects.Source,
            ambient.SpecularTargets[0].Source.Source,skySource.Source}.Any(s=>s.OverrideSource is not null)||
            sta.Any(s=>s.Resource.Source.OverrideSource is not null))
            throw new InvalidDataException("Fresh source assembler cannot infer constructor resources from edited source overrides.");
        foreach(var role in new[]{OriginalSkySamplingRole.Earth,OriginalSkySamplingRole.Moon,OriginalSkySamplingRole.MilkyWay})
            _=OriginalSky2SourceNull2D.FromFreshConstruction(OriginalSkySamplingResources.ReadCameraReference(
                cameraDefinitions,camera.ObjectId,role),OriginalSkyTextureStateScope.FreshSourceConstruction);
        var nulls=new[]{OriginalSkySamplingRole.Earth,OriginalSkySamplingRole.MilkyWay}.Select(role=>
            OriginalSky2SourceNull2D.FromFreshConstruction(OriginalSkySamplingResources.ReadCameraReference(
                cameraDefinitions,camera.ObjectId,role),OriginalSkyTextureStateScope.FreshSourceConstruction)).ToArray();
        var copied=explicitRetained with{SixRetainedFaces=SnapshotCaches(explicitRetained.SixRetainedFaces),
            VertexTemplate=explicitRetained.VertexTemplate.ToArray(),PixelTemplate=explicitRetained.PixelTemplate.ToArray(),
            ProgramSelection=explicitRetained.ProgramSelection with{FreshNullTextures=Array.AsReadOnly(nulls)}};
        var native=OriginalDeferredSamplerSources.Load(repository.SourceDirectory,"TC");
        byte[] staticSourceKey=SourceResourceRegistrationAmbientCube.GetSamplerKey(registration.Source.AmbientCube);
        if(!OriginalDeferredSamplerSources.DecodeKey(native,staticSourceKey).EncodeD3D11Descriptor().AsSpan()
            .SequenceEqual(SourceResourceRegistrationAmbientCube.GetSamplerDescriptor(registration.Source.AmbientCube)))
            throw new InvalidDataException("Static Cube source sampler key and original native conversion disagree.");
        byte[] ambientKey=OriginalDeferredSamplerSources.AmbientSpecularKey(staticSourceKey);
        if(!ambientKey.AsSpan().SequenceEqual(OriginalDeferredSamplerSources.AmbientSpecularKey(
            OriginalDeferredSamplerSources.CubeTextureKey(native,(int)ambient.SpecularTargets[0].LevelNum))))
            throw new InvalidDataException("This exact Static and generated Cube final consumer recipe changed.");
        byte[] sampler=OriginalDeferredSamplerSources.DecodeKey(native,ambientKey).EncodeD3D11Descriptor();
        var parameters=OriginalSkySceneSourceDefaults.CreateSky2Parameters(ambient.SpecularTargets[0].PixelFormat);
        var controller=OriginalSkySceneSourceDefaults.ApplySourceCameraControllerInputs(
            OriginalSkySceneSourceDefaults.CreateControllerConstructor(),camera);
        var initial=Prepare(camera,lighting,copied,ambient.SpecularTargets[0],firstAcceptedFrame,parameters,controller,null,null);
        var faces=OriginalSkySceneFramePreparation.Prepare(copied.SixRetainedFaces,initial.Source.SourceFrame);
        var textures=new OriginalSkySceneTextures(sta.Select((s,i)=>OriginalSkySceneTextureSelection.FromStatic("source-sta"+i,s,10)).ToArray());
        OriginalSkyScene? scene=null;OriginalSkyStarProducer? stars=null;OriginalSkyCloudColorProducer? cloud=null;Rid consumerSampler=default;
        try
        {
            var inputs=new List<OriginalSky2DeviceInput>();
            for(uint i=0;i<3;i++)inputs.Add(await textures.GetRetainedInputAsync(i,"source-sta"+i));
            scene=await OriginalSkyScene.BuildAsync(ambient,skySource,copied.ProgramSelection,compiler,
                faces.DrawPackets,copied.VertexTemplate,copied.PixelTemplate,inputs,sampler,sampler,
                OriginalNativeLogTables.Load(repository.SourceDirectory,"TC"),OriginalNativeLogDispatch.AvxFma,allowReadback);
            if(producerFactory is not null)(stars,cloud)=await producerFactory(scene);
            consumerSampler=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>OriginalDeviceSampler.Create(rd,sampler));
            return new(scene,textures,registration,lighting,camera,copied,sampler,ambientKey,consumerSampler,
                firstAcceptedFrame.Lighting.Binding,stars,cloud);
        }
        catch
        {
            if(cloud is not null)await cloud.DisposeAsync();if(stars is not null)await stars.DisposeAsync();
            if(consumerSampler.IsValid)await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(consumerSampler);return true;});
            if(scene is not null)await scene.DisposeAsync();await textures.DisposeAsync();throw;
        }
    }

    public async Task<OriginalSkyAmbientCombinedFrame> SubmitFrameAsync(uint explicitGenerationIndex,
        OriginalSkyAmbientSourceFrameInputs input,uint maximumWorkUnits,OriginalSkySceneOptionalLightInputs? optionalLight=null)
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
            if(progress.Phase is not (OriginalSkyScenePhase.Idle or OriginalSkyScenePhase.Faulted))
                throw new InvalidOperationException("Finish the combined source sky generation before staging another frame.");
            var prepared=Prepare(CameraSource,LightingSource,construction,Scene.SpecularTargets[(int)explicitGenerationIndex].Source,
                input,constructorParameters,constructorController,acceptedParameters,acceptedController,appliedSeasonScope);
            var selection=OriginalSkyAmbientCubeSourceSelection.FromDeclaredCube(Scene.SpecularTargets[(int)explicitGenerationIndex].Source,
                $"same-source-created-frame={input.Lighting.FrameNumber};{construction.RetainedProvenance}");
            var sky=await AmbientProvider.SubmitSourceFrameAsync(explicitGenerationIndex,prepared.Source,
                input.Producers,maximumWorkUnits,selection,generatedSampler,optionalLight);
            var bindings=DefaultAmbientBindings();
            current=new(input.Lighting.FrameNumber,input.Lighting,input.Lighting.Clock,
                OriginalMapSeasonMath.SelectMonth(input.SourceSeasonMonth),input.SeasonSourceProvenance,
                prepared.ControllerSource,prepared.Controller,prepared.Source,sky,bindings.Texture,bindings.Sampler);
            acceptedParameters=prepared.Parameters;acceptedController=prepared.ControllerSource;
            return current;
        }
        finally{gate.Release();}
    }

    /// <summary>Owned native World third-date adapter. Season state supplies
    /// the exact independently advanced source month; renderer clock and Sky
    /// controller phase remain separate accepted producers for this frame.</summary>
    public Task<OriginalSkyAmbientCombinedFrame> SubmitOwnedSeasonFrameAsync(uint explicitGenerationIndex,
        OriginalMapSourceCreatedLightingPacket acceptedLighting,OriginalMapSeasonRuntimeFrame ownedSeason,
        OriginalSkySceneSharedAtmosphere atmosphere,OriginalSkyAmbientSourceState skyState,
        OriginalSkySceneProducerFrame producers,uint maximumWorkUnits,
        OriginalSkySceneControllerSourceProfile? completeCurrentController=null,
        OriginalSkySceneSky2Parameters? completeCurrentParameters=null,OriginalSkySceneOptionalLightInputs? optionalLight=null)
        =>SubmitFrameAsync(explicitGenerationIndex,CreateOwnedSeasonInputs(acceptedLighting,ownedSeason,
            atmosphere,skyState,producers,completeCurrentController,completeCurrentParameters),maximumWorkUnits,optionalLight);

    /// <summary>Use the same owned calendar adapter for initial construction
    /// and later submissions; callers do not repeat the source month or its
    /// provenance. It does not derive current Sky phase from either clock.</summary>
    public static OriginalSkyAmbientSourceFrameInputs CreateOwnedSeasonInputs(
        OriginalMapSourceCreatedLightingPacket acceptedLighting,OriginalMapSeasonRuntimeFrame ownedSeason,
        OriginalSkySceneSharedAtmosphere atmosphere,OriginalSkyAmbientSourceState skyState,
        OriginalSkySceneProducerFrame producers,OriginalSkySceneControllerSourceProfile? completeCurrentController=null,
        OriginalSkySceneSky2Parameters? completeCurrentParameters=null)
    {
        ArgumentNullException.ThrowIfNull(acceptedLighting);ArgumentNullException.ThrowIfNull(ownedSeason);
        OriginalMapSeasonRuntime.ValidateOwned(ownedSeason.State);
        if(ownedSeason.FrameNumber!=acceptedLighting.FrameNumber||ownedSeason.Selection!=
            OriginalMapSeasonMath.SelectMonth(ownedSeason.State.ThirdDate.Month)||string.IsNullOrWhiteSpace(ownedSeason.SourceProvenance))
            throw new InvalidDataException("Owned native season and accepted lighting must name the same frame.");
        return new(acceptedLighting,ownedSeason.State.ThirdDate.Month,
            ownedSeason.SourceProvenance,atmosphere,skyState,producers,completeCurrentController,completeCurrentParameters);
    }

    public async Task<OriginalSkyAmbientCombinedFrame> AdvanceAsync(uint maximumWorkUnits)
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

    private (OriginalDeferredDeviceBinding Texture,OriginalDeferredDeviceBinding Sampler) DefaultAmbientBindings()
    {
        var resource=Registration.Entries[SourceResourceRegistrationAmbientCube.ObjectKey];
        return(new("Texture",7,resource.TextureRid,resource.TextureRid,resource.OwnerProvenance),
            new("Sampler",7,staticAmbientSampler,null,resource.OwnerProvenance+";D189D0-final-consumer-key="+Convert.ToHexString(ambientSamplerKey)));
    }

    public Task<OriginalSkyAmbientFrameResourceBorrow> BorrowDefaultAmbientAsync(long frameNumber,
        OriginalMapSourceCreatedLightingIdentity expectedLightingIdentity)
        =>BorrowAsync(frameNumber,expectedLightingIdentity,false);

    public Task<OriginalSkyAmbientFrameResourceBorrow> BorrowCompletedGeneratedAmbientAsync(long frameNumber,
        OriginalMapSourceCreatedLightingIdentity expectedLightingIdentity)
        =>BorrowAsync(frameNumber,expectedLightingIdentity,true);

    private async Task<OriginalSkyAmbientFrameResourceBorrow> BorrowAsync(long frameNumber,
        OriginalMapSourceCreatedLightingIdentity identity,bool generated)
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

    private static (OriginalSkySceneSourceFramePacket Source,OriginalSkySceneSky2Parameters Parameters,
        OriginalSkySceneControllerSourceProfile ControllerSource,OriginalSkyScenePreparedController Controller) Prepare(
        OriginalSkySceneCameraSourceProfile camera,OriginalMapSourceCreatedLightingSource source,
        OriginalSkyAmbientSourceConstruction construction,OriginalCubeRenderTextureDefinition target,
        OriginalSkyAmbientSourceFrameInputs input,OriginalSkySceneSky2Parameters initialParameters,
        OriginalSkySceneControllerSourceProfile initialController,OriginalSkySceneSky2Parameters? previousParameters,
        OriginalSkySceneControllerSourceProfile? previousController,bool appliedSeasonScope=false)
    {
        ArgumentNullException.ThrowIfNull(input.Lighting);ArgumentNullException.ThrowIfNull(input.SharedAtmosphere);
        ArgumentNullException.ThrowIfNull(input.Producers);
        if(input.Lighting.SourceIdentity!=source.Identity||string.IsNullOrWhiteSpace(input.SeasonSourceProvenance))
            throw new InvalidDataException("Combined sky needs this constructed lighting generation and explicit source season provenance.");
        _=OriginalMapSeasonMath.SelectMonth(input.SourceSeasonMonth);
        if(appliedSeasonScope)
        {
            var applied=input.AppliedSeason??throw new InvalidDataException("This owned shader requires its actual current applied seasonal source.");
            applied.Selection.Validate();
            ValidateAppliedStageSelection(construction.ProgramSelection,input,applied);
            if(input.SkyState!=OriginalSkyAmbientSourceState.ResolvedCurrent||input.CompleteCurrentController is not null||
                input.CompleteCurrentParameters is not null||applied.CurrentCamera.FrameNumber!=input.Lighting.FrameNumber||
                applied.Selection.Provider.Database.Action.Selection!=OriginalMapSeasonMath.SelectMonth(input.SourceSeasonMonth))
                throw new InvalidDataException("Applied source season/current View Camera and lighting require one accepted frame and season scope.");
            var current=applied.CurrentCamera;
            applied.CurrentCameraLights.SourceZeroMoon?.ValidateLighting(input.Lighting);
            var appliedController=OriginalSkySceneSourceDefaults.PrepareController(current.Controller,current.Parameters,
                applied.CurrentCameraLights.SunPresent,applied.CurrentCameraLights.MoonPresent);
            var appliedPacket=OriginalSkySceneSourceFrameProvider.PrepareAppliedSeason(applied.Selection,current,input.Lighting,target,
                applied.CurrentCameraLights,input.SharedAtmosphere,appliedController,construction.NativeClipProfile);
            return(appliedPacket,current.Parameters,current.Controller,appliedController);
        }
        if(input.AppliedSeason is not null)throw new InvalidDataException("Fresh source-only shader cannot accept an external seasonal package scope.");
        OriginalSkySceneSky2Parameters parameters;OriginalSkySceneControllerSourceProfile controller;
        switch(input.SkyState)
        {
            case OriginalSkyAmbientSourceState.FreshConstructor when previousParameters is null && previousController is null:
                if(input.CompleteCurrentController is not null||input.CompleteCurrentParameters is not null)
                    throw new InvalidDataException("Fresh constructor state cannot silently combine edited Sky inputs.");
                parameters=initialParameters;controller=initialController;break;
            case OriginalSkyAmbientSourceState.RetainedSource when previousParameters is not null && previousController is not null:
                if(input.CompleteCurrentController is not null||input.CompleteCurrentParameters is not null)
                    throw new InvalidDataException("Retained source state does not accept competing current field writers.");
                parameters=previousParameters;controller=previousController;break;
            case OriginalSkyAmbientSourceState.ResolvedCurrent:
                parameters=input.CompleteCurrentParameters??throw new InvalidDataException("Current Sky2 parameters must be complete.");
                controller=input.CompleteCurrentController??throw new InvalidDataException("Current Sky controller phase/state must be complete.");
                controller=OriginalSkySceneSourceDefaults.ApplyPreparedMainControllerInputs(controller,input.Lighting.PreparedCameraPrefix.Span);
                break;
            default:throw new InvalidDataException("Sky state must explicitly select fresh, retained or fully resolved current inputs.");
        }
        if(parameters.NativeTargetFormat!=target.PixelFormat)throw new InvalidDataException("Current Sky parameters have another source target format.");
        var lights=OriginalSkySceneLightData.ApplyOrdinaryRows(construction.RetainedLightCaches,
            input.Lighting.LocalLightData.Span,construction.SelectedLightRows);
        var prepared=OriginalSkySceneSourceDefaults.PrepareController(controller,parameters,lights.SunPresent,lights.MoonPresent);
        parameters=parameters with{MilkyStrength1484=prepared.MilkyStrength1484};
        var packet=OriginalSkySceneSourceFrameProvider.PrepareResolved(camera,input.Lighting,target,
            construction.RetainedLightCaches,construction.SelectedLightRows,input.SharedAtmosphere,new(false,false,false),
            prepared.Controller,construction.NativeClipProfile,parameters);
        packet=packet with{UsesSourceConstructorDefaults=input.SkyState==OriginalSkyAmbientSourceState.FreshConstructor,
            SourceProvenance=packet.SourceProvenance+$";source-season-month={input.SourceSeasonMonth};{input.SeasonSourceProvenance};"+
                $"sky-state={input.SkyState};sky-controller={controller.SourceProvenance};source-camera-clock-tail={input.Lighting.Clock.AutoStepExecuted}"};
        return(packet,parameters,controller,prepared);
    }

    /// <summary>Historical explicit combo0 scope remains available without a
    /// source-stage receipt. The actual source-created null Scene branch may
    /// use combo1 only with its same-frame private owned producer receipt.</summary>
    public static void ValidateAppliedStageSelection(OriginalSkySceneProgramSelection selection,
        OriginalSkyAmbientSourceFrameInputs input,OriginalSkySeasonAppliedFrameInputs applied)
    {
        ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(applied);
        if(input.SourceStages is { } stages)
        {
            stages.ValidateCurrent(input.Lighting,applied.Selection,applied.CurrentCamera);
            if(selection.Sky2Combination!=stages.Sky2Combination||
                selection.Sky2Operation!=OriginalSky2Operation.Contribution||stages.ScenePresent||
                stages.WorkContributionEnabled||input.Producers.Scene is not null||input.Producers.Contribution is not null||
                input.Producers.Sky2Enabled!=stages.Sky2Enabled)
                throw new InvalidDataException("Actual applied source stage gates disagree with the selected program or optional producers.");
            return;
        }
        if(selection.Sky2Combination!=0||selection.Sky2Operation!=OriginalSky2Operation.Atmosphere)
            throw new InvalidDataException("Applied nativepass1 requires the actual source-null Scene owned receipt; arbitrary caller combo1 is not a source gate.");
    }

    private static IReadOnlyList<OriginalSkySceneSky2FaceCache> SnapshotCaches(IReadOnlyList<OriginalSkySceneSky2FaceCache> caches)
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
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(staticAmbientSampler);return true;});
            await Scene.DisposeAsync();await textures.DisposeAsync();
        }
        finally{gate.Release();}
    }
}
