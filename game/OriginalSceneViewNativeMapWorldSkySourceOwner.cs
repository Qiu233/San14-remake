using San14.Data;

/// <summary>Actual World root80 selection, Directional Sky/Star/Cloud GPU work,
/// current Q80 publication and complete Host source-time commit. This owner
/// never uses the HDRI record splitter or substitutes a completed cube.</summary>
public sealed class OriginalSceneViewNativeMapWorldSkySourceOwner : IAsyncDisposable
{
    public IReadOnlyList<OriginalNativeMapStageTiming> LastCurrentTimings { get; private set; } =
        Array.Empty<OriginalNativeMapStageTiming>();
    public OriginalSkyDirectionalAmbientDeviceOwner Device { get; }
    public OriginalSceneViewNativeMapWorldPatternResourceOwner PatternResources { get; }
    public Task<IReadOnlyList<OriginalPatternCameraStaReadyPlaneEntry>> BorrowReadyStaPlaneEntriesAsync()
    {
        ObjectDisposedException.ThrowIf(retiring, this);
        return optional.BorrowReadyStaPlaneEntriesAsync();
    }
    private readonly OriginalMapSourceCreatedDirectionalLightingDeviceOwner lighting;
    private readonly OriginalSkyAppliedSourceStageOwner stages;
    private readonly OriginalSkyDirectionalOptionalSourceOwner optional;
    private readonly OriginalDeferredSelected383SkyProjectionOwner projection;
    private readonly LoadedOriginalSkyDirectionalAtmosphereNativeSource atmosphere;
    private readonly LoadedShaderContainer shader;
    private readonly string clockIdentity;
    private OriginalMapSourceCreatedGraphicsColorMaskOwner? history;
    private OriginalNativeMapFrameContext? completed;
    private OriginalSkyDirectionalOptionalFrame? candidate;
    private OriginalMapOwnedGraphicsMaskPrefix? prefix;
    private float callerClock;
    private readonly object lifetime = new();
    private volatile bool retiring;
    private Task? disposal;

    private OriginalSceneViewNativeMapWorldSkySourceOwner(
        OriginalMapSourceCreatedDirectionalLightingDeviceOwner lighting,
        OriginalSkyAppliedSourceStageOwner stages, OriginalSkyDirectionalOptionalSourceOwner optional,
        OriginalSkyDirectionalAmbientDeviceOwner device, OriginalDeferredSelected383SkyProjectionOwner projection,
        LoadedOriginalSkyDirectionalAtmosphereNativeSource atmosphere, LoadedShaderContainer shader, string clockIdentity,
        OriginalSceneViewNativeMapWorldPatternResourceOwner patternResources)
        => (this.lighting, this.stages, this.optional, Device, this.projection,
            this.atmosphere, this.shader, this.clockIdentity, PatternResources) =
            (lighting, stages, optional, device, projection, atmosphere, shader, clockIdentity, patternResources);

    public static async Task<OriginalSceneViewNativeMapWorldSkySourceOwner> BuildAsync(
        OriginalSceneViewNativeMapLoadInputs loaded, Vkd3dShaderCompiler compiler,
        OriginalMapSourceCreatedDirectionalLightingDeviceOwner lighting,
        OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame bootstrap,
        OriginalMainSceneNode51Execution bootstrapExecution, bool allowReadback = false,
        Func<OriginalSkyDirectionalAmbientDeviceOwner, Task>? configureBeforeStartup = null)
    {
        if (configureBeforeStartup is not null && !allowReadback)
            throw new InvalidOperationException("Sky numeric observation requires explicit readback construction.");
        var repo = loaded.Repository;
        string locale = loaded.InitialFrame.MainCamera.Camera.ExecutableLocale;
        string identity = bootstrap.First.Lighting.Binding.ViewIdentity + "/Directional-Sky";
        string clock = identity + "/owned-presentation-clock254";
        var source = SelectSource(bootstrap, bootstrap.First.Lighting, bootstrapExecution);
        var stages = OriginalSkyAppliedSourceStageOwner.CreateFresh(repo, locale, identity,
            source.AcceptedLighting.Binding.CameraOwnerIdentity);
        var firstStages = stages.PrepareCurrent(bootstrap.Present, source.AcceptedLighting);
        var raster = OriginalSkyFirstGraphicsPrefixSource.ReadFresh(locale,
            identity + "/bootstrap-pipeline-plan", source.AcceptedLighting.Binding.ViewIdentity)
            .PrepareFirstPrefix(new(bootstrap.First.FrameNumber, source.AcceptedLighting.Binding.ViewIdentity,
                true, "original fresh frontend pipeline plan; no WORK submission or Host acknowledgement"));
        var optional = OriginalSkyDirectionalOptionalSourceOwner.ReadSource(repo, locale, compiler, source,
            firstStages, bootstrapExecution, raster.ColorWriteMask, raster.SkyStarNativeCullMode, identity + "/optional");
        OriginalSkyDirectionalAmbientDeviceOwner? device = null;
        OriginalSceneViewNativeMapWorldPatternResourceOwner? patternResources = null;
        try
        {
            device = await OriginalSkyDirectionalAmbientDeviceOwner.BuildAsync(repo, locale, compiler,
                source, firstStages, raster.ColorWriteMask, identity, clock, optional.BuildAsync, allowReadback);
            var native = OriginalSkyDirectionalAtmosphereNativeSource.Load(repo, locale, OriginalNativeLogDispatch.Sse);
            var shader = repo.LoadShaderContainer("system", 0x340AC335);
            if (configureBeforeStartup is not null) await configureBeforeStartup(device);
            await device.PrepareInitialGeneratedTargetsAsync(source, firstStages, device.InitialSourceFrame,
                optional.RequireInitialProducerFrame().Producers);
            var ambient = OriginalSkyAmbientResources.Load(repo, repo.LoadObjects("system", 0xDAB638B2),
                0x910603AC, repo.LoadObjects("system", 0x718CCB8B), repo.LoadObjects("system", 0x93FB3280));
            var projection = OriginalDeferredSelected383SkyProjectionOwner.Create(repo, locale, ambient,
                source.AcceptedLighting.Binding, identity + "/Q80", identity);
            patternResources = await OriginalSceneViewNativeMapWorldPatternResourceOwner.BuildAsync(
                repo, locale, bootstrap, optional, lighting);
            return new(lighting, stages, optional, device, projection, native, shader, clock, patternResources);
        }
        catch (Exception failure)
        {
            var package = new OriginalNativeMapBorrowerRetirementPackage(identity + "/partial",
                failure is OriginalNativeMapBorrowerConstructionException nested ? nested.RetainedPackage : null,
                device, patternResources, optional);
            try { await package.DisposeAsync(); }
            catch (Exception retirementFailure)
            {
                package.RetainFailure();
                throw new OriginalNativeMapBorrowerConstructionException(failure, retirementFailure, package);
            }
            throw;
        }
    }

    private static OriginalSkyDirectionalSelectedLightSource SelectSource(
        OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame world,
        OriginalMapSourceCreatedLightingPacket outer, OriginalMainSceneNode51Execution execution)
    {
        world.First.Feature.ValidateFrame(world.First.Camera, outer);
        execution.RequireSelectedView(outer.FrameNumber, outer.Binding);
        if (execution.SelectedViewIndex != 0 || outer.Binding != world.First.Lighting.Binding ||
            !outer.LocalLightData.Span.SequenceEqual(world.First.Lighting.LocalLightData.Span) ||
            !ReferenceEquals(world.Rgb, world.Present.AcceptedSeasonCamera) ||
            world.First.Common.CurrentViewAux3F2 is not byte selector || selector is < 1 or > 32)
            throw new InvalidDataException("World Sky requires its actual primary Main/View, RGB/Present and current Common selector.");
        uint mask = 1u << (selector - 1);
        var selected = world.Queue.FirstOrDefault(r => (r.CurrentWorldPQWord78 & ~mask) == 0)
            ?? throw new InvalidDataException("Actual ordered World contains no current Sky subset-mask record.");
        var record = world.First.ViewFields.CurrentPublication.SelectedShadowRecord1BD0
            ?? throw new InvalidDataException("This World Sky branch requires its independently selected owned shadow payload.");
        if (selected.CurrentItemIndex != record.CurrentQueueIndex || selected.CurrentWorldPQOwnerIdentity != record.RecordOwnerIdentity ||
            record.RecordWord78 != 0x80 || record.LightOwnerIdentity != outer.Binding.LightOwnerIdentity ||
            record.FrameNumber != outer.FrameNumber || !record.CompleteLightData.Span.SequenceEqual(outer.LocalLightData.Span))
            throw new NotSupportedException("Current World Sky needs its independently selected exact root80 payload.");
        var payload = OriginalSkySceneSelectedViewLightSource.Capture(record.CompleteLightData.Span,
            new(outer.FrameNumber, outer.Binding.ViewIdentity, record.RecordOwnerIdentity, record.PayloadOwnerIdentity));
        return OriginalSkyDirectionalSelectedLightSource.ReadCurrent(payload, outer);
    }

    public void BindActualGraphicsColorMaskOwner(OriginalMapSourceCreatedGraphicsColorMaskOwner actual)
    {
        ObjectDisposedException.ThrowIf(retiring, this); ArgumentNullException.ThrowIfNull(actual);
        if (history is not null || completed is not null) throw new InvalidOperationException("World Sky history already bound.");
        actual.BindCurrentClipHistory(optional.ClipSource, context =>
        {
            var clip = optional.RequireCurrentClipPacket();
            if (clip.FrameNumber != context.FrameNumber) throw new InvalidDataException("Cloud clip belongs to another Host attempt.");
            return clip;
        });
        history = actual;
    }

    /// <summary>Load/reload boundary only. The caller has drained this Host's
    /// accepted request before installing a new source season. This retains
    /// state168, WORK/cubes, clock, optional phases and paired Clip history.</summary>
    public void RequireCompatibleSeasonResources(OriginalSceneViewNativeMapSeasonResourceBundle resources)
    {
        ObjectDisposedException.ThrowIf(retiring,this);ArgumentNullException.ThrowIfNull(resources);
        if(resources.Locale!=stages.Locale)
            throw new InvalidDataException("Season Sky resources must use this live owner's executable locale.");
        optional.RequireCompatibleSeasonSources(resources);
        Device.RequireCompatibleSeasonResources(resources.CameraSource);
    }
    public void InstallCompatibleSeasonResources(OriginalSceneViewNativeMapSeasonResourceBundle resources)
    {RequireCompatibleSeasonResources(resources);optional.InstallCompatibleSeasonSources(resources);}

    public async Task SubmitCurrentAsync(OriginalMapSourceCreatedNativeSessionPreparedFrame frame,
        OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame world,
        OriginalMainSceneNode51Execution execution, float delta,
        OriginalDirectionalLightRetainedConsumerReceipt? consumer)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        var timings=new List<OriginalNativeMapStageTiming>();double previous=0;
        void Mark(string name)
        {
            double elapsed=timer.Elapsed.TotalMilliseconds;
            timings.Add(new(name,elapsed-previous,elapsed));previous=elapsed;
        }
        ObjectDisposedException.ThrowIf(retiring, this);
        if (!float.IsFinite(delta) || delta < 0 || !float.IsFinite(callerClock + delta))
            throw new InvalidDataException("Owned presentation clock requires a finite nonnegative step.");
        completed = null; candidate = null; prefix = null;
        _ = PatternResources.CaptureCurrent(world);
        var source = SelectSource(world, frame.Lighting, execution);
        var currentStages = stages.PrepareCurrent(world.Present, source.AcceptedLighting);
        var sourceFrame = Device.PrepareCurrentSourceFrame(source, currentStages);
        Mark("current-source-preparation");
        var actualHistory = history ?? throw new InvalidDataException("World Sky needs this Host's actual final graphics history.");
        prefix = actualHistory.PrepareCurrentSkyPrefix(frame.Context, frame.Lighting.Binding.ViewIdentity,
            "actual World Directional WORK before model/Terrain/Aux; complete final history");
        candidate = optional.PrepareCurrent(source, currentStages, execution, sourceFrame, prefix, delta);
        Mark("current-optional-producers");
        actualHistory.BeginWorkSubmission(frame.Context, prefix);
        callerClock += delta;
        var enable = OriginalSkyAmbientSourceEnableInputs.PrepareCurrentSourcePresentTable(world.Rgb,
            world.First.Common, frame.Lighting.Binding.ViewIdentity);
        await Device.SubmitCompleteCurrentAsync(source, currentStages, candidate.Producers, enable,
            new(frame.Context.FrameNumber, clockIdentity, callerClock,
                "explicit owned presentation clock; backend complete-generation batch; native Clock254 pacing not claimed"), sourceFrame);
        Mark("current-device-submission");
        completed = frame.Context;
        LastCurrentTimings=timings.AsReadOnly();
    }

    public OriginalSkyDirectionalAmbientDeviceBinding SelectCurrentAmbient(
        OriginalMapSourceCreatedNativeSessionPreparedFrame frame,
        OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame world)
    {
        ValidateCompleteFrameCandidate(frame.Context);
        var state = Device.CaptureCurrentState(frame.Context.FrameNumber);
        var selected = projection.PrepareCurrentQ80(frame.Selection, frame.Lighting, world.Camera19B8,
            state, true, "actual same-Host Directional Sky publishes owned wrapper at Q80");
        return Device.BorrowCurrentQ80Wrapper(selected, state, frame.Selection, frame.Lighting);
    }

    public void ValidateCompleteFrameCandidate(OriginalNativeMapFrameContext context)
    {
        ObjectDisposedException.ThrowIf(retiring, this);
        if (!ReferenceEquals(completed, context) || candidate is null || prefix?.FrameNumber != context.FrameNumber)
            throw new InvalidDataException("Complete Host has no same-attempt World Sky candidate.");
        optional.ValidateCandidate(candidate);
    }
    public OriginalMapOwnedGraphicsMaskPrefix RequireCompletedSkyGraphicsPrefix(OriginalNativeMapFrameContext context)
    { ValidateCompleteFrameCandidate(context); return prefix!; }
    public void CommitCompleteFrame(OriginalNativeMapFrameContext context)
    { ValidateCompleteFrameCandidate(context); optional.CommitCompleteHost(candidate!); candidate = null; }
    public ValueTask DisposeAsync()
    { lock (lifetime) { retiring = true; return new(disposal ??= RetireAsync()); } }
    private async Task RetireAsync()
    { await Device.DisposeAsync(); await PatternResources.DisposeAsync(); await optional.DisposeAsync(); }
}
