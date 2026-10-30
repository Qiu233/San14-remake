using Godot;
using San14.Data;

/// <summary>The original CCmnMrker, resized to the officer-card mount. It is
/// a marker rather than an ordinary command button.</summary>
internal partial class AreaAdministrationTraitMarker : Control
{
    private readonly Dictionary<(byte Style,bool Active),Control> backgrounds=[];
    private UiSourceLayout source=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private UiSourceText label=null!;
    private Rect2 hitArea;
    private MapFont font=null!;
    private bool? activeText;
    public override bool _HasPoint(Vector2 point)=>hitArea.HasPoint(point);
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,Vector2? size=null)
    {
        Configure(new UiSourceLayout(repository,0xD493EA98),assets,shader,font,size??new(158,40));
    }
    internal void Configure(UiSourceLayout template,UiAssets assets,Shader shader,MapFont font,Vector2 size)
    {
        this.assets=assets;this.shader=shader;this.font=font;
        source=template.ResizedSubtree(0,size,true);
        Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Stop;
        var box=source.Rectangle(1);var centre=box.GetCenter();
        hitArea=new(new Vector2((int)centre.X-(int)(box.Size.X*.5f),(int)centre.Y-(int)(box.Size.Y*.5f)),
            new Vector2((int)box.Size.X,(int)box.Size.Y));
        label=source.Text(this,3,default,"",font,MapTextPresets.Create()[0]);label.Name="TraitName";
        // The caller hides the common focus root. Its later balloon/focus
        // consumer is a separate boundary; do not invent a hover outline.
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
    internal void Apply(int identity,string name,byte style)=>Apply(identity,name,style,true);
    internal void Apply(int identity,string name,byte style,bool active)
    {
        string sprite=style switch {0=>"000_cmn_base_indivi_brank",1=>active?"000_cmn_base_indivi_buff":"000_cmn_base_indivi_buff_off",
            2=>active?"000_cmn_base_indivi_debuff":"000_cmn_base_indivi_debuff_off",
            3=>active?"000_cmn_base_indivi_unique":"000_cmn_base_indivi_unique_off",4=>"000_cmn_base_indivi_buffdebuff",
            _=>throw new InvalidDataException("Unsupported officer trait marker style.")};
        var key=(style,active);
        if(!backgrounds.TryGetValue(key,out var background))
        {
            background=source.Art(this,2,source.Rectangle(2),assets,shader,commonSpriteOverride:sprite);
            backgrounds.Add(key,background);MoveChild(background,0);
        }
        foreach(var (value,art) in backgrounds)art.Visible=value==key;
        if(activeText!=active)
        {
            label.Configure(font,MapFontProfile.FromNode(source.Nodes[3],source.Source.Source.ResourceUri)
                .WithPreset(MapTextPresets.Create()[active?0:14]));activeText=active;
        }
        label.Text=name;SetMeta("trait_id",identity);SetMeta("source_sprite",sprite);
    }
    internal void ApplyUnknown(string caption)
    {
        foreach(var background in backgrounds.Values)background.Hide();
        label.Text=caption;RemoveMeta("trait_id");RemoveMeta("source_sprite");
    }
}
