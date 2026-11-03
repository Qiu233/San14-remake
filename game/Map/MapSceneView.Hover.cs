using Godot;
using San14.Data;

public partial class MapSceneView
{
    private readonly MapHoverSelection _mapHover=new();
    private bool _mapPointerBlocked;
    private uint? _primaryHexFocus;

    private uint? CapturePrimaryHexFocus()
    {
        // This restores ordinary selector0 only. Specialized command/representative
        // overrides retain their own source contract; no target is invented here.
        if (_detailsOpen || _periodMapActive || !IsVisibleInTree() || _mapPointerBlocked ||
            !MapPointerOverSurface() || !ReferenceEquals(_summaryInputWorld, _logicalWorld) ||
            _summaryMode != MapSummarySelectionMode.Normal || _summaryGeographySelected ||
            _commandTargetPicked is not null || _summaryCanQuery?.Invoke() != true)
            return D3D11CommandMapData.CellCount;
        return _primaryHexFocus; // Unknown retains the native channel; 48400 clears it.
    }

    // The GUI's actual hit owner decides whether this point belongs to map
    // input. Map labels remain map targets; HUD and command panels do not.
    private bool MapPointerOverSurface()
    {
        Control? hovered = GetViewport().GuiGetHoveredControl();
        if (hovered is null) return false;
        return Contains(_surface) || Contains(_d3d11Map) ||
            Contains(_nativeMapOwner?.Package.Surface) || Contains(_sceneryMap) || Contains(_d3d11Markers);

        bool Contains(Control? owner) => owner is not null &&
            (ReferenceEquals(hovered, owner) || owner.IsAncestorOf(hovered));
    }

    private SceneryMapHexSelection ReadMapHover(San14.Core.GameState actualWorld,SourceWorldContext current)
    {
        // An in-flight presentation carries its own World, not the latest logical tag.
        if(!ReferenceEquals(actualWorld,_logicalWorld)||_detailsOpen||!IsVisibleInTree())return new(false,null);
        bool? frozen=_sourceMainCamera is null?null:_sourceCameraActiveButton==-1?false:
            _sourceCameraCurrentCursorKnown?SourceDragThreshold(_sourceCameraCurrentCursor):null;
        bool blocked = !MapPointerOverSurface();
        if (blocked && !_mapPointerBlocked) _d3d11Markers?.ClearPointer();
        _mapPointerBlocked = blocked;
        var selection = blocked ? _mapHover.Clear() :
            _mapHover.Update(current,frozen,_sceneryMap?.Controller.AuxiliaryHexLookup,
                ReadSourceHoverRay,AllowsMapHover);
        _primaryHexFocus = selection.Known
            ? (uint)(selection.Hex?.PhysicalSlot ?? D3D11CommandMapData.CellCount) : null;
        UpdateSceneryMapSummary(current,selection);
        if (selection.Known)
        {
            if (selection.Hex?.PhysicalSlot is int hex) _commandTargetHoverHex = hex;
            _commandTargetHovered?.Invoke(selection.Hex?.PhysicalSlot);
        }
        return selection;
    }

    private bool? AllowsMapHover(HexReference? candidate)
    {
        bool? scene=_mapPresentation?.SelectionPolicy.Allows(candidate);
        if(scene!=true)return scene;
        // Source Scene eligibility precedes the provider; this project's
        // additional command-candidate filter does not constrain territory.
        HoverDiplomacyTerritory(candidate?.PhysicalSlot);
        HoverStrategicTargetTerritory(candidate?.PhysicalSlot);
        // This project's active command owns its additional eligibility test.
        // A missing AllowPointer rejects command targets; it is not a native
        // known-absent Scene filter and cannot be bypassed through a label.
        if(_commandTargetPicked is not null)
            return candidate?.PhysicalSlot is int physical&&_commandTargetPointerAllowed?.Invoke(physical)==true;
        return true;
    }

    private MapHoverRayResult ReadSourceHoverRay()
    {
        if(!_sourceCameraCurrentCursorKnown||_projection?.Policy.SourceFormulaId!=CeHexWorldCoordinates.FormulaId)
            return new(MapHoverRayStatus.Unavailable);
        var status=ReadSourceCameraRay(_sourceCameraCurrentCursor,out var camera,out var picker,out var ray);
        if(status!=MapHoverRayStatus.Ready)return new(status);
        if(_d3d11Markers is {} markers&&ReferenceEquals(camera,_d3d11PresentedInput?.SourceFrame))
        {
            // County names share the source hit pane with their flag. Resolve
            // that accepted UI control before the terrain ray, just as the
            // original label writes its representative Hex to Map+2F4B8.
            var point=new Vector2(_sourceCameraCurrentCursor.X34*markers.Size.X/1920f,
                _sourceCameraCurrentCursor.Y38*markers.Size.Y/1080f);
            if(markers.TryReadHoverPointer(point,camera!,out int? markerHex))
                return new(MapHoverRayStatus.Ready,markerHex);
        }
        var o=ray.Origin;var d=ray.Direction;
        if(!picker!.TryPick(new(o.X,o.Y,o.Z),new(d.X,d.Y,d.Z),camera!.Camera.Lens.Far,out var hit))
            return new(MapHoverRayStatus.Ready);
        return CeHexWorldCoordinates.TryPickTerrainHit(new(o.X,o.Y,o.Z),new(d.X,d.Y,d.Z),
            new(hit.Position.X,hit.Position.Y,hit.Position.Z),out int slot)
            ?new(MapHoverRayStatus.Ready,slot):new(MapHoverRayStatus.Ready);
    }

    private void CaptureSourceHoverPointer(InputEvent input,MainMapCameraCanonicalPoint cursor)
    {
        if(!_embedded||_detailsOpen||_sourceMainCamera is null)return;
        _sourceCameraCurrentCursor=cursor;_sourceCameraCurrentCursorKnown=true;
        // WindowInput precedes GUI consumption. It observes movement and
        // releases, including release over HUD, but cannot arm a map drag.
        // Only Surface.GuiInput admits a new press to the camera owner.
        if(input is InputEventMouse mouse)
        {sourceCameraShiftHeld=mouse.ShiftPressed;sourceCameraControlHeld=mouse.CtrlPressed;}
        if(input is not InputEventMouseButton button)return;
        if (!button.Pressed) SetSourceCameraLogicalButton(button.ButtonIndex, false);
    }
}
