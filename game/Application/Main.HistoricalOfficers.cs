using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

public partial class Main
{
    private HistoricalOfficerStore? _historicalOfficerStore;
    private HistoricalOfficerEditorCatalog? _historicalOfficerCatalog;
    private HistoricalOfficerEditorDraft? _historicalOfficerDraft;
    private Control? _historicalOfficerMenu;
    private TitleEditMenuView? _historicalOfficersParent;
    private AdministrationPageChrome? _historicalOfficerMenuChrome;
    private readonly List<RegisteredOfficerSectionButton> _historicalOfficerMenuButtons = [];
    private UiSourceText? _historicalOfficerCount;
    private UiSourceText? _historicalOfficerMaximum;
    private CommandSelectionView? _historicalOfficerList;
    private RegisteredOfficerEditorView? _historicalOfficerEditor;
    private HistoricalOfficerFieldEditorView? _historicalOfficerField;
    private DeploymentTargetConfirmation? _historicalOfficerQuestion;
    private readonly Dictionary<int, IReadOnlyList<LoadedSprite>> _historicalOfficerPortraits = [];
    private string _historicalOfficerNotice = "";
    private string? _historicalOfficerLocale;

    // Title editing and new-game adoption share this one durable application owner.
    private HistoricalOfficerStore GetHistoricalOfficerStore()
    {
        if (_historicalOfficerStore is not null) return _historicalOfficerStore;
        string path = ProjectSettings.GlobalizePath("user://historical-officers.s14h");
        return _historicalOfficerStore = File.Exists(path) ? HistoricalOfficerStore.OpenExisting(path)
            : HistoricalOfficerStore.CreateNew(path);
    }

    private bool HasHistoricalOfficers => _historicalOfficerMenu is not null;
    private bool IsHistoricalOfficersCurrent => !_failed && !_shuttingDown && _titleMenu is not null &&
        _playerForceId is null && _historicalOfficerMenu is not null &&
        ReferenceEquals(_titleEditMenu, _historicalOfficersParent) && _historicalOfficerLocale == _ui.SourceLocale;
    private bool CanEditHistoricalOfficer => IsHistoricalOfficersCurrent && _historicalOfficerEditor is not null &&
        _historicalOfficerDraft is not null && _historicalOfficerField is null && _historicalOfficerQuestion is null;

    private void OpenHistoricalOfficers()
    {
        if (_failed || _shuttingDown || _repository is null || _titleMenu is null ||
            _titleEditMenu is not { } parent || _playerForceId is not null || HasHistoricalOfficers) return;
        var menu = new Control { Name = "HistoricalOfficers", ZIndex = 5, MouseFilter = Control.MouseFilterEnum.Stop };
        _historicalOfficerMenu = menu; _historicalOfficersParent = parent;
        _historicalOfficerLocale = _ui.SourceLocale; _historicalOfficerNotice = "";
        AddChild(menu); menu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); parent.SetBusy(true); parent.Hide();
        try
        {
            BuildHistoricalOfficerMenu(menu);
            try
            {
                GetHistoricalOfficerStore();
                _historicalOfficerCatalog = HistoricalOfficerEditorCatalog.Load(_repository, _ui.SourceLocale,
                    GetRegisteredOfficerStore().Current, CollectionOfficerAdmission.FromProfile(GetApplicationProfile().Current),
                    GetPersonalityEditorStore().Current);
                RefreshHistoricalOfficerMenu();
            }
            catch (Exception error) when (RegisteredOfficerStorageFailure(error)) { HistoricalOfficerFailure(error); }
        }
        catch { CloseHistoricalOfficers(); throw; }
    }

    private void BuildHistoricalOfficerMenu(Control menu)
    {
        var chrome = new AdministrationPageChrome(); _historicalOfficerMenuChrome = chrome; menu.AddChild(chrome);
        chrome.Configure(_repository!, _ui, 0x67, () => RunUiAction(() => CloseHistoricalOfficers()), () => { }, returnOnly: true);
        var root = new UiSourceLayout(RegisteredOfficerEditorLayoutSource.LoadMenu(_repository!));
        var buttons = new UiSourceLayout(RegisteredOfficerEditorLayoutSource.LoadMenuButtons(_repository!));
        var count = new UiSourceLayout(MapMarkerLayout.LoadNamed(_repository!, "02_05_00_offset_00_new"));
        var buttonHost = new Control { Name = "HistoricalOfficerMenuButtons", Position = root.Rectangle(3).Position,
            Size = root.Rectangle(3).Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(buttonHost);
        for (int index = 0; index < 2; index++)
        {
            int action = index; var box = buttons.Rectangle(3 + index * 3);
            var button = new RegisteredOfficerSectionButton { Name = "HistoricalOfficerMenu" + index,
                Position = box.Position, Size = box.Size, InputAdmission = HistoricalOfficerMenuAdmitted };
            buttonHost.AddChild(button); _historicalOfficerMenuButtons.Add(button);
            button.Configure(_repository!, chrome.Assets, chrome.SpriteShader, chrome.Font,
                HistoricalOfficerText(0x67 + index), () =>
                { if (HistoricalOfficerMenuAdmitted() && !button.Disabled) RunUiAction(() => SelectHistoricalOfficer(action == 1)); });
        }
        var countHost = new Control { Name = "HistoricalOfficerEntryCount", Position = root.Rectangle(4).Position,
            Size = root.Rectangle(4).Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(countHost);
        foreach (var node in count.Nodes.Where(node => node.Type is 1 or 3))
            count.Art(countHost, node.Index, count.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        count.Text(countHost, 2, default, HistoricalOfficerText(0x6AB), chrome.Font, MapTextPresets.Create()[13]);
        count.Text(countHost, 4, default, HistoricalOfficerText(0x60C), chrome.Font);
        _historicalOfficerMaximum = count.Text(countHost, 3, default, "", chrome.Font);
        _historicalOfficerMaximum.Name = "HistoricalOfficerMaximum";
        _historicalOfficerCount = count.Text(countHost, 5, default, "", chrome.Font);
        _historicalOfficerCount.Name = "HistoricalOfficerSavedCount";
        menu.SetMeta("source_layout", root.Source.Source.ResourceUri);
        RefreshHistoricalOfficerMenu();
    }

    private bool HistoricalOfficerMenuAdmitted() => IsHistoricalOfficersCurrent &&
        _historicalOfficerMenu!.IsVisibleInTree() && _historicalOfficerList is null &&
        _historicalOfficerEditor is null && _historicalOfficerQuestion is null;

    private string HistoricalOfficerText(int id) => _repository!.LoadMessages(_ui.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException("Missing historical officer caption.");

    private void RefreshHistoricalOfficerMenu()
    {
        if (_historicalOfficerMenu is null) return;
        bool available = _historicalOfficerCatalog is not null && _historicalOfficerStore is not null;
        for (int index = 0; index < _historicalOfficerMenuButtons.Count; index++)
            _historicalOfficerMenuButtons[index].Disabled = !available || !HistoricalOfficerMenuAdmitted() ||
                index == 1 && !_historicalOfficerCatalog!.Rows.Any(row => _historicalOfficerStore!.Current.Find(row.Id) is not null);
        _historicalOfficerMenuChrome?.ApplyDefaultHint(_historicalOfficerNotice);
        if (_historicalOfficerCount is not null && available)
        {
            var admitted = _historicalOfficerCatalog!.Rows.Select(row => row.Id).ToHashSet();
            _historicalOfficerCount.Text = _historicalOfficerStore!.Current.Overrides.Count(row => row.Id <= 1000 || admitted.Contains(row.Id))
                .ToString(CultureInfo.InvariantCulture);
            if (_historicalOfficerMaximum is { } maximum)
                maximum.Text = _historicalOfficerCatalog.UnresolvedCandidates.Any(row => row.OfficerId is >= 2001 and <= 2088)
                    ? _ui.Get("detail_unknown") : (1000 + _historicalOfficerCatalog.Rows.Count(row => row.Id >= 2001))
                        .ToString(CultureInfo.InvariantCulture);
        }
    }

    private void SelectHistoricalOfficer(bool restoring)
    {
        if (!HistoricalOfficerMenuAdmitted() || _historicalOfficerCatalog is not { } catalog ||
            _historicalOfficerStore is not { } store) return;
        var candidates = catalog.Rows.Where(row => !restoring || store.Current.Find(row.Id) is not null).ToArray();
        if (candidates.Length == 0) return;
        var columns = new[] { new CommandSelectionColumn("name", HistoricalOfficerText(0x211), 400),
            new CommandSelectionColumn("leadership", HistoricalOfficerText(0x238), 180, true, InitialAscending: false),
            new CommandSelectionColumn("strength", HistoricalOfficerText(0x239), 180, true, InitialAscending: false),
            new CommandSelectionColumn("intelligence", HistoricalOfficerText(0x23A), 180, true, InitialAscending: false),
            new CommandSelectionColumn("politics", HistoricalOfficerText(0x23B), 180, true, InitialAscending: false),
            new CommandSelectionColumn("charm", HistoricalOfficerText(0x23C), 180, true, InitialAscending: false) };
        var rows = candidates.Select(row => new CommandSelectionRow(row.Id,
            [CommandSelectionCell.PersonName(row.Name, row.NameSortKey), new("", row.Original.Leadership),
                new("", row.Original.Strength), new("", row.Original.Intelligence), new("", row.Original.Politics),
                new("", row.Original.Charm)], true)).ToArray();
        // Numeric row text is published explicitly, while comparison remains numeric.
        rows = rows.Select(row => row with { Cells = row.Cells.Select(cell => cell.Number is long value
            ? cell with { Text = value.ToString(CultureInfo.InvariantCulture) } : cell).ToArray() }).ToArray();
        var view = new CommandSelectionView(); _historicalOfficerList = view; AddChild(view); _historicalOfficerMenu!.Hide();
        try
        {
            view.Configure(_repository!, _ui, 0x32E, columns, rows, [], restoring ? candidates.Length : 1,
                () => IsHistoricalOfficersCurrent && _historicalOfficerList == view && _historicalOfficerEditor is null &&
                    _historicalOfficerQuestion is null,
                selected => RunUiAction(() =>
                {
                    if (!IsHistoricalOfficersCurrent || _historicalOfficerList != view) return;
                    if (selected is null) { CloseHistoricalOfficerList(); return; }
                    if (restoring)
                        AskHistoricalOfficerQuestion(0x2865, () => RestoreHistoricalOfficerProfiles(selected));
                    else if (selected.Count == 1) OpenHistoricalOfficerEditor(selected[0]);
                }), CurrentListScrollSpeed, "", retainOnAccept: true, retainOnReturn: true,
                singleSelection: !restoring);
            view.Name = "HistoricalOfficerList";
        }
        catch { CloseHistoricalOfficerList(); throw; }
    }

    private void OpenHistoricalOfficerEditor(int id)
    {
        if (!IsHistoricalOfficersCurrent || _historicalOfficerCatalog is not { } catalog ||
            _historicalOfficerStore is not { } store || _historicalOfficerEditor is not null) return;
        _historicalOfficerDraft = new(catalog, store.Current, id); _historicalOfficerNotice = "";
        var view = new RegisteredOfficerEditorView(); _historicalOfficerEditor = view; AddChild(view); _historicalOfficerList?.Hide();
        try
        {
            view.Configure(_repository!, _ui, ReadHistoricalOfficerValues,
                () => CanEditHistoricalOfficer && _historicalOfficerEditor == view,
                () => RunUiAction(RequestHistoricalOfficerEditorBack), () => RunUiAction(SaveHistoricalOfficer),
                section => RunUiAction(() => EditHistoricalOfficerSection(section)),
                new(0x67, false, new HashSet<int> { 2, 3, 4, 5, 6, 7, 9, 10, 12 }));
            view.ConfigureRestoreOriginal(() => RunUiAction(() => AskHistoricalOfficerQuestion(0x2863, () =>
            { _historicalOfficerDraft!.RestoreOriginal(); _historicalOfficerNotice = ""; view.Refresh(); })));
            view.Name = "HistoricalOfficerEditor";
        }
        catch { CloseHistoricalOfficerEditor(); throw; }
    }

    private RegisteredOfficerEditorValues ReadHistoricalOfficerValues()
    {
        var draft = _historicalOfficerDraft ?? throw new InvalidOperationException("Historical officer draft has retired.");
        if (!_historicalOfficerPortraits.TryGetValue(draft.Value.PortraitId, out var portrait))
        {
            var sprites = new LocalizedUiSpriteSource(_repository!);
            var route=_historicalOfficerCatalog!.PortraitCatalog.ResolveStanding(draft.Value.PortraitId);
            portrait=(route.Value??throw new InvalidDataException(route.UnresolvedInput)).LoadSprites(sprites.LoadTexturePackSprite);
            _historicalOfficerPortraits.Add(draft.Value.PortraitId, portrait);
        }
        bool valid = true;
        try { _historicalOfficerCatalog!.Validate(draft.Value); }
        catch (InvalidDataException) { valid = false; }
        return HistoricalOfficerEditorValueMapper.Create(_repository!, _ui, draft.Row, draft.Value,
            _historicalOfficerCatalog!, portrait, valid, _historicalOfficerNotice);
    }

    private void EditHistoricalOfficerSection(int section)
    {
        if (!CanEditHistoricalOfficer || !HistoricalOfficerEditorCatalog.CanEdit(section)) return;
        var editor = _historicalOfficerEditor!; var draft = _historicalOfficerDraft!;
        var child = new HistoricalOfficerFieldEditorView(); _historicalOfficerField = child; AddChild(child);
        editor.SetBusy(true); if(section!=12)editor.Hide();
        void Finish(HistoricalOfficerEdit? value)
        {
            if (!IsHistoricalOfficersCurrent || _historicalOfficerField != child || _historicalOfficerEditor != editor) return;
            _historicalOfficerField = null; child.Abandon(); child.QueueFree(); editor.Show(); editor.SetBusy(false);
            try { if (value is not null) draft.Replace(value); _historicalOfficerNotice = ""; editor.Refresh(); }
            catch (InvalidDataException error) { HistoricalOfficerFailure(error); }
        }
        try
        {
            child.Configure(_repository!, _ui, _historicalOfficerCatalog!, draft, section,
                value => RunUiAction(() => Finish(value)), () => RunUiAction(() => Finish(null)),
                () => IsHistoricalOfficersCurrent && _historicalOfficerField == child && _historicalOfficerQuestion is null,
                CurrentListScrollSpeed);
        }
        catch { Finish(null); throw; }
    }

    private void SaveHistoricalOfficer()
    {
        if (!CanEditHistoricalOfficer || _historicalOfficerStore is not { } store) return;
        var editor = _historicalOfficerEditor!; editor.SetBusy(true);
        try { store.Save(_historicalOfficerDraft!.Commit()); CloseHistoricalOfficerEditor(); }
        catch (Exception error) when (RegisteredOfficerStorageFailure(error))
        { editor.SetBusy(false); HistoricalOfficerFailure(error); }
    }

    private void RestoreHistoricalOfficerProfiles(IReadOnlyList<int> ids)
    {
        if (!IsHistoricalOfficersCurrent || _historicalOfficerStore is not { } store) return;
        try { store.Save(HistoricalOfficerEditing.Restore(store.Current, ids)); CloseHistoricalOfficerList(); }
        catch (Exception error) when (RegisteredOfficerStorageFailure(error))
        { _historicalOfficerList?.Show(); HistoricalOfficerFailure(error); }
    }

    private void RequestHistoricalOfficerEditorBack()
    {
        if (!CanEditHistoricalOfficer) return;
        if (_historicalOfficerDraft!.DirtyAgainstSaved) AskHistoricalOfficerQuestion(0x2862, CloseHistoricalOfficerEditor);
        else CloseHistoricalOfficerEditor();
    }

    private void AskHistoricalOfficerQuestion(int message, Action confirmed)
    {
        if (!IsHistoricalOfficersCurrent || _historicalOfficerQuestion is not null) return;
        var question = new DeploymentTargetConfirmation(); _historicalOfficerQuestion = question; AddChild(question);
        _historicalOfficerEditor?.SetBusy(true);
        if (_historicalOfficerEditor is null) _historicalOfficerList?.Show();
        try
        {
            question.ConfigureGameMessage(_repository!, _ui, message, yes => RunUiAction(() =>
            {
                if (!IsHistoricalOfficersCurrent || _historicalOfficerQuestion != question) return;
                _historicalOfficerQuestion = null; question.Abandon(); question.QueueFree();
                _historicalOfficerEditor?.SetBusy(false);
                if (yes) confirmed();
                else if (_historicalOfficerEditor is { } editor) editor.Show();
                else _historicalOfficerList?.Show();
                RefreshHistoricalOfficerMenu();
            }));
            question.Name = "HistoricalOfficerConfirmation";
        }
        catch
        {
            _historicalOfficerQuestion = null; question.Abandon(); question.QueueFree();
            _historicalOfficerEditor?.SetBusy(false); throw;
        }
    }

    private void HistoricalOfficerFailure(Exception error)
    {
        _historicalOfficerNotice = _ui.Get("settings_save_failed");
        _historicalOfficerEditor?.ApplyNotice(_historicalOfficerNotice);
        _historicalOfficerList?.ApplyNotice(_historicalOfficerNotice);
        _historicalOfficerMenuChrome?.ApplyDefaultHint(_historicalOfficerNotice);
        GD.PushWarning(error.ToString()); RefreshHistoricalOfficerMenu();
    }

    private void CloseHistoricalOfficerEditor()
    {
        var field = _historicalOfficerField; _historicalOfficerField = null;
        var editor = _historicalOfficerEditor; _historicalOfficerEditor = null; _historicalOfficerDraft = null;
        if (field is not null) { field.Abandon(); field.QueueFree(); }
        if (editor is not null) { editor.Abandon(); editor.QueueFree(); }
        if (IsHistoricalOfficersCurrent) _historicalOfficerList?.Show();
    }

    private void CloseHistoricalOfficerList()
    {
        var list = _historicalOfficerList; _historicalOfficerList = null;
        if (list is not null) { list.Abandon(); list.QueueFree(); }
        if (IsHistoricalOfficersCurrent) { _historicalOfficerMenu!.Show(); RefreshHistoricalOfficerMenu(); }
    }

    private void RequestHistoricalOfficersBack()
    {
        if (_historicalOfficerQuestion is { } question) question.RequestCancel();
        else if (_historicalOfficerField is { } field) field.RequestBack();
        else if (_historicalOfficerEditor is not null) RequestHistoricalOfficerEditorBack();
        else if (_historicalOfficerList is { } list) list.RequestCancel();
        else CloseHistoricalOfficers();
    }

    private void CloseHistoricalOfficers(bool resumeParent = true)
    {
        var menu = _historicalOfficerMenu; _historicalOfficerMenu = null;
        var parent = _historicalOfficersParent; _historicalOfficersParent = null;
        var question = _historicalOfficerQuestion; _historicalOfficerQuestion = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        CloseHistoricalOfficerEditor(); CloseHistoricalOfficerList();
        _historicalOfficerMenuChrome?.Abandon(); _historicalOfficerMenuChrome = null;
        if (menu is not null) { menu.Hide(); menu.QueueFree(); }
        _historicalOfficerMenuButtons.Clear(); _historicalOfficerCount = null; _historicalOfficerMaximum = null;
        _historicalOfficerPortraits.Clear();
        _historicalOfficerCatalog = null; _historicalOfficerLocale = null; _historicalOfficerNotice = "";
        if (resumeParent && !_failed && !_shuttingDown && parent is not null && _titleEditMenu == parent)
        { parent.SetBusy(false); parent.Show(); }
    }
}
