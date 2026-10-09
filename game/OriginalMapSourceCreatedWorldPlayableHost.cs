using Godot;
using San14.Data;

/// <summary>Current five-root World producers. Source receipts and device
/// outputs remain explicit and are validated before any presentation commit.</summary>
public sealed record OriginalMapSourceCreatedWorldPlayableCurrentSources(
    Func<OriginalNativeMapFrameContext, OriginalNativeMapArmyPreparedFrame,
        OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame> PrepareCurrent,
    Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,
        OriginalDeferredEarlySourceViewQueriesPacket> PrepareEarlyCurrent,
    Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalNativeMapModelPopulationAcceptedFrame,
        OriginalDeferredEarlySourceCompletedFrame, OriginalMapSourceCreatedPlayableAuxiliaryOwners,
        Task<OriginalMapSourceCreatedPlayableAuxiliaryCompletion>> ProduceAuxiliary,
    Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,
        OriginalMapFinalOutputProductionFrameInputs> PrepareFinal)
{
    /// <summary>Prepare the current Sky with the same retained lighting scope.</summary>
    public Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,
        OriginalDirectionalLightRetainedConsumerReceipt?, Task>? AfterCurrentPreparedWithLighting { get; init; }
    /// <summary>Borrow the actual currently selected Q80 Sky wrapper. The
    /// same resolver serves early and ordered late programs; allocated-only
    /// native selections remain rejected rather than replaced.</summary>
    public required Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,
        OriginalSkyDirectionalAmbientDeviceBinding> SelectCurrentAmbient { get; init; }
    /// <summary>Other admitted original77/79/80 programs require their actual
    /// current owners. Ordinary78 is constructed automatically by the host.</summary>
    public Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalDeferredEarlyF103SelectedSource,
        OriginalDeferredPixelProgram, Task<OriginalDeferredEarlySourcePassInputs>>? PrepareOtherEarlyPass { get; init; }
    /// <summary>Prepare the actual source terrain raster from the same current
    /// Session packet. Host owns its hook and later joins actual submissions;
    /// callers need no separate completion callback or shared raster holder.</summary>
    public Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, TerrainGbufferRasterState,
        Task<OriginalNativeTerrainStencilPreparedFrame>>? PrepareTerrainNativeStencil { get; init; }
    /// <summary>Submit current static queues through the borrowed population
    /// renderer. Its real accepted aggregate feeds Terrain and later stages.</summary>
    public Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,
        OriginalNativeMapModelPopulationRenderer, Task>? SubmitStaticGeometry { get; init; }
    /// <summary>Ordered World48/49/50/51/52/53/54/55 submission using the
    /// same Army CPU capture that supplied this View's primitive counters.</summary>
    public Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,
        OriginalNativeMapArmyPreparedFrame, OriginalMapSourceCreatedWorldPlayableHost, Task>? SubmitWorldGeometryBeforeTerrain { get; init; }
    /// <summary>Run the actual remaining geometry CPU writers after terrain,
    /// then return the same World's current shading preparation.</summary>
    public Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,
        OriginalMapSourceCreatedWorldPlayableHost, Task<OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame>>? CompleteGeometryAndPrepareShading { get; init; }
    public Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,
        OriginalWorldGeometryDepthSamplingView>? RequireGeometryDepthSampling { get; init; }
    /// <summary>Borrow this Host's final chain for the frontend's cross-frame
    /// mask history. The returned owner performs the sole final draw.</summary>
    public Func<OriginalMapFinalOutputProductionFactory,
        OriginalMapSourceCreatedGraphicsColorMaskOwner>? CreateGraphicsColorMaskOwner { get; init; }

    /// <summary>Use the concrete original auxiliary pixel factory. The final
    /// callback executes source139/140 or its proved current no-op owners;
    /// ordered target receipts are subsequently required by ReadySource.</summary>
    public static OriginalMapSourceCreatedWorldPlayableCurrentSources FromSourcePixelFactory(
        Func<OriginalNativeMapFrameContext, OriginalNativeMapArmyPreparedFrame,
            OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame> prepareCurrent,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalDeferredEarlySourceViewQueriesPacket> prepareEarlyCurrent,
        OriginalNativeMapArmyPopulationRenderer borrowedArmy,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalNativeMapModelPopulationAcceptedFrame,
            OriginalDeferredEarlySourceCompletedFrame,
            Task<OriginalMapSourceCreatedPlayableAuxiliarySourceFrameInputs>> prepareAuxiliary,
        Func<OriginalDeferredAuxiliarySourcePixelStages, OriginalNativeMapModelPopulationAcceptedFrame,
            OriginalNativeMapArmyPopulationAcceptedFrame, Task> completeRefractionGeometry,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalMapFinalOutputProductionFrameInputs> prepareFinal,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalSkyDirectionalAmbientDeviceBinding> selectCurrentAmbient)
    {
        ArgumentNullException.ThrowIfNull(borrowedArmy);ArgumentNullException.ThrowIfNull(prepareAuxiliary);
        ArgumentNullException.ThrowIfNull(completeRefractionGeometry);
        ArgumentNullException.ThrowIfNull(prepareCurrent);ArgumentNullException.ThrowIfNull(prepareEarlyCurrent);
        ArgumentNullException.ThrowIfNull(prepareFinal);ArgumentNullException.ThrowIfNull(selectCurrentAmbient);
        return new(prepareCurrent,prepareEarlyCurrent,async(frame,population,early,owners)=>
        {
            var army=borrowedArmy.LastAcceptedFrame??throw new InvalidDataException("Current source Army geometry is unavailable.");
            if(!ReferenceEquals(army.Prepared.Context,frame.Context))
                throw new InvalidDataException("Source pixel inputs belong to another Army/Scene attempt.");
            var inputs=await prepareAuxiliary(frame,population,early)??throw new InvalidDataException("Current typed auxiliary source inputs are unavailable.");
            var stages=await OriginalDeferredAuxiliarySourcePixelFactory.ProducePixelStagesAsync(
                frame,population,army,early,owners,inputs.Pixels);
            await completeRefractionGeometry(stages,population,army);
            return new(frame,early,inputs.TerrainMaterials.ToArray(),inputs.Raster){SourcePixelStages=stages};
        },prepareFinal) { SelectCurrentAmbient = selectCurrentAmbient };
    }

    /// <summary>Complete the original mode16 source-null branches using the
    /// same geometry renderers' actual final-parent and submission snapshots.
    /// Unknown/non-null families reject; they are not given an empty callback.</summary>
    public static OriginalMapSourceCreatedWorldPlayableCurrentSources FromSourceNullGeometryPixelFactory(
        Func<OriginalNativeMapFrameContext, OriginalNativeMapArmyPreparedFrame,
            OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame> prepareCurrent,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalDeferredEarlySourceViewQueriesPacket> prepareEarlyCurrent,
        OriginalNativeMapArmyPopulationRenderer borrowedArmy,
        OriginalDeferredSourceViewFamilyOwner borrowedViewFamilies,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalNativeMapModelPopulationAcceptedFrame,
            OriginalDeferredEarlySourceCompletedFrame,
            Task<OriginalMapSourceCreatedPlayableAuxiliarySourceFrameInputs>> prepareAuxiliary,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalMapFinalOutputProductionFrameInputs> prepareFinal,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalSkyDirectionalAmbientDeviceBinding> selectCurrentAmbient)
    {
        ArgumentNullException.ThrowIfNull(borrowedViewFamilies);
        return FromSourcePixelFactory(prepareCurrent,prepareEarlyCurrent,borrowedArmy,prepareAuxiliary,
            async(stages,population,army)=>
            {
                var publication=OriginalDeferredSourceViewFamilyCurrentFactory.Prepare(
                    borrowedViewFamilies,stages.AcceptedFrame,population,army);
                await OriginalDeferredRefractionSourceGeometryProducer.CompleteSourceNullAsync(
                    stages,population,army,publication);
            },prepareFinal,selectCurrentAmbient);
    }
}

/// <summary>Independent Directional five-root World playable map host.
/// Owns geometry/early/auxiliary/ordered late/final transactions, and borrows
/// actual lighting and Sky owners without changing native wrapper selection.</summary>
public sealed class OriginalMapSourceCreatedWorldPlayableHost : IAsyncDisposable
{
    public OriginalNativeMapSurface Surface { get; }
    public OriginalNativeMapScene Scene { get; }
    public OriginalSkyDirectionalAmbientDeviceOwner BorrowedSky { get; }
    public OriginalMapSourceCreatedWorldNativeSession Session { get; }
    public IReadOnlyList<OriginalNativeMapStageTiming> LastConsumerPreparationTimings { get; private set; }=[];
    public IReadOnlyList<OriginalNativeMapStageTiming> LastEarlyAuxiliaryTimings { get; private set; }=[];
    public OriginalDeferredEarlySourcePixelFactory Early { get; }
    public OriginalMapFinalOutputProductionFactory Final { get; }
    public OriginalMapSourceCreatedGraphicsColorMaskOwner? GraphicsColorMaskOwner { get; private set; }
    public OriginalMapSourceCreatedPlayableAuxiliaryOwners Auxiliary { get; private set; }
    public OriginalMainMapViewportDepthSource Viewport { get; }
    public OriginalDeferredEarlyCausticsOwner Caustics { get; }
    public OriginalDirectionalLightRetainedNormalization? RetainedNormalization { get; }
    /// <summary>Native node1 selected named terminal, validated per current
    /// accepted World frame. Null only for explicit allocation diagnostics.</summary>
    public OriginalMapSceneProxySourcePolicy? SourceSceneProxyPolicy { get; }
    public bool UsesExplicitResolutionDiagnostic => SourceSceneProxyPolicy is null;
    public OriginalDirectionalLightRetainedConsumerReceipt? CurrentConsumerReceipt { get; private set; }
    public OriginalDeferredEarlySourceCompletedFrame? LastEarlyFrame { get; private set; }
    // The canvas owns its last completed snapshot independently of a final
    // target that a resize or failed attempt may have already retired.
    public bool HasAcceptedOutput => Surface.HasAcceptedOutput;
    private OriginalMapPostEffect3WorldRenderLoop? postEffect3Loop;
    internal bool HasCompletedCurrentFinal(OriginalNativeMapFrameContext context)=>
        postEffect3Loop is{} loop?loop.HasCompleted(context):Final.LastAcceptedConstants is not null;
    private readonly OriginalGameRepository repository;
    private readonly Vkd3dShaderCompiler compiler;
    private readonly OriginalMapSourceCreatedWorldPlayableCurrentSources current;
    private readonly string executableLocale;
    private readonly bool allowReadback;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object lifetime = new();
    private readonly Dictionary<long, OriginalMainMapCameraDeferredReadyFrame> cameraLeases = [];
    private bool retiring;
    private Task? disposal;
    private IReadOnlyList<OriginalDeferredEarlyF103SelectedSource>? currentEarlySelections;
    private IReadOnlyList<OriginalDeferredEarlyWorldPreparedPass>? currentWorldEarlyPasses;
    private OriginalDeferredEarlySourceViewQueriesPacket? currentEarlyQueries;
    private OriginalNativeTerrainStencilPreparedFrame? currentTerrainStencil;
    private OriginalMapSourceCreatedPlayableAuxiliaryCompletion? currentAuxiliaryCompletion;

    private OriginalMapSourceCreatedWorldPlayableHost(OriginalGameRepository repository, Vkd3dShaderCompiler compiler,
        OriginalMapSourceCreatedWorldPlayableCurrentSources current, string locale, bool allowReadback,
        OriginalMapSourceCreatedWorldNativeSession session, OriginalDeferredEarlySourcePixelFactory early,
        OriginalMapFinalOutputProductionFactory final, OriginalMapSourceCreatedPlayableAuxiliaryOwners auxiliary,
        OriginalMainMapViewportDepthSource viewport, OriginalDeferredEarlyCausticsOwner caustics,
        OriginalDirectionalLightRetainedNormalization? normalization,
        OriginalNativeMapScene scene, OriginalNativeMapSurface surface, OriginalSkyDirectionalAmbientDeviceOwner borrowedSky,
        OriginalMapSceneProxySourcePolicy? sceneProxy)
        => (this.repository, this.compiler, this.current, executableLocale, this.allowReadback,
            Session, Early, Final, Auxiliary, Viewport, Caustics, RetainedNormalization, Scene, Surface, BorrowedSky,SourceSceneProxyPolicy) =
            (repository, compiler, current, locale, allowReadback, session, early, final, auxiliary, viewport, caustics, normalization, scene, surface, borrowedSky,sceneProxy);

    /// <summary>The bootstrap is an actual owned source selection and CPU
    /// publication. Subsequent callbacks receive the serial Scene attempt and
    /// captured Army CPU frame; they cannot predict Scene.FrameNumber+1.</summary>
    public static async Task<OriginalMapSourceCreatedWorldPlayableHost> BuildAsync(
        OriginalGameRepository repository, Vkd3dShaderCompiler borrowedCompiler,
        OriginalMapSourceCreatedPlayableSceneSource sceneSource,
        OriginalMapSourceCreatedDirectionalLightingDeviceOwner borrowedLighting,
        OriginalSkyDirectionalAmbientDeviceOwner borrowedSky,
        OriginalNativeMapModelPopulationRenderer borrowedPopulation,
        OriginalNativeMapArmyPopulationRenderer borrowedArmy, OriginalMapMaterialCachePrepared materials,
        OriginalMapSourceCreatedF103CurrentFrame bootstrap,
        OriginalDeferredEarlySourceViewQueriesPacket bootstrapEarly,
        OriginalMapSourceCreatedWorldPlayableCurrentSources current,
        OriginalMapSourceCreatedPlayableTerrainFrame firstFrame,
        OriginalNativeMapSceneStages? preceding = null,
        OriginalMapExposureOwner? borrowedExposure = null, bool allowReadback = false,
        Action<OriginalDeferredEarlySourcePixelFactory>? configureEarlyDiagnostics = null,
        OriginalRenderTextureResolutionFrame? resolvedTargets = null,
        Func<OriginalMapSourceCreatedWorldPlayableHost,Task>? prepareFirstWorldConsumers = null,
        Func<OriginalMapSourceCreatedWorldPlayableHost,OriginalNativeMapFrameContext,
            OriginalMapFinalOutputProductionPrepared,Task>? afterWorldExposureBeforeFinal = null,
        OriginalMapSceneProxySourcePolicy? sourceSceneProxyPolicy = null,
        OriginalMapPostEffect3WorldRenderLoop? postEffect3Loop = null)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(borrowedCompiler);
        ArgumentNullException.ThrowIfNull(sceneSource); ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(bootstrap); ArgumentNullException.ThrowIfNull(bootstrapEarly);
        ArgumentNullException.ThrowIfNull(firstFrame);
        ArgumentNullException.ThrowIfNull(borrowedLighting);ArgumentNullException.ThrowIfNull(borrowedSky);
        ArgumentNullException.ThrowIfNull(borrowedPopulation);ArgumentNullException.ThrowIfNull(borrowedArmy);
        ArgumentNullException.ThrowIfNull(materials);
        sceneSource = sceneSource with
        {
            TerrainExtents = sceneSource.TerrainExtents.ToDictionary(p => p.Key, p => p.Value),
            OutputExtents = sceneSource.OutputExtents.ToDictionary(p => p.Key, p => p.Value)
        };
        firstFrame = SnapshotTerrainFrame(firstFrame);
        bootstrap.Selection.Validate(); bootstrap.Feature.ValidateFrame(bootstrap.Camera, bootstrap.Lighting);
        if (bootstrap.Selection.Combination.Index != 510 || bootstrap.Selection.PixelProgramIndex != 383 ||
            bootstrap.Camera.Source.Camera.ObjectId != OriginalMainMapCameraAdapter.MainCameraObjectId ||
            !borrowedLighting.Sources.Any(source => source.Identity == bootstrap.Lighting.SourceIdentity) || bootstrap.CpuPopulation is null)
            throw new InvalidDataException("Playable bootstrap requires actual Main38D/current source publication selecting510/383 and its CPU population.");
        ValidateCurrentSources(current);
        if(borrowedLighting.RetainedNormalization is not null && current.AfterCurrentPreparedWithLighting is null)
            throw new OriginalMapSourceCreatedPlayableSourceUnavailableException(
                "independent current Sky atmosphere producer",
                OriginalMapSourceCreatedPlayableRequiredInput.CurrentSkyAtmosphereOwner);
        if (borrowedSky.Binding != bootstrap.Lighting.Binding || borrowedSky.RequiresRetirement)
            throw new InvalidDataException("World playable host requires its actual live Directional Sky/View owner.");
        if(sourceSceneProxyPolicy is not null)
        {
            if(resolvedTargets is not null && !ReferenceEquals(resolvedTargets,sourceSceneProxyPolicy.Resolutions))
                throw new InvalidDataException("Explicit diagnostic resolutions cannot claim another native Scene proxy policy.");
            sourceSceneProxyPolicy.ValidateCurrent(bootstrap);
            resolvedTargets=sourceSceneProxyPolicy.Resolutions;
        }
        else if(resolvedTargets is null)
        {
            sourceSceneProxyPolicy=OriginalMapSceneProxySourcePolicy.LoadFreshWorld(repository,bootstrap,
                bootstrap.Lighting.Binding.ViewIdentity+"/node1-Scene-proxy/"+Guid.NewGuid().ToString("N"));
            resolvedTargets=sourceSceneProxyPolicy.Resolutions;
        }
        Vector2I extent = ReadNativeExtent(sceneSource.TerrainExtents, sceneSource.OutputExtents);
        var bootEarly = PrepareEarlySelections(repository, bootstrap, bootstrapEarly);
        var bootOrdinary = bootEarly.Single(s => s.SourcePlan.Node.Index == 78);
        var caustics = OriginalDeferredEarlyCausticsOwner.CreateFresh(
            OriginalDeferredSamplerSources.LoadEarlyCaustics(repository.SourceDirectory, bootstrap.Camera.ExecutableLocale),
            bootOrdinary.SourcePlan, OriginalDeferredEarlyCausticsViewScope.FreshPreparedView1B90AbsentNoExternalAssignments);
        var bootCaustics = caustics.Prepare(bootOrdinary, bootstrap);
        OriginalMapSourceCreatedWorldNativeSession? session = null;
        OriginalDeferredEarlySourcePixelFactory? early = null;
        OriginalMapFinalOutputProductionFactory? final = null;
        OriginalMapSourceCreatedPlayableAuxiliaryOwners? auxiliary = null;
        OriginalNativeMapScene? scene = null;
        OriginalNativeMapSurface? surface = null;
        OriginalMapSourceCreatedWorldPlayableHost? host = null;
        try
        {
            session = await OriginalMapSourceCreatedWorldNativeSession.BuildAsync(repository, borrowedCompiler,
                borrowedLighting, borrowedPopulation, borrowedArmy, materials, bootstrap,
                (context, army) => current.PrepareCurrent(context, army)
                    ?? throw new InvalidDataException("Current source World publication is unavailable."),
                (frame, population) => host!.PrepareGpuAsync(frame, population),
                frame => host!.SelectCurrentAmbient(frame));
            if (bootstrap.Lighting.Binding != session.Binding)
                throw new InvalidDataException("Bootstrap lighting and World Session must share the same owner binding.");
            early = await OriginalDeferredEarlySourcePixelFactory.BuildDirectionalAsync(bootEarly, borrowedCompiler,
                session.Ggx, session.Samplers, frame => host!.SelectCurrentAmbient(frame), bootCaustics.SourceNull);
            auxiliary = await OriginalMapSourceCreatedPlayableAuxiliaryOwners.BuildAsync(repository, borrowedCompiler,
                bootstrap.Camera.ExecutableLocale, extent, allowReadback);
            final = await OriginalMapFinalOutputProductionFactory.BuildAsync(repository,
                OriginalMapFinalOutputResources.Load(repository), bootstrap.Camera.Source, borrowedCompiler,
                borrowedExposure, allowReadback);
            var viewport = OriginalMainMapViewportDepthSource.CreateFreshBackend(repository,
                bootstrap.Camera.ExecutableLocale, bootstrap.Feature.Binding.ViewIdentity + "/playable-owned-backend");
            var sourceStages = (preceding ?? new OriginalNativeMapSceneStages()) with
            {
                AfterTerrain = async context =>
                {
                    if (preceding?.AfterTerrain is { } beforeEarly) await beforeEarly(context);
                    if (current.CompleteGeometryAndPrepareShading is { } completeGeometry)
                    {
                        var geometry=host!.Session.GeometryFrame??throw new InvalidDataException("Geometry phase is unavailable.");
                        var shading=await completeGeometry(geometry,host);
                        host.Session.PrepareCurrentShading(context,shading);
                        host.PrepareCurrentEarlySelections(host.RequireCurrent(context));
                    }
                    await host!.ProduceEarlyAndAuxiliaryAsync(context);
                },
                BeforeTargetRetirement = async targets =>
                {
                    if (host is not null) host.currentTerrainStencil = null;
                    host?.RetireCameraLeases();
                    await early.RetireTargetsAsync();
                    if (preceding?.BeforeTargetRetirement is { } retire) await retire(targets);
                    if (host is not null) host.LastEarlyFrame = null;
                },
                AfterDeferred = async context =>
                {
                    try
                    {
                        if (preceding?.AfterDeferred is { } previous) await previous(context);
                        var accepted = host!.RequireCurrent(context);
                        var inputs = current.PrepareFinal(accepted);
                        if(host.postEffect3Loop is{} ordinaryLoop)
                        {
                            await host.PrepareCurrentExposureBeforeFinalAsync(context,inputs);
                            return await ordinaryLoop.RenderCurrentAsync(host,context);
                        }
                        if(afterWorldExposureBeforeFinal is not null)
                        {
                            var exposurePrepared=await host.PrepareCurrentExposureBeforeFinalAsync(context,inputs);
                            await afterWorldExposureBeforeFinal(host,context,exposurePrepared);
                        }
                        return host.GraphicsColorMaskOwner is { } history
                            ? await history.RenderSourceFinalAndCompleteAsync(context, inputs)
                            : await final.RenderAsync(context, inputs);
                    }
                    finally { host!.CompleteCameraReads(context.FrameNumber, releaseOlder: true); }
                },
                BeforeFrame = async context =>
                {
                    host!.currentTerrainStencil = null;
                    host.currentEarlyQueries = null;
                    host.currentEarlySelections = null;
                    host.currentWorldEarlyPasses = null;
                    host.CurrentConsumerReceipt = null;
                    host!.CompleteCameraReads(context.FrameNumber, releaseOlder: false);
                    if (preceding?.BeforeFrame is { } prepare) await prepare(context);
                },
                PrepareTerrainRaster = async (context, raster) =>
                {
                    if (preceding?.PrepareTerrainRaster is { } preparePrevious)
                        raster = await preparePrevious(context, raster) ?? throw new InvalidDataException("Preceding raster producer returned no current state.");
                    var accepted = host!.RequireCurrent(context);
                    var terrain = await current.PrepareTerrainNativeStencil!(accepted, raster)
                        ?? throw new InvalidDataException("Source terrain raster producer returned no prepared frame.");
                    terrain.ValidateCurrent(context);
                    if (!ReferenceEquals(terrain.Frame, accepted.PreparedSourceFrame) || terrain.Node51Execution is null)
                        throw new InvalidDataException("Source terrain raster lacks this same F103 packet or actual node51 CPU activation.");
                    host.currentTerrainStencil = terrain;
                    return terrain.Raster;
                }
            };
            var finalRetirement = sourceStages with
            {
                BeforeTargetRetirement = async targets =>
                {
                    await final.Chain.RetireTargetAsync();
                    if (sourceStages.BeforeTargetRetirement is { } retire) await retire(targets);
                }
            };
            scene = await OriginalNativeMapScene.BuildAsync(sceneSource.TerrainMaterial, sceneSource.InitialHeight,
                sceneSource.TerrainTargets, sceneSource.OutputTargets, bootstrap.Selection.SourcePlan.Shader,
                bootstrap.Selection.Combination.Index, sceneSource.TerrainExtents, sceneSource.OutputExtents,
                borrowedCompiler, allowReadback, session.Stages(finalRetirement,
                    frame=>host!.PrepareCurrentConsumersAsync(frame),current.SubmitStaticGeometry,
                    current.SubmitWorldGeometryBeforeTerrain is { } submitWorld ? async (frame,army)=>
                    {await submitWorld(frame,army,host!);} : null),
                session.Program,resolvedTargets,requireExternalPresentation:true);
            surface = new OriginalNativeMapSurface();
            surface.Configure(scene);
            host = new(repository, borrowedCompiler, current, bootstrap.Camera.ExecutableLocale, allowReadback,
                session, early, final, auxiliary, viewport, caustics, borrowedLighting.RetainedNormalization, scene, surface, borrowedSky,sourceSceneProxyPolicy);
            host.GraphicsColorMaskOwner = current.CreateGraphicsColorMaskOwner?.Invoke(final)
                ?? (current.CreateGraphicsColorMaskOwner is null ? null :
                    throw new InvalidDataException("Source graphics history factory returned no owner."));
            configureEarlyDiagnostics?.Invoke(early);
            // Source consumers that require the fresh Host/context (such as
            // the separately owned native sampler reset) must initialize
            // before the first attempt. The caller retains and retires their
            // borrowers before this Host; this hook publishes no completion.
            host.postEffect3Loop=postEffect3Loop;
            if(postEffect3Loop is not null)await postEffect3Loop.InitializeAsync(host);
            if(prepareFirstWorldConsumers is not null) await prepareFirstWorldConsumers(host);
            await host.RenderFrameAsync(firstFrame);
            if (!host.HasAcceptedOutput || scene.FrameNumber != 1 || !host.HasCompletedCurrentFinal(scene.LastCompletedContext!) ||
                session.CurrentPasses.Count == 0)
                throw new InvalidDataException("Playable host cannot mount until the complete source Deferred/final chain accepts its first frame.");
            return host;
        }
        catch(Exception buildFailure)
        {
            var nested=buildFailure is OriginalNativeMapBorrowerConstructionException incomplete
                ? incomplete.RetainedPackage : null;
            var partial=host is not null
                ? new OriginalNativeMapBorrowerRetirementPackage("World-Host-build/"+Guid.NewGuid().ToString("N"),nested,host)
                : new OriginalNativeMapBorrowerRetirementPackage("World-Host-partial/"+Guid.NewGuid().ToString("N"),
                    nested,new PartialSurfaceRetirement(surface,scene),early,final,auxiliary,session);
            try { await partial.DisposeAsync(); }
            catch(Exception retirementFailure)
            {
                partial.RetainFailure();
                throw new OriginalNativeMapBorrowerConstructionException(buildFailure,retirementFailure,partial);
            }
            throw;
        }
    }

    private sealed class PartialSurfaceRetirement(OriginalNativeMapSurface? surface,
        OriginalNativeMapScene? scene) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if(surface is not null&&GodotObject.IsInstanceValid(surface))await surface.RetireAsync();
            if(scene is not null)await scene.DisposeAsync();
            if(surface is not null&&GodotObject.IsInstanceValid(surface))surface.Free();
        }
    }

    private static void ValidateCurrentSources(OriginalMapSourceCreatedWorldPlayableCurrentSources source)
    {
        ArgumentNullException.ThrowIfNull(source.PrepareCurrent); ArgumentNullException.ThrowIfNull(source.PrepareEarlyCurrent);
        ArgumentNullException.ThrowIfNull(source.ProduceAuxiliary);
        ArgumentNullException.ThrowIfNull(source.PrepareFinal);
        ArgumentNullException.ThrowIfNull(source.SelectCurrentAmbient);
        if(source.PrepareTerrainNativeStencil is null)
            throw new OriginalMapSourceCreatedPlayableSourceUnavailableException(
                "source geometry owner after shader hooks",
                OriginalMapSourceCreatedPlayableRequiredInput.CurrentGeometryStencilPublication);
    }
    private static Vector2I ReadNativeExtent(IReadOnlyDictionary<uint, Vector2I> terrain,
        IReadOnlyDictionary<uint, Vector2I> output)
    {
        if (!terrain.TryGetValue(0xE13DDB4F, out var depth) || !output.TryGetValue(0x064B746B, out var color) ||
            depth != color || depth.X <= 0 || depth.Y <= 0)
            throw new InvalidDataException("Playable source frame requires the same actual E13 main-depth and064B current-color extent.");
        return depth;
    }

    private static IReadOnlyList<OriginalDeferredEarlyF103SelectedSource> PrepareEarlySelections(
        OriginalGameRepository repository, OriginalMapSourceCreatedF103CurrentFrame frame,
        OriginalDeferredEarlySourceViewQueriesPacket queries)
    {
        queries.ValidateCurrent(frame.ViewFields);
        var current = queries.BindCurrentFrame(frame, frame.Variant.NativeColorMode);
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var shaderObjects = repository.LoadObjects("system", 0x718CCB8B);
        var textureObjects = repository.LoadObjects("system", 0x93FB3280);
        var references = OriginalRenderGraphShaderReferences.Read(graph, shaderObjects);
        return Array.AsReadOnly(new[] { 77, 78, 79, 80 }.Select(node =>
        {
            var shader = repository.LoadShaderContainer("system", references.References.Single(r => r.Node.Index == node).ShaderFileHash);
            var plan = OriginalDeferredTexturePlan.Read(graph, node, shaderObjects, textureObjects, shader, 0);
            return OriginalDeferredEarlyF103SourceSelection.PrepareFreshShader(plan, frame.Feature, frame.Lighting, current);
        }).ToArray());
    }

    private OriginalMapSourceCreatedNativeSessionPreparedFrame RequireCurrent(OriginalNativeMapFrameContext context)
    {
        var frame = Session.CurrentFrame ?? throw new InvalidDataException("Playable source Session has no current accepted frame.");
        if (!ReferenceEquals(frame.Context, context) || frame.PreparedSourceFrame is null)
            throw new InvalidDataException("Playable graph producers must consume the exact prepared source context.");
        return frame;
    }

    private OriginalMapSourceCreatedNativeSessionPreparedFrame RequireGeometry(OriginalNativeMapFrameContext context)
    {
        var frame=Session.GeometryFrame??throw new InvalidDataException("Playable Session has no geometry preparation.");
        if(!ReferenceEquals(frame.Context,context))throw new InvalidDataException("Geometry belongs to another scene attempt.");
        return frame;
    }

    /// <summary>Actual ordinary geometry that populated the native Apply
    /// InputDepth target. Available after this attempt's complete late roster;
    /// neither an E13 allocation nor a previous accepted frame is sufficient.</summary>
    internal OriginalNativeSceneGeometryStencilCompletedFrame RequirePostEffectInputDepthGeometry(
        OriginalNativeMapFrameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock(lifetime) ObjectDisposedException.ThrowIf(retiring,this);
        var frame=RequireCurrent(context);
        var late=Scene.LastWorldDeferredSubmittedFrame ??
            throw new InvalidDataException("PostEffect InputDepth requires actual current World late submissions.");
        if(!ReferenceEquals(late.Context,context) || currentTerrainStencil is not { } terrain ||
            Session.Population.LastAcceptedFrame is not { } population ||
            Session.Army.LastAcceptedFrame is not { } army ||
            Scene.LastTerrainSubmittedFrame is not { } terrainSubmission)
            throw new InvalidDataException("PostEffect InputDepth has no exact current geometry owner roster.");
        Session.RequireSubmittedWorld(late);
        var geometry=OriginalNativeSceneGeometryStencilCompletedFrame.Complete(
            RequireGeometry(context),population,army,terrain,terrainSubmission);
        geometry.ValidateCurrent(frame,population,army,terrainSubmission);
        return geometry;
    }

    /// <summary>Finite first-attempt native VS/PS prefix. The default native
    /// compute sampler state belongs to a separately initialized owned context,
    /// not to the original process or Godot's transport compute pipelines.
    /// All current private Camera/Aux and ordinary geometry owners must still
    /// be live. This does not admit an arbitrary external render callback.</summary>
    internal OriginalMapOwnedGraphicsMaskPrefix RequireFirstWorldNativeGraphicsPrefix(
        OriginalNativeMapWorldDeferredSubmittedFrame late,
        OriginalSceneViewNativeMapWorldSkySourceOwner sky,
        OriginalMapExposurePacket exposure, long exposureDrawCount,
        OriginalMapPostEffect3FirstWorldExecutionOwner? retainedOwner=null)
    {
        ArgumentNullException.ThrowIfNull(late); ArgumentNullException.ThrowIfNull(sky);
        ArgumentNullException.ThrowIfNull(exposure);
        lock(lifetime) ObjectDisposedException.ThrowIf(retiring,this);
        var context=late.Context;
        var frame=RequireCurrent(context);
        RequirePostEffectWorldAttempt(context,retainedOwner);
        if(!ReferenceEquals(Scene.LastWorldDeferredSubmittedFrame,late) ||
            !ReferenceEquals(sky.Device,BorrowedSky) ||
            pendingGpu is not { } pending || !ReferenceEquals(pending.Frame,frame) ||
            !cameraLeases.TryGetValue(context.FrameNumber,out var ready) ||
            !ReferenceEquals(pending.Inputs.SharedCameraReady,ready) ||
            currentTerrainStencil is not { } terrain ||
            Session.Population.LastAcceptedFrame is not { } population ||
            Session.Army.LastAcceptedFrame is not { } army ||
            Scene.LastTerrainSubmittedFrame is not { } terrainSubmission ||
            LastEarlyFrame is not { } early || early.Submission is not { } earlySubmission)
            throw new InvalidDataException("Fresh native sampler prefix requires the actual first World attempt before final completion.");
        Session.RequireSubmittedWorld(late);
        var prefix=sky.RequireCompletedSkyGraphicsPrefix(context);
        prefix.RequireCanonicalSkyDeclaredHookScope();
        if(!prefix.UsesConstructedFrontendState || prefix.FrameNumber!=context.FrameNumber ||
            prefix.ViewIdentity!=Session.Binding.ViewIdentity)
            throw new InvalidDataException("Fresh native sampler prefix has another Sky/frontend generation.");
        var geometry=OriginalNativeSceneGeometryStencilCompletedFrame.Complete(
            RequireGeometry(context),population,army,terrain,terrainSubmission);
        geometry.ValidateCurrent(frame,population,army,terrainSubmission);
        earlySubmission.RequireCurrent(context,early);
        // This finite source host executes Directional ordinary78 only. A new
        // original native family must extend the ordered state producer first.
        if(earlySubmission.ExecutedSources.Any(s=>s.SourcePlan.Node.Index!=78) ||
            earlySubmission.ExecutedDraws.Count!=Session.CurrentPasses.Count ||
            Session.CurrentPasses.Any(p=>p.Selection.Combination.Index!=510 || p.Selection.PixelProgramIndex!=383))
            throw new NotSupportedException("Fresh native sampler state has an unclosed Early/late native writer family.");
        foreach(var pass in Session.CurrentPasses) _=ready.RequireSharedWorldConsumer(pass);
        Final.Chain.Exposure.RequireCompletedCurrentFrame(context,exposure,exposureDrawCount);
        return prefix;
    }

    private void RequirePostEffectWorldAttempt(OriginalNativeMapFrameContext context,
        OriginalMapPostEffect3FirstWorldExecutionOwner? retainedOwner)
    {
        if(context.FrameNumber==1)
        {
            if(Scene.RenderAttemptNumber!=1||Scene.FrameNumber!=0||Scene.LastCompletedContext is not null||HasAcceptedOutput)
                throw new InvalidDataException("First post-effect scope requires the actual fresh World attempt.");
            return;
        }
        if(context.FrameNumber!=2||Scene.RenderAttemptNumber!=2||Scene.FrameNumber!=1||
            Scene.LastCompletedContext?.FrameNumber!=1||!HasAcceptedOutput||retainedOwner is null)
            throw new NotSupportedException("Retained post-effect scope requires the actual second World after its first accepted output.");
        retainedOwner.RequireRetainedWorldAttempt(context);
    }

    private async Task ProduceEarlyAndAuxiliaryAsync(OriginalNativeMapFrameContext context)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var timings=new List<OriginalNativeMapStageTiming>(5);double previous=0;
        void Mark(string stage)
        {
            double elapsed=watch.Elapsed.TotalMilliseconds;
            timings.Add(new(stage,elapsed-previous,elapsed));previous=elapsed;
        }
        var frame = RequireCurrent(context);
        var source = frame.PreparedSourceFrame!;
        var population = Session.Population.LastAcceptedFrame ?? throw new InvalidDataException("Current source model population has not completed.");
        if (!ReferenceEquals(population.Context, context)) throw new InvalidDataException("Playable population belongs to another target attempt.");
        var army=Session.Army.LastAcceptedFrame??throw new InvalidDataException("Current source Army geometry has not completed.");
        if(!ReferenceEquals(army.Prepared.Context,context))throw new InvalidDataException("Playable Army geometry belongs to another target attempt.");
        var terrain = Scene.LastTerrainSubmittedFrame ?? throw new InvalidDataException("Current source terrain has not completed its actual device submission.");
        terrain.RequireCurrent(context);
        var preparedTerrain = currentTerrainStencil ?? throw new InvalidDataException("Host has no actual source terrain raster for this accepted attempt.");
        var geometry = OriginalNativeSceneGeometryStencilCompletedFrame.Complete(RequireGeometry(context),population,army,preparedTerrain,terrain);
        var queries = currentEarlyQueries ?? throw new InvalidDataException("Early shader selection has no accepted View-bank owner packet.");
        queries.ValidateCurrent(source.ViewFields);
        var passes=currentWorldEarlyPasses??throw new InvalidDataException("Actual World early pass owners were not prepared.");
        var world=Session.CurrentWorld??throw new InvalidDataException("Actual World early publication is unavailable.");
        Mark("current-geometry-and-early-scope");
        var early = await Early.DrawWorldAsync(world.World,passes,geometry,
            (pass,selected) => PrepareEarlyParametersAsync(pass.Accepted,selected,pass.WorldPass),
            current.RequireGeometryDepthSampling?.Invoke(frame));
        Mark("current-early-submission");
        // Reject a producer superseded during early preparation/submission.
        geometry.ValidateCurrent(frame,population,army,terrain);
        LastEarlyFrame = early;
        var completed = await current.ProduceAuxiliary(frame, population, early, Auxiliary)
            ?? throw new InvalidDataException("Actual source auxiliary pixel/geometry producers are unavailable.");
        if (!ReferenceEquals(completed.AcceptedFrame, frame) || !ReferenceEquals(completed.EarlyColor, early))
            throw new InvalidDataException("Auxiliary completion belongs to another current source HDR/geometry frame.");
        Mark("current-auxiliary-production");
        var ready = await OriginalMainMapCameraDeferredReadySource.PrepareAsync(frame,
            Viewport.PrepareSourceMainFrame(frame.Selection, frame.Feature.Binding.ViewIdentity), Session.Program, Auxiliary.RefractionDepth);
        Mark("current-camera-ready");
        try
        {
            if (cameraLeases.ContainsKey(context.FrameNumber)) throw new InvalidDataException("Playable Camera source was published twice for one render attempt.");
            var inputs = ready.CreateSessionInputs(completed.TerrainMaterials.ToArray(), Auxiliary.Devices, completed.Raster);
            cameraLeases.Add(context.FrameNumber, ready);
            pendingGpu = (frame, inputs);
            currentAuxiliaryCompletion=completed;
            Mark("current-deferred-input-join");
            LastEarlyAuxiliaryTimings=timings.AsReadOnly();
        }
        catch { ready.Dispose(); throw; }
    }

    private Task<OriginalDeferredEarlySourcePassInputs> PrepareEarlyParametersAsync(
        OriginalMapSourceCreatedNativeSessionPreparedFrame frame, OriginalDeferredEarlyF103SelectedSource selected,
        OriginalMapSourceCreatedWorldF103Pass? worldPass=null)
    {
        if (selected.SourcePlan.Node.Index != 78)
            return current.PrepareOtherEarlyPass is { } producer
                ? producer(frame, selected, Early.Programs[selected.SourcePlan.Node.Index])
                : throw new NotSupportedException("The admitted original early node requires its actual current source parameter/image owners: " + selected.SourcePlan.Node.Index);
        var source = frame.PreparedSourceFrame ?? throw new InvalidDataException("Ordinary early source requires its exact current publication.");
        var caustics = worldPass is null?Caustics.Prepare(selected,source):Caustics.PrepareWorld(selected,worldPass);
        var plugins = Session.Plugins.Prepare(frame.Selection, frame.Lighting);
        var constants = OriginalDeferredEarlySceneConstants.CreateOrdinary126(selected,
            frame.Selection.SourcePlan.Shader, frame.Lighting, plugins.Occlusion, plugins.ShadowFade,
            caustics.NativeWrites, caustics.CurrentProducerProvenance);
        if(RetainedNormalization is { } normalization)
            normalization.RequireEarlyConsumer(CurrentConsumerReceipt ??
                throw new InvalidDataException("Current retained lighting scope is unavailable."),selected);
        return Task.FromResult(new OriginalDeferredEarlySourcePassInputs(
            new Dictionary<uint, ReadOnlyMemory<byte>> { { 0, constants.Bytes } }, [], constants.CurrentProducerProvenance)
            { EarlyCausticsSourceNull = caustics.SourceNull });
    }

    private void PrepareCurrentEarlySelections(OriginalMapSourceCreatedNativeSessionPreparedFrame frame)
    {
        var source=frame.PreparedSourceFrame??throw new InvalidDataException("Current source publication is unavailable.");
        SourceSceneProxyPolicy?.ValidateCurrent(source);
        var queries=current.PrepareEarlyCurrent(frame)??throw new InvalidDataException("Current native View-bank query publication is unavailable.");
        queries.ValidateCurrent(source.ViewFields);
        var world=Session.CurrentWorld??throw new InvalidDataException("Current source World publication is unavailable.");
        currentEarlyQueries=queries;
        currentWorldEarlyPasses=Array.AsReadOnly(Session.CurrentPasses.Select((pass,index)=>
            new OriginalDeferredEarlyWorldPreparedPass(pass,world.World.Passes[index],
                Early.PrepareCurrentSelections(pass,
                    queries.BindCurrentFrame(pass.PreparedSourceFrame!,pass.PreparedSourceFrame!.Variant.NativeColorMode)))).ToArray());
        currentEarlySelections=currentWorldEarlyPasses[0].Selections;
        if(RetainedNormalization is not null && CurrentConsumerReceipt is not null)
            foreach(var pass in world.World.Passes)
                RetainedNormalization.RequireLateConsumer(CurrentConsumerReceipt,pass.Frame.Selection,pass.InnerLighting);
    }

    private async Task PrepareCurrentConsumersAsync(OriginalMapSourceCreatedNativeSessionPreparedFrame frame)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var timings=new List<OriginalNativeMapStageTiming>(3);double previous=0;
        void Mark(string stage)
        {
            double elapsed=watch.Elapsed.TotalMilliseconds;
            timings.Add(new(stage,elapsed-previous,elapsed));previous=elapsed;
        }
        PrepareCurrentEarlySelections(frame);
        var source=frame.PreparedSourceFrame!;
        var world=Session.CurrentWorld!;
        var active=currentEarlySelections!.Where(s=>OriginalDeferredEarlySourceExecution.ReadWorldBins(
            s.SourcePlan,source.Context,source.ViewFields,source.Common.CurrentViewAux3F2,world.World).Count!=0).ToArray();
        Mark("current-early-selection");
        CurrentConsumerReceipt=RetainedNormalization?.CreateCurrentReceipt(frame.Lighting);
        if (RetainedNormalization is not null)
        {
            if (active.Any(s => s.SourcePlan.Node.Index != 78))
                throw new NotSupportedException("Directional retained normalization requires the actual admitted ordinary78 consumer scope.");
            foreach(var pass in world.World.Passes)
                RetainedNormalization.RequireLateConsumer(CurrentConsumerReceipt!,pass.Frame.Selection,pass.InnerLighting);
        }
        Mark("current-retained-light-consumers");
        if(current.AfterCurrentPreparedWithLighting is { } typed)
        {
            await typed(frame,CurrentConsumerReceipt);
        }
        Mark("current-source-consumer-hook-and-sky");
        LastConsumerPreparationTimings=timings.AsReadOnly();
    }

    private OriginalSkyDirectionalAmbientDeviceBinding SelectCurrentAmbient(
        OriginalMapSourceCreatedNativeSessionPreparedFrame frame)
    {
        _=RequireCurrent(frame.Context);
        if (BorrowedSky.RequiresRetirement || !Session.CurrentPasses.Any(pass => ReferenceEquals(pass,frame)))
            throw new InvalidDataException("World host cannot borrow a failed or different current Sky owner.");
        var ambient=current.SelectCurrentAmbient(frame)
            ?? throw new InvalidDataException("Actual current Q80 Sky wrapper is unavailable.");
        ambient.State.ValidateCurrent(BorrowedSky.OwnedSkyIdentity,frame.Context.FrameNumber);
        if (ambient.State.SelectedSpecularIndex>=BorrowedSky.Scene.SpecularTargets.Count ||
            !ReferenceEquals(BorrowedSky.Scene.SpecularTargets[(int)ambient.State.SelectedSpecularIndex],ambient.Cube) ||
            ambient.Texture.Resource!=ambient.Cube.TextureRid || !ambient.HasCompleteGeneratedContents)
            throw new InvalidDataException("World host requires the selected native Sky wrapper's actual generated contents.");
        return ambient;
    }

    private (OriginalMapSourceCreatedNativeSessionPreparedFrame Frame, OriginalMapSourceCreatedNativeSessionGpuInputs Inputs)? pendingGpu;
    private Task<OriginalMapSourceCreatedNativeSessionGpuInputs> PrepareGpuAsync(
        OriginalMapSourceCreatedNativeSessionPreparedFrame frame, OriginalNativeMapModelPopulationAcceptedFrame population)
    {
        if (pendingGpu is not { } pending || !ReferenceEquals(pending.Frame, frame) ||
            !ReferenceEquals(population.Context, frame.Context) || !cameraLeases.ContainsKey(frame.Context.FrameNumber))
            throw new InvalidDataException("Deferred source inputs require this attempt's completed early/auxiliary/Camera chain.");
        return Task.FromResult(pending.Inputs);
    }

    /// <summary>Narrow first-World PS7 state: the last actual terrain uniform
    /// setter, retained through this frame's original ordinary Early, Gaussian
    /// Aux, source-null refraction, late383 and node1 Exposure. Uses original
    /// RDEFs, including declarations removed by a device specialization.</summary>
    internal OriginalDeferredDeviceBinding RequireInheritedPixelSampler7(
        OriginalNativeMapWorldDeferredSubmittedFrame late,OriginalMapExposurePacket exposure,
        OriginalMapPostEffect3FirstWorldExecutionOwner? retainedOwner=null)
    {
        ArgumentNullException.ThrowIfNull(late);ArgumentNullException.ThrowIfNull(exposure);
        lock(lifetime)ObjectDisposedException.ThrowIf(retiring,this);
        var context=late.Context;var frame=RequireCurrent(context);
        RequirePostEffectWorldAttempt(context,retainedOwner);
        if(!ReferenceEquals(Scene.LastWorldDeferredSubmittedFrame,late)||
            !cameraLeases.TryGetValue(context.FrameNumber,out var ready)||
            pendingGpu is not { } pending||!ReferenceEquals(pending.Frame,frame)||
            !ReferenceEquals(pending.Inputs.SharedCameraReady,ready)||
            LastEarlyFrame is not { } early||early.Submission is not { } submittedEarly||
            currentAuxiliaryCompletion is not { } completion||completion.SourcePixelStages is not { } stages||
            stages.AcceptedSourceInputs is not { } auxiliary||
            !ReferenceEquals(completion.AcceptedFrame,frame)||!ReferenceEquals(completion.EarlyColor,early)||
            !ReferenceEquals(stages.AcceptedFrame,frame)||!ReferenceEquals(stages.EarlyColor,early))
            throw new InvalidDataException("Inherited PS7 requires this exact first World terrain/Early/Aux/late source generation.");
        Session.RequireSubmittedWorld(late);
        var geometry=RequirePostEffectInputDepthGeometry(context);
        var sampler=geometry.TerrainSubmission.RequireLastSubmittedPixelSampler(7);
        static void RequireNo7(San14.Data.Formats.DxbcResourceDefinitions definitions,string source)
        {
            if(definitions.Bindings.Any(b=>b.InputType==3&&7u>=b.BindPoint&&7u-b.BindPoint<b.BindCount))
                throw new NotSupportedException("Inherited terrain PS7 has a later original sampler declaration: "+source);
        }
        submittedEarly.RequireCurrent(context,early);
        foreach(var source in submittedEarly.ExecutedSources)
        {
            if(source.SourcePlan.Node.Index!=78)throw new NotSupportedException("Inherited terrain PS7 admits actual ordinary78 only.");
            RequireNo7(source.PixelDefinitions,"Early78/"+source.PixelProgramSha256);
        }
        var nativeCurrent=auxiliary.Current;
        auxiliary.Prefix.Validate(frame.PreparedSourceFrame!,nativeCurrent.Context,auxiliary.Prefix.ViewGenerationIdentity);
        if(!auxiliary.Prefix.Node134NoFramebufferPushOrDraw||!auxiliary.Prefix.Node135Skipped||
            nativeCurrent.FilterType90!=1||nativeCurrent.SampleCount94 is <1 or >7||
            nativeCurrent.CameraFilters.ReadDepthFilterType(nativeCurrent.DepthFilterType98)!=1)
            throw new NotSupportedException("Inherited terrain PS7 requires the actual source skipped RLR and Gaussian1 Aux route.");
        // Both exact variants are really selected in the same complete source
        // Gaussian schedule. No fresh/default shader is substituted here.
        foreach(uint pass in new uint[]{0,1})
        {
            var specular=Auxiliary.Source.FilterShader.ResolveFilterPass(1,(uint)nativeCurrent.SampleCount94,pass);
            var depth=Auxiliary.Source.DepthFilterShader.ResolveFilterPass(1,3,pass);
            RequireNo7(specular.Stages.Single(s=>s.StageCode==0).Definitions,"node137/specular-Gaussian"+pass);
            RequireNo7(depth.Stages.Single(s=>s.StageCode==0).Definitions,"node137/depth-Gaussian"+pass);
        }
        RequireNo7(Auxiliary.StockCopy.StockSource.PixelProgram.Definitions,"node137/stock-copy");
        RequireNo7(Auxiliary.DepthCopy.EmbeddedSource.PixelProgram.Definitions,"node97/depth-copy");
        ready.RequireSourceNullRefractionSamplerPreservation(frame,Auxiliary.RefractionDepth);
        foreach(var pass in Session.CurrentPasses)
        {
            _=ready.RequireSharedWorldConsumer(pass);
            RequireNo7(pass.Selection.SourcePlan.Shader.ReadResourceDefinitions(pass.Selection.PixelProgramIndex),"late383");
        }
        Final.Chain.Exposure.RequireCompletedCurrentFrame(context,exposure,Final.Chain.Exposure.DrawCount);
        // The completed Exposure owner binds the typed stock-default3 source;
        // the following LightScene default1 has its own current prefix owner.
        return sampler;
    }

    /// <summary>Optional additional source-prefix seam, before legacy Final.
    /// Uses its identical source input/provenance to prepare and draw node1.
    /// CPU preview is rolled back; it neither acknowledges Final nor advances
    /// source time/history. The later legacy chain reuses this exact exposure.</summary>
    private async Task<OriginalMapFinalOutputProductionPrepared> PrepareCurrentExposureBeforeFinalAsync(
        OriginalNativeMapFrameContext context,OriginalMapFinalOutputProductionFrameInputs input)
    {
        if(!ReferenceEquals(RequireCurrent(context).Context,context)||!ReferenceEquals(input.AcceptedContext,context))
            throw new InvalidDataException("Exposure prefix requires this exact accepted World Final input.");
        var selected=OriginalMapExposureSelectedLightReceipt.CaptureOwnedSelectedView(
            input.SelectedExposureLight??throw new InvalidDataException("World Exposure prefix lacks the selected source record."),
            input.Lighting,Final.Constants.OwnedApplyLuminanceReferenceIdentity);
        var scene=context.OutputFrame.Textures[0x064B746B];
        var legacyPolicy=OriginalMapFinalOutputPostEffect3Owner.CreateAbsent(input.SourceFrameOwner+"/owned-PostEffect3",
            "owned-complete-source-chain:no-external-PostEffect3-input;Common3C0-invariant-when-absent");
        var hdr=OriginalMapFinalOutputHdrRoute.Capture(scene.EffectiveSource,scene.ExplicitExtent.X,scene.ExplicitExtent.Y,
            Final.SelectedViewAux.CommonMode3C0??throw new InvalidDataException("Exposure prefix requires actual Common3C0."),
            legacyPolicy,context.FrameNumber,input.SourceFrameOwner);
        var bloom=OriginalMapFinalOutputCameraBloomSource.ConstructFreshSource(input.Camera,
            input.SourceFrameOwner+"/fresh-source-Camera-Bloom-tail");
        bool srgb=OriginalRenderTextureFormatMath.Resolve(Final.Chain.Source.OutputTexture).TargetViewFormat is 29 or 91;
        var prepared=Final.Constants.Prepare(input.Camera,input.Lighting,input.CurrentSeason,Final.SelectedViewAux,
            scene.ExplicitExtent.X,scene.ExplicitExtent.Y,srgb,input.TangentMode,input.SourceFrameOwner,selected,hdr,bloom.CurrentMaxLum,bloom);
        Final.Constants.Complete(prepared,false);
        await Final.Chain.Exposure.PrepareAndBorrowAsync(context,prepared.Exposure);
        Final.Chain.Exposure.RequireCompletedCurrentFrame(context,Final.Chain.Exposure.LastPacket!,Final.Chain.Exposure.DrawCount);
        return prepared;
    }

    /// <summary>Called inside the serial scene order before any next-attempt
    /// writes, and after final consumption. Cached RID lifetime remains held.</summary>
    private void CompleteCameraReads(long frameNumber, bool releaseOlder)
    {
        foreach (var pair in cameraLeases.Where(p => p.Key <= frameNumber)) pair.Value.CompleteRead();
        if (!releaseOlder) { pendingGpu = null; LastEarlyFrame = null; return; }
        foreach (long prior in cameraLeases.Keys.Where(k => k < frameNumber).ToArray())
        { cameraLeases[prior].Dispose(); cameraLeases.Remove(prior); }
    }
    private void RetireCameraLeases()
    {
        foreach (var lease in cameraLeases.Values) lease.Dispose();
        cameraLeases.Clear(); pendingGpu = null;currentEarlySelections=null;currentWorldEarlyPasses=null;currentEarlyQueries=null;CurrentConsumerReceipt=null;currentAuxiliaryCompletion=null;
    }

    /// <summary>Drain actual descriptor/image read leases in the scene's
    /// serial order. Target-dependent draw caches remain alive.</summary>
    internal async Task CompleteGeometryConsumerReadsAsync(OriginalNativeMapFrameContext context)
    {
        CompleteCameraReads(context.FrameNumber,releaseOlder:true);
        await Session.Assembler.CompleteDeferredReadsAsync(context);
    }

    internal OriginalMapPostEffect3WorldCommonClockPublication PrepareGeometryCommonClock(OriginalNativeMapFrameContext context)
        =>(postEffect3Loop??throw new InvalidOperationException("Normal geometry clock requires its ordinary World loop."))
            .PrepareCommonClockBeforeGeometry(this,context);

    internal OriginalNativeTerrainStencilPreparedFrame RequirePreparedGeometryTerrain(OriginalNativeMapFrameContext context)
    {
        var terrain=currentTerrainStencil??throw new InvalidDataException("No actual prepared terrain for this geometry invocation.");
        terrain.ValidateCurrent(context);return terrain;
    }

    public async Task RenderFrameAsync(OriginalMapSourceCreatedPlayableTerrainFrame input)
    {
        ArgumentNullException.ThrowIfNull(input);
        input = SnapshotTerrainFrame(input);
        var request = new OriginalNativeMapRenderRequest(input.Batches, input.Camera, input.TerrainParameters,
            input.TerrainRaster, input.ClearColors, input.ClearDepth, input.ClearStencil, null)
            { GeometryClearActions = input.GeometryClearActions, PrepareOrderedDeferred = Session.PrepareOrderedDeferredAsync };
        await gate.WaitAsync();
        try { ObjectDisposedException.ThrowIf(retiring, this); await Surface.RenderFrameAsync(request); }
        catch
        {
            GraphicsColorMaskOwner?.MarkFailedOrUnknownMaskWriter("failed-source-Host-frame");
            if(Session.CurrentFrame is { } attempted)
                CompleteCameraReads(attempted.Context.FrameNumber, releaseOlder: true);
            throw;
        }
        finally { gate.Release(); }
    }

    private static OriginalMapSourceCreatedPlayableTerrainFrame SnapshotTerrainFrame(
        OriginalMapSourceCreatedPlayableTerrainFrame input) => input with
    {
        Batches = Array.AsReadOnly(input.Batches.Select(batch => new OriginalNativeTerrainBatch(batch.Coarse,
            Array.AsReadOnly(batch.Patches.ToArray()))).ToArray()),
        TerrainParameters = input.TerrainParameters with
        {
            WorldToProjectionRows = input.TerrainParameters.WorldToProjectionRows.ToArray(),
            WorldToViewRows = input.TerrainParameters.WorldToViewRows.ToArray()
        },
        ClearColors = input.ClearColors.ToArray()
    };

    /// <summary>Retire cached draws and their leases before replacing actual
    /// source auxiliary owners. A resized host needs a newly prepared Camera
    /// and complete frame; the Surface has no accepted resized output yet.</summary>
    public async Task ResizeTargetsAsync(IReadOnlyDictionary<uint, Vector2I> terrainExtents,
        IReadOnlyDictionary<uint, Vector2I> outputExtents)
    {
        var terrain = terrainExtents.ToDictionary(p => p.Key, p => p.Value);
        var output = outputExtents.ToDictionary(p => p.Key, p => p.Value);
        var extent = ReadNativeExtent(terrain, output);
        await gate.WaitAsync();
        OriginalMapSourceCreatedPlayableAuxiliaryOwners? next = null;
        try
        {
            ObjectDisposedException.ThrowIf(retiring, this);
            next = await OriginalMapSourceCreatedPlayableAuxiliaryOwners.BuildAsync(repository, compiler, executableLocale, extent, allowReadback);
            await Surface.ResizeTargetsAsync(terrain, output, allowReadback);
            var previous = Auxiliary; Auxiliary = next; next = null;
            await previous.DisposeAsync();
        }
        finally { if (next is not null) await next.DisposeAsync(); gate.Release(); }
    }

    public Task<OriginalWorldMainDisplayParentEntry> PrepareMainDisplayEntryAsync(OriginalNativeMapFrameContext context)
        =>(postEffect3Loop??throw new InvalidOperationException("Actual display entry belongs to the ordinary renderer."))
            .PrepareMainDisplayEntryAsync(this,context);

    public ValueTask DisposeAsync()
    {
        lock (lifetime)
        {
            retiring = true;
            return new(disposal is null || disposal.IsFaulted ? disposal = DisposeCoreAsync() : disposal);
        }
    }
    private async Task DisposeCoreAsync()
    {
        await gate.WaitAsync();
        try
        {
            await Surface.RetireAsync();
            if(postEffect3Loop is not null)await postEffect3Loop.DisposeAsync();
            RetireCameraLeases();
            if (GraphicsColorMaskOwner is { } history) await history.DisposeAsync();
            await Early.DisposeAsync(); await Final.DisposeAsync();
            await Auxiliary.DisposeAsync(); await Session.DisposeAsync();
            if (GodotObject.IsInstanceValid(Surface)) Surface.Free();
        }
        finally { gate.Release(); }
    }
}
