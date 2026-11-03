using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record PersonnelLearningTacticValues(int Slot, string Name, bool ButtonVisible,
    bool MarkerVisible, bool Enabled, bool Selected);
internal sealed record PersonnelLearningPage(AreaAdministrationOfficerValues? Officer,
    IReadOnlyList<PersonnelLearningTacticValues> Tactics, IReadOnlyDictionary<int, string> Fields,
    bool CanChooseOfficer, bool CanSubmit, bool CanContinue, string Guide);

/// <summary>The source learning card and ten tactic cells. Learning and
/// replacement decisions belong to the retained draft, never to this view.</summary>
internal partial class PersonnelLearningView : Control
{
    private sealed record TacticDisplay(UiSourceCommandButton Button, Control Marker, UiSourceText Name);
    private AdministrationPageChrome chrome = null!;
    private AreaAdministrationOfficerView officer = null!;
    private UiSourceRoundButton again = null!;
    private readonly List<TacticDisplay> tactics = [];
    private readonly Dictionary<int, UiSourceText> fields = [];
    private readonly List<(Control Control, int ValueNode)> optional = [];
    private Func<bool>? admitted;
    private Action? returned;
    private Action<bool>? accepted;
    private PersonnelLearningPage? publication;
    private bool busy, abandoned;
    private string chooseCaption = "";

    internal void Configure(GameResourceRepository repository, PreviewText text, Func<bool> admitted,
        Action returned, Action<bool> accepted, Action chooseOfficer, Action<int> chooseTactic, Action<int?> inspectTactic, AdministrationPageChrome.Resources? resources = null)
    {
        this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        Name = "PersonnelLearning"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x237, RequestBack, () => RequestApply(false), threeButtonFooter: true, resources: resources);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        again = new() { Position = footer.Rectangle(3, new(0, 904)).Position, InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(again);
        again.ConfigureContinue(repository, chrome.Assets, chrome.SpriteShader, text.SourceLocale, () => RequestApply(true));
        var source = new UiSourceLayout(repository, 0xF6B97A16); Vector2 mount = new(432, 233);
        int[] cells = Enumerable.Range(0, 10).Select(index => 88 + index * 7).ToArray();
        foreach (var node in source.Nodes)
        {
            bool replaced = false;
            for (int? at = node.Index; at is int current; at = source.Nodes[current].ParentIndex)
                if (current == 7 || cells.Contains(current)) { replaced = true; break; }
            if (!replaced && node.Type is 1 or 3)
            {
                var art = source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount), chrome.Assets, chrome.SpriteShader);
                if (Within(node.Index, 76)) optional.Add((art, 81));
                else if (Within(node.Index, 82)) optional.Add((art, 87));
            }
        }
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing learning caption.");
        chooseCaption = Message(0x3FE);
        Label(5, 0x5E4); Label(75, 0x234); Label(79, 0x5E3); Label(85, 0x5A1);
        Label(161, 0x411); Label(170, 0x699); Label(179, 0x292);
        foreach (int node in new[] { 81, 87, 163, 164, 172, 174, 175, 181, 183, 184 })
            fields.Add(node, source.Text(chrome.Canvas, node, mount, "", chrome.Font, MapTextPresets.Create()[0]));
        officer = new() { Position = source.Rectangle(7, mount).Position }; chrome.Canvas.AddChild(officer);
        officer.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text,
            () => { if (ReadAdmission() && publication?.CanChooseOfficer == true) chooseOfficer(); }, chrome.Canvas);
        for (int index = 0; index < cells.Length; index++)
        {
            int slot = index, root = cells[index]; Rect2 box = source.Rectangle(root + 1, mount);
            var button = new UiSourceCommandButton { Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "", () =>
            {
                if (ReadAdmission() && publication?.Tactics.FirstOrDefault(item => item.Slot == slot) is { Enabled: true }) chooseTactic(slot);
            });
            button.MouseEntered += () => { if (ReadAdmission()) inspectTactic(slot); };
            button.MouseExited += () => { if (ReadAdmission()) inspectTactic(null); };
            Rect2 markerBox = source.Rectangle(root + 4, mount);
            var marker = new Control { Position = markerBox.Position, Size = markerBox.Size, MouseFilter = MouseFilterEnum.Stop };
            chrome.Canvas.AddChild(marker);
            Rect2 artBox = source.Rectangle(root + 5, mount); artBox.Position -= marker.Position;
            source.Art(marker, root + 5, artBox, chrome.Assets, chrome.SpriteShader);
            var caption = source.Text(marker, root + 6, mount - marker.Position, "", chrome.Font, MapTextPresets.Create()[0]);
            marker.MouseEntered += () => { if (ReadAdmission()) inspectTactic(slot); };
            marker.MouseExited += () => { if (ReadAdmission()) inspectTactic(null); };
            tactics.Add(new(button, marker, caption));
        }
        chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetBusy(false);

        bool Within(int node, int parent)
        {
            for (int? at = node; at is int current; at = source.Nodes[current].ParentIndex)
                if (current == parent) return true;
            return false;
        }
        void Label(int node, int id)
        {
            var caption = source.Text(chrome.Canvas, node, mount, Message(id), chrome.Font, MapTextPresets.Create()[13]);
            if (node == 79) optional.Add((caption, 81));
            else if (node == 85) optional.Add((caption, 87));
        }
    }

    internal void Apply(PersonnelLearningPage value)
    {
        if (abandoned) return;
        publication = value;
        officer.Apply(value.Officer ?? new(chooseCaption, null, null, null, null), !busy && value.CanChooseOfficer);
        foreach (var (node, field) in fields)
        {
            field.Visible = value.Fields.TryGetValue(node, out string? content);
            field.SetKnownColourLine(content ?? "");
        }
        foreach (var item in optional) item.Control.Visible = value.Fields.ContainsKey(item.ValueNode);
        for (int slot = 0; slot < tactics.Count; slot++)
        {
            var current = value.Tactics.FirstOrDefault(item => item.Slot == slot); var display = tactics[slot];
            display.Button.Visible = current?.ButtonVisible == true;
            display.Button.Caption = current?.Name ?? ""; display.Button.Selected = current?.Selected == true;
            display.Button.Disabled = busy || current?.Enabled != true;
            display.Marker.Visible = current?.MarkerVisible == true; display.Name.Text = current?.Name ?? "";
        }
        chrome.ApplyDefaultHint(value.Guide); RefreshAdmission();
    }
    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    internal void ApplyEffectLines(IReadOnlyList<string> lines)
    {
        if (abandoned) return;
        for (int index = 0; index < 2; index++)
            fields[163 + index].SetKnownColourLine(index < lines.Count ? lines[index] : "");
    }
    private void RefreshAdmission()
    { again.Disabled = busy || publication?.CanContinue != true; chrome.ApplyDecision(!busy && publication?.CanSubmit == true, true); chrome.ApplyReturn(!busy); }
    internal void SetBusy(bool value) { busy = value; if (publication is { } current) Apply(current); else if (chrome is not null) RefreshAdmission(); }
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    private void RequestApply(bool keepOpen) { if (ReadAdmission() && (keepOpen ? publication?.CanContinue : publication?.CanSubmit) == true) accepted?.Invoke(keepOpen); }
    internal void Abandon()
    { abandoned = true; admitted = null; returned = null; accepted = null; publication = null; officer?.Abandon(); chrome?.Abandon(); Hide(); }
}
