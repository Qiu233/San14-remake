using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private EventEditorMessageMenuView? _eventMessageMenu;
    private EventEditorMessageListView? _eventMessageList;
    private EventEditorMessageView? _eventMessageEditor;
    private Action<int?>? _eventMessageSelected;
    private int? _eventMessageCurrentSlot;
    private bool HasEventMessagesPage => _eventMessageMenu is not null || _eventMessageList is not null || _eventMessageEditor is not null;
    private bool CanUseEventMessages => IsEventEditorsCurrent && _editedEventQuestion is null && _editedEventNotice is null;

    private void OpenEditedEventMessages()
    {
        if (!IsEventEditorsCurrent || _repository is null || HasEventMessagesPage) return;
        _eventMessageSelected = null; _eventMessageCurrentSlot = null;
        OpenEventMessageMenu();
    }

    // The special Contents picker shares the real article library and menu,
    // without publishing a parent event draft or mutating its message argument.
    private void SelectEventMessage(int? currentSlot, Action<int?> closed)
    {
        if (!IsEventEditorsCurrent || _repository is null || HasEventMessagesPage) return;
        _eventMessageCurrentSlot = currentSlot is int slot && GetEditedEventStore().Current.FindMessage(slot) is { Text.Length: > 0 }
            ? slot : null;
        _eventMessageSelected = closed;
        OpenEventMessageMenu();
    }

    private EventEditorMessageMenuPublication ReadEventMessageMenu()
    {
        var library = GetEditedEventStore().Current;
        int count = library.Messages.Count(message => message.Text.Length > 0);
        bool selecting = _eventMessageSelected is not null;
        bool current = _eventMessageCurrentSlot is int slot && library.FindMessage(slot) is { Text.Length: > 0 };
        return new(count, EditedEventArticleStorage.UsedStorageBytes(library), library.FirstFreeMessageSlot().HasValue,
            selecting ? current : count > 0, selecting ? current : count > 0) { CanSelect = count > 0 };
    }

    private void OpenEventMessageMenu()
    {
        if (_repository is null) return;
        var view = new EventEditorMessageMenuView(); _eventMessageMenu = view; AddChild(view);
        _editedEventMenu?.Hide();
        try
        {
            view.Configure(_repository, _ui, ReadEventMessageMenu(),
                () => CanUseEventMessages && _eventMessageMenu == view && _eventMessageList is null && _eventMessageEditor is null,
                action => RunUiAction(() => SelectEventMessageAction(view, action)),
                () => RunUiAction(() => FinishEventMessages(null)), selecting: _eventMessageSelected is not null);
        }
        catch { CloseEventMessages(); ResumeEditedEventMenu(); throw; }
    }

    private void SelectEventMessageAction(EventEditorMessageMenuView owner, EventEditorMessageAction action)
    {
        if (!CanUseEventMessages || _eventMessageMenu != owner || _eventMessageList is not null || _eventMessageEditor is not null) return;
        var menu = ReadEventMessageMenu();
        if (action == EventEditorMessageAction.Create && menu.CanCreate)
        {
            int slot = GetEditedEventStore().Current.FirstFreeMessageSlot()
                ?? throw new InvalidDataException("event_editor.article.free_slot");
            OpenEventMessageEditor(new(slot, ""), creating: true, originalText: "");
        }
        else if (action == EventEditorMessageAction.Edit && menu.CanEdit)
        {
            if (_eventMessageSelected is not null && _eventMessageCurrentSlot is int slot)
            {
                var original = GetEditedEventStore().Current.FindMessage(slot)
                    ?? throw new InvalidDataException("event_editor.article.current_slot");
                OpenEventMessageEditor(original, creating: false, originalText: original.Text);
            }
            else OpenEventMessageList(EventEditorMessageListMode.Edit);
        }
        else if (action == EventEditorMessageAction.Select && menu.CanSelect)
            OpenEventMessageList(EventEditorMessageListMode.Select);
        else if (action == EventEditorMessageAction.Delete && menu.CanDelete)
        {
            if (_eventMessageSelected is not null && _eventMessageCurrentSlot is int slot)
            {
                //554990's special Contents route deletes only its current
                //article; it does not delete the referencing event.
                if (SaveEditedEventLibrary(GetEditedEventStore().Current.RemoveMessages(new HashSet<int> { slot })))
                { _eventMessageCurrentSlot = null; owner.Apply(ReadEventMessageMenu()); }
            }
            else OpenEventMessageList(EventEditorMessageListMode.Delete);
        }
    }

    private IReadOnlyList<EditedEventMessage> EventMessageRows() => GetEditedEventStore().Current.Messages
        .Where(message => message.Text.Length > 0).OrderBy(message => message.Slot).ToArray();
    private void OpenEventMessageList(EventEditorMessageListMode mode)
    {
        if (_repository is null || _eventMessageList is not null) return;
        var view = new EventEditorMessageListView(); _eventMessageList = view;
        _eventMessageMenu?.Hide(); AddChild(view);
        try
        {
            view.Configure(_repository, _ui, EventMessageRows(), EditedEventArticleStorage.UsedStorageBytes(GetEditedEventStore().Current), mode,
                () => CanUseEventMessages && _eventMessageList == view && _eventMessageEditor is null,
                slots => RunUiAction(() =>
                {
                    if (!CanUseEventMessages || _eventMessageList != view || _eventMessageEditor is not null || slots.Count == 0) return;
                    if (mode == EventEditorMessageListMode.Select)
                    {
                        int slot = slots[0];
                        if (GetEditedEventStore().Current.FindMessage(slot) is { Text.Length: > 0 }) FinishEventMessages(slot);
                    }
                    else if (mode == EventEditorMessageListMode.Edit)
                    {
                        var original = GetEditedEventStore().Current.FindMessage(slots[0]);
                        if (original is not null) OpenEventMessageEditor(original, false, original.Text);
                    }
                    else
                    {
                        //563C20 ->4A17F0 performs deletion on the actual footer
                        //confirmation, then returns. There is no extra YesNo.
                        if (SaveEditedEventLibrary(GetEditedEventStore().Current.RemoveMessages(slots.ToHashSet())))
                        { CloseEventMessageList(); ResumeEventMessageMenu(); }
                    }
                }), () => RunUiAction(() =>
                {
                    if (!CanUseEventMessages || _eventMessageList != view) return;
                    CloseEventMessageList(); ResumeEventMessageMenu();
                }), CurrentListScrollSpeed);
        }
        catch { CloseEventMessageList(); ResumeEventMessageMenu(); throw; }
    }

    private void OpenEventMessageEditor(EditedEventMessage working, bool creating, string originalText)
    {
        if (_repository is null || _eventMessageEditor is not null) return;
        var view = new EventEditorMessageView(); _eventMessageEditor = view; AddChild(view);
        _eventMessageList?.Hide(); _eventMessageMenu?.Hide();
        try
        {
            string hint = RegisteredOfficerExchangeMessages.Format(_repository, _ui.SourceLocale, creating ? 0x28A0 : 0x28A2);
            EventEditorMessageEditState Evaluate(string raw)
            {
                var value = new EditedEventMessage(working.Slot, EditedEventArticleText.NormalizeForAuthoring(raw));
                bool changed = creating || !string.Equals(value.Text, originalText, StringComparison.Ordinal);
                return new(value, value.Text.Length > 0 && changed && !value.Text.Contains('\0') &&
                    EditedEventArticleStorage.CanStoreMessage(GetEditedEventStore().Current, value), hint);
            }
            view.Configure(_repository, _ui, working, creating, Evaluate,
                () => CanUseEventMessages && _eventMessageEditor == view,
                result => RunUiAction(() =>
                {
                    if (!CanUseEventMessages || _eventMessageEditor != view) return;
                    CloseEventMessageEditor();
                    if (result is not null)
                    {
                        var current = GetEditedEventStore().Current;
                        if (!EditedEventArticleStorage.CanStoreMessage(current, result))
                            throw new InvalidDataException("event_editor.article.storage_capacity");
                        if (!SaveEditedEventLibrary(current.SetMessage(result)))
                        { OpenEventMessageEditor(result, creating, originalText); return; }
                        _eventMessageCurrentSlot = result.Slot;
                    }
                    if (_eventMessageList is { } list)
                    {
                        list.Apply(EventMessageRows(), EditedEventArticleStorage.UsedStorageBytes(GetEditedEventStore().Current));
                        list.Show();
                    }
                    else ResumeEventMessageMenu();
                }));
        }
        catch { CloseEventMessageEditor(); ResumeEventMessageMenu(); throw; }
    }

    private void ResumeEventMessageMenu()
    {
        if (!IsEventEditorsCurrent || _eventMessageMenu is not { } menu || _eventMessageList is not null || _eventMessageEditor is not null) return;
        menu.Apply(ReadEventMessageMenu()); menu.Show();
    }
    private void FinishEventMessages(int? selected)
    {
        if (!CanUseEventMessages) return;
        var callback = _eventMessageSelected;
        CloseEventMessages();
        if (callback is not null) callback(selected); else ResumeEditedEventMenu();
    }
    private void CloseEventMessageEditor()
    {
        var old = _eventMessageEditor; _eventMessageEditor = null;
        if (old is not null) { old.Abandon(); old.QueueFree(); }
    }
    private void CloseEventMessageList()
    {
        var old = _eventMessageList; _eventMessageList = null;
        if (old is not null) { old.Abandon(); old.QueueFree(); }
    }
    private void RequestEventMessagesBack()
    {
        if (_eventMessageEditor is { } editor) editor.RequestBack();
        else if (_eventMessageList is { } list) list.RequestBack();
        else _eventMessageMenu?.RequestBack();
    }
    private void CloseEventMessages()
    {
        _eventMessageSelected = null; _eventMessageCurrentSlot = null;
        CloseEventMessageEditor(); CloseEventMessageList();
        var old = _eventMessageMenu; _eventMessageMenu = null;
        if (old is not null) { old.Abandon(); old.QueueFree(); }
    }
}
