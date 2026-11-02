using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record AreaAdministrationDevelopmentValues(int? Commerce,int? Agriculture,int? Barracks,
    int? Focus,int? CommerceGain,int? AgricultureGain,int? BarracksGain);

/// <summary>Three original development displays. These consume a preview
/// publication; the image and circular gauge always use current development.</summary>
internal partial class AreaAdministrationDevelopmentView : Control
{
    private sealed record Item(Control Root,UiSourceSprite Image,UiSourceText Progress,Control Gauge,
        UiSourceText Level,Control Maximum,UiSourceSprite[] Bars,UiSourceGaugeCurve[] Curves,string Prefix);
    private readonly List<Item> items=[];
    private readonly Dictionary<string,Texture2D> developmentImages=[];
    private UiAssets assets=null!;
    private string unknown="",arrow="",levelPattern="";
    private uint previewColour,normalColour;
    private int startFrame;
    private static readonly LcskUvQuad FullUv=new(new(0,0),new(1,0),new(0,1),new(1,1));

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,PreviewText text)
    {
        this.assets=assets;MouseFilter=MouseFilterEnum.Ignore;
        var panel=new UiSourceLayout(repository,0xC4464DC9);Size=panel.Rectangle(0).Size;
        var source=new UiSourceLayout(repository,0xC043267A);
        var gaugeSource=new UiSourceLayout(repository,0x3C47C086);
        var presets=MapTextPresets.Create();
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        unknown=text.Get("detail_unknown");arrow=messages.Records[0x056F].Text??throw new InvalidDataException("Missing development arrow.");
        levelPattern=messages.Records[0x0384].Text??throw new InvalidDataException("Missing development level pattern.");
        previewColour=presets[39].LT.CpuPacked;normalColour=presets[0].LT.CpuPacked;
        var tint=new AreaMarkerColor(0xFF0F69FF,0xFF0F69FF);
        var colours=new MapCornerColors(tint,tint,tint,tint);
        var action=UiSourceGaugeCurve.ReadAction(gaugeSource.Source.Layout,"gauge_1_ani");
        if(action.End-action.Start!=100||!action.Nodes.Order().SequenceEqual(new[]{3,4}))
            throw new InvalidDataException("Unsupported circular gauge action.");
        startFrame=action.Start;
        string[] prefixes=["commerce","agriculture","barracks"];
        for(int i=0;i<3;i++)
        {
            var root=new Control {Name="Development"+(i+1),Position=panel.Rectangle(155+i).Position,
                Size=source.Rectangle(0).Size,MouseFilter=MouseFilterEnum.Ignore};AddChild(root);
            root.SetMeta("source_mount_node",155+i);
            var image=new UiSourceSprite {Name="DevelopmentImage",Position=source.Rectangle(1).Position,Size=source.Rectangle(1).Size};root.AddChild(image);
            // No dummy image is displayed while the current value is unknown.
            image.Visible=false;
            var progress=source.Text(root,2,default,unknown,font,presets[27]);progress.Name="DevelopmentProgress";
            // Replacement type8 positions a fresh generic layout. It does
            // not copy the removed placeholder's 80px size or .8 scale.
            var gauge=new Control {Name="DevelopmentGauge",Position=source.Rectangle(3).Position,Size=gaugeSource.Rectangle(0).Size,
                MouseFilter=MouseFilterEnum.Ignore};root.AddChild(gauge);
            gaugeSource.Art(gauge,1,gaugeSource.Rectangle(1),assets,shader);
            var bars=new UiSourceSprite[2];var curves=new UiSourceGaugeCurve[2];
            var barTexture=assets.SrgbCommonSpritePart("000_cmn_gauge_circle_bar",FullUv);
            for(int b=0;b<2;b++)
            {
                int node=3+b;var rect=gaugeSource.Rectangle(node);
                var components=LcskSpriteNodeReader.Read(gaugeSource.Source.Layout,gaugeSource.Nodes[node]).Groups.Single().Parts;
                if(components.Count!=2||components.Any(p=>p.SpriteName!="000_cmn_gauge_circle_bar"||p.Uv!=FullUv)||
                    components[0].RawBytes[6]!=0||components[0].RawBytes[7]!=0||components[0].RawBytes[8]!=0||
                    components[1].RawBytes[6]!=2||components[1].RawBytes[7]!=1||components[1].RawBytes[8]!=0)
                    throw new InvalidDataException("Unsupported circular gauge texture composition.");
                var bar=new UiSourceSprite {Name="GaugeBar"+b,Position=rect.Position,Size=rect.Size};gauge.AddChild(bar);
                if(b==1){bar.Position+=rect.Size;bar.Scale=-Vector2.One;}
                bar.Configure(shader,barTexture,UiSourceSprite.Blend.GaugeUvMask,colours);
                bar.SetGaugeComponent(barTexture);bars[b]=bar;
                curves[b]=UiSourceGaugeCurve.ForNode(gaugeSource.Source.Layout,node);
            }
            var level=gaugeSource.Text(gauge,11,default,unknown,font);level.Name="DevelopmentLevel";
            var maximum=source.Art(root,7,source.Rectangle(7),assets,shader,commonSpriteOverride:"cmn_mrk_max");
            maximum.Name="DevelopmentMaximum";
            items.Add(new(root,image,progress,gauge,level,maximum,bars,curves,prefixes[i]));
            // Bind the shader before the first known publication while keeping
            // all unknown imagery hidden; source image0 is not an unknown icon.
            image.Configure(shader,Image("texture_city_"+prefixes[i]+"_0"),UiSourceSprite.Blend.Ordinary);
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        Apply(new(null,null,null,null,null,null,null));
    }

    internal void Apply(AreaAdministrationDevelopmentValues values)
    {
        int?[] points=[values.Commerce,values.Agriculture,values.Barracks];
        int?[] gains=[values.CommerceGain,values.AgricultureGain,values.BarracksGain];
        for(int i=0;i<items.Count;i++)
        {
            var item=items[i];
            if(points[i] is not int p)
            {item.Image.Hide();item.Gauge.Hide();item.Maximum.Hide();item.Progress.Show();item.Progress.Text=unknown;continue;}
            if(p<0||p>1000)throw new InvalidDataException("Development display is outside 0..1000.");
            int imageIndex=p==0?0:p<100?1:p<300?2:p<600?3:p<1000?4:5;
            string name="texture_city_"+item.Prefix+"_"+imageIndex;
            item.Image.SetSource(Image(name));item.Image.Show();
            item.Maximum.Visible=p==1000;item.Gauge.Visible=p<1000;item.Progress.Visible=p<1000;
            if(p==1000)continue;
            int level=p<100?1:p<300?2:p<600?3:4;
            int lower=level switch{1=>0,2=>100,3=>300,_=>600};
            int span=level*100,current=p-lower;
            item.Level.Text=levelPattern.Replace("%d",Number(level),StringComparison.Ordinal);
            int frame=startFrame+(int)(Math.Clamp((float)current/span,0f,1f)*100f);
            for(int b=0;b<2;b++)item.Bars[b].SetGaugeUvRotation(item.Curves[b].Evaluate(frame));
            int? gain=values.Focus is int focus ? focus==i+1?gains[i]:0 : null;
            if(gain is null)
                item.Progress.SetRuns(new(Number(current).PadLeft(4)),new(arrow),new(unknown,previewColour),new("/"+Number(span).PadLeft(4),normalColour));
            else if(gain<=0)item.Progress.Text=Number(current).PadLeft(4)+"/"+Number(span).PadLeft(4);
            else
            {
                int after=unchecked(current+gain.Value);if(level>=4)after=Math.Clamp(after,0,span);
                item.Progress.SetRuns(new(Number(current).PadLeft(4)),new(arrow),new(Number(after).PadLeft(4),previewColour),
                    new("/"+Number(span).PadLeft(4),normalColour));
            }
        }
    }
    private Texture2D Image(string name)
    {
        if(!developmentImages.TryGetValue(name,out var image))
        {
            image=assets.SrgbTexturePackSpritePart("texture_stl_city",name,FullUv);
            developmentImages.Add(name,image);
        }
        return image;
    }
    private static string Number(int value)=>value.ToString(CultureInfo.InvariantCulture);
}
