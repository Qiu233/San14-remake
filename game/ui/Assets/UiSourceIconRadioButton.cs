using Godot;
using San14.Data;

/// <summary>The original style6 icon radio used by the three map speeds.</summary>
internal partial class UiSourceIconRadioButton : UiSourceButton
{
    private Rect2 hit;
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,
        int speed,Action released)
    {
        if((uint)speed>2)throw new ArgumentOutOfRangeException(nameof(speed));
        var source=new UiSourceLayout(repository,0x27447F69);
        Size=source.Rectangle(0).Size;hit=source.Rectangle(1);ActionMode=ActionModeEnum.Release;
        int[][] nodes=[[3],[5,6],[8,9],[11],[13,14]];
        var states=new Control[5];
        for(int state=0;state<states.Length;state++)
        {
            var layer=new Control {Name="State"+state,MouseFilter=MouseFilterEnum.Ignore};
            AddChild(layer);states[state]=layer;
            foreach(int node in nodes[state])
                source.Art(layer,node,source.Rectangle(node),assets,shader,
                    commonSpriteOverride:state<4?$"000_cmn_btn_spd{speed+1}_{state}":null);
        }
        ConfigureStates(states);
        Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)released();};
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SetMeta("source_command",2+speed*2);
    }
}
