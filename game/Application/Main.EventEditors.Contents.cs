using System.Collections.Immutable;
using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private EventEditorContentListView? _editedEventContentsPage;
    private EventEditorContentView? _editedEventContentPage;
    private EditedEventDraft? _editedEventContentsOwner;
    private ImmutableArray<EditedEventContent> _editedEventContentsOriginal;
    private ImmutableArray<EditedEventContent> _editedEventContentsWorking;
    private int? _editedEventContentsSelection;
    private IReadOnlyList<EventEditorContentListRow> _editedEventContentsRows = [];
    private EditedEventContentCatalog? _editedEventContentCatalog;
    private EditedEventLibrary? _editedEventContentLibrary;
    private readonly Dictionary<int, LoadedStandingPortraitSources> _editedEventContentPortraits = [];
    private readonly Dictionary<int, LoadedSprite> _editedEventContentFaces = [];
    private bool HasEditedEventContents => _editedEventContentsPage is not null;
    private bool IsEditedEventContentsCurrent => IsEventEditorsCurrent && _editedEventContentsPage is not null &&
        ReferenceEquals(_editedEventDraft, _editedEventContentsOwner);
    private bool CanEditEditedEventContents => IsEditedEventContentsCurrent && _editedEventContentPage is null &&
        _editedEventQuestion is null && _editedEventNotice is null && !HasEventMessagesPage && _editedEventTest is null;

    private void OpenEditedEventContents(int? selectedIndex = null)
    {
        if (!IsEventEditorsCurrent || _repository is null || _editedEventDraft is not { } draft || HasEditedEventContents) return;
        _editedEventContentsOwner = draft;
        _editedEventContentsOriginal = _editedEventContentsWorking = draft.Contents;
        _editedEventContentsSelection = selectedIndex is int index && (uint)index < (uint)draft.Contents.Length ? index : null;
        PublishEditedEventContentsRows();
        var view = new EventEditorContentListView(); _editedEventContentsPage = view; AddChild(view);
        try
        {
            view.Configure(_repository, _ui, ReadEditedEventContentsValues,
                () => CanEditEditedEventContents && _editedEventContentsPage == view,
                () => RunUiAction(RequestEditedEventContentsBack), () => RunUiAction(AcceptEditedEventContents),
                index => RunUiAction(() =>
                {
                    if (!CanEditEditedEventContents || (uint)index >= (uint)_editedEventContentsWorking.Length) return;
                    _editedEventContentsSelection = index; view.Refresh();
                }), action => RunUiAction(() => ApplyEditedEventContentsAction(action)), CurrentListScrollSpeed);
        }
        catch { CloseEditedEventContents(); ResumeEditedEventDraft(); throw; }
    }

    private EventEditorContentListValues ReadEditedEventContentsValues()
    {
        int count = _editedEventContentsWorking.Length;
        var actions = new HashSet<EventEditorContentListAction>();
        if (count < EditedEventLibrary.ContentCapacity) actions.Add(EventEditorContentListAction.Insert);
        if (_editedEventContentsSelection is int index && (uint)index < (uint)count)
        {
            if (index > 0) actions.Add(EventEditorContentListAction.MoveUp);
            if (index + 1 < count) actions.Add(EventEditorContentListAction.MoveDown);
            actions.Add(EventEditorContentListAction.Edit); actions.Add(EventEditorContentListAction.Delete);
            if (count < EditedEventLibrary.ContentCapacity) actions.Add(EventEditorContentListAction.Copy);
        }
        actions.Add(EventEditorContentListAction.Preview);
        return new(_editedEventContentsRows, _editedEventContentsSelection, actions,
            count <= EditedEventLibrary.ContentCapacity, "");
    }

    private void PublishEditedEventContentsRows()
    {
        var catalog = ReadEditedEventContentCatalog();
        string unknown = _ui.Get("detail_unknown");
        string Person(int id) => id == 0 ? EditedEventDataCaption(0x53F)
            : _editedEventPeople.FirstOrDefault(person => person.Value == id)?.Caption ?? unknown;
        string Choice(IReadOnlyList<EditedEventContentChoice> choices, int value) =>
            choices.FirstOrDefault(row => row.Value == value)?.Caption ?? unknown;
        _editedEventContentsRows = _editedEventContentsWorking.Select((content, index) =>
        {
            EditedEventContentValues value;
            try { value = EditedEventContentEditing.Read(content); }
            catch (InvalidDataException)
            { return new EventEditorContentListRow(index, [unknown, unknown, ""],
                [unknown, unknown, unknown, unknown, unknown], [unknown, unknown, unknown]); }
            var article = GetEditedEventStore().Current.FindMessage(value.MessageSlot);
            string illustration = catalog.Illustrations.FirstOrDefault(row => row.Value == value.IllustrationId &&
                row.Kind == value.IllustrationKind)?.Caption ?? unknown;
            return new EventEditorContentListRow(index,
                [Person(value.SpeakerId), article?.Text ?? "", article is { Text.Length: > 0 }
                    ? checked(article.Slot + 1).ToString(CultureInfo.InvariantCulture) : ""],
                [Choice(catalog.Backgrounds, value.BackgroundId), Person(value.FirstStandingOfficerId),
                    Person(value.SecondStandingOfficerId), Person(value.ThirdStandingOfficerId), Person(value.SpeakerId)],
                [Choice(catalog.Music, value.MusicId), Choice(catalog.Sounds, value.SoundEffectId), illustration])
                { Portrait = ReadEditedEventContentSpeakerFace(value.SpeakerId) };
        }).ToArray();
    }

    private void ApplyEditedEventContentsAction(EventEditorContentListAction action)
    {
        if (!CanEditEditedEventContents || !ReadEditedEventContentsValues().EnabledActions.Contains(action)) return;
        int index = _editedEventContentsSelection ?? _editedEventContentsWorking.Length;
        switch (action)
        {
            case EventEditorContentListAction.Preview:
                PlayEditedEventTest(); return;
            case EventEditorContentListAction.MoveUp:
            case EventEditorContentListAction.MoveDown:
                int destination = index + (action == EventEditorContentListAction.MoveUp ? -1 : 1);
                var moving = _editedEventContentsWorking[index];
                _editedEventContentsWorking = _editedEventContentsWorking.SetItem(index, _editedEventContentsWorking[destination])
                    .SetItem(destination, moving);
                _editedEventContentsSelection = destination; break;
            case EventEditorContentListAction.Insert:
                _editedEventContentsWorking = _editedEventContentsWorking.Insert(index, EditedEventAuthoringCatalog.CreateDefaultContent());
                _editedEventContentsSelection = index;
                PublishEditedEventContentsRows(); OpenEditedEventContentRow(index); return;
            case EventEditorContentListAction.Edit:
                OpenEditedEventContentRow(index); return;
            case EventEditorContentListAction.Delete:
                _editedEventContentsWorking = _editedEventContentsWorking.RemoveAt(index);
                _editedEventContentsSelection = _editedEventContentsWorking.IsEmpty ? null : Math.Min(index, _editedEventContentsWorking.Length - 1);
                break;
            case EventEditorContentListAction.Copy:
                _editedEventContentsWorking = _editedEventContentsWorking.Insert(index + 1, _editedEventContentsWorking[index]);
                _editedEventContentsSelection = index + 1; break;
        }
        RefreshEditedEventContents();
    }

    private void RefreshEditedEventContents()
    {
        if (!IsEditedEventContentsCurrent || _editedEventContentsPage is not { } view) return;
        PublishEditedEventContentsRows(); view.Show(); view.SetBusy(false); view.Refresh();
    }

    private bool EditedEventContentsChanged()
    {
        if (_editedEventContentsWorking.Length != _editedEventContentsOriginal.Length) return true;
        for (int index = 0; index < _editedEventContentsWorking.Length; index++)
            if (!EditedEventProgramsEqual(_editedEventContentsWorking[index].Program.Instructions,
                _editedEventContentsOriginal[index].Program.Instructions)) return true;
        return false;
    }

    private void AcceptEditedEventContents()
    {
        if (!CanEditEditedEventContents || _editedEventContentsOwner is not { } owner) return;
        try { if (EditedEventContentsChanged()) owner.SetContents(_editedEventContentsWorking); }
        catch (InvalidDataException error) { ReportEditedEventFailure(error); return; }
        CloseEditedEventContents(); ResumeEditedEventDraft();
    }
    private void RequestEditedEventContentsBack()
    {
        if (_editedEventTest is { } test) { test.Host?.RequestSkip(); return; }
        if (HasEventMessagesPage) { RequestEventMessagesBack(); return; }
        if (_editedEventContentPage is { } row) { row.RequestBack(); return; }
        if (!CanEditEditedEventContents) return;
        void Discard() { CloseEditedEventContents(); ResumeEditedEventDraft(); }
        if (EditedEventContentsChanged()) AskEditedEventQuestion(0x2888, Discard); else Discard();
    }
    private void CloseEditedEventContents()
    {
        CloseEditedEventTest(restore: false);
        _editedEventContentsOwner = null;
        var content = _editedEventContentPage; _editedEventContentPage = null;
        if (content is not null) { content.Abandon(); content.QueueFree(); }
        var page = _editedEventContentsPage; _editedEventContentsPage = null;
        if (page is not null) { page.Abandon(); page.QueueFree(); }
        _editedEventContentsOriginal = _editedEventContentsWorking = default;
        _editedEventContentsRows = []; _editedEventContentsSelection = null; _editedEventContentPortraits.Clear();
        _editedEventContentFaces.Clear();
        _editedEventContentCatalog = null; _editedEventContentLibrary = null;
    }

    private EditedEventContentCatalog ReadEditedEventContentCatalog()
    {
        var library = GetEditedEventStore().Current;
        if (_editedEventContentCatalog is { } cached && ReferenceEquals(_editedEventContentLibrary, library)) return cached;
        var catalog = EditedEventContentCatalog.Load(
            _repository ?? throw new InvalidOperationException("The title resource owner has retired."), _ui.SourceLocale,
            _editedEventTitlePeopleCatalog ?? throw new InvalidOperationException("The title person catalog has retired."), library);
        _editedEventContentLibrary = library; return _editedEventContentCatalog = catalog;
    }

    private void OpenEditedEventContentRow(int index)
    {
        if (!CanEditEditedEventContents || _repository is null || (uint)index >= (uint)_editedEventContentsWorking.Length) return;
        var original = _editedEventContentsWorking[index];
        EditedEventContentValues initial;
        try { initial = EditedEventContentEditing.Read(original); }
        catch (InvalidDataException error) { ReportEditedEventFailure(error); return; }
        var catalog = ReadEditedEventContentCatalog();
        // This row borrows the current application generation. Its cached
        // source arrays must not outlive the previous row's resource lease.
        _editedEventContentPortraits.Clear();
        var view = new EventEditorContentView(); _editedEventContentPage = view; AddChild(view);
        _editedEventContentsPage!.Hide(); _editedEventContentsPage.SetBusy(true);
        try
        {
            view.Configure(_repository, _ui, catalog, initial,
                () => IsEditedEventContentsCurrent && _editedEventContentPage == view && !HasEventMessagesPage &&
                    _editedEventQuestion is null && _editedEventNotice is null,
                result => RunUiAction(() =>
                {
                    if (!IsEditedEventContentsCurrent || _editedEventContentPage != view) return;
                    try
                    {
                        if (result is not null)
                        {
                            // Creating/editing an article is its own confirmed
                            // transaction. Validate against that current directory.
                            ReadEditedEventContentCatalog().ValidateChange(initial, result);
                            _editedEventContentsWorking = _editedEventContentsWorking.SetItem(index,
                                EditedEventContentEditing.Apply(original, result));
                        }
                    }
                    catch (InvalidDataException error)
                    {
                        _editedEventContentPage = null; view.Abandon(); view.QueueFree();
                        RefreshEditedEventContents(); ReportEditedEventFailure(error); return;
                    }
                    _editedEventContentPage = null; view.Abandon(); view.QueueFree(); RefreshEditedEventContents();
                }), CurrentListScrollSpeed,
                (slot, completed) => RunUiAction(() =>
                {
                    if (!IsEditedEventContentsCurrent || _editedEventContentPage != view) return;
                    view.Hide(); view.SetBusy(true);
                    SelectEventMessage(slot, chosen =>
                    {
                        if (!IsEditedEventContentsCurrent || _editedEventContentPage != view) return;
                        view.Show(); view.SetBusy(false); completed(chosen);
                    });
                }), TryBorrowApplicationUiResources(view), ReadEditedEventContentPreview);
        }
        catch { _editedEventContentPage = null; view.Abandon(); view.QueueFree(); RefreshEditedEventContents(); throw; }
    }

    private EventEditorContentPreview ReadEditedEventContentPreview(EditedEventContentValues value)
    {
        var actors = new List<StoryActorVisual>(); string? missing = null;
        // Source preview positions the second person on the left and the
        // first in the middle; its third person occupies the right slot.
        foreach (var (slot, id) in new[] { (0, value.SecondStandingOfficerId), (1, value.FirstStandingOfficerId),
            (2, value.ThirdStandingOfficerId) })
        {
            if (id == 0) continue;
            int? appearance = GetRegisteredOfficerStore().Current.Find(id)?.PortraitId ??
                _editedEventTitlePeopleCatalog?.Rows.FirstOrDefault(row => row.Id == id)?.Original.PortraitId;
            if (appearance is null) { missing ??= "event_editor.content.preview.title_person"; continue; }
            try { actors.Add(new(slot, id, ReadEditedEventStandingPortrait(appearance.Value))); }
            catch (Exception error) when (error is InvalidDataException or IOException)
            { missing ??= "event_editor.content.preview.portrait"; }
        }
        string speaker = "";
        if (value.SpeakerId != 0)
        {
            var person = _editedEventPeople.FirstOrDefault(row => row.Value == value.SpeakerId);
            if (person is null) missing ??= "event_editor.content.preview.speaker"; else speaker = person.Caption;
        }
        string message = "";
        if (value.MessageSlot >= 0)
        {
            var article = GetEditedEventStore().Current.FindMessage(value.MessageSlot);
            if (article is null) missing ??= "event_editor.content.preview.article"; else message = article.Text;
        }
        return new(actors, speaker, message, missing);
    }

    private LoadedSprite? ReadEditedEventContentSpeakerFace(int speakerId)
    {
        if (speakerId == 0) return null;
        int appearance = GetRegisteredOfficerStore().Current.Find(speakerId)?.PortraitId ??
            _editedEventTitlePeopleCatalog?.Rows.FirstOrDefault(row => row.Id == speakerId)?.Original.PortraitId ??
            throw new InvalidDataException("event_editor.content.speaker_title_person");
        if (_editedEventContentFaces.TryGetValue(appearance, out var cached)) return cached;
        var route = _repository!.ResolveFacePortrait(appearance);
        var sprites = new LocalizedUiSpriteSource(_repository);
        var face = sprites.LoadTexturePackSprite(route.ResourceName, route.SpriteName);
        _editedEventContentFaces.Add(appearance, face); return face;
    }

    private LoadedStandingPortraitSources ReadEditedEventStandingPortrait(int portraitId)
    {
        if (_editedEventContentPortraits.TryGetValue(portraitId, out var layers)) return layers;
        var catalog = _editedEventTitlePeopleCatalog?.PortraitCatalog ??
            throw new InvalidDataException("event_editor.content.portrait_catalog_owner");
        var resolved = catalog.ResolveStanding(portraitId);
        var route = resolved.Value ?? throw new InvalidDataException(resolved.UnresolvedInput);
        var assets = (_editedEventContentPage ?? throw new InvalidOperationException("Event content preview owner is absent."))
            .SourceAssets;
        layers = route.LoadSources(assets.Portraits.RequireSource);
        _editedEventContentPortraits.Add(portraitId, layers); return layers;
    }
}
