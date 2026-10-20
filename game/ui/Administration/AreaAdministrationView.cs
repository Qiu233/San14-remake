using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original single-region page. Its presenter owns the child
/// draft; displaying or selecting a focus never submits a campaign order.</summary>
internal partial class AreaAdministrationView : Control
{
    private AdministrationPageChrome chrome = null!;
    private Control panel = null!;
    private AreaAdministrationSummaryView summary = null!;
    private AreaAdministrationFocusView focuses = null!;
    private AreaAdministrationDevelopmentView development = null!;
    private AreaAdministrationOfficerView officer = null!;
    private AreaAdministrationOfficerSpeechView speech = null!;
    private UiSourceCommandButton dismiss = null!;
    private readonly Dictionary<Control, string?> hoverHints = [];
    private bool abandoned;
    private Action? returned, accepted, dismissed, chooseOfficer;
    private Action<int>? focusChosen;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Action returned, Action accepted, Action dismissed, Action chooseOfficer, Action<int> focusChosen)
    {
        this.returned = returned; this.accepted = accepted; this.dismissed = dismissed;
        this.chooseOfficer = chooseOfficer; this.focusChosen = focusChosen;
        Name = "AreaAdministration"; ZIndex = 3;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new AdministrationPageChrome { Name = "AdministrationChrome" };
        AddChild(chrome);
        chrome.Configure(repository, text, 0x04B7, RequestBack,
            () => { if (!abandoned) this.accepted?.Invoke(); });
        chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        var source = new UiSourceLayout(repository, 0xC4464DC9);
        panel = new Control { Name = "RegionPanel", Position = new(460, 127),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Stop };
        chrome.Canvas.AddChild(panel);
        // The authored mask and Type6 blur do not have an ordinary RGB pass.
        // Keep the real frame, information base and source separators.
        foreach (int node in new[] { 5, 7, 10, 12, 94, 95, 96 })
            source.Art(panel, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        var presets = MapTextPresets.Create();
        foreach (var (node, message) in new[] { (8, 0x0479), (11, 0x02B6) })
            source.Text(panel, node, default, messages.Records[message].Text ??
                throw new InvalidDataException("Missing regional administration caption."), chrome.Font, presets[13]);
        summary = new AreaAdministrationSummaryView(); panel.AddChild(summary);
        summary.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text);
        development = new AreaAdministrationDevelopmentView { Name = "AreaDevelopment" }; panel.AddChild(development);
        development.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text);
        officer = new AreaAdministrationOfficerView { Name = "AreaOfficer", Position = source.Rectangle(13).Position }; panel.AddChild(officer);
        officer.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text,
            () => { if (!abandoned) this.chooseOfficer?.Invoke(); },chrome.Canvas);
        hoverHints[officer.SelectionControl] = new AdministrationMessages(repository, text.SourceLocale).Format(0x56D2);
        var speechRectangle=source.Rectangle(97);
        speech=new AreaAdministrationOfficerSpeechView{Name="OfficerSpeech",Position=speechRectangle.Position,Size=speechRectangle.Size};
        panel.AddChild(speech);speech.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font);
        focuses = new AreaAdministrationFocusView(); panel.AddChild(focuses);
        focuses.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text,
            focus => { if (!abandoned) this.focusChosen?.Invoke(focus); });
        var dismissRectangle = source.Rectangle(77);
        dismiss = new UiSourceCommandButton { Name = "DismissAreaOfficer", Position = dismissRectangle.Position,
            Size = dismissRectangle.Size, Disabled = true };
        panel.AddChild(dismiss);
        dismiss.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
            messages.Records[0x029E].Text ?? throw new InvalidDataException("Missing regional dismissal caption."),
            () => { if (!abandoned) this.dismissed?.Invoke(); });
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void ApplySummary(AreaAdministrationSummaryValues values)
    { if (!abandoned) summary.Apply(values); }
    internal void ApplyFocus(AreaAdministrationFocusValues values)
    { if (!abandoned) focuses.Apply(values); }
    internal void ApplyDevelopment(AreaAdministrationDevelopmentValues values)
    { if (!abandoned) development.Apply(values); }
    internal void ApplyOfficer(AreaAdministrationOfficerValues values, bool canChoose)
    { if (!abandoned) officer.Apply(values, canChoose); }
    internal void ApplySpeech(string? expanded)
    { if (!abandoned) speech.ApplyText(expanded); }
    internal void ApplySpeechUnknown(string label)
    { if (!abandoned) speech.ApplyUnknown(label); }
    internal void ApplyDecision(bool canAccept)
    { if (!abandoned) chrome.ApplyDecision(canAccept, true); }
    internal void ApplyDismissal(bool enabled, string? hint)
    { if (abandoned) return; dismiss.Disabled = !enabled; hoverHints[dismiss] = hint; RefreshHint(); }
    internal void ApplyDefaultHint(string hint)
    { if (!abandoned) chrome.ApplyDefaultHint(hint); }
    internal void ApplyFocusHints(string? commerce, string? agriculture, string? barracks)
    {
        if (abandoned) return;
        hoverHints[focuses.GetNode<UiSourceRadioButton>("Focus1")] = commerce;
        hoverHints[focuses.GetNode<UiSourceRadioButton>("Focus2")] = agriculture;
        hoverHints[focuses.GetNode<UiSourceRadioButton>("Focus3")] = barracks;
        RefreshHint();
    }
    private void RefreshHint()
    {
        if (abandoned || chrome is null) return;
        var owner = GetViewport().GuiGetHoveredControl();
        chrome.ApplyHoverHint(owner is not null && hoverHints.TryGetValue(owner, out var hint) ? hint : null);
    }
    public override void _Process(double delta) => RefreshHint();
    internal void RequestBack() { if (!abandoned) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; returned = null; accepted = null; dismissed = null; chooseOfficer = null; focusChosen = null;
        hoverHints.Clear(); dismiss?.Abandon(); officer?.Abandon(); focuses?.Abandon(); chrome?.Abandon(); Hide();
    }
}
