using Godot;
using San14.Core;
using San14.Data;

/// <summary>The City cancellation popup edits no draft: each accepted row
/// requests the ordinary command, then displays the newly collected roster.</summary>
internal partial class CityDeploymentCancellationView : Control
{
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;
    private UiAssets assets=null!;
    private MapFont font=null!;
    private Shader shader=null!;
    private Control canvas=null!,popup=null!,rows=null!;
    private UiSourceScrollbar? scrollbar;
    private readonly List<(CityDeploymentCancellationButton Button,int Index)> buttons=[];
    private Func<bool>? admitted;
    private Action<ArmyReceiverId>? cancel;
    private Action? close;
    private Rect2? menuRectangle;
    private int scroll;
    private bool abandoned, ownsResources;
    private Vector2? rightStart;

    internal void Configure(GameResourceRepository repository,PreviewText text,
        DeploymentCancellationPage page,Rect2? menuRectangle,Func<bool> admitted,
        Action<ArmyReceiverId> cancel,Action close,Func<int> scrollSpeed, AdministrationPageChrome.Resources? resources = null)
    {
        this.repository=repository;this.text=text;this.menuRectangle=menuRectangle;
        this.admitted=admitted;this.cancel=cancel;this.close=close;
        Name="CityDeploymentCancellation";ZIndex=5;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ownsResources=resources is null;
        assets=resources?.Assets??new(repository);font=resources?.Font??new(repository,text.SourceLocale);
        shader=resources?.SpriteShader??UiSourceSprite.CreateShader();
        canvas=new(){Name="CancellationCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Stop};
        AddChild(canvas);Resized+=Fit;Fit();
        Apply(page,scrollSpeed);
    }

    internal void Apply(DeploymentCancellationPage page,Func<int> scrollSpeed)
    {
        if(popup is not null){canvas.RemoveChild(popup);popup.QueueFree();}
        buttons.Clear();
        var source=assets.SourceLayout(0xFE9C1A61);
        Vector2 position=menuRectangle is Rect2 menu?new(menu.End.X,menu.Position.Y):new(900,500);
        Vector2 size=source.Rectangle(0).Size;
        if(position.X+size.X>1920&&menuRectangle is Rect2 parent)position.X=parent.Position.X-size.X;
        position=new(Math.Clamp(position.X,0,Math.Max(0,1920-size.X)),Math.Clamp(position.Y,0,Math.Max(0,1080-size.Y)));
        popup=new(){Name="Popup",Position=position,Size=size,MouseFilter=MouseFilterEnum.Stop};canvas.AddChild(popup);
        source.Blur(popup,1,source.Rectangle(1));source.Art(popup,2,source.Rectangle(2),assets,shader);
        Rect2 viewport=source.Rectangle(3);
        rows=new(){Name="CancellationRows",Position=viewport.Position,Size=viewport.Size,ClipContents=true,MouseFilter=MouseFilterEnum.Pass};
        popup.AddChild(rows);
        rows.GuiInput+=input=>{if(Current())scrollbar?.HandleWheel(input);};
        int range=Math.Max(0,page.Rows.Count*86-(int)viewport.Size.Y);
        scrollbar=new(){Name="CancellationScrollbar",Position=source.Rectangle(16).Position,Size=source.Rectangle(16).Size};
        popup.AddChild(scrollbar);
        scrollbar.Configure(repository,assets,shader,range,scrollSpeed(),value=>{scroll=value;PositionRows();},currentScrollSpeed:scrollSpeed);
        foreach(var row in page.Rows)
        {
            var button=new CityDeploymentCancellationButton{Name="CancelDeploymentArmy"+row.SourceReceiver.Value,InputAdmission=Current};
            rows.AddChild(button);buttons.Add((button,buttons.Count));
            button.Configure(repository,text,assets,shader,font,row,()=>{if(Current())cancel?.Invoke(row.SourceReceiver);});
            button.GuiInput+=input=>{if(Current())scrollbar.HandleWheel(input);};
        }
        scrollbar.SetValue(Math.Clamp(scroll,0,range));PositionRows();
        SetMeta("city_id",page.CityId);SetMeta("source_unknown",page.UnresolvedOwner??"");
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }

    private void PositionRows()
    {
        foreach(var (button,index) in buttons)
        {button.Position=new(0,index*86-scroll);button.Visible=button.Position.Y>-86&&button.Position.Y<rows.Size.Y;}
    }
    private bool Current()=>!abandoned&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    internal bool TryHandleCancelInput(InputEvent input)
    {
        if(!Current())return false;
        if(input is InputEventKey{Keycode:Key.Escape} key)
        {if(!key.Pressed&&!key.Echo)close?.Invoke();return true;}
        if(input is InputEventMouseButton{ButtonIndex:MouseButton.Right} mouse)
        {
            if(mouse.Pressed)rightStart=mouse.Position;
            else {if(rightStart is Vector2 start&&(mouse.Position-start).LengthSquared()<=100)close?.Invoke();rightStart=null;}
            return true;
        }
        return false;
    }
    public override void _Input(InputEvent input)
    {
        if(!Current())return;
        if(TryHandleCancelInput(input)){GetViewport().SetInputAsHandled();return;}
        if(input is InputEventMouseButton{ButtonIndex:MouseButton.Left,Pressed:false} mouse&&
            !popup.GetGlobalRect().HasPoint(mouse.Position))
        {close?.Invoke();GetViewport().SetInputAsHandled();}
    }
    internal void Abandon()
    {
        abandoned=true;admitted=null;cancel=null;close=null;
        foreach(var (button,_) in buttons){button.Disabled=true;button.InputAdmission=null;}
        Hide();
    }
    private void Fit()
    {float scale=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=new(scale,scale);canvas.Position=(Size-canvas.Size*scale)*.5f;}
    public override void _ExitTree(){Abandon();if(ownsResources){assets?.Dispose();font?.Dispose();shader?.Dispose();}}
}

internal partial class CityDeploymentCancellationButton : UiSourceButton
{
    internal void Configure(GameResourceRepository repository,PreviewText text,UiAssets assets,Shader shader,
        MapFont font,DeploymentCancellationRow row,Action selected)
    {
        var source=assets.SourceLayout(0x4BC1E64C);Size=source.Rectangle(0).Size;
        source.Art(this,2,source.Rectangle(2),assets,shader);
        var states=new Control[5];
        for(int state=0;state<5;state++)
        {
            states[state]=new(){MouseFilter=MouseFilterEnum.Ignore};AddChild(states[state]);
            if(state is 1 or 2)
                foreach(int node in new[]{14,15})source.Art(states[state],node,source.Rectangle(node),assets,shader);
        }
        var palettes=MapSpritePalettes.Create();
        if(row.FormationIcon is{} formation)
            source.Art(this,3,source.Rectangle(3),assets,shader,palettes[formation.Palette],
                commonSpriteOverride:ArmyMapUiSources.Create().IconName(formation),expansionCommonSprite:formation.SpriteId==0x3F0);
        source.Art(this,5,source.Rectangle(5),assets,shader,palettes[10]);
        if(row.SupplyPalette is int supply)source.Art(this,7,source.Rectangle(7),assets,shader,palettes[supply]);
        source.Text(this,4,default,row.Name??text.Get("detail_unknown"),font);
        source.Text(this,6,default,row.TroopsText??text.Get("detail_unknown"),font);
        var barSource=assets.SourceLayout(0xAFC125D8);
        Rect2 barBounds=source.Rectangle(8);
        var bar=new Control{Name="CancellationMorale",Position=barBounds.Position,Size=barSource.Rectangle(0).Size,
            Scale=barBounds.Size/barSource.Rectangle(0).Size,MouseFilter=MouseFilterEnum.Ignore};
        AddChild(bar);barSource.Art(bar,1,barSource.Rectangle(1),assets,shader);
        if(row.Morale is int morale&&row.MaximumMorale is int maximum)
        {
            int width=maximum>0?(int)((long)barSource.Nodes[1].Value44*Math.Clamp(morale,0,maximum)/maximum):barSource.Nodes[2].Value44;
            if(width>0)
            {
                var fill=new UiSourceLayout(barSource.Source,new Dictionary<int,UiSourceNodeGeometry>
                    {[2]=new(Size:new(width,barSource.Nodes[2].Value46))});
                fill.Art(bar,2,fill.Rectangle(2),assets,shader,palettes[morale>=palettes.MoraleStyleThreshold?3:2]);
            }
        }
        if(row.Portrait is{Kind:OfficerPortraitRouteKind.StockNamed} portrait)
        {
            var face=new UiSourcePortrait{Name="CancellationCommanderPortrait",Position=source.Rectangle(10).Position,Size=source.Rectangle(10).Size};
            AddChild(face);face.ConfigureFace5(repository,assets,shader,
                new LocalizedUiSpriteSource(repository).LoadTexturePackSprite(portrait.ResourceName!,portrait.SpriteName!));
        }
        ConfigureStates(states);Pressed+=()=>{if(SourceInputAdmitted&&!Disabled)selected();};
        SetMeta("army_receiver_id",row.SourceReceiver.Value);SetMeta("physical_slot",row.PhysicalSlot);
        SetMeta("source_unknown",string.Join(";",row.UnresolvedFields.Select(pair=>pair.Key+":"+pair.Value)));
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
}
