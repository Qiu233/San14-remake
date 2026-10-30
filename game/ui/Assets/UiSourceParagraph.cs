using Godot;
using San14.Data;

/// <summary>The ordinary source paragraph shared by officer speech and report
/// cells. Wrapping and width fitting apply to the whole paragraph; its caller
/// supplies the original cell, profile and already expanded message.</summary>
internal partial class UiSourceParagraph : Control, IUiSourceDrawing
{
    private MapFont font = null!;
    private MapFontProfile original = null!, fitted = null!;
    private IReadOnlyList<NarrationParagraphLine> lines = [];
    private IReadOnlyList<float> baselines = [];
    private IReadOnlyList<IReadOnlyList<uint?>> colours = [];
    private string? expanded;
    private Vector2 preparedSize;

    internal string WrappedText { get; private set; } = "";
    internal IReadOnlyList<NarrationParagraphLine> Lines => lines;
    internal MapFontProfile CurrentProfile => fitted;
    internal IReadOnlyList<float> Baselines => baselines;
    public void SetDrawingVisibilityLayer(uint layer) => font.SetDrawingVisibilityLayer(this, layer);

    /// <summary>The formatted-message caller wraps the expanded text and then
    /// explicitly selects width-fit mode 3 and literal character mode 2. These
    /// runtime setters replace the authored text node's initial modes.</summary>
    internal void ConfigureFormattedMessage(MapFont sourceFont, MapFontProfile resourceProfile) =>
        Configure(sourceFont, resourceProfile with { WidthFitMode = 3, RawCharacterMode = 2 });

    internal void Configure(MapFont sourceFont, MapFontProfile profile)
    {
        // These are the ordinary 30D290 cells. Ruby, font switches and other
        // alignment modes need their own source consumers.
        if (profile.RawAlign is not (0 or 1) ||
            profile.RawLineArrange != 1 || profile.WidthFitMode is not (0 or 3) || profile.RawCharacterMode != 2)
            throw new InvalidDataException("Unsupported ordinary source paragraph profile.");
        font = sourceFont; original = profile; fitted = profile;
        MouseFilter = MouseFilterEnum.Ignore; SetNotifyTransform(true);
    }

    internal void Apply(string text)
    {
        if (expanded == text && preparedSize == Size) return;
        int WrapWidth(string line)
        {
            int raw = 0;
            foreach (char code in line) raw += font.Source.Record(code, original.Face).HorizontalAdvance;
            return (int)(raw * original.ScaleX);
        }
        int nominal = font.Source.Record('\u3042', original.Face).Height;
        int allowedLf = 0;
        while (allowedLf < 64 && (int)(unchecked((short)(nominal * (allowedLf + 2))) * original.ScaleY +
            original.VerticalSpace * allowedLf) <= (int)Size.Y) allowedLf++;
        string wrapped = DeploymentNarrationPlainParagraph.WrapKnownColourParagraph(
            text, WrapWidth, (int)Size.X, allowedLf);
        var preparedLines = DeploymentNarrationParagraphPlan.Runs(wrapped);
        string widest = preparedLines.MaxBy(line => font.Measure(line.VisibleText, original).Width)!.VisibleText;
        var profile = font.Source.FitToWidth(widest, original, Size.X) with { WidthFitMode = 0 };
        var presets = MapTextPresets.Create();
        var positions = new List<float>();
        var preparedColours = new List<IReadOnlyList<uint?>>();
        int baseline = (int)(nominal * profile.ScaleY), totalAdvance = 0;
        foreach (var line in preparedLines)
        {
            // Block alignment is consumed below. Each fixed-height glyph line
            // is prepared at its explicit baseline without a second alignment.
            font.Prepare([line.VisibleText], profile.UsesFixedLineHeight ? profile with { RawAlign = 0 } : profile);
            positions.Add(totalAdvance + baseline);
            preparedColours.Add(line.Runs.SelectMany(run => Enumerable.Repeat<uint?>(run.Colour switch
            {
                NarrationBodyColour.TextPreset38Body => presets[38].LT.CpuPacked,
                NarrationBodyColour.TextPreset39Body => presets[39].LT.CpuPacked,
                NarrationBodyColour.TextPreset40Body => presets[40].LT.CpuPacked,
                _ => null
            }, run.Text.Length)).ToArray());
            int advance = (int)(nominal * profile.ScaleY + profile.VerticalSpace);
            foreach (char code in profile.UsesFixedLineHeight ? "" : line.VisibleText)
            {
                var glyph = font.Source.Record(code, profile.Face);
                int difference = glyph.Height - glyph.HorizontalOffsetY;
                if (difference - profile.VerticalSpace > 0)
                    advance = Math.Max(advance, Round((glyph.Height + unchecked((byte)Math.Max(difference, 0))) *
                        profile.ScaleY + profile.VerticalSpace));
            }
            // AD5F20 accumulates the prepared line's UInt16 advance. The
            // admitted ordinary slice has no ruby/above-line contribution.
            totalAdvance += unchecked((ushort)advance);
        }
        if (profile.RawAlign == 1)
        {
            int offset = -(int)((profile.VerticalSpace - totalAdvance + Size.Y) * -0.5f);
            for (int index = 0; index < positions.Count; index++) positions[index] += offset;
        }
        WrappedText = wrapped; lines = preparedLines; fitted = profile;
        baselines = positions; colours = preparedColours;
        expanded = text; preparedSize = Size; QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && font is not null && expanded is { } text) Apply(text);
        if (what == NotificationTransformChanged || what == NotificationResized) QueueRedraw();
    }

    public override void _Draw()
    {
        if (lines.Count == 0) return;
        var owner = font.BeginDraw(this);
        for (int index = 0; index < lines.Count; index++)
            font.Draw(this, lines[index].VisibleText, new(0, baselines[index]), fitted, glyphColours: colours[index]);
        owner.End();
    }

    private static int Round(float value) => (int)(value < 0 ? value - .5f : value + .5f);
}
