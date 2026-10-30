using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum EventEditorProgramAction { MoveUp, MoveDown, Insert, Edit, Delete, Copy }
internal sealed record EventEditorProgramValues(IReadOnlyList<EventEditorChoiceRow> Rows, int? SelectedId,
    IReadOnlySet<EventEditorProgramAction> EnabledActions, bool CanConfirm, string Notice)
{
    internal int? UsedBytes { get; init; }
}

/// <summary>The original forty-condition/twenty-result list. All six source
/// actions operate through the draft owner; selecting a row cannot mutate it.</summary>
internal partial class EventEditorProgramView : Control
{
    private AdministrationPageChrome chrome = null!;
    private EventEditorUsageView usage = null!;
    private readonly List<UiSourceCommandButton> commands = [];
    private readonly List<EventEditorRowButton> rows = [];
    private UiSourceScrollbar? scroll;
    private Control clip = null!;
    private UiSourceLayout source = null!;
    private GameResourceRepository repository = null!;
    private Func<EventEditorProgramValues>? read;
    private Func<bool>? admitted;
    private Action? returned, accepted;
    private Action<int>? selected;
    private Action<EventEditorProgramAction>? action;
    private Func<int> scrollSpeed = null!;
    private IReadOnlyList<EventEditorChoiceRow>? currentRows;
    private int caption, capacity;
    private bool busy, abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, bool results,
        Func<EventEditorProgramValues> read, Func<bool> admitted, Action returned, Action accepted,
        Action<int> selected, Action<EventEditorProgramAction> action, Func<int> scrollSpeed)
    {
        this.repository = repository; this.read = read; this.admitted = admitted;
        this.returned = returned; this.accepted = accepted; this.selected = selected; this.action = action; this.scrollSpeed = scrollSpeed;
        caption = results ? 0x4A2 : 0x2F7; capacity = results ? 20 : 40;
        Name = results ? "EventEditorResults" : "EventEditorConditions"; ZIndex = 7; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, 0x91, RequestBack, Accept);
        source = new UiSourceLayout(repository, 0x4FEB10A8);
        var body = new Control { Name = "EventProgramBody", Position = new(0, 122),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(body);
        source.Art(body, 3, source.Rectangle(3), chrome.Assets, chrome.SpriteShader);
        source.Blur(body, 4, source.Rectangle(4)); source.Blur(body, 5, source.Rectangle(5));
        foreach (int node in new[] { 6, 22 }) source.Art(body, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        int[] captions = [0x93, 0x94, 0x95, 0x6AC, 0x4F, 0x96];
        for (int index = 0; index < captions.Length; index++)
        {
            var operation = (EventEditorProgramAction)index; var box = source.Rectangle(8 + index * 2);
            var button = new UiSourceCommandButton { Name = "EventProgram" + operation,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            body.AddChild(button); commands.Add(button);
            string label = messages.Records[captions[index]].Text ?? throw new InvalidDataException("Missing event list action caption.");
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, label,
                () => { if (CanEdit() && this.read?.Invoke().EnabledActions.Contains(operation) == true) this.action?.Invoke(operation); });
        }
        var statsBox = source.Rectangle(1);
        usage = new() { Name = "EventProgramUsage", Position = statsBox.Position };
        body.AddChild(usage);
        usage.Configure(repository, text, chrome.Assets, chrome.SpriteShader, chrome.Font,
            new(caption, read().Rows.Count, capacity, read().UsedBytes, 512000));
        var clipBox = source.Rectangle(30);
        clip = new() { Name = "EventProgramRows", Position = clipBox.Position, Size = clipBox.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        body.AddChild(clip); clip.GuiInput += input => { if (CanEdit()) scroll?.HandleWheel(input); };
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();
    }

    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void Refresh()
    {
        if (abandoned || read is null) return;
        var value = read();
        if (!ReferenceEquals(currentRows, value.Rows)) { currentRows = value.Rows; RebuildRows(value.Rows); }
        for (int index = 0; index < commands.Count; index++)
            commands[index].Disabled = busy || !value.EnabledActions.Contains((EventEditorProgramAction)index);
        for (int index = 0; index < rows.Count; index++)
        { rows[index].Disabled = busy || !value.Rows[index].Enabled; rows[index].Selected = value.Rows[index].Id == value.SelectedId; }
        if (scroll is not null) scroll.ProcessMode = busy ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        usage.Apply(new(caption, value.Rows.Count, capacity, value.UsedBytes, 512000));
        chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy && value.CanConfirm, true); chrome.ApplyDefaultHint(value.Notice);
    }
    private void RebuildRows(IReadOnlyList<EventEditorChoiceRow> items)
    {
        foreach (var row in rows) { row.Abandon(); clip.RemoveChild(row); row.QueueFree(); }
        rows.Clear();
        var body = clip.GetParent();
        if (scroll is { } old) { old.SetProcessInput(false); body.RemoveChild(old); old.QueueFree(); }
        var scrollBox = source.Rectangle(23);
        scroll = new() { Name = "EventProgramScroll", Position = scrollBox.Position, Size = scrollBox.Size };
        body.AddChild(scroll);
        float first = source.Rectangle(31).Position.Y - clip.Position.Y;
        float step = source.Rectangle(32).Position.Y - source.Rectangle(31).Position.Y;
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader,
            Math.Max(0, (int)(first + Math.Max(0, items.Count - 1) * step + source.Rectangle(31).Size.Y - clip.Size.Y)),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        foreach (var item in items)
        {
            var value = item; var row = new EventEditorRowButton { Name = "EventProgramRow" + value.Id,
                Size = source.Rectangle(31).Size, InputAdmission = CanEdit };
            clip.AddChild(row); rows.Add(row);
            row.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, value.Caption,
                () => { if (CanEdit() && value.Enabled) selected?.Invoke(value.Id); });
            row.GuiInput += input => { if (CanEdit()) scroll?.HandleWheel(input); };
        }
        PositionRows();
    }
    private void PositionRows()
    {
        float first = source.Rectangle(31).Position.Y - clip.Position.Y;
        float step = source.Rectangle(32).Position.Y - source.Rectangle(31).Position.Y;
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
        abandoned = true; read = null; admitted = null; returned = accepted = null; selected = null; action = null;
        foreach (var row in rows) row.Abandon();
        foreach (var button in commands) { button.Abandon(); button.InputAdmission = null; }
        if (scroll is not null) { scroll.SetProcessInput(false); scroll.ProcessMode = ProcessModeEnum.Disabled; }
        chrome?.Abandon(); Hide();
    }
}
