using Godot;

internal readonly record struct UiSourceTextRun(string Text,uint? Colour=null);

/// <summary>A single original UI text line, borrowing its owning view's G1N font.</summary>
internal partial class UiSourceText : Control, IUiSourceDrawing
{
    private MapFont _font = null!;
    private MapFontProfile _profile = null!;
    private HorizontalAlignment _alignment;
    private string _text = "";
    private uint? _firstGlyphColour;
    private uint?[]? _glyphColours;
    internal MapFontProfile LayoutProfile => _font.Source.FitToWidth(_text, _profile, Size.X);
    public void SetDrawingVisibilityLayer(uint layer) => _font.SetDrawingVisibilityLayer(this, layer);
    internal uint? FirstGlyphColour
    {
        get => _firstGlyphColour;
        set { if (_firstGlyphColour == value) return; _firstGlyphColour = value; QueueRedraw(); }
    }

    internal string Text
    {
        get => _text;
        set
        {
            if (_text == value && _glyphColours is null) return;
            _font.Prepare([value], _profile);
            _text = value;_glyphColours=null;
            QueueRedraw();
        }
    }

    internal void SetRuns(params UiSourceTextRun[] runs)
    {
        string text=string.Concat(runs.Select(run=>run.Text));
        _font.Prepare([text],_profile);
        _text=text;_glyphColours=runs.SelectMany(run=>Enumerable.Repeat(run.Colour,run.Text.Length)).ToArray();
        _firstGlyphColour=null;
        QueueRedraw();
    }

    internal void Configure(MapFont font, MapFontProfile profile)
    {
        _font = font;
        _profile = profile;
        // Original AC30C0: high nibble selects left / centre / right;
        // the low nibble is consumed by the shared nominal-line baseline.
        _alignment = (profile.RawAlign >> 4) switch
        {
            0 => HorizontalAlignment.Left,
            1 => HorizontalAlignment.Center,
            2 => HorizontalAlignment.Right,
            _ => throw new InvalidDataException($"Unsupported UI text alignment: {profile.Source}.")
        };
        MouseFilter = MouseFilterEnum.Ignore;
        SetNotifyTransform(true);
    }

    public override void _Notification(int what)
    {
        // Geometry and the locally defined background copy move together.
        if (what == NotificationTransformChanged || what == NotificationResized) QueueRedraw();
    }

    public override void _Draw()
    {
        MapFontProfile profile = LayoutProfile;
        MapFont.DrawOwner owner = _font.BeginDraw(this);
        float baseline = _font.CellBaselineY(profile, Size.Y);
        if (_glyphColours is { } colours)
            _font.Draw(this,_text,new(0,baseline),profile,_alignment,Size.X,glyphColours:colours);
        else if (_firstGlyphColour is uint colour && _text.Length != 0)
        {
            // The detail-force string paints its leading source ■, then
            // resets the original text style. Fit/alignment use the full line.
            float width = _font.Measure(_text, profile).Width;
            float x = _alignment switch
            {
                HorizontalAlignment.Center => (Size.X - width) * .5f,
                HorizontalAlignment.Right => Size.X - width,
                _ => 0
            };
            var marker = profile with { ColourLT = colour, ColourRT = colour, ColourLB = colour, ColourRB = colour };
            _font.Draw(this, _text[..1], new(x, baseline), marker);
            x += (int)(_font.Source.Record(_text[0], profile.Face).HorizontalAdvance * profile.ScaleX) + profile.HorizontalSpace;
            _font.Draw(this, _text[1..], new(x, baseline), profile);
        }
        else _font.Draw(this, _text, new Vector2(0, baseline), profile, _alignment, Size.X);
        owner.End();
    }
}
