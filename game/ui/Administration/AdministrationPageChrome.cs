using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The title, footer and original-font narration shared by the two
/// domestic pages. The page owner supplies its decisions and modal state.</summary>
internal partial class AdministrationPageChrome : Control
{
    internal UiAssets Assets { get; private set; } = null!;
    internal MapFont Font { get; private set; } = null!;
    internal Shader SpriteShader { get; private set; } = null!;
    internal Control Canvas { get; private set; } = null!;
    private UiSourceRoundButton back = null!;
    private UiSourceRoundButton? confirm;
    private UiSourceText narration = null!;
    private UiSourceText title = null!;
    private Action? returned, accepted;
    private string defaultHint = "";
    private string? returnHint;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, int titleMessage,
        Action returned, Action accepted, bool returnOnly = false, bool showFooterHint = true)
    {
        this.returned = returned; this.accepted = accepted;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Assets = new(repository); Font = new(repository, text.SourceLocale);
        SpriteShader = UiSourceSprite.CreateShader();
        Canvas = new Control { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(Canvas);
        var header = new UiSourceLayout(repository, 0xDC8AC8AA);
        header.Art(Canvas, 1, header.Rectangle(1), Assets, SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        title = header.Text(Canvas, 2, default, messages.Records[titleMessage].Text ??
            throw new InvalidDataException("Missing domestic page title."), Font, MapTextPresets.Create()[56]);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        Vector2 mount = new(0, 904);
        back = Round("ReturnAdministration", returnOnly ? 1 : 4, "000_cmn_txt_decision_modoru", () => this.returned?.Invoke());
        if (!returnOnly)
        {
            confirm = Round("ConfirmAdministration", 5, "000_cmn_txt_decision_kettei", () => this.accepted?.Invoke());
            confirm.Disabled = true;
        }
        var hint = new UiSourceLayout(repository, 0xAB6ADAA7);
        Vector2 hintMount = footer.Rectangle(19, mount).Position;
        foreach (var node in hint.Nodes.Where(node => node.Type is 1 or 3))
            hint.Art(Canvas, node.Index, hint.Rectangle(node.Index, hintMount), Assets, SpriteShader).Visible = showFooterHint;
        narration = hint.Text(Canvas, hint.Nodes.Single(node => node.Type == 2).Index, hintMount, "", Font);
        narration.Name = "AdministrationHint";
        narration.Visible = showFooterHint;
        Resized += Fit; Fit();

        UiSourceRoundButton Round(string name, int slot, string caption, Action action)
        {
            var button = new UiSourceRoundButton { Name = name, Position = footer.Rectangle(slot, mount).Position,
                ActionMode = BaseButton.ActionModeEnum.Release };
            Canvas.AddChild(button);
            button.Configure(repository, Assets, SpriteShader, text.SourceLocale, caption,
                () => { if (!abandoned && !button.Disabled) action(); });
            return button;
        }
    }

    internal void ApplyDecision(bool enabled, bool visible)
    { if (abandoned || confirm is null) return; confirm.Disabled = !enabled; confirm.Visible = visible; }
    internal void ApplyReturn(bool enabled) { if (!abandoned) back.Disabled = !enabled; }
    internal void ApplyTitle(string value) { if (!abandoned) title.Text = value; }
    internal void ApplyReturnHint(string value) { if (!abandoned) returnHint = value; }
    internal void ApplyDefaultHint(string text)
    { if (abandoned) return; defaultHint = text; narration.Text = text; }
    internal void ApplyHoverHint(string? text)
    {
        if (!abandoned) narration.Text = text ?? (GetViewport().GuiGetHoveredControl() == back ? returnHint : null) ?? defaultHint;
    }

    internal void Abandon()
    {
        abandoned = true; returned = null; accepted = null;
        if (back is not null) back.Disabled = true;
        if (confirm is not null) confirm.Disabled = true;
        Hide();
    }

    private void Fit()
    {
        if (Canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        Canvas.Scale = Vector2.One * scale;
        Canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    { returned = null; accepted = null; Font?.Dispose(); Assets?.Dispose(); SpriteShader?.Dispose(); }
}
