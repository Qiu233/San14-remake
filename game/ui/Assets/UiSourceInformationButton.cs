using Godot;
using San14.Data;

/// <summary>The six original information choices share one authored skin.
/// Selected is its sixth pose, independent of the ordinary pressed pose.</summary>
internal partial class UiSourceInformationButton : UiSourceButton
{
    internal static readonly string[][] Icons =
    [
        ["000_cmn_btn_view_color_0", "000_cmn_btn_view_color_1", "000_cmn_btn_view_color_2", "000_cmn_btn_view_color_3", "000_cmn_btn_view_color_4", "000_cmn_btn_view_color_5"],
        ["000_cmn_btn_view_kokkyou_0", "000_cmn_btn_view_kokkyou_1", "000_cmn_btn_view_kokkyou_2", "000_cmn_btn_view_kokkyou_3", "000_cmn_btn_view_kokkyou_4", "000_cmn_btn_view_kokkyou_5"],
        ["000_cmn_btn_view_gaikou_0", "000_cmn_btn_view_gaikou_1", "000_cmn_btn_view_gaikou_2", "000_cmn_btn_view_gaikou_3", "000_cmn_btn_view_gaikou_4", "000_cmn_btn_view_gaikou_5"],
        ["000_cmn_btn_view_kyoten_0", "000_cmn_btn_view_kyoten_1", "000_cmn_btn_view_kyoten_2", "000_cmn_btn_view_kyoten_3", "000_cmn_btn_view_kyoten_4", "000_cmn_btn_view_kyoten_5"],
        ["000_cmn_btn_view_butai_0", "000_cmn_btn_view_butai_1", "000_cmn_btn_view_butai_2", "000_cmn_btn_view_butai_3", "000_cmn_btn_view_butai_4", "000_cmn_btn_view_butai_5"],
        ["000_cmn_btn_view_rinsetsu_0", "000_cmn_btn_view_rinsetsu_1", "000_cmn_btn_view_rinsetsu_2", "000_cmn_btn_view_rinsetsu_3", "000_cmn_btn_view_rinsetsu_4", "000_cmn_btn_view_rinsetsu_5"]
    ];
    internal static readonly string[] Actions = ["btn_0","btn_1","btn_2","btn_3","btn4"];
    internal static readonly int[] Artwork = [3,5,6,8,9,11,13,14,16,17,18];
    private Rect2 hit;
    internal override int VisualState => Selected&&!Disabled?5:base.VisualState;
    public override bool _HasPoint(Vector2 point)=>SourceInputAdmitted&&hit.HasPoint(point);

    internal void Configure(UiSourceLayout source,UiAssets assets,Shader shader,int type,
        UiSourceButtonStateProfile ordinary,UiSourceButtonStateProfile selected,Action action)
    {
        Size=source.Rectangle(0).Size;hit=source.Rectangle(1);
        var bindings=new List<(int Node,Control Root,UiSourceSprite[] Sprites)>();
        foreach(int node in Artwork)
        {
            int icon=node switch {3=>0,5 or 6=>1,8=>2,11=>3,13 or 14=>4,16 or 17=>5,_=>-1};
            var root=source.Art(this,node,source.Rectangle(node),assets,shader,
                commonSpriteOverride:icon<0?null:Icons[type][icon],
                blend:ordinary.BlendAt(node)==0x00114400?UiSourceSprite.Blend.Additive:UiSourceSprite.Blend.Ordinary);
            bindings.Add((node,root,Parts(root).ToArray()));
        }
        ConfigureVisualState(state=>
        {
            foreach(var (node,root,sprites) in bindings)
            {
                byte alpha=state==5?selected.AlphaAt(0,node):ordinary.AlphaAt(state,node);
                root.Visible=alpha!=0;
                foreach(var sprite in sprites)sprite.SetSourceAlpha(alpha);
            }
        });
        Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)action();};
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("information_type",type);
    }
    private static IEnumerable<UiSourceSprite> Parts(Node root)
    {
        if(root is UiSourceSprite sprite)yield return sprite;
        foreach(Node child in root.GetChildren())foreach(var part in Parts(child))yield return part;
    }
}
