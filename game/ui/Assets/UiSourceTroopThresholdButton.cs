using Godot;
using San14.Data;

/// <summary>The original three/five troop threshold indicator. Its caption is
/// a classification, while the caller owns the actual troop value.</summary>
internal partial class UiSourceTroopThresholdButton : UiSourceButton
{
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        int classification,Action pressed)
    {
        if(classification is not (3 or 5))throw new ArgumentOutOfRangeException(nameof(classification));
        var source=new UiSourceLayout(repository,0xBCF8A04D);
        Size=source.Rectangle(0).Size;
        ActionMode=ActionModeEnum.Release;
        // Focus glow uses a distinct additive material; its animation is
        // deferred rather than compositing it with ordinary source alpha.
        int[][] artwork=[[3],[5],[8],[11],[13]];
        var states=new Control[5];
        for(int i=0;i<states.Length;i++)
        {
            states[i]=new Control {MouseFilter=MouseFilterEnum.Ignore};AddChild(states[i]);
            foreach(int node in artwork[i])source.Art(states[i],node,source.Rectangle(node),assets,shader);
        }
        var caption=source.Text(this,15,default,classification.ToString(System.Globalization.CultureInfo.InvariantCulture),font);
        var profile=MapFontProfile.FromNode(source.Nodes[15],source.Source.Source.ResourceUri);
        var presets=MapTextPresets.Create();
        ConfigureStates(states,state=>caption.Configure(font,profile.WithPreset(presets[3+state])));
        Pressed+=()=>{if(!Disabled)pressed();};
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
}
