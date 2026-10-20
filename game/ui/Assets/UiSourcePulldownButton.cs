using Godot;
using San14.Data;

/// <summary>The first, middle and last common pulldown rows share their own
/// caption styles, independently of the ordinary bottom buttons.</summary>
internal partial class UiSourcePulldownButton : UiSourceButton
{
    private Rect2 hit;
    public override bool _HasPoint(Vector2 point)=>hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,
        MapFont font,uint sourceLayout,string text,Action released)
    {
        if(sourceLayout is not (0xF0E45A19 or 0xE1B56178 or 0xD28668D7))
            throw new ArgumentOutOfRangeException(nameof(sourceLayout));
        // 855700 creates these rows with recursive resize flag0.
        var source=new UiSourceLayout(repository,sourceLayout).ResizedSubtree(0,Size,false);
        Size=source.Rectangle(0).Size;var area=source.Rectangle(1);
        hit=new(new Vector2((int)area.Position.X,(int)area.Position.Y),
            new Vector2((int)area.Size.X,(int)area.Size.Y));
        ActionMode=ActionModeEnum.Release;
        var states=new[]{4,6,9,11,13}.Select(node=>source.Art(this,node,source.Rectangle(node),assets,shader)).ToArray();
        // The source blur and additive animation have separate draw paths.
        var caption=source.Text(this,15,default,text,font);caption.Name="Caption";
        var profile=MapFontProfile.FromNode(source.Nodes[15],source.Source.Source.ResourceUri);
        var presets=MapTextPresets.Create();int[] styles=[57,58,59,60,60];
        ConfigureStates(states,state=>
        {caption.Configure(font,profile.WithPreset(presets[styles[state]]));caption.QueueRedraw();});
        Pressed+=()=>{if(!Disabled)released();};
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
}
