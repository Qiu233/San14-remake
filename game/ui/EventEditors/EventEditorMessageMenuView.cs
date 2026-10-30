using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum EventEditorMessageAction { Create, Edit, Delete, Select }
internal sealed record EventEditorMessageMenuPublication(int Count, int? UsedBytes,
    bool CanCreate, bool CanEdit, bool CanDelete)
{ internal bool CanSelect { get; init; } }

/// <summary>The ordinary three-action article menu, independently of the
/// special Contents-state article picker and the five-action event menu.</summary>
internal partial class EventEditorMessageMenuView : Control
{
    private AdministrationPageChrome chrome = null!;
    private EventEditorUsageView usage = null!;
    private readonly List<EventEditorCommandButton> buttons = [];
    private EventEditorMessageAction[] actions = [];
    private EventEditorMessageMenuPublication publication = null!;
    private Func<bool>? admitted;
    private Action<EventEditorMessageAction>? selected;
    private Action? returned;
    private bool abandoned, busy;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        EventEditorMessageMenuPublication publication, Func<bool> admitted,
        Action<EventEditorMessageAction> selected, Action returned, bool selecting = false)
    {
        this.publication = publication; this.admitted = admitted; this.selected = selected; this.returned = returned;
        actions = selecting ? [EventEditorMessageAction.Create, EventEditorMessageAction.Edit,
            EventEditorMessageAction.Select, EventEditorMessageAction.Delete]
            : [EventEditorMessageAction.Create, EventEditorMessageAction.Edit, EventEditorMessageAction.Delete];
        Name = "EventEditorMessageMenu"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x81, RequestBack, () => { }, returnOnly: true);
        var root = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_05_00_edit_register_pk"));
        var offset = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_04_00_btn_set_offset_00_new"));
        Vector2 mount = root.Rectangle(3).Position;
        foreach (int node in new[] { 1, 2 })
            offset.Art(chrome.Canvas, node, offset.Rectangle(node, mount), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        for (int index = 0; index < actions.Length; index++)
        {
            var action = actions[index];
            var box = offset.Rectangle(3 + index * 3, mount);
            var button = new EventEditorCommandButton { Name = "EventMessageMenuAction" + index,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            chrome.Canvas.AddChild(button); buttons.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text.SourceLocale,
                messages.Records[action == EventEditorMessageAction.Select ? 0x99 : 0x86 + (int)action].Text
                    ?? throw new InvalidDataException("Missing article menu caption."), false,
                () => { if (CanEdit() && Enabled(action)) this.selected?.Invoke(action); });
        }
        usage = new() { Name = "EventMessageUsage", Position = root.Rectangle(10).Position };
        chrome.Canvas.AddChild(usage);
        usage.Configure(repository, text, chrome.Assets, chrome.SpriteShader, chrome.Font,
            new(0x2EA, publication.Count, 1000, publication.UsedBytes, 122000));
        SetMeta("source_layout", root.Source.Source.ResourceUri); Apply(publication);
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    private bool Enabled(EventEditorMessageAction action) => action switch
    { EventEditorMessageAction.Create => publication.CanCreate, EventEditorMessageAction.Edit => publication.CanEdit,
        EventEditorMessageAction.Select => publication.CanSelect, _ => publication.CanDelete };
    internal void Apply(EventEditorMessageMenuPublication value)
    {
        if (abandoned) return;
        publication = value;
        for (int i = 0; i < buttons.Count; i++) buttons[i].Disabled = busy || !Enabled(actions[i]);
        usage.Apply(new(0x2EA, value.Count, 1000, value.UsedBytes, 122000));
        chrome.ApplyReturn(!busy);
    }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Apply(publication); } }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; selected = null; returned = null;
        foreach (var button in buttons) button.Abandon(); chrome?.Abandon(); Hide();
    }
    public override void _ExitTree() => Abandon();
}
