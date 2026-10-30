using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record EventEditorSectionRows(IReadOnlyList<EventEditorChoiceRow> Rows);
internal sealed record EventEditorPageValues(string Name, string Description, byte? Category,
    bool Automatic, bool Repeat, IReadOnlyList<EventEditorSectionRows> Sections, bool CanConfirm, string Notice)
{
    internal int? UsedBytes { get; init; }
}

/// <summary>The normal event authoring page. Its four list projections are
/// owned by the application draft, including the separate key-person consumer.</summary>
internal partial class EventEditorView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<UiSourceCommandButton> commands = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private readonly List<EventEditorRowButton> rows = [];
    private readonly List<Control> metadataIcons = [];
    private UiSourceText eventName = null!;
    private EventEditorLiteralParagraph description = null!;
    private EventEditorSpaceView space = null!;
    private Control clip = null!;
    private UiSourceScrollbar? scroll;
    private GameResourceRepository repository = null!;
    private UiSourceLayout source = null!;
    private Func<EventEditorPageValues>? read;
    private Func<bool>? admitted;
    private Action? returned, accepted;
    private Action<int>? editSection;
    private Action<int, int>? editRow;
    private Func<int> scrollSpeed = null!;
    private IReadOnlyList<EventEditorChoiceRow>? currentRows;
    private bool abandoned, busy;
    internal int CurrentTab { get; private set; }

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Func<EventEditorPageValues> read, Func<bool> admitted, Action returned, Action accepted,
        Action<int> editSection, Action<int, int> editRow, Func<int> scrollSpeed)
    {
        this.repository = repository; this.read = read; this.admitted = admitted;
        this.returned = returned; this.accepted = accepted; this.editSection = editSection;
        this.editRow = editRow; this.scrollSpeed = scrollSpeed;
        Name = "EventEditor"; ZIndex = 6; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, 0x91, RequestBack, Accept);
        source = new UiSourceLayout(repository, 0x16CC742A);
        var body = new Control { Name = "EventEditorBody", Position = new(0, 122),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(body);
        source.Art(body, 3, source.Rectangle(3), chrome.Assets, chrome.SpriteShader);
        source.Blur(body, 4, source.Rectangle(4)); source.Blur(body, 5, source.Rectangle(5));
        foreach (int node in new[] { 6, 20, 28, 29, 31, 32 })
            source.Art(body, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Caption(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing event authoring caption.");
        int[] mounts = [8, 10, 12, 14, 16], captions = [0x2F6, 0x2F7, 0x2F8, 0x2F9, 0x4A2];
        for (int index = 0; index < mounts.Length; index++)
        {
            int field = index; var box = source.Rectangle(mounts[index]);
            var button = new UiSourceCommandButton { Name = "EventEditorSection" + field,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            body.AddChild(button); commands.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Caption(captions[index]),
                () => { if (CanEdit()) this.editSection?.Invoke(field); });
        }
        for (int index = 0; index < 4; index++)
        {
            int tab = index; int[] tabMounts = [40, 42, 43, 44]; var box = source.Rectangle(tabMounts[index]);
            var button = new UiSourceTabButton { Name = "EventEditorTab" + tab,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            body.AddChild(button); tabs.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Caption(captions[index + 1]),
                index == 0 ? 0x172EDC24u : index == 3 ? 0xD45873AAu : 0x9AFB27B7u);
            button.Pressed += () => { if (CanEdit()) SelectTab(tab); };
        }
        eventName = source.Text(body, 26, default, "", chrome.Font);
        var paragraphBox = source.Rectangle(27);
        description = new EventEditorLiteralParagraph { Name = "EventEditorDescription",
            Position = paragraphBox.Position, Size = paragraphBox.Size };
        body.AddChild(description); description.Configure(chrome.Font,
            MapFontProfile.FromNode(source.Nodes[27], source.Source.Source.ResourceUri));
        foreach (int node in new[] { 22, 23, 24, 25 })
            metadataIcons.Add(source.Art(body, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader));
        var spaceBox = source.Rectangle(1);
        space = new() { Name = "EventEditorSpace", Position = spaceBox.Position };
        body.AddChild(space); space.Configure(repository, text, chrome.Assets, chrome.SpriteShader, chrome.Font);
        var boxClip = source.Rectangle(51);
        clip = new Control { Name = "EventEditorRows", Position = boxClip.Position, Size = boxClip.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        body.AddChild(clip);
        clip.GuiInput += input => { if (CanEdit()) scroll?.HandleWheel(input); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Refresh();
    }

    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void SelectTab(int tab)
    {
        if ((uint)tab >= 4) throw new ArgumentOutOfRangeException(nameof(tab));
        if (abandoned) return;
        CurrentTab = tab; currentRows = null; Refresh();
    }
    internal void Refresh()
    {
        if (abandoned || read is null) return;
        var value = read(); eventName.Text = value.Name; description.Apply(value.Description);
        // h/i and j/k are the original opposite automatic/repeat badges.
        metadataIcons[0].Visible = value.Automatic; metadataIcons[1].Visible = !value.Automatic;
        metadataIcons[2].Visible = !value.Repeat; metadataIcons[3].Visible = value.Repeat;
        for (int index = 0; index < tabs.Count; index++) { tabs[index].Selected = CurrentTab == index; tabs[index].Disabled = busy; }
        foreach (var button in commands) button.Disabled = busy;
        var items = CurrentTab < value.Sections.Count ? value.Sections[CurrentTab].Rows : Array.Empty<EventEditorChoiceRow>();
        if (!ReferenceEquals(items, currentRows)) { currentRows = items; RebuildRows(items); }
        for (int index = 0; index < rows.Count; index++) rows[index].Disabled = busy || !items[index].Enabled;
        if (scroll is not null) scroll.ProcessMode = busy ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        space.Apply(value.UsedBytes);
        chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy && value.CanConfirm, true); chrome.ApplyDefaultHint(value.Notice);
    }
    private void RebuildRows(IReadOnlyList<EventEditorChoiceRow> items)
    {
        foreach (var row in rows) { row.Abandon(); clip.RemoveChild(row); row.QueueFree(); }
        rows.Clear();
        var body = clip.GetParent();
        if (scroll is { } old) { old.SetProcessInput(false); body.RemoveChild(old); old.QueueFree(); }
        var scrollBox = source.Rectangle(33);
        scroll = new() { Name = "EventEditorScroll", Position = scrollBox.Position, Size = scrollBox.Size };
        body.AddChild(scroll);
        float first = source.Rectangle(52).Position.Y - clip.Position.Y;
        float step = source.Rectangle(53).Position.Y - source.Rectangle(52).Position.Y;
        float height = source.Rectangle(52).Size.Y;
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader,
            Math.Max(0, (int)(first + Math.Max(0, items.Count - 1) * step + height - clip.Size.Y)),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        foreach (var item in items)
        {
            var value = item;
            var row = new EventEditorRowButton { Name = "EventEditorRow" + value.Id,
                Size = source.Rectangle(52).Size, InputAdmission = CanEdit };
            clip.AddChild(row); rows.Add(row);
            row.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, value.Caption,
                () => { if (CanEdit() && value.Enabled) editRow?.Invoke(CurrentTab, value.Id); });
            row.GuiInput += input => { if (CanEdit()) scroll?.HandleWheel(input); };
        }
        PositionRows();
    }
    private void PositionRows()
    {
        float first = source.Rectangle(52).Position.Y - clip.Position.Y;
        float step = source.Rectangle(53).Position.Y - source.Rectangle(52).Position.Y;
        for (int index = 0; index < rows.Count; index++)
        {
            rows[index].Position = new(0, first + index * step - (scroll?.Value ?? 0));
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
        abandoned = true; read = null; admitted = null; returned = accepted = null; editSection = null; editRow = null;
        foreach (var row in rows) row.Abandon();
        foreach (var button in commands) { button.Abandon(); button.InputAdmission = null; }
        foreach (var tab in tabs) { tab.Disabled = true; tab.InputAdmission = null; }
        if (scroll is not null) { scroll.SetProcessInput(false); scroll.ProcessMode = ProcessModeEnum.Disabled; }
        chrome?.Abandon(); Hide();
    }
}

/// <summary>Literal editor text is wrapped with the authored paragraph's
/// actual face, scale and line spacing, without speech-message substitution.</summary>
internal partial class EventEditorLiteralParagraph : Control
{
    private MapFont font = null!;
    private MapFontProfile profile = null!;
    private string? current;
    internal void Configure(MapFont font, MapFontProfile profile)
    { this.font = font; this.profile = profile; MouseFilter = MouseFilterEnum.Ignore; }
    internal void Apply(string value)
    {
        if (value == current) return;
        current = value;
        foreach (Node child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        float advance = font.Source.Record('\u3042', profile.Face).Height * profile.ScaleY + profile.VerticalSpace;
        int allowedLf = Math.Max(0, (int)(Size.Y / advance) - 1);
        string wrapped = DeploymentNarrationPlainParagraph.WrapKnownColourParagraph(value,
            line => (int)font.Measure(line, profile).Width, (int)Size.X, allowedLf);
        var lines = DeploymentNarrationParagraphPlan.Runs(wrapped);
        for (int index = 0; index < lines.Count; index++)
        {
            var line = new UiSourceText { Position = new(0, index * advance), Size = new(Size.X, advance) };
            AddChild(line); line.Configure(font, profile);
            line.SetKnownColourLine(string.Concat(lines[index].Runs.Select(run => run.Text)));
        }
    }
}
