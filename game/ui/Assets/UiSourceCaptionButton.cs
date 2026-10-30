using Godot;
using San14.Data;

/// <summary>A source common radio or small text button. Its caller owns the
/// selection and value; this control only publishes a completed release.</summary>
internal partial class UiSourceCaptionButton : UiSourceButton
{
    private Rect2 hit;
    private UiSourceText label=null!;
    internal void ApplyCaption(string value)=>label.Text=value;
    public override bool _HasPoint(Vector2 point)=>SourceInputAdmitted&&hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,
        MapFont font,uint sourceLayout,string text,Action released)
    {
        int firstPreset=sourceLayout switch
        {
            0xA97E37BE or 0x01649C44=>3,
            0x60C991A4=>28,
            _=>throw new ArgumentOutOfRangeException(nameof(sourceLayout))
        };
        var source=assets.SourceLayout(sourceLayout).ResizedSubtree(0,Size,true);
        ConfigureSource(repository,assets,shader,font,source,firstPreset,text,released);
    }

    // Common behavior25 is the original cmn_btn_ft, independently resized to
    // the caller's actual mount. It uses text presets3..7, not round captions.
    internal void ConfigureFooterText(GameResourceRepository repository,UiAssets assets,Shader shader,
        MapFont font,string text,Action released)
    {
        var declared=MapMarkerLayout.LoadNamed(repository,"cmn_btn_ft");
        var source=assets.SourceLayout(declared.Source.FileId).ResizedSubtree(0,Size,true);
        ConfigureSource(repository,assets,shader,font,source,3,text,released);
    }

    private void ConfigureSource(GameResourceRepository repository,UiAssets assets,Shader shader,
        MapFont font,UiSourceLayout source,int firstPreset,string text,Action released)
    {
        Size=source.Rectangle(0).Size;
        Rect2 sourceHit=source.Rectangle(1);
        // CCmnButton copies the source HitArea using integer position/size
        // queries after its float32 recursive resize.
        hit=new(new Vector2((int)sourceHit.Position.X,(int)sourceHit.Position.Y),
            new Vector2((int)sourceHit.Size.X,(int)sourceHit.Size.Y));
        ActionMode=ActionModeEnum.Release;
        int[][] layers=[[3],[5,6],[8,9],[11],[13,14]];
        var art=new UiSourceButtonStateArt(this,source,assets,shader,layers,"CaptionButtonState");
        var caption=source.Text(this,16,default,text,font);caption.Name="Caption";label=caption;
        var profile=MapFontProfile.FromNode(source.Nodes[16],source.Source.Source.ResourceUri);
        var presets=MapTextPresets.Create();
        ConfigureStates(art.States,state=>
        {art.ApplyState(state);caption.Configure(font,profile.WithPreset(presets[firstPreset+state]));caption.QueueRedraw();});
        Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)released();};
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
}
