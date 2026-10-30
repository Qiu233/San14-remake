using Godot;
using San14.Core;
using San14.Data;

/// <summary>The original event result panel and confirmation footer. Its
/// caller owns the prepared text and the later business continuation.</summary>
internal partial class EventResultSummaryView : Control
{
    private Control canvas = null!;
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private UiSourceReportFooter footer = null!;
    private Func<bool>? admitted;
    private Action? confirmed;
    private bool abandoned, busy;

    internal void Configure(GameResourceRepository repository, EditedEventResultSummary summary,
        Func<bool> admitted, Action confirmed)
    {
        Name = "EditedEventResultSummary"; ZIndex = 9;
        ConfigurePage(repository, summary.Locale, summary.Title, summary.Body, summary.Hint, admitted, confirmed);
    }

    internal void Configure(GameResourceRepository repository, NormalTurnStoryResultSummary summary,
        Func<bool> admitted, Action confirmed)
    {
        Name = "NormalTurnStoryResult"; ZIndex = 97;
        ConfigurePage(repository, summary.Locale, summary.Title, summary.Body, null, admitted, confirmed);
    }

    private void ConfigurePage(GameResourceRepository repository, string locale, string titleText, string bodyText,
        string? hintText, Func<bool> admitted, Action confirmed)
    {
        this.admitted = admitted; this.confirmed = confirmed;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository, locale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var blur = new UiSourceLayout(repository, 0xAEF1609E);
        blur.Blur(canvas, 1, blur.Rectangle(1), weightInput: 100);
        blur.Blur(canvas, 2, blur.Rectangle(2), weightInput: 100);
        var source = new UiSourceLayout(repository, 0x49C33043);
        var panel = new Control { Name = "EventSummaryPanel", Size = source.Rectangle(0).Size,
            Position = (canvas.Size - source.Rectangle(0).Size) / 2, MouseFilter = MouseFilterEnum.Ignore };
        canvas.AddChild(panel);
        foreach (int node in new[] { 1, 2 })
            source.Art(panel, node, source.Rectangle(node), assets, shader);
        var title = source.Text(panel, 3, default, "", font);
        title.Name = "EventSummaryTitle"; title.SetKnownColourLine(titleText);
        Rect2 box = source.Rectangle(4);
        var body = new UiSourceParagraph { Name = "EventSummaryBody", Position = box.Position,
            Size = box.Size, ClipContents = true };
        panel.AddChild(body);
        body.ConfigureFormattedMessage(font, MapFontProfile.FromNode(source.Nodes[4], source.Source.Source.ResourceUri));
        // An empty body retains the actual page and footer without an
        // invented paragraph or automatic confirmation.
        if (bodyText.Length != 0) body.Apply(bodyText);
        footer = new() { Name = "EventSummaryFooter" }; canvas.AddChild(footer);
        footer.Configure(repository, assets, shader, locale, false, Confirm, CanUse);
        if (hintText is not null)
        {
            var footerSource = new UiSourceLayout(repository, 0xD8913AB2);
            var hint = new UiSourceLayout(repository, 0xAB6ADAA7);
            Vector2 mount = footerSource.Rectangle(19, new(0, 904)).Position;
            foreach (var node in hint.Nodes.Where(node => node.Type is 1 or 3))
                hint.Art(canvas, node.Index, hint.Rectangle(node.Index, mount), assets, shader);
            var explanation = hint.Text(canvas, hint.Nodes.Single(node => node.Type == 2).Index, mount, "", font);
            explanation.Name = "EventSummaryHint"; explanation.SetKnownColourLine(hintText);
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Resized += Fit; Fit();
    }

    private bool CanUse() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    private void Confirm() { if (CanUse()) confirmed?.Invoke(); }
    internal void RequestConfirmation() { if (CanUse()) footer.RequestConfirmation(); }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; footer.ConfirmEnabled = !value; } }
    internal void Abandon()
    { abandoned = true; admitted = null; confirmed = null; if (footer is not null) footer.ConfirmEnabled = false; Hide(); }
    public override void _Input(InputEvent input)
    {
        if (!CanUse() || input is not (InputEventKey { Keycode: Key.Escape } or
            InputEventMouseButton { ButtonIndex: MouseButton.Right })) return;
        if (input is InputEventKey { Pressed: true, Echo: false } or InputEventMouseButton { Pressed: false })
            footer.RequestConfirmation();
        GetViewport().SetInputAsHandled();
    }
    private void Fit()
    {
        if (canvas is null) return;
        float factor = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * factor;
        canvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2;
    }
    public override void _ExitTree() { Abandon(); font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
}
