using Godot;
using San14.Data;
using San14.Core;

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
    private uint _d3d11CommonFrustumMode;
    private D3D11MapInput? _d3d11PresentedInput;
    private Task _d3d11Retirement = Task.CompletedTask;
    private double _d3d11Elapsed;
    private bool _d3d11TerritoryDisplay = true;
    private D3D11AnimationPacer? _d3d11AnimationPacer;
    private bool _d3d11AnimationDue;
    private bool UseD3D11Map => _embedded && RenderingServer.GetCurrentRenderingDriverName() == "d3d12"&&
        RenderingServer.GetRenderingDevice() is not null;
    private bool D3D11MapInteractive => _d3d11Map is { IsClosed: false, LastError: null, LastReceipt: not null };
    public void SetD3D11CurrentWorldQueries(Func<GameState, SourceWorldContext?> capture,
        Func<GameState, UnitState, ArmyGraphicsCurrentInputs?>? armyMotion=null,
        Func<GameState, ArmyMapGroupingFrame?>? armyGrouping=null)
    {
        _d3d11CurrentWorldQueries = capture;
        _d3d11ArmyMotionQueries = armyMotion;
        _d3d11ArmyGroupingQueries = armyGrouping;
    }

    private async Task InitializeD3D11MapAsync(uint generation)
    {
        if (_d3d11Map is not null || _sourceMainCameraFrame is not { } frame ||
            _sceneTerrain?.Source is not { } terrainSources) return;
        var scene = terrainSources.Scene;
        D3D11MapSurface? surface = null;
        try
        {
            // Logical locations are built before the source camera owner.
            // Army screen offsets need that loaded owner's E940 anchors.
            _d3d11Markers!.ConfigureArmyLabels(_repository, _sourceSnapshot!, _d3d11CurrentWorldQueries,
                _sourceMainCamera!.Parameters);
            string source = Path.GetFullPath(ProjectSettings.GlobalizePath(ProjectSettings.GetSetting(
                "san14/render/source_package",
                $"user://render-cache/{_text.SourceLocale}/{scene.Scene.Source.DecodedSha256}/input").AsString()));
            string libraryDefault = OS.HasFeature("editor")
                ? "res://../native/San14.Render.D3D11/bin/x64/Release/San14.Render.D3D11.dll"
                : Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!, "native", "windows-x64", "San14.Render.D3D11.dll");
            string librarySetting = System.Environment.GetEnvironmentVariable("SAN14_RENDER_NATIVE_LIBRARY")
                ?? ProjectSettings.GetSetting("san14/render/native_library", libraryDefault).AsString();
            string library = Path.GetFullPath(ProjectSettings.GlobalizePath(librarySetting));
            // These banks are produced from this scene and accepted camera.
            // Their metadata describes this output, not a required offline package.
            var sceneMedia = D3D11SceneMediaPreparation.Prepare(_repository, terrainSources, frame.Camera,
                source, D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
            var cloudPrograms=SkyCloudProgramResources.Load(_repository,sceneMedia.Renderer);
            var groundDetails = GroundDetailResources.Prepare(_repository, scene,
                frame.Camera.Lens.ReverseDepth, _text.SourceLocale);
            var fogPrograms=HeightFogProgramResources.Load(_repository);
            var fogAcceleration=FogAccelerationResources.Load(_repository);
            var skyPrograms=SkyMainProgramResources.Load(_repository,sceneMedia.Renderer);
            SkyMainResourceOwner? skyResources=null;
            SkyAtmosphereResourceOwner? atmosphereOwner=null;
            SkyAtmosphereResourceOwner.Replacement? pendingAtmosphere=null;
            string? pendingAtmosphereDirectory=null;
            D3D11SkyAtmosphereResources? atmosphereResources=null;
            SkyStarResources? starResources=null;
            D3D11AmbientRenderResources? ambientResources=null;
            D3D11CloudProgramMedia? cloudMedia=null;
            D3D11CloudRenderResources? cloudResources = null;
            ShadowReceiverPatternResources? receiverPatterns = null;
            string inputs = Path.GetDirectoryName(source)!;
            _d3d11Ownership!.WriteStaticInputs(Path.Combine(inputs, TerritoryStaticMapEncoding.NativeDirectory));
            D3D11ProjectShaderInputPreparation.WriteNativeInputs(source, _text.SourceLocale);
            D3D11TimingInputPreparation.Load(_repository, _text.SourceLocale)
                .WriteAnimationCadenceInputs(Path.Combine(inputs, "animation-cadence-input"));
            var seasonResources = D3D11CurrentSeasonResources.LoadSourceReadyCpu(_repository,
                _logicalWorld!.NativeMapSeason ?? throw new InvalidDataException("Playable Main has no owned third-date season."), frame);
            string seasonalInputs = Path.Combine(Path.GetDirectoryName(source)!, "current-season-resource-input");
            var seasonLibrary = new D3D11CurrentSeasonLibrary(_repository, seasonResources, frame);
            seasonLibrary.WriteNativeLutInputs(seasonalInputs);
            new D3D11TerrainSeasonPalette(sceneMedia.Terrain.Parameters).WriteNativeInputs(seasonalInputs);
            var season = new D3D11CurrentSeasonPublisher(_repository, seasonResources,
                actualMainMode:_sourceMainMode,resourceLibrary: seasonLibrary);
            var writtenSeasonResources = seasonResources;
            var contextSeason = seasonResources.Source.CameraSource.Season.Resources.Action.Objects;
            var resourceContext = RenderResourceContext.LoadCurrentMap(_repository, scene,
                seasonResources.Source.CameraSource.Season.Resources.Action, out var resourceSeasonBatch);
            RenderResourceContext.Batch? pendingResourceSeasonBatch = null;
            LoadedObjects? pendingContextSeason = null;
            _d3d11ResourceContext = resourceContext;
            var captureEffects = new D3D11CaptureEffects(_repository, resourceContext, generation);
            _captureEffects = captureEffects;
            G1EffectMapInputs? effectInputs = null;
            D3D11CurrentSeasonResources? effectInputResources = null;
            string resourceContextDirectory = Path.Combine(inputs, "resource-context-input");
            _d3d11ResourceContextDirectory = resourceContextDirectory;
            resourceContext.WriteNativeInputs(resourceContextDirectory);
            int writtenSeasonCount = seasonLibrary.LoadedInstallations.Count;
            var hdrResources = HdrShaderResources.Load(_repository);
            var hdr = D3D11CurrentHdrCatalog.Create(_repository, frame, hdrResources);
            var scenarioMetadata = _repository.EffectiveScenarios.Single(s =>
                GameResourceId.File(s.SourcePath).ToString() == _logicalWorld!.ScenarioKey && s.Locale == _text.SourceLocale);
            var initialModelCommon = MapCommonRenderState.FromSettings(
                CommonRenderResources.ImportSettings(_repository.LoadObjects("KIDSSystemResource",CommonRenderResources.SourceFileId)),
                frame.Camera.FrameNumber,"map-model/settings","map-model/common","map-model/view","initial-resource-preparation");
            var initialContext = MainMapContextOwnerSource.LoadFreshHostDefaults(_text.SourceLocale);
            var primaryTemplates = D3D11CityGateTemplateBank.Prepare(_repository,scene,frame.Camera,
                initialModelCommon,initialContext.ContextFlags3E4,
                MapSourceCreatedWorldKind1Publication.ReadSeasonSourceRecords(seasonResources.Source.DirectionalDeclarations),
                sceneMedia.FieldModels.ShaderTableOwners,Path.Combine(inputs,"base-visual-world-input","primary-templates"));
            primaryTemplates.WriteNativeInputs();
            var cityAccessories = CityGateAccessoryResources.Load(_repository,scene);
            cityAccessories.WriteNativeInputs(Path.Combine(inputs,"city-accessory-input"));
            var accessoryGeometry = CityGateAccessoryGeometryResources.Prepare(_repository,cityAccessories,
                frame.Camera,initialModelCommon,initialContext.ContextFlags3E4,
                MapSourceCreatedWorldKind1Publication.ReadSeasonSourceRecords(seasonResources.Source.DirectionalDeclarations),
                _text.SourceLocale,Path.Combine(inputs,"city-accessory-input"));
            accessoryGeometry.WriteNativeInputs();
            var armyResources = ArmyGeometryResources.Prepare(_repository,frame.Camera,initialModelCommon,
                initialContext.ContextFlags3E4,
                MapSourceCreatedWorldKind1Publication.ReadSeasonSourceRecords(seasonResources.Source.DirectionalDeclarations),
                accessoryGeometry,_text.SourceLocale,Path.Combine(inputs,"army-visual-input"));
            armyResources.WriteNativeInputs();
            armyResources.WriteActionInputs(Path.Combine(inputs,"army-graphics-action-input"));
            _d3d11Army = new(_repository,_repository.LoadScenario(scenarioMetadata),_repository.LoadCeMap(),
                _projection!,TerrainMeshes!.Source.HeightField,_text.SourceLocale,_d3d11ArmyMotionQueries,
                armyResources.GraphicsActions);
            _d3d11ArmyMaterials = ArmyMaterialResources.Load(_repository,_text.SourceLocale,armyResources);
            _d3d11ArmyMaterialDirectory = Path.Combine(inputs,"army-visual-input","material-source");
            ArmyMaterialManagerResources.Load(_repository, armyResources.Catalog,
                resourceContext.MaterialInitialization!).WriteNativeInputs(_d3d11ArmyMaterialDirectory);
            _d3d11FlagMaterials = CityGateFlagMaterialResources.Load(_repository,_text.SourceLocale,accessoryGeometry);
            _d3d11FlagMaterialDirectory = Path.Combine(inputs,"city-accessory-input","material-source");
            var cityGatePublisher = new D3D11CityGatePublisher(_repository, scene, _repository.LoadScenario(scenarioMetadata), _repository.LoadCeMap(),
                _logicalWorld!.NewGameOptions!, _d3d11CurrentWorldQueries ??
                    throw new InvalidDataException("Native City/Gate visuals require the current campaign query owner."),
                primaryTemplates,cityAccessories);
            _d3d11CityGate = cityGatePublisher;
            _d3d11AreaCapital = new(_repository, scene, _repository.LoadScenario(scenarioMetadata),
                cityGatePublisher.PreparedBank, _d3d11CurrentWorldQueries);
            _d3d11CommonFrustumMode = MapModelNativeVisibilitySources.ReadCommonViewTemplate(
                _repository.LoadObjects("KIDSSystemResource", 0x6D011726), 0x899F101B).Value;
            long preparationNumber = 0;
            bool nativeRuleInputsPrepared = false;
            surface = new D3D11MapSurface { Name = "OriginalD3D11WorldMap" };
            _d3d11Map = surface;
            var parent = _sourceMainCameraAspect as Control ?? _surface.GetParent() as Control ?? this;
            parent.AddChild(surface); surface.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
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
                if (input.CurrentSeason is { } current) season.AcceptCurrent(current);
                if (input.CityGateWorld is { RequiredFlagMaterials.Count: > 0 } cityGate)
                    cityGatePublisher.PublishCompletedFlagMaterials(cityGate.RequiredFlagMaterials);
                if (input.AreaCapitalWorld is { } areaCapital)
                    cityGatePublisher.PublishCompletedFlagMaterials(areaCapital.FlagMaterialKeys);
                if (input.SourceFrame is { } camera)
                    _d3d11Markers?.Present(camera, input.AreaCapitalWorld, input.CityGateWorld);
                _d3d11Markers?.PresentArmy(input, receipt);
                PresentInformationLayers(input);
                if(input.SceneryWorld is {} scenery)_sceneryMap?.Present(scenery,false);
                SetMeta("native_map_army_label_unit_count", receipt.ArmyUnitPositions.Count);
            };
            surface.Accepted += (input, receipt) =>
            {
                if (_d3d11Map != surface || generation != _sceneLoadGeneration) return;
                _d3d11PresentedInput = input;
                captureEffects.Accept(input.Effects);
                _d3d11Markers?.AcceptArmyHits();
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
            };
            surface.Failed += error =>
            {
                pendingResourceSeasonBatch?.Dispose();
                pendingResourceSeasonBatch = null; pendingContextSeason = null;
                pendingAtmosphere?.Dispose();
                pendingAtmosphere = null; pendingAtmosphereDirectory = null;
                if (_d3d11Map == surface && generation == _sceneLoadGeneration)
                    RecordD3D11MapFailure(error);
            };
            await surface.ConfigureAsync(source, library, CaptureD3D11Input(frame, 0,initialManagerUpdate:true), populationMask: 3,
                prepareCurrent: input =>
                {
                    var geometry = input.SourceFrame!;
                    var currentMain = geometry with { Camera = geometry.Camera.CopyAcceptedSnapshotForOwnedFrame(
                        ++preparationNumber, geometry.Camera.FrameProvenance) };
                    var current = input.PeriodSeason is { } periodSeason
                        ? season.PrepareCurrent(periodSeason, currentMain, (float)input.ElapsedSeconds)
                        : season.PrepareCurrent(input.WorldState!, currentMain, (float)input.ElapsedSeconds);
                    string? atmosphereDirectory = null;
                    if (!nativeRuleInputsPrepared)
                    {
                        groundDetails.WriteNativeInputs(inputs, current.Rgb.Inputs.MainPostSelection!.AppFlags);
                        var globalFog=current.Lights.First(light=>(light.GroupMask&0x80)!=0).State;
                        var fogRgb=globalFog.Lighting.Directional[0].Color;
                        var initialFog=fogAcceleration.PrepareInitialFrame(globalFog.Fog,
                            new System.Numerics.Vector3(fogRgb.X,fogRgb.Y,fogRgb.Z),current.Present.CapturePreparedCamera());
                        fogAcceleration.WriteNativeInputs(initialFog,Path.Combine(inputs,"fog-accel-input"));
                        cloudMedia=D3D11CloudProgramMedia.Prepare(cloudPrograms,current.Present,
                            current.Lights.First(light=>(light.GroupMask&0x80)!=0).State);
                        cloudMedia.WriteNativeInputs(inputs);
                        D3D11CloudPatternProgramMedia.Prepare(cloudMedia.Resources).WriteNativeInputs(inputs);
                        ambientResources = D3D11AmbientRenderResources.Load(_repository,current.Present,season.LoadedResources.Source.Locale);
                        ambientResources.WriteNativeInputs(inputs);
                        var images = SkyAppliedCloudTextureSet.Import(current.Present.AcceptedSeasonCamera.Inputs.Source.CloudTextures);
                        atmosphereResources = D3D11SkyAtmosphereResources.Load(
                            current.Present.AcceptedSeasonCamera.Inputs.Source, images);
                        atmosphereResources.WriteNativeInputs(inputs);
                        D3D11PatternRenderResources.Load(_repository,ambientResources.Declaration,season.LoadedResources.Source.Locale)
                            .WriteNativeInputs(inputs);
                        skyResources = SkyMainResourceOwner.CreateFresh(_repository, current.Present, skyPrograms, images);
                        atmosphereOwner = new(atmosphereResources, images, skyResources, null);
                        _d3d11SkyResources = atmosphereOwner;
                        cloudResources = D3D11CloudRenderResources.Prepare(_repository, current.Present,
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight, images,
                            skyResources.Sampling, cloudMedia);
                        cloudResources.WriteNativeInputs(inputs);
                        var starPackage = current.Present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage;
                        starResources = SkyStarResources.Load(_repository, sceneMedia.Renderer, starPackage);
                        string starDirectory = Path.Combine(inputs, "main-sky-input", "stars");
                        Directory.CreateDirectory(starDirectory);
                        File.WriteAllBytes(Path.Combine(starDirectory, "program-presence.bin"),
                            BitConverter.GetBytes(starResources is not null ? 1u : 0u));
                        if (starResources is not null)
                            D3D11MainStarInputPreparation.Load(starResources).WriteNativeInputs(starDirectory);
                        SkyStarCurrentEncoding.WriteNativeInputs(starDirectory, current.Stars,
                            current.Atmosphere.Controller.MatrixF0, current.Atmosphere.Controller.Controller.Scalar38);
                        var cameraInputs = _sourceMainCameraInputs!;
                        var post = current.Rgb.Inputs.MainPostSelection!;
                        var fieldModels = sceneMedia.FieldModels;
                        D3D11OwnCameraInputPreparation.WriteFreshNativeInputs(_repository, scene,
                            new(currentMain, cameraInputs.CurrentMaximumYOverride,
                                cameraInputs.NativeClipProfile, cameraInputs.TangentDispatch),
                            new(post.QualityOption18, post.MainMode288, post.AppFlags,
                                post.PriorCloudy430Bits, post.SelectedCloudy430Bits),
                            _sourceMainCameraTerrain!.CurrentField, inputs, fieldModels, sceneMedia.Terrain);
                        var initialMainNumeric = D3D11MainCameraInitialNumericInputs.Create(_repository, currentMain);
                        initialMainNumeric.WriteNativeInputs(inputs);
                        ShadowReceiverResources.Load(_repository).WriteNativeInputs(inputs);
                        ShadowReceiverSamplerEncoding.WriteNativeInputs(_repository, _text.SourceLocale, inputs);
                        receiverPatterns = ShadowReceiverPatternResources.Load(_repository, starPackage, _text.SourceLocale,
                            season.LoadedResources.PatternTextures);
                        receiverPatterns.WriteNativeInputs(inputs);
                        D3D11ShadowInputs.WriteNativeInputs(_repository, fieldModels, source,
                            cityGatePublisher.PreparedBank, season.LoadedResources, current);
                        D3D11OwnedNumericInputPreparation.WriteFreshNativeInputs(_repository, scene,
                            currentMain, current, season.LoadedResources, seasonLibrary,
                            fieldModels,initialContext.ContextFlags3E4,inputs,
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        var sceneColor=SceneColorTargetResources.PrepareFresh(_repository,
                            current.Present.CapturePreparedCamera(),season.LoadedResources.InitialCommonSettings,
                            D3D11MapSurface.OutputWidth,D3D11MapSurface.OutputHeight);
                        ScreenMapResources.PrepareNormal(_repository,
                            current.Present.CapturePreparedCamera(),_text.SourceLocale).WriteNativeInputs(inputs);
                        ContactOcclusionResources.Load(_repository,_text.SourceLocale).WriteNativeInputs(inputs);
                        D3D11HdrResourceBindings.WriteProducerTargets(_repository,
                            Path.Combine(inputs, "hdr-bank"),
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        D3D11RefractionCopyInputPreparation.WriteNativeInputs(_repository, current,sceneColor,
                            Path.Combine(inputs, "refraction-source-input"),
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        D3D11RefractionFilterInputPreparation.WriteNativeInputs(_repository, current,sceneColor,
                            Path.Combine(inputs, "refraction-source-input", "filter137"),
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        D3D11HdrProgramMediaPreparation.WriteNativeInputs(_repository,
                            current, season.LoadedResources, hdr, sceneColor, inputs,
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        var atmosphere = AtmosphereInitialConstantsWriter.WriteFreshNativeInputs(_repository, current,
                            sceneColor,
                            cloudElapsedSeconds: 0, commonClock: 0,
                            Path.Combine(inputs, "atmosphere-camera-continuous-input", "initial"),
                            Path.Combine(inputs, "main-sky-input"), Path.Combine(inputs, "main-cloud-input"),
                            Path.Combine(inputs, "ssfog-population-input"),cloudMedia,cloudResources,
                            skyPrograms,skyResources.Sampling,fogPrograms);
                        if (atmosphere.Sky is { } sky)
                            skyResources.Prepare(sky.Program).WriteNativeInputs(Path.Combine(inputs, "main-sky-input"));
                        if (current.Present.AcceptedSeasonCamera.Inputs.Source.RenderingAtmosphere is not null)
                        {
                            var workSky = atmosphereOwner.PrepareWorkSky(_repository, current.Present, skyPrograms);
                            var work = D3D11AmbientWorkInitialConstants.Prepare(_repository,
                                current, cloudMedia, ambientResources.Declaration,
                                skyPrograms, workSky.Sampling);
                            workSky.Prepare(work.SkyMedia.Program);
                            D3D11AmbientWorkInputPreparation.WriteNativeInputs(work, inputs);
                        }
                        D3D11NormalPostInitialConstantPreparation.WriteNativeInputs(_repository,
                            current, inputs, D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight,sceneColor);
                        D3D11PrimaryForwardInputs.WriteFreshNativeInputs(_repository,
                            cityGatePublisher.PreparedBank, season.LoadedResources, current, inputs,
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        nativeRuleInputsPrepared = true;
                    }
                    else
                    {
                        ambientResources!.RequireCurrent(current.Present);
                        cloudMedia!.RequireCurrent(current.Present,
                            current.Lights.First(light=>(light.GroupMask&0x80)!=0).State);
                        cloudResources!.RequireCurrentSurfaces(current.Present,
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        fogPrograms.RequireCurrentMask(current.Present);
                        var replacement = atmosphereOwner!.PrepareReplacement(_repository, skyPrograms, current.Present);
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
                                    var work = D3D11AmbientWorkInitialConstants.Prepare(_repository,
                                        current, cloudMedia, ambientResources.Declaration,
                                        skyPrograms, replacement.WorkSky!.Sampling);
                                    replacement.WorkSky.RequireCurrent(work.SkyMedia.Program);
                                }
                            }
                            starResources?.RequireCompatiblePackage(_repository,
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
                            receiverPatterns!.RequireCompatible(_repository,
                                current.Present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage,
                                season.LoadedResources.PatternTextures);
                            int loadedCount = seasonLibrary.LoadedInstallations.Count;
                            if (loadedCount != writtenSeasonCount)
                            {
                                seasonLibrary.WriteNativeLutInputs(seasonalInputs);
                                D3D11OwnedNumericInputPreparation.WritePostCallers(_repository,
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
                        var scenery=input.PeriodSeason is not null ? null : _sceneryMap?.Capture(input.WorldState!,current.Mode,
                            input.SceneryCameraStable??throw new InvalidDataException("Captured camera transition state is unavailable."),
                            new(D3D11MapSurface.OutputWidth,D3D11MapSurface.OutputHeight),
                            input.SceneryPresentation??throw new InvalidDataException("Captured map presentation is unavailable."));
                        var preparedInput = input.WithCurrentSeason(current, hdr, atmosphereDirectory, resourceContext.Revision)
                            .WithSceneryWorld(scenery).WithRoutes(PrepareRouteResources(input.Routes));
                        if (captureEffects.HasWork && _d3d11AnimationPacer is { } effectPacer)
                        {
                            if (!ReferenceEquals(effectInputResources, season.LoadedResources))
                            {
                                effectInputs = new(_repository, season.LoadedResources);
                                effectInputResources = season.LoadedResources;
                            }
                            //The current native View has no primary-light
                            //publisher. Its normal Common light selector still
                            //selects the actual registered subset in the helper.
                            var effectFrame = effectInputs!.Prepare(current, D3D11MapSurface.OutputWidth,
                                D3D11MapSurface.OutputHeight, effectInputs.OwnedRendererSelection(current),
                                pendingAtmosphere?.MainSky ?? atmosphereOwner?.MainSky);
                            preparedInput = preparedInput.WithEffects(captureEffects.PrepareFrame(effectFrame,
                                input.ElapsedSeconds, effectPacer.FramesPerSecond(input.CommandMapFpsMode), surface.EffectComputeDevice));
                        }
                        return preparedInput;
                    }
                    catch
                    {
                        pendingResourceSeasonBatch?.Dispose();
                        pendingResourceSeasonBatch = null; pendingContextSeason = null;
                        pendingAtmosphere?.Dispose();
                        pendingAtmosphere = null; pendingAtmosphereDirectory = null;
                        throw;
                    }
                });
            if (_d3d11Map != surface || generation != _sceneLoadGeneration) return;
            _d3d11AnimationPacer=new(Path.Combine(inputs,"animation-cadence-input","fps-source.bin"));
            surface.MouseFilter = MouseFilterEnum.Stop;
            surface.GuiInput += input => Run(() => OnInput(input));
            surface.Resized += () => _surface.Size = surface.Size;
            _surface.Size = surface.Size;
        }
        catch (Exception error)
        {
            // Release/reload can cancel an old opening. It has no claim on
            // the current map; genuine surface failures are published above.
            if (generation == _sceneLoadGeneration && _d3d11Map == surface &&
                surface?.IsClosed != true && surface?.LastError is null)
                RecordD3D11MapFailure(error);
        }
    }

    private void RecordD3D11MapFailure(Exception error)
    {
        _d3d11PresentedInput = null;
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

    private D3D11MapInput CaptureD3D11Input(San14.Data.MainMapCameraFrame frame, double delta,bool initialManagerUpdate=false)
    {
        if(initialManagerUpdate&&!_periodMapActive)_d3d11Army?.AdvanceMotion(_logicalWorld!,0);
        var state = frame.ControllerState;
        var input = new D3D11MapInput(frame.Camera.Prefix.Span,
            [state.FocusX, state.FocusY, state.FocusZ, state.FocusW, state.Distance90, state.YawA8, state.PitchC0],
            frame.ZoomSelection.Band1E0, frame.ZoomSelection.Fraction1E4, delta,
            paused:_periodMapActive&&_periodMapPaused, eligibleAnimationUpdate:initialManagerUpdate, sourceFrame: frame)
        {
            TerrainPicker = CurrentTerrainPicker,
            TerritoryOwnership = _periodMapFrame is {} periodTerritory ? periodTerritory.Territory : _d3d11Ownership?.Capture(_logicalWorld!),
            CommandMap = _commandMapDisplay,
            Routes = CaptureDeploymentRoutes(frame),
            InformationTerritoryStyle = InformationTerritoryStyle,
            InformationLayers = CaptureInformationLayers(_logicalWorld!),
            CommandMapFpsMode = _d3d11AnimationPacer?.FpsMode ?? 0,
            ArmyWorld = _periodMapFrame is {} periodArmy ? periodArmy.Army : _d3d11Army?.Capture(_logicalWorld!),
            ArmyGroupingWorld = _periodMapFrame is {} periodGroups ? periodGroups.Groups : _d3d11ArmyGroupingQueries?.Invoke(_logicalWorld!),
            CityGateWorld = _periodMapFrame is {} periodCities ? periodCities.Cities : _d3d11CityGate?.Capture(_logicalWorld!, _d3d11CommonFrustumMode),
            AreaCapitalWorld = _periodMapFrame is {} periodAreas ? periodAreas.Areas : _d3d11AreaCapital?.Capture(_logicalWorld!, _d3d11CommonFrustumMode),
            PeriodSeason = _periodMapFrame?.Season,
            PeriodArmyLabels = _periodMapFrame?.Labels,
            WorldState = _logicalWorld,
            SceneryPresentation=_mapPresentation?.Capture(),
            SceneryCameraStable=_sourceMainCamera is {} mainOwner?mainOwner.CurrentRuntime.Transition is null:null,
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
        if (_d3d11Map is not { IsClosed: false, LastError: null } surface || _sourceMainCameraFrame is not { } frame) return;
        if(!_periodMapActive&&_d3d11AnimationDue&&_d3d11AnimationPacer is { } motionPacer)
            _d3d11Army?.AdvanceMotion(_logicalWorld!,motionPacer.FpsMode);
        else if(_periodMapActive&&!_periodMapPaused&&_periodBorrowedCandidate is {} borrowed&&
            _periodMapFrame is {} period&&_d3d11AnimationDue&&_d3d11AnimationPacer is {} periodPacer)
            _periodMapFrame=period with{Army=_d3d11Army?.AdvanceMotion(borrowed,periodPacer.FpsMode)};
        var input=CaptureD3D11Input(frame,_d3d11Elapsed);
        if(_d3d11AnimationDue&&_d3d11AnimationPacer is { } pacer)surface.RecordAnimationUpdate(input,pacer.FpsMode);
        _d3d11AnimationDue=false;
        surface.Record(input); _d3d11Elapsed = 0;
    }
    private void ReleaseD3D11Map()
    {
        _traitRefreshWorld = null; _traitRefreshArmy = null;
        EndCampaignTargetMapPick();
        ReleaseInformationLayers();
        _periodMapActive=false;_periodMapFrame=null;_periodBorrowedCandidate=null;
        _periodMapOwner=null;
        _periodOriginalArmy?.Dispose();_periodOriginalArmy=null;
        ReleaseDeploymentRoutes();
        if (_d3d11Markers is not null) { _d3d11Markers.QueueFree(); _d3d11Markers = null; }
        _d3d11Ownership = null;
        _d3d11Army?.Dispose(); _d3d11Army = null;
        _d3d11CityGate = null;
        _d3d11AreaCapital = null;
        _d3d11FlagMaterials = null; _d3d11FlagMaterialDirectory = null;
        _d3d11ArmyMaterials = null; _d3d11ArmyMaterialDirectory = null;
        _preparedCityGateFlagSource=null;_preparedAreaCapitalFlagSource=null;_preparedArmyFlagRequests=null;
        var retiringResourceContext = _d3d11ResourceContext; _d3d11ResourceContext = null;
        _d3d11ResourceContextDirectory = null;
        _d3d11AnimationPacer=null;_d3d11AnimationDue=false;
        var surface = _d3d11Map; _d3d11Map = null; _d3d11PresentedInput = null; _d3d11Elapsed = 0;
        var skyResources = _d3d11SkyResources; _d3d11SkyResources = null;
        var effects = _captureEffects; _captureEffects = null;
        effects?.CancelPending();
        if (surface is null) { skyResources?.Dispose(); effects?.Dispose(); retiringResourceContext?.Dispose(); return; }
        _d3d11Retirement = RetireWithEffects();
        async Task RetireWithEffects()
        {
            try { await RetireD3D11MapAsync(surface, skyResources); }
            finally { effects?.Dispose(); retiringResourceContext?.Dispose(); }
        }
    }

    private void InitializeD3D11MapLocations()
    {
        var markers = new D3D11MapMarkers { Name = "NativeMapLocations", ZIndex = 1, Visible = _showLocations.ButtonPressed };
        _d3d11Markers = markers;
        var parent = _sourceMainCameraAspect as Control ?? _surface.GetParent() as Control ?? this;
        parent.AddChild(markers); markers.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        markers.Configure(_repository, _logicalWorld!, _projection!, TerrainMeshes!.Source, ForceMarkerColors,
            _text.SourceLocale, _playerForceId, _sourceSnapshot!);
        ApplyKeyboardMapInformation();
        var scenario = _sourceSnapshot ?? throw new InvalidDataException("Native playable map requires its actual scenario snapshot.");
        _d3d11Ownership = new(scenario, _repository.LoadCeMap(),
            _repository.LoadTerrainTypes(scenario.FixedDataPath), _logicalWorld!,
            ForcePalette ?? throw new InvalidDataException("Native playable map source palette is unavailable."));
        RefreshD3D11MapLocationSelection();
        SetMeta("map_projection_available", true);
        SetMeta("map_projection_policy_id", _projection!.Policy.Id);
        SetMeta("native_map_marker_backend", "canvas-source-main");
        SetMeta("playable_unit_marker_count", 0);
        SetMeta("playable_structure_marker_count", 0);
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
