using Godot;
using San14.Data;

public partial class MapSceneView
{
    private Action<int>? _commandTargetPicked;
    private Func<int,bool>? _commandTargetPointerAllowed;
    private Action<int?>? _commandTargetHovered;
    private int? _commandTargetHoverHex;
    private bool _commandTargetPressAllowed;
    private bool _commandTargetShiftWaypoint;
    private DeploymentTargetAnchorSource? _deploymentTargetAnchors;

    internal Vector2? ProjectDeploymentTarget(int hex)
        => ProjectCommandHex(hex, groundMarker: false);

    internal Vector2? ProjectWaypointMarker(int hex)
        => ProjectCommandHex(hex, groundMarker: true);

    private Vector2? ProjectCommandHex(int hex, bool groundMarker)
    {
        var frame=UseD3D11Map?_d3d11PresentedInput?.SourceFrame:_sourceMainCameraFrame;
        if(_sourceMainCameraTerrain is not { } terrain||frame is null)return null;
        if(_deploymentTargetAnchors?.Terrain!=terrain)_deploymentTargetAnchors=new(terrain);
        var anchor = _deploymentTargetAnchors.Query(hex);
        if((groundMarker ? anchor.GroundWorld : anchor.MenuWorld) is not { } world)return null;
        var pixel=DeploymentTargetProjection.ProjectCanonical1920(world,frame.Camera.Matrices.WorldToProjection100);
        return pixel is { } point?new Vector2(point.X,point.Y):null;
    }

    /// <summary>The active command page owns map clicks while camera controls
    /// remain active. Its draft decides hex eligibility and command semantics.</summary>
    internal void SetCommandTargetPicker(Action<int>? picked,Func<int,bool>? pointerAllowed=null,
        Action<int?>? hovered=null,bool supportsShiftWaypoint=false)
    {
        CloseCityCommandMenu();ResetSourceCameraInput();
        _commandTargetPicked=picked;
        _commandTargetPointerAllowed=pointerAllowed;_commandTargetPressAllowed=false;
        _commandTargetHovered=hovered;
        _commandTargetShiftWaypoint=picked is not null&&supportsShiftWaypoint;
        _commandTargetHoverHex=null;
        _d3d11Markers?.ClearPointer();
    }

    // Opening a modal resets camera input. Keep the last admitted map Hex
    // available when its command owner resumes without a new pointer event.
    internal void ReplayCommandTargetHover() => _commandTargetHovered?.Invoke(_commandTargetHoverHex);

    private bool PublishCommandTarget(int hex)
    {
        if(_commandTargetPicked is not { } picked)return false;
        picked(hex);
        return true;
    }

    private bool CommandTargetPointerDown(Vector2 position)
    {
        if(_commandTargetPicked is null)return true;
        return TryPickCommandTarget(position,out int hex)&&_commandTargetPointerAllowed?.Invoke(hex)==true;
    }

    private bool TryPickCommandTarget(Vector2 position,out int hex)
    {
        hex=default;
        if(!_loaded||_projection is null||CurrentTerrainPicker is null||!_terrain.Visible||
            _surface.Size.X<=0||_surface.Size.Y<=0||position.X<0||position.Y<0||
            position.X>=_surface.Size.X||position.Y>=_surface.Size.Y)return false;
        if(D3D11MapInteractive&&_d3d11PresentedInput?.SourceFrame is { } frame&&_d3d11Markers is { } markers)
        {
            if(markers.TryPickArmy(position,frame,out var army)){hex=army.HexId;return true;}
            if(markers.TryPickMarker(position,frame,out hex))return true;
        }
        var viewport=(SubViewport)_surface.GetChild(0);
        var point=position*new Vector2(viewport.Size.X/_surface.Size.X,viewport.Size.Y/_surface.Size.Y);
        TerrainPick hit;
        if(_sourceMainCameraFrame is not null)return TrySourceCameraHexPick(position,out hit,out hex);
        return CurrentTerrainPicker.TryPick(_camera,point,out hit)&&_projection.TryPick(hit.Position.X,hit.Position.Z,out hex);
    }
}
