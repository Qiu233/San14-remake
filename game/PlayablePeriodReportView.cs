using Godot;
using San14.Data;

/// <summary>
/// Read-only report presentation. Acknowledgement is delegated to the campaign;
/// opening, resizing and scrolling this view never run the simulation again.
/// The shell uses original artwork with a responsive client layout.
/// </summary>
public partial class PlayablePeriodReportView : Control
{
    private Control _canvas = null!;
    private Label _title = null!;
    private Label _status = null!;
    private Label _returnText = null!;
    private RichTextLabel _body = null!;
    private TextureButton _acknowledge = null!;
    private Action? _finish;
    private bool _acknowledging;

    internal void Configure(OriginalGameRepository repository, PreviewText text,
        string status, string body, int period, Action finish)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(finish);
        Name = "PlayablePeriodReportView";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _finish = finish;
        AddChild(new ColorRect
        {
            Name = "ReportBackdrop", Color = new Color(0.03f, 0.025f, 0.02f, 0.88f),
            MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1
        });
        _canvas = new Control
        {
            Size = new Vector2(1920, 1080), MouseFilter = MouseFilterEnum.Stop,
            Theme = new Theme
            {
                DefaultFont = new SystemFont { FontNames = ["KaiTi", "DFKai-SB", "Microsoft YaHei"] }
            }
        };
        AddChild(_canvas);
        var assets = new OriginalUiAssets(repository);
        AddPicture(assets.Sprite("000_cmn_base_title").Texture, new Rect2(0, 0, 540, 88));
        _title = AddLabel("ReportTitle", new Rect2(115, 23, 380, 48), 32, new Color("#eee3a6"));
        _status = AddLabel("ReportPeriod", new Rect2(200, 120, 1520, 48), 26, new Color("#efdfb4"));
        var panel = new Panel
        {
            Name = "ReportPanel", Position = new Vector2(190, 188), Size = new Vector2(1540, 704),
            MouseFilter = MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("#3c3025"), BorderColor = new Color("#98814d"),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2
        });
        _canvas.AddChild(panel);
        _body = new RichTextLabel
        {
            Name = "ReportEntries", Position = new Vector2(26, 22), Size = new Vector2(1488, 660),
            BbcodeEnabled = false, ScrollActive = true, FitContent = false,
            MouseFilter = MouseFilterEnum.Stop, SelectionEnabled = true
        };
        _body.AddThemeFontSizeOverride("normal_font_size", 26);
        _body.AddThemeColorOverride("default_color", new Color("#efdfb4"));
        _body.AddThemeConstantOverride("line_separation", 9);
        panel.AddChild(_body);
        _acknowledge = new TextureButton
        {
            Name = "AcknowledgePeriodReport", Position = new Vector2(896, 944), Size = new Vector2(128, 128),
            TextureNormal = assets.Sprite("000_cmn_btn_decision_0").Texture,
            TextureHover = assets.Sprite("000_cmn_btn_decision_1").Texture,
            TexturePressed = assets.Sprite("000_cmn_btn_decision_2").Texture,
            IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.Scale
        };
        _canvas.AddChild(_acknowledge);
        _acknowledge.Pressed += RequestAcknowledge;
        _returnText = AddLabel("ReportReturnText", new Rect2(916, 985, 88, 44), 30, new Color("#35261b"));
        _returnText.HorizontalAlignment = HorizontalAlignment.Center;
        Refresh(text, status, body, period);
        Resized += FitCanvas;
        FitCanvas();
        _acknowledge.GrabFocus();
    }

    internal void Refresh(PreviewText text, string status, string body, int period)
    {
        _title.Text = text.Get("play_report_heading");
        _status.Text = status;
        // Preserve scroll and selected text across unchanged world/UI refreshes.
        if (_body.Text != body) _body.Text = body;
        _returnText.Text = text.Get("detail_return");
        _acknowledge.TooltipText = text.Get("play_finish_report");
        SetMeta("report_period", period);
        SetMeta("report_locale", text.SourceLocale);
        SetMeta("report_body", body);
        SetMeta("report_read_only", true);
    }

    internal void RequestAcknowledge()
    {
        if (_acknowledging || _finish is null || !IsVisibleInTree() || IsQueuedForDeletion()) return;
        _acknowledging = true;
        try { _finish(); }
        finally { _acknowledging = false; }
    }

    private void FitCanvas()
    {
        float factor = Math.Min(Size.X / 1920, Size.Y / 1080);
        _canvas.Scale = Vector2.One * factor;
        _canvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2;
    }

    private void AddPicture(Texture2D texture, Rect2 rectangle) => _canvas.AddChild(new TextureRect
    {
        Texture = texture, Position = rectangle.Position, Size = rectangle.Size,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore
    });

    private Label AddLabel(string name, Rect2 rectangle, int size, Color color)
    {
        var label = new Label
        {
            Name = name, Position = rectangle.Position, Size = rectangle.Size,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        _canvas.AddChild(label);
        return label;
    }

    public override void _ExitTree() => _finish = null;
}
