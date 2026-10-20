using Godot;
using San14.Data;

/// <summary>The one null sentinel appended to the source summary list.</summary>
internal partial class DeploymentAddRow : UiSourceButton
{
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,Action add)
    {
        var source=new UiSourceLayout(repository,0xB5E86370);
        Size=new(source.Nodes[0].Value44,source.Nodes[0].Value46);
        var states=Enumerable.Range(0,5).Select(state=>source.Art(this,2,source.Rectangle(2),assets,shader,
            commonSpriteOverride:$"000_cmn_btn_hanyo_l_{Math.Min(state,3)}")).ToArray();
        source.Art(this,4,source.Rectangle(4),assets,shader);
        var plus=source.Art(this,5,source.Rectangle(5),assets,shader);
        var presets=MapTextPresets.Create();
        ConfigureStates(states,state=>
        {
            var preset=presets[3+Math.Min(state,3)];
            var colors=new MapCornerColors(preset.LT,preset.RT,preset.LB,preset.RB);
            foreach(var part in plus.GetChildren().OfType<UiSourceSprite>())part.SetColors(colors);
        });
        Pressed+=()=>{if(!Disabled)add();};
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
}
