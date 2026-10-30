using Godot;
using San14.Data;

/// <summary>CCmnBtnFncCheckBehavior, including its independent check frame.</summary>
internal partial class UiSourceFunctionCheckButton : UiSourceButton
{
    private Rect2 hit;
    private Control check=null!;
    internal override bool Selected
    {
        get=>base.Selected;
        set {base.Selected=value;if(check is not null)check.Visible=value;}
    }
    public override bool _HasPoint(Vector2 point)=>SourceInputAdmitted&&hit.HasPoint(point);
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,string icon,Action action)
    {
        var source=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"cmn_btn_fnc_check"));
        Size=source.Rectangle(0).Size;hit=source.Rectangle(1);
        int[][] artwork=[[3],[5,6],[8,9],[11],[13,14]];
        var states=new Control[5];
        for(int i=0;i<states.Length;i++)
        {
            var layer=new Control{Name="State"+i,MouseFilter=MouseFilterEnum.Ignore};AddChild(layer);states[i]=layer;
            foreach(int node in artwork[i])source.Art(layer,node,source.Rectangle(node),assets,shader);
        }
        var glyph=source.Art(this,15,source.Rectangle(15),assets,shader,commonSpriteOverride:icon,expansionCommonSprite:true);
        check=source.Art(this,16,source.Rectangle(16),assets,shader);check.Visible=Selected;
        var palettes=MapSpritePalettes.Create();
        ConfigureStates(states,state=>
        {
            foreach(var part in glyph.GetChildren().OfType<UiSourceSprite>())part.SetColors(palettes[25+state]);
        });
        Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)action();};
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
}
