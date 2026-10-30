using Godot;
using San14.Data;

/// <summary>The centered, explicitly separated original caption and guide
/// lines. It retains the authored align/arrange fields and fits all lines
/// with one profile rather than flattening LF or treating a block as one line.</summary>
internal partial class UiSourceMultilineText : Control, IUiSourceDrawing
{
    private MapFont font = null!;
    private MapFontProfile source = null!, fitted = null!;
    private IReadOnlyList<NarrationParagraphLine> lines = [];
    private float[] baselines = [];
    private IReadOnlyList<uint?>[] colours = [];
    private string? current;
    public void SetDrawingVisibilityLayer(uint layer) => font.SetDrawingVisibilityLayer(this, layer);

    internal void Configure(MapFont font, MapFontProfile profile)
    {
        if (profile.RawAlign != 0x11 || profile.RawLineArrange != 2 ||
            profile.WidthFitMode != 3 || profile.RawCharacterMode != 2)
            throw new InvalidDataException("Movie caption requires its authored centered multiline profile.");
        this.font = font; source = fitted = profile;
        MouseFilter = MouseFilterEnum.Ignore; SetNotifyTransform(true);
    }

    internal void ConfigureCommanderStatus(MapFont font,MapFontProfile profile)
    {
        bool warning=profile.WidthFitMode==3&&profile.RawCharacterMode==1;
        bool unlock=profile.WidthFitMode==2&&profile.RawCharacterMode==2;
        if(profile.RawAlign!=0x11||profile.RawLineArrange!=2||!warning&&!unlock)
            throw new InvalidDataException("Commander status requires its original centered source profile.");
        this.font=font;source=fitted=profile;
        MouseFilter=MouseFilterEnum.Ignore;SetNotifyTransform(true);
    }

    internal void ConfigureCommonGuide(MapFont font, MapFontProfile profile)
    {
        // CCmnNarration keeps node4's authored profile. Its string setter
        // preserves LF; fit2 measures the widest line without changing height.
        if (profile.RawAlign != 0x11 || profile.RawLineArrange != 2 ||
            profile.WidthFitMode != 2 || profile.RawCharacterMode != 2)
            throw new InvalidDataException("Common guide requires its original centered source profile.");
        this.font = font; source = fitted = profile;
        MouseFilter = MouseFilterEnum.Ignore; SetNotifyTransform(true);
    }

    internal void Apply(string value)
    {
        if (value == current) return;
        DeploymentNarrationPlainParagraph.RequireKnownColours(value);
        lines = DeploymentNarrationParagraphPlan.Runs(value);
        string widest = lines.MaxBy(line => font.Measure(line.VisibleText, source).Width)!.VisibleText;
        fitted = font.Source.FitToWidth(widest, source, Size.X) with { WidthFitMode = 0 };
        baselines = new float[lines.Count]; colours = new IReadOnlyList<uint?>[lines.Count];
        int nominal = font.Source.Record('\u3042', fitted.Face).Height;
        int baseline = (int)(nominal * fitted.ScaleY), top = 0;
        var presets = MapTextPresets.Create();
        for (int index = 0; index < lines.Count; index++)
        {
            var line = lines[index]; font.Prepare([line.VisibleText], fitted);
            baselines[index] = top + baseline;
            colours[index] = line.Runs.SelectMany(run => Enumerable.Repeat<uint?>(run.Colour switch
            {
                NarrationBodyColour.TextPreset38Body => presets[38].LT.CpuPacked,
                NarrationBodyColour.TextPreset39Body => presets[39].LT.CpuPacked,
                NarrationBodyColour.TextPreset40Body => presets[40].LT.CpuPacked,
                _ => null
            }, run.Text.Length)).ToArray();
            int advance = (int)(nominal * fitted.ScaleY + fitted.VerticalSpace);
            foreach (char code in line.VisibleText)
            {
                var glyph = font.Source.Record(code, fitted.Face);
                int difference = glyph.Height - glyph.HorizontalOffsetY;
                if (difference - fitted.VerticalSpace > 0)
                {
                    float raw = (glyph.Height + unchecked((byte)Math.Max(difference, 0))) * fitted.ScaleY + fitted.VerticalSpace;
                    advance = Math.Max(advance, (int)(raw < 0 ? raw - .5f : raw + .5f));
                }
            }
            top += unchecked((ushort)advance);
        }
        float offset = (Size.Y - top + fitted.VerticalSpace) * .5f;
        for (int index = 0; index < baselines.Length; index++) baselines[index] += offset;
        current = value; QueueRedraw();
    }
    public override void _Notification(int what)
    { if (what == NotificationTransformChanged || what == NotificationResized) QueueRedraw(); }
    public override void _Draw()
    {
        if (lines.Count == 0) return;
        var owner = font.BeginDraw(this);
        for (int index = 0; index < lines.Count; index++)
            font.Draw(this, lines[index].VisibleText, new(0, baselines[index]), fitted,
                HorizontalAlignment.Center, Size.X, glyphColours: colours[index]);
        owner.End();
    }
}
