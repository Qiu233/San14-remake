using Godot;
using San14.Data;
using San14.Core;

public partial class MapSceneView
{
    private D3D11MapSurface? _d3d11Map;
    private SkyMainResourceOwner? _d3d11SkyResources;
    private D3D11MapMarkers? _d3d11Markers;
    private D3D11TerritoryPublisher? _d3d11Ownership;
    private D3D11ArmyPublisher? _d3d11Army;
    private D3D11CityGatePublisher? _d3d11CityGate;
    private D3D11AreaCapitalPublisher? _d3d11AreaCapital;
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
    private bool UseD3D11Map => _embedded && RenderingServer.GetCurrentRenderingDriverName() == "d3d12";
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
        if (_d3d11Map is not null || _sourceMainCameraFrame is not { } frame || _loadedSceneResources is not { } scene) return;
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
            var sceneMedia = D3D11SceneMediaPreparation.Prepare(_repository, scene, frame.Camera,
                source, D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
            var cloudPrograms=SkyCloudProgramResources.Load(_repository,sceneMedia.Renderer);
            var fogPrograms=HeightFogProgramResources.Load(_repository);
            var skyPrograms=SkyMainProgramResources.Load(_repository,sceneMedia.Renderer);
            SkyMainResourceOwner? skyResources=null;
            D3D11SkyAtmosphereResources? atmosphereResources=null;
            SkyStarResources? starResources=null;
            D3D11AmbientRenderResources? ambientResources=null;
            D3D11CloudProgramMedia? cloudMedia=null;
            D3D11CloudRenderResources? cloudResources = null;
            ShadowReceiverPatternResources? receiverPatterns = null;
            string inputs = Path.GetDirectoryName(source)!;
            D3D11ProjectShaderInputPreparation.WriteNativeInputs(source, _text.SourceLocale);
            D3D11TimingInputPreparation.Load(_repository, _text.SourceLocale)
                .WriteAnimationCadenceInputs(Path.Combine(inputs, "animation-cadence-input"));
            var seasonResources = D3D11CurrentSeasonResources.LoadSourceReadyCpu(_repository,
                _logicalWorld!.NativeMapSeason ?? throw new InvalidDataException("Playable Main has no owned third-date season."), frame);
            string seasonalInputs = Path.Combine(Path.GetDirectoryName(source)!, "current-season-resource-input");
            var seasonLibrary = new D3D11CurrentSeasonLibrary(_repository, seasonResources, frame);
            seasonLibrary.WriteNativeLutInputs(seasonalInputs);
            new D3D11TerrainSeasonPalette(TerrainParameterResources.Read(scene)).WriteNativeInputs(seasonalInputs);
            var season = new D3D11CurrentSeasonPublisher(_repository, seasonResources, resourceLibrary: seasonLibrary);
            var writtenSeasonResources = seasonResources;
            int writtenSeasonCount = seasonLibrary.LoadedInstallations.Count;
            var hdrResources = HdrShaderResources.Load(_repository);
            var hdr = D3D11CurrentHdrCatalog.Create(_repository, frame, hdrResources);
            var scenarioMetadata = _repository.EffectiveScenarios.Single(s =>
                GameResourceId.File(s.SourcePath).ToString() == _logicalWorld!.ScenarioKey && s.Locale == _text.SourceLocale);
            // CPU clips/players only; retained category geometry and materials
            // are shared by all current graphics actions.
            ArmyGraphicsAnimationNativeInputs.Write(_repository,new ArmyGraphicsActions(_repository),
                Path.Combine(inputs,"army-graphics-action-input"));
            var cityGatePublisher = new D3D11CityGatePublisher(_repository, scene, _repository.LoadScenario(scenarioMetadata), _repository.LoadCeMap(),
                _logicalWorld!.NewGameOptions!, _d3d11CurrentWorldQueries ??
                    throw new InvalidDataException("Native City/Gate visuals require the current campaign query owner."),
                Path.Combine(inputs, "base-visual-world-input", "primary-templates"), Path.Combine(inputs, "city-accessory-input"));
            _d3d11CityGate = cityGatePublisher;
            _d3d11AreaCapital = new(_repository, scene, _repository.LoadScenario(scenarioMetadata),
                cityGatePublisher.PreparedBank, _d3d11CurrentWorldQueries);
            _d3d11CommonFrustumMode = MapModelNativeVisibilitySources.ReadCommonViewTemplate(
                _repository.LoadObjects("KIDSSystemResource", 0x6D011726), 0x899F101B).Value;
            D3D11CityGateFlagImports.WriteNativeKeys(Path.Combine(inputs, "city-accessory-next-input", "material-source"));
            long preparationNumber = 0;
            bool nativeRuleInputsPrepared = false;
            surface = new D3D11MapSurface { Name = "OriginalD3D11WorldMap" };
            _d3d11Map = surface;
            var parent = _sourceMainCameraAspect as Control ?? _surface.GetParent() as Control ?? this;
            parent.AddChild(surface); surface.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            surface.FramePrepared += (input, receipt) =>
            {
                if (_d3d11Map != surface || generation != _sceneLoadGeneration) return;
                if (input.CurrentSeason is { } current) season.AcceptCurrent(current);
                if (input.CityGateWorld is { RequiredFlagMaterials.Count: > 0 } cityGate)
                    cityGatePublisher.PublishCompletedFlagMaterials(cityGate.RequiredFlagMaterials);
                if (input.SourceFrame is { } camera)
                    _d3d11Markers?.Present(camera, input.AreaCapitalWorld, input.CityGateWorld);
                _d3d11Markers?.PresentArmy(input, receipt);
                SetMeta("native_map_army_label_unit_count", receipt.ArmyUnitPositions.Count);
            };
            surface.Accepted += (input, receipt) =>
            {
                if (_d3d11Map != surface || generation != _sceneLoadGeneration) return;
                _d3d11PresentedInput = input;
                _d3d11Markers?.AcceptArmyHits();
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
                if (_d3d11Map == surface && generation == _sceneLoadGeneration)
                    RecordD3D11MapFailure(error);
            };
            await surface.ConfigureAsync(source, library, CaptureD3D11Input(frame, 0,initialManagerUpdate:true), populationMask: 3,
                prepareCurrent: input =>
                {
                    var geometry = input.SourceFrame!;
                    var currentMain = geometry with { Camera = geometry.Camera.CopyAcceptedSnapshotForOwnedFrame(
                        ++preparationNumber, geometry.Camera.FrameProvenance) };
                    var current = season.PrepareCurrent(input.WorldState!, currentMain, (float)input.ElapsedSeconds);
                    if (!nativeRuleInputsPrepared)
                    {
                        cloudMedia=D3D11CloudProgramMedia.Prepare(cloudPrograms,current.Present,
                            current.Lights.First(light=>(light.GroupMask&0x80)!=0).CompleteData.Span);
                        cloudMedia.WriteNativeInputs(inputs);
                        D3D11CloudPatternProgramMedia.Prepare(cloudMedia.Resources).WriteNativeInputs(inputs);
                        var work = D3D11AmbientWorkInitialConstants.Prepare(_repository,
                            current, season.LoadedResources, cloudMedia, skyPrograms);
                        ambientResources = D3D11AmbientRenderResources.Load(_repository,work,season.LoadedResources.Source.Locale);
                        atmosphereResources = D3D11SkyAtmosphereResources.Load(
                            current.Present.AcceptedSeasonCamera.Inputs.Source.Transmittance.RequireTables(),
                            current.Present.AcceptedSeasonCamera.Inputs.Source.CloudTextures, work);
                        atmosphereResources.WriteNativeInputs(inputs);
                        D3D11PatternRenderResources.Load(_repository,work.Ambient,season.LoadedResources.Source.Locale)
                            .WriteNativeInputs(inputs);
                        cloudResources = D3D11CloudRenderResources.Prepare(_repository, current.Present,
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight, work, cloudMedia);
                        cloudResources.WriteNativeInputs(inputs);
                        skyResources = SkyMainResourceOwner.CreateFresh(_repository, current.Present, skyPrograms, work.CloudTextures);
                        _d3d11SkyResources = skyResources;
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
                            currentMain, current, season.LoadedResources, seasonLibrary, inputs,
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        D3D11HdrResourceBindings.WriteProducerTargets(_repository,
                            Path.Combine(inputs, "hdr-bank"),
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        D3D11RefractionCopyInputPreparation.WriteNativeInputs(_repository, current,
                            Path.Combine(inputs, "refraction-source-input"),
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        D3D11RefractionFilterInputPreparation.WriteNativeInputs(_repository, current,
                            Path.Combine(inputs, "refraction-source-input", "filter137"),
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        D3D11HdrProgramMediaPreparation.WriteNativeInputs(_repository,
                            current, season.LoadedResources, hdr, inputs,
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        var atmosphere = AtmosphereInitialConstantsWriter.WriteFreshNativeInputs(_repository, current,
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight,
                            cloudElapsedSeconds: 0, commonClock: 0,
                            Path.Combine(inputs, "atmosphere-camera-continuous-input", "initial"),
                            Path.Combine(inputs, "main-sky-input"), Path.Combine(inputs, "main-cloud-input"),
                            Path.Combine(inputs, "ssfog-population-input"),cloudMedia,cloudResources,
                            skyPrograms,skyResources.Sampling,fogPrograms);
                        skyResources.Prepare(atmosphere.SkyProgram).WriteNativeInputs(Path.Combine(inputs, "main-sky-input"));
                        D3D11AmbientWorkInputPreparation.WriteNativeInputs(_repository,
                            work, inputs, season.LoadedResources.Source.Locale,ambientResources);
                        D3D11NormalPostInitialConstantPreparation.WriteNativeInputs(_repository,
                            current, inputs, D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        D3D11PrimaryForwardInputs.WriteFreshNativeInputs(_repository,
                            cityGatePublisher.PreparedBank, season.LoadedResources, current, inputs,
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        nativeRuleInputsPrepared = true;
                    }
                    else
                    {
                        ambientResources!.RequireCurrent(current.Present);
                        atmosphereResources!.RequireCurrent(current.Present.AcceptedSeasonCamera.Inputs.Source.Transmittance.RequireTables());
                        cloudMedia!.RequireCurrent(current.Present,
                            current.Lights.First(light=>(light.GroupMask&0x80)!=0).CompleteData.Span);
                        cloudResources!.RequireCurrent(_repository, current.Present,
                            D3D11MapSurface.OutputWidth, D3D11MapSurface.OutputHeight);
                        fogPrograms.RequireCurrentMask(current.Present);
                        skyResources!.RequireCompatibleGeneration(_repository, skyPrograms, current.Present);
                        var selectedSky = D3D11AtmosphereInitialConstantPreparation.SelectSkyProgram(current.Present,
                            current.Lights.First(light=>(light.GroupMask&0x80)!=0).CompleteData.Span,
                            skyPrograms, skyResources.Sampling.Presence);
                        skyResources.RequireCurrent(selectedSky);
                        starResources?.RequireCompatiblePackage(_repository,
                            current.Present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage);
                    }
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
                    return input.WithCurrentSeason(current, hdr);
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
        if(initialManagerUpdate)_d3d11Army?.AdvanceMotion(_logicalWorld!,0);
        var state = frame.ControllerState;
        return new(frame.Camera.Prefix.Span,
            [state.FocusX, state.FocusY, state.FocusZ, state.FocusW, state.Distance90, state.YawA8, state.PitchC0],
            frame.ZoomSelection.Band1E0, frame.ZoomSelection.Fraction1E4, delta, eligibleAnimationUpdate:initialManagerUpdate, sourceFrame: frame)
        {
            TerrainPicker = CurrentTerrainPicker,
            TerritoryOwnership = _d3d11Ownership?.Capture(_logicalWorld!),
            ArmyWorld = _d3d11Army?.Capture(_logicalWorld!),
            ArmyGroupingWorld = _d3d11ArmyGroupingQueries?.Invoke(_logicalWorld!),
            CityGateWorld = _d3d11CityGate?.Capture(_logicalWorld!, _d3d11CommonFrustumMode),
            AreaCapitalWorld = _d3d11AreaCapital?.Capture(_logicalWorld!, _d3d11CommonFrustumMode),
            WorldState = _logicalWorld,
            TerritoryDisplayEnabled = _d3d11TerritoryDisplay
        };
    }
    public void SetTerritoryDisplay(bool enabled)
    {
        _d3d11TerritoryDisplay = enabled;
        ScheduleD3D11MapFrame();
    }
    private void ScheduleD3D11MapFrame()
    {
        if (_d3d11Map is not { IsClosed: false, LastError: null } surface || _sourceMainCameraFrame is not { } frame) return;
        if(_d3d11AnimationDue&&_d3d11AnimationPacer is { } motionPacer)
            _d3d11Army?.AdvanceMotion(_logicalWorld!,motionPacer.FpsMode);
        var input=CaptureD3D11Input(frame,_d3d11Elapsed);
        if(_d3d11AnimationDue&&_d3d11AnimationPacer is { } pacer)surface.RecordAnimationUpdate(input,pacer.FpsMode);
        _d3d11AnimationDue=false;
        surface.Record(input); _d3d11Elapsed = 0;
    }
    private void ReleaseD3D11Map()
    {
        if (_d3d11Markers is not null) { _d3d11Markers.QueueFree(); _d3d11Markers = null; }
        _d3d11Ownership = null;
        _d3d11Army?.Dispose(); _d3d11Army = null;
        _d3d11CityGate = null;
        _d3d11AreaCapital = null;
        _d3d11AnimationPacer=null;_d3d11AnimationDue=false;
        var surface = _d3d11Map; _d3d11Map = null; _d3d11PresentedInput = null; _d3d11Elapsed = 0;
        var skyResources = _d3d11SkyResources; _d3d11SkyResources = null;
        if (surface is null) { skyResources?.Dispose(); return; }
        _d3d11Retirement = RetireD3D11MapAsync(surface, skyResources);
    }

    private void InitializeD3D11MapLocations()
    {
        var markers = new D3D11MapMarkers { Name = "NativeMapLocations", ZIndex = 1, Visible = _showLocations.ButtonPressed };
        _d3d11Markers = markers;
        var parent = _sourceMainCameraAspect as Control ?? _surface.GetParent() as Control ?? this;
        parent.AddChild(markers); markers.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        markers.Configure(_repository, _logicalWorld!, _projection!, TerrainMeshes!.Source, ForceMarkerColors,
            _text.SourceLocale, _playerForceId, _sourceSnapshot!);
        var scenario = _sourceSnapshot ?? throw new InvalidDataException("Native playable map requires its actual scenario snapshot.");
        _d3d11Ownership = new(scenario, _repository.LoadCeMap(),
            _repository.LoadTerrainTypes(scenario.FixedDataPath), _logicalWorld!,
            ForcePalette ?? throw new InvalidDataException("Native playable map source palette is unavailable."));
        var metadata = _repository.EffectiveScenarios.Single(s =>
            GameResourceId.File(s.SourcePath).ToString() == _logicalWorld!.ScenarioKey && s.Locale == _text.SourceLocale);
        _d3d11Army = new(_repository, _repository.LoadScenario(metadata), _repository.LoadCeMap(),
            _projection!, TerrainMeshes.Source.HeightField, _text.SourceLocale,_d3d11ArmyMotionQueries);
        RefreshD3D11MapLocationSelection();
        SetMeta("map_projection_available", true);
        SetMeta("map_projection_policy_id", _projection!.Policy.Id);
        SetMeta("native_map_marker_backend", "canvas-source-main");
        SetMeta("playable_unit_marker_count", 0);
        SetMeta("playable_structure_marker_count", 0);
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
    private static async Task RetireD3D11MapAsync(D3D11MapSurface surface, SkyMainResourceOwner? skyResources)
    {
        try { await surface.CloseAsync(); }
        finally { skyResources?.Dispose(); }
        if (GodotObject.IsInstanceValid(surface)) surface.QueueFree();
    }
}
