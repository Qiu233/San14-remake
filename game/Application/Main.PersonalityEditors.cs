using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private PersonalityEditorStore? _personalityEditorStore;
    private PersonalityEditorCatalog? _personalityEditorCatalog;
    private TitleEditMenuView? _personalityEditorsParent;
    private PersonalityEditorMenuView? _personalityEditorMenu;
    private PersonalityEditorView? _personalityEditorPage;
    private PersonalityEditorDraft? _personalityEditorDraft;
    private CommandSelectionView? _personalityEditorSelector, _personalityEditorReferences;
    private Control? _personalityEditorChild;
    private DeploymentTargetConfirmation? _personalityEditorQuestion;
    private DeploymentNarrationView? _personalityEditorNotice;
    private bool _personalityEditorCreating;
    private int _personalityExchangeCount;
    private string _personalityEditorPageNotice = "";

    private bool HasPersonalityEditors => _personalityEditorMenu is not null;
    private bool IsPersonalityEditorsCurrent => !_failed && !_shuttingDown && _titleMenu is not null &&
        _playerForceId is null && _personalityEditorMenu is not null && ReferenceEquals(_titleEditMenu, _personalityEditorsParent);
    private bool CanEditPersonalityPage => IsPersonalityEditorsCurrent && _personalityEditorPage is not null &&
        _personalityEditorChild is null && _personalityEditorSelector is null && _personalityEditorQuestion is null &&
        _personalityEditorNotice is null;

    private PersonalityEditorStore GetPersonalityEditorStore()
    {
        if (_personalityEditorStore is not null) return _personalityEditorStore;
        string path = ProjectSettings.GlobalizePath("user://personality-editors.s14r");
        return _personalityEditorStore = File.Exists(path) ? PersonalityEditorStore.OpenExisting(path) : PersonalityEditorStore.CreateNew(path);
    }
    private PersonalityEditorCatalog GetPersonalityEditorCatalog()
    {
        if (_repository is null) throw new InvalidOperationException("The title resource owner has retired.");
        if (_personalityEditorCatalog?.Locale == _ui.SourceLocale) return _personalityEditorCatalog;
        return _personalityEditorCatalog = PersonalityEditorCatalog.Load(_repository, _ui.SourceLocale);
    }
    private void RefreshPersonalityExchangeCount() => _personalityExchangeCount =
        PersonalityEditorExchange.CountSignatureFiles(ProjectSettings.GlobalizePath("user://PERSONALITYDATA"));

    private void OpenPersonalityEditors()
    {
        if (_repository is null || _titleMenu is null || _titleEditMenu is not { } parent || HasPersonalityEditors ||
            _failed || _shuttingDown || _playerForceId is not null) return;
        var menu = new PersonalityEditorMenuView(); _personalityEditorMenu = menu; _personalityEditorsParent = parent;
        AddChild(menu); parent.SetBusy(true); parent.Hide();
        try
        {
            GetPersonalityEditorStore(); GetPersonalityEditorCatalog(); RefreshPersonalityExchangeCount();
            menu.Configure(_repository, _ui, () => GetPersonalityEditorStore().Current.Created.Length,
                PersonalityEditorMenuEnabled, () => IsPersonalityEditorsCurrent && _personalityEditorMenu == menu &&
                    _personalityEditorPage is null && _personalityEditorSelector is null &&
                    _personalityEditorQuestion is null && _personalityEditorNotice is null,
                () => RunUiAction(() => ClosePersonalityEditors()), action => RunUiAction(() => SelectPersonalityAction(action)));
        }
        catch { ClosePersonalityEditors(); throw; }
    }
    private bool PersonalityEditorMenuEnabled(int action)
    {
        if (_personalityEditorStore is not { } store) return false;
        int count = store.Current.Created.Length;
        return action switch
        {
            0 => count < 100,
            1 or 2 => count > 0,
            3 => count > 0 && _personalityExchangeCount < PersonalityEditorExchange.MaximumDirectoryRecords,
            4 => count < 100 && _personalityExchangeCount > 0,
            _ => false
        };
    }
    private void OpenPersonalityEditor(CreatedPersonality? record)
    {
        if (!IsPersonalityEditorsCurrent || _repository is null || _personalityEditorPage is not null) return;
        var catalog = GetPersonalityEditorCatalog();
        _personalityEditorCreating = record is null;
        _personalityEditorDraft = record is null ? catalog.CreateDraft(GetPersonalityEditorStore().Current.FirstFreeId ??
            throw new InvalidDataException("The personality library is full.")) : catalog.Edit(record);
        _personalityEditorPageNotice = "";
        var page = new PersonalityEditorView(); _personalityEditorPage = page; AddChild(page); _personalityEditorMenu!.Hide();
        try
        {
            page.Configure(_repository, _ui, ReadPersonalityEditorValues,
                () => CanEditPersonalityPage && _personalityEditorPage == page,
                () => RunUiAction(RequestPersonalityPageBack), () => RunUiAction(SavePersonalityEditor),
                field => RunUiAction(() => OpenPersonalityField(field)), _personalityEditorCreating ? 0x7F7 : 0x7F5);
        }
        catch { ClosePersonalityEditorPage(); throw; }
    }
    private PersonalityEditorPageValues ReadPersonalityEditorValues()
    {
        var draft = _personalityEditorDraft ?? throw new InvalidOperationException("The personality draft has retired.");
        var catalog = GetPersonalityEditorCatalog(); var value = draft.Value;
        bool Variable(int code) => PersonalityEditorOptions.ReadEffectRange(value.ConditionKind, code) is { } range &&
            range.Minimum != range.Maximum;
        return new(value.Name, catalog.ConditionName(value.ConditionKind), catalog.OptionName(value.ConditionKind, value.OptionKind),
            catalog.EffectName(value.Effect1Code), catalog.EffectName(value.Effect2Code),
            catalog.Conditions.Any(row => row.Code == value.ConditionKind && row.Minimum != row.Maximum) ? catalog.ConditionAmount(value) : "",
            Variable(value.Effect1Code) ? catalog.EffectAmount(value.Effect1Code, value.Effect1Value) : "",
            Variable(value.Effect2Code) ? catalog.EffectAmount(value.Effect2Code, value.Effect2Value) : "",
            draft.ConditionOptionEnabled, draft.Effect2Enabled,
            draft.CanConfirm && (_personalityEditorCreating || draft.IsDirty), _personalityEditorPageNotice);
    }
    private void SavePersonalityEditor()
    {
        if (!CanEditPersonalityPage || _personalityEditorDraft is not { CanConfirm: true } draft ||
            !_personalityEditorCreating && !draft.IsDirty) return;
        if (SavePersonalityLibrary(GetPersonalityEditorStore().Current.Set(draft.ToRecord()))) ClosePersonalityEditorPage();
    }
    private bool SavePersonalityLibrary(PersonalityEditorLibrary next)
    {
        try
        {
            GetPersonalityEditorStore().Save(next);
            // A subsequent title editor must rebuild its choices from the newly
            // published library. Existing worlds keep their own immutable snapshot.
            _registeredOfficerCatalog = null;
            _historicalOfficerCatalog = null;
            _personalityEditorMenu?.Refresh(); return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        { PersonalityEditorStorageFailure(error); return false; }
    }
    private void OpenPersonalityField(int field)
    {
        if (!CanEditPersonalityPage || _repository is null || _personalityEditorDraft is not { } draft || field is < 0 or > 4 ||
            field == 2 && !draft.ConditionOptionEnabled || field == 4 && !draft.Effect2Enabled) return;
        var page = _personalityEditorPage!; var catalog = GetPersonalityEditorCatalog();
        page.SetBusy(true); page.Hide();
        bool Admitted() => IsPersonalityEditorsCurrent && _personalityEditorPage == page &&
            _personalityEditorQuestion is null && _personalityEditorReferences is null;
        void Finish(Control child, Action? apply)
        {
            if (!IsPersonalityEditorsCurrent || _personalityEditorPage != page || _personalityEditorChild != child) return;
            _personalityEditorChild = null; RetirePersonalityControl(child);
            try { apply?.Invoke(); _personalityEditorPageNotice = ""; }
            catch (InvalidDataException error) { PersonalityEditorStorageFailure(error); }
            page.Show(); page.SetBusy(false); page.Refresh();
        }
        if (field == 0)
        {
            var child = new PersonalityEditorTextView(); _personalityEditorChild = child; AddChild(child);
            try { child.Configure(_repository, _ui, draft.Value.Name, Admitted,
                result => RunUiAction(() => Finish(child, result is null ? null : () => draft.SetName(result)))); }
            catch { Finish(child, null); throw; }
        }
        else
        {
            var choices = field == 1 ? catalog.Conditions : field == 2 ? catalog.ReadOptions(draft) : catalog.ReadEffects(draft, field - 2);
            int code = field switch { 1 => draft.Value.ConditionKind, 2 => draft.Value.OptionKind, 3 => draft.Value.Effect1Code, _ => draft.Value.Effect2Code };
            int amount = field switch { 1 => unchecked((int)draft.Value.ConditionValue), 3 => draft.Value.Effect1Value, 4 => draft.Value.Effect2Value, _ => 0 };
            if (field is 3 or 4 && code == 98) amount = -amount;
            string caption = field switch { 1 => catalog.ConditionName(code), 2 => catalog.OptionName(draft.Value.ConditionKind, code), _ => catalog.EffectName(code) };
            PersonalityEditorOptionRow? initialRow = field is 3 or 4 &&
                PersonalityEditorOptions.ReadEffectRange(draft.Value.ConditionKind, code) is { } range
                ? new(code, caption, range.Minimum, range.Maximum, range.Step) : null;
            var child = new PersonalityEditorFieldView(); _personalityEditorChild = child; AddChild(child);
            try
            {
                child.Configure(_repository, _ui, field, choices.Select(row => new PersonalityEditorOptionRow(row.Code, row.Caption,
                    row.Minimum, row.Maximum, row.Step)).ToArray(), code, amount, Admitted,
                    result => RunUiAction(() => Finish(child, result is null ? null : () =>
                    {
                        if (field == 1) draft.SetCondition(result.Code, result.Value);
                        else if (field == 2) draft.SetOption(result.Code);
                        else draft.SetEffect(field - 2, result.Code, result.Value);
                    })), CurrentListScrollSpeed, selectedCode => RunUiAction(() => OpenPersonalityReferences(child, field, selectedCode)),
                    initialRow: initialRow, initialCaption: caption);
            }
            catch { Finish(child, null); throw; }
        }
    }
    private void OpenPersonalityReferences(PersonalityEditorFieldView parent, int field, int code)
    {
        if (!IsPersonalityEditorsCurrent || _repository is null || _personalityEditorChild != parent ||
            _personalityEditorDraft is not { } draft || _personalityEditorReferences is not null || field is not (1 or 3 or 4)) return;
        var catalog = GetPersonalityEditorCatalog();
        var rows = catalog.ReadReferences(draft, field, code).Select(row => new CommandSelectionRow(row.Id,
            [new(row.Name, SortUnavailable: true), new(row.Description, SortUnavailable: true)], true)).ToArray();
        var view = new CommandSelectionView(); _personalityEditorReferences = view; AddChild(view); parent.SetBusy(true); parent.Hide();
        void Finish()
        {
            if (!IsPersonalityEditorsCurrent || _personalityEditorReferences != view || _personalityEditorChild != parent) return;
            _personalityEditorReferences = null; view.Abandon(); view.QueueFree(); parent.Show(); parent.SetBusy(false);
        }
        try
        {
            view.Configure(_repository, _ui, 0x431,
                [new("name", catalog.Data(0x5A8), 360), new("description", catalog.Data(0x400), 1150)], rows, [], 0,
                () => IsPersonalityEditorsCurrent && _personalityEditorReferences == view,
                _ => RunUiAction(Finish), CurrentListScrollSpeed, PersonalityMessage(0x292C), returnOnly: true);
            view.Name = "PersonalityReadOnlyReferences";
        }
        catch { Finish(); throw; }
    }
    private void RequestPersonalityPageBack()
    {
        if (!CanEditPersonalityPage) return;
        if (_personalityEditorDraft?.IsDirty == true) AskPersonalityQuestion(0x292B, ClosePersonalityEditorPage);
        else ClosePersonalityEditorPage();
    }
    private string PersonalityMessage(int id, int? count = null, string? file = null) =>
        RegisteredOfficerExchangeMessages.Format(_repository!, _ui.SourceLocale, id, count,
            file ?? (id is 0x2923 or 0x2926 ? GetPersonalityEditorCatalog().Data(0x23D) : null));
    private void AskPersonalityQuestion(int id, Action yes)
    {
        if (!IsPersonalityEditorsCurrent || _repository is null || _personalityEditorQuestion is not null) return;
        var view = new DeploymentTargetConfirmation(); _personalityEditorQuestion = view; AddChild(view);
        try
        {
            view.ConfigureWrappedMessage(_repository, _ui, PersonalityMessage(id), confirmed => RunUiAction(() =>
            {
                if (!IsPersonalityEditorsCurrent || _personalityEditorQuestion != view) return;
                _personalityEditorQuestion = null; view.Abandon(); view.QueueFree(); if (confirmed) yes();
            }), resources: TryBorrowApplicationUiResources(view));
            view.SetMeta("message_record", id);
        }
        catch { _personalityEditorQuestion = null; view.Abandon(); view.QueueFree(); throw; }
    }
    private void ShowPersonalityNotice(int id, int? count = null, string? file = null, Action? closed = null)
    {
        if (!IsPersonalityEditorsCurrent || _repository is null || _personalityEditorNotice is not null) return;
        var view = new DeploymentNarrationView(); _personalityEditorNotice = view; AddChild(view); _personalityEditorMenu!.SetBusy(true);
        try
        {
            view.ConfigureAnnouncement(_repository, _ui, PersonalityMessage(id, count, file), () => RunUiAction(() =>
            {
                if (!IsPersonalityEditorsCurrent || _personalityEditorNotice != view) return;
                _personalityEditorNotice = null; view.Abandon(); view.QueueFree();
                _personalityEditorMenu!.SetBusy(false); _personalityEditorMenu.Refresh(); closed?.Invoke();
            }), admitted: () => IsPersonalityEditorsCurrent && _personalityEditorNotice == view,
                resources: TryBorrowApplicationUiResources(view));
            view.SetMeta("message_record", id);
        }
        catch { _personalityEditorNotice = null; view.Abandon(); view.QueueFree(); _personalityEditorMenu!.SetBusy(false); throw; }
    }
    private void PersonalityEditorStorageFailure(Exception error)
    {
        _personalityEditorPageNotice = _ui.Get("settings_save_failed");
        _personalityEditorPage?.ApplyNotice(_personalityEditorPageNotice);
        _personalityEditorMenu?.ApplyNotice(_personalityEditorPageNotice); GD.PushWarning(error.ToString());
    }
    private void RequestPersonalityEditorsBack()
    {
        if (_personalityEditorQuestion is { } question) question.RequestCancel();
        else if (_personalityEditorNotice is not null) return;
        else if (_personalityEditorReferences is { } references) references.RequestCancel();
        else if (_personalityEditorSelector is { } selector) selector.RequestCancel();
        else if (_personalityEditorChild is PersonalityEditorTextView name) name.RequestBack();
        else if (_personalityEditorChild is PersonalityEditorFieldView field) field.RequestBack();
        else if (_personalityEditorPage is not null) RequestPersonalityPageBack();
        else _personalityEditorMenu?.RequestBack();
    }
    private void ClosePersonalityEditorPage()
    {
        var references = _personalityEditorReferences; _personalityEditorReferences = null;
        if (references is not null) { references.Abandon(); references.QueueFree(); }
        var child = _personalityEditorChild; _personalityEditorChild = null; if (child is not null) RetirePersonalityControl(child);
        var page = _personalityEditorPage; _personalityEditorPage = null; _personalityEditorDraft = null;
        if (page is not null) { page.Abandon(); page.QueueFree(); }
        if (_personalityEditorMenu is { } menu) { menu.Show(); menu.SetBusy(false); menu.Refresh(); }
    }
    private void ClosePersonalityEditors(bool resumeParent = true)
    {
        var question = _personalityEditorQuestion; _personalityEditorQuestion = null; if (question is not null) { question.Abandon(); question.QueueFree(); }
        var notice = _personalityEditorNotice; _personalityEditorNotice = null; if (notice is not null) { notice.Abandon(); notice.QueueFree(); }
        var selector = _personalityEditorSelector; _personalityEditorSelector = null; if (selector is not null) { selector.Abandon(); selector.QueueFree(); }
        ClosePersonalityEditorPage(); var menu = _personalityEditorMenu; _personalityEditorMenu = null; _personalityEditorsParent = null;
        if (menu is not null) { menu.Abandon(); menu.QueueFree(); }
        _personalityEditorCatalog = null;
        if (resumeParent && !_failed && !_shuttingDown && _titleEditMenu is { } parent) { parent.SetBusy(false); parent.Show(); }
    }
    private static void RetirePersonalityControl(Control child)
    {
        if (child is PersonalityEditorTextView name) name.Abandon();
        else if (child is PersonalityEditorFieldView field) field.Abandon();
        child.QueueFree();
    }
}
