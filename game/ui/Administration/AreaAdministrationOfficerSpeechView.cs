using Godot;
using San14.Data;

/// <summary>The region page's source node97, with its own ordinary paragraph
/// profile. The presenter prepares the message once per resolved person.</summary>
internal partial class AreaAdministrationOfficerSpeechView : Control
{
    private AreaAdministrationSpeechBody body=null!;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font)
    {
        MouseFilter=MouseFilterEnum.Ignore;
        var source=new UiSourceLayout(repository,0xC7A85B99);
        source.Art(this,1,source.Rectangle(1),assets,shader);
        var rectangle=source.Rectangle(2);
        var profile=MapFontProfile.FromNode(source.Nodes[2],source.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[0]) with {RawAlign=0,RawLineArrange=1,WidthFitMode=3,RawCharacterMode=2};
        body=new AreaAdministrationSpeechBody{Name="SpeechBody",Position=rectangle.Position,Size=rectangle.Size};
        AddChild(body);body.Configure(font,profile);
        SetMeta("source_layout",source.Source.Source.ResourceUri);Hide();
    }

    internal void ApplyText(string? expanded)
    {
        SetMeta("speech_available",true);
        if(expanded is null){body.Apply("");Hide();return;}
        body.Apply(expanded);Show();
    }

    internal void ApplyUnknown(string label)
    {
        body.Apply(label);SetMeta("speech_available",false);Show();
    }
}

internal partial class AreaAdministrationSpeechBody : Control
{
    private MapFont font=null!;
    private MapFontProfile original=null!,fitted=null!;
    private IReadOnlyList<NarrationParagraphLine> lines=[];
    private IReadOnlyList<float> baselines=[];
    private IReadOnlyList<IReadOnlyList<uint?>> colours=[];
    private string? expanded;
    internal string WrappedText {get;private set;}="";
    internal IReadOnlyList<NarrationParagraphLine> Lines=>lines;
    internal MapFontProfile CurrentProfile=>fitted;
    internal IReadOnlyList<float> Baselines=>baselines;

    internal void Configure(MapFont sourceFont,MapFontProfile profile)
    {
        font=sourceFont;original=profile;fitted=profile;
        MouseFilter=MouseFilterEnum.Ignore;SetNotifyTransform(true);
    }

    internal void Apply(string text)
    {
        if(expanded==text)return;
        // 92E1A0 measures at raw scale1; 30D290 scales the completed sum.
        // The final width fit below intentionally uses the renderer's other
        // measure, which truncates each scaled glyph before adding it.
        int WrapWidth(string line)
        {
            int raw=0;
            foreach(char code in line)raw+=font.Source.Record(code,original.Face).HorizontalAdvance;
            return (int)(raw*original.ScaleX);
        }
        int nominal=font.Source.Record('\u3042',original.Face).Height;
        int allowedLf=0;
        while(allowedLf<64&&(int)((nominal*(allowedLf+2))*original.ScaleY+
            original.VerticalSpace*allowedLf)<=(int)Size.Y)allowedLf++;
        WrappedText=DeploymentNarrationPlainParagraph.WrapKnownColourParagraph(text,WrapWidth,(int)Size.X,allowedLf);
        lines=DeploymentNarrationParagraphPlan.Runs(WrappedText);
        string widest=lines.MaxBy(line=>font.Measure(line.VisibleText,original).Width)!.VisibleText;
        fitted=font.Source.FitToWidth(widest,original,Size.X) with {WidthFitMode=0};
        var presets=MapTextPresets.Create();
        var nameColour=presets[38];var areaColour=presets[39];
        var positions=new List<float>();var runs=new List<IReadOnlyList<uint?>>();
        int baseline=(int)(nominal*fitted.ScaleY),top=0;
        foreach(var line in lines)
        {
            font.Prepare([line.VisibleText],fitted);positions.Add(top+baseline);
            runs.Add(line.Runs.SelectMany(run=>Enumerable.Repeat<uint?>(run.Colour switch
                {NarrationBodyColour.TextPreset38Body=>nameColour.LT.CpuPacked,
                 NarrationBodyColour.TextPreset39Body=>areaColour.LT.CpuPacked,_=>null},run.Text.Length)).ToArray());
            int advance=(int)(nominal*fitted.ScaleY+fitted.VerticalSpace);
            foreach(char code in line.VisibleText)
            {
                var glyph=font.Source.Record(code,fitted.Face);
                int difference=glyph.Height-glyph.HorizontalOffsetY;
                if(difference-fitted.VerticalSpace>0)
                    advance=Math.Max(advance,Round((glyph.Height+unchecked((byte)Math.Max(difference,0)))*
                        fitted.ScaleY+fitted.VerticalSpace));
            }
            top+=advance;
        }
        baselines=positions;colours=runs;expanded=text;QueueRedraw();
    }

    public override void _Notification(int what)
    {if(what==NotificationTransformChanged||what==NotificationResized)QueueRedraw();}

    public override void _Draw()
    {
        if(lines.Count==0)return;
        var owner=font.BeginDraw(this);
        for(int index=0;index<lines.Count;index++)
            font.Draw(this,lines[index].VisibleText,new(0,baselines[index]),fitted,glyphColours:colours[index]);
        owner.End();
    }

    private static int Round(float value)=>(int)(value<0?value-.5f:value+.5f);
}
