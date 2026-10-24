using Godot;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Presentation;

/// <summary>
/// Read-only report presentation. Acknowledgement is delegated to the campaign;
/// opening, resizing and scrolling this view never run the simulation again.
/// The shell uses original artwork with a responsive client layout.
/// </summary>
public partial class PlayablePeriodReportView : Control
{
    private Control _canvas = null!;
    private UiSourceHeader _title = null!;
    private Label _status = null!;
    private RichTextLabel _body = null!;
    private Label _entryTitle = null!;
    private PeriodReportEntriesView? _entries;
    private PeriodReportPresentation? _presentation;
    private GameResourceRepository _repository = null!;
    private PreviewText _text = null!;
    private int _scrollSpeed;
    private Func<int> _currentScrollSpeed = null!;
    private UiSourceReportFooter _footer = null!;
    private UiAssets? _assets;
    private MapFont? _font;
    private Shader? _shader;
    private Action? _finish;
    private bool _acknowledging;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        string status, PeriodReportPresentation report, int period, Action finish, UiSourceHeaderStack headers,
        int scrollSpeed, Func<int> currentScrollSpeed)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(finish);
        Name = "PlayablePeriodReportView";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _finish = finish;
        _repository = repository; _text = text; _scrollSpeed = scrollSpeed; _currentScrollSpeed = currentScrollSpeed;
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
        _assets = new UiAssets(repository);
        _font = new MapFont(repository, text.SourceLocale);
        _shader = UiSourceSprite.CreateShader();
        _title = new UiSourceHeader { Name = "ReportTitle" };
        _canvas.AddChild(_title);
        _title.Configure(repository, _assets, _shader, _font, text.Get("play_report_heading"));
        _title.Publish(headers);
        _status = AddLabel("ReportPeriod", new Rect2(80, 120, 700, 48), 24, new Color("#efdfb4"));
        var source = new UiSourceLayout(repository, 0xDBB537B1);
        source.Blur(_canvas, 3, source.Rectangle(3));
        source.Art(_canvas, 4, source.Rectangle(4), _assets, _shader);
        var allBox = source.Rectangle(17);
        var all = new UiSourceTabButton { Name = "PeriodReportAll", Position = allBox.Position, Size = allBox.Size, Selected = true };
        _canvas.AddChild(all);
        all.Configure(repository, _assets, _shader, _font,
            repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records[0x5B5].Text
                ?? throw new InvalidDataException("Missing all-report caption."), 0xD45873AA);
        _entryTitle = AddLabel("SelectedReportTitle", new Rect2(95, 205, 640, 50), 26, new Color("#efdfb4"));
        _body = new RichTextLabel
        {
            Name = "SelectedReportDetail", Position = new Vector2(95, 270), Size = new Vector2(640, 575),
            BbcodeEnabled = false, ScrollActive = true, FitContent = false,
            MouseFilter = MouseFilterEnum.Stop, SelectionEnabled = true
        };
        _body.AddThemeFontSizeOverride("normal_font_size", 26);
        _body.AddThemeColorOverride("default_color", new Color("#efdfb4"));
        _body.AddThemeConstantOverride("line_separation", 9);
        _canvas.AddChild(_body);
        _footer = new UiSourceReportFooter { Name = "PeriodReportFooter" };
        _canvas.AddChild(_footer);
        _footer.Configure(repository, _assets, _shader, text.SourceLocale, false,
            RequestAcknowledge, () => !_acknowledging && _finish is not null && IsVisibleInTree() && !IsQueuedForDeletion());
        Refresh(text, status, report, period);
        Resized += FitCanvas;
        FitCanvas();
    }

    internal void Refresh(PreviewText text, string status, PeriodReportPresentation report, int period)
    {
        _text = text;
        _title.ApplyCaption(text.Get("play_report_heading"));
        _status.Text = status;
        // A refresh of the same committed report keeps selection and scrolling.
        if (!ReferenceEquals(_presentation, report))
        {
            _presentation = report;
            if (_entries is { } previous)
            { previous.Abandon(); _canvas.RemoveChild(previous); previous.QueueFree(); }
            var source = new UiSourceLayout(_repository, 0xDBB537B1);
            var box = source.Rectangle(42); var scroll = source.Rectangle(76); scroll.Position -= box.Position;
            _entries = new PeriodReportEntriesView { Name = "PeriodReportEntries", Position = box.Position, Size = box.Size };
            _canvas.AddChild(_entries);
            _entries.Configure(_repository, _assets!, _shader!, _font!, report.Entries, scroll, _scrollSpeed, _currentScrollSpeed, SelectEntry);
            if (report.Entries.Count == 0)
            { _entryTitle.Text = ""; _body.Text = text.Get("play_report_empty"); }
        }
        SetMeta("report_period", period);
        SetMeta("report_locale", text.SourceLocale);
        SetMeta("report_body", report.Body);
        SetMeta("report_entry_count", report.Entries.Count);
        SetMeta("report_read_only", true);
    }

    private void SelectEntry(int index)
    {
        _entryTitle.Text = _text.Format("play_report_entry_title", index + 1, _presentation!.Entries.Count);
        _body.Text = _presentation.Entries[index];
        SetMeta("report_selected_entry", index);
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

    internal void Retire()
    {
        _finish = null;
        _entries?.Abandon();
        Release(_footer);
        Release(_title);
        Hide();
    }

    private static void Release(Control? control)
    {
        if (control is null || !GodotObject.IsInstanceValid(control)) return;
        if (control.GetParent() is Node parent) parent.RemoveChild(control);
        control.QueueFree();
    }

    public override void _ExitTree()
    {
        _finish = null;
        _entries?.Abandon();
        _font?.Dispose(); _assets?.Dispose(); _shader?.Dispose();
    }
}
