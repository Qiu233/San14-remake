using Godot;
using San14.Data;

/// <summary>The calculator's source button, distinct from the number opener.</summary>
internal partial class UiSourceCalculatorButton : UiSourceButton
{
    private Rect2 hitArea;
    internal override int VisualState=>Disabled?3:base.VisualState;
    public override bool _HasPoint(Vector2 point)=>SourceInputAdmitted&&hitArea.HasPoint(point);

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        string caption,bool closeIcon,Action pressed)
    {
        ActionMode=ActionModeEnum.Release;
        var source=new UiSourceLayout(repository,0xDE6DAAC5).ResizedSubtree(0,Size,true);
        hitArea=source.Rectangle(1);
        int[][] artwork=[[3],[5,6],[8,9],[11],[13,14]];
        var states=new Control[5];
        for(int i=0;i<states.Length;i++)
        {
            states[i]=new Control {MouseFilter=MouseFilterEnum.Ignore};AddChild(states[i]);
            foreach(int node in artwork[i])source.Art(states[i],node,source.Rectangle(node),assets,shader);
        }
        UiSourceText? label=null;Control? icon=null;
        var profile=MapFontProfile.FromNode(source.Nodes[16],source.Source.Source.ResourceUri) with
            {WidthFitMode=2,RawCharacterMode=2};
        if(closeIcon)icon=source.Art(this,17,source.Rectangle(17),assets,shader);
        else label=source.Text(this,16,default,caption,font);
        var presets=MapTextPresets.Create();var palettes=MapSpritePalettes.Create();
        ConfigureStates(states,state=>
        {
            int style=Math.Min(state,3);
            label?.Configure(font,profile.WithPreset(presets[3+style]));
            if(icon is not null)
                foreach(var part in icon.GetChildren().OfType<UiSourceSprite>())part.SetColors(palettes[17+style]);
        });
        Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)pressed();};
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
}
