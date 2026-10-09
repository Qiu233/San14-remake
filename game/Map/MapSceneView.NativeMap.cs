using Godot;
using San14.Core;
using San14.Data;

/// <summary>Detached CPU inputs for one queued production frame. Publishing a
/// render attempt copies the already prepared camera; it does not advance the
/// controller, reapply FOV or read live SceneView fields after an await.</summary>
public sealed record SceneViewNativeMapFrameCapture(
    GameState World, MainMapCameraFrame MainCamera,
    IReadOnlyList<NativeTerrainBatch> TerrainBatches,
    Vector2I DisplayExtent, uint SceneGeneration, long RequestNumber,
    string SourceProvenance)
{
    // Presentation time comes only from this SceneView's eligible Godot
    // process updates. Coalescing requests must not lose elapsed animation
    // time or apply an earlier queued delta twice after another frame commits.
    public double PresentationElapsedSeconds { get; init; }
    public float PresentationDeltaSeconds { get; init; }
    /// <summary>Immutable CPU triangle snapshot for this captured patch set.
    /// It stays valid when the preview replaces or retires its mesh cache.</summary>
    public TerrainPicker? TerrainPicker { get; init; }

    public SceneViewNativeMapFrameCapture WithPresentationDeltaSince(double acceptedElapsedSeconds)
    {
        double delta = PresentationElapsedSeconds - acceptedElapsedSeconds;
        if (!double.IsFinite(PresentationElapsedSeconds) || !double.IsFinite(acceptedElapsedSeconds) ||
            acceptedElapsedSeconds < 0 || delta < 0 || delta > float.MaxValue)
            throw new InvalidDataException("Native presentation requires finite ordered owned process time.");
        return this with { PresentationDeltaSeconds = (float)delta };
    }

    public SceneViewNativeMapFrameCapture ForAcceptedContext(NativeMapFrameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var camera = MainCamera.Camera.CopyAcceptedSnapshotForOwnedFrame(context.FrameNumber,
            MainCamera.Camera.FrameProvenance + ";" + SourceProvenance + ";accepted-scene-attempt=" + context.FrameNumber);
        var prepared = PreparedCameraSceneSource.ReadCompletePrefix(camera.Prefix.Span);
        if (context.PreparedCamera != prepared)
            throw new InvalidDataException("SceneView production capture differs from the camera submitted to this exact Scene attempt.");
        return this with { MainCamera = MainCamera with { Camera = camera } };
    }
}

/// <summary>Already loaded inert source assets. No preview material, mesh or
/// camera owner is borrowed by a production dependency factory.</summary>
public sealed record SceneViewNativeMapLoadInputs(
    GameResourceRepository Repository, LoadedSceneResources Scene,
    LoadedTerrainResources Terrain, LoadedTerrainInitialHeightResources InitialHeight,
    LoadedTerrainGbiMapResources TerrainMaterial, MapWorldProjection Projection,
    ScenarioCityPreview Scenario, int PlayerForceSlot,
    SceneViewNativeMapFrameCapture InitialFrame);

public sealed record SceneViewNativeMapResizeInputs(
    IReadOnlyDictionary<uint, Vector2I> TerrainExtents,
    IReadOnlyDictionary<uint, Vector2I> OutputExtents);

/// <summary>The factory transfers ownership of these dependencies to the
/// SceneView. Host borrows them. Partial factory failures must retire resources
/// inside the factory before throwing. All current writers are mandatory typed
/// Host inputs; no absent writer is replaced with an empty callback.</summary>
public sealed record SceneViewNativeMapDependencies(
    Vkd3dShaderCompiler Compiler, MapSourceCreatedLightingDeviceOwner Lighting,
    NativeMapModelPopulationRenderer Population, NativeMapArmyPopulationRenderer Army,
    MapSourceCreatedPlayableSceneSource SceneSource, MapMaterialCachePrepared Materials,
    MapSourceCreatedF103CurrentFrame Bootstrap,
    DeferredEarlySourceViewQueriesPacket BootstrapEarly,
    MapSourceCreatedPlayableCurrentSources Current,
    Func<SceneViewNativeMapFrameCapture, MapSourceCreatedPlayableTerrainFrame> PrepareTerrainFrame,
    Func<SceneViewNativeMapFrameCapture, SceneViewNativeMapResizeInputs> PrepareResize)
{
    private int retiredStage, retiredOtherStage;
    public NativeMapSceneStages? Preceding { get; init; }
    public MapExposureOwner? Exposure { get; init; }
    /// <summary>Pure retained source-state commit after complete Host output.
    /// This callback receives the actual accepted context and cannot declare
    /// completion or replace any mandatory source/GPU producer.</summary>
    public Action<NativeMapFrameContext>? CommitAcceptedFrame { get; init; }
    /// <summary>Additional owned stage dependencies, already listed in their
    /// required retirement order, after geometry and before lighting/compiler.</summary>
    public IReadOnlyList<IAsyncDisposable> OtherOwnedStages { get; init; } = [];

    internal async Task RetireBorrowedOwnersAsync()
    {
        // Advance only after successful retirement. A retry resumes the first
        // failed owner rather than discarding the package or replaying owners
        // whose teardown has already completed.
        if (retiredStage == 0) { await Army.DisposeAsync(); retiredStage = 1; }
        if (retiredStage == 1) { await Population.DisposeAsync(); retiredStage = 2; }
        if (retiredStage == 2)
        {
            while (retiredOtherStage < OtherOwnedStages.Count)
            { await OtherOwnedStages[retiredOtherStage].DisposeAsync(); ++retiredOtherStage; }
            retiredStage = 3;
        }
        if (retiredStage == 3) { if (Exposure is not null) await Exposure.DisposeAsync(); retiredStage = 4; }
        if (retiredStage == 4) { await Lighting.DisposeAsync(); retiredStage = 5; }
        if (retiredStage == 5) { Compiler.Dispose(); retiredStage = 6; }
    }
}

public delegate Task<SceneViewNativeMapDependencies> SceneViewNativeMapDependencyFactory(
    SceneViewNativeMapLoadInputs loaded,
    Func<NativeMapFrameContext, SceneViewNativeMapFrameCapture> captureAcceptedFrame);

public partial class MapSceneView
{
    private sealed record NativeMapOwner(SceneViewNativeMapMountPackage Package);
    private SceneViewNativeMapDependencyFactory? _nativeMapDependencyFactory;
    private SceneViewNativeMapWorldDependencyFactory? _nativeWorldMapDependencyFactory;
    private LoadedTerrainInitialHeightResources? _nativeMapInitialHeight;
    private readonly SemaphoreSlim _nativeMapGate = new(1, 1);
    private NativeMapOwner? _nativeMapOwner;
    private SceneViewNativeMapFrameCapture? _nativeMapActiveCapture, _nativeMapPendingCapture;
    private NativeMapFrameContext? _nativeMapAcceptedContext;
    private SceneViewNativeMapFrameCapture? _nativeMapAcceptedCapture;
    private SceneViewNativeMapFrameCapture? _nativeMapPresentedCapture;
    private SceneViewNativeMapFrameCapture? _nativeMapLastQueuedCapture;
    private GameState? _nativeMapWorldSource, _nativeMapDetachedWorld;
    private GameState? _nativeMapSuspendedWorldSource;
    private Task _nativeMapRetirement = Task.CompletedTask;
    private readonly List<SceneViewNativeMapMountPackage> _nativeMapFailedRetirements = [];
    private bool _nativeMapPumpRunning, _nativeMapInitializing;
    private int _nativeMapGeneration;
    private long _nativeMapRequestNumber;
    private Vector2I? _nativeMapPendingExtent;
    private double _nativeMapPresentationElapsedSeconds, _nativeMapAcceptedPresentationSeconds;

    private void AdvanceNativeMapPresentationTime(double processDelta)
    {
        if (_nativeMapDependencyFactory is null && _nativeWorldMapDependencyFactory is null) return;
        if (ReferenceEquals(_logicalWorld, _nativeMapSuspendedWorldSource)) return;
        if (!double.IsFinite(processDelta) || processDelta < 0 ||
            !double.IsFinite(_nativeMapPresentationElapsedSeconds + processDelta))
            throw new InvalidDataException("Native presentation process delta is invalid.");
        _nativeMapPresentationElapsedSeconds += processDelta;
    }

    public bool HasNativeMapOutput => _d3d11Map is {IsClosed:false,LastReceipt:not null}||_nativeMapOwner?.Package.HasAcceptedOutput == true;
    public bool HasNativeWorldMapFactory => _nativeWorldMapDependencyFactory is not null;
    public Task NativeMapRetirement => Task.WhenAll(_nativeMapRetirement,_d3d11Retirement);
    public bool HasPendingNativeMapRetirement => _nativeMapFailedRetirements.Count != 0 ||
        !_nativeMapRetirement.IsCompleted || !_d3d11Retirement.IsCompleted || SceneViewNativeMapWorldPackage.FailedRetirements.Count != 0 ||
        SceneViewNativeMapWorldPartialRetirementPackage.FailedPackages.Count != 0;

    /// <summary>A current source writer may request a new frame even when the
    /// camera and campaign state stayed unchanged.</summary>
    public void RequestNativeMapFrame()
    {
        _nativeMapSuspendedWorldSource = null;
        _nativeMapLastQueuedCapture = null;
        ScheduleNativeMapFrame();
    }

    /// <summary>Supply the real source/current writers before loading, or
    /// retry a loaded preview when its producers become available. The default
    /// client remains playable until a complete first native frame succeeds.</summary>
    public void SetNativeMapDependencyFactory(SceneViewNativeMapDependencyFactory? factory)
    {
        ReleaseNativeMap();
        _nativeMapDependencyFactory = factory;
        _nativeWorldMapDependencyFactory = null;
        if (_embedded && _loaded) _ = InitializeNativeMapAsync(_sceneLoadGeneration);
    }

    /// <summary>Explicitly select the independent five-root World route.
    /// An accepted complete first frame is required before hiding the preview.</summary>
    public void SetNativeWorldMapDependencyFactory(SceneViewNativeMapWorldDependencyFactory? factory)
    {
        ReleaseNativeMap();
        _nativeWorldMapDependencyFactory = factory;
        _nativeMapDependencyFactory = null;
        if (_embedded && _loaded) _ = InitializeNativeMapAsync(_sceneLoadGeneration);
    }

    private SceneViewNativeMapFrameCapture CaptureNativeMapFrame(Vector2I? extent = null)
    {
        if (_logicalWorld is not { } world || _sourceMainCameraFrame is not { } frame ||
            _terrainMeshes is null || _nativeMapInitialHeight is null || _terrainPicker is null)
            throw new InvalidDataException("Production map needs its loaded current world, prepared Main camera and original height source.");
        if (!ReferenceEquals(world, _nativeMapWorldSource))
        {
            // Commands replace GameState. Detach mutable state once per
            // publication; camera movement reuses that captured world.
            _nativeMapDetachedWorld = GameStateCopy.Copy(world);
            _nativeMapWorldSource = world;
        }
        var groups = _activeTerrainPatches.Select(patch =>
        {
            uint edges = checked((uint)patch.Mesh.GetMeta("source_patch_edge_flags").AsInt64());
            var input = TerrainPatchInputMath.Ordinary(_terrainMeshes.Source.SpatialTree, patch.SourceNode,
                _nativeMapInitialHeight.HeightBinding.Image.Width, _nativeMapInitialHeight.ControlBinding.Image.Width, edges);
            return (Coarse: patch.SourceNode.Level != 0, Input: input);
        }).GroupBy(row => row.Coarse).Select(group => new NativeTerrainBatch(group.Key,
            Array.AsReadOnly(group.Select(row => row.Input).ToArray()))).ToArray();
        if (groups.Length == 0) throw new InvalidDataException("Production map has no current source-selected terrain patches.");
        Vector2 displaySize = _nativeMapOwner?.Package.Surface.Size ?? _surface.Size;
        if (extent is null && (!displaySize.IsFinite() || displaySize.X <= 0 || displaySize.Y <= 0))
            throw new InvalidDataException("Production map display layout has no current positive extent.");
        Vector2I display = extent ?? new((int)Math.Ceiling(displaySize.X), (int)Math.Ceiling(displaySize.Y));
        return new(_nativeMapDetachedWorld!, frame with { Camera = frame.Camera.CopyAcceptedSnapshot() },
            Array.AsReadOnly(groups), display, _sceneLoadGeneration, checked(++_nativeMapRequestNumber),
            "SceneView-loaded-current-source;scene-generation=" + _sceneLoadGeneration)
            { PresentationElapsedSeconds = _nativeMapPresentationElapsedSeconds, TerrainPicker = _terrainPicker };
    }

    private SceneViewNativeMapFrameCapture RequireNativeMapAcceptedCapture(NativeMapFrameContext context)
    {
        if (ReferenceEquals(context, _nativeMapAcceptedContext) && _nativeMapAcceptedCapture is { } captured) return captured;
        if (_nativeMapAcceptedContext is not null)
            throw new InvalidDataException("A queued SceneView request cannot publish into a second Scene context.");
        var accepted = (_nativeMapActiveCapture ?? throw new InvalidDataException("No captured SceneView request owns this accepted attempt."))
            .ForAcceptedContext(context);
        _nativeMapAcceptedContext = context; _nativeMapAcceptedCapture = accepted;
        return accepted;
    }

    private void InstallNativeMapCapture(SceneViewNativeMapFrameCapture? capture)
    {
        _nativeMapActiveCapture = capture;
        _nativeMapAcceptedContext = null; _nativeMapAcceptedCapture = null;
    }

    private bool NativeMapGenerationIsCurrent(uint scene, int native) =>
        scene == _sceneLoadGeneration && native == _nativeMapGeneration &&
        GodotObject.IsInstanceValid(this) && IsInsideTree();

    private async Task InitializeNativeMapAsync(uint sceneGeneration, NativeMapOwner? replacing = null,
        SceneViewNativeMapFrameCapture? replacementRequest = null)
    {
        if (!_embedded || !_loaded || _nativeMapInitializing) return;
        if(UseD3D11Map)
        {
            await InitializeD3D11MapAsync(sceneGeneration);
            return;
        }
        int generation = _nativeMapGeneration;
        var factory = _nativeMapDependencyFactory;
        var worldFactory = _nativeWorldMapDependencyFactory;
        if (factory is null && worldFactory is null)
        {
            RecordNativeMapUnavailable(new MapSourceCreatedPlayableSourceUnavailableException(
                "SceneView current production dependencies have not been supplied",
                MapSourceCreatedPlayableRequiredInput.CurrentTerrainAndArmyPrimitivePublication,
                MapSourceCreatedPlayableRequiredInput.CurrentGeometryStencilPublication,
                MapSourceCreatedPlayableRequiredInput.CurrentSkyAtmosphereOwner));
            return;
        }
        // Mark before the first await. A replacement keeps the old accepted
        // canvas mounted, but must not submit any more frames to its sources.
        _nativeMapInitializing = true;
        bool gateHeld = false;
        bool replacementCommitted = false;
        SceneViewNativeMapLoadInputs? loaded = null;
        SceneViewNativeMapMountPackage? package = null;
        try
        {
            // Configure can load cached assets before Godot has sorted the
            // aspect/container hierarchy. Capture the real layout after it.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!NativeMapGenerationIsCurrent(sceneGeneration, generation)) return;
            // ProcessFrame precedes _Process. Wait for actual owned elapsed
            // time; MotionBlur must not receive a fabricated zero interval.
            while (_nativeMapPresentationElapsedSeconds <= _nativeMapAcceptedPresentationSeconds)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!NativeMapGenerationIsCurrent(sceneGeneration, generation)) return;
            }
            if (_loadedSceneResources is null || _terrainMeshes is null || _terrainGbiMaterial is null ||
                _nativeMapInitialHeight is null || _projection is null || _sourceSnapshot is null || _playerForceId is not int player)
                throw new InvalidDataException("SceneView production map sources are not yet loaded.");
            loaded = new(_repository, _loadedSceneResources, _terrainMeshes.Source, _nativeMapInitialHeight,
                _terrainGbiMaterial.Source, _projection, _sourceSnapshot, player,
                CaptureNativeMapFrame(_nativeMapPendingExtent).WithPresentationDeltaSince(_nativeMapAcceptedPresentationSeconds));
            await _nativeMapRetirement;
            await _nativeMapGate.WaitAsync();
            gateHeld = true;
            if (!NativeMapGenerationIsCurrent(sceneGeneration, generation)) return;
            if (replacing is not null && !ReferenceEquals(_nativeMapOwner, replacing)) return;
            // A previous failure still owns its borrowed dependencies. No new
            // source package is created until that retirement actually closes.
            await RetryNativeMapRetirementsAsync();
            InstallNativeMapCapture(loaded.InitialFrame);
            _nativeMapLastQueuedCapture = loaded.InitialFrame;
            // Requests captured before this bootstrap are already superseded
            // by it. Replaying one after acceptance would subtract a newer
            // accepted timestamp from an older pending timestamp.
            if (_nativeMapPendingCapture is { } pending && pending.RequestNumber <= loaded.InitialFrame.RequestNumber)
            {
                _nativeMapPendingCapture = null;
                _nativeMapPendingExtent = null;
            }
            if (worldFactory is not null)
            {
                var dependencies = await worldFactory(loaded, RequireNativeMapAcceptedCapture)
                    ?? throw new InvalidDataException("World dependency factory returned no owned source package.");
                package = SceneViewNativeMapMountPackage.FromWorld(dependencies);
            }
            else
            {
                var dependencies = await factory!(loaded, RequireNativeMapAcceptedCapture)
                    ?? throw new InvalidDataException("Production dependency factory returned no owned source package.");
                package = SceneViewNativeMapMountPackage.FromLegacy(_repository, dependencies);
            }
            if (!NativeMapGenerationIsCurrent(sceneGeneration, generation)) return;
            var first = RequireNativeTerrainFrame(package, loaded.InitialFrame);
            await package.BuildHostAsync(loaded.InitialFrame, first);
            if (!NativeMapGenerationIsCurrent(sceneGeneration, generation)) return;
            if (!package.HasAcceptedOutput) throw new InvalidDataException("Production map has no complete first-frame output.");
            if (_nativeMapAcceptedContext is null || _nativeMapAcceptedCapture is null)
                throw new InvalidDataException("Complete native output has no captured accepted context.");
            var parent = _sourceMainCameraAspect ?? throw new InvalidDataException("Production map has no original Main display container.");
            var acceptedCapture = _nativeMapAcceptedCapture;
            var mounted = new NativeMapOwner(package);
            package.Surface.Name = "PlayableOriginalNativeMap";
            package.Surface.BeforeAcceptedOutputPublished += context =>
            {
                // A failed candidate does not retire this canvas. Its callback
                // remains owned by the mounted package even after the async
                // construction generation changed.
                if (sceneGeneration != _sceneLoadGeneration || !ReferenceEquals(_nativeMapOwner, mounted)) return;
                if (!ReferenceEquals(context, _nativeMapAcceptedContext) || _nativeMapAcceptedCapture is null)
                    throw new InvalidDataException("Presented map image has no matching accepted camera/terrain capture.");
                _nativeMapPresentedCapture = _nativeMapAcceptedCapture;
            };
            package.Surface.Visible = false;
            parent.AddChild(package.Surface);
            package.Surface.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            package.Surface.MouseFilter = MouseFilterEnum.Stop;
            package.Surface.GuiInput += HandleNativeMapInput;
            package.Surface.DisplayExtentChanged += ScheduleNativeMapResize;
            // Publish image and picking together only after the candidate's
            // full first output and its source-state commit have succeeded.
            _nativeMapOwner = mounted;
            package = null;
            replacementCommitted = replacing is not null;
            _nativeMapPresentedCapture = acceptedCapture;
            _nativeMapAcceptedPresentationSeconds = loaded.InitialFrame.PresentationElapsedSeconds;
            SynchronizeNativeMapInputExtent();
            _surface.Visible = false;
            _nativeMapOwner.Package.Surface.Visible = true;
            SetMeta("native_map_production_ready", true);
            RemoveMeta("native_map_source_error");
            RemoveMeta("native_map_source_error_detail");
            RemoveMeta("native_map_missing_source_inputs");
            RemoveMeta("native_map_updates_suspended");
        }
        catch (Exception error)
        {
            if (NativeMapGenerationIsCurrent(sceneGeneration, generation))
            {
                RecordNativeMapUnavailable(error);
                if (replacing is not null && ReferenceEquals(_nativeMapOwner, replacing) &&
                    (loaded?.InitialFrame ?? replacementRequest) is { } failedCapture)
                    SuspendNativeMapFailedReplacement(failedCapture);
            }
        }
        finally
        {
            // Host retirement must complete before releasing anything it
            // borrowed. A failure keeps those owners alive for diagnostics.
            try
            {
                if (package is not null)
                    await RetireNativeMapPackageAsync(package);
            }
            catch (Exception error) { RecordNativeMapRetirementFailure(error); }
            if (gateHeld)
            {
                if (replacementCommitted)
                {
                    // Ownership has already moved to the complete candidate.
                    // Retire old even if a later UI publication threw. The
                    // ordinary retire path would deadlock on our held gate.
                    _nativeMapRetirement = RetireNativeMapReplacementAsync(replacing!);
                    await _nativeMapRetirement;
                }
                InstallNativeMapCapture(null);
                _nativeMapGate.Release();
            }
            // An explicit factory/scene replacement may already have started
            // another initialization. A stale finally must not clear its flag.
            if (generation == _nativeMapGeneration) _nativeMapInitializing = false;
            if (!_nativeMapInitializing && _nativeMapOwner is not null && _nativeMapPendingCapture is not null)
                StartNativeMapPump();
        }
    }

    private static MapSourceCreatedPlayableTerrainFrame RequireNativeTerrainFrame(
        SceneViewNativeMapMountPackage package, SceneViewNativeMapFrameCapture capture)
    {
        var request = package.PrepareTerrainFrame(capture)
            ?? throw new InvalidDataException("Current production terrain writer returned no frame.");
        if (request.Camera != PreparedCameraSceneSource.ReadCompletePrefix(capture.MainCamera.Camera.Prefix.Span) ||
            request.Batches.Count != capture.TerrainBatches.Count || request.Batches.Where((batch, i) =>
                batch.Coarse != capture.TerrainBatches[i].Coarse || !batch.Patches.SequenceEqual(capture.TerrainBatches[i].Patches)).Any())
            throw new InvalidDataException("Production frame writer changed the captured camera or source-selected terrain batch.");
        return request;
    }

    private void HandleNativeMapInput(InputEvent input)
    {
        // Existing selection/cursor code retains its original viewport bridge.
        SynchronizeNativeMapInputExtent();
        Run(() => OnInput(input));
    }

    private void SynchronizeNativeMapInputExtent()
    {
        if (_nativeMapOwner is not { } owner || owner.Package.Surface.Size.X <= 0 || owner.Package.Surface.Size.Y <= 0) return;
        // Stretch=true lets the container resize its SubViewport. Assigning
        // the child's Size directly is ignored by Godot and logs a warning.
        _surface.Size = owner.Package.Surface.Size;
    }

    private void ScheduleNativeMapFrame()
    {
        if(_d3d11Map is not null){ScheduleD3D11MapFrame();return;}
        if (!_embedded || !_loaded || (_nativeMapOwner is null && !_nativeMapInitializing)) return;
        if (_nativeMapSuspendedWorldSource is not null)
        {
            if (ReferenceEquals(_logicalWorld, _nativeMapSuspendedWorldSource)) return;
            _nativeMapSuspendedWorldSource = null;
            RemoveMeta("native_map_updates_suspended");
        }
        try
        {
            var capture = CaptureNativeMapFrame(_nativeMapPendingExtent);
            var prior = _nativeMapLastQueuedCapture;
            if (prior is not null && ReferenceEquals(capture.World, prior.World) &&
                capture.PresentationElapsedSeconds == prior.PresentationElapsedSeconds &&
                capture.DisplayExtent == prior.DisplayExtent && capture.SceneGeneration == prior.SceneGeneration &&
                capture.MainCamera.Camera.Prefix.Span.SequenceEqual(prior.MainCamera.Camera.Prefix.Span) &&
                capture.TerrainBatches.Count == prior.TerrainBatches.Count &&
                capture.TerrainBatches.Where((batch, i) => batch.Coarse != prior.TerrainBatches[i].Coarse ||
                    !batch.Patches.SequenceEqual(prior.TerrainBatches[i].Patches)).Any() == false) return;
            _nativeMapLastQueuedCapture = capture;
            _nativeMapPendingCapture = capture;
            if (_nativeMapOwner is not null) StartNativeMapPump();
        }
        catch (Exception error) { RecordNativeMapUnavailable(error); }
    }

    private void ScheduleNativeMapResize(Vector2I extent)
    {
        if (extent.X <= 0 || extent.Y <= 0) return;
        SynchronizeNativeMapInputExtent();
        _nativeMapPendingExtent = extent;
        ScheduleNativeMapFrame();
    }

    private void StartNativeMapPump()
    {
        if (_nativeMapPumpRunning || _nativeMapInitializing) return;
        _nativeMapPumpRunning = true;
        _ = PumpNativeMapAsync();
    }

    private async Task PumpNativeMapAsync()
    {
        await _nativeMapGate.WaitAsync();
        try
        {
            while (_nativeMapOwner is { } owner && _nativeMapPendingCapture is { } capture)
            {
                if (_nativeMapInitializing) break;
                int generation = _nativeMapGeneration;
                if (!NativeMapGenerationIsCurrent(capture.SceneGeneration, generation))
                {
                    _nativeMapPendingCapture = null;
                    break;
                }
                _nativeMapPendingCapture = null;
                capture = capture.WithPresentationDeltaSince(_nativeMapAcceptedPresentationSeconds);
                Vector2I? resize = _nativeMapPendingExtent;
                _nativeMapPendingExtent = null;
                InstallNativeMapCapture(capture);
                try
                {
                    // Replace loaded seasonal sources after the preceding
                    // request drained, retaining the actual renderer, clocks,
                    // controller and accepted image until a new full output.
                    if(owner.Package.SeasonSelection is { } mountedSeason&&capture.World.NativeMapSeason is { } season&&
                        MapSeasonMath.SelectMonth(season.ThirdDate.Month)!=mountedSeason)
                        await owner.Package.InstallSeasonResourcesAsync(capture);
                    var frame = RequireNativeTerrainFrame(owner.Package, capture);
                    if (resize is not null)
                    {
                        await owner.Package.ResizeTargetsAsync(capture);
                    }
                    if (!NativeMapGenerationIsCurrent(capture.SceneGeneration, generation)) break;
                    await owner.Package.RenderFrameAsync(capture, frame);
                    if (NativeMapGenerationIsCurrent(capture.SceneGeneration, generation))
                    {
                        // The actual surface has published this complete GPU
                        // output. A source-cache commit failure must not leave
                        // picking attached to another image's camera.
                        _nativeMapPresentedCapture = _nativeMapAcceptedCapture ?? capture;
                        _nativeMapAcceptedPresentationSeconds = capture.PresentationElapsedSeconds;
                        _surface.Visible = !owner.Package.HasAcceptedOutput;
                        owner.Package.Surface.Visible = owner.Package.HasAcceptedOutput;
                        SetMeta("native_map_production_ready", owner.Package.HasAcceptedOutput);
                        RemoveMeta("native_map_source_error");
                        RemoveMeta("native_map_source_error_detail");
                        RemoveMeta("native_map_missing_source_inputs");
                        RemoveMeta("native_map_updates_suspended");
                    }
                }
                catch (Exception error)
                {
                    if (NativeMapGenerationIsCurrent(capture.SceneGeneration, generation))
                    {
                        RecordNativeMapUnavailable(error);
                        SuspendNativeMapUnsupportedState(capture, error);
                        owner.Package.Surface.Visible = owner.Package.HasAcceptedOutput;
                        _surface.Visible = !owner.Package.HasAcceptedOutput;
                    }
                    break; // Retry only after a new current publication.
                }
                finally { InstallNativeMapCapture(null); }
            }
        }
        finally
        {
            _nativeMapGate.Release(); _nativeMapPumpRunning = false;
            if (!_nativeMapInitializing && _nativeMapOwner is not null && _nativeMapPendingCapture is not null)
                StartNativeMapPump();
        }
    }

    private void RecordNativeMapUnavailable(Exception error)
    {
        if (!GodotObject.IsInstanceValid(this)) return;
        SetMeta("native_map_production_ready", _nativeMapOwner?.Package.HasAcceptedOutput == true);
        SetMeta("native_map_source_error", error.Message);
        SetMeta("native_map_source_error_detail", error.ToString());
        if (error is MapSourceCreatedPlayableSourceUnavailableException missing)
            SetMeta("native_map_missing_source_inputs", new Godot.Collections.Array<string>(missing.RemainingRequiredInputs.Select(input => input.ToString())));
        else RemoveMeta("native_map_missing_source_inputs");
    }

    private void SuspendNativeMapUnsupportedState(
        SceneViewNativeMapFrameCapture capture, Exception error)
    {
        if (_nativeWorldMapDependencyFactory is null ||
            (error is not NotSupportedException && capture.World.Units.Count == 0 &&
             capture.World.Structures?.Count is not > 0) ||
            !ReferenceEquals(capture.World, _nativeMapDetachedWorld)) return;
        // Campaign commands replace GameState. Suspend only this exact failed
        // state, never a newer state that arrived while GPU work was awaited.
        SuspendNativeMapFailedReplacement(capture);
    }

    private void SuspendNativeMapFailedReplacement(SceneViewNativeMapFrameCapture capture)
    {
        if (!ReferenceEquals(capture.World, _nativeMapDetachedWorld)) return;
        _nativeMapSuspendedWorldSource = _nativeMapWorldSource;
        _nativeMapPendingCapture = null;
        SetMeta("native_map_updates_suspended", true);
    }

    private void ReleaseNativeMap()
    {
        ReleaseD3D11Map();
        ++_nativeMapGeneration;
        _nativeMapInitializing = false;
        _nativeMapPendingCapture = null; _nativeMapPendingExtent = null;
        _nativeMapLastQueuedCapture = null;
        _nativeMapPresentedCapture = null;
        _nativeMapWorldSource = null; _nativeMapDetachedWorld = null;
        _nativeMapSuspendedWorldSource = null;
        if (GodotObject.IsInstanceValid(this)) RemoveMeta("native_map_updates_suspended");
        _nativeMapPresentationElapsedSeconds = _nativeMapAcceptedPresentationSeconds = 0;
        var owner = _nativeMapOwner; _nativeMapOwner = null;
        if (GodotObject.IsInstanceValid(this)) SetMeta("native_map_production_ready", false);
        if (owner is not null) DetachNativeMapSurface(owner);
        if (GodotObject.IsInstanceValid(_surface)) _surface.Visible = true;
        if (owner is not null) _nativeMapRetirement = RetireNativeMapOwnerAsync(owner, _nativeMapRetirement);
    }

    private void DetachNativeMapSurface(NativeMapOwner owner)
    {
        if (!GodotObject.IsInstanceValid(owner.Package.Surface)) return;
        owner.Package.Surface.Visible = false;
        owner.Package.Surface.GuiInput -= HandleNativeMapInput;
        owner.Package.Surface.DisplayExtentChanged -= ScheduleNativeMapResize;
        owner.Package.Surface.GetParent()?.RemoveChild(owner.Package.Surface);
    }

    // Nonthrowing tracked retirement while the caller holds the gate. Failed
    // owners remain strongly retained by the existing retry mechanism.
    private async Task RetireNativeMapReplacementAsync(NativeMapOwner owner)
    {
        try
        {
            DetachNativeMapSurface(owner);
            await RetireNativeMapPackageAsync(owner.Package);
        }
        catch (Exception error)
        {
            // Detachment can fail before Dispose reached its own retention
            // path. Keep the transferred old owner for the same retry list.
            if (!_nativeMapFailedRetirements.Contains(owner.Package)) _nativeMapFailedRetirements.Add(owner.Package);
            RecordNativeMapRetirementFailure(error);
        }
    }

    private async Task RetireNativeMapOwnerAsync(NativeMapOwner owner, Task prior)
    {
        await prior;
        await _nativeMapGate.WaitAsync();
        try
        {
            await RetireNativeMapPackageAsync(owner.Package);
        }
        catch (Exception error) { RecordNativeMapRetirementFailure(error); }
        finally { _nativeMapGate.Release(); }
    }

    // Called only while holding the SceneView lifecycle gate.
    private async Task RetireNativeMapPackageAsync(SceneViewNativeMapMountPackage package)
    {
        try
        {
            await package.DisposeAsync();
            _nativeMapFailedRetirements.Remove(package);
        }
        catch
        {
            if (!_nativeMapFailedRetirements.Contains(package)) _nativeMapFailedRetirements.Add(package);
            throw;
        }
    }

    private async Task RetryNativeMapRetirementsAsync()
    {
        foreach (var package in SceneViewNativeMapWorldPackage.FailedRetirements)
            await package.DisposeAsync();
        foreach (var package in SceneViewNativeMapWorldPartialRetirementPackage.FailedPackages)
            await package.DisposeAsync();
        foreach (var package in _nativeMapFailedRetirements.ToArray())
            await RetireNativeMapPackageAsync(package);
        if (GodotObject.IsInstanceValid(this)) RemoveMeta("native_map_retirement_error");
    }

    private void RecordNativeMapRetirementFailure(Exception error)
    {
        if (GodotObject.IsInstanceValid(this)) SetMeta("native_map_retirement_error", error.Message);
        GD.PushError("SceneView native map retirement failed: " + error.Message);
    }
}
