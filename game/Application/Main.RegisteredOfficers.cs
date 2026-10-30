using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private RegisteredOfficerStore? _registeredOfficerStore;
    private RegisteredOfficerLibraryMenuView? _registeredOfficers;
    private TitleEditMenuView? _registeredOfficersParent;
    private RegisteredOfficerListView? _registeredOfficerList;
    private RegisteredOfficerEditorView? _registeredOfficerEditor;
    private RegisteredOfficerFieldEditorView? _registeredOfficerField;
    private DeploymentTargetConfirmation? _registeredOfficerQuestion;
    private RegisteredOfficerEditorCatalog? _registeredOfficerCatalog;
    private RegisteredOfficerLibrary? _registeredOfficerWorkingLibrary;
    private LoadedScenario? _registeredOfficerDefinitions;
    private RegisteredOfficer? _registeredOfficerOriginal, _registeredOfficerDraft;
    private readonly Dictionary<int, IReadOnlyList<LoadedSprite>> _registeredOfficerPortraits = [];
    private string _registeredOfficerNotice = "";

    // Shared with new-game selection. No other entry point opens or caches a
    // second library; Save publishes Current only after atomic replacement.
    private RegisteredOfficerStore GetRegisteredOfficerStore()
    {
        if (_registeredOfficerStore is not null) return _registeredOfficerStore;
        string path = ProjectSettings.GlobalizePath("user://registered-officers.s14r");
        return _registeredOfficerStore = File.Exists(path) ? RegisteredOfficerStore.OpenExisting(path)
            : RegisteredOfficerStore.CreateNew(path);
    }

    private bool HasRegisteredOfficers => _registeredOfficers is not null;
    private bool IsRegisteredOfficersCurrent => !_failed && !_shuttingDown && _titleMenu is not null &&
        _playerForceId is null && _registeredOfficers is not null &&
        ReferenceEquals(_titleEditMenu, _registeredOfficersParent);
    private bool CanEditRegisteredOfficer => IsRegisteredOfficersCurrent && _registeredOfficerEditor is not null &&
        _registeredOfficerDraft is not null && _registeredOfficerField is null && _registeredOfficerQuestion is null;

    private void OpenRegisteredOfficers()
    {
        if (_failed || _shuttingDown || _repository is null || _titleMenu is null ||
            _titleEditMenu is not { } parent || HasRegisteredOfficers || _playerForceId is not null) return;
        var menu = new RegisteredOfficerLibraryMenuView();
        _registeredOfficers = menu; _registeredOfficersParent = parent;
        AddChild(menu); parent.SetBusy(true); parent.Hide();
        try
        {
            menu.Configure(_repository, _ui,
                () => IsRegisteredOfficersCurrent && _registeredOfficers == menu &&
                    _registeredOfficerEditor is null && _registeredOfficerList is null && _registeredOfficerQuestion is null &&
                    !HasRegisteredOfficerBirthYears && !HasRegisteredOfficerExchange,
                () => RunUiAction(() => CloseRegisteredOfficers()),
                () => RunUiAction(CreateRegisteredOfficer),
                () => RunUiAction(() => SelectRegisteredOfficer(deleting: false)),
                () => RunUiAction(() => SelectRegisteredOfficer(deleting: true)),
                () => _registeredOfficerStore?.Current.FirstFreeId is not null,
                () => (_registeredOfficerStore?.Current.Officers.Length ?? 0) > 0,
                () => RunUiAction(OpenRegisteredOfficerBirthYears),
                () => RunUiAction(() => OpenRegisteredOfficerExchange(importing: false)),
                () => RunUiAction(() => OpenRegisteredOfficerExchange(importing: true)));
            try { GetRegisteredOfficerStore(); menu.Refresh(); }
            catch (Exception error) when (RegisteredOfficerStorageFailure(error))
            { RegisteredOfficerSaveFailure(error); }
        }
        catch { CloseRegisteredOfficers(); throw; }
    }

    private void CreateRegisteredOfficer()
    {
        if (!IsRegisteredOfficersCurrent || _registeredOfficerEditor is not null || _registeredOfficerList is not null ||
            _registeredOfficerQuestion is not null || _registeredOfficerStore?.Current.FirstFreeId is not int id) return;
        OpenRegisteredOfficerEditor(RegisteredOfficerEditing.CreateDraft(id));
    }

    private void SelectRegisteredOfficer(bool deleting)
    {
        if (!IsRegisteredOfficersCurrent || _repository is null || _registeredOfficerStore is not { } store ||
            _registeredOfficerList is not null || _registeredOfficerEditor is not null || _registeredOfficerQuestion is not null ||
            store.Current.Officers.Length == 0) return;
        var menu = _registeredOfficers!;
        var view = new RegisteredOfficerListView(); _registeredOfficerList = view;
        AddChild(view); menu.SetBusy(true); menu.Hide();
        try
        {
            view.Configure(_repository, _ui, store.Current.Officers,
                () => IsRegisteredOfficersCurrent && _registeredOfficerList == view && _registeredOfficerQuestion is null,
                selected => RunUiAction(() =>
                {
                    if (!IsRegisteredOfficersCurrent || _registeredOfficerList != view) return;
                    _registeredOfficerList = null; view.Abandon(); view.QueueFree();
                    menu.SetBusy(false); menu.Show();
                    if (selected is not int id || store.Current.Find(id) is not { } officer) return;
                    if (deleting) AskRegisteredOfficerQuestion(0x283B, () => DeleteRegisteredOfficer(id));
                    else OpenRegisteredOfficerEditor(officer);
                }), CurrentListScrollSpeed);
        }
        catch
        {
            _registeredOfficerList = null; view.Abandon(); view.QueueFree();
            menu.SetBusy(false); menu.Show(); throw;
        }
    }

    private void OpenRegisteredOfficerEditor(RegisteredOfficer original)
    {
        if (!IsRegisteredOfficersCurrent || _repository is null || _registeredOfficerStore is not { } store ||
            _registeredOfficerEditor is not null) return;
        //The title has deliberately retired the campaign composition. Borrow
        //only an installed scenario's immutable definition/name catalog; this
        //does not create or adopt a playable World for the editor.
        var context = _registeredOfficerDefinitions ??= _campaignComposition?.Scenario ??
            (_shownScenarios.Count > 0 ? _repository.LoadScenario(_shownScenarios[0].Selection)
                : throw new InvalidDataException("Registered officer editor scenario definitions are unavailable."));
        var catalog = RegisteredOfficerEditorCatalog.Load(_repository, _ui.SourceLocale, context,
            store.Current, RegisteredOfficerPortraitCatalog.OrdinaryPortraits, GetPersonalityEditorStore().Current,
            CollectionOfficerAdmission.FromProfile(GetApplicationProfile().Current));
        _registeredOfficerOriginal = original; _registeredOfficerDraft = original;
        _registeredOfficerWorkingLibrary = store.Current;
        _registeredOfficerCatalog = catalog; _registeredOfficerNotice = "";
        var view = new RegisteredOfficerEditorView(); _registeredOfficerEditor = view;
        AddChild(view); _registeredOfficers!.SetBusy(true); _registeredOfficers.Hide();
        try
        {
            view.Configure(_repository, _ui, ReadRegisteredOfficerValues,
                () => CanEditRegisteredOfficer && _registeredOfficerEditor == view,
                () => RunUiAction(RequestRegisteredOfficerEditorBack),
                () => RunUiAction(SaveRegisteredOfficer),
                section => RunUiAction(() => EditRegisteredOfficerSection(section)));
        }
        catch { CloseRegisteredOfficerEditor(); throw; }
    }

    private RegisteredOfficerEditorValues ReadRegisteredOfficerValues()
    {
        var draft = _registeredOfficerDraft ?? throw new InvalidOperationException("Registered officer draft has retired.");
        var catalog = _registeredOfficerCatalog!;
        if (!_registeredOfficerPortraits.TryGetValue(draft.PortraitId, out var portrait))
        {
            var sprites = new LocalizedUiSpriteSource(_repository!);
            var route=catalog.PortraitCatalog.ResolveStanding(draft.PortraitId);
            portrait=(route.Value??throw new InvalidDataException(route.UnresolvedInput)).LoadSprites(sprites.LoadTexturePackSprite);
            _registeredOfficerPortraits.Add(draft.PortraitId, portrait);
        }
        var values = RegisteredOfficerEditorView.CreateValues(_repository!, _ui, draft, catalog, portrait);
        bool valid = values.CanConfirm;
        if (valid)
        {
            try { catalog.Validate(draft); }
            catch (InvalidDataException) { valid = false; }
        }
        return values with { CanConfirm = valid, Notice = _registeredOfficerNotice };
    }

    private void EditRegisteredOfficerSection(int section)
    {
        if (!CanEditRegisteredOfficer || _repository is null) return;
        var editor = _registeredOfficerEditor!;
        var original = _registeredOfficerDraft!;
        var child = new RegisteredOfficerFieldEditorView(); _registeredOfficerField = child;
        AddChild(child); editor.SetBusy(true); if(section!=12)editor.Hide();
        try
        {
            child.Configure(_repository, _ui, original, section, _registeredOfficerCatalog!,
                () => IsRegisteredOfficersCurrent && _registeredOfficerEditor == editor && _registeredOfficerField == child &&
                    _registeredOfficerQuestion is null,
                result => RunUiAction(() =>
                {
                    if (!IsRegisteredOfficersCurrent || _registeredOfficerField != child || _registeredOfficerEditor != editor) return;
                    _registeredOfficerField = null; child.Abandon(); child.QueueFree();
                    editor.Show(); editor.SetBusy(false); editor.Refresh();
                    if (result is not null) AdoptRegisteredOfficerField(original, result);
                }),CurrentListScrollSpeed);
        }
        catch
        {
            _registeredOfficerField = null; child.Abandon(); child.QueueFree();
            editor.Show(); editor.SetBusy(false); throw;
        }
    }

    private void AdoptRegisteredOfficerField(RegisteredOfficer original, RegisteredOfficer proposed)
    {
        if (!CanEditRegisteredOfficer || _registeredOfficerWorkingLibrary is not { } library ||
            _registeredOfficerCatalog is not { } catalog) return;
        void Adopt(bool confirmed)
        {
            if (!CanEditRegisteredOfficer || !ReferenceEquals(_registeredOfficerDraft, original)) return;
            try
            {
                if (original.Sex != proposed.Sex)
                {
                    var next = RegisteredOfficerSexChange.Prepare(library, original, proposed, catalog, confirmed);
                    _registeredOfficerWorkingLibrary = next.Library; _registeredOfficerDraft = next.Officer;
                }
                else _registeredOfficerDraft = proposed;
                _registeredOfficerNotice = ""; _registeredOfficerEditor!.Refresh();
            }
            catch (InvalidDataException error)
            {
                _registeredOfficerNotice = error.Message; _registeredOfficerEditor!.Refresh();
                GD.PushWarning(error.Message);
            }
        }
        if (RegisteredOfficerSexChange.RequiresConfirmation(library, original, proposed, catalog))
            AskRegisteredOfficerQuestion(0x2838, () => Adopt(true));
        else Adopt(false);
    }

    private void SaveRegisteredOfficer()
    {
        if (!CanEditRegisteredOfficer || _registeredOfficerStore is not { } store) return;
        var editor = _registeredOfficerEditor!; editor.SetBusy(true);
        try
        {
            _registeredOfficerCatalog!.Validate(_registeredOfficerDraft!);
            store.Save((_registeredOfficerWorkingLibrary ?? store.Current).Set(_registeredOfficerDraft!));
            CloseRegisteredOfficerEditor();
        }
        catch (Exception error) when (RegisteredOfficerStorageFailure(error))
        { editor.SetBusy(false); RegisteredOfficerSaveFailure(error); }
    }

    private void DeleteRegisteredOfficer(int id)
    {
        if (!IsRegisteredOfficersCurrent || _registeredOfficerStore is not { } store || store.Current.Find(id) is null) return;
        var menu = _registeredOfficers!; menu.SetBusy(true);
        try { store.Save(store.Current.Delete(id)); menu.Refresh(); }
        catch (Exception error) when (RegisteredOfficerStorageFailure(error)) { RegisteredOfficerSaveFailure(error); }
        finally { menu.SetBusy(false); }
    }

    private static bool RegisteredOfficerStorageFailure(Exception error) =>
        error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException;

    private void RegisteredOfficerSaveFailure(Exception error)
    {
        _registeredOfficerNotice = _ui.Get("settings_save_failed");
        if (_registeredOfficerEditor is { } editor) { editor.Refresh(); editor.ApplyNotice(_registeredOfficerNotice); }
        else _registeredOfficers?.ApplyNotice(_registeredOfficerNotice);
        GD.PushWarning(error.ToString());
    }

    private void RequestRegisteredOfficerEditorBack()
    {
        if (!CanEditRegisteredOfficer) return;
        if (_registeredOfficerOriginal is { } original && _registeredOfficerDraft is { } draft &&
            (!RegisteredOfficerSameValues(original, draft) || RegisteredOfficerRelatedLibraryChanged(draft.Id)))
            AskRegisteredOfficerQuestion(0x282E, CloseRegisteredOfficerEditor);
        else CloseRegisteredOfficerEditor();
    }

    private bool RegisteredOfficerRelatedLibraryChanged(int editedId)
    {
        if (_registeredOfficerWorkingLibrary is not { } working || _registeredOfficerStore is not { } store) return false;
        foreach (var officer in working.Officers)
            if (officer.Id != editedId &&
                (store.Current.Find(officer.Id) is not { } saved || !RegisteredOfficerSameValues(officer, saved))) return true;
        return false;
    }

    private static bool RegisteredOfficerSameValues(RegisteredOfficer left, RegisteredOfficer right) =>
        left.LikePersonIds.SequenceEqual(right.LikePersonIds) && left.DislikePersonIds.SequenceEqual(right.DislikePersonIds) &&
        left.TraitIds.SequenceEqual(right.TraitIds) && left.FormationIds.SequenceEqual(right.FormationIds) &&
        left.TacticIds.SequenceEqual(right.TacticIds) &&
        (left with { LikePersonIds = right.LikePersonIds, DislikePersonIds = right.DislikePersonIds,
            TraitIds = right.TraitIds, FormationIds = right.FormationIds, TacticIds = right.TacticIds }) == right;

    private void AskRegisteredOfficerQuestion(int message, Action confirmed)
    {
        if (!IsRegisteredOfficersCurrent || _registeredOfficerQuestion is not null || _repository is null) return;
        var question = new DeploymentTargetConfirmation(); _registeredOfficerQuestion = question;
        _registeredOfficerEditor?.SetBusy(true); _registeredOfficers?.SetBusy(true); AddChild(question);
        try
        {
            question.ConfigureGameMessage(_repository, _ui, message, yes => RunUiAction(() =>
            {
                if (!IsRegisteredOfficersCurrent || _registeredOfficerQuestion != question) return;
                _registeredOfficerQuestion = null; question.Abandon(); question.QueueFree();
                _registeredOfficerEditor?.SetBusy(false);
                if (_registeredOfficerEditor is null) _registeredOfficers?.SetBusy(false);
                if (yes) confirmed();
            }));
            question.Name = "RegisteredOfficerConfirmation";
        }
        catch
        {
            _registeredOfficerQuestion = null; question.Abandon(); question.QueueFree();
            _registeredOfficerEditor?.SetBusy(false);
            if (_registeredOfficerEditor is null) _registeredOfficers?.SetBusy(false);
            throw;
        }
    }

    private void CloseRegisteredOfficerEditor()
    {
        var field = _registeredOfficerField; _registeredOfficerField = null;
        var editor = _registeredOfficerEditor; _registeredOfficerEditor = null;
        _registeredOfficerOriginal = null; _registeredOfficerDraft = null; _registeredOfficerCatalog = null;
        _registeredOfficerWorkingLibrary = null;
        _registeredOfficerNotice = "";
        if (field is not null) { field.Abandon(); field.QueueFree(); }
        if (editor is not null) { editor.Abandon(); editor.QueueFree(); }
        if (IsRegisteredOfficersCurrent) { _registeredOfficers!.SetBusy(false); _registeredOfficers.Show(); _registeredOfficers.Refresh(); }
    }

    private void RequestRegisteredOfficersBack()
    {
        if (_registeredOfficerQuestion is { } question) question.RequestCancel();
        else if (HasRegisteredOfficerExchange) RequestRegisteredOfficerExchangeBack();
        else if (HasRegisteredOfficerBirthYears) RequestRegisteredOfficerBirthYearsBack();
        else if (_registeredOfficerField is { } field) field.RequestBack();
        else if (_registeredOfficerEditor is not null) RequestRegisteredOfficerEditorBack();
        else if (_registeredOfficerList is { } list) list.RequestBack();
        else _registeredOfficers?.RequestBack();
    }

    private void CloseRegisteredOfficers(bool resumeParent = true)
    {
        CloseRegisteredOfficerExchange(resumeMenu: false);
        CloseRegisteredOfficerBirthYears(resumeMenu: false);
        var menu = _registeredOfficers; _registeredOfficers = null;
        var parent = _registeredOfficersParent; _registeredOfficersParent = null;
        var question = _registeredOfficerQuestion; _registeredOfficerQuestion = null;
        var list = _registeredOfficerList; _registeredOfficerList = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        if (list is not null) { list.Abandon(); list.QueueFree(); }
        CloseRegisteredOfficerEditor();
        if (menu is not null) { menu.Abandon(); menu.QueueFree(); }
        _registeredOfficerPortraits.Clear();
        _registeredOfficerDefinitions = null;
        if (resumeParent && !_failed && !_shuttingDown && parent is not null && _titleEditMenu == parent)
        { parent.SetBusy(false); parent.Show(); }
    }
}
