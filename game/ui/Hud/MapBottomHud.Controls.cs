using Godot;
using San14.Data;

internal partial class MapBottomSwitchButton : UiSourceButton
{
    private Rect2 hit;
    private Control check=null!;
    internal override UiAudioCue PressedCue=>UiAudioCue.Tab;
    internal override bool Selected {get=>base.Selected;set{base.Selected=value;if(check is not null)check.Visible=value;}}
    public override bool _HasPoint(Vector2 point)=>SourceInputAdmitted&&hit.HasPoint(point);
    internal void Configure(MapSummaryResources resources,string layout,Action pressed)
    {
        var source=resources.Layout(layout);Size=source.Rectangle(0).Size;hit=source.Rectangle(1);
        int[][] layers=[[3],[5,6],[8,9],[11],[13,14]];
        var art=new UiSourceButtonStateArt(this,source,resources.Assets,resources.Shader,layers,"BottomSwitchState");
        var glyph=source.Art(this,16,source.Rectangle(16),resources.Assets,resources.Shader);
        check=source.Art(this,17,source.Rectangle(17),resources.Assets,resources.Shader);check.Visible=Selected;
        // This custom switch delegates disabled-focus to its disable callback.
        // Its node16 sprite writer therefore differs from generic function buttons.
        int[] glyphPalettes=[25,26,27,28,28];
        ConfigureStates(art.States,state=>
        {
            art.ApplyState(state);
            foreach(var part in glyph.GetChildren().OfType<UiSourceSprite>())
                part.SetColors(resources.Palettes[glyphPalettes[state]]);
        });ActionMode=ActionModeEnum.Release;
        Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)pressed();};
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
}

internal partial class MapBottomFunctionButton : UiSourceButton
{
    private Rect2 hit;
    private Control? check;
    private Control glyph=null!;
    private Control? alternateGlyph;
    internal override int VisualState=>check is null?base.VisualState:InputVisualState;
    internal override bool Selected{get=>base.Selected;set{base.Selected=value;if(check is not null)check.Visible=value;}}
    public override bool _HasPoint(Vector2 point)=>SourceInputAdmitted&&hit.HasPoint(point);
    internal void Configure(MapSummaryResources resources,string icon,Action pressed,bool checkable=false,bool expansion=false,
        string? alternateIcon=null)
    {
        var source=resources.Layout(checkable?"cmn_btn_fnc_check":"cmn_btn_fnc");
        Size=source.Rectangle(0).Size;hit=source.Rectangle(1);
        int[][] layers=[[3],[5,6],[8,9],[11],[13,14]];
        var art=new UiSourceButtonStateArt(this,source,resources.Assets,resources.Shader,layers,"BottomFunctionState",
            new UiSourceButtonStateProfile(source.Source,checkable?
                ["0_normal","1_focus","2_push ","3_disable","4_disable_focus"]:["btn_0","btn_1","btn_2","btn_3","btn_4"]));
        glyph=source.Art(this,15,source.Rectangle(15),resources.Assets,resources.Shader,
            commonSpriteOverride:icon,expansionCommonSprite:expansion);
        if(alternateIcon is not null)
        {
            alternateGlyph=source.Art(this,15,source.Rectangle(15),resources.Assets,resources.Shader,
                commonSpriteOverride:alternateIcon,expansionCommonSprite:expansion);
            alternateGlyph.Hide();
        }
        if(checkable){check=source.Art(this,16,source.Rectangle(16),resources.Assets,resources.Shader);check.Visible=Selected;}
        ConfigureStates(art.States,state=>
        {
            art.ApplyState(state);
            foreach(var part in glyph.GetChildren().OfType<UiSourceSprite>())part.SetColors(resources.Palettes[25+state]);
            if(alternateGlyph is not null)
                foreach(var part in alternateGlyph.GetChildren().OfType<UiSourceSprite>())part.SetColors(resources.Palettes[25+state]);
        });
        ActionMode=ActionModeEnum.Release;Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)pressed();};
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
    internal void ShowAlternateIcon(bool value)
    {glyph.Visible=!value;if(alternateGlyph is not null)alternateGlyph.Visible=value;}
}

internal partial class MapBottomFilterButton : UiSourceButton
{
    private Rect2 hit;private Control check=null!;
    internal override int VisualState=>InputVisualState;
    internal override bool Selected{get=>base.Selected;set{base.Selected=value;if(check is not null)check.Visible=value;}}
    public override bool _HasPoint(Vector2 point)=>SourceInputAdmitted&&hit.HasPoint(point);
    internal void Configure(MapSummaryResources resources,string caption,Action pressed)
    {
        var source=resources.Layout("cmn_btn_hanyou_s_check").ResizedSubtree(0,Size,true);hit=source.Rectangle(1);
        int[][] layers=[[3],[5,6],[8,9],[11],[13,14]];
        var art=new UiSourceButtonStateArt(this,source,resources.Assets,resources.Shader,layers,"LogFilterState",
            new UiSourceButtonStateProfile(source.Source,["0_normal","1_focus ","2_push","3_disable","4_disable_focus"]));
        var text=source.Text(this,16,default,caption,resources.Font);
        var profile=MapFontProfile.FromNode(source.Nodes[16],source.Source.Source.ResourceUri);
        check=source.Art(this,17,source.Rectangle(17),resources.Assets,resources.Shader);check.Visible=Selected;
        ConfigureStates(art.States,state=>{art.ApplyState(state);text.Configure(resources.Font,profile.WithPreset(resources.Presets[28+state]));});
        ActionMode=ActionModeEnum.Release;Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)pressed();};
    }
}
