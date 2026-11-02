using Godot;
using San14.Data;

public partial class MapSceneView
{
    private D3D11CommandMapPublication? _commandMapDisplay;
    private ulong _commandMapRevision, _commandMapProviderGeneration;
    private bool _administrationCameraLimited, _administrationPresentationActive;
    private AreaAdministrationMapLayer? _administrationMapLayer;
    private D3D11SceneryMapLabels? _administrationSceneryLabels;

    internal bool BeginAdministrationMapDisplay(AreaAdministrationDraft draft, int cityHex,
        GameResourceRepository repository, AdministrationPageChrome.Resources resources, Action<string> unavailable)
    {
        if (!IsPlayableMapReady || _commandMapDisplay is { Present: true })
            throw new InvalidOperationException("Command map display requires an available map owner.");
        var presentation = _mapPresentation ?? throw new InvalidOperationException("area-map.presentation_owner");
        var camera = _sourceMainCamera ?? throw new InvalidOperationException("area-map.camera_owner");
        var input = _sourceMainCameraInputs ?? throw new InvalidOperationException("area-map.camera_inputs");
        var scenery = _sceneryMap?.Controller ?? throw new InvalidOperationException("area-map.scenery_owner");
        var controls = draft.QueryMapSceneryControls(scenery.PhysicalReceivers);
        if (!controls.Available)
        {
            unavailable(controls.UnresolvedInput ?? "area-map.scenery_qualification");
            return false;
        }
        var data = D3D11CommandMapData.Capture(draft);
        int band = camera.CurrentZoom.Band1E0;
        if (band < 3 || band >= 5)
        {
            var focus = CeHexWorldCoordinates.Project(cityHex);
            var request = new MainMapCameraControllerRequest(500,
                new((float)focus.X, 0, (float)focus.Z, 1),
                camera.Parameters.ZoomAnchor(band < 3 ? 3 : 4), 62.83185308, -1, 1);
            var prepared = MapCameraController.MaterializeRequest(camera.CurrentRuntime,
                camera.Parameters, request, input.CurrentMaximumYOverride, SourceCameraTerrainQualifier);
            // Busy rejection follows the existing source camera contract.
            // Entering Domestic never cancels an unrelated timed request.
            camera.QueueTimedRequest(prepared, unchecked((int)Time.GetTicksMsec()));
        }
        camera.SetMaximumZoomBand(4, input.CurrentMaximumYOverride, SourceCameraTerrainQualifier);
        _administrationCameraLimited = true;
        ulong revision = checked(++_commandMapRevision);
        _commandMapDisplay = new(data, revision, revision, checked(++_commandMapProviderGeneration));
        _administrationSceneryLabels = scenery;
        foreach (var row in controls.Value!)
        {
            if (!row.ShouldWrite) continue;
            if (row.Enabled is not bool enabled) throw new InvalidDataException("area-map.scenery_qualification");
            scenery.SetDomesticControl(row.SceneryId, enabled);
        }
        _administrationPresentationActive = true;
        presentation.BeginDomesticMode();
        var layer = new AreaAdministrationMapLayer { Name = "DomesticAreaButtons", ZIndex = 2 };
        _administrationMapLayer = layer;
        StrategicMapOverlayParent().AddChild(layer);
        layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layer.Configure(this, repository, resources, draft, unavailable);
        ScheduleD3D11MapFrame();
        return true;
    }

    internal void EndAdministrationMapDisplay()
    {
        if (_administrationMapLayer is { } layer)
        {
            _administrationMapLayer = null; layer.Retire();
            layer.GetParent()?.RemoveChild(layer); layer.QueueFree();
        }
        if (_administrationCameraLimited)
        {
            _administrationCameraLimited = false;
            if (_sourceMainCamera is { } camera && _sourceMainCameraInputs is { } input)
                camera.SetMaximumZoomBand(6, input.CurrentMaximumYOverride, SourceCameraTerrainQualifier);
        }
        if (_administrationSceneryLabels is { } scenery)
        {
            _administrationSceneryLabels = null;
            if (GodotObject.IsInstanceValid(scenery)) scenery.EndDomesticControls();
        }
        if (_administrationPresentationActive)
        {
            _administrationPresentationActive = false;
            _mapPresentation?.EndDomesticMode();
        }
        if (_commandMapDisplay?.Data is not { Kind: D3D11CommandMapDisplayKind.AreaAdministration }) return;
        // Exiting releases the distance limit; it does not restore a saved pose.
        _commandMapDisplay = new(null, checked(++_commandMapRevision), 0, checked(++_commandMapProviderGeneration));
        ScheduleD3D11MapFrame();
    }

    internal MainMapCameraFrame? DisplayedAdministrationCamera => UseD3D11Map
        ? _d3d11PresentedInput?.SourceFrame : _sourceMainCameraFrame;

    internal bool AdministrationMapPointerAllowed(int hex) =>
        _administrationMapLayer is not null && _commandTargetPicked is not null &&
        _commandTargetPointerAllowed?.Invoke(hex) == true;

    internal void PickAdministrationMapTarget(int hex)
    { if (AdministrationMapPointerAllowed(hex)) _commandTargetPicked?.Invoke(hex); }

    internal void RefreshAdministrationMapInput() => _administrationMapLayer?.RefreshInput();

    internal bool TryProjectAdministrationArea(int hex, MainMapCameraFrame frame, out Vector2 pixel)
    {
        pixel = default;
        if (_sceneTerrain?.WorldGroundHeight is not { } terrain) return false;
        if (!ReferenceEquals(_deploymentTargetAnchors?.Terrain, terrain)) _deploymentTargetAnchors = new(terrain);
        var anchor = _deploymentTargetAnchors.Query(hex);
        if (anchor.GroundWorld is not { } world) return false;
        world.Y += 800;
        if (DeploymentTargetProjection.ProjectCanonical1920(world, frame.Camera.Matrices.WorldToProjection100) is not { } point) return false;
        pixel = new(point.X, point.Y); return true;
    }

    private void UpdateAdministrationMapHover(InputEvent input)
    {
        if (input is not InputEventMouseMotion motion || _commandMapDisplay is not { Data: { } data } display ||
            data.Kind != D3D11CommandMapDisplayKind.AreaAdministration ||
            _dragging || _panning || _sourceCameraLeftUiPressed ||
            _sourceCameraRawNow70[0] || _sourceCameraRawNow70[1] || _sourceCameraRawNow70[2] ||
            !TryPickCommandTarget(motion.Position, out int hex)) return;
        // Invalid/no pointer retains the previous focus. Area0 is a real raw
        // value; neither held membership nor Area lookup participates here.
        ushort previous = display.HoverHex < D3D11CommandMapData.CellCount
            ? data.RawAreas[display.HoverHex] : (ushort)0;
        if (data.RawAreas[hex] == previous) return;
        _commandMapDisplay = new(data, checked(++_commandMapRevision), display.DataRevision,
            display.ProviderGeneration, (uint)hex);
        ScheduleD3D11MapFrame();
    }
}
