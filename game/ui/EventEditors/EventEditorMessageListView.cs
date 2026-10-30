using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

internal enum EventEditorMessageListMode { Edit, Select, Delete }

/// <summary>The normal article manager and the Contents article picker.
/// Display labels are one-based; callbacks always retain the real sparse slot.</summary>
internal partial class EventEditorMessageListView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceLayout source = null!;
    private EventEditorUsageView usage = null!;
    private Control clip = null!;
    private UiSourceScrollbar? scroll;
    private UiSourceBottomButton? selectAll;
    private readonly List<EventEditorMessageListRow> pool = [];
    private readonly HashSet<int> selected = [];
    private IReadOnlyList<EditedEventMessage> records = [];
    private GameResourceRepository repository = null!;
    private EventEditorMessageListMode mode;
    private Func<bool>? admitted;
    private Func<int>? scrollSpeed;
    private Action<IReadOnlyList<int>>? chosen;
    private Action? returned;
    private string selectCaption = "", clearCaption = "";
    private float firstY, pitch;
    private Rect2 scrollBox, rowBox;
    private bool abandoned, busy;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<EditedEventMessage> records, int? usedBytes, EventEditorMessageListMode mode,
        Func<bool> admitted, Action<IReadOnlyList<int>> chosen, Action returned, Func<int> scrollSpeed)
    {
        this.repository = repository; this.mode = mode; this.admitted = admitted;
        this.chosen = chosen; this.returned = returned; this.scrollSpeed = scrollSpeed;
        Name = "EventEditorMessageList"; ZIndex = 7; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        int title = mode switch { EventEditorMessageListMode.Edit => 0x87, EventEditorMessageListMode.Select => 0x99, _ => 0x88 };
        chrome.Configure(repository, text, title, RequestBack, Accept,
            returnOnly: mode != EventEditorMessageListMode.Delete);
        source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_31_21_event_edit_message"));
        var body = new Control { Name = "EventMessageBody", Position = new(0, 145),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(body);
        source.Art(body, 3, source.Rectangle(3), chrome.Assets, chrome.SpriteShader);
        foreach (int node in new[] { 4, 5 }) source.Blur(body, node, source.Rectangle(node));
        foreach (int node in new[] { 6, 8 })
            source.Art(body, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        usage = new() { Name = "EventMessageUsage", Position = source.Rectangle(1).Position };
        body.AddChild(usage);
        usage.Configure(repository, text, chrome.Assets, chrome.SpriteShader, chrome.Font,
            new(0x2EA, records.Count, 1000, usedBytes, 122000));
        var box = source.Rectangle(16);
        clip = new() { Name = "EventMessageRows", Position = box.Position, Size = box.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        body.AddChild(clip);
        rowBox = source.Rectangle(17); firstY = rowBox.Position.Y - box.Position.Y;
        pitch = source.Rectangle(18).Position.Y - rowBox.Position.Y;
        if (pitch <= 0) throw new InvalidDataException("Article list row pitch must be positive.");
        scrollBox = source.Rectangle(9);
        // A bounded visible pool retains the authored row geometry for all
        // 1000 sparse library slots, without allocating one control per slot.
        int poolSize = (int)Math.Ceiling(box.Size.Y / pitch) + 2;
        for (int i = 0; i < poolSize; i++)
        {
            var row = new EventEditorMessageListRow { Name = "EventMessageRow" + i,
                Size = rowBox.Size, InputAdmission = CanEdit };
            clip.AddChild(row); pool.Add(row);
            row.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                mode == EventEditorMessageListMode.Delete, Activate);
            row.GuiInput += Wheel;
        }
        clip.GuiInput += Wheel;
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Data(int id) => messages.Records[id].Text ?? throw new InvalidDataException($"Missing article Data1/{id:X}.");
        //500FB0/500DE0 bind these footer hints through the Game message
        //consumer. The small select/clear captions below remain Data1.
        int hintId = mode switch { EventEditorMessageListMode.Edit => 0x28A5,
            EventEditorMessageListMode.Select => 0x28D4, _ => 0x28A6 };
        chrome.ApplyDefaultHint(RegisteredOfficerExchangeMessages.Format(repository, text.SourceLocale, hintId));
        if (mode == EventEditorMessageListMode.Delete)
        {
            selectCaption = Data(0x3A); clearCaption = Data(0x3B);
            var footer = new UiSourceLayout(repository, 0xD8913AB2);
            selectAll = new() { Name = "EventMessageSelectAll", Position = footer.Rectangle(14, new(0, 904)).Position,
                InputAdmission = CanEdit };
            chrome.Canvas.AddChild(selectAll);
            selectAll.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, selectCaption, ToggleAll);
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Apply(records, usedBytes);
    }

    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    private void Wheel(InputEvent input) { if (CanEdit()) scroll?.HandleWheel(input); }
    internal void Apply(IReadOnlyList<EditedEventMessage> records, int? usedBytes)
    {
        if (abandoned) return;
        foreach (var record in records) record.Validate();
        if (records.Count > 1000 || records.Select(row => row.Slot).Distinct().Count() != records.Count)
            throw new InvalidDataException("Article list contains duplicate or excessive slots.");
        // Library physical-slot order is supplied by the Data owner.
        this.records = records.ToArray();
        selected.IntersectWith(records.Select(row => row.Slot));
        usage.Apply(new(0x2EA, records.Count, 1000, usedBytes, 122000));
        int value = scroll?.Value ?? 0;
        if (scroll is { } old) { old.ProcessMode = ProcessModeEnum.Disabled; old.GetParent().RemoveChild(old); old.QueueFree(); }
        var next = new UiSourceScrollbar { Name = "EventMessageScroll", Position = scrollBox.Position, Size = scrollBox.Size };
        clip.GetParent().AddChild(next); scroll = next;
        int range = Math.Max(0, (int)(firstY + Math.Max(0, records.Count - 1) * pitch + rowBox.Size.Y - clip.Size.Y));
        next.Configure(repository, chrome.Assets, chrome.SpriteShader, range, scrollSpeed!(),
            _ => { if (!abandoned && ReferenceEquals(scroll, next)) PositionRows(); }, currentScrollSpeed: scrollSpeed);
        next.SetValue(value); Refresh();
    }
    private void PositionRows()
    {
        int offset = scroll?.Value ?? 0;
        int first = Math.Max(0, (int)Math.Floor((offset - firstY) / pitch));
        for (int i = 0; i < pool.Count; i++)
        {
            var row = pool[i]; int index = first + i;
            if (index >= records.Count) { row.Hide(); row.Clear(); continue; }
            var value = records[index];
            row.Position = new(0, firstY + index * pitch - offset);
            row.Bind(value, selected.Contains(value.Slot)); row.Disabled = busy;
            row.Visible = row.Position.Y > -row.Size.Y && row.Position.Y < clip.Size.Y;
        }
    }
    private void Refresh()
    {
        PositionRows();
        if (scroll is not null) scroll.ProcessMode = busy ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        if (selectAll is not null)
        {
            selectAll.Disabled = busy || records.Count == 0;
            selectAll.ApplyCaption(records.Count != 0 && selected.Count == records.Count ? clearCaption : selectCaption);
        }
        chrome.ApplyReturn(!busy);
        chrome.ApplyDecision(!busy && selected.Count != 0, mode == EventEditorMessageListMode.Delete);
    }
    private void Activate(int slot)
    {
        if (!CanEdit() || !records.Any(record => record.Slot == slot)) return;
        if (mode != EventEditorMessageListMode.Delete) { chosen?.Invoke([slot]); return; }
        if (!selected.Remove(slot)) selected.Add(slot); Refresh();
    }
    private void ToggleAll()
    {
        if (!CanEdit()) return;
        if (selected.Count == records.Count) selected.Clear();
        else foreach (var record in records) selected.Add(record.Slot);
        Refresh();
    }
    private void Accept()
    { if (CanEdit() && mode == EventEditorMessageListMode.Delete && selected.Count != 0)
        chosen?.Invoke(records.Where(record => selected.Contains(record.Slot)).Select(record => record.Slot).ToArray()); }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; admitted = null; chosen = null; returned = null;
        foreach (var row in pool) row.Abandon();
        if (scroll is not null) scroll.ProcessMode = ProcessModeEnum.Disabled;
        if (selectAll is not null) selectAll.Disabled = true;
        chrome?.Abandon(); Hide();
    }
    public override void _ExitTree() => Abandon();
}

internal partial class EventEditorMessageListRow : UiSourceButton
{
    private Rect2 hit;
    private UiSourceText body = null!, label = null!;
    private Control? tick;
    private int? slot;
    private Action<int>? released;
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, bool deleting, Action<int> released)
    {
        this.released = released;
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,
            deleting ? "02_31_21_event_edit_offset_00" : "02_31_21_event_edit_offset_01"));
        hit = source.Rectangle(1); ActionMode = ActionModeEnum.Release;
        int[][] artwork = deleting ? [[2,3], [8,9,10,11], [13,14,15,16], [2,3], [2,3]]
            : [[2], [6,7], [9,10], [2], [2]];
        var states = new Control[5];
        for (int state = 0; state < states.Length; state++)
        {
            states[state] = new Control { MouseFilter = MouseFilterEnum.Ignore }; AddChild(states[state]);
            foreach (int node in artwork[state]) source.Art(states[state], node, source.Rectangle(node), assets, shader);
        }
        body = source.Text(this, deleting ? 5 : 3, default, "", font);
        label = source.Text(this, deleting ? 6 : 4, default, "", font);
        if (deleting) tick = source.Art(this, 4, source.Rectangle(4), assets, shader);
        ConfigureStates(states);
        Pressed += () => { if (!Disabled && SourceInputAdmitted && slot is int value) this.released?.Invoke(value); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
    internal void Bind(EditedEventMessage message, bool selected)
    {
        slot = message.Slot; body.Text = message.Text;
        label.Text = (message.Slot + 1).ToString(CultureInfo.InvariantCulture);
        Selected = selected; if (tick is not null) tick.Visible = selected;
        SetMeta("message_slot", message.Slot);
    }
    internal void Clear() { slot = null; Disabled = true; }
    internal void Abandon() { released = null; InputAdmission = null; Clear(); }
}
