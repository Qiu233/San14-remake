using Godot;
using San14.Data.Formats;

/// <summary>The header's two authored gauge layers at a static action seek.
/// It shares the original texture-mask composition with the area gauge.</summary>
internal partial class MapDoctrineGauge : Control
{
    private readonly UiSourceSprite[] bars=new UiSourceSprite[2];
    private readonly UiSourceGaugeCurve[] rotations=new UiSourceGaugeCurve[2],alphas=new UiSourceGaugeCurve[2];
    private int startFrame;
    private static readonly LcskUvQuad FullUv=new(new(0,0),new(1,0),new(0,1),new(1,1));

    internal void Configure(UiSourceLayout source,UiAssets assets,Shader shader)
    {
        Name="DoctrineGauge";MouseFilter=MouseFilterEnum.Ignore;Size=source.Rectangle(0).Size;
        var action=UiSourceGaugeCurve.ReadAction(source.Source.Layout,"gauge");
        if(action.End-action.Start!=100||!action.Nodes.SequenceEqual(new[]{20,21}))
            throw new InvalidDataException("Unsupported header doctrine gauge action.");
        startFrame=action.Start;
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("source_action","gauge");
        for(int i=0;i<2;i++)
        {
            int index=action.Nodes[i];var node=source.Nodes[index];
            var sprite=LcskSpriteNodeReader.Read(source.Source.Layout,node);
            var parts=sprite.Groups.Single().Parts;
            if(parts.Count!=2||parts.Any(p=>p.SpriteName!="000_cmn_gauge_syugi"||p.Uv!=FullUv)||
                parts[0].RawBytes[6]!=0||parts[0].RawBytes[7]!=0||parts[0].RawBytes[8]!=0||
                parts[1].RawBytes[6]!=2||parts[1].RawBytes[7]!=1||parts[1].RawBytes[8]!=0||
                sprite.RawTintBytes.Any(b=>b!=255))
                throw new InvalidDataException("Unsupported header doctrine gauge composition.");
            var rect=source.Rectangle(index);var f=node.HeaderValues;
            float x=f[3],y=f[4],z=f[5],w=f[6];
            float horizontal=1-2*(y*y+z*z),vertical=1-2*(x*x+z*z);
            if(Math.Abs(x*y+z*w)>0.00001f||Math.Abs(x*y-z*w)>0.00001f)
                throw new InvalidDataException("Unsupported header gauge planar projection.");
            Vector2 orientation=new(Math.Sign(horizontal),Math.Sign(vertical));
            var bar=new UiSourceSprite {Name="DoctrineGaugeBar"+i,Position=rect.Position+
                new Vector2(orientation.X<0?rect.Size.X:0,orientation.Y<0?rect.Size.Y:0),
                Size=rect.Size,Scale=orientation};AddChild(bar);
            var texture=assets.SrgbLayoutSpritePart(source.Source,parts[0].SpriteName,FullUv);
            bar.Configure(shader,texture,UiSourceSprite.Blend.GaugeUvMask);bar.SetGaugeComponent(texture);
            bar.SetMeta("source_node_index",index);bars[i]=bar;
            rotations[i]=UiSourceGaugeCurve.ForNode(source.Source.Layout,index);
            alphas[i]=UiSourceGaugeCurve.ForAlphaNode(source.Source.Layout,index);
        }
        Apply(null);
    }

    internal void Apply(int? percentage)
    {
        // A quotient outside this finite domain is retained by the query, but
        // the corresponding complete native action evaluation is not proved.
        Visible=percentage is >=0 and <=100;
        SetMeta("progress_known",Visible);
        if(!Visible)return;
        float time=(float)percentage!.Value+startFrame;
        for(int i=0;i<bars.Length;i++)
        {
            bars[i].SetGaugeUvRotation(rotations[i].Evaluate(time));
            byte alpha=alphas[i].EvaluateAlphaByte(time);
            bars[i].Modulate=new Color(1,1,1,alpha/255f);
            bars[i].SetMeta("source_alpha",alpha);
        }
        SetMeta("percentage",percentage.Value);SetMeta("source_frame",time);
    }
}
