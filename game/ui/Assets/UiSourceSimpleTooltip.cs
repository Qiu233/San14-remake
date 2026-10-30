using Godot;
using San14.Data;

/// <summary>The original cmn_tooltip. Its controller uses nominal half/full
/// character widths; all rendered lines retain one shared font fit.</summary>
internal partial class UiSourceSimpleTooltip : Control
{
    private UiSourceLayout source=null!;
    private UiAssets assets=null!;
    private MapFont font=null!;
    private Shader shader=null!;
    private MapFontProfile profile=null!;
    private Control? artwork;
    private UiSourceType6 blur = null!;
    private readonly List<UiSourceText> lines=[];
    private float nominalHeight;
    private string? current;
    private bool currentKnownColours;
    private bool followPointer;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        short? lineSpacing = null, bool followPointer = true)
    {
        Name="SourceSimpleTooltip";ZIndex=20;MouseFilter=MouseFilterEnum.Ignore;
        this.assets=assets;this.shader=shader;this.font=font;
        this.followPointer=followPointer;
        source=new(repository,0xBE0DC488);
        blur=source.Blur(this,1,source.Rectangle(1));
        profile=MapFontProfile.FromNode(source.Nodes[3],source.Source.Source.ResourceUri);
        // 89D8E0 explicitly clears compact+5D and +2C. It does not apply
        // text preset0 to this authored face0/palette0 profile.
        profile=profile with{RawCharacterMode=0,RawFlags=profile.RawFlags&~4u};
        if(lineSpacing is short spacing) profile=profile with{VerticalSpace=spacing};
        nominalHeight=font.Source.Record('\u3042',0).Height*profile.ScaleY;
        SetMeta("source_layout",source.Source.Source.ResourceUri);Hide();
    }

    internal void DisplayText(string? value)
        => Display(value, false);

    internal void DisplayKnownColourText(string? value)
        => Display(value, true);

    private void Display(string? value, bool knownColours)
    {
        if(value is null){Hide();return;}
        string text=value.Length==0?" ":value;
        if(current!=text || currentKnownColours!=knownColours)
        {
            string[] paragraphs=text.Split('\n');
            // Source formatting atoms require their own text consumer; never
            // paint them as literal glyphs or silently remove their semantics.
            if(!knownColours && text.Any(code=>code=='^'||char.IsControl(code)&&code!='\n'))
                throw new InvalidDataException("Simple tooltip requires expanded plain LF text.");
            if (knownColours) DeploymentNarrationPlainParagraph.RequireKnownColours(text);
            var colourLines = knownColours ? DeploymentNarrationParagraphPlan.Runs(text) : null;
            string[] visible = colourLines?.Select(line => line.VisibleText).ToArray() ?? paragraphs;
            var size=MeasureSource(string.Join('\n', visible),nominalHeight,profile.VerticalSpace);
            Size=new(size.X+20,size.Y+4);
            blur.Size=Size;
            if(artwork is { } old){old.Hide();old.Name="PreviousTooltipArtwork";old.QueueFree();}
            var backgroundColour=new AreaMarkerColor(0xB4FFFFFF,0xB4FFFFFF);
            artwork=source.Art(this,2,new(Vector2.Zero,Size),assets,shader,
                new MapCornerColors(backgroundColour,backgroundColour,backgroundColour,backgroundColour));
            artwork.Name="TooltipBackground";MoveChild(artwork,1);
            int widest=visible.Max(line=>font.Measure(line,profile).Width);
            int available=size.X-(int)profile.WidthReserve;
            float factor=widest>available?available/(float)widest*.98f:1;
            var fitted=profile with{ScaleX=profile.ScaleX*factor,
                HorizontalSpace=unchecked((short)(int)(profile.HorizontalSpace*factor)),WidthFitMode=0};
            while(lines.Count<paragraphs.Length)
            {
                var line=new UiSourceText{Name="TooltipLine"+lines.Count};AddChild(line);lines.Add(line);
            }
            int top=0;
            for(int index=0;index<lines.Count;index++)
            {
                var line=lines[index];line.Visible=index<paragraphs.Length;
                if(!line.Visible)continue;
                line.Position=source.Rectangle(3).Position+new Vector2(0,top);
                line.Size=new(size.X,(int)nominalHeight);line.Configure(font,fitted);
                if (colourLines is not null)
                {
                    var presets = MapTextPresets.Create();
                    line.SetRuns(colourLines[index].Runs.Select(run => new UiSourceTextRun(run.Text, run.Colour switch
                    {
                        NarrationBodyColour.TextPreset38Body => presets[38].LT.CpuPacked,
                        NarrationBodyColour.TextPreset39Body => presets[39].LT.CpuPacked,
                        NarrationBodyColour.TextPreset40Body => presets[40].LT.CpuPacked,
                        _ => (uint?)null
                    })).ToArray());
                }
                else line.Text=paragraphs[index];
                // Another line can change the shared fit while this line's
                // text, position and size stay identical.
                line.QueueRedraw();
                top+=LineAdvance(visible[index],fitted);
            }
            current=text; currentKnownColours=knownColours;
        }
        Show();
        if(followPointer)UpdatePointer(GetParent<Control>().GetLocalMousePosition());
    }

    // 3098C0 truncates each line maximum and accumulated nominal height,
    // then 8A6C10 adds compact.short24 spacing using that truncated height.
    internal static Vector2I MeasureSource(string text,float height,short verticalSpace)
    {
        float width=0,totalHeight=height;int maximum=0;
        for(int index=0;index<text.Length;index++)
        {
            char code=text[index];
            if(code=='^')
            {
                if(index+1>=text.Length)throw new InvalidDataException("Incomplete tooltip control atom.");
                // Actual 3098F0..30990C skips two UTF16 units here. This
                // nominal measure is distinct from font-format expansion.
                index++;continue;
            }
            if(code=='\n'){maximum=Math.Max(maximum,(int)width);width=0;totalHeight+=height;continue;}
            if(char.IsControl(code))throw new InvalidDataException("Unsupported tooltip control character.");
            bool half=unchecked((ushort)(code-0x20))<0x5F||unchecked((ushort)(code+0x9F))<0x3F;
            width+=half?height*.5f:height;
        }
        maximum=Math.Max(maximum,(int)width);
        int pixels=(int)totalHeight;
        return new(maximum,pixels+((int)(pixels/height)-1)*verticalSpace);
    }

    private int LineAdvance(string text,MapFontProfile fitted)
    {
        int nominal=font.Source.Record('\u3042',fitted.Face).Height;
        int advance=(int)(nominal*fitted.ScaleY+fitted.VerticalSpace);
        foreach(char code in text)
        {
            var glyph=font.Source.Record(code,fitted.Face);
            int difference=glyph.Height-glyph.HorizontalOffsetY;
            if(difference-fitted.VerticalSpace>0)
            {
                float value=(glyph.Height+unchecked((byte)Math.Max(difference,0)))*fitted.ScaleY+fitted.VerticalSpace;
                advance=Math.Max(advance,(int)(value<0?value-.5f:value+.5f));
            }
        }
        return advance;
    }

    internal void UpdatePointer(Vector2 pointer)
    {
        int x=(int)pointer.X+20,y=(int)pointer.Y+20;
        x=x<0?0:Math.Min(x,1920-(int)Size.X);
        y=y<0?0:Math.Min(y,1080-(int)Size.Y);
        Position=new(x,y);
    }
}
