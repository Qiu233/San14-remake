using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private MapCityCommandMenu? _cityCommandMenu;
    private Func<GameState,int,int?,AreaAdministrationQuery<CityDomesticMenuAdmission>>? _queryCityDomesticMenu;
    private GameState? _cityDomesticMenuWorld;
    private Func<GameState,int,int?,AreaAdministrationQuery<CityCommandMenuProjection>>? _queryCityCommandMenu;

    internal void SetCityCommandMenuQuery(Func<GameState,int,int?,AreaAdministrationQuery<CityCommandMenuProjection>> query) =>
        _queryCityCommandMenu = query;

    private AreaAdministrationQuery<CityCommandMenuProjection> QueryCityCommandMenu(int city)
    {
        if (_logicalWorld is not { } world || _queryCityCommandMenu is null)
            return new(null,"city-command-menu.current_query");
        var frame = SourceMainCameraFrame;
        int? level = frame is null ? null : MapLabelPresentation.SettledCameraLevel(frame.ZoomSelection);
        return _queryCityCommandMenu(world,city,level);
    }

    internal void SetCityDomesticMenuQuery(Func<GameState,int,int?,AreaAdministrationQuery<CityDomesticMenuAdmission>> query) =>
        _queryCityDomesticMenu = query;

    internal AreaAdministrationQuery<CityDomesticMenuAdmission> QueryCityDomesticMenu(int city)
    {
        if (_logicalWorld is not { } world || _queryCityDomesticMenu is null)
            return new(null,"city-domestic.menu.current_query");
        var frame = SourceMainCameraFrame;
        int? level = frame is null ? null : MapLabelPresentation.SettledCameraLevel(frame.ZoomSelection);
        return _queryCityDomesticMenu(world,city,level);
    }
    internal event Action<int, MapCityCommand>? CityCommand;
    internal event Action<bool>? MapOverlayChanged;
    internal bool IsCityCommandMenuOpen => _cityCommandMenu?.Visible == true;

    internal bool ReopenCityCommandMenu(int cityId)
    {
        if(_logicalWorld?.Cities.GetValueOrDefault(cityId)?.HexId is not int hex||
            _sourceMainCameraTerrain is not { } terrain)return false;
        if(_deploymentTargetAnchors?.Terrain!=terrain)_deploymentTargetAnchors=new(terrain);
        if(_deploymentTargetAnchors.Query(hex).GroundWorld is not { } ground)return false;
        ShowPickedCell(hex,new(ground.X,ground.Y,ground.Z));
        if(_d3d11Markers?.TryGetCityMenuAnchor(cityId,out _)==true)
        {OpenPickedCityMenu(Vector2.Zero);return IsCityCommandMenuOpen;}
        var frame=UseD3D11Map?_d3d11PresentedInput?.SourceFrame:_sourceMainCameraFrame;
        if(frame is null||DeploymentTargetProjection.ProjectCanonical1920(ground,frame.Camera.Matrices.WorldToProjection100) is not { } point)
            return false;
        OpenPickedCityMenu(new Vector2(point.X,point.Y)*_surface.Size/new Vector2(1920,1080));
        return IsCityCommandMenuOpen;
    }

    internal void CloseCityCommandMenu()
    {
        if (!IsCityCommandMenuOpen) return;
        _cityCommandMenu!.Close();
        ReleaseCityCommandMenuOverlay();
    }

    private void ReleaseCityCommandMenuOverlay()
    {
        _cityDomesticMenuWorld = null;
        GetNodeOrNull<Control>("ResetPlayableMapCamera")?.Show();
        _d3d11Markers?.ClearPointer();
        MapOverlayChanged?.Invoke(false);
    }

    private void OpenPickedCityMenu(Vector2 cursor)
    {
        if (!_embedded || _logicalWorld is null || _mapSelection.Current?.CityDetails is not { } city) return;
        if (_cityCommandMenu is null)
        {
            _cityCommandMenu = new() { ZIndex = 2 };
            AddChild(_cityCommandMenu);
            _cityCommandMenu.Configure(_repository, _text);
            _cityCommandMenu.Command += (id, command) => Run(() =>
            {
                bool staleCity = !ReferenceEquals(_cityDomesticMenuWorld,_logicalWorld);
                CloseCityCommandMenu();
                if (staleCity) return;
                if (command == MapCityCommand.CityInformation) _openCityDetails?.Invoke(id);
                else CityCommand?.Invoke(id, command);
            });
        }
        Vector2 anchor = GetGlobalTransform().AffineInverse() * (_surface.GetGlobalTransform() * cursor);
        if (_d3d11Markers?.TryGetCityMenuAnchor(city.Id, out var markerAnchor) == true)
            anchor = GetGlobalTransform().AffineInverse() * (_d3d11Markers.GetGlobalTransform() * markerAnchor);
        var query = QueryCityCommandMenu(city.Id);
        bool wasOpen = IsCityCommandMenuOpen;
        _cityDomesticMenuWorld = _logicalWorld;
        if (!_cityCommandMenu.Open(city.Id, anchor, Size, query))
        {
            _cityDomesticMenuWorld = null;
            if (wasOpen) ReleaseCityCommandMenuOverlay();
            return;
        }
        GetNodeOrNull<Control>("ResetPlayableMapCamera")?.Hide();
        if (_mapCityInformation is not null) _mapCityInformation.Hide();
        if (_mapCityOrders is not null) _mapCityOrders.Hide();
        if (_mapAreaOrders is not null) _mapAreaOrders.Hide();
        if (_mapUnitOrders is not null) _mapUnitOrders.Hide();
        if (_mapNextUnit is not null) _mapNextUnit.Hide();
        ResetSourceCameraInput();
        _d3d11Markers?.ClearPointer();
        MapOverlayChanged?.Invoke(true);
    }

    private void UpdateMarkerPointer(InputEvent input)
    {
        if (_d3d11PresentedInput?.SourceFrame is not { } frame || _d3d11Markers is null) return;
        if (input is InputEventMouseMotion motion)
            _d3d11Markers.UpdatePointer(motion.Position, frame);
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
            _d3d11Markers.UpdatePointer(button.Position, frame, button.Pressed);
    }
}
