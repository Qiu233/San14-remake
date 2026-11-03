using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum CityDomesticOfficerRole { Recruiter, Trainer }
internal sealed record CityDomesticPolicyValues(int? Selected, bool? Current, bool? Commerce,
    bool? Agriculture, bool? Barracks, bool? Delegate);

/// <summary>The independent city domestic page; its presenter owns the draft,
/// selector and final command lifecycle.</summary>
internal partial class CityDomesticView : Control
{
    internal UiSourceCommonBlur CommonBackdrop { get; private set; } = null!;
    private AdministrationPageChrome chrome = null!;
    private CityDomesticSummaryView summary = null!;
    private readonly List<AreaAdministrationOfficerView> officers = [];
    private readonly List<UiSourceCommandButton> dismissals = [];
    private readonly List<UiSourceRadioButton> policies = [];
    private readonly Dictionary<Control, string?> hints = [];
    private Action? returned, accepted;
    private Action<CityDomesticOfficerRole>? choose, dismiss;
    private Action<int>? policyChosen;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, Action returned, Action accepted,
        Action<CityDomesticOfficerRole> choose, Action<CityDomesticOfficerRole> dismiss, Action<int> policyChosen, AdministrationPageChrome.Resources? resources = null)
    {
        this.returned = returned; this.accepted = accepted; this.choose = choose;
        this.dismiss = dismiss; this.policyChosen = policyChosen;
        Name = "CityDomestic"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        CommonBackdrop = new UiSourceCommonBlur(); AddChild(CommonBackdrop); CommonBackdrop.Configure(repository);
        chrome = new AdministrationPageChrome { Name = "CityDomesticChrome" }; AddChild(chrome);
        chrome.Configure(repository, text, 0x0476, RequestBack,
            () => { if (!abandoned) this.accepted?.Invoke(); }, resources: resources);
        chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        var source = new UiSourceLayout(repository, 0xCF0252EE);
        var panel = new Control { Name = "CityDomesticPanel", Position = new(460, 173),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Stop };
        chrome.Canvas.AddChild(panel);
        source.Art(panel, 2, source.Rectangle(2), chrome.Assets, chrome.SpriteShader,
            blend: UiSourceSprite.Blend.AlphaMask);
        var firstBlur = source.Blur(panel, 3, source.Rectangle(3));
        Rect2 secondBlur = source.Rectangle(4);
        source.Blur(firstBlur, 4, new(secondBlur.Position - firstBlur.Position, secondBlur.Size));
        foreach (int node in Enumerable.Range(5, 10))
            source.Art(panel, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        var presets = MapTextPresets.Create();
        foreach (var (node, message) in new[] { (139, 0x02B7), (140, 0x03EE), (141, 0x03EF) })
            source.Text(panel, node, default, Message(message), chrome.Font, presets[13]);
        summary = new CityDomesticSummaryView(); panel.AddChild(summary);
        summary.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text);
        foreach (CityDomesticOfficerRole role in Enum.GetValues<CityDomesticOfficerRole>())
        {
            int index = (int)role, mount = index == 0 ? 15 : 80;
            var card = new AreaAdministrationOfficerView { Name = role + "Card", Position = source.Rectangle(mount).Position };
            panel.AddChild(card);
            card.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text,
                () => { if (!abandoned) this.choose?.Invoke(role); }, chrome.Canvas);
            officers.Add(card);
            int dismissMount = index == 0 ? 79 : 81;
            var box = source.Rectangle(dismissMount);
            var button = new UiSourceCommandButton { Name = "Dismiss" + role, Position = box.Position,
                Size = box.Size, Disabled = true }; panel.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(0x029E),
                () => { if (!abandoned) this.dismiss?.Invoke(role); });
            dismissals.Add(button);
        }
        int[] mounts = [82, 100, 118, 119, 120];
        int[] captions = [0x02B8, 0x02B3, 0x02B4, 0x02B5, 0x0286];
        for (int i = 0; i < mounts.Length; i++)
        {
            int selected = i; var box = source.Rectangle(mounts[i]);
            var button = new UiSourceRadioButton { Name = "Policy" + i, Position = box.Position,
                Size = box.Size, Disabled = true }; panel.AddChild(button);
            var segment = i == 0 ? UiSourceRadioSegment.Left : i == 4 ? UiSourceRadioSegment.Right : UiSourceRadioSegment.Centre;
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, segment, Message(captions[i]),
                () => { if (!abandoned) this.policyChosen?.Invoke(selected); });
            button.SetMeta("source_mount_node", mounts[i]); policies.Add(button);
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        string Message(int id) => messages.Records[id].Text ??
            throw new InvalidDataException($"Missing city domestic caption {id:X4}.");
    }

    internal void ApplySummary(CityDomesticSummaryValues values) { if (!abandoned) summary.Apply(values); }
    internal void ApplyBooks(int? current, int? after, int? cost)
    { if (!abandoned) summary.ApplyBooks(current, after, cost); }
    internal void ApplyOfficer(CityDomesticOfficerRole role, AreaAdministrationOfficerValues values,
        bool canChoose, bool canDismiss, string? chooseHint, string? dismissHint)
    {
        if (abandoned) return;
        int index = (int)role;
        officers[index].Apply(values, canChoose); dismissals[index].Disabled = !canDismiss;
        hints[officers[index].SelectionControl] = chooseHint; hints[dismissals[index]] = dismissHint;
    }
    internal void ApplyPolicy(CityDomesticPolicyValues values)
    {
        if (abandoned) return;
        if (values.Selected is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(values));
        bool?[] enabled = [values.Current, values.Commerce, values.Agriculture, values.Barracks, values.Delegate];
        for (int i = 0; i < policies.Count; i++)
        {
            policies[i].Disabled = enabled[i] != true;
            policies[i].Selected = values.Selected == i;
        }
    }
    internal void ApplyDecision(bool canConfirm) { if (!abandoned) chrome.ApplyDecision(canConfirm, true); }
    internal void ApplyPolicyHints(string? current, string? commerce, string? agriculture, string? barracks, string? delegated)
    {
        if (abandoned) return;
        string?[] values = [current, commerce, agriculture, barracks, delegated];
        for (int i = 0; i < policies.Count; i++) hints[policies[i]] = values[i];
    }
    internal void ApplyDefaultHint(string hint) { if (!abandoned) chrome.ApplyDefaultHint(hint); }
    internal void RequestBack() { if (!abandoned) returned?.Invoke(); }
    public override void _Process(double delta)
    {
        if (abandoned || chrome is null) return;
        var hovered = GetViewport().GuiGetHoveredControl();
        chrome.ApplyHoverHint(hovered is not null && hints.TryGetValue(hovered, out var hint) ? hint : null);
    }
    public override void _ExitTree() => CommonBackdrop = null!;
    internal void Abandon()
    {
        abandoned = true; returned = null; accepted = null; choose = null; dismiss = null; policyChosen = null;
        hints.Clear(); foreach (var card in officers) card.Abandon();
        foreach (var button in dismissals) button.Abandon(); foreach (var button in policies) button.Abandon();
        chrome?.Abandon(); SetProcess(false); Hide();
    }
}
