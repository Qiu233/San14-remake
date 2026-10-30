using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record EventEditorContentPreview(IReadOnlyList<StoryActorVisual> Actors,
    string Speaker, string Message, string? UnresolvedOwner = null);

/// <summary>One detached six-statement performance row. Source directories
/// supply selections; only accepting this page publishes its working values.</summary>
internal partial class EventEditorContentView : Control
{
    private AdministrationPageChrome chrome = null!;
    private EventEditorSpaceView space = null!;
    private StoryPresentationView scene = null!;
    private UiSourceText music = null!, sound = null!;
    private readonly List<UiSourceCommandButton> commands = [];
    private readonly string[] captions = new string[6];
    private readonly string[] standingCaptions = new string[4];
    private readonly string[] speakerCaptions = new string[4];
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private EditedEventContentCatalog catalog = null!;
    private Func<bool>? admitted;
    private Action<EditedEventContentValues?>? closed;
    private Func<int> scrollSpeed = null!;
    private Action<int, Action<int?>>? editMessage;
    private Func<EditedEventContentValues, EventEditorContentPreview>? readPreview;
    private Func<int?>? usedBytes;
    private EventEditorChoiceView? choice;
    private EventEditorParameterView? standing;
    private EditedEventContentValues? painted;
    private string unknown = "", notice = "";
    private bool busy, abandoned, messagePending;
    internal EditedEventContentValues Working { get; private set; } = null!;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        EditedEventContentCatalog catalog, EditedEventContentValues initial,
        Func<bool> admitted, Action<EditedEventContentValues?> closed, Func<int> scrollSpeed,
        Action<int, Action<int?>> editMessage,
        Func<EditedEventContentValues, EventEditorContentPreview>? readPreview = null,
        Func<int?>? usedBytes = null)
    {
        this.repository = repository; this.text = text; this.catalog = catalog;
        this.admitted = admitted; this.closed = closed; this.scrollSpeed = scrollSpeed;
        this.editMessage = editMessage; this.readPreview = readPreview; this.usedBytes = usedBytes;
        Working = initial;
        Name = "EventEditorContent"; ZIndex = 8; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, 0x91, RequestBack, Accept);
        var source = new UiSourceLayout(repository, 0x24C5A7C7);
        var body = new Control { Name = "EventContentBody", Position = new(0, 122),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(body);
        source.Art(body, 3, source.Rectangle(3), chrome.Assets, chrome.SpriteShader);
        source.Blur(body, 4, source.Rectangle(4)); source.Blur(body, 5, source.Rectangle(5));
        foreach (int node in new[] { 6, 23, 25, 28, 30 })
            source.Art(body, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ??
            throw new InvalidDataException("Missing event performance caption.");
        int[] captionIds = [0x300, 0x2FD, 0x2EA, 0x303, 0x304, 0x2FE];
        int[] standingIds = [0x30B, 0x30C, 0x30D, 0x7B8];
        int[] speakerIds = [0x53F, 0x7B9, 0x7BA, 0x7BB];
        for (int index = 0; index < 4; index++)
        { standingCaptions[index] = Message(standingIds[index]); speakerCaptions[index] = Message(speakerIds[index]); }
        for (int index = 0; index < captions.Length; index++)
        {
            int field = index; captions[index] = Message(captionIds[index]);
            var box = source.Rectangle(7 + 2 * index);
            var button = new UiSourceCommandButton { Name = "EventContentField" + field,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            body.AddChild(button); commands.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, captions[index],
                () => { if (CanEdit()) Edit(field); });
        }
        source.Text(body, 24, default, captions[3], chrome.Font);
        source.Text(body, 29, default, captions[4], chrome.Font);
        music = source.Text(body, 26, default, "", chrome.Font);
        sound = source.Text(body, 31, default, "", chrome.Font);
        var stats = source.Rectangle(1);
        space = new() { Name = "EventContentSpace", Position = stats.Position };
        body.AddChild(space); space.Configure(repository, text, chrome.Assets, chrome.SpriteShader, chrome.Font);
        var previewBox = source.Rectangle(19);
        var preview = new Control { Name = "EventContentPreview", Position = previewBox.Position,
            Size = previewBox.Size, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        body.AddChild(preview);
        scene = new() { Size = previewBox.Size, MouseFilter = MouseFilterEnum.Ignore };
        preview.AddChild(scene);
        // This is the ordinary retained visual consumer, without a story
        // executor, audio owner, clock wait or message-advance permission.
        scene.Configure(repository, text, () => false);
        scene.ZIndex = 0;
        unknown = text.Get("detail_unknown");
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Refresh();
    }

    private bool Available() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    private bool CanEdit() => Available() && choice is null && standing is null && !messagePending;

    internal void Refresh()
    {
        if (abandoned) return;
        music.Text = Caption(catalog.Music, Working.MusicId);
        sound.Text = Caption(catalog.Sounds, Working.SoundEffectId);
        space.Apply(usedBytes?.Invoke());
        foreach (var button in commands) button.Disabled = busy || messagePending || choice is not null || standing is not null;
        chrome.ApplyReturn(!busy && !messagePending && choice is null && standing is null);
        chrome.ApplyDecision(!busy && !messagePending && choice is null && standing is null, true);
        chrome.ApplyDefaultHint(notice);
        if (painted == Working) return;
        painted = Working;
        var visual = readPreview?.Invoke(Working);
        scene.SetBackground(Working.BackgroundId);
        scene.SetIllustration(0); scene.SetSmallIllustration(0);
        scene.ApplyActors(visual?.Actors ?? Array.Empty<StoryActorVisual>(), null);
        string? missing = visual?.UnresolvedOwner;
        if (visual is null && (Working.FirstStandingOfficerId > 0 || Working.SecondStandingOfficerId > 0 ||
            Working.ThirdStandingOfficerId > 0 || Working.MessageSlot != 0))
            missing = "event_editor.content.preview.title_person_and_edited_message";
        if (Working.IllustrationId > 0)
        {
            switch (Working.IllustrationKind)
            {
                case 13: scene.SetIllustration(Working.IllustrationId); break;
                case 14: scene.SetBackground(Working.IllustrationId, 14); break;
                case 16: scene.SetSmallIllustration(Working.IllustrationId); break;
                default: missing = "event_editor.content.preview.illustration_kind"; break;
            }
        }
        scene.PresentEventEditorPreview(visual?.Speaker ?? "", visual?.Message ?? "");
        scene.SetMeta("unresolved_owner", missing ?? "");
    }

    private string Caption(IReadOnlyList<EditedEventContentChoice> rows, int value) =>
        rows.FirstOrDefault(row => row.Value == value)?.Caption ?? unknown;

    private void Edit(int field)
    {
        switch (field)
        {
            case 0: Select(captions[0], catalog.Backgrounds, Working.BackgroundId, null,
                value => Working = Working with { BackgroundId = value.Value }); break;
            case 1: OpenStanding(); break;
            case 2:
                messagePending = true; Refresh();
                editMessage?.Invoke(Working.MessageSlot, selected =>
                {
                    if (abandoned || !messagePending) return;
                    messagePending = false;
                    if (selected is int slot) Working = Working with { MessageSlot = slot };
                    // A new article can publish a newer library while the
                    // performance page is open. The article owner admits it.
                    painted = null; Refresh();
                });
                break;
            case 3: Select(captions[3], catalog.Music, Working.MusicId, null,
                value => Working = Working with { MusicId = value.Value }); break;
            case 4: Select(captions[4], catalog.Sounds, Working.SoundEffectId, null,
                value => Working = Working with { SoundEffectId = value.Value }); break;
            case 5: Select(captions[5], catalog.Illustrations, Working.IllustrationId, Working.IllustrationKind,
                value => Working = Working with { IllustrationId = value.Value, IllustrationKind = value.Kind!.Value }); break;
        }
    }

    private void Select(string heading, IReadOnlyList<EditedEventContentChoice> rows,
        int selectedValue, int? selectedKind, Action<EditedEventContentChoice> changed,
        Action? refreshed = null)
    {
        var view = new EventEditorChoiceView(); choice = view; AddChild(view);
        int? selected = null;
        var entries = new EventEditorChoiceRow[rows.Count];
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            // Local row identity preserves separate zero entries and equal
            // image numbers from the three original illustration families.
            entries[index] = new(index, row.Caption, row.Enabled, row.UnresolvedOwner ?? "");
            if (selected is null && row.Value == selectedValue && (selectedKind is null || row.Kind == selectedKind)) selected = index;
        }
        standing?.SetBusy(true);
        view.Configure(repository, text, heading, entries, selected,
            () => Available() && choice == view, result =>
            {
                if (abandoned || choice != view) return;
                choice = null; RemoveChild(view); view.QueueFree();
                if (result is int index) changed(rows[index]);
                standing?.SetBusy(false); refreshed?.Invoke(); Refresh();
            }, scrollSpeed);
        view.ZIndex = 21;
        Refresh();
    }

    private void OpenStanding()
    {
        var initial = Working;
        var draft = initial;
        int speakerSlot = initial.SpeakerId == 0 ? 0 : initial.SpeakerId == initial.FirstStandingOfficerId ? 1 :
            initial.SpeakerId == initial.SecondStandingOfficerId ? 2 : initial.SpeakerId == initial.ThirdStandingOfficerId ? 3 : 0;
        bool changed = false;
        var view = new EventEditorParameterView(); standing = view; AddChild(view);
        int Person(int slot) => slot switch { 0 => draft.FirstStandingOfficerId,
            1 => draft.SecondStandingOfficerId, _ => draft.ThirdStandingOfficerId };
        void SetPerson(int slot, int value)
        {
            draft = slot switch { 0 => draft with { FirstStandingOfficerId = value },
                1 => draft with { SecondStandingOfficerId = value }, _ => draft with { ThirdStandingOfficerId = value } };
            changed = true;
        }
        EventEditorParameterValues Read()
        {
            var rows = new EventEditorParameterRow[4];
            for (int slot = 0; slot < 3; slot++) rows[slot] = new(slot, standingCaptions[slot],
                Caption(catalog.People, Person(slot)), catalog.People.Count != 0, false, false,
                catalog.PeopleUnresolvedOwner ?? "");
            rows[3] = new(3, standingCaptions[3], speakerCaptions[speakerSlot], true,
                speakerSlot > 0, speakerSlot < 3);
            return new(rows, true, "");
        }
        void Close(bool accept)
        {
            if (abandoned || standing != view) return;
            standing = null; view.Abandon(); RemoveChild(view); view.QueueFree();
            if (accept && changed)
            {
                int speaker = speakerSlot == 0 ? 0 : Person(speakerSlot - 1);
                Working = draft with { SpeakerId = speaker };
            }
            Refresh();
        }
        view.Configure(repository, text, captions[1], 4, Read,
            () => Available() && standing == view && choice is null,
            () => Close(false), () => Close(true), slot =>
            {
                if (slot < 3)
                    Select(standingCaptions[slot], catalog.People, Person(slot), null,
                        value => SetPerson(slot, value.Value), view.Refresh);
                else
                {
                    var speakers = Enumerable.Range(0, 4)
                        .Select(index => new EditedEventContentChoice(index, speakerCaptions[index])).ToArray();
                    Select(standingCaptions[3], speakers, speakerSlot, null,
                        value => { speakerSlot = value.Value; changed = true; }, view.Refresh);
                }
            }, (slot, direction) =>
            {
                if (slot != 3) return;
                speakerSlot = Math.Clamp(speakerSlot + direction, 0, 3); changed = true; view.Refresh();
            }, alternateFour: true);
        view.ZIndex = 20;
        Refresh();
    }

    private void Accept() { if (CanEdit()) Finish(Working); }
    private void Finish(EditedEventContentValues? value) { var callback = closed; Abandon(); callback?.Invoke(value); }
    internal void RequestBack()
    {
        if (choice is { } selected) { selected.RequestBack(); return; }
        if (standing is { } people) { people.RequestBack(); return; }
        if (CanEdit()) Finish(null);
    }
    internal void SetBusy(bool value)
    { if (abandoned) return; busy = value; choice?.SetBusy(value); standing?.SetBusy(value || choice is not null); Refresh(); }
    internal void ApplyNotice(string value) { if (!abandoned) { notice = value; chrome.ApplyDefaultHint(value); } }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; admitted = null; closed = null; editMessage = null; readPreview = null; usedBytes = null;
        choice?.Abandon(); standing?.Abandon(); scene?.Abandon();
        foreach (var button in commands) button.Abandon();
        chrome?.Abandon(); Hide();
    }
    public override void _ExitTree() => Abandon();
}

internal partial class StoryPresentationView
{
    /// <summary>Keep the ordinary message artwork and font while the editor
    /// owns input. Campaign playback controls have no editor consumer.</summary>
    internal void PresentEventEditorPreview(string speaker, string paragraph)
    {
        // An empty authoring row has no message publication. Retire any
        // previous preview while keeping its background and actors visible.
        if (paragraph.Length == 0)
        {
            RetireMessage();
            return;
        }
        RestoreMessage(speaker, paragraph);
        playbackControls.Hide();
        message!.ZIndex = 0;
    }
}
