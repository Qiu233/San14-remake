using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The title, footer and original-font narration shared by the two
/// domestic pages. The page owner supplies its decisions and modal state.</summary>
internal partial class AdministrationPageChrome : Control
{
    // A containing page may lend all three resources for its lifetime.
    internal sealed record Resources(UiAssets Assets, MapFont Font, Shader SpriteShader, LoadedMessages? Messages = null);
    private bool ownsResources;
    internal UiAssets Assets { get; private set; } = null!;
    internal MapFont Font { get; private set; } = null!;
    internal Shader SpriteShader { get; private set; } = null!;
    internal Control Canvas { get; private set; } = null!;
    internal int MenuFooterTop { get; private set; }
    internal Node2D HeaderMotion { get; private set; } = null!;
    internal Node2D FooterMotion { get; private set; } = null!;
    internal Node2D NarrationMotion { get; private set; } = null!;
    private UiSourceRoundButton back = null!;
    private UiSourceRoundButton? confirm;
    private UiSourceMultilineText narration = null!;
    private UiSourceText title = null!;
    private Action? returned, accepted;
    private string defaultHint = "";
    private string? returnHint, decisionHint;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, int titleMessage,
        Action returned, Action accepted, bool returnOnly = false, bool showFooterHint = true, bool compactReturn = false,
        bool threeButtonFooter = false, int? returnNode = null, int titleSection = 1, Resources? resources = null, bool modalMotion = false)
    {
        this.returned = returned; this.accepted = accepted;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ownsResources = resources is null;
        if (resources is null)
        {
            Assets = new(repository); Font = new(repository, text.SourceLocale);
            SpriteShader = UiSourceSprite.CreateShader();
        }
        else
        {
            Assets = resources.Assets; Font = resources.Font; SpriteShader = resources.SpriteShader;
        }
        Canvas = new Control { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(Canvas);
        CanvasItem headerMount = Canvas, footerMount = Canvas, narrationMount = Canvas;
        if (modalMotion)
        {
            HeaderMotion = new Node2D { Name = "HeaderMotion" };
            FooterMotion = new Node2D { Name = "FooterMotion" };
            NarrationMotion = new Node2D { Name = "NarrationMotion" };
            Canvas.AddChild(HeaderMotion); Canvas.AddChild(FooterMotion); Canvas.AddChild(NarrationMotion);
            headerMount = HeaderMotion; footerMount = FooterMotion; narrationMount = NarrationMotion;
        }
        var header = Assets.SourceLayout(0xDC8AC8AA);
        header.Art(headerMount, 1, header.Rectangle(1), Assets, SpriteShader);
        var messages = (resources?.Messages ?? repository.LoadMessages(text.SourceLocale, MessageKind.Data)).Messages.Sections[titleSection];
        title = header.Text(headerMount, 2, default, messages.Records[titleMessage].Text ??
            throw new InvalidDataException("Missing domestic page title."), Font, MapTextPresets.Create()[56]);
        var footer = Assets.SourceLayout(0xD8913AB2);
        Vector2 mount = new(0, 904);
        MenuFooterTop = (int)footer.Rectangle(19, mount).Position.Y;
        back = Round("ReturnAdministration", returnNode ?? (returnOnly || threeButtonFooter ? 1 : 4), "000_cmn_txt_decision_modoru", () => this.returned?.Invoke());
        if (!returnOnly)
        {
            confirm = Round("ConfirmAdministration", threeButtonFooter ? 2 : 5, "000_cmn_txt_decision_kettei", () => this.accepted?.Invoke());
            confirm.Disabled = true;
            confirm.MouseEntered += () => { if (!abandoned && decisionHint is not null) ApplyHoverHint(decisionHint); };
            confirm.MouseExited += () => { if (decisionHint is not null) ApplyDefaultHint(defaultHint); };
        }
        var hint = Assets.SourceLayout(0xAB6ADAA7);
        Vector2 hintMount = footer.Rectangle(19, mount).Position;
        foreach (var node in hint.Nodes.Where(node => node.Type is 1 or 3))
            hint.Art(narrationMount, node.Index, hint.Rectangle(node.Index, hintMount), Assets, SpriteShader).Visible = showFooterHint;
        var hintNode = hint.Nodes.Single(node => node.Type == 2);
        var hintBox = hint.Rectangle(hintNode.Index, hintMount);
        narration = new UiSourceMultilineText { Name = "AdministrationHint", Position = hintBox.Position, Size = hintBox.Size };
        narrationMount.AddChild(narration);
        narration.ConfigureCommonGuide(Font, MapFontProfile.FromNode(hintNode, hint.Source.Source.ResourceUri));
        narration.Visible = showFooterHint;
        back.MouseEntered += () => { if (!abandoned && !back.Disabled) ApplyHoverHint(returnHint); };
        back.MouseExited += () => ApplyDefaultHint(defaultHint);
        Resized += Fit; Fit();

        UiSourceRoundButton Round(string name, int slot, string caption, Action action)
        {
            var button = new UiSourceRoundButton { Name = name, Position = footer.Rectangle(slot, mount).Position,
                ActionMode = BaseButton.ActionModeEnum.Release };
            footerMount.AddChild(button);
            Action activate = () => { if (!abandoned && !button.Disabled) action(); };
            if (compactReturn && slot == 1)
                button.ConfigureScenarioReturn(repository, Assets, SpriteShader, text.SourceLocale, activate);
            else button.Configure(repository, Assets, SpriteShader, text.SourceLocale, caption, activate,
                cue: name == "ReturnAdministration" ? UiAudioCue.Cancel : UiAudioCue.ButtonSelect);
            return button;
        }
    }

    internal void ApplyDecision(bool enabled, bool visible)
    { if (abandoned || confirm is null) return; confirm.Disabled = !enabled; confirm.Visible = visible; }
    internal void ApplyReturn(bool enabled) { if (!abandoned && back is not null) back.Disabled = !enabled; }
    internal void ApplyReturnVisible(bool visible) { if (!abandoned && back is not null) back.Visible = visible; }
    internal void ApplyTitle(string value) { if (!abandoned) title.Text = value; }
    internal void ApplyReturnHint(string value) { if (!abandoned) returnHint = value; }
    internal void ApplyDecisionHint(string? value) { if (!abandoned) decisionHint = value; }
    internal void ApplyDefaultHint(string text)
    { if (abandoned) return; defaultHint = text; narration.Apply(text); }
    internal void ApplyHoverHint(string? text)
    {
        if (abandoned) return;
        var hovered = GetViewport().GuiGetHoveredControl();
        narration.Apply(text ?? (hovered == back ? returnHint : hovered == confirm ? decisionHint : null) ?? defaultHint);
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
    {
        returned = null; accepted = null;
        if (ownsResources) { Font?.Dispose(); Assets?.Dispose(); SpriteShader?.Dispose(); }
    }
}
