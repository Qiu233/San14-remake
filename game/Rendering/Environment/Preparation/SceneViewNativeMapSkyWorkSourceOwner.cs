using San14.Data;

/// <summary>Concrete fresh owned WORK orchestration. The current selection,
/// Star packets and source-time candidate are typed. This file remains pending
/// ONLY the concrete active Cloud named-field source writer;
/// neither missing producer can be represented by a null callback.</summary>
public sealed class SceneViewNativeMapSkyWorkSourceOwner : IAsyncDisposable
{
    private readonly Vkd3dShaderCompiler compiler;
    private readonly SceneViewNativeMapSkyCurrentSelectionOwner selection;
    private readonly SkyAppliedSourceStarOwner stars;
    // Remaining exact native1193A90/Cloud RGB packet + selected noise/table
    // binding producer. Its source is nonnull in the actual seasonal Camera.
    private readonly SceneViewNativeMapSkyCloudSourceOwner cloud;
    private readonly SkyFirstGraphicsPrefix bootstrapRasterPlan;
    private MapSourceCreatedGraphicsColorMaskOwner? maskHistory;
    private SkyScene? scene;
    private SceneViewNativeMapSkyWorkPreparedFrame? bootstrapWork, current;
    private SkyAmbientCombinedFrame? currentSubmitted;
    private SceneViewNativeMapSkyWorkPreparedFrame? disabledSourceCall;
    private uint nextBackendGenerationIndex;
    public GameResourceRepository Repository { get; }
    public SceneViewNativeMapSkyCurrentSelection BootstrapSelection { get; }
    public TerrainSelectedKind1Record BootstrapSelectedSkyRecord => BootstrapSelection.SelectedSkyRecord;
    public SkyAppliedSourceStageFrame BootstrapStages => BootstrapSelection.Stages;
    public SkySceneProducerFrame BootstrapProducers => bootstrapWork?.Producers ??
        throw new InvalidDataException("Actual bootstrap Star and Cloud packets are not prepared.");

    private SceneViewNativeMapSkyWorkSourceOwner(SceneViewNativeMapLoadInputs loaded,
        Vkd3dShaderCompiler compiler, SceneViewNativeMapSkyCurrentSelectionOwner selection,
        SkyAppliedSourceStarOwner stars, SceneViewNativeMapSkyCloudSourceOwner cloud,
        SkyFirstGraphicsPrefix bootstrapRasterPlan,
        SceneViewNativeMapSkyCurrentSelection bootstrap)
    { Repository = loaded.Repository; this.compiler = compiler; this.selection = selection;
      this.stars = stars; this.cloud = cloud; this.bootstrapRasterPlan = bootstrapRasterPlan;
      BootstrapSelection = bootstrap; }

    public static SceneViewNativeMapSkyWorkSourceOwner ReadSource(SceneViewNativeMapLoadInputs loaded,
        Vkd3dShaderCompiler compiler, MapSourceCreatedF103CurrentFrame bootstrap,
        MapSourceCreatedSeasonLightingFrame present, SkySeasonMainCameraRgbFrame rgb,
        MainSceneNode51Execution actualBootstrapView)
    {
        string locale = loaded.InitialFrame.MainCamera.Camera.ExecutableLocale;
        string workIdentity = bootstrap.Lighting.Binding.ViewIdentity + "/fresh-owned-Sky-WORK";
        var selection = SceneViewNativeMapSkyCurrentSelectionOwner.CreateFresh(loaded.Repository,
            locale, bootstrap.Lighting.Binding, workIdentity);
        var first = selection.PrepareCurrent(bootstrap, present, rgb, actualBootstrapView);
        var stars = SkyAppliedSourceStarOwner.CreateFresh(loaded.Repository, locale, workIdentity + "/Star");
        var cloud = SceneViewNativeMapSkyCloudSourceOwner.ReadSource(loaded, compiler, first);
        // Separate unconsumed bootstrap source plan for cached GPU pipeline
        // construction. The actual attempt's typed prefix is published later,
        // matched to these scalars and consumed immediately before submission.
        var prototype = SkyFirstGraphicsPrefixSource.ReadFresh(locale,
            workIdentity + "/bootstrap-source-plan-not-GPU-publication", first.Source.Lighting.Binding.ViewIdentity);
        var plan = prototype.PrepareFirstPrefix(new(first.FrameNumber, first.Source.Lighting.Binding.ViewIdentity, true,
            "bootstrap-original-constructor-and-D3A850-draw-state-plan;cached-pipeline-only-no-WORK-submit"));
        return new(loaded, compiler, selection, stars, cloud, plan, first);
    }

    public void BindActualGraphicsColorMaskOwner(MapSourceCreatedGraphicsColorMaskOwner actualOwner)
    {
        ArgumentNullException.ThrowIfNull(actualOwner);
        if (maskHistory is not null || current is not null)
            throw new InvalidOperationException("WORK graphics history was already bound or an actual attempt prepared.");
        maskHistory = actualOwner;
        actualOwner.BindCurrentClipHistory(cloud.ClipSource, cloud.RequirePreparedClipPacket);
    }

    public void PrepareBootstrapProducers(SkySceneSourceFramePacket actualSourceFrame)
    {
        if (bootstrapWork is not null) throw new InvalidOperationException("Bootstrap packets already prepared.");
        bootstrapWork = PrepareWork(null, BootstrapSelection, actualSourceFrame, 0);
    }

    public SceneViewNativeMapSkyCurrentSelection PrepareCurrentSelection(
        MapSourceCreatedNativeSessionPreparedFrame actual,
        MapSourceCreatedSeasonLightingFrame present, SkySeasonMainCameraRgbFrame rgb,
        TerrainSelectedKind1Record record, MainSceneNode51Execution execution)
    {
        var source = actual.PreparedSourceFrame ?? throw new InvalidDataException("Current source View is missing.");
        var selected = selection.PrepareCurrent(source, present, rgb, execution);
        if (!ReferenceEquals(selected.SelectedSkyRecord, record))
            throw new InvalidDataException("Sky core must use the independently verified actual current record.");
        return selected;
    }

    public SceneViewNativeMapSkyWorkPreparedFrame PrepareCurrentWork(
        NativeMapFrameContext actualContext, SceneViewNativeMapSkyCurrentSelection selected,
        SkySceneSourceFramePacket actualSourceFrame, float explicitOwnedClockDelta)
    {
        selected.RequireCurrent();
        if (actualContext.FrameNumber != selected.FrameNumber)
            throw new InvalidDataException("Sky work and complete Host attempt belong to different frames.");
        currentSubmitted = null;
        disabledSourceCall = null;
        return current = PrepareWork(actualContext, selected, actualSourceFrame, explicitOwnedClockDelta);
    }

    private SceneViewNativeMapSkyWorkPreparedFrame PrepareWork(NativeMapFrameContext? context,
        SceneViewNativeMapSkyCurrentSelection selected, SkySceneSourceFramePacket source,
        float delta)
    {
        selected.RequireCurrent();
        MapOwnedGraphicsMaskPrefix? actualPrefix = context is null ? null :
            (maskHistory ?? throw new InvalidDataException("WORK needs the actual same-Host final graphics history owner."))
                .PrepareCurrentSkyPrefix(context, selected.Source.Lighting.Binding.ViewIdentity,
                    "actual-SceneView-source-WORK-before-model/Terrain/Aux;actual-final-history;resource-uploads-only-before-first-WORK");
        actualPrefix?.RequireCanonicalSkyDeclaredHookScope();
        if (actualPrefix is not null && (actualPrefix.ColorWriteMask != bootstrapRasterPlan.ColorWriteMask ||
            actualPrefix.SkyStarNativeCullMode != bootstrapRasterPlan.SkyStarNativeCullMode))
            throw new InvalidDataException("Actual retained Sky frontend disagrees with its cached source pipeline plan.");
        var star = stars.PrepareCurrent(source, selected.Present, selected.Stages, selected.ViewExecution, delta,
            "explicit-owned-SceneView-presentation-delta;not-original-Clock254;candidate-from-last-complete-Host");
        var clouds = actualPrefix is null
            ? cloud.PrepareBootstrap(source, selected.Present, selected.Stages, selected.ViewExecution, delta)
            : cloud.PrepareCurrent(context!, actualPrefix, source, selected.Present, selected.Stages, selected.ViewExecution, delta);
        var packets = star.SixPackets.Select(p => new SkyStarFacePacket(p.VertexBytes, p.GeometryBytes, p.PixelBytes)).ToArray();
        var producers = new SkySceneProducerFrame(packets,
            new(true, star.NativeScalar15C0, star.NativeScalar15C4), clouds.SixPackets,
            selected.Stages.Sky2Enabled, null, null);
        // null Scene and pass12 contribution are actual typed source gates;
        // Cloud is active and has six real packets, never an absent substitute.
        if (selected.Stages.ScenePresent || selected.Stages.WorkContributionEnabled || clouds.SixPackets.Count != 6)
            throw new NotSupportedException("WORK optional branches require their actual producer scope.");
        return new(context, selected, source, star, clouds, producers, nextBackendGenerationIndex, actualPrefix);
    }

    public SkySceneProgramSelection RequireSourceProgramSelection(LoadedSkyAmbientResources ambient)
    {
        BootstrapSelection.RequireCurrent();
        if (ambient.SpecularTargets.Count != 4 || ambient.SpecularTargets.Any(t => t.Width != 128 || t.LevelNum != 7))
            throw new NotSupportedException("Complete backend batch needs the verified original128/7 WORK schedule.");
        return new(BootstrapStages.Sky2Combination, Sky2Operation.Contribution, 9, 10, null)
            { Sky2ColorWriteMask = bootstrapRasterPlan.ColorWriteMask };
    }

    public async Task<(SkyStarProducer? Stars, SkyCloudColorProducer? Cloud)> BuildOptionalProducersAsync(
        SkyScene actualScene)
    {
        if (scene is not null || bootstrapWork is null) throw new InvalidOperationException("WORK owner is already bound or bootstrap packets missing.");
        scene = actualScene;
        if (bootstrapRasterPlan.SkyStarNativeCullMode != 1)
            throw new InvalidDataException("Unknown original first WORK Star cull mode.");
        var star = await SkyStarProducer.BuildAsync(stars.Shader, 0, compiler, scene.PrimaryWork,
            stars.Geometry, bootstrapWork.Producers.StarFaces!, null, bootstrapRasterPlan.ColorWriteMask,
            new(Godot.RenderingDevice.PolygonCullMode.Disabled, false, false, Godot.RenderingDevice.CompareOperator.Always),
            bootstrapWork.Producers.StarGate!);
        try
        {
            var clouds = await cloud.BuildAsync(scene, bootstrapWork.Cloud, bootstrapRasterPlan.ColorWriteMask);
            return (star, clouds);
        }
        catch (Exception buildFailure)
        {
            var partial = new NativeMapBorrowerRetirementPackage(
                BootstrapSelection.Source.Lighting.Binding.ViewIdentity + "/WORK-Star-partial/" + Guid.NewGuid().ToString("N"), star);
            try { await partial.DisposeAsync(); }
            catch (Exception retirementFailure)
            {
                partial.RetainFailure();
                throw new NativeMapBorrowerConstructionException(buildFailure, retirementFailure, partial);
            }
            throw;
        }
    }

    public void CompleteDisabledSourceCall(SceneViewNativeMapSkyWorkPreparedFrame actual)
    {
        RequireCurrent(actual);
        if (actual.Enable.AllowsWorkInitialization) throw new InvalidDataException("Current source Sky work is enabled.");
        disabledSourceCall = actual;
    }

    public void BeginActualWorkCall(SceneViewNativeMapSkyWorkPreparedFrame actual)
    {
        RequireCurrent(actual);
        if (!actual.Enable.AllowsWorkInitialization || actual.FirstGraphicsPrefix is not { } prefix)
            throw new InvalidDataException("Actual enabled first WORK needs its typed current graphics prefix.");
        prefix.RequireCanonicalSkyDeclaredHookScope();
        (maskHistory ?? throw new InvalidDataException("Actual WORK mask owner is unavailable."))
            .BeginWorkSubmission(actual.Context!, prefix);
        // Only the actual complete source final call can publish a subsequent
        // prefix. An interrupted source attempt retains its consumed state.
    }

    public void CompleteActualWorkCall(SceneViewNativeMapSkyWorkPreparedFrame actual,
        SkyAmbientCombinedFrame submitted)
    {
        RequireCurrent(actual);
        if (!actual.Enable.AllowsWorkInitialization || scene is null ||
            submitted.FrameNumber != actual.Selection.FrameNumber || !ReferenceEquals(submitted.Lighting, actual.Selection.Source.Lighting) ||
            submitted.SkyUpdate.Update.Progress.Phase != SkyScenePhase.Idle ||
            submitted.SkyUpdate.Update.Progress.LastCompletedIndex != actual.GenerationIndex ||
            submitted.SkyUpdate.Ambient is null || submitted.SkyUpdate.Update.CompletedSpecular is not { } cube ||
            !ReferenceEquals(cube, scene.GetCompletedSpecular(actual.GenerationIndex)))
            throw new InvalidDataException("Sky WORK did not complete the actual same-generation source draws/copies/filter.");
        currentSubmitted = submitted;
        nextBackendGenerationIndex = (actual.GenerationIndex + 1) % 3;
        // Actual GPU work is complete. Candidate Star/Cloud time is NOT yet
        // committed; later complete Host failure must not advance it twice.
    }

    public void ValidateCompleteFrameCandidate(NativeMapFrameContext context)
    {
        var candidate = current ?? throw new InvalidDataException("No current Sky time candidate.");
        RequireCurrent(candidate);
        if (!ReferenceEquals(candidate.Context, context) ||
            (candidate.Enable.AllowsWorkInitialization ? currentSubmitted is null : !ReferenceEquals(disabledSourceCall, candidate)))
            throw new InvalidDataException("Complete Host lacks its actual same-attempt completed Sky candidate.");
        candidate.Star.ValidateCandidate(candidate.SourceFrame, candidate.Selection.Present);
        cloud.ValidateCandidate(candidate.Cloud);
    }

    public void CommitCompleteFrame(NativeMapFrameContext context)
    {
        // The factory validates both F103 and Sky before entering this store.
        // CPU time/history commit is separate from already submitted GPU work.
        var candidate = current!;
        if (!candidate.Enable.AllowsWorkInitialization) return;
        stars.CommitCompleteFrame(candidate.Star);
        cloud.CommitCompleteFrame(candidate.Cloud);
    }

    public MapOwnedGraphicsMaskPrefix RequireCompletedSkyGraphicsPrefix(NativeMapFrameContext context)
    {
        ValidateCompleteFrameCandidate(context);
        return current!.FirstGraphicsPrefix ?? throw new InvalidDataException("Completed source Sky has no retained graphics prefix.");
    }

    private void RequireCurrent(SceneViewNativeMapSkyWorkPreparedFrame candidate)
    {
        if (!ReferenceEquals(candidate, current)) throw new InvalidDataException("Sky WORK candidate is stale or foreign.");
        candidate.Selection.RequireCurrent();
    }
    public ValueTask DisposeAsync() => cloud.DisposeAsync();
}

public sealed class SceneViewNativeMapSkyWorkPreparedFrame
{
    internal SceneViewNativeMapSkyWorkPreparedFrame(NativeMapFrameContext? context,
        SceneViewNativeMapSkyCurrentSelection selection, SkySceneSourceFramePacket source,
        SkyAppliedSourceStarFrame star, SceneViewNativeMapSkyCloudPreparedFrame cloud,
        SkySceneProducerFrame producers, uint generation, MapOwnedGraphicsMaskPrefix? prefix)
    { Context = context; Selection = selection; SourceFrame = source; Star = star; Cloud = cloud;
      Producers = producers; GenerationIndex = generation; FirstGraphicsPrefix = prefix; }
    public NativeMapFrameContext? Context { get; }
    public SceneViewNativeMapSkyCurrentSelection Selection { get; }
    public SkySceneSourceFramePacket SourceFrame { get; }
    public SkyAppliedSourceStarFrame Star { get; }
    public SceneViewNativeMapSkyCloudPreparedFrame Cloud { get; }
    public SkySceneProducerFrame Producers { get; }
    public SkyAppliedSourceStageFrame Stages => Selection.Stages;
    public SkyAmbientSourceEnableInputs Enable => Selection.Enable;
    public uint GenerationIndex { get; }
    public MapOwnedGraphicsMaskPrefix? FirstGraphicsPrefix { get; }
    /// <summary>Our complete-generation backend batch: 6base +1copy call
    /// +36filter steps. This is explicitly not the original Clock254 budget.</summary>
    public uint MaximumWorkUnits => 43;
    public SkySceneOptionalLightInputs? OptionalLight => null; // typed WORK1F2=false above
}
