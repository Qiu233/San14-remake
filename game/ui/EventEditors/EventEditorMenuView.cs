using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record EventEditorMenuAction(int Id, string Caption, bool Enabled, string? UnresolvedOwner = null);
internal sealed record EventEditorMenuPublication(bool MakeEvent, int EventCount, int MessageCount,
    int? EventBytes, int? MessageBytes, IReadOnlyList<EventEditorMenuAction> Actions);

/// <summary>The retained normal title event-editor command or MakeEvent menu.</summary>
internal partial class EventEditorMenuView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!;
    private UiSourceHeader header = null!;
    private EventEditorUsageView eventUsage = null!;
    private EventEditorUsageView? messageUsage;
    private UiSourceRoundButton back = null!;
    private readonly List<EventEditorCommandButton> buttons = [];
    private EventEditorMenuPublication publication = null!;
    private Func<bool>? admitted;
    private Action<int>? command;
    private Action? returned;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        EventEditorMenuPublication publication, Func<bool> admitted, Action<int> command, Action returned)
    {
        this.repository = repository; this.text = text; this.publication = publication;
        this.admitted = admitted; this.command = command; this.returned = returned;
        if (publication.Actions.Count != 5) throw new InvalidDataException("Event menu requires its five original actions.");
        Name = publication.MakeEvent ? "EventEditorMakeMenu" : "EventEditorCommandMenu";
        MouseFilter = MouseFilterEnum.Stop; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var root = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_05_00_edit_register_pk"));
        var offset = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_04_00_btn_set_offset_00_new"));
        Vector2 mount = root.Rectangle(3).Position;
        foreach (int node in new[] { 1, 2 })
            offset.Art(canvas, node, offset.Rectangle(node, mount), assets, shader);
        header = new() { Name = "EventEditorHeader" }; canvas.AddChild(header);
        header.Configure(repository, assets, shader, font, Message(publication.MakeEvent ? 0x80 : 0x2E7));
        for (int i = 0; i < 5; i++)
        {
            int index = i; Rect2 box = offset.Rectangle(3 + i * 3, mount);
            var button = new EventEditorCommandButton { Name = "EventMenuAction" + i,
                Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
            canvas.AddChild(button);
            button.Configure(repository, assets, shader, font, text.SourceLocale, publication.Actions[i].Caption,
                !publication.MakeEvent, () =>
                {
                    if (!ReadAdmission() || !this.publication.Actions[index].Enabled) return;
                    this.command?.Invoke(this.publication.Actions[index].Id);
                });
            buttons.Add(button);
        }
        eventUsage = new() { Name = "EventSlotUsage", Position = root.Rectangle(10).Position };
        canvas.AddChild(eventUsage);
        eventUsage.Configure(repository, text, assets, shader, font, EventUsage(publication));
        if (!publication.MakeEvent)
        {
            messageUsage = new() { Name = "MessageSlotUsage", Position = root.Rectangle(11).Position };
            canvas.AddChild(messageUsage);
            messageUsage.Configure(repository, text, assets, shader, font, MessageUsage(publication));
        }
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        back = new() { Name = "ReturnFromEventMenu", Position = footer.Rectangle(1, new(0, 904)).Position,
            InputAdmission = ReadAdmission };
        canvas.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru", RequestBack, UiAudioCue.Cancel);
        SetMeta("source_layout", root.Source.Source.ResourceUri);
        SetMeta("source_usage_mounts", publication.MakeEvent ? "10" : "10,11");
        Apply(publication); Resized += Fit; Fit();
    }

    internal void Apply(EventEditorMenuPublication publication)
    {
        if (abandoned) return;
        if (publication.MakeEvent != this.publication.MakeEvent || publication.Actions.Count != 5)
            throw new InvalidDataException("Event menu kind is retained for its lifetime.");
        this.publication = publication;
        for (int i = 0; i < buttons.Count; i++)
        {
            var action = publication.Actions[i]; buttons[i].Caption = action.Caption;
            buttons[i].Disabled = !action.Enabled;
            buttons[i].SetMeta("unresolved_owner", action.UnresolvedOwner ?? "");
        }
        eventUsage.Apply(EventUsage(publication)); messageUsage?.Apply(MessageUsage(publication));
    }
    private static EventEditorUsagePublication EventUsage(EventEditorMenuPublication p) => new(0x249, p.EventCount, 100, p.EventBytes, 512000);
    private static EventEditorUsagePublication MessageUsage(EventEditorMenuPublication p) => new(0x2EA, p.MessageCount, 1000, p.MessageBytes, 122000);
    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException($"Missing event menu Data1/{id:X}.");
    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    internal void RegisterHeader(UiSourceHeaderStack stack) => header.Publish(stack);
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    internal void Abandon()
    {
        if (abandoned) return; abandoned = true; admitted = null; command = null; returned = null;
        foreach (var button in buttons) button.Abandon();
        if (back is not null) { back.InputAdmission = null; back.Disabled = true; }
        Hide();
    }
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree() { Abandon(); font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
}
