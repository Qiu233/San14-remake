using Godot;
using San14.Data;

/// <summary>The source common bottom button, with its inset hit area.</summary>
internal partial class UiSourceBottomButton : UiSourceButton
{
    private Rect2 hit;
    private UiSourceText _label = null!;
    public override bool _HasPoint(Vector2 point)=>hit.HasPoint(point);

    internal void ApplyCaption(string caption) => _label.Text = caption;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,
        MapFont font,string caption,Action released)
    {
        var source=new UiSourceLayout(repository,0xCEE6F358);
        Size=source.Rectangle(0).Size;hit=source.Rectangle(1);ActionMode=ActionModeEnum.Release;
        int[] nodes=[3,5,8,11,13];
        var states=nodes.Select(node=>source.Art(this,node,source.Rectangle(node),assets,shader)).ToArray();
        _label=source.Text(this,16,default,caption,font);
        var profile=MapFontProfile.FromNode(source.Nodes[16],source.Source.Source.ResourceUri);
        var presets=MapTextPresets.Create();
        ConfigureStates(states,state=>
        {_label.Configure(font,profile.WithPreset(presets[3+state]));_label.QueueRedraw();});
        Pressed+=()=>{if(!Disabled)released();};
        // The footer owns the device guide; node17 is hidden by the source.
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
}
