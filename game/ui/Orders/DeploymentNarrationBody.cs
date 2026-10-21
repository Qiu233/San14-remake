using Godot;
using San14.Data;

/// <summary>One source narration paragraph. Page splitting precedes wrapping;
/// all lines share the page's fitted profile and one borrowed G1N draw owner.</summary>
internal partial class DeploymentNarrationBody : Control, IUiSourceDrawing
{
    private sealed record Page(NarrationParagraphPage Source,MapFontProfile Profile,
        IReadOnlyList<float> Baselines,IReadOnlyList<IReadOnlyList<uint?>> Colours);
    private MapFont font=null!;
    private IReadOnlyList<Page> pages=[];
    private int pageIndex,publication,drawnPublication;
    internal event Action<int>? PageDrawn;
    internal int PageCount=>pages.Count;
    internal int Publication=>publication;
    internal NarrationParagraphPage CurrentPage=>pages[pageIndex].Source;
    internal MapFontProfile CurrentProfile=>pages[pageIndex].Profile;
    internal IReadOnlyList<float> CurrentBaselines=>pages[pageIndex].Baselines;
    public void SetDrawingVisibilityLayer(uint layer) => font.SetDrawingVisibilityLayer(this, layer);

    internal void Configure(MapFont sourceFont,MapFontProfile profile,string expanded)
    {
        if(profile.Face!=1||profile.Palette!=0||profile.RawFlags!=0||profile.RawAlign!=0||
            profile.RawLineArrange!=1||profile.ScaleX!=1||profile.ScaleY!=1||
            profile.HorizontalSpace!=0||profile.VerticalSpace!=12||Size!=new Vector2(672,120))
            throw new InvalidDataException("Narration requires its original ordinary body profile.");
        font=sourceFont;MouseFilter=MouseFilterEnum.Ignore;SetNotifyTransform(true);
        var baseProfile=profile with{WidthFitMode=3,RawCharacterMode=2};
        var plans=DeploymentNarrationParagraphPlan.Create(expanded,line=>font.Measure(line,baseProfile).Width);
        if(plans.All(page=>page.Lines.All(line=>line.VisibleText.Length==0)))
            throw new InvalidDataException("Deployment narration has no visible source message.");
        var presets=MapTextPresets.Create();var nameColour=presets[38];var areaColour=presets[39];
        if(new[]{nameColour,areaColour}.Any(colour=>colour.LT.CpuPacked!=colour.RT.CpuPacked||
            colour.LT.CpuPacked!=colour.LB.CpuPacked||colour.LT.CpuPacked!=colour.RB.CpuPacked))
            throw new InvalidDataException("Narration name colour requires its source corner-colour consumer.");
        pages=plans.Select(Prepare).ToArray();
        ShowPage(0);

        Page Prepare(NarrationParagraphPage plan)
        {
            int available=(int)Size.X-(int)baseProfile.WidthReserve;
            float factor=plan.MaximumRawWidth>available?available/(float)plan.MaximumRawWidth*.98f:1;
            var fitted=baseProfile with{ScaleX=baseProfile.ScaleX*factor,
                HorizontalSpace=unchecked((short)(int)(baseProfile.HorizontalSpace*factor)),WidthFitMode=0};
            var baselines=new List<float>();var colours=new List<IReadOnlyList<uint?>>();
            int nominalHeight=font.Source.Record('\u3042',fitted.Face).Height;
            int baseline=(int)(nominalHeight*fitted.ScaleY),top=0;
            foreach(var line in plan.Lines)
            {
                font.Prepare([line.VisibleText],fitted);
                baselines.Add(top+baseline);
                colours.Add(line.Runs.SelectMany(run=>Enumerable.Repeat<uint?>(
                    run.Colour switch{NarrationBodyColour.TextPreset38Body=>nameColour.LT.CpuPacked,
                        NarrationBodyColour.TextPreset39Body=>areaColour.LT.CpuPacked,_=>null},run.Text.Length)).ToArray());
                int advance=(int)(nominalHeight*fitted.ScaleY+fitted.VerticalSpace);
                foreach(char code in line.VisibleText)
                {
                    var glyph=font.Source.Record(code,fitted.Face);
                    int difference=glyph.Height-glyph.HorizontalOffsetY;
                    // AD6140/AC2040: this is the original line-advance rule,
                    // not the visible glyph bounding box or a fixed 44px step.
                    if(difference-fitted.VerticalSpace>0)
                        advance=Math.Max(advance,Round((glyph.Height+unchecked((byte)Math.Max(difference,0)))*
                            fitted.ScaleY+fitted.VerticalSpace));
                }
                top+=advance;
            }
            return new(plan,fitted,baselines,colours);
        }
    }

    internal void ShowPage(int index)
    {
        if((uint)index>=(uint)pages.Count)throw new ArgumentOutOfRangeException(nameof(index));
        pageIndex=index;publication++;QueueRedraw();
    }

    public override void _Notification(int what)
    {if(what==NotificationTransformChanged||what==NotificationResized)QueueRedraw();}

    public override void _Draw()
    {
        if(pages.Count==0)return;
        var current=pages[pageIndex];var owner=font.BeginDraw(this);
        for(int index=0;index<current.Source.Lines.Count;index++)
            font.Draw(this,current.Source.Lines[index].VisibleText,new(0,current.Baselines[index]),
                current.Profile,glyphColours:current.Colours[index]);
        owner.End();
        if(drawnPublication==publication)return;
        drawnPublication=publication;PageDrawn?.Invoke(publication);
    }

    private static int Round(float value)=>(int)(value<0?value-.5f:value+.5f);
}
