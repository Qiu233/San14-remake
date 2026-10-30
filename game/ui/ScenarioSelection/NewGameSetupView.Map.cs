using Godot;
using San14.Data;

internal partial class NewGameSetupView
{
    private NewGamePreparationView? mapLoading;
    private Action? sourceMapTick;
    private Func<InputEventKey,bool>? sourceMapKey;
    private NewGameForceMapContent? sourceMapContent;
    internal int? PreviewForceId=>forcePage.PreviewForceId;
    internal NewGamePreparationView BeginSourceMapLoading(NewGameForceMapContent content)
    {
        sourceMapContent=content;
        var loading=new NewGamePreparationView();mapLoading=loading;AddChild(loading);
        loading.Configure(repository,text,()=>
        {
            if(!abandoned&&(admitted?.Invoke()??false))returned?.Invoke();
        },new(rulerAssets,pageFont!,rulerShader));
        SetBusy(busy);
        return loading;
    }
    internal void PresentSourceMap(MapSceneView map)
    {
        if(abandoned||sourceMapContent is null||map.ScenarioPreviewPresentation is not {} accepted)return;
        forcePage.PresentSourceMap(accepted.Texture,sourceMapContent,map,accepted.Input,
            rulerAssets,rulerShader,pageFont!);
    }
    internal void CompleteSourceMapLoading()
    {
        var loading=mapLoading;mapLoading=null;
        if(loading is not null){loading.Abandon();RemoveChild(loading);loading.QueueFree();}
        SetBusy(busy);
    }
    internal void ApplySourceMapSelection()=>forcePage.ApplySourceMapSelection();
    internal void SetSourceMapInput(Action<InputEventMouse> input,Action left,Action tick,Func<InputEventKey,bool> key)
    {sourceMapTick=tick;sourceMapKey=key;forcePage.ConfigureSourceMapInput(input,left);}
    public override void _Process(double delta)
    {if(CanReadSourceMapInput)sourceMapTick?.Invoke();}
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if(CanReadSourceMapInput&&input is InputEventKey key&&sourceMapKey?.Invoke(key)==true)
            GetViewport().SetInputAsHandled();
    }
    internal bool ReadSourceMapPointer(Vector2 canonical,bool? press,out int hex)=>forcePage.ReadSourceMapPointer(canonical,press,out hex);
    internal void PreviewSourceMapForce(int? force)=>forcePage.PreviewForce(force);
    internal void SelectSourceMapForce(int force)=>forcePage.SelectForce(force);
    internal void ClearSourceMapPointer()=>forcePage.ClearSourceMapPointer();
    internal bool CanReadSourceMapInput=>ReadAdmission()&&!IsSettingsPage&&!IsConfirmationPage;
}
