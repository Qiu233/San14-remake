using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private EditedEventDraft? _editedEventDraft;
    private EventEditorView? _editedEventPage;
    private Control? _editedEventChild;
    private EventEditorTextView? _editedEventText;
    private EventEditorSummaryValues? _editedEventSummaryDraft;
    private EditedEventAuthoringCatalog? _editedEventAuthoringCatalog;
    private HistoricalOfficerEditorCatalog? _editedEventTitlePeopleCatalog;
    private IReadOnlyList<EditedEventParameterValue> _editedEventPeople = [];
    private EventEditorPageValues? _editedEventPageValues;
    private bool _editedEventCreating;
    private string _editedEventDraftNotice = "";

    private bool HasEditedEventDraft => _editedEventPage is not null;
    private bool HasEditedEventPage => HasEditedEventDraft || HasEventMessagesPage || HasEditedEventControlPage;
    private bool CanEditEditedEventDraft => IsEventEditorsCurrent && _editedEventPage is not null &&
        _editedEventDraft is not null && _editedEventChild is null && _editedEventText is null &&
        !HasEditedEventProgramPage && !HasEditedEventContents && !HasEventMessagesPage &&
        _editedEventQuestion is null && _editedEventNotice is null;

    private void OpenEditedEventDraft(EditedEventDraft draft, bool creating)
    {
        if (!IsEventEditorsCurrent || _repository is null || HasEditedEventDraft) return;
        _editedEventDraft = draft; _editedEventCreating = creating; _editedEventDraftNotice = "";
        var view = new EventEditorView(); _editedEventPage = view; AddChild(view); _editedEventMenu!.Hide();
        try
        {
            var registered = GetRegisteredOfficerStore().Current;
            var people = HistoricalOfficerEditorCatalog.Load(_repository, _ui.SourceLocale, registered,
                CollectionOfficerAdmission.FromProfile(GetApplicationProfile().Current), GetPersonalityEditorStore().Current);
            _editedEventTitlePeopleCatalog = people;
            // These are actual immutable title definitions and actual registered
            // records, never a campaign or synthetic Person population.
            _editedEventPeople = people.Rows.Where(row => row.Id is >= 1 and <= 1000 or >= 2001 and <= 2088)
                .Select(row => new EditedEventParameterValue(row.Id, row.Name))
                .Concat(registered.Officers.Select(row => new EditedEventParameterValue(row.Id, row.Name))).ToArray();
            var personSexes = people.Rows.Where(row => row.Id is >= 1 and <= 1000 or >= 2001 and <= 2088)
                .Select(row => new KeyValuePair<int, byte>(row.Id,row.Sex))
                .Concat(registered.Officers.Select(row => new KeyValuePair<int, byte>(row.Id,row.Sex)))
                .ToDictionary(row => row.Key,row => row.Value);
            _editedEventAuthoringCatalog = EditedEventAuthoringCatalog.Load(_repository, _ui.SourceLocale)
                .WithTitleContext(_editedEventPeople,personSexes);
            RebuildEditedEventDraftValues();
            view.Configure(_repository, _ui, () => _editedEventPageValues!,
                () => CanEditEditedEventDraft && _editedEventPage == view,
                () => RunUiAction(RequestEditedEventDraftBack), () => RunUiAction(SaveEditedEventDraft),
                section => RunUiAction(() => OpenEditedEventSection(section)),
                (tab, row) => RunUiAction(() => OpenEditedEventRow(tab, row)), CurrentListScrollSpeed);
            view.SetMeta("key_people_source_context", "immutable_title_person_directory");
            view.SetMeta("key_people_unresolved_candidates", people.UnresolvedCandidates.Count);
        }
        catch { CloseEditedEventDraft(); throw; }
    }

    private EditedEventAuthoringCatalog ReadEditedEventAuthoringCatalog()
    {
        var draft = _editedEventDraft ?? throw new InvalidOperationException("The event draft has retired.");
        var catalog = _editedEventAuthoringCatalog ?? throw new InvalidOperationException("The event authoring catalog has retired.");
        return catalog.WithKeyPeople(draft.ReadKeyPeople().Select(id =>
            _editedEventPeople.FirstOrDefault(row => row.Value == id) ??
            throw new InvalidDataException("event_editor.key_people_inventory")).ToArray());
    }

    private void RebuildEditedEventDraftValues()
    {
        if (_editedEventDraft is not { } draft) return;
        var catalog = ReadEditedEventAuthoringCatalog();
        EventEditorSectionRows Rows(EditedEventProgram program) => new(program.Instructions
            .Select((item, index) => new EventEditorChoiceRow(index, catalog.DescribeInstruction(item))).ToArray());
        var people = draft.ReadKeyPeople().Select(id => new EventEditorChoiceRow(id,
            _editedEventPeople.FirstOrDefault(row => row.Value == id)?.Caption ?? _ui.Get("detail_unknown"))).ToArray();
        var contents = draft.Contents.Select((content, index) => new EventEditorChoiceRow(index,
            string.Join("  ", content.Program.Instructions.Select(catalog.DescribeInstruction)))).ToArray();
        _editedEventPageValues = new(draft.Name, draft.Description, draft.Category, draft.Automatic, draft.Repeat,
            [Rows(draft.ReadEditableConditions()), new(people), new(contents), Rows(draft.Results)],
            draft.CanCommit && (_editedEventCreating || draft.IsDirty), _editedEventDraftNotice);
    }

    private void ResumeEditedEventDraft()
    {
        if (!IsEventEditorsCurrent || _editedEventPage is not { } page || _editedEventDraft is null) return;
        RebuildEditedEventDraftValues(); page.Show(); page.SetBusy(false); page.Refresh();
    }

    private void OpenEditedEventSection(int section)
    {
        if (!CanEditEditedEventDraft || section is < 0 or > 4) return;
        if (section == 0) OpenEditedEventSummary();
        else if (section == 2) OpenEditedEventKeyPeople();
        else
        {
            _editedEventPage!.Hide(); _editedEventPage.SetBusy(true);
            if (section == 3) OpenEditedEventContents(); else OpenEditedEventProgram(results: section == 4);
        }
    }
    private void OpenEditedEventRow(int tab, int row)
    {
        if (!CanEditEditedEventDraft || tab is < 0 or > 3) return;
        if (tab == 1) OpenEditedEventKeyPeople();
        else
        {
            _editedEventPage!.Hide(); _editedEventPage.SetBusy(true);
            if (tab == 2) OpenEditedEventContents(row); else OpenEditedEventProgram(results: tab == 3, selectedIndex: row);
        }
    }

    private void SaveEditedEventDraft()
    {
        if (!CanEditEditedEventDraft || _editedEventDraft is not { CanCommit: true } draft ||
            !_editedEventCreating && !draft.IsDirty) return;
        try
        {
            // Articles may have been created in a content subpage after this
            // draft opened. Adopt into the actual current library, not SourceLibrary.
            if (SaveEditedEventLibrary(GetEditedEventStore().Current.SetEvent(draft.Build()))) CloseEditedEventDraft();
        }
        catch (Exception error) when (EditedEventStorageFailure(error)) { ReportEditedEventFailure(error); }
    }

    private void OpenEditedEventSummary()
    {
        if (!CanEditEditedEventDraft || _repository is null || _editedEventDraft is not { } draft) return;
        _editedEventSummaryDraft = new(draft.Name, draft.Description, draft.Category, draft.Automatic, draft.Repeat, false, "")
            { EventId = draft.Slot };
        var view = new EventEditorSummaryView(); _editedEventChild = view; AddChild(view);
        _editedEventPage!.SetBusy(true); _editedEventPage.Hide();
        bool Admitted() => IsEventEditorsCurrent && _editedEventChild == view && _editedEventText is null &&
            _editedEventQuestion is null && _editedEventNotice is null;
        void Finish(bool accepted)
        {
            if (!IsEventEditorsCurrent || _editedEventChild != view) return;
            if (accepted && _editedEventSummaryDraft is { } value)
                draft.SetSummary(value.Name, value.Description, value.Category, value.Automatic, value.Repeat);
            _editedEventSummaryDraft = null; _editedEventChild = null; view.Abandon(); view.QueueFree(); ResumeEditedEventDraft();
        }
        try
        {
            view.Configure(_repository, _ui, () =>
            {
                var value = _editedEventSummaryDraft!;
                return value with { CanConfirm = EditedEventSummaryText.EvaluateName(value.Name).CanConfirm &&
                    EditedEventSummaryText.EvaluateDescription(value.Description).CanConfirm && value.Category is < 5 };
            }, Admitted, () => RunUiAction(() => Finish(false)), () => RunUiAction(() => Finish(true)),
                () => RunUiAction(() => OpenEditedEventSummaryText(false)),
                () => RunUiAction(() => OpenEditedEventSummaryText(true)),
                category => _editedEventSummaryDraft = _editedEventSummaryDraft! with { Category = category },
                automatic => _editedEventSummaryDraft = _editedEventSummaryDraft! with { Automatic = automatic },
                repeat => _editedEventSummaryDraft = _editedEventSummaryDraft! with { Repeat = repeat });
            view.SetMeta("summary_text_capacity_unresolved", "event_editor.summary.narrow_input_capacity");
        }
        catch { Finish(false); throw; }
    }

    private void OpenEditedEventSummaryText(bool description)
    {
        if (!IsEventEditorsCurrent || _repository is null || _editedEventChild is not EventEditorSummaryView summary ||
            _editedEventSummaryDraft is not { } original || _editedEventText is not null) return;
        var view = new EventEditorTextView(); _editedEventText = view; AddChild(view); summary.SetBusy(true);
        try
        {
            view.Configure(_repository, _ui, description, description ? original.Description : original.Name,
                value => (description ? EditedEventSummaryText.EvaluateDescription(value) : EditedEventSummaryText.EvaluateName(value)).CanConfirm,
                () => IsEventEditorsCurrent && _editedEventText == view && _editedEventChild == summary &&
                    _editedEventQuestion is null && _editedEventNotice is null,
                result => RunUiAction(() =>
                {
                    if (!IsEventEditorsCurrent || _editedEventText != view || _editedEventChild != summary) return;
                    _editedEventText = null; view.Abandon(); view.QueueFree();
                    if (result is not null) _editedEventSummaryDraft = description
                        ? _editedEventSummaryDraft! with { Description = result } : _editedEventSummaryDraft! with { Name = result };
                    summary.SetBusy(false); summary.Refresh();
                }));
        }
        catch { _editedEventText = null; view.Abandon(); view.QueueFree(); summary.SetBusy(false); throw; }
    }

    private void OpenEditedEventKeyPeople()
    {
        if (!CanEditEditedEventDraft || _repository is null || _editedEventDraft is not { } draft) return;
        var view = new CommandSelectionView(); _editedEventChild = view; AddChild(view);
        _editedEventPage!.SetBusy(true); _editedEventPage.Hide();
        try
        {
            var rows = _editedEventPeople.Select(row => new CommandSelectionRow(row.Value,
                [new(row.Caption, SortUnavailable: true)], true)).ToArray();
            view.Configure(_repository, _ui, 0x2F8, [new("person", EditedEventDataCaption(0x211), 1500)],
                rows, draft.ReadKeyPeople(), 20,
                () => IsEventEditorsCurrent && _editedEventChild == view && _editedEventQuestion is null && _editedEventNotice is null,
                result => RunUiAction(() =>
                {
                    if (!IsEventEditorsCurrent || _editedEventChild != view) return;
                    try { if (result is not null) draft.SetKeyPeople(result); }
                    catch (InvalidDataException error) { ReportEditedEventFailure(error); }
                    _editedEventChild = null; view.Abandon(); view.QueueFree(); ResumeEditedEventDraft();
                }), CurrentListScrollSpeed);
            view.Name = "EditedEventKeyPeople";
        }
        catch { _editedEventChild = null; view.Abandon(); view.QueueFree(); ResumeEditedEventDraft(); throw; }
    }

    private void RequestEditedEventDraftBack()
    {
        if (!CanEditEditedEventDraft) return;
        if (_editedEventDraft?.IsDirty == true) AskEditedEventQuestion(0x2888, CloseEditedEventDraft);
        else CloseEditedEventDraft();
    }
    private void RequestEditedEventPageBack()
    {
        if (HasEventMessagesPage) RequestEventMessagesBack();
        else if (HasEditedEventControlPage) RequestEditedEventControlBack();
        else if (HasEditedEventProgramPage) RequestEditedEventProgramBack();
        else if (_editedEventText is { } text) text.RequestBack();
        else if (_editedEventChild is EventEditorSummaryView summary) summary.RequestBack();
        else if (_editedEventChild is CommandSelectionView people) people.RequestCancel();
        else if (HasEditedEventContents) RequestEditedEventContentsBack();
        else RequestEditedEventDraftBack();
    }
    private void CloseEditedEventPages()
    {
        CloseEventMessages(); CloseEditedEventControlPages(); RetireEditedEventDraft(resumeMenu: false);
    }
    private void CloseEditedEventDraft() => RetireEditedEventDraft(resumeMenu: true);
    private void RetireEditedEventDraft(bool resumeMenu)
    {
        CloseEventMessages(); CloseEditedEventProgram(); CloseEditedEventContents();
        var text = _editedEventText; _editedEventText = null;
        if (text is not null) { text.Abandon(); text.QueueFree(); }
        var child = _editedEventChild; _editedEventChild = null;
        if (child is EventEditorSummaryView summary) summary.Abandon();
        else if (child is CommandSelectionView people) people.Abandon();
        if (child is not null) child.QueueFree();
        var page = _editedEventPage; _editedEventPage = null; _editedEventDraft = null;
        _editedEventSummaryDraft = null; _editedEventAuthoringCatalog = null; _editedEventTitlePeopleCatalog = null;
        _editedEventPeople = [];
        _editedEventPageValues = null; _editedEventCreating = false; _editedEventDraftNotice = "";
        if (page is not null) { page.Abandon(); page.QueueFree(); }
        if (resumeMenu) ResumeEditedEventMenu();
    }
}
