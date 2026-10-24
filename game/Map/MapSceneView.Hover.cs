using Godot;
using San14.Data;

public partial class MapSceneView
{
    private readonly MapHoverSelection _mapHover=new();

    private SceneryMapHexSelection ReadMapHover(SourceWorldContext current)
    {
        if(_detailsOpen||!IsVisibleInTree())return new(false,null);
        bool? frozen=_sourceMainCamera is null?null:_sourceCameraActiveButton==-1?false:
            _sourceCameraCurrentCursorKnown?SourceDragThreshold(_sourceCameraCurrentCursor):null;
        var selection = _mapHover.Update(current,frozen,_sceneryMap?.Controller.AuxiliaryHexLookup,
            ReadSourceHoverRay,AllowsMapHover);
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
        // WindowInput precedes GUI consumption. Feed the existing sampled
        // camera owner, so labels and terrain share its one held/drag counter.
        if(input is InputEventMouse mouse)
        {sourceCameraShiftHeld=mouse.ShiftPressed;sourceCameraControlHeld=mouse.CtrlPressed;}
        if(input is not InputEventMouseButton button)return;
        SetSourceCameraLogicalButton(button.ButtonIndex,button.Pressed);
    }
}
