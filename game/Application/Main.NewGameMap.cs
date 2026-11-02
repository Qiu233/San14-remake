using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private Control? _mapLayoutSlot;
    private MapSceneView? _newGameMap,_startedNewGameMap;
    private NewGameSetupView? _newGameMapPage;
    private NewGameForceMapContent? _newGameMapContent;
    private Action? _newGameMapPresentation;
    private Vector2? _newGameMapPress,_newGameMapCursor;
    private int? _newGameMapCandidate;
    private uint _newGameMapHeldFrames;
    private bool _newGameMapPressedEdge,_newGameMapAction23Down;
    private readonly HashSet<int> _newGameMapHeldKeys=[];

    private void UpdateMapHostPlacement()
    {
        if(_mapHost is null||_mapLayoutSlot is null||!IsInsideTree()||
            !GodotObject.IsInstanceValid(_mapHost)||!GodotObject.IsInstanceValid(_mapLayoutSlot)||
            !_mapHost.IsInsideTree()||!_mapLayoutSlot.IsInsideTree())return;
        if(_newGameMapPage is not null||_startedNewGameMap is not null)
        {_mapHost.Position=Vector2.Zero;_mapHost.Size=Size;}
        else
        {
            _mapHost.Position=GetGlobalTransformWithCanvas().AffineInverse()*_mapLayoutSlot.GlobalPosition;
            _mapHost.Size=_mapLayoutSlot.Size;
        }
    }

    private void BeginNewGameMap(NewGameSetupView page,NewGameForceMapContent content)
    {
        RetireNewGameMap();
        _newGameMapPage=page;_newGameMapContent=content;
        UpdateMapHostPlacement();
        var loading=page.BeginSourceMapLoading(content);
        page.SetSourceMapInput(mouse=>ReadNewGameMapInput(page,mouse),()=>ClearNewGameMapPointer(page),
            ()=>TickNewGameMapInput(page),key=>ReadNewGameMapKey(page,key));
        _=Prepare();
        async Task Prepare()
        {
            MapSceneView? map=null;
            try
            {
                await loading.Drawn;
                if(!IsNewGameMapPageCurrent(page))return;
                var world=_world!;var composition=_campaignComposition!;
                var queries=composition.PrepareArmyQueries(world);
                map=new MapSceneView();_newGameMap=map;
                map.SetStaticMapResources(BorrowApplicationMapResources());
                ApplyCommittedCgQuality(map);_mapHost.AddChild(map);
                map.SetSourceNavigationOptions(_preferences.Navigation);
                map.SetKeyboardBindings(_preferences.KeyboardBindings);
                map.SetD3D11CurrentWorldQueries(current=>ReferenceEquals(current,world)?content.Current:
                    throw new InvalidDataException("The unstarted map has not adopted a current campaign query owner."));
                map.SetFacilitySceneModelQuery(queries.QueryFacilitySceneModels,queries.QueryInitialFacilitySceneConnections);
                _newGameMapPresentation=()=>
                {
                    if(IsNewGameMapPageCurrent(page)&&_newGameMap==map)page.PresentSourceMap(map);
                };
                map.ScenarioPreviewPresented+=_newGameMapPresentation;
                map.Configure(_repository!,_ui,new PreviewText(_locale.Selected==1,null),
                    ()=>RunUiAction(ReturnFromNewGameForceSelection),_map,world,embedded:true,
                    sourceSnapshot:_selectedSnapshot,worldScene:true,cameraEntry:MapCameraEntry.NewGameForceSelection,
                    scenarioPreview:content.Calendar);
                await map.InitialSceneLoad;
                if(!IsNewGameMapPageCurrent(page)||_newGameMap!=map)return;
                if(!map.IsScenarioPreviewReady)
                    throw new InvalidDataException("The real force map did not complete its native/Canvas opening: "+
                        map.GetMeta("scene_error",map.GetMeta("native_map_source_error","")).AsString());
                page.PresentSourceMap(map);
                map.EnterScenarioForceSelection();
                page.CompleteSourceMapLoading();
            }
            catch(OperationCanceledException) { }
            catch(Exception error)
            {
                if(IsNewGameMapPageCurrent(page))ShowFailure(error,startup:false);
            }
        }
    }
    private bool IsNewGameMapPageCurrent(NewGameSetupView page)=>!_failed&&!_shuttingDown&&
        _newGameSetup==page&&_newGameMapPage==page&&_playerForceId is null&&
        _world is {Phase:TurnPhase.Initializing}&&IsInsideTree();

    private void RetireNewGameMap()
    {
        var map=_newGameMap;_newGameMap=null;_newGameMapPage=null;_newGameMapContent=null;
        if(map is not null)
        {
            if(_newGameMapPresentation is not null)map.ScenarioPreviewPresented-=_newGameMapPresentation;
            if(GodotObject.IsInstanceValid(map))map.QueueFree();
        }
        // A failed adoption can retire after the preview was detached but
        // before the resident caller took it. Ordinary page changes retain it.
        if((_failed||_shuttingDown||IsReturningToTitle)&&TakeStartedNewGameMap() is {} retained&&
            GodotObject.IsInstanceValid(retained))retained.QueueFree();
        _newGameMapPresentation=null;_newGameMapPress=_newGameMapCursor=null;_newGameMapCandidate=null;
        _newGameMapHeldFrames=0;_newGameMapHeldKeys.Clear();_newGameMapAction23Down=false;UpdateMapHostPlacement();
    }
    private void RetainNewGameMapForStartedCampaign()
    {
        if(_newGameMap is null)return;
        if(!_newGameMap.IsScenarioPreviewReady)
            throw new InvalidOperationException("The confirmed campaign requires its actually prepared force map.");
        _startedNewGameMap=_newGameMap;
        if(_newGameMapPresentation is not null)_newGameMap.ScenarioPreviewPresented-=_newGameMapPresentation;
        _newGameMap=null;_newGameMapPage=null;_newGameMapContent=null;_newGameMapPresentation=null;
        _newGameMapPress=_newGameMapCursor=null;_newGameMapCandidate=null;
    }
    private MapSceneView? TakeStartedNewGameMap()
    {
        var map=_startedNewGameMap;_startedNewGameMap=null;
        return map;
    }

    private void ClearNewGameMapPointer(NewGameSetupView page)
    {
        _newGameMapPress=_newGameMapCursor=null;_newGameMapCandidate=null;_newGameMapHeldFrames=0;
        _newGameMapHeldKeys.Clear();_newGameMapAction23Down=false;
        page.ClearSourceMapPointer();
        // Leaving the map has no formal selection side effect. The face-list
        // and source map share the page's ordinary preview-fallback operation.
        if(IsNewGameMapPageCurrent(page)&&page.CanReadSourceMapInput)page.PreviewSourceMapForce(null);
    }
    private void ReadNewGameMapInput(NewGameSetupView page,InputEventMouse mouse)
    {
        if(!IsNewGameMapPageCurrent(page)||!page.CanReadSourceMapInput||
            _newGameMap is not {IsScenarioPreviewReady:true} map||_newGameMapContent is not {} content)return;
        var cursor=new Vector2(BattleUnitSourceMath.TruncateSingleToInt32(mouse.Position.X),
            BattleUnitSourceMath.TruncateSingleToInt32(mouse.Position.Y));
        _newGameMapCursor=cursor;
        bool? press=mouse is InputEventMouseButton {ButtonIndex:MouseButton.Left} button?button.Pressed:null;
        bool flag=page.ReadSourceMapPointer(cursor,press,out int hex);
        if(press==true){_newGameMapPress=cursor;_newGameMapHeldFrames=0;_newGameMapPressedEdge=true;}
        if(!flag)
        {
            bool frozen=_newGameMapPress is {} start&&MapHoverSelection.IsFrozen(1,_newGameMapHeldFrames,
                (int)start.X,(int)start.Y,(int)cursor.X,(int)cursor.Y);
            // Release clears the source held counter before3F5450, so a long
            // stationary hold remains a legitimate release selection.
            if(press==false&&_newGameMapPress is {} released)
                frozen=MapHoverSelection.IsFrozen(1,0,(int)released.X,(int)released.Y,(int)cursor.X,(int)cursor.Y);
            if(frozen){if(press==false)_newGameMapPress=null;return;}
            if(!map.TryPickScenarioPreviewHex(new((int)cursor.X,(int)cursor.Y),out hex))hex=-1;
        }
        int? candidate=ReadNewGameMapForce(content,hex);
        _newGameMapCandidate=candidate;
        bool releasedPrimary=press==false&&_newGameMapPress is {} origin&&
            !MapHoverSelection.IsFrozen(1,0,(int)origin.X,(int)origin.Y,(int)cursor.X,(int)cursor.Y);
        if(releasedPrimary&&candidate is int selected)page.SelectSourceMapForce(selected);
        if(press==false)_newGameMapPress=null;
        if(hex<0)page.PreviewSourceMapForce(null);
        else
        {
            var pointer=content.Current.ReadHexTableReference(hex)??throw new InvalidDataException("The source Hex pointer is absent.");
            byte type=content.Current.ReadHexSurfaceCode(pointer)??throw new InvalidDataException("The source Hex surface is absent.");
            var surface=content.Current.ReadSurfaceTypeTableReference(type<31?type:0)??throw new InvalidDataException("The source terrain type is absent.");
            ushort field=content.Current.ReadSurfaceTypeRawField2A(surface)??throw new InvalidDataException("The source terrain2A is absent.");
            if(field!=0&&candidate is not null)page.PreviewSourceMapForce(candidate);
        }
    }
    private void TickNewGameMapInput(NewGameSetupView page)
    {
        if(!IsNewGameMapPageCurrent(page)||_newGameMapPress is null)return;
        if(_newGameMapPressedEdge){_newGameMapPressedEdge=false;return;}
        if(Input.IsMouseButtonPressed(MouseButton.Left))_newGameMapHeldFrames++;
    }
    private bool ReadNewGameMapKey(NewGameSetupView page,InputEventKey key)
    {
        if(key.Echo||!IsNewGameMapPageCurrent(page)||!page.CanReadSourceMapInput||
            GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)return false;
        int usage=KeyboardPhysicalInput.Hid(key.PhysicalKeycode);
        if(usage!=0){if(key.Pressed)_newGameMapHeldKeys.Add(usage);else _newGameMapHeldKeys.Remove(usage);}
        int modifiers=(key.ShiftPressed?KeyboardBindingCatalog.ShiftModifier:0)|
            (key.CtrlPressed?KeyboardBindingCatalog.ControlModifier:0);
        var bindings=_preferences.KeyboardBindings;bool down=false;
        for(int slot=0;slot<2;slot++)
        {
            int value=bindings[23*2+slot];
            if(value!=0&&(value&0x3000)==modifiers&&_newGameMapHeldKeys.Contains(value&0xFFF))down=true;
        }
        bool pressed=down&&!_newGameMapAction23Down;_newGameMapAction23Down=down;
        if(!pressed||_newGameMapCursor is null||_newGameMapCandidate is not int force)return false;
        page.SelectSourceMapForce(force);return true;
    }
    private static int? ReadNewGameMapForce(NewGameForceMapContent content,int hex)
    {
        if(hex<0)return null;
        var current=content.Current;
        var pointer=current.ReadHexTableReference(hex)??throw new InvalidDataException("The selected source Hex is unknown.");
        ushort raw=current.ReadHexAreaSlot(pointer)??throw new InvalidDataException("The selected source Area12 is unknown.");
        var area=current.ReadAreaTableReference(raw<=500?raw:0)??throw new InvalidDataException("The selected source Area pointer is unknown.");
        byte force=current.ReadAreaCurrentForceSlot(area)??throw new InvalidDataException("The selected source Area Force is unknown.");
        var owner=current.ReadForceTableReference(force<=51?force:0)??throw new InvalidDataException("The selected source Force pointer is unknown.");
        bool valid=current.ReadForceNativeValidity(owner)??throw new InvalidDataException("The selected source Force validity is unknown.");
        return valid&&owner.PhysicalSlot is int physical&&physical is not (>=46 and <=50)?physical:null;
    }
}
