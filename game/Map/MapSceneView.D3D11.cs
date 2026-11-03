using Godot;
using San14.Data;
using San14.Core;
using San14.Client.Gameplay;

public partial class MapSceneView
{
    private D3D11MapSurface? _d3d11Map;
    private D3D11CaptureEffects? _captureEffects;
    private SkyAtmosphereResourceOwner? _d3d11SkyResources;
    private D3D11MapMarkers? _d3d11Markers;
    private D3D11TerritoryPublisher? _d3d11Ownership;
    private D3D11ArmyPublisher? _d3d11Army;
    private D3D11CityGatePublisher? _d3d11CityGate;
    private D3D11AreaCapitalPublisher? _d3d11AreaCapital;
    private CityGateFlagMaterialResources? _d3d11FlagMaterials;
    private string? _d3d11FlagMaterialDirectory;
    private ArmyMaterialResources? _d3d11ArmyMaterials;
    private string? _d3d11ArmyMaterialDirectory;
    private D3D11CityGateWorldFrame? _preparedCityGateFlagSource;
    private D3D11AreaCapitalWorldFrame? _preparedAreaCapitalFlagSource;
    private IReadOnlyList<ArmyFlagMaterialRequest>? _preparedArmyFlagRequests;
    private RenderResourceContext? _d3d11ResourceContext;
    private string? _d3d11ResourceContextDirectory;
    private Func<GameState, SourceWorldContext?>? _d3d11CurrentWorldQueries;
    private Func<GameState, UnitState, ArmyGraphicsCurrentInputs?>? _d3d11ArmyMotionQueries;
    private Func<GameState, ArmyMapGroupingFrame?>? _d3d11ArmyGroupingQueries;
    private Func<GameState, ArmyMapPresenceFrame?>? _d3d11ArmyPresenceQueries;
    private Func<GameState,MapStaticModelAssetCatalog,UnitGeometry,MapStaticModelGroundHeightSource,
        IReadOnlyList<FacilitySceneConnection>,FacilitySceneModelFrame>? _facilityModelQuery;
    private Func<GameState,IReadOnlyList<FacilitySceneConnection>>? _initialFacilityConnectionsQuery;
    private Func<GameState,int,AreaAdministrationQuery<FacilityMapInfo>>? _facilityMapInfoQuery;
    private GameState? _facilityInfoCaptureWorld;
    private D3D11FacilityPublication? _facilityInfoCaptureModels;
    private FacilityMapInfoPublication? _facilityInfoCapture;
    private uint _d3d11CommonFrustumMode;
    private D3D11MapInput? _d3d11PresentedInput;
    private Task _d3d11Retirement = Task.CompletedTask;
    private CancellationTokenSource? _d3d11Opening;
    private D3D11MapInputDirectory? _d3d11InputDirectory;
    private double _d3d11Elapsed;
    private bool _d3d11TerritoryDisplay = true;
    private D3D11AnimationPacer? _d3d11AnimationPacer;
    private bool _d3d11AnimationDue;
    private bool UseD3D11Map => _embedded && RenderingServer.GetCurrentRenderingDriverName() == "d3d12"&&
        RenderingServer.GetRenderingDevice() is not null;
    private bool D3D11MapInteractive => _d3d11Map is { IsClosed: false, LastError: null, LastReceipt: not null };
    public void SetD3D11CurrentWorldQueries(Func<GameState, SourceWorldContext?> capture,
        Func<GameState, UnitState, ArmyGraphicsCurrentInputs?>? armyMotion=null,
        Func<GameState, ArmyMapGroupingFrame?>? armyGrouping=null,
        Func<GameState, ArmyMapPresenceFrame?>? armyPresence=null)
    {
        _d3d11CurrentWorldQueries = capture;
        _d3d11ArmyMotionQueries = armyMotion;
        _d3d11ArmyGroupingQueries = armyGrouping;
        _d3d11ArmyPresenceQueries = armyPresence;
    }

    internal void SetFacilitySceneModelQuery(Func<GameState,MapStaticModelAssetCatalog,UnitGeometry,
        MapStaticModelGroundHeightSource,IReadOnlyList<FacilitySceneConnection>,FacilitySceneModelFrame> query,
        Func<GameState,IReadOnlyList<FacilitySceneConnection>> initialConnections)
        => (_facilityModelQuery,_initialFacilityConnectionsQuery)=(query,initialConnections);

    internal void SetFacilityMapInfoQuery(Func<GameState,int,AreaAdministrationQuery<FacilityMapInfo>> query)
    {
        _facilityMapInfoQuery=query;
        _d3d11Markers?.SetFacilityMapInfoQuery(query);
    }

    private FacilityMapInfoPublication? CaptureNormalFacilityInfo(GameState world,D3D11FacilityPublication? models)
    {
        //Startup renders the real facility models, but has not installed the
        //campaign information-label query/service. Adoption binds it normally.
        if (IsNewGameScenarioPreview) return null;
        if(!ReferenceEquals(world,_facilityInfoCaptureWorld)||!ReferenceEquals(models,_facilityInfoCaptureModels))
        {
            _facilityInfoCapture=_d3d11Markers?.CaptureFacilityInfo(world,models);
            _facilityInfoCaptureWorld=world;_facilityInfoCaptureModels=models;
        }
        return _facilityInfoCapture;
    }

    private D3D11CityGatePublication? CaptureCityGateFacilityWorld(GameState world,D3D11CityGatePublication? cities)
    {
        if(cities is null)return null;
        var publisher=_d3d11Facilities??throw new InvalidDataException("facility.presentation.publisher");
        return publisher.Combine(cities,publisher.Capture(world,_d3d11CommonFrustumMode,CaptureFacilitySceneConnections(world)));
    }

    private async Task InitializeD3D11MapAsync(uint generation, CgQuality? quality = null,
        CampaignPeriodPlayback? periodQualityOwner = null)
    {
        if (_d3d11Map is not null || _d3d11Opening is not null || _sourceMainCameraFrame is not { } frame ||
            _sceneTerrain?.Source is not { } terrainSources) return;
        if (periodQualityOwner is not null && !IsPeriodCgQualityInspectionCurrent(periodQualityOwner)) return;
        var scene = terrainSources.Scene;
        D3D11MapSurface? surface = null;
        D3D11MapCpuResources? cpu = null;
        D3D11MapInitialEnvironment? environment = null;
        D3D11MapInputDirectory? inputDirectory = null;
        bool adopted = false;
        // CG replacement is ready only after its live-only publication has
        // passed Next and real Canvas acceptance, after the base Create image.
        var cgLiveAccepted = _cgQualityRebuilding
            ? new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        D3D11MapInput? cgLiveInput = null;
        bool cgLiveWaitStarted = false;
        var opening = new CancellationTokenSource();
        _d3d11Opening = opening;
        var token = opening.Token;
        bool IsCurrent() => ReferenceEquals(_d3d11Opening, opening) && !token.IsCancellationRequested &&
            generation == _sceneLoadGeneration && !_nativeMapClosing &&
            GodotObject.IsInstanceValid(this) && IsInsideTree() &&
            (periodQualityOwner is null || IsPeriodCgQualityInspectionCurrent(periodQualityOwner));
        try
        {
            // Keep this opening's transition flag with the accepted camera
            // captured above; background preparation may outlive later ticks.
            bool openingSceneryCameraStable = ActiveSourceCameraStable;
            // Logical locations are built before the source camera owner.
            // Army screen offsets need that loaded owner's E940 anchors.
            if(!IsNewGameScenarioPreview)_d3d11Markers!.ConfigureArmyLabels(_repository, _sourceSnapshot!,
                CaptureCurrentVisualWorld,ActiveSourceCameraParameters,CaptureCurrentArmyPresence);
            string source = Path.GetFullPath(ProjectSettings.GlobalizePath(ProjectSettings.GetSetting(
                "san14/render/source_package",
                $"user://render-cache/{_text.SourceLocale}/{scene.Scene.Source.DecodedSha256}/input").AsString()));
            string library = D3D11RenderSession.ResolveNativeLibraryPath();
            // Capture only CPU source data before either exclusive worker starts.
            // Each opening writes its own bank; a retired native session can
            // continue borrowing its previous files until its normal close.
            inputDirectory = new(source);
            source = inputDirectory.Source;
            var staticScene = RequireLoadedStaticMapScene(terrainSources);
            string locale = _text.SourceLocale;
            string scenarioKey = _logicalWorld!.ScenarioKey;
            var previewSeason=_scenarioPreviewCalendar;
            // The opening worker receives a captured season value, never the
            // borrowed execution World. Native input retains the committed anchor.
            var ownedSeason=periodQualityOwner is null ? _logicalWorld.NativeMapSeason
                : periodQualityOwner.Candidate!.NativeMapSeason;
            if(previewSeason is null&&ownedSeason is null)
                throw new InvalidDataException("Playable Main has no owned third-date season.");
            var cameraInputs = _sourceMainCameraInputs!;
            var currentField = _sourceMainCameraTerrain!.CurrentField;
            cpu = await Task.Run(() => previewSeason is {} preview
                ? D3D11MapCpuResources.PrepareScenarioPreview(staticScene,frame,
                    preview,locale,source,D3D11MapSurface.OutputWidth,D3D11MapSurface.OutputHeight,scenarioKey,token)
                : D3D11MapCpuResources.Prepare(staticScene,frame,
                    ownedSeason!,locale,source,D3D11MapSurface.OutputWidth,D3D11MapSurface.OutputHeight,scenarioKey,token),token);
            if (!IsCurrent()) return;
            var mapRepository = cpu.Repository;
            var seasonResources = cpu.SeasonResources;
            var seasonLibrary = cpu.SeasonLibrary;
            // Actual camera/Main state remains on its owning thread.
            var season = new D3D11CurrentSeasonPublisher(mapRepository, seasonResources,
                actualMainMode:_sourceMainMode, resourceLibrary:seasonLibrary);
            if (quality is { } appliedQuality) season.ApplyCgQuality(appliedQuality);
            long preparationNumber = 1;
            var openingMain = frame with { Camera = frame.Camera.CopyAcceptedSnapshotForOwnedFrame(
                preparationNumber, frame.Camera.FrameProvenance) };
            D3D11CurrentSeasonFrame? openingCurrent = previewSeason is {} openingPreview
                ? season.PrepareScenarioPreview(openingPreview,openingMain,0)
                : season.PrepareCurrent(ownedSeason!,openingMain,0);
            // The CPU writers consume the stored Mode packet values and its
            // accepted camera, never Mode.ValidateCurrent or the mode owner.
            // D3D12 scenery skips the fallback mode writer; while surface is
            // absent, ScheduleD3D11MapFrame cannot run another current update.
            var initialCurrent = openingCurrent;
            var preparingCpu = cpu;
            environment = await Task.Run(() => D3D11MapInitialEnvironment.Prepare(preparingCpu,
                initialCurrent, openingMain, cameraInputs, currentField, token), token);
            if (!IsCurrent()) return;
            season.BindCurrentPost(environment.Post,initialCurrent);

            // Transfer all prepared CPU owners together. No worker can access
            // this repository or these owners after the awaited preparation.
            string inputs = cpu.Inputs;
            string seasonalInputs = cpu.SeasonalInputsDirectory;
            var sceneMedia = cpu.SceneMedia;
            var skyPrograms = cpu.SkyPrograms;
            var fogPrograms = cpu.FogPrograms;
            var resourceContext = cpu.ResourceContext;
            var resourceSeasonBatch = cpu.ResourceSeasonBatch;
            var contextSeason = seasonResources.Source.CameraSource.Season.Resources.Action.Objects;
            var rainProvider = cpu.RainProvider;
            var specialEffectInputs = cpu.SpecialEffectInputs;
            var hdr = cpu.Hdr;
            var writtenSeasonResources = seasonResources;
            int writtenSeasonCount = seasonLibrary.LoadedInstallations.Count;
            RenderResourceContext.Batch? pendingResourceSeasonBatch = null;
            LoadedObjects? pendingContextSeason = null;
            SkyAtmosphereResourceOwner.Replacement? pendingAtmosphere = null;
            string? pendingAtmosphereDirectory = null;
            ShadowReceiverPatternResources? pendingReceiverPatterns = null;
            string? pendingPatternDirectory = null;
            var atmosphereOwner = environment.AtmosphereOwner;
            var atmosphereResources = environment.AtmosphereResources;
            var skyResources = environment.SkyResources;
            var starResources = environment.StarResources;
            var ambientResources = environment.AmbientResources;
            var cloudMedia = environment.CloudMedia;
            var cloudResources = environment.CloudResources;
            var receiverPatterns = environment.ReceiverPatterns;
            _routeStaticResources = cpu.RouteMedia ?? throw new InvalidDataException(
                "The product map opening has no loaded route media source.");
            _d3d11ResourceContext = resourceContext;
            string resourceContextDirectory = cpu.ResourceContextDirectory;
            _d3d11ResourceContextDirectory = resourceContextDirectory;
            var captureEffects = new D3D11CaptureEffects(mapRepository, resourceContext, generation,
                cpu.DomesticEffects ?? throw new InvalidDataException(
                    "The product map opening has no loaded domestic effect source."));
            _captureEffects = captureEffects;
            var effectInputsByMode = new G1EffectMapInputs?[5];
            G1EffectStoryViewInputs? storyEffectInputs = null;
            D3D11CurrentSeasonResources? effectInputResources = null;
            _d3d11Ownership!.WriteStaticInputs(Path.Combine(inputs, TerritoryStaticMapEncoding.NativeDirectory));
            _d3d11ArmySources = new(mapRepository, cpu.Scenario, cpu.Map,
                _projection!, TerrainMeshes!.Source.HeightField, locale, cpu.ArmyResources.GraphicsActions);
            if (!IsNewGameScenarioPreview) InstallCampaignArmyPublisher();
            _d3d11ArmyMaterials = cpu.ArmyMaterials;
            _d3d11ArmyMaterialDirectory = cpu.ArmyMaterialDirectory;
            _d3d11FlagMaterials = cpu.FlagMaterials;
            _d3d11FlagMaterialDirectory = cpu.FlagMaterialDirectory;
            var cityGatePublisher = new D3D11CityGatePublisher(mapRepository, scene, cpu.Scenario, cpu.Map,
                _logicalWorld!.NewGameOptions!, CaptureCurrentVisualWorld,
                cpu.PrimaryTemplates, cpu.CityAccessories);
            _d3d11CityGate = cityGatePublisher;
            if (periodQualityOwner is null)
                _facilityConnections = CaptureFacilitySceneConnections(_logicalWorld!);
            _d3d11Facilities = new(mapRepository, scene, cpu.PrimaryTemplates, CaptureCurrentFacilityModels);
            _d3d11AreaCapital = new(mapRepository, scene, cpu.Scenario,
                cityGatePublisher.PreparedBank,CaptureCurrentVisualWorld);
            _d3d11CommonFrustumMode = cpu.CommonFrustumMode;
            _d3d11SkyResources = atmosphereOwner;
            cpu.DetachResourceContext();
            _d3d11InputDirectory = inputDirectory;
            adopted = true;
            surface = new D3D11MapSurface
            {
                Name = "OriginalD3D11WorldMap",
                NativeDeviceOwner = RequireApplicationNativeMapDevice(),
                TerrainPrograms = cpu.TerrainPrograms ?? throw new InvalidDataException(
                    "The product map opening has no loaded Landscape program source.")
            };
            _d3d11Map = surface;
            var parent = _sourceMainCameraAspect as Control ?? _surface.GetParent() as Control ?? this;
            parent.AddChild(surface); surface.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            // Canvas Z order does not order GUI hit testing. Keep the map
            // image before its strategic controls in this same input mount.
            if (strategicMapOverlays is { } overlays && overlays.GetParent() == parent)
                parent.MoveChild(surface, overlays.GetIndex());
            surface.FramePrepared += (input, receipt) =>
            {
                if (_d3d11Map != surface || generation != _sceneLoadGeneration) return;
                if (pendingResourceSeasonBatch is { } acceptedBatch)
                {
                    resourceSeasonBatch.Dispose(); resourceSeasonBatch = acceptedBatch;
                    contextSeason = pendingContextSeason!;
                    pendingResourceSeasonBatch = null; pendingContextSeason = null;
                }
                if (input.AtmosphereResourceDirectory is not null &&
                    input.AtmosphereResourceDirectory == pendingAtmosphereDirectory)
                {
                    pendingAtmosphere!.Commit();
                    pendingAtmosphere = null; pendingAtmosphereDirectory = null;
                    skyResources = atmosphereOwner!.MainSky;
                }
                if (input.PatternResourceDirectory is not null &&
                    input.PatternResourceDirectory == pendingPatternDirectory)
                {
                    receiverPatterns = pendingReceiverPatterns!;
                    pendingReceiverPatterns = null; pendingPatternDirectory = null;
                }
                if (input.CurrentSeason is { } current) season.AcceptCurrent(current);
                if (input.CityGateWorld is { RequiredFlagMaterials.Count: > 0 } cityGate)
                    cityGatePublisher.PublishCompletedFlagMaterials(cityGate.RequiredFlagMaterials);
                if (input.AreaCapitalWorld is { } areaCapital)
                    cityGatePublisher.PublishCompletedFlagMaterials(areaCapital.FlagMaterialKeys);
                var markerOwner = _d3d11Markers;
                markerOwner?.BeginInformationPresentation();
                try
                {
                    if (input.SourceFrame is { } camera)
                        markerOwner?.Present(camera, input.AreaCapitalWorld, input.CityGateWorld);
                    markerOwner?.PresentArmy(input, receipt);
                    PresentInformationLayers(input);
                }
                finally { markerOwner?.EndInformationPresentation(); }
                if(input.SourceFrame is {} hitFrame)_d3d11Markers?.PrepareInformationHits(hitFrame);
                PresentAdministrationMap(input);
                if(input.SceneryWorld is {} scenery)_sceneryMap?.Present(scenery,false);
                SetMeta("native_map_army_label_unit_count", receipt.ArmyUnitPositions.Count);
            };
            surface.Accepted += (input, receipt) =>
            {
                if (_d3d11Map != surface || generation != _sceneLoadGeneration) return;
                _d3d11PresentedInput = input;
                PresentChronologyFrame(input);
                AcceptOccupationTerritory(input.TerritoryOwnership);
                _d3d11Markers?.PresentFacilities(input.FacilityInfo);
                AcceptFacilityConstructionModels(input.CityGateWorld?.Facilities);
                AcceptFacilityRepairModels(input.CityGateWorld?.Facilities);
                AcceptMapObjectRetirementModels(input.CityGateWorld?.Facilities);
                tacticArmyPositions = receipt.ArmyUnitPositions;
                PublishTacticPresentationViews();
                PublishFacilityArmyViews();
                BindHexFirePresentation(input.HexFire);
                captureEffects.Accept(input.Effects);
                _d3d11Markers?.AcceptArmyHits();
                if(input.SourceFrame is {} hitFrame)_d3d11Markers?.AcceptInformationHits(hitFrame);
                if(input.SceneryWorld is {} scenery)_sceneryMap?.Accept(scenery);
                _surface.Visible = false;
                ((SubViewport)_surface.GetChild(0)).RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
                SetMeta("native_map_production_ready", true);
                SetMeta("native_map_backend", "d3d11on12");
                SetMeta("native_map_presented_request", receipt.Request);
                SetMeta("native_map_world_publication", "actual-territory-army-city-gate-area-capital-and-labels");
                SetMeta("native_map_city_gate_revision", input.CityGateWorld?.Revision ?? 0);
                SetMeta("native_map_city_gate_primary_count", input.CityGateWorld?.Source.Primary.Owners.Count ?? 0);
                SetMeta("native_map_area_capital_primary_count", input.AreaCapitalWorld?.Source.Source.Owners.Count ?? 0);
                SetMeta("native_map_area_capital_revision", input.AreaCapitalWorld?.Revision ?? 0);
                SetMeta("native_map_army_published_actor_count", input.ArmyWorld?.Actors.Count ?? 0);
                SetMeta("native_map_army_published_unavailable_count", input.ArmyWorld?.Unavailable.Count ?? 0);
                SetMeta("native_map_current_season", input.CurrentSeason?.SeasonIndex ?? -1);
                RemoveMeta("native_map_source_error");
                RemoveMeta("native_map_source_error_detail");
                AcceptScenarioMapRoleFrame(surface,input,receipt);
                if (cgLiveInput is not null && ReferenceEquals(input, cgLiveInput))
                    cgLiveAccepted!.TrySetResult(true);
            };
            surface.Failed += error =>
            {
                if (cgLiveWaitStarted) cgLiveAccepted?.TrySetException(error);
                _startedMapFrame?.TrySetException(error);
                pendingResourceSeasonBatch?.Dispose();
                pendingResourceSeasonBatch = null; pendingContextSeason = null;
                pendingAtmosphere?.Dispose();
                pendingAtmosphere = null; pendingAtmosphereDirectory = null;
                pendingReceiverPatterns = null; pendingPatternDirectory = null;
                if (_d3d11Map == surface && generation == _sceneLoadGeneration)
                    RecordD3D11MapFailure(error);
            };
            if (periodQualityOwner is not null)
            {
                // All new capture publishers now exist. Capture synchronously
                // while Main still owns this exact checkpoint; no extra motion tick.
                if (!IsPeriodCgQualityInspectionCurrent(periodQualityOwner)) return;
                CapturePeriodPlaybackFrame(periodQualityOwner.Candidate!);
            }
            await surface.ConfigureAsync(source, library, CaptureD3D11Input(frame, 0,initialManagerUpdate:true,
                    sceneryCameraStable:openingSceneryCameraStable,
                    createBaseOnly:true), populationMask: 3,
                prepareCurrent: input =>
                {
                    var geometry = input.SourceFrame!;
                    var firstPrepared = openingCurrent;
                    bool initialCreate = firstPrepared is not null;
                    openingCurrent = null;
                    var currentMain = firstPrepared?.Mode.AcceptedMainFrame ?? geometry with
                    { Camera = geometry.Camera.CopyAcceptedSnapshotForOwnedFrame(
                        ++preparationNumber, geometry.Camera.FrameProvenance) };
                    if(firstPrepared is null&&_pendingStartedSeason is {} adoptingSeason)
                    {
                        season.AdoptStartedCampaignCalendar(adoptingSeason,currentMain,
                            _pendingAcceptedPreviewSeason??throw new InvalidDataException("The real final preview frame is absent."));
                        _pendingStartedSeason=null;_pendingAcceptedPreviewSeason=null;
                    }
                    var current = firstPrepared ?? (_scenarioPreviewCalendar is {} scenarioCalendar
                        ? season.PrepareScenarioPreview(scenarioCalendar,currentMain,(float)input.ElapsedSeconds)
                        : input.PeriodSeason is { } periodSeason
                            ? season.PrepareCurrent(periodSeason,currentMain,(float)input.ElapsedSeconds)
                            : season.PrepareCurrent(input.WorldState!,currentMain,(float)input.ElapsedSeconds));
                    string? atmosphereDirectory = null;
                    string? patternDirectory = null;
                    if (firstPrepared is null)
                    {
                        ambientResources!.RequireCurrent(current.Present);
                        cloudMedia!.RequireCurrent(current.Present,
                            current.Lights.First(light=>(light.GroupMask&0x80)!=0).State);
                        cloudResources!.RequireCurrentSurfaces(current.Present,
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        fogPrograms.RequireCurrentMask(current.Present);
                        var replacement = atmosphereOwner!.PrepareReplacement(mapRepository, skyPrograms, current.Present);
                        try
                        {
                            var candidateSky = replacement?.MainSky ?? atmosphereOwner.MainSky;
                            if (current.Present.AcceptedSeasonCamera.Inputs.Source.RenderingAtmosphere is not null)
                            {
                                var selectedSky = D3D11AtmosphereInitialConstantPreparation.SelectSkyProgram(current.Present,
                                    current.Lights.First(light=>(light.GroupMask&0x80)!=0).State,
                                    skyPrograms, candidateSky.Sampling.Presence);
                                candidateSky.RequireCurrent(selectedSky);
                                if (replacement is not null)
                                {
                                    var work = D3D11AmbientWorkInitialConstants.Prepare(mapRepository,
                                        current, cloudMedia, ambientResources.Declaration,
                                        skyPrograms, replacement.WorkSky!.Sampling);
                                    replacement.WorkSky.RequireCurrent(work.SkyMedia.Program);
                                }
                            }
                            starResources?.RequireCompatiblePackage(mapRepository,
                                current.Present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage);
                            if (replacement is not null)
                            {
                                atmosphereDirectory = Path.Combine(inputs, "atmosphere-replacements", Guid.NewGuid().ToString("N"));
                                ambientResources.WriteNativeInputs(atmosphereDirectory);
                                cloudMedia.WriteNativeInputs(atmosphereDirectory);
                                cloudResources.WriteSurfaceInputs(atmosphereDirectory);
                                replacement.WriteNativeInputs(atmosphereDirectory, cloudResources);
                                var global = current.Lights.First(light => (light.GroupMask & 0x80) != 0).Lighting.Directional[0].Color;
                                D3D11OwnedRuleInputPreparation.WriteWeather(
                                    Path.Combine(atmosphereDirectory, "weather-fog-camera-input", "initial", "weather"),
                                    current.Rgb, current.Present.AcceptedWeather, new(global.X, global.Y, global.Z));
                                pendingAtmosphere = replacement; pendingAtmosphereDirectory = atmosphereDirectory;
                            }
                        }
                        catch { replacement?.Dispose(); throw; }
                    }
                    try
                    {
                        if (!ReferenceEquals(writtenSeasonResources, season.LoadedResources))
                        {
                            var replacementPatterns = receiverPatterns!.PrepareReplacement(mapRepository,
                                current.Present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage,
                                season.LoadedResources.PatternTextures);
                            if (replacementPatterns is not null)
                            {
                                string root = Path.Combine(inputs, "pattern-replacements", Guid.NewGuid().ToString("N"));
                                replacementPatterns.WriteNativeInputs(root);
                                patternDirectory = Path.Combine(root, "normal-pattern-receiver-input");
                                pendingReceiverPatterns = replacementPatterns; pendingPatternDirectory = patternDirectory;
                            }
                            int loadedCount = seasonLibrary.LoadedInstallations.Count;
                            if (loadedCount != writtenSeasonCount)
                            {
                                seasonLibrary.WriteNativeLutInputs(seasonalInputs);
                                D3D11OwnedNumericInputPreparation.WritePostCallers(mapRepository,
                                    seasonLibrary, inputs);
                                writtenSeasonCount = loadedCount;
                            }
                            writtenSeasonResources = season.LoadedResources;
                        }
                        var nextContextSeason = season.LoadedResources.Source.CameraSource.Season.Resources.Action.Objects;
                        if (contextSeason.Source.ResourceUri != nextContextSeason.Source.ResourceUri ||
                            contextSeason.Source.DecodedSha256 != nextContextSeason.Source.DecodedSha256)
                        {
                            // The old Environment namespace retires before
                            // loading its replacement. Accepted frames and
                            // MaterialData retain their own prior snapshots.
                            resourceSeasonBatch.Dispose();
                            pendingResourceSeasonBatch = resourceContext.RegisterSeason(
                                season.LoadedResources.Source.CameraSource.Season.Resources.Action);
                            pendingContextSeason = nextContextSeason;
                        }
                        resourceContext.WriteNativeInputs(resourceContextDirectory);
                        if (!rainProvider.IsCurrent(resourceContext))
                            rainProvider = RainDropProviderSource.Load(mapRepository, resourceContext);
                        current.BindRainProvider(rainProvider);
                        specialEffectInputs.Bind(current);
                        var scenery=input.PeriodSeason is not null ? null : _sceneryMap?.Capture(input.WorldState!,current.Mode,
                            input.SceneryCameraStable??throw new InvalidDataException("Captured camera transition state is unavailable."),
                            new(D3D11MapSurface.OutputWidth,D3D11MapSurface.OutputHeight),
                            input.SceneryPresentation??throw new InvalidDataException("Captured map presentation is unavailable."));
                        var preparedInput = input.WithCurrentSeason(current, hdr, atmosphereDirectory,
                            resourceContext.Revision, patternDirectory)
                            .WithSceneryWorld(scenery).WithRoutes(PrepareRouteResources(input.Routes));
                        // Create accepts the base graph only. Existing live
                        // effects join the following normal prepared scene.
                        if (!initialCreate && captureEffects.HasWork && _d3d11AnimationPacer is { } effectPacer)
                        {
                            if (!ReferenceEquals(effectInputResources, season.LoadedResources))
                            {
                                Array.Clear(effectInputsByMode);
                                effectInputResources = season.LoadedResources;
                            }
                            int effectMode = current.Mode.Mode288;
                            if ((uint)effectMode >= (uint)effectInputsByMode.Length)
                                throw new InvalidDataException("Effect Main mode has no installed Camera source.");
                            // Each ordinary mode borrows its own actual package
                            // from this season's already loaded Camera graph.
                            var effectInputs = effectInputsByMode[effectMode] ??=
                                new(mapRepository, season.LoadedResources, resourceContext, effectMode,
                                    hdr.Resources[HdrShaderResources.MainLightingNode].Shader);
                            //The current native View has no primary-light
                            //publisher. Its normal Common light selector still
                            //selects the actual registered subset in the helper.
                            var effectFrame = effectInputs!.Prepare(current, D3D11MapSurface.OutputWidth,
                                D3D11MapSurface.OutputHeight, effectInputs.OwnedRendererSelection(current),
                                pendingAtmosphere?.MainSky ?? atmosphereOwner?.MainSky);
                            G1EffectMapInputFrame? storyFrame = null;
                            if (captureEffects.HasStoryWork)
                            {
                                storyEffectInputs ??= new(mapRepository);
                                storyFrame = storyEffectInputs.Prepare(input.StoryEffectCamera ??
                                    throw new InvalidOperationException("story_effect.mode1_captured_camera"), effectFrame,
                                    D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                            }
                            preparedInput = preparedInput.WithEffects(captureEffects.PrepareFrame(effectFrame,
                                input.ElapsedSeconds, effectPacer.FramesPerSecond(input.CommandMapFpsMode),
                                current.Mode.EcB0Index, surface.EffectComputeDevice, storyFrame));
                        }
                        if (!initialCreate && cgLiveAccepted is not null && cgLiveInput is null)
                            cgLiveInput = preparedInput;
                        return preparedInput;
                    }
                    catch
                    {
                        pendingResourceSeasonBatch?.Dispose();
                        pendingResourceSeasonBatch = null; pendingContextSeason = null;
                        pendingAtmosphere?.Dispose();
                        pendingAtmosphere = null; pendingAtmosphereDirectory = null;
                        pendingReceiverPatterns = null; pendingPatternDirectory = null;
                        throw;
                    }
                });
            if (_d3d11Map != surface || generation != _sceneLoadGeneration) return;
            _d3d11AnimationPacer=new(Path.Combine(inputs,"animation-cadence-input","fps-source.bin"));
            // The playable client targets 60 FPS. Apply the source manager's
            // 60 FPS mode explicitly; its constructor starts in 30 FPS mode.
            _d3d11AnimationPacer.ApplyLoadedOption31(D3D11AnimationPacer.SixtyFramesPerSecondMode);
            if (cgLiveAccepted is not null)
            {
                if (!IsCurrent()) return;
                cgLiveWaitStarted = true;
                // Preserve all logical fields in the ordinary capture. Record
                // runs the established PrepareScene/Copy path; no fake receipt.
                surface.Record(CaptureD3D11Input(_sourceMainCameraFrame ?? frame, 0,
                    sceneryCameraStable:openingSceneryCameraStable));
                await cgLiveAccepted.Task.WaitAsync(token);
                if (!IsCurrent()) return;
            }
            surface.MouseFilter = MouseFilterEnum.Stop;
            surface.GuiInput += input => Run(() => OnInput(input));
            surface.Resized += () => _surface.Size = surface.Size;
            _surface.Size = surface.Size;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || !IsCurrent()) { }
        catch (Exception error)
        {
            // Release/reload can cancel an old opening. It has no claim on
            // the current map; genuine surface failures are published above.
            if (IsCurrent() && _d3d11Map == surface &&
                surface?.IsClosed != true && surface?.LastError is null)
                RecordD3D11MapFailure(error);
        }
        finally
        {
            try
            {
                if (!adopted) environment?.AtmosphereOwner.Dispose();
                cpu?.Dispose();
            }
            finally
            {
                if (!adopted) DisposeD3D11MapInputs(inputDirectory);
                if (ReferenceEquals(_d3d11Opening, opening)) _d3d11Opening = null;
                opening.Dispose();
            }
        }
    }

    private void RecordD3D11MapFailure(Exception error)
    {
        StopPeriodProgressFrames(error);
        FailMapObjectRetirementPresentation(error.Message);
        if (uniqueDirectionOwner is not null) uniqueDirectionFailure?.Invoke(error);
        _d3d11PresentedInput = null;
        tacticArmyPositions = null;
        PublishTacticPresentationViews();
        _sceneryMap?.Suspend();
        if (_d3d11Markers is not null) _d3d11Markers.Visible = false;
        ResetSourceCameraInput();
        _dragging = false; _panning = false;
        ClearPickedCell();
        RecordNativeMapUnavailable(error);
        if (_sourceMainCameraNotice is not null)
        {
            _sourceMainCameraNotice.Text = _text.Get("scene_map_failed") + "\n" + error.Message;
            _sourceMainCameraNotice.Visible = true;
        }
    }

    private D3D11MapInput CaptureD3D11Input(San14.Data.MainMapCameraFrame frame, double delta,
        bool initialManagerUpdate=false, bool? sceneryCameraStable=null, bool createBaseOnly=false)
    {
        if(initialManagerUpdate&&!_periodMapActive)_d3d11Army?.AdvanceMotion(_logicalWorld!,0);
        var state = frame.ControllerState;
        var cityGateWorld=_periodMapFrame is {} periodCities ? periodCities.Cities :
            CaptureCityGateFacilityWorld(_logicalWorld!,CaptureCurrentCityGate());
        var input = new D3D11MapInput(frame.Camera.Prefix.Span,
            [state.FocusX, state.FocusY, state.FocusZ, state.FocusW, state.Distance90, state.YawA8, state.PitchC0],
            frame.ZoomSelection.Band1E0, frame.ZoomSelection.Fraction1E4, delta,
            paused:_periodMapActive&&_periodMapPaused, eligibleAnimationUpdate:initialManagerUpdate, sourceFrame: frame)
        {
            MainModeOverride = _sourceMainMode?.CurrentOverride28C ?? -1,
            TerrainPicker = CurrentTerrainPicker,
            StoryEffectCamera = storyEffectCommonCamera?.Capture(),
            TerritoryOwnership = chronologyTerritory ?? GeographicTerritory(_logicalWorld!) ?? (_occupationTerritory ?? (_periodMapFrame is {} periodTerritory ? periodTerritory.Territory : _d3d11Ownership?.Capture(_logicalWorld!))),
            // Session.Create explicitly rejects these live-only fields. They
            // remain owned by their logical publishers and are captured by Next.
            CommandMap = createBaseOnly ? null : CaptureMapDisplay(),
            HexFocus = createBaseOnly ? null : CapturePrimaryHexFocus(),
            Routes = createBaseOnly ? null : CaptureDeploymentRoutes(frame),
            InformationTerritoryStyle = InformationTerritoryStyle,
            InformationLayers = CaptureInformationLayers(_logicalWorld!),
            CommandMapFpsMode = _d3d11AnimationPacer?.FpsMode ?? 0,
            ArmyPublicationRole = IsNewGameScenarioPreview ? D3D11ArmyPublicationRole.StartupAbsent :
                D3D11ArmyPublicationRole.Campaign,
            ArmyWorld = _periodMapFrame is {} periodArmy ? periodArmy.Army : _d3d11Army?.Capture(_logicalWorld!),
            ArmyGroupingWorld = _periodMapFrame is {} periodGroups ? periodGroups.Groups : _d3d11ArmyGroupingQueries?.Invoke(_logicalWorld!),
            CityGateWorld = cityGateWorld,
            FacilityInfo = _periodMapFrame is {} periodFacilities ? periodFacilities.FacilityInfo :
                CaptureNormalFacilityInfo(_logicalWorld!,cityGateWorld?.Facilities),
            HexFire = _periodMapFrame is { } periodFire ? periodFire.HexFire :
                CaptureNormalHexFirePresentation(_logicalWorld!),
            AreaCapitalWorld = _periodMapFrame is {} periodAreas ? periodAreas.Areas : _d3d11AreaCapital?.Capture(_logicalWorld!, _d3d11CommonFrustumMode),
            PeriodSeason = _periodMapFrame?.Season,
            PeriodArmyLabels = _periodMapFrame?.Labels,
            WorldState = _logicalWorld,
            SceneryPresentation=_mapPresentation?.Capture(),
            SceneryCameraStable=sceneryCameraStable ??
                ActiveSourceCameraStable,
            TerritoryDisplayEnabled = _d3d11TerritoryDisplay
        };
        // Animation callbacks retain their own World even when draw requests
        // merge. Prepare each captured identity before either consumer queues it.
        var citySource=input.CityGateWorld?.Source;
        var areaSource=input.AreaCapitalWorld?.Source;
        if(!ReferenceEquals(citySource,_preparedCityGateFlagSource)||
            !ReferenceEquals(areaSource,_preparedAreaCapitalFlagSource))
        {
            var flagNames = (citySource?.Accessories
                .Where(unit => unit.Material is not null).Select(unit => unit.PaneName!)
                ?? Enumerable.Empty<ArmyFlagPaneNameSelection>())
                .Concat(areaSource?.Source.Owners
                    .Where(owner => owner.CurrentPaneName is not null).Select(owner => owner.CurrentPaneName!)
                    ?? Enumerable.Empty<ArmyFlagPaneNameSelection>());
            _d3d11FlagMaterials!.Prepare(flagNames);
            _d3d11FlagMaterials.WriteNativeInputs(_d3d11FlagMaterialDirectory!);
            _preparedCityGateFlagSource=citySource;_preparedAreaCapitalFlagSource=areaSource;
        }
        var armyRequests=input.ArmyWorld?.FlagMaterialRequests;
        if(!ReferenceEquals(armyRequests,_preparedArmyFlagRequests))
        {
            _d3d11ArmyMaterials!.Prepare(armyRequests??[]);
            _d3d11ArmyMaterials.WriteNativeInputs(_d3d11ArmyMaterialDirectory!);
            _preparedArmyFlagRequests=armyRequests;
        }
        // Both queues retain the resource snapshot belonging to this capture.
        // Preparing a later drawing request may publish another generation.
        _d3d11ResourceContext!.WriteNativeInputs(_d3d11ResourceContextDirectory!);
        return input.WithResourceContextGeneration(_d3d11ResourceContext.Revision);
    }
    public void SetTerritoryDisplay(bool enabled)
    {
        _d3d11TerritoryDisplay = enabled;
        ScheduleD3D11MapFrame();
    }
    private void ScheduleD3D11MapFrame()
    {
        if (_nativeMapClosing || _adoptingStartedCampaign || _d3d11Map is not { IsClosed: false, LastError: null } surface || _sourceMainCameraFrame is not { } frame) return;
        if(!_periodMapActive&&_d3d11AnimationDue&&_d3d11AnimationPacer is { } motionPacer)
            _d3d11Army?.AdvanceMotion(_logicalWorld!,motionPacer.FpsMode);
        else if(_periodMapActive&&!_periodMapPaused&&_periodMapFrame is {} period&&
            _d3d11AnimationDue&&_d3d11AnimationPacer is {} periodPacer)
            _periodMapFrame=period with{Army=_d3d11Army?.AdvanceCapturedMotion(periodPacer.FpsMode)};
        var input=CaptureD3D11Input(frame,_d3d11Elapsed);
        if(_d3d11AnimationDue&&_d3d11AnimationPacer is { } pacer)surface.RecordAnimationUpdate(input,pacer.FpsMode);
        _d3d11AnimationDue=false;
        surface.Record(input); _d3d11Elapsed = 0;
    }
    private void ReleaseD3D11Map(bool preservePeriodPresentation = false)
    {
        if (!preservePeriodPresentation)
            StopPeriodProgressFrames(new OperationCanceledException("Period source-frame owner retired."));
        var sourcePreparation = _sceneSourcePreparation; _sceneSourcePreparation = null;
        sourcePreparation?.Cancel();
        var opening = _d3d11Opening; _d3d11Opening = null;
        opening?.Cancel();
        RetireFirePresentation();
        RetireDamagePresentation();
        RetireHexFirePresentation();
        ResetHexFirePublicationCapture();
        RetireFacilityEffectsPresentation();
        RetireFacilityRepairPresentation();
        // The retiring map retains its tickets until its render owner closes.
        // Detach UI callbacks; shared particles are disposed below after Native.
        _administrationEffects = null;
        RetireMapObjectRetirementPresentation();
        tacticArmyPositions = null;
        TacticPresentationViews.Clear();
        _traitRefreshWorld = null; _traitRefreshArmy = null; _traitPersonalities = null;
        EndCampaignTargetMapPick();
        ReleaseInformationLayers();
        _periodMapFrame=null;_periodBorrowedCandidate=null;
        if (!preservePeriodPresentation)
        {
            _periodMapActive=false;_periodMapOwner=null;
            _periodOriginalArmy?.Dispose();_periodOriginalArmy=null;
            ReleaseFacilitySceneConnections();
        }
        // Keep period-owned logical paths, never their retired geometry or
        // resource directory. The next capture rebuilds against the new graph.
        var periodRoutePaths = preservePeriodPresentation ? _deploymentRoutePaths : null;
        ReleaseDeploymentRoutes();
        if (periodRoutePaths is not null) _deploymentRoutePaths = periodRoutePaths;
        if (_d3d11Markers is not null) { _d3d11Markers.QueueFree(); _d3d11Markers = null; }
        _scenarioPreviewMarkerSources=null;
        _d3d11Ownership = null;
        if (!preservePeriodPresentation) { _d3d11Army?.Dispose(); _d3d11Army = null; }
        _d3d11ArmySources = null;
        _d3d11CityGate = null;
        _d3d11Facilities=null;
        _d3d11AreaCapital = null;
        _d3d11FlagMaterials = null; _d3d11FlagMaterialDirectory = null;
        _d3d11ArmyMaterials = null; _d3d11ArmyMaterialDirectory = null;
        _preparedCityGateFlagSource=null;_preparedAreaCapitalFlagSource=null;_preparedArmyFlagRequests=null;
        var retiringResourceContext = _d3d11ResourceContext; _d3d11ResourceContext = null;
        _d3d11ResourceContextDirectory = null;
        _d3d11AnimationPacer=null;_d3d11AnimationDue=false;
        var surface = _d3d11Map; _d3d11Map = null; _d3d11PresentedInput = null; _d3d11Elapsed = 0;
        var inputDirectory = _d3d11InputDirectory; _d3d11InputDirectory = null;
        var skyResources = _d3d11SkyResources; _d3d11SkyResources = null;
        var effects = _captureEffects; _captureEffects = null;
        effects?.CancelPending();
        _d3d11Retirement = RetireWithEffects();
        async Task RetireWithEffects()
        {
            bool nativeClosed = surface is null;
            try
            {
                try { await RetireUniqueTacticEffectsAsync(); }
                finally
                {
                    // A failed per-owner retirement cannot leave the retained
                    // native Map alive while its shared resources are disposed.
                    if (surface is not null)
                    {
                        await RetireD3D11MapAsync(surface, skyResources);
                        nativeClosed = true;
                    }
                    else skyResources?.Dispose();
                }
            }
            finally
            {
                try { effects?.Dispose(); retiringResourceContext?.Dispose(); }
                finally
                {
                    if (nativeClosed) DisposeD3D11MapInputs(inputDirectory);
                    else if (inputDirectory is not null)
                        GD.PushWarning($"Generated map inputs retained because native close failed: {inputDirectory.Root}");
                }
            }
        }
    }

    private static void DisposeD3D11MapInputs(D3D11MapInputDirectory? directory)
    {
        try { directory?.Dispose(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { GD.PushWarning($"Generated map input cleanup failed for {directory!.Root}: {error.Message}"); }
    }

    private void InitializeD3D11MapLocations()
    {
        // Startup's real Foothold overlay consumes CityFlag source layouts.
        // Ordinary strategy ink has no preview consumer and is created on
        // campaign adoption, while this same map/native owner is retained.
        if(IsNewGameScenarioPreview)
        {
            _scenarioPreviewMarkerSources??=CityGateMapMarkerSources.Load(_repository);
            _locationSelection.Clear();
            _locationSelection.Disabled=_focusLocation.Disabled=_showLocations.Disabled=true;
        }
        else InitializeD3D11CampaignMapLocations();
        var scenario = _sourceSnapshot ?? throw new InvalidDataException("Native playable map requires its actual scenario snapshot.");
        _d3d11Ownership = new(scenario, _repository.LoadCeMap(),
            _repository.LoadTerrainTypes(scenario.FixedDataPath), _logicalWorld!,
            ForcePalette ?? throw new InvalidDataException("Native playable map source palette is unavailable."));
        SetMeta("map_projection_available", true);
        SetMeta("map_projection_policy_id", _projection!.Policy.Id);
        SetMeta("playable_unit_marker_count", 0);
        SetMeta("playable_structure_marker_count", 0);
    }

    private void InitializeD3D11CampaignMapLocations()
    {
        var markers = new D3D11MapMarkers { Name = "NativeMapLocations", ZIndex = 1, Visible = _showLocations.ButtonPressed };
        _d3d11Markers = markers;
        if(_facilityMapInfoQuery is {} facilityQuery)markers.SetFacilityMapInfoQuery(facilityQuery);
        var parent = StrategicMapOverlayParent();
        parent.AddChild(markers); markers.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        markers.Configure(_repository, _logicalWorld!, _projection!, TerrainMeshes!.Source, ForceMarkerColors,
            _text.SourceLocale, _playerForceId, _sourceSnapshot!, sharedCitySources:_scenarioPreviewMarkerSources);
        ApplyKeyboardMapInformation();
        RefreshD3D11MapLocationSelection();
        SetMeta("native_map_marker_backend", "canvas-source-main");
        InitializeInformationLayers();
    }

    private void RefreshD3D11MapLocationSelection()
    {
        int oldHex = _locationSelection.Selected >= 0 && _locationSelection.Selected < _locationSelection.ItemCount ?
            _locationSelection.GetItemId(_locationSelection.Selected) : -1;
        var locations = _d3d11Markers!.Locations;
        _locationSelection.Clear();
        foreach (var location in locations) _locationSelection.AddItem(location.Name, location.HexId);
        bool available = locations.Count > 0;
        _locationSelection.Disabled = !available; _focusLocation.Disabled = !available; _showLocations.Disabled = !available;
        int index = Enumerable.Range(0, locations.Count).FirstOrDefault(i => locations[i].HexId == oldHex, available ? 0 : -1);
        _locationSelection.Select(index);
        _locationNotice.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            _text.Get("scene_preview_projection_notice"), locations.Count, 0);
    }
    private static async Task RetireD3D11MapAsync(D3D11MapSurface surface, SkyAtmosphereResourceOwner? skyResources)
    {
        try { await surface.CloseAsync(); }
        finally { skyResources?.Dispose(); }
        if (GodotObject.IsInstanceValid(surface)) surface.QueueFree();
    }
}
