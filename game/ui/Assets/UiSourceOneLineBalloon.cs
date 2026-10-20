using Godot;
using San14.Data;

/// <summary>The original mouse-following balloon for plain single-line
/// literals. Its controller bounds differ from the visible window bounds.</summary>
internal partial class UiSourceOneLineBalloon : Control
{
    private UiSourceLayout source=null!;
    private UiAssets assets=null!;
    private MapFont font=null!;
    private Shader shader=null!;
    private UiSourceText caption=null!;
    private Control? artwork;
    private string? current;
    private float nominalHeight;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font)
    {
        Name="SourceOneLineBalloon";ZIndex=5;MouseFilter=MouseFilterEnum.Ignore;
        this.assets=assets;this.shader=shader;this.font=font;
        source=new(repository,0x0ABE99AC);
        caption=source.Text(this,4,default,"",font);caption.Name="BalloonText";
        var profile=caption.LayoutProfile;
        // 92E2A0 measures default face0's nominal height at scale1, then
        // applies this node's ScaleY. It does not measure the current string.
        nominalHeight=font.Source.Record('\u3042',0).Height*profile.ScaleY;
        SetMeta("source_layout",source.Source.Source.ResourceUri);Hide();
    }

    internal void DisplayText(string? text)
    {
        if(string.IsNullOrEmpty(text)){Hide();return;}
        if(current!=text)
        {
            int width=MeasurePlainLiteral(text,nominalHeight),height=(int)nominalHeight;
            Size=new(width+2,height+4);
            if(artwork is { } old){old.Hide();old.Name="PreviousBalloonArtwork";old.QueueFree();}
            artwork=new Control{Name="BalloonArtwork",MouseFilter=MouseFilterEnum.Ignore};AddChild(artwork);MoveChild(artwork,0);
            // Window retains source height56; base and text retain integer
            // top-left positions copied by the source size setters.
            source.Art(artwork,2,new(0,0,width+26,56),assets,shader);
            source.Art(artwork,3,new(12,14,width+2,height+4),assets,shader);
            caption.Position=new(14,15);caption.Size=new(width,height);caption.Text=text;
            current=text;
        }
        Show();UpdatePointer(GetParent<Control>().GetLocalMousePosition());
    }

    internal static int MeasurePlainLiteral(string text,float nominalHeight)
    {
        float width=0;
        foreach(char code in text)
        {
            if(char.IsControl(code)||code=='^')
                throw new InvalidDataException("Balloon control text requires its source paragraph consumer.");
            bool half=unchecked((ushort)(code-0x20))<0x5F||unchecked((ushort)(code+0x9F))<0x3F;
            width+=half?nominalHeight*.5f:nominalHeight;
        }
        return (int)width;
    }

    internal void UpdatePointer(Vector2 pointer)
    {
        int x=(int)pointer.X+20,y=(int)pointer.Y+20;
        // 8A72A0 uses the controller W+2,H+4, not Window's W+26,56.
        x=x<0?0:Math.Min(x,1920-(int)Size.X);
        y=y<0?0:Math.Min(y,1080-(int)Size.Y);
        Position=new(x,y);
    }
    public override void _Process(double delta)
    {if(Visible)UpdatePointer(GetParent<Control>().GetLocalMousePosition());}
}
