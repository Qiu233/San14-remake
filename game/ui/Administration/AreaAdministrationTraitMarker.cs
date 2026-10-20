using Godot;
using San14.Data;

/// <summary>The original CCmnMrker, resized to the officer-card mount. It is
/// a marker rather than an ordinary command button.</summary>
internal partial class AreaAdministrationTraitMarker : Control
{
    private readonly Dictionary<byte,Control> backgrounds=[];
    private UiSourceLayout source=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private UiSourceText label=null!;
    private Rect2 hitArea;
    public override bool _HasPoint(Vector2 point)=>hitArea.HasPoint(point);
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font)
    {
        this.assets=assets;this.shader=shader;
        source=new UiSourceLayout(repository,0xD493EA98).ResizedSubtree(0,new(158,40),true);
        Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Stop;
        var box=source.Rectangle(1);var centre=box.GetCenter();
        hitArea=new(new Vector2((int)centre.X-(int)(box.Size.X*.5f),(int)centre.Y-(int)(box.Size.Y*.5f)),
            new Vector2((int)box.Size.X,(int)box.Size.Y));
        label=source.Text(this,3,default,"",font,MapTextPresets.Create()[0]);label.Name="TraitName";
        // The caller hides the common focus root. Its later balloon/focus
        // consumer is a separate boundary; do not invent a hover outline.
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
    internal void Apply(int identity,string name,byte style)
    {
        string sprite=style switch {0=>"000_cmn_base_indivi_brank",1=>"000_cmn_base_indivi_buff",2=>"000_cmn_base_indivi_debuff",
            3=>"000_cmn_base_indivi_unique",4=>"000_cmn_base_indivi_buffdebuff",_=>throw new InvalidDataException("Unsupported officer trait marker style.")};
        if(!backgrounds.TryGetValue(style,out var background))
        {
            background=source.Art(this,2,source.Rectangle(2),assets,shader,commonSpriteOverride:sprite);
            backgrounds.Add(style,background);MoveChild(background,0);
        }
        foreach(var (value,art) in backgrounds)art.Visible=value==style;
        label.Text=name;SetMeta("trait_id",identity);SetMeta("source_sprite",sprite);
    }
}
