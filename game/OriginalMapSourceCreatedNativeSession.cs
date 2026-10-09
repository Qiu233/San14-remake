using System.Buffers.Binary;
using San14.Data;

public sealed record OriginalMapSourceCreatedNativeSessionCurrentInputs(
    OriginalMapCameraConstructionPacket MainCamera,
    OriginalMapSourceCreatedLightingFrameInputs? Lighting,
    OriginalDeferredF103CurrentViewInputs View,
    OriginalDeferredF103CurrentVariantInputs Variant)
{
    public OriginalMapSourceCreatedF103CurrentFrame? PreparedF103Frame { get; init; }
    public static OriginalMapSourceCreatedNativeSessionCurrentInputs FromPrepared(
        OriginalMapSourceCreatedF103CurrentFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return new(frame.Camera,null,frame.View,frame.Variant){PreparedF103Frame=frame};
    }
}
public sealed record OriginalMapSourceCreatedNativeSessionPreparedFrame(
    OriginalNativeMapFrameContext Context, OriginalMapCameraConstructionPacket MainCamera,
    OriginalMapSourceCreatedLightingPacket Lighting, OriginalMapSourceCreatedF103FeaturePacket Feature,
    OriginalDeferredF103SelectedSource Selection, OriginalMapSelectedSourceSamplerPlan Samplers)
{
    public OriginalMapSourceCreatedF103CurrentFrame? PreparedSourceFrame { get; init; }
    /// <summary>Earlier immutable source packet used by geometry. Later Q
    /// writers reselect shading without changing completed draw receipts.</summary>
    public OriginalMapSourceCreatedF103CurrentFrame? GeometrySourceFrame { get; init; }
    public OriginalMapSourceCreatedF103CurrentFrame? ActualGeometrySourceFrame => GeometrySourceFrame ?? PreparedSourceFrame;
    public OriginalMapStaticModelCurrentOwnedFrame? CpuPopulation => PreparedSourceFrame?.CpuPopulation;
}
public sealed record OriginalMapSourceCreatedNativeSessionGpuInputs(
    OriginalDeferredRefractionInputs Refraction,
    OriginalDeferredTranslucencyViewportInputs TranslucencyViewport,
    OriginalDeferredProjectionInputs Projection,
    string CameraProducerProvenance,
    IReadOnlyList<OriginalMapMaterialTerrainConsumer> TerrainMaterials,
    IReadOnlyList<OriginalDeferredAuxiliaryTargetDevice> ProducedAuxiliaryDevices,
    OriginalDeferredRasterState Raster)
{
    public OriginalMainMapCameraDeferredReadyFrame? SharedCameraReady { get; init; }
    /// <summary>Consume the source Main camera module result directly. Current
    /// viewport/texture/cache values were resolved by that producer for this
    /// same attempt; the session supplies the other three plugin groups.</summary>
    public static OriginalMapSourceCreatedNativeSessionGpuInputs FromAcceptedCamera(
        OriginalMapSourceCreatedNativeSessionPreparedFrame frame,
        OriginalMainMapCameraDeferredSourcePacket camera,
        IReadOnlyList<OriginalMapMaterialTerrainConsumer> terrainMaterials,
        IReadOnlyList<OriginalDeferredAuxiliaryTargetDevice> producedAuxiliaryDevices,
        OriginalDeferredRasterState raster)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(camera);
        if(camera.FrameNumber!=frame.Context.FrameNumber||camera.FrameNumber!=frame.MainCamera.FrameNumber)
            throw new InvalidDataException("Source Camera plugin result belongs to another accepted native attempt.");
        return new(camera.Refraction,camera.TranslucencyViewport,camera.Projection,camera.SourceProvenance,
            terrainMaterials,producedAuxiliaryDevices,raster);
    }
}

/// <summary>Concrete owned1022 source session. Creates the selected original
/// program, native GGX, exact table and sampler/image owners once. The current
/// Main/View, actual auxiliary pixel and complete active-CB producers remain
/// explicit typed boundaries. Borrow its Program in NativeMapScene.BuildAsync;
/// retire the composed scene before disposing this session.</summary>
public sealed class OriginalMapSourceCreatedNativeSession : IAsyncDisposable
{
    /// <summary>Create the expected finite source binding before compiling the
    /// program. The current F103 factory must still reselect actual1022/767;
    /// this binding neither selects a shader nor supplies current View fields.</summary>
    public static OriginalMapLightingBinding CreateExpectedDefault1022Binding(
        LoadedOriginalDeferredTexturePlan source, string viewIdentity,
        string cameraOwnerIdentity, string lightOwnerIdentity)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (string identity in new[] { viewIdentity, cameraOwnerIdentity, lightOwnerIdentity })
            ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        if (source.Node.Index != 166 || source.Node.Call.FactoryClassId != 0xF103DB00 ||
            source.Shader.Source.ResourceUri != "san14://rdb/system/9D8E811F" ||
            source.Shader.Source.DecodedSha256 != OriginalMapSourceCreatedShaderSelection.ShaderSha256 ||
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source.Shader.Source.Bytes)) !=
                OriginalMapSourceCreatedShaderSelection.ShaderSha256 ||
            source.Shader.Source.OverrideSource is not null)
            throw new InvalidDataException("Expected default binding requires the actual node166 original source shader.");
        return new(viewIdentity, cameraOwnerIdentity, lightOwnerIdentity,
            source.Shader.Source.ResourceUri + "@" + source.Shader.Source.DecodedSha256,
            1022, 4, OriginalMapLightingBasePath.UnmodifiedOrdinary);
    }

    public OriginalDeferredPixelProgram Program { get; }
    public OriginalGgxAmbientLut Ggx { get; }
    public OriginalNativeDeferredResources Resources { get; }
    public OriginalMapSelectedSourceSamplerOwner Samplers { get; }
    public OriginalMapSelectedSourceImageOwner Images { get; }
    public OriginalDeferredSelected767PluginOwner Plugins { get; }
    public OriginalMapSourceCreatedSelectedDeferredFrameAssembler Assembler { get; }
    public OriginalNativeMapModelPopulationRenderer Population => Assembler.PopulationRenderer;
    public OriginalNativeMapArmyPopulationRenderer Army { get; }
    public OriginalMapLightingBinding Binding => Assembler.LightingProvider.Binding;
    public OriginalMapSourceCreatedNativeSessionPreparedFrame? CurrentFrame { get; private set; }
    public OriginalNativeMapArmyPreparedFrame? CurrentArmyCpuFrame { get; private set; }
    public OriginalMapSourceCreatedDeferredFramePacket? LastPreparedDeferred { get; private set; }
    public OriginalDeferredSelected767ConstantPacket? LastPreparedConstants { get; private set; }
    public OriginalDeferredSelected767PluginPacket? LastPreparedPlugins { get; private set; }
    private readonly LoadedOriginalDeferredSamplerNativeSource native;
    private readonly SourceResourceRegistrationDevice registration;
    private readonly Func<OriginalNativeMapFrameContext,OriginalMapSourceCreatedNativeSessionCurrentInputs> current;
    private readonly Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,
        OriginalNativeMapModelPopulationAcceptedFrame,Task<OriginalMapSourceCreatedNativeSessionGpuInputs>> gpu;
    private readonly object lifetime=new();
    private bool retiring;
    private Task? disposal;

    private OriginalMapSourceCreatedNativeSession(OriginalDeferredPixelProgram program,OriginalGgxAmbientLut ggx,
        OriginalNativeDeferredResources resources,OriginalMapSelectedSourceSamplerOwner samplers,
        OriginalMapSelectedSourceImageOwner images,OriginalMapSourceCreatedSelectedDeferredFrameAssembler assembler,
        OriginalNativeMapArmyPopulationRenderer army,
        OriginalDeferredSelected767PluginOwner plugins,
        LoadedOriginalDeferredSamplerNativeSource native,SourceResourceRegistrationDevice registration,
        Func<OriginalNativeMapFrameContext,OriginalMapSourceCreatedNativeSessionCurrentInputs> current,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,OriginalNativeMapModelPopulationAcceptedFrame,
            Task<OriginalMapSourceCreatedNativeSessionGpuInputs>> gpu)
        =>(Program,Ggx,Resources,Samplers,Images,Assembler,Army,Plugins,this.native,this.registration,this.current,this.gpu)=
            (program,ggx,resources,samplers,images,assembler,army,plugins,native,registration,current,gpu);

    public static async Task<OriginalMapSourceCreatedNativeSession> BuildDefault1022Async(
        OriginalGameRepository repository,Vkd3dShaderCompiler borrowedCompiler,
        OriginalMapSourceCreatedLightingDeviceOwner borrowedLighting,
        OriginalNativeMapModelPopulationRenderer borrowedPopulation,OriginalNativeMapArmyPopulationRenderer borrowedArmy,
        OriginalMapMaterialCachePrepared materials,
        OriginalDeferredF103SelectedSource bootstrapSelection,
        Func<OriginalNativeMapFrameContext,OriginalMapSourceCreatedNativeSessionCurrentInputs> current,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,OriginalNativeMapModelPopulationAcceptedFrame,
            Task<OriginalMapSourceCreatedNativeSessionGpuInputs>> gpu)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(borrowedCompiler);
        ArgumentNullException.ThrowIfNull(borrowedLighting);ArgumentNullException.ThrowIfNull(borrowedPopulation);
        ArgumentNullException.ThrowIfNull(borrowedArmy);
        ArgumentNullException.ThrowIfNull(materials);ArgumentNullException.ThrowIfNull(bootstrapSelection);
        ArgumentNullException.ThrowIfNull(current);ArgumentNullException.ThrowIfNull(gpu);
        bootstrapSelection.Validate();
        if(bootstrapSelection.Combination.Index!=1022||bootstrapSelection.PixelProgramIndex!=767||
            bootstrapSelection.Camera.Source.Camera.ObjectId!=0x38D04576||
            bootstrapSelection.OwnedFeatureSource is not { } feature||
            feature.LightingSourceIdentity!=borrowedLighting.Source.Identity)
            throw new InvalidDataException("Production default session requires actual owned Main38D/kind1 feature and original1022/767.");
        var native=OriginalDeferredSamplerSources.Load(repository.SourceDirectory,bootstrapSelection.Camera.ExecutableLocale);
        var pairs=borrowedLighting.Source.CreateFreshSourceNullImages(
            OriginalDeferredImageStateScope.FreshSourceConstructionNoAssignments).Where(p=>p.PixelRegister==6).ToArray();
        var program=await OriginalDeferredPixelProgram.BuildAsync(bootstrapSelection,borrowedCompiler,pairs);
        OriginalGgxAmbientLut? ggx=null;OriginalNativeDeferredResources? resources=null;
        OriginalMapSelectedSourceSamplerOwner? samplers=null;OriginalMapSelectedSourceImageOwner? images=null;
        OriginalMapSourceCreatedSelectedDeferredFrameAssembler? assembler=null;
        try
        {
            ggx=await OriginalGgxAmbientLut.BuildAsync(OriginalEmbeddedGgxShader.Load(
                bootstrapSelection.Camera.ExecutableLocale),borrowedCompiler);
            resources=await OriginalNativeDeferredResources.BuildSelectedAsync(program,materials.Table,ggx);
            var imagePlan=OriginalMapSelectedSourceImagePlan.Read(bootstrapSelection);
            var initialSamplers=PrepareSamplers(program,native,borrowedLighting.Registration,imagePlan);
            samplers=await OriginalMapSelectedSourceSamplerOwner.BuildAsync(initialSamplers);
            images=OriginalMapSelectedSourceImageOwner.FromSource(imagePlan);
            var binding=OriginalMapSourceCreatedSelectedDeferredFrameAssembler.CreateSourceBinding(program,
                feature.Binding.ViewIdentity,feature.Binding.CameraOwnerIdentity,feature.Binding.LightOwnerIdentity);
            assembler=await OriginalMapSourceCreatedSelectedDeferredFrameAssembler.FromSourceOwnersAsync(program,
                borrowedLighting.Registration,borrowedLighting.Source,binding,resources,samplers,images,null,
                materials,borrowedPopulation);
            var plugins=OriginalDeferredSelected767PluginOwner.CreateAfterSuccessfulGraphicsInitialization(
                OriginalDeferredSamplerSources.LoadSelected767PluginSource(repository.SourceDirectory,
                    bootstrapSelection.Camera.ExecutableLocale),bootstrapSelection.SourcePlan);
            return new(program,ggx,resources,samplers,images,assembler,borrowedArmy,plugins,native,borrowedLighting.Registration,current,gpu);
        }
        catch
        {
            if(assembler is not null)await assembler.DisposeAsync();
            if(images is not null)await images.DisposeAsync();if(samplers is not null)await samplers.DisposeAsync();
            if(resources is not null)await resources.DisposeAsync();if(ggx is not null)await ggx.DisposeAsync();
            await program.DisposeAsync();throw;
        }
    }

    /// <summary>Attach once. The session owns the single population submission;
    /// Army snapshot and population submissions run here once; ordinary
    /// model geometry runs before terrain, matching source CPU.v8. Preceding stages
    /// must not submit either borrowed owner a second time.</summary>
    public OriginalNativeMapSceneStages Stages(OriginalNativeMapSceneStages? preceding=null,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,Task>? afterCurrentPrepared=null)
    {
        var coordinated=(preceding ?? new OriginalNativeMapSceneStages()) with
        {
            BeforeTerrain = async context=>
            {
                RequireCurrent(context);
                await Population.PrepareAndDrawAsync(context);
                await Army.PrepareAndDrawAsync(context);
                if(preceding?.BeforeTerrain is { } beforeTerrain)await beforeTerrain(context);
            },
            BeforeTargetRetirement = async context=>
            {
                CurrentFrame=null;CurrentArmyCpuFrame=null;LastPreparedDeferred=null;LastPreparedConstants=null;LastPreparedPlugins=null;
                await Army.BeforeTargetRetirementAsync(context);
                await Population.BeforeTargetRetirementAsync(context);
                if(preceding?.BeforeTargetRetirement is { } retire)await retire(context);
            },
            BeforeFrame = async context=>
            {
                if(preceding?.BeforeFrame is { } before)await before(context);
                CurrentArmyCpuFrame=await Army.CaptureBeforeFrameAsync(context);
                PrepareCurrent(context);
                if(afterCurrentPrepared is not null)await afterCurrentPrepared(RequireCurrent(context));
            }
        };
        return Assembler.Stages(coordinated);
    }

    public void PrepareCurrent(OriginalNativeMapFrameContext context)
    {
        lock(lifetime)ObjectDisposedException.ThrowIf(retiring,this);
        var input=current(context)??throw new InvalidDataException("Current owned Main/View source producer is unavailable.");
        if(input.MainCamera.Source.Camera.ObjectId!=0x38D04576||input.MainCamera.FrameNumber!=context.FrameNumber||
            context.PreparedCamera is null||OriginalPreparedCameraSceneSource.ReadCompletePrefix(input.MainCamera.Prefix.Span)!=context.PreparedCamera||
            input.View.ViewIdentity!=Binding.ViewIdentity||input.View.CameraOwnerIdentity!=Binding.CameraOwnerIdentity||
            input.View.SelectedRecordLightOwnerIdentity!=Binding.LightOwnerIdentity)
            throw new InvalidDataException("Session Main/View/current record belongs to a different accepted camera, owner or attempt.");
        OriginalMapSourceCreatedLightingPacket lighting;
        OriginalMapSourceCreatedF103FeaturePacket feature;
        OriginalDeferredF103SelectedSource selected;
        if(input.PreparedF103Frame is { } prepared)
        {
            if(input.Lighting is not null||prepared.FrameNumber!=context.FrameNumber||
                prepared.Lighting.SourceIdentity!=Assembler.LightingProvider.Source.Identity||
                prepared.Lighting.Binding!=Binding||input.View!=prepared.View||input.Variant!=prepared.Variant||
                !input.MainCamera.Prefix.Span.SequenceEqual(prepared.Camera.Prefix.Span)||
                !ReferenceEquals(prepared.Selection.OwnedFeatureSource,prepared.Feature))
                throw new InvalidDataException("Borrowed F103 preparation differs from the same source lighting, owner, Camera or current attempt.");
            prepared.Feature.ValidateFrame(prepared.Camera,prepared.Lighting);prepared.Selection.Validate();
            if(prepared.Lighting.SceneInputs.Camera!=context.PreparedCamera)
                throw new InvalidDataException("Borrowed source lighting differs from the already accepted geometry Camera.");
            lighting=prepared.Lighting;feature=prepared.Feature;selected=prepared.Selection;
        }
        else
        {
            var initial=input.Lighting??throw new InvalidDataException("Current source lighting input or an exact prepared F103 packet is required.");
            lighting=Assembler.PrepareLightingFrame(context,initial with{Binding=Binding,FrameNumber=context.FrameNumber});
            feature=OriginalMapSourceCreatedF103Feature.PrepareFirstKind1(input.MainCamera,lighting,input.View);
            selected=OriginalMapSourceCreatedShaderSelection.PrepareFreshShader(Program.SourceSelection!.SourcePlan,
                feature,lighting,input.Variant);
        }
        var scene=lighting.SceneInputs;
        if(scene.Occlusion is not null||scene.Refraction is not null||scene.ShadowFade is not null||
            scene.Hatching is not null||scene.TranslucencyViewport is not null||scene.Projection is not null)
            throw new InvalidDataException("Session additional PS767 writers belong to the same-frame Camera/plugin constant factories.");
        Program.ValidateCompatibleCurrentSelection(selected);
        var samplers=PrepareSamplers(Program,native,registration,OriginalMapSelectedSourceImagePlan.Read(selected));
        _=Samplers.GetBindings(samplers);
        CurrentFrame=new(context,input.MainCamera.CopyAcceptedSnapshot(),lighting,feature,selected,samplers)
            {PreparedSourceFrame=input.PreparedF103Frame};
    }

    /// <summary>Use as the scene's serial prepareDeferred callback. Actual
    /// source pixels and the three Camera plugin groups are supplied by their
    /// production factories. Occ/Fade/Hatching are produced by this session's
    /// exact fresh plugin owner. The complete active PS767 CB is constructed
    /// from these groups and the accepted Camera/light generation.</summary>
    public async Task<OriginalNativeDeferredPacket> PrepareDeferredAsync(OriginalNativeMapFrameContext context)
    {
        var frame=RequireCurrent(context);
        var population=Population.LastAcceptedFrame??throw new InvalidDataException("Session population has not completed this accepted frame.");
        if(!ReferenceEquals(population.Context,context))throw new InvalidDataException("Session model frame belongs to an older attempt.");
        if(frame.CpuPopulation is { } cpu&&!ReferenceEquals(population.Frame.Visibility,cpu.Visibility))
            throw new InvalidDataException("GPU population differs from the exact CPU population that published current F103 counters.");
        if(Army.LastAcceptedFrame is not { } army||!ReferenceEquals(army.Prepared.Context,context))
            throw new InvalidDataException("Session ordinary Army/flag population has not completed this accepted frame.");
        var produced=await gpu(frame,population)??throw new InvalidDataException("Source auxiliary pixel/active constant producers are unavailable.");
        var plugins=Plugins.Prepare(frame.Selection,frame.Lighting);
        var constants=OriginalDeferredSelected767Constants.Create(frame.Selection,frame.Lighting,
            plugins.Complete(produced.Refraction,produced.TranslucencyViewport,produced.Projection,
                produced.CameraProducerProvenance));
        var transparency=ReadTransparency(frame.Selection);
        var geometry=OriginalMapSourceCreatedDeferredFrameInputs.FromAcceptedPopulation(population,produced.TerrainMaterials,
            constants.Constants,[],transparency,[],produced.Raster);
        var packet=await Assembler.PrepareDeferredAsync(context,frame.Lighting,
            new(frame.Selection,frame.Samplers,geometry,produced.ProducedAuxiliaryDevices));
        LastPreparedPlugins=plugins;LastPreparedConstants=constants;LastPreparedDeferred=packet;return packet.Deferred;
    }

    private OriginalMapSourceCreatedNativeSessionPreparedFrame RequireCurrent(OriginalNativeMapFrameContext context)
    {
        lock(lifetime)ObjectDisposedException.ThrowIf(retiring,this);
        var frame=CurrentFrame??throw new InvalidDataException("Session BeforeFrame source preparation has not completed.");
        if(!ReferenceEquals(frame.Context,context))throw new InvalidDataException("Session source state belongs to another serial render attempt.");
        return frame;
    }
    private static OriginalDeferredTransparencyImageSelection ReadTransparency(OriginalDeferredF103SelectedSource selected)
    {
        var feature=selected.OwnedFeatureSource??throw new InvalidDataException("Session transparency requires actual kind1 LightData.");
        return new(BinaryPrimitives.ReadInt32LittleEndian(feature.EffectiveLocalLightData.Span.Slice(0xED8,4)),
            BinaryPrimitives.ReadInt32LittleEndian(selected.Camera.Prefix.Span.Slice(0x8C,4)));
    }
    private static OriginalMapSelectedSourceSamplerPlan PrepareSamplers(OriginalDeferredPixelProgram program,
        LoadedOriginalDeferredSamplerNativeSource native,SourceResourceRegistrationDevice registration,
        OriginalMapSelectedSourceImagePlan images)
    {
        var selection=images.Selection;
        var proofs=program.SelectedSourceNullSpecialization??throw new InvalidDataException("Owned default Hatching source proof is unavailable.");
        var currentNull=OriginalDeferredSelectedSourceNullAdapter.Adapt(program.OriginalTranslation,program.SourceDescriptorBindings,
            selection,proofs.ProofPairs,proofs.SourceNullIes,proofs.SourceLocalAmbient);
        var keys=new List<OriginalMapSelectedSamplerSourceInput>();
        foreach(var target in new[]{images.GraphTargets.Single(t=>t.Role=="RefractionDepth"),
            images.SelectSpecular(ReadTransparency(selection)),images.GraphTargets.Single(t=>t.Role=="BlurredDepth")})
            keys.Add(OriginalMapSelectedSourceSamplerPlan.FromRenderTexture(images,native,target.EncodedParameter,target.Texture));
        var cube=registration.Entries[SourceResourceRegistrationAmbientCube.ObjectKey];
        keys.Add(OriginalMapSelectedSourceSamplerPlan.FromProducerKey(images,native,27,cube.Source.Definitions.Source,
            "object/5A53AD58/actual-kind1-FE0",OriginalDeferredSamplerSources.AmbientSpecularKey(
                SourceResourceRegistrationAmbientCube.GetSamplerKey(cube.Source))));
        return OriginalMapSelectedSourceSamplerPlan.Resolve(images,native,program.OriginalTranslation,keys,currentNull);
    }

    public ValueTask DisposeAsync(){lock(lifetime){retiring=true;return new(disposal??=RetireAsync());}}
    private async Task RetireAsync()
    {
        CurrentFrame=null;CurrentArmyCpuFrame=null;LastPreparedDeferred=null;LastPreparedConstants=null;LastPreparedPlugins=null;
        await Assembler.DisposeAsync();await Images.DisposeAsync();await Samplers.DisposeAsync();
        await Resources.DisposeAsync();await Ggx.DisposeAsync();await Program.DisposeAsync();
    }
}
