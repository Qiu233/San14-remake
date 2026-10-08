using System.Text.Json;
using Godot;
using San14.Data;
using San14.Core;

public partial class OriginalSceneView
{
    private OriginalD3D11MapSurface? _d3d11Map;
    private OriginalD3D11MapMarkers? _d3d11Markers;
    private OriginalD3D11TerritoryPublisher? _d3d11Ownership;
    private OriginalD3D11ArmyPublisher? _d3d11Army;
    private OriginalD3D11CityGatePublisher? _d3d11CityGate;
    private OriginalD3D11AreaCapitalPublisher? _d3d11AreaCapital;
    private Func<GameState, OriginalEventInitializationContext?>? _d3d11CurrentWorldQueries;
    private Func<GameState, UnitState, OriginalArmyGraphicsCurrentInputs?>? _d3d11ArmyMotionQueries;
    private Func<GameState, OriginalArmyMapGroupingFrame?>? _d3d11ArmyGroupingQueries;
    private uint _d3d11CommonFrustumMode;
    private OriginalD3D11MapInput? _d3d11PresentedInput;
    private Task _d3d11Retirement = Task.CompletedTask;
    private double _d3d11Elapsed;
    private bool _d3d11TerritoryDisplay = true;
    private OriginalD3D11AnimationPacer? _d3d11AnimationPacer;
    private bool _d3d11AnimationDue;
    private bool UseOriginalD3D11Map => _embedded && RenderingServer.GetCurrentRenderingDriverName() == "d3d12";
    private bool OriginalD3D11MapInteractive => _d3d11Map is { IsClosed: false, LastError: null, LastReceipt: not null };
    public void SetOriginalD3D11CurrentWorldQueries(Func<GameState, OriginalEventInitializationContext?> capture,
        Func<GameState, UnitState, OriginalArmyGraphicsCurrentInputs?>? armyMotion=null,
        Func<GameState, OriginalArmyMapGroupingFrame?>? armyGrouping=null)
    {
        _d3d11CurrentWorldQueries = capture;
        _d3d11ArmyMotionQueries = armyMotion;
        _d3d11ArmyGroupingQueries = armyGrouping;
    }

    private async Task InitializeOriginalD3D11MapAsync(uint generation)
    {
        if (_d3d11Map is not null || _sourceMainCameraFrame is not { } frame || _loadedSceneResources is not { } scene) return;
        OriginalD3D11MapSurface? surface = null;
        try
        {
            // Logical locations are built before the source camera owner.
            // Army screen offsets need that loaded owner's E940 anchors.
            _d3d11Markers!.ConfigureArmyLabels(_repository, _sourceSnapshot!, _d3d11CurrentWorldQueries,
                _sourceMainCamera!.Parameters);
            string source = Path.GetFullPath(ProjectSettings.GlobalizePath(ProjectSettings.GetSetting(
                "san14/render/source_package", "res://../.local/render-d3d11-e2e/input").AsString()));
            string libraryDefault = OS.HasFeature("editor")
                ? "res://../native/San14.Render.D3D11/bin/x64/Release/San14.Render.D3D11.dll"
                : Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!, "native", "windows-x64", "San14.Render.D3D11.dll");
            string librarySetting = System.Environment.GetEnvironmentVariable("SAN14_RENDER_NATIVE_LIBRARY")
                ?? ProjectSettings.GetSetting("san14/render/native_library", libraryDefault).AsString();
            string library = Path.GetFullPath(ProjectSettings.GlobalizePath(librarySetting));
            // Match the imported source package once at the scene loading boundary.
            // Camera requests reuse it; no package/executable hashes run per frame.
            using (var metadata = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(source, "source.json"))))
            {
                var root = metadata.RootElement;
                if (root.GetProperty("sourceScene").GetString() != scene.Scene.Source.ResourceUri ||
                    root.GetProperty("sceneSha256").GetString() != scene.Scene.Source.DecodedSha256)
                    throw new InvalidDataException("Imported native map package belongs to another Scene revision.");
            }
            var seasonResources = OriginalD3D11CurrentSeasonResources.LoadSourceReadyCpu(_repository,
                _logicalWorld!.NativeMapSeason ?? throw new InvalidDataException("Playable Main has no owned third-date season."), frame);
            string seasonalInputs = Path.Combine(Path.GetDirectoryName(source)!, "current-season-resource-input");
            var seasonLibrary = new OriginalD3D11CurrentSeasonLibrary(_repository, seasonResources, frame);
            seasonLibrary.WriteNativeLutInputs(seasonalInputs);
            new OriginalD3D11TerrainSeasonPalette(OriginalTerrainParameterResources.Read(scene)).WriteNativeInputs(seasonalInputs);
            var season = new OriginalD3D11CurrentSeasonPublisher(_repository, seasonResources, resourceLibrary: seasonLibrary);
            var writtenSeasonResources = seasonResources;
            int writtenSeasonCount = seasonLibrary.LoadedInstallations.Count;
            var hdr = OriginalD3D11CurrentHdrCatalog.Load(_repository, frame, source);
            var scenarioMetadata = _repository.EffectiveScenarios.Single(s =>
                GameResourceId.File(s.SourcePath).ToString() == _logicalWorld!.ScenarioKey && s.Locale == _text.SourceLocale);
            string inputs = Path.GetDirectoryName(source)!;
            // CPU clips/players only; retained category geometry and materials
            // are shared by all current graphics actions.
            OriginalArmyGraphicsAnimationNativeInputs.Write(_repository,new OriginalArmyGraphicsActions(_repository),
                Path.Combine(inputs,"army-graphics-action-input"));
            var cityGatePublisher = new OriginalD3D11CityGatePublisher(_repository, scene, _repository.LoadScenario(scenarioMetadata), _repository.LoadCeMap(),
                _logicalWorld!.NewGameOptions!, _d3d11CurrentWorldQueries ??
                    throw new InvalidDataException("Native City/Gate visuals require the current campaign query owner."),
                Path.Combine(inputs, "base-visual-world-input", "primary-templates"), Path.Combine(inputs, "city-accessory-input"));
            _d3d11CityGate = cityGatePublisher;
            _d3d11AreaCapital = new(_repository, scene, _repository.LoadScenario(scenarioMetadata),
                cityGatePublisher.PreparedBank, _d3d11CurrentWorldQueries);
            _d3d11CommonFrustumMode = OriginalMapModelNativeVisibilitySources.ReadCommonViewTemplate(
                _repository.LoadObjects("KIDSSystemResource", 0x6D011726), 0x899F101B).Value;
            OriginalD3D11CityGateFlagImports.WriteNativeKeys(Path.Combine(inputs, "city-accessory-next-input", "material-source"));
            long preparationNumber = 0;
            surface = new OriginalD3D11MapSurface { Name = "OriginalD3D11WorldMap" };
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
                    RecordOriginalD3D11MapFailure(error);
            };
            await surface.ConfigureAsync(source, library, CaptureOriginalD3D11Input(frame, 0,initialManagerUpdate:true), populationMask: 3,
                prepareCurrent: input =>
                {
                    var geometry = input.SourceFrame!;
                    var currentMain = geometry with { Camera = geometry.Camera.CopyAcceptedSnapshotForOwnedFrame(
                        ++preparationNumber, geometry.Camera.FrameProvenance) };
                    var current = season.PrepareCurrent(input.WorldState!, currentMain, (float)input.ElapsedSeconds);
                    if (!ReferenceEquals(writtenSeasonResources, season.LoadedResources))
                    {
                        int loadedCount = seasonLibrary.LoadedInstallations.Count;
                        if (loadedCount != writtenSeasonCount)
                        {
                            seasonLibrary.WriteNativeLutInputs(seasonalInputs);
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
                RecordOriginalD3D11MapFailure(error);
        }
    }

    private void RecordOriginalD3D11MapFailure(Exception error)
    {
        _d3d11PresentedInput = null;
        if (_d3d11Markers is not null) _d3d11Markers.Visible = false;
        ResetOriginalSourceCameraInput();
        _dragging = false; _panning = false;
        ClearPickedCell();
        RecordOriginalNativeMapUnavailable(error);
        if (_sourceMainCameraNotice is not null)
        {
            _sourceMainCameraNotice.Text = _text.Get("scene_map_failed") + "\n" + error.Message;
            _sourceMainCameraNotice.Visible = true;
        }
    }

    private OriginalD3D11MapInput CaptureOriginalD3D11Input(San14.Data.OriginalMainMapCameraFrame frame, double delta,bool initialManagerUpdate=false)
    {
        if(initialManagerUpdate)_d3d11Army?.AdvanceMotion(_logicalWorld!,0);
        var state = frame.ControllerState;
        return new(frame.Camera.Prefix.Span,
            [state.FocusX, state.FocusY, state.FocusZ, state.FocusW, state.Distance90, state.YawA8, state.PitchC0],
            frame.ZoomSelection.Band1E0, frame.ZoomSelection.Fraction1E4, delta, eligibleAnimationUpdate:initialManagerUpdate, sourceFrame: frame)
        {
            TerrainPicker = _terrainPicker,
            TerritoryOwnership = _d3d11Ownership?.Capture(_logicalWorld!),
            ArmyWorld = _d3d11Army?.Capture(_logicalWorld!),
            ArmyGroupingWorld = _d3d11ArmyGroupingQueries?.Invoke(_logicalWorld!),
            CityGateWorld = _d3d11CityGate?.Capture(_logicalWorld!, _d3d11CommonFrustumMode),
            AreaCapitalWorld = _d3d11AreaCapital?.Capture(_logicalWorld!, _d3d11CommonFrustumMode),
            WorldState = _logicalWorld,
            TerritoryDisplayEnabled = _d3d11TerritoryDisplay
        };
    }
    public void SetOriginalTerritoryDisplay(bool enabled)
    {
        _d3d11TerritoryDisplay = enabled;
        ScheduleOriginalD3D11MapFrame();
    }
    private void ScheduleOriginalD3D11MapFrame()
    {
        if (_d3d11Map is not { IsClosed: false, LastError: null } surface || _sourceMainCameraFrame is not { } frame) return;
        if(_d3d11AnimationDue&&_d3d11AnimationPacer is { } motionPacer)
            _d3d11Army?.AdvanceMotion(_logicalWorld!,motionPacer.FpsMode);
        var input=CaptureOriginalD3D11Input(frame,_d3d11Elapsed);
        if(_d3d11AnimationDue&&_d3d11AnimationPacer is { } pacer)surface.RecordAnimationUpdate(input,pacer.FpsMode);
        _d3d11AnimationDue=false;
        surface.Record(input); _d3d11Elapsed = 0;
    }
    private void ReleaseOriginalD3D11Map()
    {
        if (_d3d11Markers is not null) { _d3d11Markers.QueueFree(); _d3d11Markers = null; }
        _d3d11Ownership = null;
        _d3d11Army?.Dispose(); _d3d11Army = null;
        _d3d11CityGate = null;
        _d3d11AreaCapital = null;
        _d3d11AnimationPacer=null;_d3d11AnimationDue=false;
        var surface = _d3d11Map; _d3d11Map = null; _d3d11PresentedInput = null; _d3d11Elapsed = 0;
        if (surface is null) return;
        _d3d11Retirement = RetireOriginalD3D11MapAsync(surface);
    }

    private void InitializeOriginalD3D11MapLocations()
    {
        var markers = new OriginalD3D11MapMarkers { Name = "NativeMapLocations", ZIndex = 1, Visible = _showLocations.ButtonPressed };
        _d3d11Markers = markers;
        var parent = _sourceMainCameraAspect as Control ?? _surface.GetParent() as Control ?? this;
        parent.AddChild(markers); markers.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        markers.Configure(_repository, _logicalWorld!, _projection!, _terrainMeshes!.Source, _sourceMarkerColorCandidates,
            _text.SourceLocale, _playerForceId, _sourceSnapshot!);
        var scenario = _sourceSnapshot ?? throw new InvalidDataException("Native playable map requires its actual scenario snapshot.");
        _d3d11Ownership = new(scenario, _repository.LoadCeMap(),
            _repository.LoadTerrainTypes(scenario.FixedDataPath), _logicalWorld!,
            _sourceForcePalette ?? throw new InvalidDataException("Native playable map source palette is unavailable."));
        var metadata = _repository.EffectiveScenarios.Single(s =>
            GameResourceId.File(s.SourcePath).ToString() == _logicalWorld!.ScenarioKey && s.Locale == _text.SourceLocale);
        _d3d11Army = new(_repository, _repository.LoadScenario(metadata), _repository.LoadCeMap(),
            _projection!, _terrainMeshes.Source.HeightField, _text.SourceLocale,_d3d11ArmyMotionQueries);
        RefreshOriginalD3D11MapLocationSelection();
        SetMeta("map_projection_available", true);
        SetMeta("map_projection_policy_id", _projection!.Policy.Id);
        SetMeta("native_map_marker_backend", "canvas-source-main");
        SetMeta("playable_unit_marker_count", 0);
        SetMeta("playable_structure_marker_count", 0);
    }

    private void RefreshOriginalD3D11MapLocationSelection()
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
    private static async Task RetireOriginalD3D11MapAsync(OriginalD3D11MapSurface surface)
    {
        await surface.CloseAsync();
        if (GodotObject.IsInstanceValid(surface)) surface.QueueFree();
    }
}
