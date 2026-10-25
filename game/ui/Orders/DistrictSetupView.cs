using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum DistrictSetupField
{
    Policy, Priority, Attack, Transport, Recruitment, Enlistment, Merchant,
    Facilities, FacilitiesAll, Demolition, GoldUse, FoodUse, TroopsUse,
    IntelligenceUse, IntelligenceBudget, IntelligenceMissions, IntelligenceMissionsAll
}
internal sealed record DistrictSetupChoice(DistrictSetupField Field, int Value, bool Selected, bool Enabled, string? Caption = null);
internal sealed record DistrictSetupPageValues(string DistrictName,
    IReadOnlyList<IReadOnlyDictionary<int, string>> Text,
    IReadOnlyList<DistrictSetupChoice> Choices, string CityCaption, string LeaderCaption, string TargetCaption,
    bool CanChooseCities, bool CanChooseLeader, bool CanChooseTarget, bool CanConfirm, bool CanContinue, string Guide);

/// <summary>The four authored district-setting panes. Only input changes the
/// captured draft; switching panes retains every setting and scroll owner.</summary>
internal partial class DistrictSetupView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly Control[] panes = new Control[4];
    private readonly List<UiSourceTabButton> tabs = [];
    private readonly List<(int Tab, int Node, UiSourceText Text)> fields = [];
    private readonly List<(DistrictSetupField Field, int Value, UiSourceButton Button)> choices = [];
    private UiSourceCommandButton cities = null!, leader = null!, target = null!;
    private UiSourceRoundButton again = null!;
    private UiSourceText district = null!;
    private Func<bool>? admitted;
    private Action? returned;
    private Action<bool>? accepted;
    private Action<DistrictSetupField, int>? selected;
    private DistrictSetupPageValues? publication;
    private bool busy, abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, bool editing,
        Func<bool> admitted, Action returned, Action<bool> accepted,
        Action chooseCities, Action chooseLeader, Action chooseTarget, Action<DistrictSetupField, int> selected)
    {
        this.admitted = admitted; this.returned = returned; this.accepted = accepted; this.selected = selected;
        Name = "DistrictSetup"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, editing ? 0x3A8 : 0x3A7, RequestBack, () => RequestApply(false), threeButtonFooter: true);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        again = new() { Position = footer.Rectangle(3, new(0, 904)).Position, InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(again);
        again.ConfigureContinue(repository, chrome.Assets, chrome.SpriteShader, text.SourceLocale, () => RequestApply(true));
        var messageTable = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages;
        var messages = messageTable.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException($"Missing district caption {id:X4}.");
        string Catalogue(int id) => messageTable.Sections[0].Records[id].Text
            ?? throw new InvalidDataException($"Missing district catalogue caption {id:X4}.");
        var shell = new UiSourceLayout(repository, 0xAC8D7A9F); Vector2 mount = new(198, 110);
        Draw(shell, chrome.Canvas, mount, new HashSet<int> { 3, 19 });
        district = shell.Text(chrome.Canvas, 20, mount, "", chrome.Font);
        int[] tabTitles = [0x3CD, 0x262, 0x2BB, 0x835];
        for (int index = 0; index < 4; index++)
        {
            int slot = index; var box = shell.Rectangle(4 + index * 2, mount);
            var tab = new UiSourceTabButton { Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(tab); tab.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(tabTitles[index]));
            tab.Pressed += () => { if (ReadAdmission()) SetTab(slot); }; tabs.Add(tab);
            panes[index] = new Control { Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
            chrome.Canvas.AddChild(panes[index]);
        }
        uint[] files = [0xB54757E9, 0x0500440B, 0x49AA8A6B, 0x0585A602];
        var layouts = files.Select(file => new UiSourceLayout(repository, file)).ToArray();
        Vector2 paneMount = shell.Rectangle(1, mount).Position;
        BuildBasic();
        BuildConstruction();
        BuildResources();
        BuildIntelligence();
        chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        SetMeta("source_layout", shell.Source.Source.ResourceUri); SetTab(0); SetBusy(false);

        void Draw(UiSourceLayout layout, Control parent, Vector2 origin, IReadOnlySet<int> replaced)
        {
            foreach (var node in layout.Nodes)
            {
                bool hidden = false;
                for (int? at = node.Index; at is int current; at = layout.Nodes[current].ParentIndex)
                    if (replaced.Contains(current)) { hidden = true; break; }
                if (!hidden && node.Type is 1 or 3)
                    layout.Art(parent, node.Index, layout.Rectangle(node.Index, origin), chrome.Assets, chrome.SpriteShader);
            }
        }
        void Label(int tab, int node, int message) => layouts[tab].Text(panes[tab], node, paneMount, Message(message), chrome.Font);
        void Value(int tab, int node)
        { fields.Add((tab, node, layouts[tab].Text(panes[tab], node, paneMount, "", chrome.Font))); }
        UiSourceCommandButton Command(int tab, int node, Action action, string? caption = null)
        {
            var box = layouts[tab].Rectangle(node, paneMount);
            var button = new UiSourceCommandButton { Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
            panes[tab].AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption ?? Message(0x3FE), () => { if (ReadAdmission()) action(); });
            return button;
        }
        void Choice(int tab, int node, DistrictSetupField field, int value, string caption)
        {
            var button = Command(tab, node, () => this.selected?.Invoke(field, value), caption);
            choices.Add((field, value, button));
        }
        void Radios(int tab, DistrictSetupField field, int[] roots, int[] values, int[] captions, bool catalogue = false)
        {
            for (int index = 0; index < roots.Length; index++)
            {
                int value = values[index]; var box = layouts[tab].Rectangle(roots[index], paneMount);
                var button = new UiSourceRadioButton { Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
                panes[tab].AddChild(button);
                button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                    index == 0 ? UiSourceRadioSegment.Left : index == roots.Length - 1 ? UiSourceRadioSegment.Right : UiSourceRadioSegment.Centre,
                    catalogue ? Catalogue(captions[index]) : Message(captions[index]),
                    () => { if (ReadAdmission()) this.selected?.Invoke(field, value); });
                choices.Add((field, value, button));
            }
        }
        void BuildBasic()
        {
            int[] roots = [4, 9, 25, 27, 29, 31, 33, 38, 43, 45, 50, 52, 58, 60, 62, 64, 69, 71, 76, 78, 83, 85, 89, 93, 106];
            Draw(layouts[0], panes[0], paneMount, roots.ToHashSet());
            foreach (var (node, message) in new[] { (3, 0x207), (8, 0x214), (14, 0x211), (19, 0x20E), (24, 0x3A6),
                (37, 0x39F), (42, 0x27D), (49, 0x26B), (57, 0x3A0), (68, 0x277), (75, 0x4BB), (82, 0x2B0) }) Label(0, node, message);
            Value(0, 15); Value(0, 20);
            cities = Command(0, 4, chooseCities); leader = Command(0, 9, chooseLeader); target = Command(0, 38, chooseTarget);
            Radios(0, DistrictSetupField.Policy, [25, 27, 29, 31, 33], [1, 2, 3, 4, 5], [0x176, 0x177, 0x178, 0x179, 0x17A], true);
            Radios(0, DistrictSetupField.Priority, [58, 62, 60, 64], [1, 2, 3, 4], [0x17C, 0x17D, 0x17E, 0x17F], true);
            foreach (var (field, first, second) in new[] { (DistrictSetupField.Attack, 43, 45), (DistrictSetupField.Transport, 50, 52),
                (DistrictSetupField.Recruitment, 69, 71), (DistrictSetupField.Enlistment, 76, 78), (DistrictSetupField.Merchant, 83, 85) })
                Radios(0, field, [first, second], [1, 0], [0x27F, 0x280]);
        }
        void BuildConstruction()
        {
            int[] facilityRoots = [6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 68, 70, 72];
            Draw(layouts[1], panes[1], paneMount, facilityRoots.Concat([4, 33, 35, 38, 41, 50, 59, 74, 76, 78]).ToHashSet());
            Label(1, 3, 0x4B5); Label(1, 32, 0x264);
            Choice(1, 4, DistrictSetupField.FacilitiesAll, 0, Message(0x3A));
            for (int index = 0; index < facilityRoots.Length; index++)
                Choice(1, facilityRoots[index], DistrictSetupField.Facilities, 3 + index, "");
            Radios(1, DistrictSetupField.Demolition, [33, 35], [1, 0], [0x27F, 0x280]);
        }
        void BuildResources()
        {
            int[] inherited = [1, 30];
            Draw(layouts[2], panes[2], paneMount, inherited.Concat([44, 46, 48, 53, 55, 57, 62, 64, 66]).ToHashSet());
            Label(2, 40, 0x4B6); Label(2, 43, 0x20C); Label(2, 52, 0x20D); Label(2, 61, 0x20E);
            Radios(2, DistrictSetupField.GoldUse, [44, 46, 48], [0, 1, 2], [0x180, 0x181, 0x182], true);
            Radios(2, DistrictSetupField.FoodUse, [53, 55, 57], [0, 1, 2], [0x180, 0x181, 0x182], true);
            Radios(2, DistrictSetupField.TroopsUse, [62, 64, 66], [0, 1, 2], [0x180, 0x181, 0x182], true);
        }
        void BuildIntelligence()
        {
            int[] roots = [4, 6, 17, 19, 29, 31, 33, 35, 37, 39, 41, 43, 45];
            Draw(layouts[3], panes[3], paneMount, roots.ToHashSet());
            Label(3, 3, 0x8A5); Label(3, 11, 0x8A7); Label(3, 28, 0x8A6);
            Value(3, 13); Value(3, 15); Value(3, 16);
            Radios(3, DistrictSetupField.IntelligenceUse, [4, 6], [1, 0], [0x27F, 0x280]);
            Choice(3, 17, DistrictSetupField.IntelligenceBudget, 0, Message(0x8A8));
            Choice(3, 19, DistrictSetupField.IntelligenceBudget, 1, Message(0x29F));
            Choice(3, 29, DistrictSetupField.IntelligenceMissionsAll, 0, Message(0x3A));
            int[] captions = [0x2A6, 0x2A7, 0x2A8, 0x2A9, 0x6F0, 0xC5, 0xC6];
            int[] missionIds = [7, 5, 6, 3, 4, 1, 0];
            for (int index = 0; index < captions.Length; index++)
                Choice(3, 31 + index * 2, DistrictSetupField.IntelligenceMissions, missionIds[index], Message(captions[index]));
        }
    }

    internal void Apply(DistrictSetupPageValues value)
    {
        if (abandoned) return;
        publication = value; district.Text = value.DistrictName;
        cities.Caption = value.CityCaption; leader.Caption = value.LeaderCaption; target.Caption = value.TargetCaption;
        foreach (var field in fields)
            field.Text.SetKnownColourLine(field.Tab < value.Text.Count ? value.Text[field.Tab].GetValueOrDefault(field.Node) ?? "" : "");
        foreach (var choice in choices)
        {
            var state = value.Choices.FirstOrDefault(item => item.Field == choice.Field && item.Value == choice.Value);
            choice.Button.Selected = state?.Selected == true; choice.Button.Disabled = busy || state?.Enabled != true;
            if (state?.Caption is string caption && choice.Button is UiSourceCommandButton command) command.Caption = caption;
        }
        cities.Disabled = busy || !value.CanChooseCities; leader.Disabled = busy || !value.CanChooseLeader; target.Disabled = busy || !value.CanChooseTarget;
        again.Disabled = busy || !value.CanContinue;
        chrome.ApplyDecision(!busy && value.CanConfirm, true); chrome.ApplyReturn(!busy); chrome.ApplyDefaultHint(value.Guide);
    }
    private void SetTab(int tab)
    {
        for (int index = 0; index < panes.Length; index++) { panes[index].Visible = index == tab; tabs[index].Selected = index == tab; }
    }
    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    internal void SetBusy(bool value)
    {
        if (abandoned) return;
        busy = value; foreach (var tab in tabs) tab.Disabled = value;
        if (publication is { } current) Apply(current);
        else { again.Disabled = true; chrome.ApplyDecision(false, true); chrome.ApplyReturn(!busy); }
    }
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    private void RequestApply(bool keepOpen)
    { if (ReadAdmission() && (keepOpen ? publication?.CanContinue : publication?.CanConfirm) == true) accepted?.Invoke(keepOpen); }
    internal void Abandon()
    { abandoned = true; admitted = null; returned = null; accepted = null; selected = null; publication = null; chrome?.Abandon(); Hide(); }
}
