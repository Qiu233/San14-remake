using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary empty-Hex menu's single source Move entry. Its owner
/// supplies a projected Hex anchor and the actual command permission.</summary>
internal partial class DeploymentMoveMenu : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Action? release;
    private Action<string?>? narration;
    private bool finished;
    private Func<Vector2?>? project;
    private Action<Vector2?>? policyPositioned;
    private int footerTop=912;

    internal void Abandon()
    {finished=true;project=null;release=null;narration=null;policyPositioned?.Invoke(null);policyPositioned=null;Hide();}

    internal void TrackAnchor(Func<Vector2?> currentProjection,Action<Vector2?>? placePolicy=null,int footerBoundary=912)
    {project=currentProjection;policyPositioned=placePolicy;footerTop=footerBoundary;ApplyProjectedAnchor(project());}
    public override void _Process(double delta)
    {if(!finished&&project is { } current)ApplyProjectedAnchor(current());}

    internal void Configure(GameResourceRepository repository,PreviewText text,Action released,
        Action<string?> hintChanged)
    {
        Name="DeploymentMoveMenu";MouseFilter=MouseFilterEnum.Ignore;Size=new(154,338);
        release=released;narration=hintChanged;
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        var menu=new UiSourceLayout(repository,0xA7513A25).ResizedSubtree(1,new(154,60),true);
        menu.Art(this,1,menu.Rectangle(1),assets,shader);
        var button=new DeploymentMoveButton{Name="Move",Position=menu.Rectangle(2).Position};AddChild(button);
        string caption=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records[0x0261].Text
            ??throw new InvalidDataException("Missing source Move caption.");
        string hint=repository.LoadMessages(text.SourceLocale,MessageKind.Game).Messages.Sections[2].Records[0x0881].Literals.Single().Text;
        button.Configure(repository,assets,shader,font,caption,()=>
        {
            if(finished)return;
            finished=true;Hide();policyPositioned?.Invoke(null);narration?.Invoke(null);release?.Invoke();
        });
        button.MouseEntered+=()=>narration?.Invoke(hint);
        button.MouseExited+=()=>narration?.Invoke(null);
        SetMeta("source_layout",menu.Source.Source.ResourceUri);
    }

    /// <summary>Canonical pixels from the actual target world anchor plus Y200.
    /// The companion policy pane follows the original asymmetric avoidance.</summary>
    internal void ApplyProjectedAnchor(Vector2? projected)
    {
        if(projected is not Vector2 point){Hide();policyPositioned?.Invoke(null);narration?.Invoke(null);return;}
        int x=Math.Max((int)point.X,0),y=Math.Max((int)point.Y,0);
        bool flip=x+525>1919;
        if(flip&&x+145>1919)x=1775;
        // The original query reads resized window node1 (60 for one Move),
        // while the authored menu root remains 154 by 338.
        if(footerTop<=y+60)y=footerTop-60;
        Position=new(x,y);Visible=!finished;
        int extra=footerTop<=y+240?Math.Min(0,footerTop-240-y):0;
        policyPositioned?.Invoke(finished?null:new Vector2(flip?x-380:x+150,y+extra));
    }

    public override void _ExitTree()
    {narration?.Invoke(null);policyPositioned?.Invoke(null);release=null;narration=null;project=null;policyPositioned=null;font?.Dispose();assets?.Dispose();shader?.Dispose();}
}

internal partial class DeploymentMoveButton : UiSourceListCheckboxButton
{
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,
        MapFont font,string caption,Action released)
    {
        var wrapper=new UiSourceLayout(repository,0xA7DA0617);
        base.Configure(repository,assets,shader,font,new(124,48),caption,released,wrapper.Rectangle(2));
        var palettes=MapSpritePalettes.Create();
        wrapper.Art(this,3,wrapper.Rectangle(3),assets,shader,palettes[9]);
        wrapper.Art(this,4,wrapper.Rectangle(4),assets,shader,palettes[10],"000_cmn_icon_bt_kidou");
        CaptionControl.Name="MoveCaption";
        MoveChild(CaptionControl,GetChildCount()-1);
    }
}
