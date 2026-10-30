using Godot;
using San14.Data;

/// <summary>The AUTab's CUnitMakeUISelectButtonBody uses cmn_btn_list, including
/// its own resized hit area, state art, text box and text presets8..11.</summary>
internal partial class DeploymentTacticButton : UiSourceButton
{
    private Rect2 hit;
    public override bool _HasPoint(Vector2 point)=>SourceInputAdmitted&&hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        DeploymentTacticChoice choice,Action<int> choose)
    {
        ActionMode=ActionModeEnum.Release;
        var body=new UiSourceLayout(repository,0x5794E0A4).ResizedSubtree(0,Size,true);
        hit=body.Rectangle(1);
        var states=new[]{3,5,8,11,13}.Select(node=>body.Art(this,node,body.Rectangle(node),assets,shader)).ToArray();
        var label=body.Text(this,16,default,choice.Name,font);
        var profile=MapFontProfile.FromNode(body.Nodes[16],body.Source.Source.ResourceUri);
        var presets=MapTextPresets.Create();
        ConfigureStates(states,state=>
        {label.Configure(font,profile.WithPreset(presets[8+Math.Min(state,3)]));label.QueueRedraw();});
        Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)choose(choice.Occurrence);};
        SetMeta("tactic_id",choice.TacticId);SetMeta("tactic_occurrence",choice.Occurrence);
        SetMeta("source_layout",body.Source.Source.ResourceUri);
    }
}
