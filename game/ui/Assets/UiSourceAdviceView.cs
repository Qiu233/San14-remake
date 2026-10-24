using Godot;
using San14.Data;

/// <summary>The passive face and multiline message in the common advice pane.
/// The page owns its already prepared message, portrait and render resources.</summary>
internal partial class UiSourceAdviceView : Control
{
    private GameResourceRepository repository=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private UiSourceParagraph message=null!;
    private Control faceHost=null!;
    private Rect2 imageRectangle;
    private UiSourcePortrait? portrait;
    private LoadedSprite? portraitSource;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        UiSourceLayout source,int faceNode,Vector2 mount)
    {
        this.repository=repository;this.assets=assets;this.shader=shader;MouseFilter=MouseFilterEnum.Ignore;
        var root=source.Rectangle(source.Nodes[faceNode].ParentIndex!.Value,mount);
        Position=root.Position;Size=root.Size;
        var face=source.Rectangle(faceNode,mount);
        faceHost=new() { Name="AdvisorFace",Position=face.Position-root.Position,Size=face.Size,
            MouseFilter=MouseFilterEnum.Ignore,Visible=false };
        AddChild(faceHost);
        var image=source.Rectangle(faceNode+1,mount);imageRectangle=new(image.Position-face.Position,image.Size);
        var frame=source.Rectangle(faceNode+3,mount);
        source.Art(faceHost,faceNode+3,new(frame.Position-face.Position,frame.Size),assets,shader);
        var text=source.Rectangle(faceNode+4,mount);
        message=new() { Name="AdvisorMessage",Position=text.Position-root.Position,Size=text.Size };AddChild(message);
        message.Configure(font,MapFontProfile.FromNode(source.Nodes[faceNode+4],source.Source.Source.ResourceUri)
            with { WidthFitMode=3,RawCharacterMode=2 });
    }

    internal void Apply(LoadedSprite? face,string text)
    {
        message.Apply(text);faceHost.Visible=face is not null;
        if(ReferenceEquals(portraitSource,face))return;
        if(portrait is not null){portrait.Hide();portrait.QueueFree();portrait=null;}
        portraitSource=face;
        if(face is null)return;
        portrait=new() { Name="AdvisorPortrait",Position=imageRectangle.Position,Size=imageRectangle.Size };
        faceHost.AddChild(portrait);faceHost.MoveChild(portrait,0);
        portrait.ConfigureFace2(repository,assets,shader,face);
    }
}
