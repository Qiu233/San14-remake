using Godot;
using System.Buffers.Binary;
using San14.Data;
using San14.Data.Formats;

/// <summary>The diplomacy common gauge at its authored static action end.
/// Probability changes the actual coloured pane width; endpoint animation is deferred.</summary>
internal partial class UiSourceDiplomacyGauge : Control
{
    private static readonly Vector2 SourceSize = new(540,32);
    private Control? canvas;
    private UiSourceSprite senderBar=null!,recipientBar=null!,endpointBar=null!;
    private Rect2 barRectangle;
    private int? probability;
    private bool endpoint;
    private AreaMarkerColor? senderFill,recipientFill,endpointFill;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader)
    {
        MouseFilter=MouseFilterEnum.Ignore;
        if(Size.X<=0||Size.Y<=0)Size=SourceSize;
        var source=new UiSourceLayout(repository,0x5415A7CC);
        var content=new Control {Name="SourceGauge",Size=SourceSize,MouseFilter=MouseFilterEnum.Ignore};
        canvas=content;AddChild(content);ResizeSource();
        source.Art(content,2,source.Rectangle(2),assets,shader);
        source.Art(content,3,source.Rectangle(3),assets,shader);

        var masked=LcskSpriteNodeReader.Read(source.Source.Layout,source.Nodes[4]);
        var parts=masked.Groups.Single().Parts;
        if(parts.Count!=2||parts[0].SpriteName!="06_str_bar"||parts[1].SpriteName!="06_str_bar_mask"||
            parts[0].RawBytes[6]!=0||parts[1].RawBytes[6]!=13)
            throw new InvalidDataException("Unsupported diplomacy gauge mask composition.");
        var action=UiSourceGaugeCurve.ReadAction(source.Source.Layout,"06_str_gauge");
        var maskOffset=new Vector2(
            UiSourceGaugeCurve.ForUvOffsetNode(source.Source.Layout,4).Evaluate(action.End),
            UiSourceGaugeCurve.ForUvOffsetNode(source.Source.Layout,4,true).Evaluate(action.End));
        var rectangle=source.Rectangle(4);
        var texture=assets.SrgbLayoutSpritePart(source.Source,parts[0].SpriteName,parts[0].Uv);
        var mask=assets.SrgbLayoutSpritePart(source.Source,parts[1].SpriteName,parts[1].Uv);
        var image=new UiSourceSprite {Name="SourceGaugeMask",Position=rectangle.Position,Size=rectangle.Size};
        content.AddChild(image);
        image.Configure(shader,texture,UiSourceSprite.Blend.TranslatedAlphaMask,Tint(masked.RawTintBytes));
        image.SetClipping(texture,mask,new(Vector2.Zero,rectangle.Size));
        image.SetMaskUvOffset(maskOffset);

        recipientBar=Bar(5);senderBar=Bar(6);endpointBar=Bar(7);
        barRectangle=source.Rectangle(7);
        var loop=UiSourceGaugeCurve.ReadAction(source.Source.Layout,"loop_gauge");
        endpointBar.Modulate=new Color(1,1,1,
            UiSourceGaugeCurve.ForAlphaNode(source.Source.Layout,7).EvaluateAlphaByte(loop.Start)/255f);
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SetMeta("source_action","06_str_gauge");SetMeta("source_frame",action.End);
        if(senderFill is { } sender&&recipientFill is { } recipient)
        {senderBar.SetColors(Solid(sender));recipientBar.SetColors(Solid(recipient));}
        Refresh();

        UiSourceSprite Bar(int index)
        {
            var sprite=LcskSpriteNodeReader.Read(source.Source.Layout,source.Nodes[index]);
            var part=sprite.Groups.Single().Parts.Single();
            var rect=source.Rectangle(index);
            var bar=new UiSourceSprite {Name=source.Nodes[index].Name,Position=rect.Position,Size=rect.Size};
            content.AddChild(bar);
            bar.Configure(shader,assets.SrgbLayoutSpritePart(source.Source,part.SpriteName,part.Uv),
                UiSourceSprite.Blend.Ordinary,Tint(sprite.RawTintBytes));
            bar.SetMeta("source_node_index",index);return bar;
        }
    }

    //85B1F0: pane6 takes the sender's fill, pane5 the recipient's fill.
    internal void ApplyForceColors(AreaMarkerColor sender,AreaMarkerColor recipient)
    {
        senderFill=sender;recipientFill=recipient;
        if(canvas is not null)
        {
            senderBar.SetColors(Solid(sender));recipientBar.SetColors(Solid(recipient));Refresh();
        }
    }

    /// <param name="endpoint">The source valid/player Force gate, evaluated by the page owner.</param>
    internal void Apply(int? percentage,bool endpoint,AreaMarkerColor? endpointFill)
    {
        probability=percentage;this.endpoint=endpoint;this.endpointFill=endpointFill;
        if(canvas is not null)Refresh();
    }

    private void Refresh()
    {
        Visible=probability.HasValue&&senderFill.HasValue&&recipientFill.HasValue;
        SetMeta("progress_known",Visible);
        if(!Visible)return;
        int raw=probability!.Value,p=Math.Clamp(raw,0,100);
        int width=(int)(barRectangle.Size.X*p/100f);
        senderBar.Size=new(width,barRectangle.Size.Y);
        bool atEndpoint=endpoint&&(raw is 0 or 100)&&endpointFill.HasValue;
        senderBar.Visible=recipientBar.Visible=!atEndpoint;
        endpointBar.Visible=atEndpoint;
        if(atEndpoint)endpointBar.SetColors(Solid(endpointFill!.Value));
        SetMeta("percentage",raw);SetMeta("source_fill_width",width);SetMeta("source_endpoint",atEndpoint);
    }

    public override void _Notification(int what)
    {if(what==NotificationResized)ResizeSource();}

    private void ResizeSource()
    {if(canvas is not null)canvas.Scale=Size/SourceSize;}

    private static MapCornerColors Solid(AreaMarkerColor color)=>new(color,color,color,color);
    private static MapCornerColors Tint(byte[] bytes)
    {
        AreaMarkerColor Read(int offset)=>MapSpritePalettes.FromAuthored(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset,4)));
        return new(Read(0),Read(4),Read(8),Read(12));
    }
}
