using Godot;
using San14.Data;

public partial class MapSceneView
{
    internal event Action<BaseKind,int,Vector2,int?>? EnemyFootholdSelected;
    internal int? ReadEnemyFootholdCameraLevel()
    {
        var frame=UseD3D11Map?_d3d11PresentedInput?.SourceFrame:_sourceMainCameraFrame;
        return frame is null?null:MapLabelPresentation.SettledCameraLevel(frame.ZoomSelection);
    }

    //Called only after the existing accepted marker/terrain picker installed
    //the actual selected location. It neither selects a player Army nor
    //substitutes a nearby City for the clicked Gate.
    private bool TryOpenPickedEnemyFootholdCommands(Vector2 surfacePosition)
    {
        if(!_embedded||_detailsOpen||_logicalWorld is not {} world||_playerForceId is not int player||
            EnemyFootholdSelected is null)return false;
        int index=_locationSelection.Selected;
        int id;bool gate;
        if(_d3d11Markers is {} markers)
        {if(index<0||index>=markers.Locations.Count)return false;var selected=markers.Locations[index];id=selected.Id;gate=selected.IsGate;}
        else if(_locations is {} locations)
        {if(index<0||index>=locations.Locations.Count)return false;var selected=locations.Locations[index];id=selected.Id;gate=selected.IsGate;}
        else return false;
        BaseKind kind;int? force;
        if(gate)
        {if(!world.Gates.TryGetValue(id,out var at))return false;kind=BaseKind.Gate;force=at.ForceId;}
        else
        {if(!world.Cities.TryGetValue(id,out var at))return false;kind=BaseKind.City;force=at.ForceId;}
        if(force==player)return false;
        var point=CanonicalSourceCursor(surfacePosition);
        int? level=ReadEnemyFootholdCameraLevel();
        CloseCityCommandMenu();ResetSourceCameraInput();
        EnemyFootholdSelected.Invoke(kind,id,new(point.X34,point.Y38),level);
        return true;
    }
}
