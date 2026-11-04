using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

public partial class Main
{
    private EditedEventStore? _editedEventStore;
    private TitleEditMenuView? _editedEventParent;
    private EventEditorMenuView? _editedEventMenu;
    private CommandSelectionView? _editedEventSelector;
    private DeploymentTargetConfirmation? _editedEventQuestion;
    private DeploymentNarrationView? _editedEventNotice;
    private bool _editedEventMaking;

    private bool HasEventEditors => _editedEventMenu is not null;
    private bool IsEventEditorsCurrent => !_failed && !_shuttingDown && _titleMenu is not null &&
        _playerForceId is null && _editedEventMenu is not null && ReferenceEquals(_titleEditMenu, _editedEventParent);
    private bool CanUseEditedEventMenu => IsEventEditorsCurrent && !HasEditedEventPage &&
        _editedEventSelector is null && _editedEventQuestion is null && _editedEventNotice is null;

    // All title editing and future campaign preparation share this one archive
    // owner. Current is published by the store only after atomic replacement.
    private EditedEventStore GetEditedEventStore()
    {
        if (_editedEventStore is not null) return _editedEventStore;
        string path = ProjectSettings.GlobalizePath("user://edited-events.s14e");
        return _editedEventStore = File.Exists(path) ? EditedEventStore.OpenExisting(path) : EditedEventStore.CreateNew(path);
    }

    private string EditedEventDataCaption(int id) => (_repository ??
        throw new InvalidOperationException("The title resource owner has retired."))
        .LoadMessages(_ui.SourceLocale, MessageKind.Data).Messages.Sections[1].Records[id].Text
        ?? throw new InvalidDataException($"event_editor.data_caption.{id:X}");

    private void OpenEventEditors()
    {
        if (_repository is null || _titleMenu is null || _titleEditMenu is not { } parent || HasEventEditors ||
            _failed || _shuttingDown || _playerForceId is not null) return;
        _editedEventParent = parent; parent.SetBusy(true); parent.Hide();
        try { GetEditedEventStore(); ReplaceEditedEventMenu(makeEvent: false); }
        catch { CloseEventEditors(); throw; }
    }

    private EventEditorMenuPublication ReadEditedEventMenu()
    {
        var library = GetEditedEventStore().Current;
        bool events = library.Events.Length > 0, free = library.Events.Length < EditedEventLibrary.EventCapacity;
        EventEditorMenuAction Action(int id, bool enabled = true, string? missing = null) =>
            new(id, EditedEventDataCaption(id), enabled, missing);
        EventEditorMenuAction[] actions = _editedEventMaking
            ? [Action(0x8C, free), Action(0x8D, events), Action(0x8E, events && free),
                Action(0x8F, free), Action(0x90, events)]
            : [Action(0x80), Action(0x81), Action(0x82, events),
                Action(0x579), Action(0x68)];
        // JSON archive size is not packed program usage. Article storage has
        // its separately published source byte rule; event-bank usage is unknown.
        return new(_editedEventMaking, library.Events.Length, library.Messages.Length, null,
            EditedEventArticleStorage.UsedStorageBytes(library), actions);
    }

    private void ReplaceEditedEventMenu(bool makeEvent)
    {
        if (_repository is null) return;
        var old = _editedEventMenu; _editedEventMenu = null;
        if (old is not null) { old.Abandon(); old.QueueFree(); }
        _editedEventMaking = makeEvent;
        var view = new EventEditorMenuView(); _editedEventMenu = view; AddChild(view);
        try
        {
            view.Configure(_repository, _ui, ReadEditedEventMenu(),
                () => CanUseEditedEventMenu && _editedEventMenu == view,
                action => RunUiAction(() => SelectEditedEventMenuAction(action)),
                () => RunUiAction(() => { if (_editedEventMaking) ReplaceEditedEventMenu(false); else CloseEventEditors(); }));
            view.RegisterHeader(_sourceHeaders);
        }
        catch { _editedEventMenu = null; view.Abandon(); view.QueueFree(); throw; }
    }

    private void RefreshEditedEventMenu()
    {
        if (IsEventEditorsCurrent) _editedEventMenu!.Apply(ReadEditedEventMenu());
    }

    private void ResumeEditedEventMenu()
    {
        if (!CanUseEditedEventMenu) return;
        _editedEventMenu!.Show(); RefreshEditedEventMenu();
    }

    private void SelectEditedEventMenuAction(int action)
    {
        if (!CanUseEditedEventMenu || !ReadEditedEventMenu().Actions.Any(item => item.Id == action && item.Enabled)) return;
        if (!_editedEventMaking)
        {
            switch (action)
            {
                case 0x80: ReplaceEditedEventMenu(true); break;
                case 0x81: _editedEventMenu!.Hide(); OpenEditedEventMessages(); break;
                case 0x82: _editedEventMenu!.Hide(); OpenEditedEventEnableList(); break;
                case 0x579: _editedEventMenu!.Hide(); OpenEditedEventHelp(); break;
                case 0x68:
                    AskEditedEventQuestion(0x2932, () => SaveEditedEventLibrary(EditedEventLibrary.Empty));
                    break;
            }
            return;
        }
        if (action == 0x8C)
        {
            var draft = EditedEventDraft.Create(GetEditedEventStore().Current);
            _editedEventMenu!.Hide(); OpenEditedEventDraft(draft, creating: true);
        }
        else if (action == 0x8F) OpenEditedEventSamples();
        else if (action is 0x8D or 0x8E or 0x90) SelectEditedEvents(action);
    }

    private void SelectEditedEvents(int action)
    {
        if (!CanUseEditedEventMenu || _repository is null || action is not (0x8D or 0x8E or 0x90)) return;
        var source = GetEditedEventStore().Current;
        int maximum = action == 0x8D ? 1 : action == 0x8E
            ? Math.Min(source.Events.Length, EditedEventLibrary.EventCapacity - source.Events.Length) : source.Events.Length;
        if (maximum <= 0) return;
        var rows = source.Events.Select(item => new CommandSelectionRow(item.Slot,
            [new(item.Name, SortUnavailable: true), new(item.Description, SortUnavailable: true)], true)).ToArray();
        var view = new CommandSelectionView(); _editedEventSelector = view; AddChild(view); _editedEventMenu!.Hide();
        try
        {
            view.Configure(_repository, _ui, action,
                [new("name", EditedEventDataCaption(0x2EC), 500), new("description", EditedEventDataCaption(0x400), 1050)],
                rows, [], maximum, () => IsEventEditorsCurrent && _editedEventSelector == view &&
                    _editedEventQuestion is null && _editedEventNotice is null,
                result => RunUiAction(() =>
                {
                    if (!IsEventEditorsCurrent || _editedEventSelector != view) return;
                    _editedEventSelector = null; view.Abandon(); view.QueueFree();
                    ResumeEditedEventMenu();
                    if (result is not { Count: > 0 } || !ReferenceEquals(GetEditedEventStore().Current, source)) return;
                    if (action == 0x8D)
                    {
                        _editedEventMenu!.Hide(); OpenEditedEventDraft(EditedEventDraft.Edit(source, result[0]), creating: false);
                    }
                    else if (action == 0x8E)
                    {
                        try
                        {
                            var next = source;
                            foreach (int slot in result) next = EditedEventDraft.Copy(next, slot).Commit();
                            SaveEditedEventLibrary(next);
                        }
                        catch (Exception error) when (EditedEventStorageFailure(error)) { ReportEditedEventFailure(error); }
                    }
                    else
                    {
                        var slots = result.ToHashSet();
                        AskEditedEventQuestion(0x28E1, () =>
                        {
                            if (ReferenceEquals(GetEditedEventStore().Current, source))
                                SaveEditedEventLibrary(source.RemoveEvents(slots));
                        });
                    }
                }), CurrentListScrollSpeed, requireSelection: true, singleSelection: action == 0x8D);
            view.Name = action == 0x8D ? "EditedEventChangeSelector" : action == 0x8E ? "EditedEventCopySelector" : "EditedEventDeleteSelector";
        }
        catch { _editedEventSelector = null; view.Abandon(); view.QueueFree(); ResumeEditedEventMenu(); throw; }
    }

    private bool SaveEditedEventLibrary(EditedEventLibrary next)
    {
        try { GetEditedEventStore().Save(next); RefreshEditedEventMenu(); return true; }
        catch (Exception error) when (EditedEventStorageFailure(error)) { ReportEditedEventFailure(error); return false; }
    }
    private static bool EditedEventStorageFailure(Exception error) =>
        error is IOException or UnauthorizedAccessException or InvalidDataException;
    private void ReportEditedEventFailure(Exception error)
    {
        GD.PushWarning(error.ToString());
        ShowEditedEventNotice(_ui.Get("settings_save_failed"));
    }

    private void AskEditedEventQuestion(int message, Action confirmed, Action? declined = null)
    {
        if (!IsEventEditorsCurrent || _repository is null || _editedEventQuestion is not null || _editedEventNotice is not null) return;
        var view = new DeploymentTargetConfirmation(); _editedEventQuestion = view; AddChild(view);
        try
        {
            view.ConfigureGameMessage(_repository, _ui, message, yes => RunUiAction(() =>
            {
                if (!IsEventEditorsCurrent || _editedEventQuestion != view) return;
                _editedEventQuestion = null; view.Abandon(); view.QueueFree();
                if (yes) confirmed(); else declined?.Invoke();
            }), resources: TryBorrowApplicationUiResources(view));
            view.Name = "EditedEventConfirmation"; view.SetMeta("message_record", message);
        }
        catch { _editedEventQuestion = null; view.Abandon(); view.QueueFree(); throw; }
    }

    private void ShowEditedEventNotice(int message, Action? closed = null) =>
        ShowEditedEventNotice(RegisteredOfficerExchangeMessages.Format(_repository!, _ui.SourceLocale, message), closed, message);
    private void ShowEditedEventNotice(string message, Action? closed = null, int? messageId = null)
    {
        if (!IsEventEditorsCurrent || _repository is null || _editedEventNotice is not null) return;
        var view = new DeploymentNarrationView(); _editedEventNotice = view; AddChild(view);
        try
        {
            view.ConfigureAnnouncement(_repository, _ui, message, () => RunUiAction(() =>
            {
                if (!IsEventEditorsCurrent || _editedEventNotice != view) return;
                _editedEventNotice = null; view.Abandon(); view.QueueFree(); closed?.Invoke();
                ResumeEditedEventMenu();
            }), admitted: () => IsEventEditorsCurrent && _editedEventNotice == view,
                resources: TryBorrowApplicationUiResources(view));
            view.Name = "EditedEventNotice";
            if (messageId is int id) view.SetMeta("message_record", id);
        }
        catch { _editedEventNotice = null; view.Abandon(); view.QueueFree(); throw; }
    }

    private void RequestEventEditorsBack()
    {
        if (_editedEventQuestion is { } question) question.RequestCancel();
        else if (_editedEventNotice is not null) return;
        else if (_editedEventSelector is { } selector) selector.RequestCancel();
        else if (HasEditedEventPage) RequestEditedEventPageBack();
        else _editedEventMenu?.RequestBack();
    }

    private void CloseEventEditors(bool resumeParent = true)
    {
        // Retire callbacks first, while the title/store owner is still valid.
        var question = _editedEventQuestion; _editedEventQuestion = null;
        var notice = _editedEventNotice; _editedEventNotice = null;
        var selector = _editedEventSelector; _editedEventSelector = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        if (notice is not null) { notice.Abandon(); notice.QueueFree(); }
        if (selector is not null) { selector.Abandon(); selector.QueueFree(); }
        CloseEditedEventPages();
        var menu = _editedEventMenu; _editedEventMenu = null;
        var parent = _editedEventParent; _editedEventParent = null; _editedEventMaking = false;
        if (menu is not null) { menu.Abandon(); menu.QueueFree(); }
        if (resumeParent && !_failed && !_shuttingDown && ReferenceEquals(_titleEditMenu, parent) && parent is not null)
        { parent.SetBusy(false); parent.Show(); }
    }
}
