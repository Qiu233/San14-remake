using Godot;
using San14.Data;

/// <summary>The ordinary source paragraph shared by officer speech and report
/// cells. Wrapping and width fitting apply to the whole paragraph; its caller
/// supplies the original cell, profile and already expanded message.</summary>
internal partial class UiSourceParagraph : Control, IUiSourceDrawing
{
    private MapFont font = null!;
    private uint? drawingVisibilityLayer;
    private MapFontProfile original = null!, fitted = null!;
    private IReadOnlyList<NarrationParagraphLine> lines = [];
    private IReadOnlyList<float> baselines = [];
    private IReadOnlyList<IReadOnlyList<uint?>> colours = [];
    private IReadOnlyList<IReadOnlyList<San14.Data.MapTextPreset?>?> bodyPresets = [];
    private string? expanded;
    private Vector2 preparedSize;

    internal string WrappedText { get; private set; } = "";
    internal IReadOnlyList<NarrationParagraphLine> Lines => lines;
    internal MapFontProfile CurrentProfile => fitted;
    internal IReadOnlyList<float> Baselines => baselines;
    // Compositor assignment/restoration may precede this borrowed font.
    // Keep the requested layer for the first actual configured draw.
    public void SetDrawingVisibilityLayer(uint layer)
    {
        drawingVisibilityLayer = layer;
        if (font is not null) font.SetDrawingVisibilityLayer(this, layer);
    }

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
            profile.RawLineArrange is not (0 or 1) || profile.WidthFitMode is not (0 or 3) || profile.RawCharacterMode != 2)
            throw new InvalidDataException("Unsupported ordinary source paragraph profile.");
        font = sourceFont; original = profile; fitted = profile;
        if (drawingVisibilityLayer is uint layer) font.SetDrawingVisibilityLayer(this, layer);
        MouseFilter = MouseFilterEnum.Ignore; SetNotifyTransform(true);
    }

    // Startup and the live paragraph share the same line/fitting algorithm.
    // The resource worker prepares immutable pixels without creating Controls.
    private static (string WrappedText, IReadOnlyList<NarrationParagraphLine> Lines, MapFontProfile Profile)
        LayoutText(MapFontSource source, string text, MapFontProfile original, Vector2 size)
    {
        int WrapWidth(string line)
        {
            int raw = 0;
            foreach (char code in line) raw += source.Record(code, original.Face).HorizontalAdvance;
            return (int)(raw * original.ScaleX);
        }
        int nominal = source.Record('\u3042', original.Face).Height;
        int allowedLf = 0;
        while (allowedLf < 64 && (int)(unchecked((short)(nominal * (allowedLf + 2))) * original.ScaleY +
            original.VerticalSpace * allowedLf) <= (int)size.Y) allowedLf++;
        string wrapped = original.WidthFitMode == 0
            ? DeploymentNarrationPlainParagraph.WrapContainedKnownColourParagraph(
                text, line => source.Measure(line, original).Width, (int)size.X, allowPresetColours: true)
            : DeploymentNarrationPlainParagraph.WrapKnownColourParagraph(
                text, WrapWidth, (int)size.X, allowedLf, allowPresetColours: true);
        var preparedLines = DeploymentNarrationParagraphPlan.Runs(wrapped, allowPresetColours: true);
        string widest = preparedLines.MaxBy(line => source.Measure(line.VisibleText, original).Width)!.VisibleText;
        var profile = source.FitToWidth(widest, original, size.X) with { WidthFitMode = 0 };
        return (wrapped, preparedLines, profile);
    }

    internal static void PrepareLiteralText(MapFontSource font, string text, MapFontProfile profile, Vector2 size,
        IDictionary<MapFont.PreparedTextKey, MapTextCache> caches)
    {
        var layout = LayoutText(font, text, profile, size);
        var drawing = layout.Profile.UsesFixedLineHeight
            ? layout.Profile with { RawAlign = 0, RawLineArrange = 1 } : layout.Profile;
        foreach (var line in layout.Lines)
            MapFont.PrepareText(font, line.VisibleText, drawing, size.X, caches);
    }

    internal void Apply(string text)
    {
        if (expanded == text && preparedSize == Size) return;
        var layout = LayoutText(font.Source, text, original, Size);
        var wrapped = layout.WrappedText;
        var preparedLines = layout.Lines;
        var profile = layout.Profile;
        int nominal = font.Source.Record('\u3042', original.Face).Height;
        var presets = MapTextPresets.Create();
        var positions = new List<float>();
        var preparedColours = new List<IReadOnlyList<uint?>>();
        var preparedPresets = new List<IReadOnlyList<San14.Data.MapTextPreset?>?>();
        int baseline = (int)(nominal * profile.ScaleY), totalAdvance = 0;
        foreach (var line in preparedLines)
        {
            // Block alignment is consumed below. Each fixed-height glyph line
            // is prepared at its explicit baseline without a second alignment.
            font.Prepare([line.VisibleText], profile.UsesFixedLineHeight ? profile with { RawAlign = 0, RawLineArrange = 1 } : profile);
            positions.Add(totalAdvance + baseline);
            if (line.Runs.Any(run => run.BodyPresetIndex.HasValue))
            {
                // Preserve all four source body corners in the same whole-line
                // cache; splitting Draw calls would reorder shadow and stroke.
                preparedPresets.Add(line.Runs.SelectMany(run => Enumerable.Repeat(
                    run.SourcePresetIndex is int index ? presets[index] : null, run.Text.Length)).ToArray());
                preparedColours.Add(Array.Empty<uint?>());
            }
            else
            {
                preparedPresets.Add(null);
                preparedColours.Add(line.Runs.SelectMany(run => Enumerable.Repeat<uint?>(run.Colour switch
                {
                    NarrationBodyColour.TextPreset38Body => presets[38].LT.CpuPacked,
                    NarrationBodyColour.TextPreset39Body => presets[39].LT.CpuPacked,
                    NarrationBodyColour.TextPreset40Body => presets[40].LT.CpuPacked,
                    _ => null
                }, run.Text.Length)).ToArray());
            }
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
        baselines = positions; colours = preparedColours; bodyPresets = preparedPresets;
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
            font.Draw(this, lines[index].VisibleText, new(0, baselines[index]), fitted,
                glyphColours: bodyPresets[index] is null ? colours[index] : null,
                glyphBodyPresets: bodyPresets[index]);
        owner.End();
    }

    private static int Round(float value) => (int)(value < 0 ? value - .5f : value + .5f);
}
