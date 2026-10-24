using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private MapPresentationState? _mapPresentation;
    private Func<GameState,SourceWorldContext?>? _captureSceneryCurrent;
    private SceneryMapLayer? _sceneryMap;
    private MainMapCameraModeOwner? _sourceMainMode;
    private Window? _sceneryWindow;
    private Vector2I? _sceneryPointer;

    internal void SetSceneryMapBinding(MapPresentationState presentation,
        Func<GameState,SourceWorldContext?> capture)
    {
        if(_mapPresentation is not null)throw new InvalidOperationException("Map presentation is already bound.");
        _mapPresentation=presentation;_captureSceneryCurrent=capture;
        presentation.Changed+=SceneryPresentationChanged;
    }
    private void InitializeSceneryMap()
    {
        if(_sceneryMap is not null||_mapPresentation is null||_captureSceneryCurrent is null)return;
        if(_logicalWorld is not {} world||_sourceSnapshot is not {} scenario||_loadedSceneResources is null)
            throw new InvalidDataException("The actual Scenery map dependencies are not loaded.");
        var layer=new SceneryMapLayer{Name="SceneryMap",ZIndex=2};
        var parent=_sourceMainCameraAspect as Control??_surface.GetParent() as Control??this;
        parent.AddChild(layer);layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _sceneryMap=layer;
        try
        {
            layer.Configure(_repository,_loadedSceneResources,scenario.FixedDataPath,_text.SourceLocale,
                world,_mapPresentation,_captureSceneryCurrent,()=>_sceneryPointer,SelectSceneryMapEntry,
                ()=>((SubViewport)_surface.GetChild(0)).Size,ReadMapHover,
                ()=>!_detailsOpen&&!_periodMapActive&&IsVisibleInTree(),CanPressSceneryMapEntry);
            _sceneryWindow=GetTree().Root;
            _sceneryWindow.WindowInput+=CaptureSceneryPointer;
        }
        catch
        {layer.Abandon();layer.QueueFree();_sceneryMap=null;throw;}
    }
    private void CaptureSceneryPointer(InputEvent input)
    {
        if(input is not InputEventMouse mouse||_sceneryMap is null)return;
        // The physical client input boundary is signed16, before the actual
        // viewport/Canvas transforms and the map's canonical cursor writer.
        Vector2 physical=new(unchecked((short)BattleUnitSourceMath.TruncateSingleToInt32(mouse.Position.X)),
            unchecked((short)BattleUnitSourceMath.TruncateSingleToInt32(mouse.Position.Y)));
        Vector2 viewport=GetViewport().GetFinalTransform().AffineInverse()*physical;
        Vector2 local=_surface.GetGlobalTransformWithCanvas().AffineInverse()*viewport;
        var point=CanonicalSourceCursor(local);
        _sceneryPointer=new(point.X34,point.Y38);
        CaptureSourceHoverPointer(input,point);
    }
    private void PresentGodotScenery(MainMapCameraFrame frame)
    {
        if(UseD3D11Map||_sceneryMap is not {} layer||_logicalWorld is not {} world||_sourceMainMode is null)return;
        var target=(SubViewport)_surface.GetChild(0);
        var captured=layer.Capture(world,_sourceMainMode.PrepareCurrent(frame),
            _sourceMainCamera!.CurrentRuntime.Transition is null,target.Size);
        layer.Present(captured,true);
    }
    private void SceneryPresentationChanged()
    {
        ApplyKeyboardMapInformation();
        if(!_loaded)return;
        if(UseD3D11Map)ScheduleD3D11MapFrame();
        else if(_sourceMainCameraFrame is {} frame)PresentGodotScenery(frame);
    }
    private void SelectSceneryMapEntry(int id)
    {
        if(!CanPressSceneryMapEntry(id))return;
        if(_sceneryMap?.Accepted is not {} accepted||!ReferenceEquals(accepted.World,_logicalWorld))return;
        var current=accepted.Current;
        if(current.ReadSceneryNativeValidity(new(id))!=true)return;
        ushort raw=current.ReadSceneryHexIndex(new(id))??throw new InvalidDataException("Selected Scenery Hex is unknown.");
        var reference=current.ReadHexTableReference(raw<48400?raw:0);
        if(reference?.PhysicalSlot is not int physical)return;
        if(PublishCommandTarget(physical))return;
        var point=CeHexWorldCoordinates.Project(physical);
        ShowPickedCell(physical,new((float)point.X,
            MapGrounding.SampleHeight(TerrainMeshes!,point.X,point.Z),(float)point.Z));
        _acceptHex?.Invoke(physical);
    }
    private bool CanPressSceneryMapEntry(int id)
    {
        if(_detailsOpen||!IsVisibleInTree()||_sceneryMap?.Accepted is not {} accepted||
            !ReferenceEquals(accepted.World,_logicalWorld))return false;
        var current=accepted.Current;
        if(current.ReadSceneryNativeValidity(new(id))!=true||current.ReadSceneryHexIndex(new(id)) is not ushort raw)
            return false;
        var candidate=current.ReadHexTableReference(raw<48400?raw:0);
        return candidate?.PhysicalSlot is not null&&AllowsMapHover(candidate)==true;
    }
    private void ReleaseSceneryMap()
    {
        if(_sceneryWindow is {} window)
        {window.WindowInput-=CaptureSceneryPointer;_sceneryWindow=null;}
        _sceneryPointer=null;
        var layer=_sceneryMap;_sceneryMap=null;
        if(layer is not null){layer.Abandon();layer.QueueFree();}
    }
    private void UnbindSceneryMapPresentation()
    {
        if(_mapPresentation is {} presentation)presentation.Changed-=SceneryPresentationChanged;
    }
}
