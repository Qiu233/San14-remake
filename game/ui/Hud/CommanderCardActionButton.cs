using Godot;
using San14.Data;

/// <summary>The actual card automatic checkbox and roster-removal button.</summary>
internal partial class CommanderCardActionButton : UiSourceButton
{
    private Rect2 hit;
    private Control? check;
    public override bool _HasPoint(Vector2 point)=>SourceInputAdmitted&&hit.HasPoint(point);
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,bool automatic,Action released)
    {
        var source=new UiSourceLayout(repository,automatic?0x2570083Fu:0xBE97487Eu);
        Size=source.Rectangle(0).Size;hit=source.Rectangle(1);ActionMode=ActionModeEnum.Release;
        int[][] nodes=automatic?[[3],[5,6],[8,9],[11],[13,14]]:[[3],[5,6],[8],[10],[12,13]];
        var states=new Control[5];
        for(int i=0;i<states.Length;i++)
        {
            var state=new Control {Name="State"+i,MouseFilter=MouseFilterEnum.Ignore};AddChild(state);states[i]=state;
            foreach(int node in nodes[i])source.Art(state,node,source.Rectangle(node),assets,shader);
        }
        var glyph=source.Art(this,automatic?16:15,source.Rectangle(automatic?16:15),assets,shader);
        if(automatic)check=source.Art(this,17,source.Rectangle(17),assets,shader);
        var palettes=MapSpritePalettes.Create();
        ConfigureStates(states,state=>
        {foreach(var part in glyph.GetChildren().OfType<UiSourceSprite>())part.SetColors(palettes[25+state]);});
        Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)released();};
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
    internal void ApplyChecked(bool? value)
    {
        if(check is not null)check.Visible=value==true;
        SetMeta("automatic_state_known",value.HasValue);
    }
}
