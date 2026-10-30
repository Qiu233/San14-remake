using Godot;
using San14.Data.Formats;

/// <summary>The normal UI's two complementary source UV-rotation masks.
/// Each caller supplies its real action nodes and whether alpha tracks exist.</summary>
internal partial class UiSourceRadialGauge : Control
{
    private readonly UiSourceSprite[] bars=new UiSourceSprite[2];
    private readonly UiSourceGaugeCurve[] rotations=new UiSourceGaugeCurve[2];
    private readonly UiSourceGaugeCurve?[] alphas=new UiSourceGaugeCurve?[2];
    internal int StartFrame {get;private set;}
    private static readonly LcskUvQuad FullUv=new(new(0,0),new(1,0),new(0,1),new(1,1));
    internal void Configure(UiSourceLayout source,UiAssets assets,Shader shader,string actionName,
        int firstNode,int secondNode,bool alphaTracks)
        => ConfigureParts(source,assets,shader,actionName,firstNode,secondNode,alphaTracks,[firstNode,secondNode]);

    // CProgressButton's action also owns its structural parent and static ring
    // base. Its two animated texture-mask components are the actual nodes3/4.
    internal void ConfigureProgressButton(UiSourceLayout source,UiAssets assets,Shader shader)
        => ConfigureParts(source,assets,shader,"gauge",3,4,true,[1,2,3,4]);

    private void ConfigureParts(UiSourceLayout source,UiAssets assets,Shader shader,string actionName,
        int firstNode,int secondNode,bool alphaTracks,int[] actionMembers)
    {
        MouseFilter=MouseFilterEnum.Ignore;Size=source.Rectangle(0).Size;
        var action=UiSourceGaugeCurve.ReadAction(source.Source.Layout,actionName);
        if(action.End-action.Start!=100||firstNode==secondNode||
            !action.Nodes.Order().SequenceEqual(actionMembers.Order())||
            !action.Nodes.Contains(firstNode)||!action.Nodes.Contains(secondNode))
            throw new InvalidDataException("Radial gauge requires its two-node source action.");
        StartFrame=action.Start;
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("source_action",actionName);
        int[] nodes=[firstNode,secondNode];
        for(int i=0;i<2;i++)
        {
            int index=nodes[i];var node=source.Nodes[index];
            var sprite=LcskSpriteNodeReader.Read(source.Source.Layout,node);
            var parts=sprite.Groups.Single().Parts;
            if(parts.Count!=2||parts[0].SpriteName!=parts[1].SpriteName||parts.Any(part=>part.Uv!=FullUv)||
                parts[0].RawBytes[6]!=0||parts[0].RawBytes[7]!=0||parts[0].RawBytes[8]!=0||
                parts[1].RawBytes[6]!=2||parts[1].RawBytes[7]!=1||parts[1].RawBytes[8]!=0||
                sprite.RawTintBytes.Any(value=>value!=255))
                throw new InvalidDataException("Radial gauge source texture-mask composition differs.");
            var rect=source.Rectangle(index);var f=node.HeaderValues;
            float x=f[3],y=f[4],z=f[5],w=f[6];
            float horizontal=1-2*(y*y+z*z),vertical=1-2*(x*x+z*z);
            if(Math.Abs(x*y+z*w)>.00001f||Math.Abs(x*y-z*w)>.00001f)
                throw new InvalidDataException("Radial gauge requires the source axis-aligned planar projection.");
            Vector2 orientation=new(Math.Sign(horizontal),Math.Sign(vertical));
            var bar=new UiSourceSprite {Name="RadialGaugeBar"+index,Position=rect.Position+
                new Vector2(orientation.X<0?rect.Size.X:0,orientation.Y<0?rect.Size.Y:0),Size=rect.Size,Scale=orientation};AddChild(bar);
            var texture=assets.SrgbLayoutSpritePart(source.Source,parts[0].SpriteName,FullUv);
            bar.Configure(shader,texture,UiSourceSprite.Blend.GaugeUvMask);bar.SetGaugeComponent(texture);
            bar.SetMeta("source_node_index",index);bars[i]=bar;
            rotations[i]=UiSourceGaugeCurve.ForNode(source.Source.Layout,index);
            if(alphaTracks)alphas[i]=UiSourceGaugeCurve.ForAlphaNode(source.Source.Layout,index);
        }
    }
    internal void SetColors(San14.Data.MapCornerColors colors)
    { foreach(var bar in bars)bar.SetColors(colors); }

    internal void ApplySourceFrame(float frame)
    {
        for(int i=0;i<bars.Length;i++)
        {
            bars[i].SetGaugeUvRotation(rotations[i].Evaluate(frame));
            if(alphas[i] is{} curve)
            {
                byte alpha=curve.EvaluateAlphaByte(frame);
                bars[i].Modulate=new(1,1,1,alpha/255f);bars[i].SetMeta("source_alpha",alpha);
            }
        }
        SetMeta("source_frame",frame);
    }
}
