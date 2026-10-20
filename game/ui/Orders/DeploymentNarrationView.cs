using Godot;
using San14.Data;

/// <summary>Ordinary deployment narration. Only a fresh advance after the
/// current body has drawn consumes a page; the caller owns the later transaction.</summary>
internal partial class DeploymentNarrationView : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas=null!;
    private DeploymentNarrationBody body=null!;
    private Action? completed;
    private bool ready,mouseArmed,finished;
    private Key keyArmed;
    internal int PageIndex {get;private set;}
    internal bool IsAwaitingAdvance=>ready&&!finished;

    internal void Configure(GameResourceRepository repository,PreviewText text,string speaker,
        string expanded,IReadOnlyList<LoadedSprite> standingLayers,Action consumed)
    {
        Name="DeploymentNarration";ZIndex=10;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);completed=consumed;
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        canvas=new Control{Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Stop};AddChild(canvas);
        var root=new UiSourceLayout(repository,0xD8B39FC6);
        var characterParent=new UiSourceLayout(repository,0x86BA499F);
        var portrait=new UiSourceStandingPortrait{Name="NarrationPortrait",Position=characterParent.Rectangle(2).Position};
        canvas.AddChild(portrait);portrait.Configure(repository,assets,shader,standingLayers);
        var message=new UiSourceLayout(repository,0x98D39EC1);
        message.Art(canvas,2,message.Rectangle(2),assets,shader);
        var name=new UiSourceText{Name="NarrationSpeaker",Position=message.Rectangle(3).Position,Size=message.Rectangle(3).Size};
        canvas.AddChild(name);
        name.Configure(font,MapFontProfile.FromNode(message.Nodes[3],message.Source.Source.ResourceUri) with
            {WidthFitMode=2,RawCharacterMode=2});
        name.Text=speaker;
        var rectangle=message.Rectangle(4);
        body=new DeploymentNarrationBody{Name="NarrationBody",Position=rectangle.Position,Size=rectangle.Size};canvas.AddChild(body);
        body.PageDrawn+=publication=>{if(!finished&&publication==body.Publication)ready=true;};
        body.Configure(font,MapFontProfile.FromNode(message.Nodes[4],message.Source.Source.ResourceUri),expanded);
        SetMeta("source_layout",root.Source.Source.ResourceUri);
        SetMeta("message_layout",message.Source.Source.ResourceUri);
        SetMeta("portrait_parent_layout",characterParent.Source.Source.ResourceUri);
        Resized+=Fit;Fit();
    }

    public override void _Input(InputEvent input)
    {
        if(finished||!IsVisibleInTree())return;
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Right}||input is InputEventKey {Keycode:Key.Escape})
        {GetViewport().SetInputAsHandled();return;}
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Left} mouse)
        {
            GetViewport().SetInputAsHandled();
            if(mouse.Pressed){mouseArmed=ready;return;}
            bool advance=mouseArmed&&ready;mouseArmed=false;if(advance)Advance();
        }
        else if(input is InputEventKey key&&key.Keycode is Key.Space or Key.Enter or Key.KpEnter)
        {
            GetViewport().SetInputAsHandled();if(key.Echo)return;
            if(key.Pressed){keyArmed=ready?key.Keycode:Key.None;return;}
            bool advance=ready&&keyArmed==key.Keycode;keyArmed=Key.None;if(advance)Advance();
        }
    }

    private void Advance()
    {
        ready=false;mouseArmed=false;keyArmed=Key.None;
        if(++PageIndex<body.PageCount){body.ShowPage(PageIndex);return;}
        var callback=completed;Abandon();callback?.Invoke();
    }

    internal void Abandon()
    {finished=true;ready=false;mouseArmed=false;keyArmed=Key.None;completed=null;Hide();}

    private void Fit()
    {
        if(canvas is null)return;
        float scale=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*scale;
        canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }

    public override void _ExitTree()
    {completed=null;finished=true;font?.Dispose();assets?.Dispose();shader?.Dispose();}
}
