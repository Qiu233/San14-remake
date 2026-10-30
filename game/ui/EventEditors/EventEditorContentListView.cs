using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum EventEditorContentListAction { MoveUp, MoveDown, Insert, Edit, Delete, Copy, Preview }
internal sealed record EventEditorContentListRow(int Id, IReadOnlyList<string> Message,
    IReadOnlyList<string> Talk, IReadOnlyList<string> Staging, bool Enabled = true)
{
    internal LoadedSprite? Portrait { get; init; }
}
internal sealed record EventEditorContentListValues(IReadOnlyList<EventEditorContentListRow> Rows,
    int? SelectedId, IReadOnlySet<EventEditorContentListAction> EnabledActions, bool CanConfirm, string Notice)
{
    internal int? UsedBytes { get; init; }
}

/// <summary>The original content list: messages, dialogue and effects are
/// three projections of the same two-hundred six-statement rows.</summary>
internal partial class EventEditorContentListView : Control
{
    private AdministrationPageChrome chrome = null!;
    private EventEditorUsageView usage = null!;
    private readonly List<UiSourceCommandButton> commands = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private readonly List<Control> headings = [];
    private readonly List<EventEditorContentListButton> rows = [];
    private Control clip = null!;
    private UiSourceScrollbar? scroll;
    private UiSourceLayout source = null!;
    private GameResourceRepository repository = null!;
    private Func<EventEditorContentListValues>? read;
    private Func<bool>? admitted;
    private Action? returned, accepted;
    private Action<int>? selected;
    private Action<EventEditorContentListAction>? action;
    private Func<int> scrollSpeed = null!;
    private IReadOnlyList<EventEditorContentListRow>? currentRows;
    private bool abandoned, busy;
    internal int CurrentTab { get; private set; }

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Func<EventEditorContentListValues> read, Func<bool> admitted, Action returned, Action accepted,
        Action<int> selected, Action<EventEditorContentListAction> action, Func<int> scrollSpeed)
    {
        this.repository = repository; this.read = read; this.admitted = admitted;
        this.returned = returned; this.accepted = accepted; this.selected = selected; this.action = action; this.scrollSpeed = scrollSpeed;
        Name = "EventEditorContentsList"; ZIndex = 7; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, 0x91, RequestBack, Accept);
        source = new UiSourceLayout(repository, 0xC5D309CC);
        var body = new Control { Name = "EventContentsListBody", Position = new(0, 122),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(body);
        source.Art(body, 3, source.Rectangle(3), chrome.Assets, chrome.SpriteShader);
        source.Blur(body, 4, source.Rectangle(4)); source.Blur(body, 5, source.Rectangle(5));
        foreach (int node in new[] { 6, 23, 24, 56 }) source.Art(body, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Caption(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing event content list caption.");
        int[] captions = [0x93, 0x94, 0x95, 0x6AC, 0x4F, 0x96, 0x309];
        for (int index = 0; index < captions.Length; index++)
        {
            var operation = (EventEditorContentListAction)index; var box = source.Rectangle(8 + index * 2);
            var button = new UiSourceCommandButton { Name = "EventContents" + operation,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            body.AddChild(button); commands.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Caption(captions[index]),
                () => { if (CanEdit() && this.read?.Invoke().EnabledActions.Contains(operation) == true) this.action?.Invoke(operation); });
        }
        int[] tabMounts = [32, 34, 35], tabCaptions = [0x2EA, 0x30A, 0x301];
        for (int index = 0; index < tabMounts.Length; index++)
        {
            int tab = index; var box = source.Rectangle(tabMounts[index]);
            var button = new UiSourceTabButton { Name = "EventContentsTab" + tab,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            body.AddChild(button); tabs.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Caption(tabCaptions[index]),
                index == 0 ? 0x172EDC24u : index == 2 ? 0xD45873AAu : 0x9AFB27B7u);
            button.Pressed += () => { if (CanEdit()) SelectTab(tab); };
        }
        int[][] headingNodes = [[43, 44, 45], [47, 48, 49, 50, 51], [53, 54, 55]];
        int[][] headingCaptions = [[0x2FD, 0x2EA, 0x498], [0x305, 0x30B, 0x30C, 0x30D, 0x2FD], [0x303, 0x304, 0x2FE]];
        for (int tab = 0; tab < headingNodes.Length; tab++)
        {
            var group = new Control { Name = "EventContentsHeadings" + tab, MouseFilter = MouseFilterEnum.Ignore };
            body.AddChild(group); headings.Add(group);
            for (int field = 0; field < headingNodes[tab].Length; field++)
                source.Text(group, headingNodes[tab][field], default, Caption(headingCaptions[tab][field]), chrome.Font, MapTextPresets.Create()[13]);
        }
        var usageBox = source.Rectangle(1);
        usage = new() { Name = "EventContentsUsage", Position = usageBox.Position }; body.AddChild(usage);
        var initial = read();
        usage.Configure(repository, text, chrome.Assets, chrome.SpriteShader, chrome.Font,
            new(0x2F9, initial.Rows.Count, 200, initial.UsedBytes, 512000));
        var listBox = source.Rectangle(57);
        clip = new() { Name = "EventContentsRows", Position = listBox.Position, Size = listBox.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        body.AddChild(clip); clip.GuiInput += input => { if (CanEdit()) scroll?.HandleWheel(input); };
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void SelectTab(int tab)
    {
        if ((uint)tab >= 3) throw new ArgumentOutOfRangeException(nameof(tab));
        if (abandoned) return;
        CurrentTab = tab; currentRows = null; Refresh();
    }
    internal void Refresh()
    {
        if (abandoned || read is null) return;
        var value = read();
        if (!ReferenceEquals(currentRows, value.Rows)) { currentRows = value.Rows; RebuildRows(value.Rows); }
        for (int index = 0; index < tabs.Count; index++)
        { tabs[index].Disabled = busy; tabs[index].Selected = CurrentTab == index; headings[index].Visible = CurrentTab == index; }
        for (int index = 0; index < commands.Count; index++)
            commands[index].Disabled = busy || !value.EnabledActions.Contains((EventEditorContentListAction)index);
        for (int index = 0; index < rows.Count; index++)
        { rows[index].Disabled = busy || !value.Rows[index].Enabled; rows[index].Selected = value.SelectedId == value.Rows[index].Id; }
        if (scroll is not null) scroll.ProcessMode = busy ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        usage.Apply(new(0x2F9, value.Rows.Count, 200, value.UsedBytes, 512000));
        chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy && value.CanConfirm, true); chrome.ApplyDefaultHint(value.Notice);
    }
    private void RebuildRows(IReadOnlyList<EventEditorContentListRow> items)
    {
        foreach (var row in rows) { row.Abandon(); clip.RemoveChild(row); row.QueueFree(); }
        rows.Clear(); var body = clip.GetParent();
        int previousScroll = scroll?.Value ?? 0;
        if (scroll is { } old) { old.SetProcessInput(false); body.RemoveChild(old); old.QueueFree(); }
        var box = source.Rectangle(25);
        scroll = new() { Name = "EventContentsScroll", Position = box.Position, Size = box.Size }; body.AddChild(scroll);
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0, items.Count * 52 - (int)clip.Size.Y),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        foreach (var item in items)
        {
            var value = item;
            var row = new EventEditorContentListButton { Name = "EventContentRow" + value.Id, InputAdmission = CanEdit };
            clip.AddChild(row); rows.Add(row);
            row.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, CurrentTab, value,
                () => { if (CanEdit() && value.Enabled) selected?.Invoke(value.Id); });
            row.GuiInput += input => { if (CanEdit()) scroll?.HandleWheel(input); };
        }
        scroll.SetValue(previousScroll); PositionRows();
    }
    private void PositionRows()
    {
        for (int index = 0; index < rows.Count; index++)
        {
            rows[index].Position = new(0, index * 52 - (scroll?.Value ?? 0));
            rows[index].Visible = rows[index].Position.Y > -rows[index].Size.Y && rows[index].Position.Y < clip.Size.Y;
        }
    }
    private void Accept() { if (CanEdit() && read?.Invoke().CanConfirm == true) accepted?.Invoke(); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void ApplyTitle(string value) { if (!abandoned) chrome.ApplyTitle(value); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; read = null; admitted = null; returned = accepted = null; selected = null; action = null;
        foreach (var row in rows) row.Abandon();
        foreach (var button in commands) { button.Abandon(); button.InputAdmission = null; }
        foreach (var tab in tabs) { tab.Disabled = true; tab.InputAdmission = null; }
        if (scroll is not null) { scroll.SetProcessInput(false); scroll.ProcessMode = ProcessModeEnum.Disabled; }
        chrome?.Abandon(); Hide();
    }
}

internal partial class EventEditorContentListButton : UiSourceButton
{
    private Rect2 hit;
    private Action? released;
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        int tab, EventEditorContentListRow value, Action released)
    {
        this.released = released;
        uint[] layouts = [0xB07081D2, 0x79717913, 0x42727054];
        var source = new UiSourceLayout(repository, layouts[tab]);
        Size = source.Rectangle(0).Size; hit = source.Rectangle(1); ActionMode = ActionModeEnum.Release;
        int[][] normal = [[2, 3], [2, 3], [2, 3]];
        int[][] focus = [[13, 15], [11, 13], [9, 11]];
        int[][] push = [[18, 20], [16, 18], [14, 16]];
        var states = new Control[5];
        for (int state = 0; state < states.Length; state++)
        {
            var group = new Control { MouseFilter = MouseFilterEnum.Ignore }; AddChild(group); states[state] = group;
            foreach (int node in state == 1 ? focus[tab] : state == 2 ? push[tab] : normal[tab])
                source.Art(group, node, source.Rectangle(node), assets, shader);
        }
        int[][] textNodes = [[5, 10, 11], [9, 5, 6, 7, 8], [5, 6, 7]];
        var contents = tab == 0 ? value.Message : tab == 1 ? value.Talk : value.Staging;
        if (contents.Count != textNodes[tab].Length) throw new InvalidDataException("Content row projection has the wrong field count.");
        var labels = new List<(UiSourceText Text, MapFontProfile Profile)>();
        for (int field = 0; field < textNodes[tab].Length; field++)
        {
            int node = textNodes[tab][field]; var text = source.Text(this, node, default, "", font);
            text.SetKnownColourLine(contents[field]); labels.Add((text, MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri)));
        }
        if (tab == 0 && value.Portrait is { } portrait)
        {
            var box = source.Rectangle(8);
            var face = new UiSourcePortrait { Name = "ContentSpeakerPortrait", Position = box.Position, Size = box.Size };
            AddChild(face); face.Configure(repository, assets, shader, portrait);
            source.Art(this, 9, source.Rectangle(9), assets, shader);
        }
        var presets = MapTextPresets.Create();
        ConfigureStates(states, state =>
        {
            foreach (var (text, profile) in labels)
            { text.Configure(font, profile.WithPreset(presets[state is 2 or 3 or 4 ? 13 : 0])); text.QueueRedraw(); }
        });
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
    internal void Abandon() { released = null; Disabled = true; InputAdmission = null; }
}
