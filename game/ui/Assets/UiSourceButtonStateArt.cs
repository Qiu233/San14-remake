using Godot;

/// <summary>Owned ordinary/additive panes for one button. Parsed resource
/// endpoints are borrowed from its page's UiAssets; no independent timer lives
/// beyond the control and no state redraw restarts an animation.</summary>
internal sealed class UiSourceButtonStateArt
{
    private readonly UiSourceButtonStateProfile profile;
    private readonly List<(int Node,Control Root,UiSourceSprite[] Sprites)> bindings=[];
    private int state=-1;
    internal Control[] States { get; }

    internal UiSourceButtonStateArt(Control owner,UiSourceLayout source,UiAssets assets,Shader shader,
        IReadOnlyList<int[]> layers,string name)
    {
        profile=assets.ButtonStateProfile(source.Source);
        if(layers.Count!=5)throw new ArgumentException("Button art requires five semantic states.",nameof(layers));
        States=new Control[layers.Count];
        for(int index=0;index<layers.Count;index++)
        {
            var group=new Control{Name=name+index,Size=owner.Size,MouseFilter=Control.MouseFilterEnum.Ignore};
            owner.AddChild(group);States[index]=group;
            foreach(int node in layers[index])
            {
                var art=source.Art(group,node,source.Rectangle(node),assets,shader,
                    blend:profile.BlendAt(node)==0x00114400?UiSourceSprite.Blend.Additive:UiSourceSprite.Blend.Ordinary);
                bindings.Add((node,art,SpriteParts(art).ToArray()));
            }
        }
    }

    internal void ApplyState(int next)
    {
        if(state==next)return;
        state=next;
        foreach(var (node,root,sprites) in bindings)
        {
            byte alpha=profile.AlphaAt(next,node);
            root.Visible=alpha!=0;
            foreach(var sprite in sprites)sprite.SetSourceAlpha(alpha);
        }
    }

    private static IEnumerable<UiSourceSprite> SpriteParts(Node root)
    {
        if(root is UiSourceSprite sprite)yield return sprite;
        foreach(Node child in root.GetChildren())
            foreach(var part in SpriteParts(child))yield return part;
    }
}
