using Godot;
using San14.Data;

internal partial class NewGameForceSelectionView
{
    private NewGameForceMapOverlay? sourceMapOverlay;
    private Action<InputEventMouse>? sourceMapInput;
    private Action? sourceMapLeft;
    private Window? sourceMapWindow;
    internal void ConfigureSourceMapInput(Action<InputEventMouse> input,Action left)
    {
        sourceMapInput=input;sourceMapLeft=left;
        sourceMapWindow=GetWindow();sourceMapWindow.FocusExited+=SourceMapFocusExited;
    }
    internal void PresentSourceMap(Texture2D texture,NewGameForceMapContent content,MapSceneView map,D3D11MapInput input,
        UiAssets assets,Shader shader,MapFont font)
    {
        ApplyMap(texture);
        if(sourceMapOverlay is null)
        {
            sourceMapOverlay=new NewGameForceMapOverlay {Name="SourceFootholdMapControls"};
            body.AddChild(sourceMapOverlay);body.MoveChild(sourceMapOverlay,1);
            sourceMapOverlay.Configure(content,assets,shader,font);
        }
        sourceMapOverlay.Present(map,input,map.CityGateMarkerSources);
        ApplySourceMapSelection();
    }
    internal void ApplySourceMapSelection()=>sourceMapOverlay?.ApplyInteraction(SelectedForceId,PreviewForceId);
    internal bool ReadSourceMapPointer(Vector2 canonical,bool? press,out int hex)
    {
        if(sourceMapOverlay is {} overlay)return overlay.ReadPointer(canonical,press,out hex);
        hex=-1;return false;
    }
    internal void ClearSourceMapPointer(){if(!abandoned)sourceMapOverlay?.ClearPointer();}
    private void SourceMapFocusExited()=>sourceMapLeft?.Invoke();
    private void RetireSourceMapInput()
    {
        if(sourceMapWindow is {} window&&GodotObject.IsInstanceValid(window))window.FocusExited-=SourceMapFocusExited;
        sourceMapWindow=null;sourceMapInput=null;sourceMapLeft=null;
    }
    private void AttachSourceMapInput(TextureRect map)
    {
        map.MouseFilter=MouseFilterEnum.Stop;
        map.GuiInput+=input=>
        {
            if(ReadAdmission()&&input is InputEventMouse mouse)sourceMapInput?.Invoke(mouse);
        };
        map.MouseExited+=()=>sourceMapLeft?.Invoke();
    }
}
