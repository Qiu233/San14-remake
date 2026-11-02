using Godot;
using San14.Data;

/// <summary>The original common function button, including its smaller hitarea.</summary>
internal partial class UiSourceFunctionButton : UiSourceButton
{
    private Rect2 _hit;
    private UiSourceLayout source=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private Control? glyph;
    private string? currentIcon;
    private bool expansionCommonSprite;
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && _hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,string icon,Action pressed,
        bool expansionCommonSprite=false)
    {
        this.assets=assets;this.shader=shader;
        this.expansionCommonSprite=expansionCommonSprite;
        source = new UiSourceLayout(repository,0x2B7AA77D);
        Size = new(source.Nodes[0].Value44,source.Nodes[0].Value46);
        _hit = source.Rectangle(1);
        int[][] art = [[3],[5,6],[8,9],[11],[13,14]];
        var states = new Control[5];
        for (int i=0;i<5;i++)
        {
            var state = new Control {Name="State"+i,MouseFilter=MouseFilterEnum.Ignore};
            AddChild(state); states[i]=state;
            foreach (int node in art[i]) source.Art(state,node,source.Rectangle(node),assets,shader);
        }
        SetIcon(icon);
        var colors = MapSpritePalettes.Create();
        ConfigureStates(states,state =>
        {
            foreach (var part in glyph!.GetChildren().OfType<UiSourceSprite>()) part.SetColors(colors[25+state]);
        });
        Pressed += () => { if (!Disabled && SourceInputAdmitted) pressed(); };
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }

    internal void SetIcon(string icon)
    {
        if(currentIcon==icon)return;
        var replacement=source.Art(this,15,source.Rectangle(15),assets,shader,commonSpriteOverride:icon,
            expansionCommonSprite:expansionCommonSprite);
        foreach(var part in replacement.GetChildren().OfType<UiSourceSprite>())
            part.SetColors(MapSpritePalettes.Create()[25+VisualState]);
        if(glyph is not null){RemoveChild(glyph);glyph.QueueFree();}
        glyph=replacement;currentIcon=icon;
    }
}
