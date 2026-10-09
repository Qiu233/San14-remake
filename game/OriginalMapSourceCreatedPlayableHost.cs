using Godot;
using San14.Data;

/// <summary>Specific unresolved source boundaries of this playable pipeline.
/// Source factories use these fields to report unavailable current authority.</summary>
public enum OriginalMapSourceCreatedPlayableRequiredInput
{
    LightingRetainedConsumerProof,
    CurrentTerrainAndArmyPrimitivePublication,
    CurrentGeometryStencilPublication,
    OtherAdmittedEarlyParameters,
    AuxiliaryColorPrefix134And135,
    CurrentAuxiliaryFilterOwner,
    CurrentSkyAtmosphereOwner,
    RefractionGeometry139And140,
    CurrentFinalSeason
}

public sealed class OriginalMapSourceCreatedPlayableSourceUnavailableException : InvalidOperationException
{
    public IReadOnlyList<OriginalMapSourceCreatedPlayableRequiredInput> RemainingRequiredInputs { get; }
    public string SourceProducerProvenance { get; }
    public OriginalMapSourceCreatedPlayableSourceUnavailableException(
        string sourceProducerProvenance,
        params OriginalMapSourceCreatedPlayableRequiredInput[] remainingRequiredInputs)
        : base("Source-created playable frame requires: " + string.Join(", ", remainingRequiredInputs))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceProducerProvenance);
        if (remainingRequiredInputs.Length == 0)
            throw new ArgumentException("An unavailable source producer must identify its actual required input.", nameof(remainingRequiredInputs));
        RemainingRequiredInputs = Array.AsReadOnly(remainingRequiredInputs.Distinct().ToArray());
        SourceProducerProvenance = sourceProducerProvenance;
    }
}

/// <summary>Actual source geometry targets and material owners. Extents belong
/// to the same native frame; source Camera/terrain writers are supplied for
/// each render rather than inferred from control size.</summary>
public sealed record OriginalMapSourceCreatedPlayableSceneSource(
    LoadedTerrainGbiMapResources TerrainMaterial,
    LoadedTerrainInitialHeightResources InitialHeight,
    LoadedRenderGraphTargets TerrainTargets,
    LoadedRenderGraphTargets OutputTargets,
    IReadOnlyDictionary<uint, Vector2I> TerrainExtents,
    IReadOnlyDictionary<uint, Vector2I> OutputExtents);

/// <summary>The geometry request has no Deferred callback. The host always
/// installs its own same-attempt source Session and complete final chain.</summary>
public sealed record OriginalMapSourceCreatedPlayableTerrainFrame(
    IReadOnlyList<OriginalNativeTerrainBatch> Batches,
    OriginalDeferredCameraInputs Camera,
    TerrainGbufferDrawInputs TerrainParameters,
    TerrainGbufferRasterState TerrainRaster,
    Color[] ClearColors, float ClearDepth, uint ClearStencil)
{
    public RenderingDevice.DrawFlags GeometryClearActions { get; init; } = RenderingDevice.DrawFlags.ClearAll;
}

/// <summary>The remaining current producers have concrete source packets.
/// Missing current publication, early parameters or mode16 geometry must fail
/// here; no unknown field or allocated-only target is given a default.</summary>
public sealed record OriginalMapSourceCreatedPlayableCurrentSources(
    Func<OriginalNativeMapFrameContext, OriginalNativeMapArmyPreparedFrame,
        OriginalMapLightingBinding, OriginalMapSourceCreatedF103CurrentFrame> PrepareCurrent,
    Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,
        OriginalDeferredEarlySourceViewQueriesPacket> PrepareEarlyCurrent,
    Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalNativeMapModelPopulationAcceptedFrame,
        OriginalDeferredEarlySourceCompletedFrame, OriginalMapSourceCreatedPlayableAuxiliaryOwners,
        Task<OriginalMapSourceCreatedPlayableAuxiliaryCompletion>> ProduceAuxiliary,
    Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,
        OriginalMapFinalOutputProductionFrameInputs> PrepareFinal)
{
    /// <summary>Sky/current season may consume the accepted packet here, after
    /// preparation and before geometry. It must preserve that same context.</summary>
    public Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, Task>? AfterCurrentPrepared { get; init; }
    /// <summary>Normalized source Sky consumers must validate their actual
    /// private Sky capability with this receipt before issuing those draws.</summary>
    public Func<OriginalMapSourceCreatedNativeSessionPreparedFrame,
        OriginalMapSourceCreatedLightingRetainedConsumerReceipt?, Task>? AfterCurrentPreparedWithLighting { get; init; }
    /// <summary>Other admitted original77/79/80 programs require their actual
    /// current owners. Ordinary78 is constructed automatically by the host.</summary>
    public Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalDeferredEarlyF103SelectedSource,
        OriginalDeferredPixelProgram, Task<OriginalDeferredEarlySourcePassInputs>>? PrepareOtherEarlyPass { get; init; }
    /// <summary>Prepare the actual source terrain raster from the same current
    /// Session packet. Host owns its hook and later joins actual submissions;
    /// callers need no separate completion callback or shared raster holder.</summary>
    public Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, TerrainGbufferRasterState,
        Task<OriginalNativeTerrainStencilPreparedFrame>>? PrepareTerrainNativeStencil { get; init; }
    /// <summary>Borrow this Host's final chain for the frontend's cross-frame
    /// mask history. The returned owner performs the sole final draw.</summary>
    public Func<OriginalMapFinalOutputProductionFactory,
        OriginalMapSourceCreatedGraphicsColorMaskOwner>? CreateGraphicsColorMaskOwner { get; init; }

    /// <summary>Use the concrete original auxiliary pixel factory. The final
    /// callback executes source139/140 or its proved current no-op owners;
    /// ordered target receipts are subsequently required by ReadySource.</summary>
    public static OriginalMapSourceCreatedPlayableCurrentSources FromSourcePixelFactory(
        Func<OriginalNativeMapFrameContext, OriginalNativeMapArmyPreparedFrame, OriginalMapLightingBinding,
            OriginalMapSourceCreatedF103CurrentFrame> prepareCurrent,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalDeferredEarlySourceViewQueriesPacket> prepareEarlyCurrent,
        OriginalNativeMapArmyPopulationRenderer borrowedArmy,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalNativeMapModelPopulationAcceptedFrame,
            OriginalDeferredEarlySourceCompletedFrame,
            Task<OriginalMapSourceCreatedPlayableAuxiliarySourceFrameInputs>> prepareAuxiliary,
        Func<OriginalDeferredAuxiliarySourcePixelStages, OriginalNativeMapModelPopulationAcceptedFrame,
            OriginalNativeMapArmyPopulationAcceptedFrame, Task> completeRefractionGeometry,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalMapFinalOutputProductionFrameInputs> prepareFinal)
    {
        ArgumentNullException.ThrowIfNull(borrowedArmy);ArgumentNullException.ThrowIfNull(prepareAuxiliary);
        ArgumentNullException.ThrowIfNull(completeRefractionGeometry);
        return new(prepareCurrent,prepareEarlyCurrent,async(frame,population,early,owners)=>
        {
            var army=borrowedArmy.LastAcceptedFrame??throw new InvalidDataException("Current source Army geometry is unavailable.");
            if(!ReferenceEquals(army.Prepared.Context,frame.Context))
                throw new InvalidDataException("Source pixel inputs belong to another Army/Scene attempt.");
            var inputs=await prepareAuxiliary(frame,population,early)??throw new InvalidDataException("Current typed auxiliary source inputs are unavailable.");
            var stages=await OriginalDeferredAuxiliarySourcePixelFactory.ProducePixelStagesAsync(
                frame,population,army,early,owners,inputs.Pixels);
            await completeRefractionGeometry(stages,population,army);
            return new(frame,early,inputs.TerrainMaterials.ToArray(),inputs.Raster);
        },prepareFinal);
    }

    /// <summary>Complete the original mode16 source-null branches using the
    /// same geometry renderers' actual final-parent and submission snapshots.
    /// Unknown/non-null families reject; they are not given an empty callback.</summary>
    public static OriginalMapSourceCreatedPlayableCurrentSources FromSourceNullGeometryPixelFactory(
        Func<OriginalNativeMapFrameContext, OriginalNativeMapArmyPreparedFrame, OriginalMapLightingBinding,
            OriginalMapSourceCreatedF103CurrentFrame> prepareCurrent,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalDeferredEarlySourceViewQueriesPacket> prepareEarlyCurrent,
        OriginalNativeMapArmyPopulationRenderer borrowedArmy,
        OriginalDeferredSourceViewFamilyOwner borrowedViewFamilies,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalNativeMapModelPopulationAcceptedFrame,
            OriginalDeferredEarlySourceCompletedFrame,
            Task<OriginalMapSourceCreatedPlayableAuxiliarySourceFrameInputs>> prepareAuxiliary,
        Func<OriginalMapSourceCreatedNativeSessionPreparedFrame, OriginalMapFinalOutputProductionFrameInputs> prepareFinal)
    {
        ArgumentNullException.ThrowIfNull(borrowedViewFamilies);
        return FromSourcePixelFactory(prepareCurrent,prepareEarlyCurrent,borrowedArmy,prepareAuxiliary,
            async(stages,population,army)=>
            {
                var publication=OriginalDeferredSourceViewFamilyCurrentFactory.Prepare(
                    borrowedViewFamilies,stages.AcceptedFrame,population,army);
                await OriginalDeferredRefractionSourceGeometryProducer.CompleteSourceNullAsync(
                    stages,population,army,publication);
            },prepareFinal);
    }
}

public sealed record OriginalMapSourceCreatedPlayableAuxiliarySourceFrameInputs(
    OriginalDeferredAuxiliarySourcePixelInputs Pixels,
    IReadOnlyList<OriginalMapMaterialTerrainConsumer> TerrainMaterials,
    OriginalDeferredRasterState Raster);

/// <summary>Actual completed source graph outputs. The host verifies all
/// mandatory view/mip receipts and the ordered refraction97/139/140 chain
/// before creating any Camera plugin publication.</summary>
public sealed record OriginalMapSourceCreatedPlayableAuxiliaryCompletion(
    OriginalMapSourceCreatedNativeSessionPreparedFrame AcceptedFrame,
    OriginalDeferredEarlySourceCompletedFrame EarlyColor,
    IReadOnlyList<OriginalMapMaterialTerrainConsumer> TerrainMaterials,
    OriginalDeferredRasterState Raster)
{
    internal OriginalDeferredAuxiliarySourcePixelStages? SourcePixelStages { get; init; }
}

/// <summary>Owns the exact source auxiliary allocations and original pixel
/// producers. None of these allocations publishes ready content. The actual
/// current graph callback writes them using the borrowed accepted Scene.</summary>
public sealed class OriginalMapSourceCreatedPlayableAuxiliaryOwners : IAsyncDisposable
{
    public LoadedOriginalDeferredAuxiliaryPixelSourceRecipes Source { get; }
    public OriginalDeferredAuxiliaryPixelProducer Filters { get; }
    public OriginalDeferredAuxiliaryStockCopyProducer StockCopy { get; }
    public OriginalDeferredAuxiliaryDepthCopyProducer DepthCopy { get; }
    public IReadOnlyList<OriginalDeferredAuxiliaryTargetDevice> Devices { get; }
    public OriginalDeferredAuxiliaryTargetDevice RefractionDepth => Devices.Single(d => d.Source.Object.ObjectId == 0xB6169FDB);
    private readonly object lifetime = new();
    private Task? disposal;

    private OriginalMapSourceCreatedPlayableAuxiliaryOwners(
        LoadedOriginalDeferredAuxiliaryPixelSourceRecipes source,
        OriginalDeferredAuxiliaryPixelProducer filters, OriginalDeferredAuxiliaryStockCopyProducer stock,
        OriginalDeferredAuxiliaryDepthCopyProducer depth, List<OriginalDeferredAuxiliaryTargetDevice> devices)
        => (Source, Filters, StockCopy, DepthCopy, Devices) = (source, filters, stock, depth, devices.AsReadOnly());

    public static async Task<OriginalMapSourceCreatedPlayableAuxiliaryOwners> BuildAsync(
        OriginalGameRepository repository, Vkd3dShaderCompiler compiler, string executableLocale,
        Vector2I nativeFrameExtent, bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(compiler);
        var source = OriginalDeferredAuxiliaryPixelSourceRecipes.Read(
            repository.LoadRenderGraph("system", 0x024496EE), repository.LoadObjects("system", 0x718CCB8B),
            repository.LoadObjects("system", 0x93FB3280), repository.LoadShaderContainer("system", 0xEC36DC74),
            repository.LoadShaderContainer("system", 0x98327BB0), nativeFrameExtent.X, nativeFrameExtent.Y);
        var devices = new List<OriginalDeferredAuxiliaryTargetDevice>();
        OriginalDeferredAuxiliaryPixelProducer? filters = null;
        OriginalDeferredAuxiliaryStockCopyProducer? stock = null;
        OriginalDeferredAuxiliaryDepthCopyProducer? depth = null;
        try
        {
            foreach (var target in source.Targets.Concat(source.Intermediates))
                devices.Add(await OriginalDeferredAuxiliaryTargetDevice.BuildAsync(target, allowReadback, allowGpuCopy: true));
            filters = await OriginalDeferredAuxiliaryPixelProducer.BuildAsync(source,
                OriginalDeferredSamplerSources.Load(repository.SourceDirectory, executableLocale), compiler);
            stock = await OriginalDeferredAuxiliaryStockCopyProducer.BuildAsync(source,
                OriginalDeferredAuxiliaryStockCopySource.ReadCurrent(repository.SourceDirectory, executableLocale), compiler);
            depth = await OriginalDeferredAuxiliaryDepthCopyProducer.BuildAsync(source,
                OriginalDeferredAuxiliaryDepthCopySource.ReadCurrent(executableLocale), compiler);
            return new(source, filters, stock, depth, devices);
        }
        catch
        {
            if (depth is not null) await depth.DisposeAsync();
            if (stock is not null) await stock.DisposeAsync();
            if (filters is not null) await filters.DisposeAsync();
            foreach (var device in devices.AsEnumerable().Reverse()) await device.DisposeAsync();
            throw;
        }
    }

    /// <summary>Consumers and graph callbacks must retire before this owner.</summary>
    public ValueTask DisposeAsync()
    {
        lock (lifetime)
            return new(disposal is null || disposal.IsFaulted ? disposal = DisposeCoreAsync() : disposal);
    }
    private async Task DisposeCoreAsync()
    {
        await DepthCopy.DisposeAsync(); await StockCopy.DisposeAsync(); await Filters.DisposeAsync();
        foreach (var device in Devices.Reverse()) await device.DisposeAsync();
    }
}

/// <summary>A complete source-created playable map owner. It borrows the
/// lighting registry and the single static/Army population owners; owns its
/// Session, early original programs, auxiliary graph owners, final chain and
/// native Scene/Surface. Build renders a complete first frame before returning
/// a mountable Surface. A source producer failure leaves no partial UI host.</summary>
public sealed class OriginalMapSourceCreatedPlayableHost : IAsyncDisposable
{
    public OriginalNativeMapSurface Surface { get; }
    public OriginalNativeMapScene Scene { get; }
    public OriginalMapSourceCreatedNativeSession Session { get; }
    public OriginalDeferredEarlySourcePixelFactory Early { get; }
    public OriginalMapFinalOutputProductionFactory Final { get; }
    public OriginalMapSourceCreatedGraphicsColorMaskOwner? GraphicsColorMaskOwner { get; private set; }
    public OriginalMapSourceCreatedPlayableAuxiliaryOwners Auxiliary { get; private set; }
    public OriginalMainMapViewportDepthSource Viewport { get; }
    public OriginalDeferredEarlyCausticsOwner Caustics { get; }
    public OriginalMapSourceCreatedLightingRetainedNormalization? RetainedNormalization { get; }
    public OriginalMapSourceCreatedLightingRetainedConsumerReceipt? CurrentConsumerReceipt { get; private set; }
    public OriginalDeferredEarlySourceCompletedFrame? LastEarlyFrame { get; private set; }
    // The canvas owns its last completed snapshot independently of a final
    // target that a resize or failed attempt may have already retired.
    public bool HasAcceptedOutput => Surface.HasAcceptedOutput;
    private readonly OriginalGameRepository repository;
    private readonly Vkd3dShaderCompiler compiler;
    private readonly OriginalMapSourceCreatedPlayableCurrentSources current;
    private readonly string executableLocale;
    private readonly bool allowReadback;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object lifetime = new();
    private readonly Dictionary<long, OriginalMainMapCameraDeferredReadyFrame> cameraLeases = [];
    private bool retiring;
    private Task? disposal;
    private IReadOnlyList<OriginalDeferredEarlyF103SelectedSource>? currentEarlySelections;
    private OriginalDeferredEarlySourceViewQueriesPacket? currentEarlyQueries;
    private OriginalNativeTerrainStencilPreparedFrame? currentTerrainStencil;

    private OriginalMapSourceCreatedPlayableHost(OriginalGameRepository repository, Vkd3dShaderCompiler compiler,
        OriginalMapSourceCreatedPlayableCurrentSources current, string locale, bool allowReadback,
        OriginalMapSourceCreatedNativeSession session, OriginalDeferredEarlySourcePixelFactory early,
        OriginalMapFinalOutputProductionFactory final, OriginalMapSourceCreatedPlayableAuxiliaryOwners auxiliary,
        OriginalMainMapViewportDepthSource viewport, OriginalDeferredEarlyCausticsOwner caustics,
        OriginalMapSourceCreatedLightingRetainedNormalization? normalization,
        OriginalNativeMapScene scene, OriginalNativeMapSurface surface)
        => (this.repository, this.compiler, this.current, executableLocale, this.allowReadback,
            Session, Early, Final, Auxiliary, Viewport, Caustics, RetainedNormalization, Scene, Surface) =
            (repository, compiler, current, locale, allowReadback, session, early, final, auxiliary, viewport, caustics, normalization, scene, surface);

    /// <summary>The bootstrap is an actual owned source selection and CPU
    /// publication. Subsequent callbacks receive the serial Scene attempt and
    /// captured Army CPU frame; they cannot predict Scene.FrameNumber+1.</summary>
    public static async Task<OriginalMapSourceCreatedPlayableHost> BuildAsync(
        OriginalGameRepository repository, Vkd3dShaderCompiler borrowedCompiler,
        OriginalMapSourceCreatedPlayableSceneSource sceneSource,
        OriginalMapSourceCreatedLightingDeviceOwner borrowedLighting,
        OriginalNativeMapModelPopulationRenderer borrowedPopulation,
        OriginalNativeMapArmyPopulationRenderer borrowedArmy, OriginalMapMaterialCachePrepared materials,
        OriginalMapSourceCreatedF103CurrentFrame bootstrap,
        OriginalDeferredEarlySourceViewQueriesPacket bootstrapEarly,
        OriginalMapSourceCreatedPlayableCurrentSources current,
        OriginalMapSourceCreatedPlayableTerrainFrame firstFrame,
        OriginalNativeMapSceneStages? preceding = null,
        OriginalMapExposureOwner? borrowedExposure = null, bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(borrowedCompiler);
        ArgumentNullException.ThrowIfNull(sceneSource); ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(bootstrap); ArgumentNullException.ThrowIfNull(bootstrapEarly);
        ArgumentNullException.ThrowIfNull(firstFrame);
        sceneSource = sceneSource with
        {
            TerrainExtents = sceneSource.TerrainExtents.ToDictionary(p => p.Key, p => p.Value),
            OutputExtents = sceneSource.OutputExtents.ToDictionary(p => p.Key, p => p.Value)
        };
        firstFrame = SnapshotTerrainFrame(firstFrame);
        bootstrap.Selection.Validate(); bootstrap.Feature.ValidateFrame(bootstrap.Camera, bootstrap.Lighting);
        if (bootstrap.Selection.Combination.Index != 1022 || bootstrap.Selection.PixelProgramIndex != 767 ||
            bootstrap.Camera.Source.Camera.ObjectId != OriginalMainMapCameraAdapter.MainCameraObjectId ||
            bootstrap.Lighting.SourceIdentity != borrowedLighting.Source.Identity || bootstrap.CpuPopulation is null)
            throw new InvalidDataException("Playable bootstrap requires actual Main38D/current source publication selecting1022/767 and its CPU population.");
        ValidateCurrentSources(current);
        if(borrowedLighting.RetainedNormalization is not null && current.AfterCurrentPrepared is not null)
            throw new InvalidDataException("Normalized source consumers require the typed receipt hook before current Sky submission.");
        if(borrowedLighting.RetainedNormalization is not null && current.AfterCurrentPreparedWithLighting is null)
            throw new OriginalMapSourceCreatedPlayableSourceUnavailableException(
                "all declared retained-normalization consumers, including independent current Sky atmosphere",
                OriginalMapSourceCreatedPlayableRequiredInput.CurrentSkyAtmosphereOwner);
        Vector2I extent = ReadNativeExtent(sceneSource.TerrainExtents, sceneSource.OutputExtents);
        var bootEarly = PrepareEarlySelections(repository, bootstrap, bootstrapEarly);
        var bootOrdinary = bootEarly.Single(s => s.SourcePlan.Node.Index == 78);
        var caustics = OriginalDeferredEarlyCausticsOwner.CreateFresh(
            OriginalDeferredSamplerSources.LoadEarlyCaustics(repository.SourceDirectory, bootstrap.Camera.ExecutableLocale),
            bootOrdinary.SourcePlan, OriginalDeferredEarlyCausticsViewScope.FreshPreparedView1B90AbsentNoExternalAssignments);
        var bootCaustics = caustics.Prepare(bootOrdinary, bootstrap);
        OriginalMapSourceCreatedNativeSession? session = null;
        OriginalDeferredEarlySourcePixelFactory? early = null;
        OriginalMapFinalOutputProductionFactory? final = null;
        OriginalMapSourceCreatedPlayableAuxiliaryOwners? auxiliary = null;
        OriginalNativeMapScene? scene = null;
        OriginalNativeMapSurface? surface = null;
        OriginalMapSourceCreatedPlayableHost? host = null;
        try
        {
            session = await OriginalMapSourceCreatedNativeSession.BuildDefault1022Async(repository, borrowedCompiler,
                borrowedLighting, borrowedPopulation, borrowedArmy, materials, bootstrap.Selection,
                context =>
                {
                    var army = session!.CurrentArmyCpuFrame ?? throw new InvalidDataException("Current Army publication was not captured before F103.");
                    if (!ReferenceEquals(army.Context, context)) throw new InvalidDataException("Army CPU publication belongs to another accepted attempt.");
                    var prepared = current.PrepareCurrent(context, army, session.Binding)
                        ?? throw new InvalidDataException("Current source F103 publication is unavailable.");
                    if (prepared.CpuPopulation is null) throw new InvalidDataException("Playable current F103 must retain its exact CPU population publication.");
                    return OriginalMapSourceCreatedNativeSessionCurrentInputs.FromPrepared(prepared);
                }, (frame, population) => host!.PrepareGpuAsync(frame, population));
            if (bootstrap.Lighting.Binding != session.Binding)
                throw new InvalidDataException("Bootstrap lighting and playable Session must share the same selected shader/combination and owner binding.");
            early = await OriginalDeferredEarlySourcePixelFactory.BuildAsync(bootEarly, borrowedCompiler,
                borrowedLighting.Registration, session.Ggx, session.Samplers, bootCaustics.SourceNull);
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
                        return host.GraphicsColorMaskOwner is { } history
                            ? await history.RenderSourceFinalAndCompleteAsync(context, inputs)
                            : await final.RenderAsync(context, inputs);
                    }
                    finally { host!.CompleteCameraReads(context.FrameNumber, releaseOlder: true); }
                },
                BeforeFrame = async context =>
                {
                    host!.currentTerrainStencil = null;
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
                    frame=>host!.PrepareCurrentConsumersAsync(frame)), session.Program);
            surface = new OriginalNativeMapSurface();
            surface.Configure(scene);
            host = new(repository, borrowedCompiler, current, bootstrap.Camera.ExecutableLocale, allowReadback,
                session, early, final, auxiliary, viewport, caustics, borrowedLighting.RetainedNormalization, scene, surface);
            host.GraphicsColorMaskOwner = current.CreateGraphicsColorMaskOwner?.Invoke(final)
                ?? (current.CreateGraphicsColorMaskOwner is null ? null :
                    throw new InvalidDataException("Source graphics history factory returned no owner."));
            await host.RenderFrameAsync(firstFrame);
            if (!host.HasAcceptedOutput || scene.FrameNumber != 1 || final.LastAcceptedConstants is null ||
                session.LastPreparedDeferred is null)
                throw new InvalidDataException("Playable host cannot mount until the complete source Deferred/final chain accepts its first frame.");
            return host;
        }
        catch
        {
            if (host is not null) await host.DisposeAsync();
            else
            {
                if (surface is not null) { await surface.RetireAsync(); surface.Free(); }
                if (scene is not null) await scene.DisposeAsync();
                if (final is not null) await final.DisposeAsync();
                if (early is not null) await early.DisposeAsync();
                if (auxiliary is not null) await auxiliary.DisposeAsync();
                if (session is not null) await session.DisposeAsync();
            }
            throw;
        }
    }

    private static void ValidateCurrentSources(OriginalMapSourceCreatedPlayableCurrentSources source)
    {
        ArgumentNullException.ThrowIfNull(source.PrepareCurrent); ArgumentNullException.ThrowIfNull(source.PrepareEarlyCurrent);
        ArgumentNullException.ThrowIfNull(source.ProduceAuxiliary);
        ArgumentNullException.ThrowIfNull(source.PrepareFinal);
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

    private async Task ProduceEarlyAndAuxiliaryAsync(OriginalNativeMapFrameContext context)
    {
        var frame = RequireCurrent(context);
        var source = frame.PreparedSourceFrame!;
        var population = Session.Population.LastAcceptedFrame ?? throw new InvalidDataException("Current source model population has not completed.");
        if (!ReferenceEquals(population.Context, context)) throw new InvalidDataException("Playable population belongs to another target attempt.");
        var army=Session.Army.LastAcceptedFrame??throw new InvalidDataException("Current source Army geometry has not completed.");
        if(!ReferenceEquals(army.Prepared.Context,context))throw new InvalidDataException("Playable Army geometry belongs to another target attempt.");
        var terrain = Scene.LastTerrainSubmittedFrame ?? throw new InvalidDataException("Current source terrain has not completed its actual device submission.");
        terrain.RequireCurrent(context);
        var preparedTerrain = currentTerrainStencil ?? throw new InvalidDataException("Host has no actual source terrain raster for this accepted attempt.");
        var geometry = OriginalNativeSceneGeometryStencilCompletedFrame.Complete(frame,population,army,preparedTerrain,terrain);
        var queries = currentEarlyQueries ?? throw new InvalidDataException("Early shader selection has no accepted View-bank owner packet.");
        queries.ValidateCurrent(source.ViewFields);
        var currentVariant = queries.BindCurrentFrame(source, source.Variant.NativeColorMode);
        var selections = currentEarlySelections ?? throw new InvalidDataException("Actual early selections were not prepared before source consumers.");
        if(selections.Any(s=>s.Current.FrameNumber!=context.FrameNumber||s.EarlyCurrent!=currentVariant))
            throw new InvalidDataException("Early selections belong to another current source attempt.");
        var early = await Early.DrawAsync(frame, source.Context, source.ViewFields, selections, source.Common.CurrentViewAux3F2,
            selected => PrepareEarlyParametersAsync(frame, selected));
        // Reject a producer superseded during early preparation/submission.
        geometry.ValidateCurrent(frame,population,army,terrain);
        LastEarlyFrame = early;
        var completed = await current.ProduceAuxiliary(frame, population, early, Auxiliary)
            ?? throw new InvalidDataException("Actual source auxiliary pixel/geometry producers are unavailable.");
        if (!ReferenceEquals(completed.AcceptedFrame, frame) || !ReferenceEquals(completed.EarlyColor, early))
            throw new InvalidDataException("Auxiliary completion belongs to another current source HDR/geometry frame.");
        var ready = await OriginalMainMapCameraDeferredReadySource.PrepareAsync(frame,
            Viewport.PrepareSourceMainFrame(frame.Selection, frame.Feature.Binding.ViewIdentity), Session.Program, Auxiliary.RefractionDepth);
        try
        {
            if (cameraLeases.ContainsKey(context.FrameNumber)) throw new InvalidDataException("Playable Camera source was published twice for one render attempt.");
            var inputs = ready.CreateSessionInputs(completed.TerrainMaterials.ToArray(), Auxiliary.Devices, completed.Raster);
            cameraLeases.Add(context.FrameNumber, ready);
            pendingGpu = (frame, inputs);
        }
        catch { ready.Dispose(); throw; }
    }

    private Task<OriginalDeferredEarlySourcePassInputs> PrepareEarlyParametersAsync(
        OriginalMapSourceCreatedNativeSessionPreparedFrame frame, OriginalDeferredEarlyF103SelectedSource selected)
    {
        if (selected.SourcePlan.Node.Index != 78)
            return current.PrepareOtherEarlyPass is { } producer
                ? producer(frame, selected, Early.Programs[selected.SourcePlan.Node.Index])
                : throw new NotSupportedException("The admitted original early node requires its actual current source parameter/image owners: " + selected.SourcePlan.Node.Index);
        var source = frame.PreparedSourceFrame ?? throw new InvalidDataException("Ordinary early source requires its exact current publication.");
        var caustics = Caustics.Prepare(selected, source);
        var plugins = Session.Plugins.Prepare(frame.Selection, frame.Lighting);
        var constants = OriginalDeferredEarlySceneConstants.CreateOrdinary126(selected,
            frame.Selection.SourcePlan.Shader, frame.Lighting, plugins.Occlusion, plugins.ShadowFade,
            caustics.NativeWrites, caustics.CurrentProducerProvenance);
        if(RetainedNormalization is { } normalization)
            normalization.RequireCurrentEarlyConsumer(CurrentConsumerReceipt ??
                throw new InvalidDataException("Current retained lighting scope is unavailable."),selected);
        return Task.FromResult(new OriginalDeferredEarlySourcePassInputs(
            new Dictionary<uint, ReadOnlyMemory<byte>> { { 0, constants.Bytes } }, [], constants.CurrentProducerProvenance)
            { EarlyCausticsSourceNull = caustics.SourceNull });
    }

    private async Task PrepareCurrentConsumersAsync(OriginalMapSourceCreatedNativeSessionPreparedFrame frame)
    {
        var source=frame.PreparedSourceFrame??throw new InvalidDataException("Current source publication is unavailable.");
        var queries=current.PrepareEarlyCurrent(frame)??throw new InvalidDataException("Current native View-bank query publication is unavailable.");
        queries.ValidateCurrent(source.ViewFields);
        var early=queries.BindCurrentFrame(source,source.Variant.NativeColorMode);
        currentEarlyQueries=queries;
        currentEarlySelections=Early.PrepareCurrentSelections(frame,early);
        var active=currentEarlySelections.Where(s=>OriginalDeferredEarlySourceExecution.ReadBins(
            s.SourcePlan,source.Context,source.ViewFields,source.Common.CurrentViewAux3F2).Count!=0).ToArray();
        CurrentConsumerReceipt=RetainedNormalization?.RequireCurrentConsumers(frame.Lighting.AcceptedInputs,
            frame.Lighting,frame.Feature,frame.Selection,active,RetainedNormalization.GenericFogOwner);
        if(current.AfterCurrentPreparedWithLighting is { } typed)
        {
            await typed(frame,CurrentConsumerReceipt);
        }
        if(current.AfterCurrentPrepared is { } previous)await previous(frame);
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
        cameraLeases.Clear(); pendingGpu = null;currentEarlySelections=null;currentEarlyQueries=null;CurrentConsumerReceipt=null;
    }

    public async Task RenderFrameAsync(OriginalMapSourceCreatedPlayableTerrainFrame input)
    {
        ArgumentNullException.ThrowIfNull(input);
        input = SnapshotTerrainFrame(input);
        var request = new OriginalNativeMapRenderRequest(input.Batches, input.Camera, input.TerrainParameters,
            input.TerrainRaster, input.ClearColors, input.ClearDepth, input.ClearStencil, Session.PrepareDeferredAsync)
            { GeometryClearActions = input.GeometryClearActions };
        await gate.WaitAsync();
        try { ObjectDisposedException.ThrowIf(retiring, this); await Surface.RenderFrameAsync(request); }
        catch
        {
            GraphicsColorMaskOwner?.MarkFailedOrUnknownMaskWriter("failed-source-Host-frame");
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
            RetireCameraLeases();
            if (GraphicsColorMaskOwner is { } history) await history.DisposeAsync();
            await Early.DisposeAsync(); await Final.DisposeAsync();
            await Auxiliary.DisposeAsync(); await Session.DisposeAsync();
            if (GodotObject.IsInstanceValid(Surface)) Surface.Free();
        }
        finally { gate.Release(); }
    }
}
