using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;
using San14.Core;
using San14.Client.MapInteraction;

/// <summary>Source-space preview with native unparented WorldPQ matrices.
/// Camera, material execution and later parent binding remain separate.</summary>
public partial class MapSceneView : Control
{
    private GameResourceRepository _repository = null!;
    private PreviewText _text = null!;
    private PreviewText? _safeText;
    private OptionButton _selection = null!;
    private Label _status = null!;
    private SubViewportContainer _surface = null!;
    private Camera3D _camera = null!;
    private Node3D _models = null!;
    private Node3D _terrain = null!;
    private MapSceneConfiguration _sceneConfig = null!;
    private readonly MapModelMeshCache _modelMeshes = new();
    private MapSceneModelResources? _sceneModels;
    private UiAssets? _markerAssets;
    private LoadedLayout? _markerLayout;
    private LoadedLayout? _unitMarkerLayout;
    private MapTerrainResources? _sceneTerrain;
    private TerrainMeshResult? TerrainMeshes => _sceneTerrain?.Meshes;
    private TerrainPicker? CurrentTerrainPicker => _sceneTerrain?.Picker;
    private ITerrainSurface? TerrainSurface => _sceneTerrain?.Surface;
    private PlayableTerrainOverlay? _territoryOverlay;
    private CheckButton _terrainTextureToggle = null!;
    private Vector3 _center;
    private float _radius = 1, _yaw = 0.7f, _pitch = 0.5f, _zoom = 1;
    // B/A distances fit three Zhang Jiao city marker centers in the original
    // 2560x1440 BMPs; C/D use their measured terrain scale ratios. Farther
    // levels are extrapolated until their original camera states are observed.
    private static readonly float[] PlayableZoomSteps =
        [0.00818f, 0.01484f, 0.02595f, 0.04885f, 0.08793f, 0.15827f, 0.28489f, 0.5128f];
    private const int PlayableDefaultZoomIndex = 2;
    private int _playableZoomIndex = PlayableDefaultZoomIndex;
    private bool _dragging, _panning, _loaded;
    private MapDocument? _logicalMap;
    private GameState? _logicalWorld;
    private ScenarioCityPreview? _sourceSnapshot;
    private MapForcePresentation? _forcePresentation;
    private MapStructureCatalog? _structureCatalog;
    private IReadOnlyDictionary<int, Color>? ForceMapColors => _forcePresentation?.MapColors;
    private IReadOnlyDictionary<int, Color>? ForceMarkerColors => _forcePresentation?.MarkerColors;
    private CeForceMapPalette? ForcePalette => _forcePresentation?.Palette;
    private IReadOnlyDictionary<string, string>? StructureNames => _structureCatalog?.Names;
    private LoadedSceneResources? _loadedSceneResources;
    private MapLocationLayer? _locations;
    private MapUnitLayer? _unitLayer;
    private PlayableMapStructureLayer? _structureLayer;
    private int? _playerForceId;
    private Node3D _previewWorld = null!;
    private OptionButton _locationSelection = null!;
    private Button _focusLocation = null!;
    private CheckButton _showLocations = null!;
    private Label _locationNotice = null!;
    private Vector3 _overviewCenter;
    private Vector3? _playableCameraCenter;
    private MapWorldProjection? _projection;
    private Label _cellInfo = null!;
    private MeshInstance3D? _selectionMarker;
    private SphereMesh? _selectionMarkerMesh;
    private StandardMaterial3D? _selectionMarkerMaterial;
    private Vector2 _pressPosition;
    private float _dragDistance;
    private Button _openSelectedCity = null!;
    private Button? _mapCityInformation;
    private Button? _mapCityOrders;
    private Button? _mapAreaOrders;
    private Button? _mapUnitOrders;
    private Button? _mapNextUnit;
    private Button _useSelectedHex = null!;
    private Action<int>? _openCityDetails;
    private Action<int>? _selectCommandCity;
    private Action<int>? _selectCommandArea;
    private Action<int>? _selectCommandUnit;
    private Action<int>? _acceptHex;
    private Action? _closeScene;
    private readonly MapSelection _mapSelection = new();
    private bool _detailsOpen;
    private bool _embedded;
    private string _sceneSummary = "";
    private uint _sceneLoadGeneration;
    private bool _sceneLoadInProgress;
    private bool _sceneSelectionWasDisabled;
    internal Task InitialSceneLoad { get; private set; } = Task.CompletedTask;
    internal bool IsPlayableMapReady => _loaded && _projection is not null &&
        (!UseD3D11Map || D3D11MapInteractive);

    internal void Configure(GameResourceRepository repository, PreviewText text, PreviewText? safeText, Action close,
        MapDocument? logicalMap = null, GameState? logicalWorld = null,
        Action<int>? openCityDetails = null, Action<int>? acceptHex = null,
        int? playerForceId = null, bool embedded = false,
        ScenarioCityPreview? sourceSnapshot = null, Action<int>? selectCommandCity = null,
        Action<int>? selectCommandUnit = null, Action<int>? selectCommandArea = null)
    {
        _repository = repository;
        _text = text;
        _safeText = safeText;
        _logicalMap = logicalMap;
        _logicalWorld = logicalWorld;
        _sourceSnapshot = sourceSnapshot;
        _openCityDetails = openCityDetails;
        _selectCommandCity = selectCommandCity;
        _selectCommandArea = selectCommandArea;
        _selectCommandUnit = selectCommandUnit;
        _acceptHex = acceptHex;
        _playerForceId = playerForceId;
        _embedded = embedded;
        _closeScene = close;
        Name = embedded ? "PlayableWorldMap" : "OriginalScenePreview";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var background = new ColorRect { Color = new Color("#111b1d") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (string side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", embedded ? 0 : 20);
        AddChild(margin);
        var column = new VBoxContainer();
        margin.AddChild(column);
        var title = new Label { Name = "ScenePreviewTitle", Text = text.Get(
            acceptHex is null ? "scene_preview_title" : "play_terrain_map_title") };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.Visible = !embedded;
        column.AddChild(title);
        column.AddChild(new Label
        {
            Name = "ScenePreviewNotice", Text = text.Get(
                acceptHex is null ? "scene_preview_notice" : "play_terrain_map_notice"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = !embedded
        });
        var toolbar = new HBoxContainer { Visible = !embedded };
        column.AddChild(toolbar);
        _selection = new OptionButton { Name = "SceneSelection", Visible = acceptHex is null };
        toolbar.AddChild(_selection);
        var reset = new Button { Name = "ResetSceneCamera", Text = text.Get("scene_preview_reset") };
        reset.Pressed += () => Run(ResetCamera);
        toolbar.AddChild(reset);
        if(UseD3D11Map)
        {
            var territoryDisplay = new CheckButton { Name="ShowMapTerritory",
                Text=text.Get("map_territory_display"),ButtonPressed=true };
            territoryDisplay.Toggled += SetTerritoryDisplay;
            toolbar.AddChild(territoryDisplay);
        }
        var terrainToggle = new CheckButton { Name = "ShowSceneTerrain", Text = text.Get("scene_preview_terrain"),
            ButtonPressed = true, Visible = acceptHex is null };
        terrainToggle.Toggled += visible => Run(() =>
        {
            _terrain.Visible = visible;
            if (_selectionMarker is not null) _selectionMarker.Visible = visible && _mapSelection.Current is not null;
        });
        toolbar.AddChild(terrainToggle);
        _terrainTextureToggle = new CheckButton { Name = "ShowTerrainTexture", Text = text.Get("scene_preview_terrain_texture"),
            ButtonPressed = true, Visible = acceptHex is null };
        _terrainTextureToggle.Toggled += _ => Run(UpdateTerrainMaterial);
        toolbar.AddChild(_terrainTextureToggle);
        toolbar.AddChild(new Label { Text = text.Get("scene_preview_controls"), SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _useSelectedHex = new Button
        {
            Name = "UseSceneTarget", Text = text.Get("scene_preview_use_target"),
            Disabled = true, Visible = acceptHex is not null
        };
        _useSelectedHex.Pressed += () => Run(() =>
        {
            if (_acceptHex is not null && _mapSelection.Current is { } selection)
                _acceptHex(selection.HexId);
        });
        toolbar.AddChild(_useSelectedHex);
        var back = new Button { Name = "CloseScenePreview", Text = text.Get("scene_preview_close") };
        back.Pressed += () => { if (!_detailsOpen) close(); };
        toolbar.AddChild(back);
        var locationBar = new HBoxContainer { Visible = !embedded };
        column.AddChild(locationBar);
        _showLocations = new CheckButton { Name = "ShowMapLocations", Text = text.Get("scene_preview_locations"), ButtonPressed = true, Disabled = true };
        _showLocations.Toggled += visible => Run(() =>
        {
            if (_locations is not null) _locations.Visible = visible;
            if (_d3d11Markers is not null) _d3d11Markers.Visible = visible;
        });
        locationBar.AddChild(_showLocations);
        _locationSelection = new OptionButton { Name = "SceneLocationSelection", Disabled = true };
        _locationSelection.ItemSelected += index => Run(() => SelectLocation((int)index));
        locationBar.AddChild(_locationSelection);
        _focusLocation = new Button { Name = "FocusSceneLocation", Text = text.Get("scene_preview_focus_location"), Disabled = true };
        _focusLocation.Pressed += () => Run(FocusLocation);
        locationBar.AddChild(_focusLocation);
        _openSelectedCity = new Button { Name = "OpenSceneCityDetails", Text = text.Get("detail_city_title"), Disabled = true };
        _openSelectedCity.Pressed += () => Run(() =>
        {
            if (_mapSelection.Current?.CityDetails?.Id is int cityId) _openCityDetails?.Invoke(cityId);
        });
        locationBar.AddChild(_openSelectedCity);
        _locationNotice = new Label { Name = "SceneLocationNotice", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        locationBar.AddChild(_locationNotice);
        _cellInfo = new Label { Name = "ScenePickedCell", Text = text.Get("scene_preview_cell_none"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = !embedded };
        column.AddChild(_cellInfo);
        _status = new Label { Name = "ScenePreviewStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = !embedded };
        column.AddChild(_status);
        _surface = new SubViewportContainer
        {
            Name = "SceneViewportContainer", Stretch = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop
        };
        AttachSourceCameraSurface(column);
        var viewport = new SubViewport
        {
            Name = "SceneViewport", OwnWorld3D = true, Size = new Vector2I(640, 360),
            RenderTargetUpdateMode = UseD3D11Map ? SubViewport.UpdateMode.Disabled : SubViewport.UpdateMode.Always,
            TextureMipmapBias = 0, Scaling3DScale = 1, UseTaa = false,
            ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled
        };
        _surface.AddChild(viewport);
        var world = new Node3D { Name = "PreviewWorld" };
        _previewWorld = world;
        viewport.AddChild(world);
        _models = new Node3D { Name = "PlacedModels" };
        world.AddChild(_models);
        _terrain = new Node3D { Name = "SourceTerrain" };
        world.AddChild(_terrain);
        _camera = new Camera3D { Name = "SceneCamera", Current = true, Fov = 50 };
        world.AddChild(_camera);
        world.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("#111b1d")
            }
        });
        _surface.GuiInput += input => Run(() => OnInput(input));
        _surface.MouseExited += () => _d3d11Markers?.ClearPointer();
        // Native Main renders at its canonical extent; resizing its hidden
        // preview only updates input coordinates, not the source camera.
        _surface.Resized += () => { if(!UseD3D11Map)Run(UpdateCamera); };
        if (_embedded && _openCityDetails is not null)
        {
            _mapCityInformation = new Button
            {
                Name = "MapCityInformation", Visible = false,
                AnchorLeft = 1, AnchorRight = 1,
                OffsetLeft = -222, OffsetRight = -12,
                OffsetTop = 67, OffsetBottom = 110,
                MouseFilter = MouseFilterEnum.Stop
            };
            _mapCityInformation.Pressed += () => Run(() =>
            {
                if (_mapSelection.Current?.CityDetails?.Id is int cityId) _openCityDetails(cityId);
            });
            AddChild(_mapCityInformation);
        }
        if (_embedded && _selectCommandCity is not null)
        {
            _mapCityOrders = new Button
            {
                Name = "MapCityOrders", Visible = false,
                AnchorLeft = 1, AnchorRight = 1,
                OffsetLeft = -222, OffsetRight = -12,
                OffsetTop = 116, OffsetBottom = 159,
                MouseFilter = MouseFilterEnum.Stop
            };
            _mapCityOrders.Pressed += () => Run(() =>
            {
                if (_mapSelection.Current?.LocationCommand?.Id is int locationId) _selectCommandCity(locationId);
            });
            AddChild(_mapCityOrders);
        }
        if (_embedded && _selectCommandArea is not null)
        {
            _mapAreaOrders = new Button
            {
                Name = "MapAreaOrders", Visible = false,
                AnchorLeft = 1, AnchorRight = 1,
                OffsetLeft = -222, OffsetRight = -12,
                OffsetTop = 165, OffsetBottom = 208,
                MouseFilter = MouseFilterEnum.Stop
            };
            _mapAreaOrders.Pressed += () => Run(() =>
            {
                if (_mapSelection.Current?.AreaCommand?.Id is int areaId) _selectCommandArea?.Invoke(areaId);
            });
            AddChild(_mapAreaOrders);
        }
        if (_embedded && _selectCommandUnit is not null)
        {
            _mapUnitOrders = new Button
            {
                Name = "MapUnitOrders", Visible = false,
                AnchorLeft = 1, AnchorRight = 1,
                OffsetLeft = -222, OffsetRight = -12,
                OffsetTop = 214, OffsetBottom = 257,
                MouseFilter = MouseFilterEnum.Stop
            };
            _mapUnitOrders.Pressed += () => Run(() =>
            {
                if (_mapSelection.Current?.SelectedUnit?.Id is int unitId) _selectCommandUnit(unitId);
            });
            AddChild(_mapUnitOrders);
            _mapNextUnit = new Button
            {
                Name = "MapNextUnit", Visible = false,
                Text = text.Get("play_map_next_unit"),
                AnchorLeft = 1, AnchorRight = 1,
                OffsetLeft = -324, OffsetRight = -228,
                OffsetTop = 214, OffsetBottom = 257,
                MouseFilter = MouseFilterEnum.Stop
            };
            _mapNextUnit.Pressed += () => Run(SelectNextUnitAtPickedHex);
            AddChild(_mapNextUnit);
        }
        if (_embedded)
        {
            var resetMapCamera = new Button
            {
                Name = "ResetPlayableMapCamera", Text = text.Get("play_map_reset_camera"),
                AnchorLeft = 1, AnchorRight = 1,
                OffsetLeft = -222, OffsetRight = -12,
                OffsetTop = 263, OffsetBottom = 306,
                MouseFilter = MouseFilterEnum.Stop
            };
            resetMapCamera.Pressed += () => Run(ResetCamera);
            AddChild(resetMapCamera);
        }
        _selection.ItemSelected += index => _ = StartSceneLoadAsync((int)index);
        Run(() =>
        {
            using Godot.FileAccess sceneFile = Godot.FileAccess.Open("res://ui/scene-preview.json", Godot.FileAccess.ModeFlags.Read)
                ?? throw new InvalidDataException("Scene preview configuration is missing.");
            using Godot.FileAccess projectionFile = Godot.FileAccess.Open("res://ui/map-projection.json", Godot.FileAccess.ModeFlags.Read)
                ?? throw new InvalidDataException("Map projection configuration is missing.");
            _sceneConfig = MapSceneConfiguration.Parse(sceneFile.GetAsText(), projectionFile.GetAsText(), _embedded);
            foreach (MapSceneDefinition scene in _sceneConfig.Scenes)
                _selection.AddItem(text.Get(scene.LabelKey));
            int initialScene = _sceneConfig.InitialSceneIndex(_acceptHex is not null);
            _selection.Select(initialScene);
            InitialSceneLoad = StartSceneLoadAsync(initialScene);
        });
        if (_acceptHex is not null && !_locationSelection.Disabled)
            _locationSelection.GrabFocus();
        else if (_selection.Visible && _selection.ItemCount > 0) _selection.GrabFocus();
        else back.GrabFocus();
    }

    private void Run(Action action)
    {
        if (_detailsOpen) return;
        try { action(); }
        catch (Exception error) { HandleSceneError(error); }
    }

    private void HandleSceneError(Exception error)
    {
        _loaded = false;
        ClearModels();
        SetMeta("scene_loaded", false);
        SetMeta("scene_error", error.Message);
        string heading;
        try { heading = _safeText?.Get("scene_preview_failed") ?? error.GetType().Name; }
        catch (Exception textError) { GD.PushError(textError.ToString()); heading = error.GetType().Name; }
        _status.Text = heading + "\n" + error.Message;
        if (_embedded) _status.Visible = true;
        GD.PushError(error.ToString());
    }

    private void ClearModels()
    {
        ReleaseSceneryMap();
        ReleaseNativeMap();
        ReleaseSourceCamera();
        ReleaseArmyVisuals();
        _dragging = false; _panning = false; _dragDistance = 0;
        _projection = null;
        _forcePresentation = null;
        RemoveMeta("source_force_map_palette_status");
        RemoveMeta("source_force_map_palette_count");
        RemoveMeta("source_force_map_palette_missing_force_slots");
        if (_selectionMarker is not null) { _previewWorld.RemoveChild(_selectionMarker); _selectionMarker.Free(); _selectionMarker = null; }
        _selectionMarkerMesh?.Dispose(); _selectionMarkerMesh = null;
        _selectionMarkerMaterial?.Dispose(); _selectionMarkerMaterial = null;
        ClearPickedCell();
        if (_unitLayer is not null) { _previewWorld.RemoveChild(_unitLayer); _unitLayer.Free(); _unitLayer = null; }
        if (_structureLayer is not null) { _previewWorld.RemoveChild(_structureLayer); _structureLayer.Free(); _structureLayer = null; }
        RemoveMeta("playable_unit_marker_count");
        RemoveMeta("playable_structure_marker_count");
        if (_locations is not null) { _previewWorld.RemoveChild(_locations); _locations.Free(); _locations = null; }
        _sceneModels?.ReleaseCityDisplays();
        _locationSelection.Clear(); _locationSelection.Disabled = true;
        _focusLocation.Disabled = true; _showLocations.Disabled = true;
        _locationNotice.Text = "";
        _playableCameraCenter = null;
        RemoveMeta("playable_start_city_id");
        SetMeta("map_projection_available", false);
        foreach (Node child in _models.GetChildren()) { _models.RemoveChild(child); child.Free(); }
        ReleaseTerrainGeometry();
        _territoryOverlay?.Dispose();
        _territoryOverlay = null;
        _terrain.RemoveMeta("playable_territory_overlay");
        _terrain.RemoveMeta("gbi_material");
        _sceneTerrain?.Dispose();
        _sceneTerrain = null;
        _sceneModels?.Dispose();
        _sceneModels = null;
        _loadedSceneResources = null;
        _markerAssets?.Dispose();
        _markerAssets = null;
        _markerLayout = null;
        _unitMarkerLayout = null;
    }

    private void ReleaseTerrainGeometry()
    {
        // Detach instances before disposing meshes owned by adaptive batches.
        if (GodotObject.IsInstanceValid(_terrain))
            foreach (Node child in _terrain.GetChildren()) { _terrain.RemoveChild(child); child.Free(); }
        _sceneTerrain?.ReleaseGeometry();
    }

    private Task StartSceneLoadAsync(int index) => _nativeMapClosing
        ? Task.CompletedTask : TrackMapInitialization(StartSceneLoadCoreAsync(index));

    private async Task StartSceneLoadCoreAsync(int index)
    {
        if (_detailsOpen) return;
        uint generation = ++_sceneLoadGeneration;
        if (!_sceneLoadInProgress) _sceneSelectionWasDisabled = _selection.Disabled;
        _sceneLoadInProgress = true;
        _selection.Disabled = true;
        try { await LoadSceneAsync(index, generation); }
        catch (Exception error)
        {
            if (generation == _sceneLoadGeneration && GodotObject.IsInstanceValid(this) && IsInsideTree())
                HandleSceneError(error);
        }
        finally
        {
            if (generation == _sceneLoadGeneration && GodotObject.IsInstanceValid(this) && IsInsideTree())
            {
                _selection.Disabled = _sceneSelectionWasDisabled;
                _sceneLoadInProgress = false;
            }
        }
    }

    private async Task LoadSceneAsync(int index, uint generation)
    {
        if ((uint)index >= _sceneConfig.Scenes.Count) throw new InvalidDataException("Scene preview selection is outside its configured sources.");
        _loaded = false;
        ClearModels();
        SetMeta("scene_loaded", false);
        RemoveMeta("scene_error");
        _status.Text = _text.Get("scene_preview_loading");
        LoadedSceneResources scene = SceneResources.Load(_repository, _sceneConfig.Archive, _sceneConfig.Scenes[index].FileId);
        ScenePlacements placements = ScenePlacementReader.Read(scene);
        var effects = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        int missingQuaternions = 0, modelCount = 0;
        Aabb bounds = default;
        bool haveBounds = false;
        MapTerrainResources preparedTerrain = await MapTerrainResources.PrepareAsync(_repository,
            scene, _embedded ? 0 : _sceneConfig.TerrainDepth, UseD3D11Map);
        if (generation != _sceneLoadGeneration || !GodotObject.IsInstanceValid(this) || !IsInsideTree())
        {
            preparedTerrain.DiscardPrepared();
            return;
        }
        _loadedSceneResources = scene;
        // Install the owner before adaptive setup so load-error cleanup releases every upload.
        _sceneTerrain = preparedTerrain;
        _sceneModels = UseD3D11Map ? null : new(_repository, scene, _sourceSnapshot,
            _text.SourceLocale, _sceneConfig.ModelTextures, _modelMeshes);
        if (_embedded) _sceneTerrain.InitializeAdaptiveGeometry();
        LoadedTerrainResources terrain = TerrainMeshes!.Source;
        LoadedTerrainShaderResources terrainShader = _sceneTerrain.Shader;
        G2ShaderTextureBases terrainTextureBases = _sceneTerrain.TextureBases;
        if(!UseD3D11Map)((SubViewport)_surface.GetChild(0)).AnisotropicFilteringLevel = _sceneTerrain.Gbi!.RequiredAnisotropy switch
        {
            1 => Viewport.AnisotropicFiltering.Disabled,
            2 => Viewport.AnisotropicFiltering.Anisotropy2X,
            4 => Viewport.AnisotropicFiltering.Anisotropy4X,
            8 => Viewport.AnisotropicFiltering.Anisotropy8X,
            16 => Viewport.AnisotropicFiltering.Anisotropy16X,
            _ => throw new InvalidDataException("Unsupported original material anisotropy.")
        };
        foreach (TerrainPatch patch in TerrainMeshes.Patches)
        {
            if(!UseD3D11Map)
            {
            var node = new MeshInstance3D
            {
                Name = $"TerrainPatch_{patch.SourceNode.Index}", Mesh = patch.Mesh,
                MaterialOverride = _terrainTextureToggle.ButtonPressed ? TerrainSurface!.Material : TerrainMeshes.PreviewMaterial
            };
            _terrain.AddChild(node);
            node.SetMeta("source_tree_node", patch.SourceNode.Index);
            }
            bounds = haveBounds ? bounds.Merge(patch.Mesh.GetAabb()) : patch.Mesh.GetAabb();
            haveBounds = true;
        }
        _terrain.SetMeta("tree_depth", _embedded ? -1 : _sceneConfig.TerrainDepth);
        _terrain.SetMeta("patch_count", TerrainMeshes.Patches.Count);
        _terrain.SetMeta("original_initial_height_adapter", true);
        _terrain.SetMeta("original_topology_verified", true);
        _terrain.SetMeta("original_world_mapping_verified", false);
        _terrain.SetMeta("height_resource_uri", terrain.HeightField.Source.ResourceUri);
        _terrain.SetMeta("height_decoded_sha256", terrain.HeightField.Source.DecodedSha256);
        if(!UseD3D11Map)
        {
            _terrain.SetMeta("gbi_material", _sceneTerrain!.Gbi!.Material);
        }
        _terrain.SetMeta("gbuffer_shader_resource_uri", terrainShader.Shader.Source.ResourceUri);
        _terrain.SetMeta("gbuffer_shader_decoded_sha256", terrainShader.Shader.Source.DecodedSha256);
        _terrain.SetMeta("gbuffer_shader_wrapper_uri", terrainShader.Parameters.Wrapper.Source.ResourceUri);
        _terrain.SetMeta("gbuffer_shader_property_hash", terrainShader.ShaderReference.NameHash);
        _terrain.SetMeta("gbuffer_shader_raw_reference", terrainShader.ShaderReference.RawValue);
        _terrain.SetMeta("gbuffer_shader_active_combination_resolved", false);
        _terrain.SetMeta("gbuffer_shader_source_texture_bases", terrainTextureBases.Rows.Select(row => (long)row.TextureBase).ToArray());
        foreach (ScenePlacement placement in placements.Instances)
        {
            if(UseD3D11Map)continue;
            SceneInstance source = placement.Source;
            var provenance = PlacementMetadata(placement);
            if (placement.Model is not SceneModelReference reference)
            {
                effects.Add(provenance);
                continue;
            }
            SceneTransformValues transform = placement.InstanceTransform;
            if (transform.Quaternion is null) missingQuaternions++;
            UnitWorldTransformState worldTransform = UnitWorldTransform.ConstructScenePlacement(placement);
            System.Numerics.Matrix4x4 nativeMatrix = worldTransform.PositionMatrix();
            // Numerics uses row vectors; Godot stores the corresponding basis
            // vectors as columns. Preserve native float32 products and raw Q.
            Transform3D matrix = new(new Basis(
                new Vector3(nativeMatrix.M11,nativeMatrix.M12,nativeMatrix.M13),
                new Vector3(nativeMatrix.M21,nativeMatrix.M22,nativeMatrix.M23),
                new Vector3(nativeMatrix.M31,nativeMatrix.M32,nativeMatrix.M33)),
                new Vector3(nativeMatrix.M41,nativeMatrix.M42,nativeMatrix.M43));
            FacilityModelDisplay assets = _sceneModels!.LoadPlacement(reference);
            ModelMeshResult model = assets.Mesh;
            PreviewMaterialSet materials = assets.Materials;
            var node = new MeshInstance3D
            {
                Name = $"Instance_{source.Index}_{source.Object.ObjectId:X8}", Mesh = model.Mesh, Transform = matrix
            };
            _models.AddChild(node);
            for (int surface = 0; surface < materials.Materials.Count; surface++)
                node.SetSurfaceOverrideMaterial(surface, materials.Materials[surface]);
            node.SetMeta("preview_materials", materials.Sources);
            foreach (var value in provenance) node.SetMeta(value.Key.AsString(), value.Value);
            node.SetMeta("quaternion_source", transform.Quaternion is null ? "definition-unit" : "instance");
            node.SetMeta("source_unparented_worldpq_matrix_verified",true);
            node.SetMeta("worldpq_quaternion_normalized",false);
            node.SetMeta("source_worldpq_transform_basis",worldTransform.Basis);
            node.SetMeta("model_resource_uri", model.Source.ResourceUri);
            node.SetMeta("model_decoded_sha256", model.Source.DecodedSha256);
            node.SetMeta("dictionary_resource_uri", reference.Dictionary.Source.ResourceUri);
            node.SetMeta("dictionary_decoded_sha256", reference.Dictionary.Source.DecodedSha256);
            node.SetMeta("model_object_id", reference.ModelObject.ObjectId);
            node.SetMeta("preview_transform", matrix);
            Aabb worldBounds = matrix * model.Mesh.GetAabb();
            if (!worldBounds.Position.IsFinite() || !worldBounds.Size.IsFinite())
                throw new InvalidDataException($"Scene instance {source.Index}: preview bounds are not finite.");
            bounds = haveBounds ? bounds.Merge(worldBounds) : worldBounds;
            haveBounds = true;
            modelCount++;
        }
        if (!haveBounds) throw new InvalidDataException($"Scene {scene.Scene.Source.ResourceUri} contains no supported geometry.");
        SetMeta("source_resource_uri", scene.Scene.Source.ResourceUri);
        SetMeta("source_decoded_sha256", scene.Scene.Source.DecodedSha256);
        SetMeta("source_instance_count", placements.Instances.Count);
        SetMeta("displayed_model_count", modelCount);
        SetMeta("unshown_effect_count", effects.Count);
        SetMeta("unshown_effects", effects);
        SetMeta("unique_mesh_count", (_sceneModels?.PlacementMeshCount ?? 0));
        SetMeta("preview_texture_count", _sceneModels?.TextureCount ?? 0);
        SetMeta("native_map_legacy_preview_resources", !UseD3D11Map);
        SetMeta("terrain_patch_count", _sceneTerrain!.ActivePatches.Count);
        SetMeta("terrain_world_mapping_verified", false);
        SetMeta("missing_instance_quaternion_count", missingQuaternions);
        SetMeta("source_space_bounds", bounds);
        SetMeta("axis_conversion_applied", false);
        SetMeta("world_position_rescaled", false);
        SetMeta("original_materials_resolved", false);
        SetMeta("native_quaternion_convention_verified", false);
        SetMeta("preview_transform_convention", "XYZW normalized; source scale then rotation then translation; unit definitions only");
        _center = bounds.GetCenter();
        _overviewCenter = _center;
        double radius = Math.Sqrt((double)bounds.Size.X * bounds.Size.X +
            (double)bounds.Size.Y * bounds.Size.Y + (double)bounds.Size.Z * bounds.Size.Z) * 0.5;
        if (!_center.IsFinite() || !double.IsFinite(radius) || radius > float.MaxValue)
            throw new InvalidDataException("Scene bounds cannot be framed with finite preview coordinates.");
        _radius = Math.Max((float)radius, 1);
        LoadLogicalLocations(_sceneConfig.Scenes[index].FileId);
        _loaded = true;
        ResetCamera();
        await InitializeNativeMapAsync(generation);
        if (generation != _sceneLoadGeneration || !GodotObject.IsInstanceValid(this) || !IsInsideTree()) return;
        SetMeta("scene_loaded", true);
        _status.Text = string.Format(CultureInfo.InvariantCulture, _text.Get("scene_preview_counts"),
            placements.Instances.Count, modelCount, effects.Count, (_sceneModels?.PlacementMeshCount ?? 0), _sceneModels?.TextureCount ?? 0, _sceneTerrain!.ActivePatches.Count);
        _sceneSummary = _status.Text;
        GD.Print($"SCENE_PREVIEW_LOADED {scene.Scene.Source.ResourceUri} instances={placements.Instances.Count} models={modelCount} effects={effects.Count} meshes={(_sceneModels?.PlacementMeshCount ?? 0)} missingQuaternion={missingQuaternions} textures={_sceneModels?.TextureCount ?? 0} terrainPatches={_sceneTerrain!.ActivePatches.Count}");
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 value) => new(value.X, value.Y, value.Z);

    private void UpdateTerrainMaterial()
    {
        if (TerrainMeshes is null || TerrainSurface is null) return;
        foreach (MeshInstance3D patch in _terrain.GetChildren().OfType<MeshInstance3D>())
            patch.MaterialOverride = _territoryOverlay?.Material ??
                (_terrainTextureToggle.ButtonPressed ? TerrainSurface!.Material : TerrainMeshes.PreviewMaterial);
    }

    private static Godot.Collections.Dictionary PlacementMetadata(ScenePlacement placement) => new()
    {
        ["source_instance_index"] = placement.Source.Index,
        ["source_object_id"] = placement.Source.Object.ObjectId,
        ["instance_resource_uri"] = placement.Source.InstanceDocument.Source.ResourceUri,
        ["instance_decoded_sha256"] = placement.Source.InstanceDocument.Source.DecodedSha256,
        ["definition_resource_uri"] = placement.Source.DefinitionDocument.Source.ResourceUri,
        ["definition_decoded_sha256"] = placement.Source.DefinitionDocument.Source.DecodedSha256,
        ["definition_object_id"] = placement.Source.Definition.ObjectId,
        ["instance_raw_header"] = placement.Source.Object.RawHeader,
        ["definition_raw_header"] = placement.Source.Definition.RawHeader,
        ["instance_transform"] = TransformMetadata(placement.InstanceTransform),
        ["definition_transform"] = TransformMetadata(placement.DefinitionTransform)
    };

    private static Godot.Collections.Dictionary TransformMetadata(SceneTransformValues value)
    {
        var result = new Godot.Collections.Dictionary
        {
            ["position"] = ToGodot(value.Position), ["scale"] = ToGodot(value.Scale),
            ["position_property"] = PropertyMetadata(value.PositionProperty),
            ["scale_property"] = PropertyMetadata(value.ScaleProperty),
            ["quaternion_present"] = value.Quaternion is not null
        };
        if (value.Quaternion is System.Numerics.Vector4 q)
        {
            result["raw_quaternion_xyzw"] = new float[] { q.X, q.Y, q.Z, q.W };
            result["quaternion_property"] = PropertyMetadata(value.QuaternionProperty!);
        }
        return result;
    }

    private static Godot.Collections.Dictionary PropertyMetadata(DokProperty property) => new()
    {
        ["index"] = property.Index, ["name_hash"] = property.NameHash,
        ["type_code"] = property.TypeCode, ["element_count"] = property.ElementCount,
        ["descriptor_offset"] = property.DescriptorOffset, ["value_offset"] = property.ValueOffset,
        ["raw_descriptor"] = property.RawDescriptor, ["raw_value"] = property.RawValue
    };

    private void ResetCamera()
    {
        if(ResetSourceCamera())return;
        _center = _playableCameraCenter ?? _overviewCenter;
        _yaw = _playableCameraCenter is null ? 0.7f : 0f;
        _pitch = _playableCameraCenter is null ? 0.5f : MathF.PI * 0.5f;
        _playableZoomIndex = PlayableDefaultZoomIndex;
        _zoom = _playableCameraCenter is null ? 1 : PlayableZoomSteps[_playableZoomIndex];
        UpdateCamera();
    }

    private void LoadLogicalLocations(uint scene)
    {
        if (_logicalMap is null || _logicalWorld is null || TerrainMeshes is null ||
            !_sceneConfig.TryGetProjection(scene, out MapWorldProjectionPolicy? policy) ||
            !policy.MapSha256.Equals(_logicalWorld.MapSha256, StringComparison.OrdinalIgnoreCase) ||
            policy.FieldResourceUri != _sceneTerrain!.ProjectionSource.ResourceUri ||
            !policy.FieldDecodedSha256.Equals(_sceneTerrain!.ProjectionSource.DecodedSha256, StringComparison.OrdinalIgnoreCase))
        {
            _locationNotice.Text = _text.Get("scene_preview_projection_unavailable");
            return;
        }
        if (policy.SourceFormulaId == CeHexWorldCoordinates.FormulaId)
            SetMeta("coordinate_rule_version",
                CeHexWorldCoordinates.GetRuleVersion(_text.SourceLocale));
        MapWorldProjection projection = MapWorldProjection.Build(_logicalMap, policy);
        _projection = projection;
        if(UseD3D11Map)
        {
            _forcePresentation = LoadForcePresentation();
            _sceneTerrain!.BuildPicker();
            InitializeD3D11MapLocations();
            return;
        }
        if (_acceptHex is not null)
        {
            _forcePresentation = LoadForcePresentation();
            _structureCatalog = LoadStructureCatalog();
            _territoryOverlay = PlayableTerrainOverlay.Build(TerrainSurface!, projection,
                _logicalWorld, ForceMapColors);
            AnnotateColorSource(_territoryOverlay, _logicalWorld);
            UpdateTerrainMaterial();
            _terrain.SetMeta("playable_territory_overlay", _territoryOverlay.Material);
        }
        _sceneTerrain!.BuildPicker();
        _markerAssets ??= new UiAssets(_repository);
        _markerLayout ??= _repository.LoadLayout("ScreenLayout", 0xEDC35713);
        if (_acceptHex is not null)
            _unitMarkerLayout ??= _repository.LoadLayout("ScreenLayout", 0x3DA1641D);
        _locations = new MapLocationLayer { Visible = _showLocations.ButtonPressed };
        _previewWorld.AddChild(_locations);
        _locations.Build(projection, TerrainMeshes, _logicalWorld, _markerAssets,
            _markerLayout, ForceMarkerColors,
            _acceptHex is null ? null : ResolveCityModelDisplays(_logicalWorld),
            _acceptHex is null ? null : _sceneModels?.ResolveGates(_logicalWorld));
        _locations.SetViewportHeight(_surface.Size.Y);
        if (_acceptHex is not null && _playerForceId is int playerId &&
            _logicalWorld.Forces.TryGetValue(playerId, out ForceState? force))
        {
            int? rulerCityId = force.RulerId is int rulerId &&
                _logicalWorld.Officers.TryGetValue(rulerId, out OfficerState? ruler)
                ? ruler.LocationId : null;
            ProjectedMapLocation? start = _locations.Locations.FirstOrDefault(location =>
                !location.IsGate && location.Id == rulerCityId && location.ForceId == playerId)
                ?? _locations.Locations.FirstOrDefault(location =>
                    !location.IsGate && location.ForceId == playerId);
            if (start is not null)
            {
                _playableCameraCenter = start.Position;
                SetMeta("playable_start_city_id", start.Id);
            }
        }
        if (_acceptHex is not null && _playerForceId is int player)
        {
            _unitLayer = new MapUnitLayer();
            _previewWorld.AddChild(_unitLayer);
            _unitLayer.Build(projection, TerrainMeshes, _logicalWorld, player, _markerAssets,
                _unitMarkerLayout ?? throw new InvalidDataException("Original unit sign layout is unavailable."),
                ForceMarkerColors);
            _unitLayer.SetViewportHeight(_surface.Size.Y);
            RefreshArmyVisuals(_logicalWorld);
            _structureLayer = new PlayableMapStructureLayer();
            _previewWorld.AddChild(_structureLayer);
        _structureLayer.Build(projection, TerrainMeshes, _logicalWorld,
            ForceMarkerColors, StructureNames,
            _sceneModels?.ResolveFacilities(_logicalWorld), _text.Get("detail_unknown"));
            _structureLayer.SetViewportHeight(_surface.Size.Y);
        }
        for (int i = 0; i < _locations.Locations.Count; i++)
        {
            ProjectedMapLocation location = _locations.Locations[i];
            _locationSelection.AddItem(location.Name, i);
        }
        bool available = _locations.Locations.Count > 0;
        _locationSelection.Disabled = !available; _focusLocation.Disabled = !available; _showLocations.Disabled = !available;
        if (available) { _locationSelection.Select(0); _locations.Select(0); }
        _locationNotice.Text = string.Format(CultureInfo.InvariantCulture, _text.Get("scene_preview_projection_notice"),
            _locations.Locations.Count, _locations.UnplacedCount);
        SetMeta("map_projection_available", true);
        SetMeta("map_projection_policy_id", policy.Id);
        SetMeta("map_projection_original_verified", false);
        SetMeta("playable_unit_marker_count", _unitLayer?.Units.Count ?? 0);
        SetMeta("playable_structure_marker_count", _structureLayer?.Count ?? 0);
        if (_logicalWorld?.RulesetId == PlayableSkeleton.RulesetId)
            SetMeta("world_period_number", _logicalWorld.PeriodNumber);
    }

    private MapForcePresentation? LoadForcePresentation()
    {
        if (_sourceSnapshot is null || _logicalWorld is null)
        {
            SetMeta("source_force_map_palette_status", "scenario-snapshot-unavailable");
            return null;
        }
        return MapForcePresentation.Load(_sourceSnapshot, _text.SourceLocale);
    }

    private MapStructureCatalog? LoadStructureCatalog()
    {
        if (_sourceSnapshot?.ObjectSourceRecords is null || _logicalWorld is null) return null;
        MapStructureCatalog catalog = MapStructureCatalog.Load(_repository, _sourceSnapshot, _logicalWorld);
        SetMeta("structure_definition_source_uri", catalog.SourceUri);
        SetMeta("structure_definition_source_sha256", catalog.SourceSha256);
        SetMeta("structure_definition_name_count", catalog.Names.Count);
        return catalog;
    }

    private void AnnotateColorSource(PlayableTerrainOverlay overlay, GameState world)
    {
        int[] missing = world.Forces.Keys.Where(forceId =>
            ForceMapColors is null || !ForceMapColors.ContainsKey(forceId)).Order().ToArray();
        SetMeta("source_force_map_palette_status", ForcePalette is null
            ? "scenario-snapshot-unavailable" : missing.Length == 0 ? "all-current-forces" : "partial");
        SetMeta("source_force_map_palette_count", ForceMapColors?.Count ?? 0);
        SetMeta("source_force_map_palette_missing_force_slots", string.Join(",", missing));
        overlay.Material.SetMeta("source_force_map_palette_count", ForceMapColors?.Count ?? 0);
        overlay.Material.SetMeta("source_force_map_palette_formula_id",
            ForcePalette?.SourceFormulaId ?? "");
        overlay.Material.SetMeta("force_map_palette_rule_version",
            ForcePalette?.ImplementationVersion ?? "");
        SetMeta("source_marker_color_candidate_selector", MapForcePresentation.MarkerColorSelector);
        SetMeta("source_marker_color_candidate_count",
            ForceMarkerColors?.Count ?? 0);
        SetMeta("original_map_marker_force_color_binding_verified", false);
        overlay.Material.SetMeta("original_overlay_composite_verified", false);
    }

    internal void RefreshPlayableWorld(GameState world)
    {
        if (!_embedded || !_loaded || _projection is null || TerrainMeshes is null ||
            (!UseD3D11Map&&TerrainSurface is null) || _playerForceId is not int player)
            throw new InvalidOperationException("Resident playable map is not ready for a world update.");
        MapWorldProjectionPolicy policy = _projection.Policy;
        if (world.MapWidth != policy.Width || world.MapHeight != policy.Height ||
            !world.MapSha256.Equals(policy.MapSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Updated playable world and displayed source map differ.");
        int selectedHex = _mapSelection.Current?.HexId ?? -1;
        if(UseD3D11Map)
        {
            _logicalWorld=world;
            _d3d11Markers!.RefreshWorld(world,ForceMarkerColors);
            RefreshD3D11MapLocationSelection();
            if(selectedHex>=0)
            {
                var point=_projection.Project(selectedHex);
                ShowPickedCell(selectedHex,new((float)point.X,
                    MapGrounding.SampleHeight(TerrainMeshes,point.X,point.Z),(float)point.Z));
            }
            SetMeta("world_period_number",world.PeriodNumber);
            // Apply the selected season's actual V410 optics before capturing
            // the same World for native Main; the controller is not advanced.
            PublishSourceCamera();
            return;
        }
        PlayableTerrainOverlay nextOverlay = PlayableTerrainOverlay.Build(TerrainSurface!,
            _projection, world, ForceMapColors);
        AnnotateColorSource(nextOverlay, world);
        PlayableTerrainOverlay? previousOverlay = _territoryOverlay;
        _territoryOverlay = nextOverlay;
        _logicalWorld = world;
        UpdateTerrainMaterial();
        _terrain.SetMeta("playable_territory_overlay", nextOverlay.Material);
        previousOverlay?.Dispose();

        if (_unitLayer is not null)
        {
            _previewWorld.RemoveChild(_unitLayer);
            _unitLayer.Free();
            _unitLayer = null;
        }
        if (_structureLayer is not null)
        {
            _previewWorld.RemoveChild(_structureLayer);
            _structureLayer.Free();
            _structureLayer = null;
        }
        if (_locations is not null)
        {
            _previewWorld.RemoveChild(_locations);
            _locations.Free();
            _locations = null;
        }
        _locations = new MapLocationLayer { Visible = _showLocations.ButtonPressed };
        _previewWorld.AddChild(_locations);
        _locations.Build(_projection, TerrainMeshes, world, _markerAssets
            ?? throw new InvalidOperationException("Original map marker assets are unavailable."),
            _markerLayout ?? throw new InvalidOperationException("Original map marker layout is unavailable."),
            ForceMarkerColors, ResolveCityModelDisplays(world),
            _sceneModels?.ResolveGates(world));
        _locations.SetViewportHeight(_surface.Size.Y);
        _unitLayer = new MapUnitLayer();
        _previewWorld.AddChild(_unitLayer);
        _unitLayer.Build(_projection, TerrainMeshes, world, player,
            _markerAssets ?? throw new InvalidOperationException("Original map marker assets are unavailable."),
            _unitMarkerLayout ?? throw new InvalidOperationException("Original unit sign layout is unavailable."),
            ForceMarkerColors);
        _unitLayer.SetViewportHeight(_surface.Size.Y);
        RefreshArmyVisuals(world);
        _structureLayer = new PlayableMapStructureLayer();
        _previewWorld.AddChild(_structureLayer);
        _structureLayer.Build(_projection, TerrainMeshes, world,
            ForceMarkerColors, StructureNames,
            _sceneModels?.ResolveFacilities(world), _text.Get("detail_unknown"));
        _structureLayer.SetViewportHeight(_surface.Size.Y);
        _locationSelection.Clear();
        foreach (ProjectedMapLocation location in _locations.Locations)
            _locationSelection.AddItem(location.Name);
        bool available = _locations.Locations.Count > 0;
        _locationSelection.Disabled = !available;
        _focusLocation.Disabled = !available;
        _showLocations.Disabled = !available;
        if (selectedHex >= 0)
        {
            ProjectedMapPoint point = _projection.Project(selectedHex);
            float ground = MapGrounding.SampleHeight(TerrainMeshes, (float)point.X, (float)point.Z);
            ShowPickedCell(selectedHex, new Vector3((float)point.X, ground, (float)point.Z));
        }
        SetMeta("playable_unit_marker_count", _unitLayer.Units.Count);
        SetMeta("playable_structure_marker_count", _structureLayer.Count);
        SetMeta("world_period_number", world.PeriodNumber);
        ScheduleNativeMapFrame();
    }

    private IReadOnlyDictionary<int, CityModelDisplay>? ResolveCityModelDisplays(GameState world)
    {
        CityVisualPlan? plan = _sceneModels?.ResolveCities(world, _d3d11CurrentWorldQueries);
        if (plan is null) return null;
        SetMeta("city_visual_unresolved_count", plan.Unresolved.Count);
        SetMeta("city_visual_retained_count", plan.Unresolved.Count(row => row.KeptPreviousModel));
        SetMeta("city_visual_unresolved_slots", plan.Unresolved.Select(row => row.CityId).ToArray());
        return plan.Displays.Count == 0 ? null : plan.Displays;
    }

    private void FocusLocation()
    {
        if(_d3d11Markers is not null)
        {
            if(_locationSelection.Selected<0)return;
            var location=_d3d11Markers.Locations[_locationSelection.Selected];
            _d3d11Markers.Select(_locationSelection.Selected);
            ShowPickedCell(location.HexId,location.Position);
            FocusSourceCamera(location.HexId);
            return;
        }
        if (_locations is null || _locationSelection.Selected < 0) return;
        ProjectedMapLocation selected = _locations.Select(_locationSelection.Selected);
        ShowPickedCell(selected.HexId, selected.Position);
        if(FocusSourceCamera(selected.HexId))return;
        _center = selected.Position;
        _zoom = _embedded ? PlayableZoomSteps[_playableZoomIndex = 3] : 0.06f;
        UpdateCamera();
    }

    private void SelectLocation(int index)
    {
        if(_d3d11Markers is not null)
        {
            _d3d11Markers.Select(index);
            var nativeLocation=_d3d11Markers.Locations[index];
            _focusLocation.Disabled=false;
            ShowPickedCell(nativeLocation.HexId,nativeLocation.Position);
            return;
        }
        if (_locations is null) return;
        ProjectedMapLocation location = _locations.Select(index);
        _focusLocation.Disabled = false;
        ShowPickedCell(location.HexId, location.Position);
    }

    private void ClearPickedCell()
    {
        CloseCityCommandMenu();
        _mapSelection.Clear();
        _openSelectedCity.Disabled = true;
        if (_mapCityInformation is not null) _mapCityInformation.Visible = false;
        if (_mapCityOrders is not null) _mapCityOrders.Visible = false;
        if (_mapAreaOrders is not null) _mapAreaOrders.Visible = false;
        if (_mapUnitOrders is not null) _mapUnitOrders.Visible = false;
        if (_mapNextUnit is not null) _mapNextUnit.Visible = false;
        _useSelectedHex.Disabled = true;
        _unitLayer?.SelectAtHex(-1, _playerForceId);
        RemoveMeta("selected_city_id");
        RemoveMeta("selected_area_id");
        RemoveMeta("selected_unit_id");
        _cellInfo.Text = _text.Get("scene_preview_cell_none");
        foreach (string key in new[] { "picked_hex_id", "picked_world_position", "picked_patch_index", "picked_triangle_index" })
            RemoveMeta(key);
        if (_selectionMarker is not null) _selectionMarker.Visible = false;
    }

    private void ShowPickedCell(int hexId, Vector3 position)
    {
        GameState world = _logicalWorld ?? throw new InvalidOperationException("No logical world for map selection.");
        MapCellSelection selection = _mapSelection.Select(world, hexId, _playerForceId);
        string area = selection.AreaName ?? _text.Get("scene_preview_no_location");
        string force = selection.ForceName ?? _text.Get("neutral");
        string locations = string.Join(_text.Get("list_separator"), selection.LocationNames);
        if (locations.Length == 0) locations = _text.Get("scene_preview_no_location");
        _cellInfo.Text = string.Format(CultureInfo.InvariantCulture, _text.Get("scene_preview_cell_info"), hexId, area, force, locations);
        if (selection.Structure is { } building)
            _cellInfo.Text += "\n" + _text.Format("play_map_structure_here",
                StructureNames is not null && StructureNames.TryGetValue(building.KindId, out string? knownName)
                    ? knownName : building.KindId,
                building.Durability, building.MaxDurability?.ToString(CultureInfo.InvariantCulture) ?? _text.Get("detail_unknown"));
        if (selection.Units.Count > 0)
            _cellInfo.Text += "\n" + _text.Format("play_map_units_here", string.Join(_text.Get("list_separator"),
                selection.Units.Select(unit => _text.Format("play_map_unit_here", unit.CommanderName, unit.Troops))));
        _openSelectedCity.Disabled = selection.CityDetails is null || _openCityDetails is null;
        if (_mapCityInformation is not null)
        {
            _mapCityInformation.Visible = selection.CityDetails is not null;
            if (selection.CityDetails is { } city)
                _mapCityInformation.Text = _text.Format("play_city_information_item", city.Name);
        }
        if (_mapCityOrders is not null)
        {
            _mapCityOrders.Visible = selection.LocationCommand is not null;
            if (selection.LocationCommand is { } location)
                _mapCityOrders.Text = _text.Format("play_city_orders_item", location.Name);
        }
        if (selection.CityDetails is { } selectedCity) SetMeta("selected_city_id", selectedCity.Id);
        else RemoveMeta("selected_city_id");
        if (_mapAreaOrders is not null)
        {
            _mapAreaOrders.Visible = selection.AreaCommand is not null;
            if (selection.AreaCommand is { } commandArea)
                _mapAreaOrders.Text = _text.Format("play_area_orders_item", commandArea.Name);
        }
        if (selection.AreaCommand is { } selectedArea) SetMeta("selected_area_id", selectedArea.Id);
        else RemoveMeta("selected_area_id");
        if (_mapNextUnit is not null) _mapNextUnit.Visible = selection.PlayerUnitCount > 1;
        if (_mapUnitOrders is not null)
        {
            _mapUnitOrders.Visible = selection.SelectedUnit is not null;
            if (selection.SelectedUnit is { } selectedUnit)
                _mapUnitOrders.Text = _text.Format("play_unit_orders_item", selectedUnit.CommanderName);
        }
        if (selection.SelectedUnit is { } chosenUnit) SetMeta("selected_unit_id", chosenUnit.Id);
        else RemoveMeta("selected_unit_id");
        SetMeta("picked_hex_id", hexId); SetMeta("picked_world_position", position);
        _useSelectedHex.Disabled = _acceptHex is null;
        _unitLayer?.SelectUnit(selection.SelectedUnit?.Id);
        RemoveMeta("picked_patch_index"); RemoveMeta("picked_triangle_index");
        if (_selectionMarker is null)
        {
            _selectionMarkerMesh = new SphereMesh { Radius = 70, Height = 140, RadialSegments = 12, Rings = 6 };
            _selectionMarkerMaterial = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color("#ffd66b")
            };
            _selectionMarker = new MeshInstance3D
            {
                Name = "SelectedTerrainPoint", Mesh = _selectionMarkerMesh, MaterialOverride = _selectionMarkerMaterial
            };
            _previewWorld.AddChild(_selectionMarker);
        }
        _selectionMarker.Position = position + new Vector3(0, 70, 0);
        _selectionMarker.Visible = _terrain.Visible;
    }

    private void SelectNextUnitAtPickedHex()
    {
        if (_logicalWorld is not GameState world || _mapSelection.CycleUnit(world, _playerForceId) is not { } next)
            return;
        SetMeta("selected_unit_id", next.Id);
        if (_mapUnitOrders is not null)
            _mapUnitOrders.Text = _text.Format("play_unit_orders_item", next.CommanderName);
        _unitLayer?.SelectUnit(next.Id);
    }

    internal void SetDetailsOpen(bool open, bool keepMapVisible = false)
    {
        if (open) ResetSourceCameraInput();
        if (open) _sceneryMap?.Suspend();
        if (open) { CloseCityCommandMenu(); _d3d11Markers?.ClearPointer(); }
        _detailsOpen = open;
        _dragging = false; _dragDistance = 0;
        SetMeta("city_details_open", open);
        SubViewport viewport = (SubViewport)_surface.GetChild(0);
        viewport.RenderTargetUpdateMode = open || UseD3D11Map ?
            SubViewport.UpdateMode.Disabled : SubViewport.UpdateMode.Always;
        Visible = !open || keepMapVisible;
        MapOverlayChanged?.Invoke(open);
        if (open) { _status.Text = _sceneSummary; RemoveMeta("city_details_error"); }
        else
        {
            UpdateCamera();
            if (!_openSelectedCity.Disabled) _openSelectedCity.GrabFocus();
            else _locationSelection.GrabFocus();
        }
    }

    internal void ReportCityDetailsError(Exception error)
    {
        SetMeta("city_details_error", error.Message);
        string heading = _safeText?.Get("scene_preview_city_failed") ?? error.GetType().Name;
        _status.Text = heading + "\n" + error.Message;
        GD.PushError(error.ToString());
    }

    private void PickTerrain(Vector2 surfacePosition)
    {
        if (!_loaded || _projection is null || CurrentTerrainPicker is null || !_terrain.Visible ||
            _surface.Size.X <= 0 || _surface.Size.Y <= 0) return;
        SubViewport viewport = (SubViewport)_surface.GetChild(0);
        // GUI events use container pixels; camera projection uses the actual SubViewport size.
        Vector2 point = surfacePosition * new Vector2(viewport.Size.X / _surface.Size.X, viewport.Size.Y / _surface.Size.Y);
        bool inside = surfacePosition.X >= 0 && surfacePosition.Y >= 0 && surfacePosition.X < _surface.Size.X && surfacePosition.Y < _surface.Size.Y;
        if (inside && D3D11MapInteractive && _d3d11PresentedInput?.SourceFrame is { } armyFrame &&
            _d3d11Markers?.TryPickArmy(surfacePosition, armyFrame, out var armyHit) == true)
        {
            if(!sourceCameraShiftHeld&&TrySelectCommandArmy(armyHit.UnitId))return;
            if(PublishCommandTarget(armyHit.HexId))return;
            ShowPickedCell(armyHit.HexId, new(armyHit.Position.X, armyHit.Position.Y, armyHit.Position.Z));
            //Source hit preserves root identity; the existing playable command
            //UI selects that live player unit. Full original dispatch is separate.
            if (_mapSelection.SelectUnit(_logicalWorld!, _playerForceId, armyHit.UnitId) is { } army)
            {
                SetMeta("selected_unit_id", army.Id);
                if (_mapUnitOrders is not null)
                {
                    _mapUnitOrders.Visible = true;
                    _mapUnitOrders.Text = _text.Format("play_unit_orders_item", army.CommanderName);
                }
                _unitLayer?.SelectUnit(army.Id);
            }
            _locationSelection.Select(-1); _focusLocation.Disabled = true;
            _d3d11Markers.ClearSelection();
            _acceptHex?.Invoke(armyHit.HexId);
            return;
        }
        if(inside&&D3D11MapInteractive&&_d3d11PresentedInput?.SourceFrame is { } displayed&&
            _d3d11Markers is not null&&_d3d11Markers.TryPickMarker(surfacePosition,displayed,out int markerHex))
        {
            if(PublishCommandTarget(markerHex))return;
            var markerPoint=_projection.Project(markerHex);
            ShowPickedCell(markerHex,new((float)markerPoint.X,
                MapGrounding.SampleHeight(TerrainMeshes!,markerPoint.X,markerPoint.Z),(float)markerPoint.Z));
            int markerIndex=Enumerable.Range(0,_d3d11Markers.Locations.Count)
                .FirstOrDefault(i=>_d3d11Markers.Locations[i].HexId==markerHex,-1);
            _locationSelection.Select(markerIndex);_focusLocation.Disabled=markerIndex<0;
            if(markerIndex>=0)_d3d11Markers.Select(markerIndex);else _d3d11Markers.ClearSelection();
            _acceptHex?.Invoke(markerHex);
            OpenPickedCityMenu(surfacePosition);
            return;
        }
        TerrainPick hit;
        int hexId=-1;
        bool picked=_sourceMainCameraFrame is not null?TrySourceCameraHexPick(surfacePosition,out hit,out hexId):
            CurrentTerrainPicker.TryPick(_camera,point,out hit)&&_projection.TryPick(hit.Position.X,hit.Position.Z,out hexId);
        if (!inside || !picked)
        {
            if(_commandTargetPicked is not null)return;
            ClearPickedCell();
            _locations?.ClearSelection(); _locationSelection.Select(-1); _focusLocation.Disabled = true;
            _d3d11Markers?.ClearSelection();
            return;
        }
        if(PublishCommandTarget(hexId))return;
        ShowPickedCell(hexId, hit.Position);
        SetMeta("picked_patch_index", hit.PatchIndex); SetMeta("picked_triangle_index", hit.TriangleIndex);
        int selected = -1;
        if(_d3d11Markers is not null)
        {
            for(int i=0;i<_d3d11Markers.Locations.Count;i++)
                if(_d3d11Markers.Locations[i].HexId==hexId){selected=i;break;}
        }
        else if (_locations is not null)
            for (int i = 0; i < _locations.Locations.Count; i++)
                if (_locations.Locations[i].HexId == hexId) { selected = i; break; }
        _locationSelection.Select(selected); _focusLocation.Disabled = selected < 0;
        if(_d3d11Markers is not null)
        {
            if(selected>=0)_d3d11Markers.Select(selected);else _d3d11Markers.ClearSelection();
        }
        else if (selected >= 0) _locations!.Select(selected);
        else _locations?.ClearSelection();
        if (_embedded) _acceptHex?.Invoke(hexId);
        OpenPickedCityMenu(surfacePosition);
    }

    private void UpdateCamera()
    {
        if (!_loaded) return;
        if(PublishSourceCamera())return;
        float aspect = Math.Max(_surface.Size.X, 1) / Math.Max(_surface.Size.Y, 1);
        float halfFov = Mathf.DegToRad(_camera.Fov * 0.5f);
        float minHalfFov = Math.Min(halfFov, MathF.Atan(MathF.Tan(halfFov) * aspect));
        float distance = _radius / MathF.Sin(minHalfFov) * 1.15f * _zoom;
        var direction = new Vector3(MathF.Cos(_pitch) * MathF.Sin(_yaw), MathF.Sin(_pitch), MathF.Cos(_pitch) * MathF.Cos(_yaw));
        Vector3 position = _center + direction * distance;
        float far = distance + _radius * 4;
        if (!float.IsFinite(distance) || !float.IsFinite(far) || !position.IsFinite())
            throw new InvalidDataException("Scene camera framing exceeds finite preview coordinates.");
        _camera.Near = Math.Max(_radius * 0.0001f, 0.01f);
        _camera.Far = far;
        // A vertical view has no well-defined LookAt basis with world Up.
        // Preserve the limiting north-up orientation, including yaw rotation.
        Vector3 cameraUp = MathF.Abs(MathF.Cos(_pitch)) < 0.0001f
            ? new Vector3(-MathF.Sign(MathF.Sin(_pitch)) * MathF.Sin(_yaw), 0,
                -MathF.Sign(MathF.Sin(_pitch)) * MathF.Cos(_yaw))
            : Vector3.Up;
        _camera.LookAtFromPosition(position, _center, cameraUp);
        UpdateTerrainLod(position, aspect);
        _sceneTerrain?.UpdateEye(position, _territoryOverlay?.Material);
        _camera.SetMeta("framed_source_bounds", GetMeta("source_space_bounds"));
        _locations?.SetViewportHeight(_surface.Size.Y);
        _unitLayer?.SetViewportHeight(_surface.Size.Y);
        _structureLayer?.SetViewportHeight(_surface.Size.Y);
        if (_embedded)
        {
            SetMeta("playable_zoom_level", _playableZoomIndex);
            SetMeta("playable_zoom_factor", _zoom);
            SetMeta("playable_camera_calibration", "original-B-A-three-city-marker-fit; C-D-relative-terrain-scale");
        }
    }

    private void UpdateTerrainLod(Vector3 eye, float aspect,System.Numerics.Matrix4x4? sourceClip=null)
    {
        if (_sceneTerrain?.HasAdaptiveGeometry != true) return;
        if (_camera.Projection != Camera3D.ProjectionType.Perspective || _camera.KeepAspect != Camera3D.KeepAspectEnum.Height)
            throw new InvalidDataException("Playable original LOD adapter requires explicit vertical-FOV perspective.");
        System.Numerics.Matrix4x4 clip;
        if(sourceClip is { } currentSourceClip)clip=currentSourceClip;
        else
        {
            Transform3D inverse = _camera.GetCameraTransform().AffineInverse();
            Basis b = inverse.Basis;
            Vector3 t = inverse.Origin;
            // Godot Transform3D is column-vector affine; transpose to the original
            // Camera row-vector RH convention before its paired matrix product.
            var view = new System.Numerics.Matrix4x4(b.X.X,b.X.Y,b.X.Z,0, b.Y.X,b.Y.Y,b.Y.Z,0,
                b.Z.X,b.Z.Y,b.Z.Z,0, t.X,t.Y,t.Z,1);
            var projection = TerrainFrustumMath.Perspective(Mathf.DegToRad(_camera.Fov), aspect, _camera.Near, _camera.Far);
            clip=TerrainFrustumMath.Multiply(view, projection);
        }
        TerrainLodUpdate update = _sceneTerrain.PrepareLod(eye, clip);
        TerrainLodDrawPlan plan = update.Plan;
        if (update.Patches is { } next)
        {
            foreach (Node old in _terrain.GetChildren()) { _terrain.RemoveChild(old); old.Free(); }
            if(!UseD3D11Map)foreach (TerrainPatch patch in next)
            {
                var node = new MeshInstance3D { Name = $"TerrainPatch_{patch.SourceNode.Index}", Mesh = patch.Mesh,
                    MaterialOverride = _territoryOverlay?.Material ??
                        (_terrainTextureToggle.ButtonPressed ? TerrainSurface!.Material : TerrainMeshes!.PreviewMaterial) };
                node.SetMeta("source_tree_node", patch.SourceNode.Index);
                _terrain.AddChild(node);
            }
            _sceneTerrain.CommitLod(update);
        }
        if(!UseD3D11Map)for (int i = 0; i < plan.Rows.Count; i++)
            _terrain.GetChild(i).SetMeta("source_draw_group", plan.Rows[i].Group);
        _terrain.SetMeta("patch_count", _sceneTerrain!.ActivePatches.Count);
        _terrain.SetMeta("lod_selected_count", plan.SelectedCount);
        _terrain.SetMeta("lod_culled_count", plan.CulledCount);
        _terrain.SetMeta("lod_distance_scale", _sceneTerrain.LodDistanceScale);
        _terrain.SetMeta("lod_source_eye", eye);
        _terrain.SetMeta("lod_clip_matrix_rows", new float[] {clip.M11,clip.M12,clip.M13,clip.M14,clip.M21,clip.M22,clip.M23,clip.M24,
            clip.M31,clip.M32,clip.M33,clip.M34,clip.M41,clip.M42,clip.M43,clip.M44});
        _terrain.SetMeta("original_lod_algorithm_implemented", true);
        _terrain.SetMeta("original_camera_values_observed", false);
        _terrain.SetMeta("source_main_camera_matrix_owned",sourceClip.HasValue);
        _terrain.SetMeta("lod_equal_sort_key_policy", "replica node index; native equal-key order unobserved; ordinary non-tessellated leaf groups share surface");
        _terrain.SetMeta("lod_geometry_cache_count", _sceneTerrain.CachedPatchCount);
        SetMeta("terrain_patch_count", _sceneTerrain!.ActivePatches.Count);
    }

    private bool TryScreenPlane(Vector2 screenPosition, out Vector3 point)
    {
        point = default;
        if (_surface.Size.X <= 0 || _surface.Size.Y <= 0) return false;
        SubViewport viewport = (SubViewport)_surface.GetChild(0);
        Vector2 native = screenPosition * new Vector2(viewport.Size.X / _surface.Size.X,
            viewport.Size.Y / _surface.Size.Y);
        Vector3 origin = _camera.ProjectRayOrigin(native);
        Vector3 direction = _camera.ProjectRayNormal(native);
        if (Math.Abs(direction.Y) < 0.0001f) return false;
        float travel = (_center.Y - origin.Y) / direction.Y;
        if (!float.IsFinite(travel) || travel <= 0) return false;
        point = origin + direction * travel;
        return point.IsFinite();
    }

    private void OnInput(InputEvent input)
    {
        if(_detailsOpen)return;
        if(UseD3D11Map && !D3D11MapInteractive)return;
        if(_periodMapActive){HandleSourceCameraInput(input);return;}
        if (IsCityCommandMenuOpen && input is InputEventMouseButton { Pressed: true })
        {
            CloseCityCommandMenu();
            return;
        }
        UpdateMarkerPointer(input);
        UpdateAdministrationMapHover(input);
        if(HandleSourceCameraInput(input))return;
        if (input is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Left)
            {
                if (_embedded && button.Pressed && button.DoubleClick && _commandTargetPicked is null&&_openCityDetails is not null)
                {
                    _dragging = false;
                    PickTerrain(button.Position);
                    if (_mapSelection.Current?.CityDetails?.Id is int cityId) _openCityDetails(cityId);
                    return;
                }
                if (button.Pressed)
                {
                    _dragging=true;_pressPosition=button.Position;_dragDistance=0;
                    _commandTargetPressAllowed=CommandTargetPointerDown(button.Position);
                }
                else
                {
                    bool click = _dragging && _dragDistance <= 4 && _pressPosition.DistanceTo(button.Position) <= 4;
                    _dragging = false;
                    if(click&&(_commandTargetPicked is null||_commandTargetPressAllowed))PickTerrain(button.Position);
                    _commandTargetPressAllowed=false;
                }
            }
            if (_embedded && button.ButtonIndex is MouseButton.Right or MouseButton.Middle)
            {
                _panning = button.Pressed;
                _pressPosition = button.Position;
            }
            if (button.Pressed && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                if (_dragging) _dragDistance = Math.Max(_dragDistance, 5);
                if (_embedded)
                {
                    _playableZoomIndex = Math.Clamp(_playableZoomIndex +
                        (button.ButtonIndex == MouseButton.WheelUp ? -1 : 1),
                        0, PlayableZoomSteps.Length - 1);
                    _zoom = PlayableZoomSteps[_playableZoomIndex];
                }
                else
                    _zoom = Math.Clamp(_zoom * (button.ButtonIndex == MouseButton.WheelUp ? 0.85f : 1.15f), 0.01f, 20);
                UpdateCamera();
            }
        }
        if (_dragging && input is InputEventMouseMotion motion)
        {
            _dragDistance += motion.Relative.Length();
            if (_dragDistance <= 4) return;
            _yaw -= motion.Relative.X * 0.005f;
            _pitch = Math.Clamp(_pitch + motion.Relative.Y * 0.005f,
                -MathF.PI * 0.5f, MathF.PI * 0.5f);
            UpdateCamera();
        }
        if (_panning && input is InputEventMouseMotion pan &&
            TryScreenPlane(pan.Position - pan.Relative, out Vector3 previous) &&
            TryScreenPlane(pan.Position, out Vector3 current))
        {
            Vector3 delta = previous - current;
            if (!delta.IsFinite()) return;
            _center.X += delta.X;
            _center.Z += delta.Z;
            if (TerrainMeshes is not null)
            {
                var height = TerrainMeshes.Source.HeightField.HeightField;
                _center.X = Math.Clamp(_center.X, 0, height.CellWidth * height.SampleSpacing);
                _center.Z = Math.Clamp(_center.Z, 0, height.CellHeight * height.SampleSpacing);
                _center.Y = MapGrounding.SampleHeight(TerrainMeshes, _center.X, _center.Z);
            }
            UpdateCamera();
        }
    }

    public override void _Input(InputEvent input)
    {
        if (HandleSourceNavigationKey(input)) { GetViewport().SetInputAsHandled(); return; }
        if (_embedded || _acceptHex is null || _detailsOpen || !IsVisibleInTree() || input is not InputEventMouseButton
            { ButtonIndex: MouseButton.Right, Pressed: true }) return;
        GetViewport().SetInputAsHandled();
        Run(() => _closeScene?.Invoke());
    }

    public override void _ExitTree()
    {
        ReleaseSceneryMap();
        UnbindSceneryMapPresentation();
        ReleaseSourceNavigationInput();
        _sceneLoadGeneration++;
        ReleaseNativeMap();
        ReleaseArmyVisuals();
        _sceneModels?.ReleaseCityDisplays();
        ReleaseTerrainGeometry();
        _selectionMarkerMesh?.Dispose(); _selectionMarkerMesh = null;
        _selectionMarkerMaterial?.Dispose(); _selectionMarkerMaterial = null;
        _territoryOverlay?.Dispose();
        _territoryOverlay = null;
        _sceneTerrain?.Dispose();
        _sceneTerrain = null;
        _sceneModels?.Dispose();
        _sceneModels = null;
        _markerAssets?.Dispose();
        _markerAssets = null;
        _markerLayout = null;
        _unitMarkerLayout = null;
        _modelMeshes.Dispose();
    }
}
