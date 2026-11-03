using Godot;
using San14.Data;

internal readonly record struct UiSourceTextRun(string Text,uint? Colour=null);

/// <summary>A single original UI text line, borrowing its owning view's G1N font.</summary>
internal partial class UiSourceText : Control, IUiSourceDrawing
{
    private static readonly MapTextPresets InlinePresets = MapTextPresets.Create();
    private MapFont _font = null!;
    private uint? _drawingVisibilityLayer;
    private MapFontProfile _profile = null!;
    private HorizontalAlignment _alignment;
    private string _text = "";
    private string? _personnelPreviewLine;
    private bool _officePreviewColours;
    private uint? _firstGlyphColour;
    private uint?[]? _glyphColours;
    private string? _preparedInlineColourSource;
    internal MapFontProfile LayoutProfile => _font.Source.FitToWidth(_text, _profile, Size.X);
    // Compositor assignment/restoration may precede this borrowed font.
    // Keep the requested layer for the first actual configured draw.
    public void SetDrawingVisibilityLayer(uint layer)
    {
        _drawingVisibilityLayer = layer;
        if (_font is not null) _font.SetDrawingVisibilityLayer(this, layer);
    }
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
            _personnelPreviewLine = null;
            _preparedInlineColourSource = null;
            if (_text == value && _glyphColours is null) return;
            _font.Prepare([value], _profile);
            _text = value;_glyphColours=null;
            QueueRedraw();
        }
    }

    internal void SetRuns(params UiSourceTextRun[] runs)
    {
        _preparedInlineColourSource = null;
        _personnelPreviewLine = null;
        string text=string.Concat(runs.Select(run=>run.Text));
        _font.Prepare([text],_profile);
        _text=text;_glyphColours=runs.SelectMany(run=>Enumerable.Repeat(run.Colour,run.Text.Length)).ToArray();
        _firstGlyphColour=null;
        QueueRedraw();
    }

    // Static source-colour captions borrow their startup pixels by the
    // explicit expanded resource string, never by hashing glyph arrays.
    internal void SetPreparedColourRuns(string source, params UiSourceTextRun[] runs)
    {
        SetRuns(runs);
        _preparedInlineColourSource = source;
    }

    // The personnel preview writer emits only these two controls. App's
    // colour7 callback uses (argument-1), so ~1~ takes preset0's body;
    // it does not change the font, stroke or shadow. Preset0 is solid at
    // all four corners. A null run restores the owning row's current body.
    internal void SetPersonnelPreviewLine(string source)=>SetPreviewColourLine(source,false);

    //309990 encodes preset+1. The office writers use presets65/67,
    //therefore their actual source controls carry arguments66/68.
    internal void SetOfficePreviewLine(string source)=>SetPreviewColourLine(source,true);

    private void SetPreviewColourLine(string source,bool officeColours)
    {
        if (_personnelPreviewLine == source && _officePreviewColours == officeColours) return;
        var runs = new List<UiSourceTextRun>();
        var text = new System.Text.StringBuilder();
        uint? colour = null;
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] == '^')
            {
                Flush();
                if(source.AsSpan(index).StartsWith("^00~0~",StringComparison.Ordinal))
                {colour=null;index+=5;}
                else if(source.AsSpan(index).StartsWith("^07~",StringComparison.Ordinal))
                {
                    int end=source.IndexOf('~',index+4);
                    if(end<0 || !int.TryParse(source.AsSpan(index+4,end-index-4),out int argument) ||
                        !(argument==1 || officeColours && argument is 66 or 68))
                        throw new InvalidDataException("Unresolved preview body-colour argument.");
                    colour=InlinePresets[argument-1].LT.CpuPacked;index=end;
                }
                else throw new InvalidDataException("Unresolved personnel preview text control.");
            }
            else if (source[index] == '~')
                throw new InvalidDataException("Unresolved personnel preview text argument.");
            else text.Append(source[index]);
        }
        Flush();
        if (runs.All(run => run.Colour is null)) Text = string.Concat(runs.Select(run => run.Text));
        else SetRuns(runs.ToArray());
        _personnelPreviewLine = source;_officePreviewColours=officeColours;

        void Flush()
        {
            if (text.Length == 0) return;
            runs.Add(new(text.ToString(), colour));text.Clear();
        }
    }

    /// <summary>The ordinary Fit2 report line removes line breaks, then
    /// retains the source body-colour controls while fitting the whole line.</summary>
    internal void SetKnownColourLine(string expanded)
    {
        string flattened = expanded.Replace("\r\n", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
        DeploymentNarrationPlainParagraph.RequireKnownColours(flattened);
        var line = DeploymentNarrationParagraphPlan.Runs(flattened).Single();
        if (line.Runs.All(run => run.Colour == NarrationBodyColour.OriginalNodeBody))
        {
            FirstGlyphColour = null;
            Text = line.VisibleText;
            return;
        }
        var presets = MapTextPresets.Create();
        SetRuns(line.Runs.Select(run => new UiSourceTextRun(run.Text, run.Colour switch
        {
            NarrationBodyColour.TextPreset38Body => presets[38].LT.CpuPacked,
            NarrationBodyColour.TextPreset39Body => presets[39].LT.CpuPacked,
            NarrationBodyColour.TextPreset40Body => presets[40].LT.CpuPacked,
            _ => (uint?)null
        })).ToArray());
    }

    internal void Configure(MapFont font, MapFontProfile profile)
    {
        bool changed = !ReferenceEquals(_font, font) || _profile != profile;
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
        if (_drawingVisibilityLayer is uint layer) _font.SetDrawingVisibilityLayer(this, layer);
        // A caption keeps its text across hover and disabled-state changes.
        // Invalidate its retained draw commands when the style changes too.
        if (changed) QueueRedraw();
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
            _font.Draw(this,_text,new(0,baseline),profile,_alignment,Size.X,glyphColours:colours,inlineColourSource:_preparedInlineColourSource);
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
