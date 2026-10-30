using Godot;
using San14.Core;
using San14.Data;
using System.Collections.Immutable;

public partial class Main
{
    private CommandSelectionView? _editedEventEnableList;
    private EventEditorHelpView? _editedEventHelp;
    private bool HasEditedEventControlPage => _editedEventEnableList is not null ||
        _editedEventHelp is not null;
    private bool CanUseEditedEventControlPages => IsEventEditorsCurrent &&
        _editedEventQuestion is null && _editedEventNotice is null;

    // Mode 5 publishes the complete checked set. An empty set is a valid
    // request to disable every saved event, rather than a cancelled selector.
    private void OpenEditedEventEnableList()
    {
        if (!IsEventEditorsCurrent || HasEditedEventPage || _repository is null ||
            _editedEventQuestion is not null || _editedEventNotice is not null) return;
        var source = GetEditedEventStore().Current;
        if (source.Events.IsEmpty) { ResumeEditedEventMenu(); return; }
        var rows = source.Events.Select(item => new CommandSelectionRow(item.Slot,
            [new(item.Name, SortUnavailable: true), new(item.Description, SortUnavailable: true)], true)).ToArray();
        var initial = source.Events.Where(item => item.Enabled).Select(item => item.Slot).ToArray();
        var view = new CommandSelectionView(); _editedEventEnableList = view; AddChild(view);
        _editedEventMenu!.Hide();
        try
        {
            view.Configure(_repository, _ui, 0x82,
                [new("name", EditedEventDataCaption(0x2EC), 500),
                 new("description", EditedEventDataCaption(0x400), 1050)],
                rows, initial, rows.Length,
                () => CanUseEditedEventControlPages && _editedEventEnableList == view,
                result => RunUiAction(() =>
                {
                    if (!IsEventEditorsCurrent || _editedEventEnableList != view) return;
                    if (result is null) { CloseEditedEventControlPages(); ResumeEditedEventMenu(); return; }
                    if (!ReferenceEquals(GetEditedEventStore().Current, source))
                    { CloseEditedEventControlPages(); ResumeEditedEventMenu(); return; }
                    var selected = result.ToHashSet();
                    // These are the source's two distinct confirmations. No
                    // changes are published until Yes and one atomic Save.
                    AskEditedEventQuestion(selected.Count == 0 ? 0x28AB : 0x28AA, () =>
                    {
                        if (!IsEventEditorsCurrent || _editedEventEnableList != view) return;
                        if (!ReferenceEquals(GetEditedEventStore().Current, source))
                        { CloseEditedEventControlPages(); ResumeEditedEventMenu(); return; }
                        var next = source with
                        {
                            Events = source.Events.Select(item => item with
                                { Enabled = selected.Contains(item.Slot) }).ToImmutableArray()
                        };
                        if (SaveEditedEventLibrary(next))
                        { CloseEditedEventControlPages(); ResumeEditedEventMenu(); }
                        else view.Show(); // Retain the exact selection after a storage failure.
                    }, () =>
                    {
                        if (IsEventEditorsCurrent && _editedEventEnableList == view) view.Show();
                    });
                }), CurrentListScrollSpeed, retainOnAccept: true, requireSelection: false);
            view.Name = "EditedEventEnableList";
            view.SetMeta("source_list_mode", 5);
            view.SetMeta("enabled_field", "FE.bit4");
        }
        catch
        {
            _editedEventEnableList = null; view.Abandon(); view.QueueFree();
            ResumeEditedEventMenu(); throw;
        }
    }

    // Mode one uses the original event dictionary category and its installed
    // messages/images. It is independent of the saved editor library.
    private void OpenEditedEventHelp()
    {
        if (!IsEventEditorsCurrent || HasEditedEventPage || _repository is null ||
            _editedEventQuestion is not null || _editedEventNotice is not null) return;
        var catalog = EventEditorHelpCatalog.Load(_repository, _ui.SourceLocale);
        var view = new EventEditorHelpView(); _editedEventHelp = view; AddChild(view);
        _editedEventMenu!.Hide();
        try
        {
            view.Configure(_repository, _ui, catalog,
                () => CanUseEditedEventControlPages && _editedEventHelp == view,
                () => RunUiAction(() =>
                {
                    if (!IsEventEditorsCurrent || _editedEventHelp != view) return;
                    CloseEditedEventControlPages(); ResumeEditedEventMenu();
                }));
            view.RegisterHeader(_sourceHeaders);
        }
        catch
        {
            _editedEventHelp = null; view.Abandon(); view.QueueFree();
            ResumeEditedEventMenu(); throw;
        }
    }

    private void RequestEditedEventControlBack()
    {
        if (!IsEventEditorsCurrent || _editedEventQuestion is not null) return;
        if (_editedEventEnableList is { } view)
        {
            if (_editedEventNotice is null) view.RequestCancel();
        }
        else _editedEventHelp?.RequestBack();
    }

    // Forced owner retirement does not confirm a selection or ask a discard
    // question. The published title library belongs only to the shared store.
    private void CloseEditedEventControlPages()
    {
        var list = _editedEventEnableList; _editedEventEnableList = null;
        var help = _editedEventHelp; _editedEventHelp = null;
        if (list is not null) { list.Abandon(); list.QueueFree(); }
        if (help is not null) { help.Abandon(); help.QueueFree(); }
    }
}
