using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private MapLocationCommandMenu? _areaHexCommandMenu;
    private GameState? _areaHexCommandWorld;
    private int? _areaHexCommandId;
    private Func<GameState,int,bool?,AreaAdministrationQuery<AreaHexCommandMenuProjection>>? _queryAreaHexCommandMenu;
    internal event Action<AreaHexCommandMenuProjection,AreaHexCommandMenuCommand>? AreaHexCommand;
    internal bool IsAreaHexCommandMenuOpen => _areaHexCommandMenu?.Visible == true;

    internal void SetAreaHexCommandMenuQuery(
        Func<GameState,int,bool?,AreaAdministrationQuery<AreaHexCommandMenuProjection>> query) =>
        _queryAreaHexCommandMenu=query;

    internal void CloseAreaHexCommandMenu()
    {
        bool open=IsAreaHexCommandMenuOpen;
        _areaHexCommandWorld=null;_areaHexCommandId=null;
        var menu=_areaHexCommandMenu;_areaHexCommandMenu=null;
        menu?.Abandon();menu?.QueueFree();
        if(!open)return;
        GetNodeOrNull<Control>("ResetPlayableMapCamera")?.Show();
        _d3d11Markers?.ClearPointer();
        MapOverlayChanged?.Invoke(false);
    }

    internal bool ReopenAreaHexCommandMenu(int areaId)
    {
        if(_logicalWorld?.Areas.GetValueOrDefault(areaId)?.HexId is not int hex||
            _projection is null||TerrainMeshes is null)return false;
        var point=_projection.Project(hex);
        ShowPickedCell(hex,new((float)point.X,
            MapGrounding.SampleHeight(TerrainMeshes,point.X,point.Z),(float)point.Z));
        return TryOpenPickedAreaHexCommands();
    }

    private bool TryOpenPickedAreaHexCommands()
    {
        if(!_embedded||_detailsOpen||IsCityCommandMenuOpen||_sourceMainCamera?.TimedMotionActive==true||
            _logicalWorld is not {} world||_mapSelection.Current is not {} selection||
            _queryAreaHexCommandMenu is null||AreaHexCommand is null||
            ReadEnemyFootholdCameraLevel() is not (>=0 and <=3)||
            !world.Territory.TryGetValue(selection.HexId,out var territory)||territory.AreaId is not int areaId||
            !world.Areas.TryGetValue(areaId,out var area)||area.HexId!=selection.HexId||
            world.Cities.Values.Any(city=>city.HexId==selection.HexId)||
            world.Gates.Values.Any(gate=>gate.HexId==selection.HexId)||
            world.Units.Values.Any(unit=>unit.HexId==selection.HexId &&
                (CaptureCurrentArmyPresence(world)?.VisibleUnitIds.Contains(unit.Id) ?? true))||
            ProjectDeploymentTarget(selection.HexId) is not {} anchor)return false;
        bool commands=world.Phase==TurnPhase.Strategy&&!_periodMapActive;
        var query=_queryAreaHexCommandMenu(world,areaId,commands);
        if(!query.Available||query.Value is not {} page)
        {SetMeta("area_menu_unknown",query.UnresolvedInput??"area-hex-menu.current_query");return false;}
        RemoveMeta("area_menu_unknown");
        CloseAreaHexCommandMenu();
        _areaHexCommandWorld=world;_areaHexCommandId=areaId;
        var menu=new MapLocationCommandMenu{Name="AreaHexCommandMenu"};
        _areaHexCommandMenu=menu;AddChild(menu);
        // Borrow from the existing application owner for this popup lifetime.
        var shared=_borrowSummaryResources?.Invoke(menu);
        if(shared is {} borrowed)menu.TreeExited+=borrowed.Release;
        menu.Configure(_repository,_text,anchor,page.Entries.Select(entry=>new MapLocationCommandMenuEntry(
            entry.Command.ToString(),entry.Payload,entry.CaptionId,entry.SourcePresent,entry.SourceEnabled)).ToArray(),
            ()=>ReferenceEquals(_areaHexCommandMenu,menu)&&ReferenceEquals(_areaHexCommandWorld,_logicalWorld),
            payload=>Run(()=>
            {
                if(_areaHexCommandWorld is not {} currentWorld||
                    !ReferenceEquals(currentWorld,_logicalWorld)||_areaHexCommandId!=areaId)return;
                var current=_queryAreaHexCommandMenu(currentWorld,areaId,commands);
                var entry=current.Value?.Entries.FirstOrDefault(row=>row.Payload==payload);
                if(!current.Available||entry is not {SourcePresent.Available:true,SourcePresent.Value:true,
                    SourceEnabled.Available:true,SourceEnabled.Value:true})return;
                CloseAreaHexCommandMenu();
                AreaHexCommand.Invoke(current.Value!,entry.Command);
            }),()=>Run(CloseAreaHexCommandMenu), resources:shared?.Resources);
        GetNodeOrNull<Control>("ResetPlayableMapCamera")?.Hide();
        _mapCityInformation?.Hide();_mapCityOrders?.Hide();_mapAreaOrders?.Hide();
        _mapUnitOrders?.Hide();_mapNextUnit?.Hide();
        ResetSourceCameraInput();_d3d11Markers?.ClearPointer();
        MapOverlayChanged?.Invoke(true);
        return true;
    }
}
