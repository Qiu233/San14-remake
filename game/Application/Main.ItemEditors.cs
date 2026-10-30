using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private ItemEditorStore? _itemEditorStore;
    private ItemEditorCatalog? _itemEditorCatalog;
    private LoadedScenario? _itemEditorDefinitions;
    private TitleEditMenuView? _itemEditorsParent;
    private ItemEditorMenuView? _itemEditorMenu;
    private ItemEditorView? _itemEditorPage;
    private CommandSelectionView? _itemEditorSelector;
    private Control? _itemEditorChild;
    private DeploymentTargetConfirmation? _itemEditorQuestion;
    private DeploymentNarrationView? _itemEditorNotice;
    private ItemEditorPageValues? _itemEditorOriginal, _itemEditorWorking;
    private IReadOnlyList<ItemEditorExternalImage> _itemExternalImages = [];
    private bool _itemEditorsCreated, _itemEditorCreating;
    private int _itemEditorId;
    private bool HasItemEditors => _itemEditorMenu is not null;
    private bool IsItemEditorsCurrent => !_failed && !_shuttingDown && _titleMenu is not null &&
        _playerForceId is null && _itemEditorMenu is not null && ReferenceEquals(_titleEditMenu, _itemEditorsParent);
    private bool CanEditItemPage => IsItemEditorsCurrent && _itemEditorPage is not null &&
        _itemEditorChild is null && _itemEditorSelector is null && _itemEditorQuestion is null && _itemEditorNotice is null;
    private ItemEditorStore GetItemEditorStore()
    {
        if (_itemEditorStore is not null) return _itemEditorStore;
        string path = ProjectSettings.GlobalizePath("user://item-editors.s14r");
        return _itemEditorStore = File.Exists(path) ? ItemEditorStore.OpenExisting(path) : ItemEditorStore.CreateNew(path);
    }
    private ItemEditorCatalog GetItemEditorCatalog()
    {
        if (_repository is null) throw new InvalidOperationException("The title resource owner has retired.");
        if (_itemEditorCatalog?.Locale == _ui.SourceLocale) return _itemEditorCatalog;
        _itemEditorDefinitions = _shownScenarios.Count > 0 ? _repository.LoadScenario(_shownScenarios[0].Selection)
            : throw new InvalidDataException("Title item definitions are unavailable.");
        _itemExternalImages = ItemEditorExternalImages.Read(ItemEditorExternalImages.OriginalDirectory);
        return _itemEditorCatalog = new(_repository, _itemEditorDefinitions, _ui.SourceLocale);
    }
    private void OpenHistoricalItemsEditor() => OpenItemEditors(false);
    private void OpenNewItemsEditor() => OpenItemEditors(true);
    private void OpenItemEditors(bool created)
    {
        if (_repository is null || _titleMenu is null || _titleEditMenu is not { } parent ||
            HasItemEditors || _failed || _shuttingDown || _playerForceId is not null) return;
        var menu = new ItemEditorMenuView(); _itemEditorMenu = menu; _itemEditorsParent = parent; _itemEditorsCreated = created;
        AddChild(menu); parent.SetBusy(true); parent.Hide();
        try
        {
            GetItemEditorStore(); GetItemEditorCatalog();
            menu.Configure(_repository, _ui, created,
                () => IsItemEditorsCurrent && _itemEditorMenu == menu && _itemEditorPage is null &&
                    _itemEditorSelector is null && _itemEditorQuestion is null && _itemEditorNotice is null,
                ItemEditorMenuEnabled, () => RunUiAction(() => CloseItemEditors()),
                action => RunUiAction(() => SelectItemEditorAction(action)));
        }
        catch { CloseItemEditors(); throw; }
    }
    private bool ItemEditorMenuEnabled(int action)
    {
        var library = _itemEditorStore?.Current;
        if (library is null) return false;
        return _itemEditorsCreated ? action is 0 or 4 ? library.FirstFreeCreatedId is not null : library.Created.Length > 0
            : action == 0 ? GetItemEditorCatalog().Historical(library).Count > 0 : library.Historical.Length > 0;
    }
    private void SelectItemEditorAction(int action)
    {
        if (!IsItemEditorsCurrent || _itemEditorSelector is not null || _itemEditorPage is not null || !ItemEditorMenuEnabled(action)) return;
        var library = GetItemEditorStore().Current; var catalog = GetItemEditorCatalog();
        if (!_itemEditorsCreated)
        {
            var rows = catalog.Historical(library, editedOnly: action == 1);
            SelectItems(action == 0 ? 0x5AA : 0x735, rows, action == 0 ? 1 : rows.Count, selected =>
            {
                if (selected.Count == 0) return;
                if (action == 0)
                {
                    var item = catalog.Historical(selected[0], library); _itemEditorId = item.Id; _itemEditorCreating = false;
                    OpenItemEditor(new(item.Name, item.Description, item.Values, true, ""));
                }
                else AskItemEditorQuestion(0x28C1, () => SaveItemLibrary(library.RestoreHistorical(selected)));
            });
            return;
        }
        if (action == 0)
        {
            var rows = catalog.Templates(library);
            SelectItems(0x7D1, rows, 1, selected =>
            {
                if (selected.Count == 0) return;
                var template = rows.First(item => item.Id == selected[0]);
                _itemEditorId = library.FirstFreeCreatedId ?? throw new InvalidDataException("The created item library is full.");
                _itemEditorCreating = true; OpenItemEditor(new(template.Name, "", template.Values, true, ""));
            }, ItemMessage(0x28E9));
        }
        else if (action == 4) OpenItemImport();
        else
        {
            var rows = library.Created.Select(item => new ItemEditorCandidate(item.Id, item.Name, item.Description,
                item.Values, item.Values, false)).ToArray();
            SelectItems(action == 1 ? 0x5AA : action == 2 ? 0x7D2 : 0x7D3, rows, action == 1 ? 1 : rows.Length, selected =>
            {
                if (selected.Count == 0) return;
                if (action == 1)
                {
                    var item = library.FindCreated(selected[0])!; _itemEditorId = item.Id; _itemEditorCreating = false;
                    OpenItemEditor(new(item.Name, item.Description, item.Values, true, ""));
                }
                else if (action == 2) AskItemEditorQuestion(0x2903, () => SaveItemLibrary(library.DeleteCreated(selected)));
                else AskItemEditorQuestion(0x2905, () => ExportItems(library, selected));
            });
        }
    }
    private void OpenItemEditor(ItemEditorPageValues original)
    {
        if (_repository is null || !IsItemEditorsCurrent) return;
        _itemEditorOriginal = _itemEditorWorking = original;
        var view = new ItemEditorView(); _itemEditorPage = view; AddChild(view); _itemEditorMenu!.Hide();
        view.Configure(_repository, _ui, GetItemEditorCatalog(), _itemEditorsCreated, _itemEditorCreating, _itemExternalImages,
            () => _itemEditorWorking! with { CanConfirm = _itemEditorCreating || _itemEditorWorking != _itemEditorOriginal },
            () => CanEditItemPage && _itemEditorPage == view,
            () => RunUiAction(RequestItemEditorPageBack), () => RunUiAction(SaveItemEditor),
            section => RunUiAction(() => OpenItemEditorField(section)),
            worth => { if (CanEditItemPage) _itemEditorWorking = _itemEditorWorking! with { Values = _itemEditorWorking!.Values with { Worth = worth } }; });
    }
    private void SaveItemEditor()
    {
        if (!CanEditItemPage || _itemEditorWorking is not { } value) return;
        var library = GetItemEditorStore().Current;
        var next = _itemEditorsCreated ? library.SetCreated(new(_itemEditorId, value.Name, value.Description, value.Values))
            : GetItemEditorCatalog().CommitHistorical(library, _itemEditorId, value.Values);
        if (SaveItemLibrary(next)) CloseItemEditorPage();
    }
    private bool SaveItemLibrary(ItemEditorLibrary next)
    {
        try { GetItemEditorStore().Save(next); _itemEditorMenu?.Refresh(); return true; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        { ItemEditorStorageFailure(error); return false; }
    }
    private void RequestItemEditorPageBack()
    {
        if (!CanEditItemPage) return;
        if (_itemEditorOriginal != _itemEditorWorking) AskItemEditorQuestion(0x28BD, CloseItemEditorPage);
        else CloseItemEditorPage();
    }
    private string ItemMessage(int id, int? count = null, string? file = null) =>
        RegisteredOfficerExchangeMessages.Format(_repository!, _ui.SourceLocale, id, count, file);
    private void AskItemEditorQuestion(int id, Action yes)
    {
        if (!IsItemEditorsCurrent || _itemEditorQuestion is not null || _repository is null) return;
        var question = new DeploymentTargetConfirmation(); _itemEditorQuestion = question; AddChild(question);
        question.ConfigureWrappedMessage(_repository, _ui, ItemMessage(id), confirmed => RunUiAction(() =>
        {
            if (!IsItemEditorsCurrent || _itemEditorQuestion != question) return;
            _itemEditorQuestion = null; question.Abandon(); question.QueueFree();
            if (confirmed) yes();
        }));
        question.SetMeta("message_record", id);
    }
    private void ItemEditorStorageFailure(Exception error)
    { _itemEditorPage?.ApplyNotice(_ui.Get("settings_save_failed")); _itemEditorMenu?.ApplyNotice(_ui.Get("settings_save_failed")); GD.PushWarning(error.ToString()); }
    private void RequestItemEditorsBack()
    {
        if (_itemEditorQuestion is { } question) question.RequestCancel();
        else if (_itemEditorNotice is not null) return;
        else if (_itemEditorSelector is { } selection) selection.RequestCancel();
        else if (_itemEditorChild is ItemEditorFieldView field) field.RequestBack();
        else if (_itemEditorChild is ItemEditorTextView text) text.RequestBack();
        else if (_itemEditorChild is ItemEditorImageView image) image.RequestBack();
        else if (_itemEditorChild is DeploymentCalculatorDialog calculator) calculator.RequestCancel();
        else if (_itemEditorPage is not null) RequestItemEditorPageBack();
        else _itemEditorMenu?.RequestBack();
    }
    private void CloseItemEditorPage()
    {
        var child = _itemEditorChild; _itemEditorChild = null; if (child is not null) RetireItemControl(child);
        var view = _itemEditorPage; _itemEditorPage = null;
        _itemEditorOriginal = _itemEditorWorking = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (_itemEditorMenu is { } menu) { menu.Show(); menu.SetBusy(false); menu.Refresh(); }
    }
    private void CloseItemEditors(bool resumeParent = true)
    {
        var question = _itemEditorQuestion; _itemEditorQuestion = null; if (question is not null) { question.Abandon(); question.QueueFree(); }
        var notice = _itemEditorNotice; _itemEditorNotice = null; if (notice is not null) { notice.Abandon(); notice.QueueFree(); }
        var selector = _itemEditorSelector; _itemEditorSelector = null; if (selector is not null) { selector.Abandon(); selector.QueueFree(); }
        CloseItemEditorPage(); var menu = _itemEditorMenu; _itemEditorMenu = null;
        _itemEditorsParent = null; if (menu is not null) { menu.Abandon(); menu.QueueFree(); }
        _itemEditorCatalog = null; _itemEditorDefinitions = null; _itemExternalImages = [];
        if (resumeParent && !_failed && !_shuttingDown && _titleEditMenu is { } parent) { parent.SetBusy(false); parent.Show(); }
    }
    private static void RetireItemControl(Control child)
    {
        switch (child)
        { case ItemEditorFieldView field: field.Abandon(); break; case ItemEditorTextView text: text.Abandon(); break;
          case ItemEditorImageView image: image.Abandon(); break; case DeploymentCalculatorDialog calculator: calculator.Abandon(); break; }
        child.QueueFree();
    }
}
