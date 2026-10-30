using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private CurrentWorldEditTargetView? _worldEntitySelector;
    private CurrentWorldEditKind _worldEntityKind;
    private int _worldEntityId;
    private Control? _worldEntityPage, _worldEntitySubpage;
    private Action? _worldEntityPageAbandon, _worldEntityPageBack, _worldEntityPageRefresh;
    private Action<bool>? _worldEntityPageBusy;
    private Action? _worldEntitySubpageAbandon, _worldEntitySubpageBack;

    private bool CanEditCurrentWorldEntity => IsCurrentWorldEditCurrent && _worldEntityPage is not null &&
        _worldEntitySubpage is null && _worldEditQuestion is null;

    private void OpenCurrentWorldEntitySelection(CurrentWorldEditKind kind)
    {
        if (_repository is null || !IsCurrentWorldEditCurrent || _worldEntitySelector is not null) return;
        var targets = CurrentWorldEditRules.QueryCurrentWorldEditTargets(_worldEditWorld!, kind);
        if (!targets.Available || targets.Value is not { } roster)
        { CurrentWorldEntityUnavailable(targets.UnresolvedInput); return; }
        _worldEntityKind = kind;
        var content = CurrentWorldEntityContent(roster.Select(target => target.TargetId).ToArray());
        int title = kind switch { CurrentWorldEditKind.City => 0x77, CurrentWorldEditKind.Area => 0x78, _ => 0x76 };
        int guide = kind switch { CurrentWorldEditKind.City => 0x5F30, CurrentWorldEditKind.Area => 0x5F34, _ => 0x5F1E };
        var view = new CurrentWorldEditTargetView(); _worldEntitySelector = view; AddChild(view);
        _worldEditMenu!.SetBusy(true); _worldEditMenu.Hide();
        try
        {
            view.Configure(_repository, _ui, content, GetItemEditorCatalog().Data(title),
                new AdministrationMessages(_repository, _ui.SourceLocale).Format(guide),
                () => IsCurrentWorldEditCurrent && _worldEntitySelector == view && _worldEntityPage is null && _worldEditQuestion is null,
                id => RunUiAction(() =>
                {
                    if (!IsCurrentWorldEditCurrent || _worldEntitySelector != view || _worldEntityPage is not null) return;
                    if (id is not int target) { CloseCurrentWorldEntitySelection(); return; }
                    _worldEntityId = target; view.Hide();
                    try
                    {
                        if (kind == CurrentWorldEditKind.City) OpenCurrentWorldCity(target);
                        else if (kind == CurrentWorldEditKind.Area) OpenCurrentWorldArea(target);
                        else OpenCurrentWorldForce(target);
                    }
                    catch { CloseCurrentWorldEntityPage(); view.Show(); throw; }
                    if (_worldEntityPage is null) view.Show();
                }), CurrentListScrollSpeed);
        }
        catch { CloseCurrentWorldEntitySelection(); throw; }
    }
    private IPagedInformationListContent CurrentWorldEntityContent(IReadOnlyList<int> ids)
    {
        var rules = CurrentWorldEditRules; var world = _worldEditWorld!; int player = _worldEditSession!.PlayerForceId!.Value;
        return _worldEntityKind switch
        {
            CurrentWorldEditKind.City => new CityInformationListContent(rules.PrepareCityInformationPages(world, player, ids)),
            CurrentWorldEditKind.Area => new AreaInformationListContent(rules.PrepareAreaInformationPages(world, player, ids)),
            _ => new ForceInformationListContent(rules.PrepareForceInformationPages(world, player, ids))
        };
    }
    private void MountCurrentWorldEntityPage(Control view, Action abandon, Action back, Action refresh, Action<bool> busy)
    {
        _worldEntityPage = view; _worldEntityPageAbandon = abandon; _worldEntityPageBack = back;
        _worldEntityPageRefresh = refresh; _worldEntityPageBusy = busy; AddChild(view);
    }
    private void MountCurrentWorldEntitySubpage(Control view, Action abandon, Action back)
    {
        _worldEntitySubpage = view; _worldEntitySubpageAbandon = abandon; _worldEntitySubpageBack = back;
        _worldEntityPageBusy?.Invoke(true); _worldEntityPage!.Hide(); AddChild(view);
    }
    private void CloseCurrentWorldEntitySubpage(bool resume = true)
    {
        var view = _worldEntitySubpage; var abandon = _worldEntitySubpageAbandon;
        _worldEntitySubpage = null; _worldEntitySubpageAbandon = _worldEntitySubpageBack = null;
        if (view is not null) { abandon?.Invoke(); view.QueueFree(); }
        if (resume && IsCurrentWorldEditCurrent && _worldEntityPage is { } page)
        { page.Show(); _worldEntityPageBusy?.Invoke(false); _worldEntityPageRefresh?.Invoke(); }
    }
    private void CloseCurrentWorldEntityPage()
    {
        CloseCurrentWorldEntitySubpage(resume: false);
        var page = _worldEntityPage; var abandon = _worldEntityPageAbandon;
        _worldEntityPage = null; _worldEntityPageAbandon = _worldEntityPageBack = _worldEntityPageRefresh = null; _worldEntityPageBusy = null;
        if (page is not null) { abandon?.Invoke(); page.QueueFree(); }
        ClearCurrentWorldCityDraft(); ClearCurrentWorldAreaDraft(); ClearCurrentWorldForceDraft();
    }
    private void ReturnToCurrentWorldEntitySelector()
    {
        if (!IsCurrentWorldEditCurrent || _worldEntitySelector is not { } selector) return;
        CloseCurrentWorldEntityPage();
        var query = CurrentWorldEditRules.QueryCurrentWorldEditTargets(_worldEditWorld!, _worldEntityKind);
        if (!query.Available || query.Value is not { } targets)
        { CurrentWorldEntityUnavailable(query.UnresolvedInput); CloseCurrentWorldEntitySelection(); return; }
        selector.AdoptContent(CurrentWorldEntityContent(targets.Select(target => target.TargetId).ToArray())); selector.Show();
    }
    private void RequestLeaveCurrentWorldEntity(bool dirty)
    {
        if (!CanEditCurrentWorldEntity) return;
        if (!dirty) ReturnToCurrentWorldEntitySelector();
        else AskCurrentWorldEditQuestion(0x5F2F, discard => { if (discard) ReturnToCurrentWorldEntitySelector(); });
    }
    private void SubmitCurrentWorldEntity(CurrentWorldEditOrder order)
    {
        if (!CanEditCurrentWorldEntity) return;
        _worldEntityPageBusy?.Invoke(true);
        if (!SubmitCurrentWorldEdit(order, ReturnToCurrentWorldEntitySelector))
        { _worldEntityPageBusy?.Invoke(false); CurrentWorldEntityUnavailable(_playMessage.Text); }
    }
    private void OpenCurrentWorldEntityCalculator(int? value, int minimum, int? maximum, int title, Action<int> accepted)
    {
        if (_repository is null || !CanEditCurrentWorldEntity || value is null || maximum is null) return;
        var view = new DeploymentCalculatorDialog();
        MountCurrentWorldEntitySubpage(view, view.Abandon, view.RequestCancel);
        view.Configure(_repository, _ui, value.Value, minimum, maximum.Value, result => RunUiAction(() =>
        {
            if (!IsCurrentWorldEditCurrent || _worldEntitySubpage != view) return;
            CloseCurrentWorldEntitySubpage(resume: false);
            if (result is int amount) accepted(amount);
            if (_worldEntitySubpage is null && _worldEntityPage is { } page) { page.Show(); _worldEntityPageBusy?.Invoke(false); _worldEntityPageRefresh?.Invoke(); }
        }), titleMessage: title);
    }
    private void OpenCurrentWorldEntityOptions(int title, IReadOnlyList<CurrentWorldEditOption> options, int? selected,
        Action<int> accepted, string? guide = null)
    {
        if (_repository is null || !CanEditCurrentWorldEntity) return;
        var view = new CommandSelectionView();
        MountCurrentWorldEntitySubpage(view, view.Abandon, view.RequestCancel);
        view.Configure(_repository, _ui, title, [new("name", GetItemEditorCatalog().Data(title), 1416)],
            options.Select(option => new CommandSelectionRow(option.Id, [new(option.Name, SortUnavailable: true)], true)).ToArray(),
            selected is int id && options.Any(option => option.Id == id) ? [id] : [], 1,
            () => IsCurrentWorldEditCurrent && _worldEntitySubpage == view && _worldEditQuestion is null,
            result => RunUiAction(() =>
            {
                if (!IsCurrentWorldEditCurrent || _worldEntitySubpage != view) return;
                CloseCurrentWorldEntitySubpage(resume: false);
                if (result is not null) accepted(result.Single());
                if (_worldEntitySubpage is null && _worldEntityPage is { } page) { page.Show(); _worldEntityPageBusy?.Invoke(false); _worldEntityPageRefresh?.Invoke(); }
            }), CurrentListScrollSpeed, guide, requireSelection: true, singleSelection: true);
    }
    private void CurrentWorldEntityUnavailable(string? reason)
    {
        _playMessage.Text = _ui.Get("detail_unknown"); _worldEditMenu?.ApplyNotice(_playMessage.Text);
        if (_worldEntityPage is CurrentWorldCityEditView city) city.ApplyNotice(_playMessage.Text);
        else if (_worldEntityPage is CurrentWorldAreaEditView area) area.ApplyNotice(_playMessage.Text);
        else if (_worldEntityPage is CurrentWorldForceEditView force) force.ApplyNotice(_playMessage.Text);
        if (reason is not null) GD.PushWarning(reason);
    }
    private void CloseCurrentWorldEntitySelection(bool resumeParent = true)
    {
        CloseCurrentWorldEntityPage();
        var selector = _worldEntitySelector; _worldEntitySelector = null;
        if (selector is not null) { selector.Abandon(); selector.QueueFree(); }
        if (resumeParent) ResumeCurrentWorldEditMenu();
    }
    private bool CancelCurrentWorldEntityChild()
    {
        if (_worldEntitySubpageBack is { } childBack) { childBack(); return true; }
        if (_worldEntityPageBack is { } pageBack) { pageBack(); return true; }
        if (_worldEntitySelector is { } selector) { selector.RequestBack(); return true; }
        return false;
    }
}
