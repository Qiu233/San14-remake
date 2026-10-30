using System.Collections.Immutable;
using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private CurrentWorldItemSelectionView? _worldItemSelector;
    private CurrentWorldItemEditView? _worldItemPage;
    private ItemEditorCustodyView? _worldItemCustody;
    private Control? _worldItemField;
    private Action? _worldItemFieldBack;
    private IReadOnlyList<CurrentWorldItemEditRow> _worldItemRows = [];
    private CurrentWorldItemEditDraft? _worldItemOriginal, _worldItemDraft;
    private CurrentWorldItemEditHolder? _worldItemHolder;
    private bool _worldItemsBatch;

    private bool CanEditCurrentWorldItem => IsCurrentWorldEditCurrent && _worldItemPage is not null &&
        _worldItemField is null && _worldItemCustody is null && _worldEditQuestion is null;

    private void OpenCurrentWorldItems(bool batch)
    {
        if (_repository is null || !IsCurrentWorldEditCurrent || _worldItemSelector is not null) return;
        _worldItemsBatch = batch;
        var query = CurrentWorldEditRules.QueryCurrentWorldItemEditSelection(_worldEditWorld!,batch);
        if (!query.Available || query.Value is not { } publication)
        { CurrentWorldItemUnavailable(query.UnresolvedInput); return; }
        _worldEditMenu!.SetBusy(true); _worldEditMenu.Hide();
        BuildCurrentWorldItemSelector(null, [],publication);
    }

    private void BuildCurrentWorldItemSelector(CurrentWorldItemSelectionState? retained,IReadOnlyList<int> selected,
        CurrentWorldItemEditSelection? prepared=null)
    {
        if (_repository is null || !IsCurrentWorldEditCurrent) return;
        if(prepared is null)
        {
            var query=CurrentWorldEditRules.QueryCurrentWorldItemEditSelection(_worldEditWorld!,_worldItemsBatch,retained?.Order);
            if(!query.Available||query.Value is not {} current)
            {CurrentWorldItemUnavailable(query.UnresolvedInput);CloseCurrentWorldItems();return;}
            prepared=current;
        }
        _worldItemRows=prepared.Rows;
        var view=new CurrentWorldItemSelectionView();_worldItemSelector=view;AddChild(view);
        try
        {
            view.Configure(_repository,_ui,prepared,selected,
                ()=>IsCurrentWorldEditCurrent&&_worldItemSelector==view&&_worldItemPage is null&&
                    _worldItemCustody is null&&_worldEditQuestion is null,
                result=>RunUiAction(()=>
                {
                    if(!IsCurrentWorldEditCurrent||_worldItemSelector!=view)return;
                    if(result is null)CloseCurrentWorldItems();
                    else if(_worldItemsBatch)OpenCurrentWorldItemBatch(result);
                    else OpenCurrentWorldItem(result.Single());
                }),CurrentListScrollSpeed,retained);
        }
        catch{CloseCurrentWorldItems();throw;}
    }

    private void OpenCurrentWorldItem(int id)
    {
        if (_repository is null || !IsCurrentWorldEditCurrent || _worldItemPage is not null) return;
        var row = _worldItemRows.Single(item => item.ItemId == id);
        if (row.IsUnavailable) return;
        _worldItemOriginal = _worldItemDraft = row.CreateDraft();
        if (!CaptureCurrentWorldItemHolder()) { _worldItemSelector?.Show(); return; }
        var view = new CurrentWorldItemEditView(); _worldItemPage = view; AddChild(view);
        try
        {
            view.Configure(_repository, _ui, GetItemEditorCatalog(), 30, UiSourceEditSlider.FunctionIcons,
                () => new(row.Name, _worldItemDraft!.Values, _worldItemHolder!.Name, _worldItemHolder.IsValid,
                    row.CanEditCustody, _worldItemDraft.TransferProhibited, _worldItemDraft != _worldItemOriginal,
                    new AdministrationMessages(_repository, _ui.SourceLocale).Format(0x5F1C), _worldItemHolder.Portrait, _worldItemHolder.ForceFlag),
                () => IsCurrentWorldEditCurrent && _worldItemPage == view && _worldItemField is null &&
                    _worldItemCustody is null && _worldEditQuestion is null,
                () => RunUiAction(RequestLeaveCurrentWorldItem), () => RunUiAction(SubmitCurrentWorldItem),
                section => RunUiAction(() => EditCurrentWorldItemField(section)),
                worth => { if (CanEditCurrentWorldItem) { _worldItemDraft = _worldItemDraft! with
                    { Values = _worldItemDraft!.Values with { Worth = worth } }; view.Refresh(); } },
                flag => { if (CanEditCurrentWorldItem) { _worldItemDraft = _worldItemDraft! with { TransferProhibited = flag }; view.Refresh(); } });
        }
        catch { RetireCurrentWorldItemPage(); _worldItemSelector?.Show(); throw; }
    }

    private bool CaptureCurrentWorldItemHolder()
    {
        var query = CurrentWorldEditRules.QueryCurrentWorldItemEditHolder(_worldEditWorld!, _worldItemDraft!.HolderOfficerId);
        if (query.Available && query.Value is { } holder) { _worldItemHolder = holder; return true; }
        CurrentWorldItemUnavailable(query.UnresolvedInput); return false;
    }

    private void EditCurrentWorldItemField(int section)
    {
        if (!CanEditCurrentWorldItem || _repository is null || _worldItemDraft is not { } before) return;
        if (section == 0) { OpenCurrentWorldItemHolder(); return; }
        if (section == 1)
        {
            _worldItemDraft = before with { HolderOfficerId = null, TransferProhibited = false };
            if (!CaptureCurrentWorldItemHolder()) _worldItemDraft = before;
            _worldItemPage!.Refresh(); return;
        }
        var page = _worldItemPage!; page.SetBusy(true); page.Hide();
        bool Admitted() => IsCurrentWorldEditCurrent && _worldItemPage == page && _worldEditQuestion is null;
        void Finish(Control child, ItemEditorValues? values)
        {
            if (!Admitted() || _worldItemField != child) return;
            _worldItemField = null; _worldItemFieldBack = null; RetireItemControl(child);
            if (values is not null) _worldItemDraft = before with { Values = values };
            page.Show(); page.SetBusy(false);
        }
        if (section is 2 or 3 or 4)
        {
            var child = new ItemEditorFieldView(); _worldItemField = child; _worldItemFieldBack = child.RequestBack; AddChild(child);
            child.Configure(_repository, _ui, GetItemEditorCatalog(), before.Values, section, Admitted,
                result => RunUiAction(() => Finish(child, result)));
        }
        else if (section == 5)
        {
            var child = new DeploymentCalculatorDialog(); _worldItemField = child; _worldItemFieldBack = child.RequestCancel; AddChild(child);
            child.Configure(_repository, _ui, before.Values.Worth, 0, 30,
                result => RunUiAction(() => Finish(child, result is int worth ? before.Values with { Worth = worth } : null)), titleMessage: 0x423);
        }
    }

    private void OpenCurrentWorldItemHolder()
    {
        if (_repository is null || !CanEditCurrentWorldItem || _worldItemDraft is not { } before) return;
        var query = CurrentWorldEditRules.QueryCurrentWorldItemEditOfficers(_worldEditWorld!);
        if (!query.Available || query.Value is not { } officers) { CurrentWorldItemUnavailable(query.UnresolvedInput); return; }
        var view = new ItemEditorCustodyView(); _worldItemCustody = view; AddChild(view);
        _worldItemPage!.SetBusy(true); _worldItemPage.Hide();
        view.Configure(_repository, _ui, GetItemEditorCatalog(), officers,
            new(before.HolderOfficerId, before.TransferProhibited), before.ItemId,
            () => IsCurrentWorldEditCurrent && _worldItemCustody == view && _worldEditQuestion is null,
            result => RunUiAction(() =>
            {
                if (!IsCurrentWorldEditCurrent || _worldItemCustody != view) return;
                _worldItemCustody = null; view.Abandon(); view.QueueFree();
                if (result is not null)
                {
                    _worldItemDraft = before with { HolderOfficerId = result.OfficerId, TransferProhibited = result.Flag8 };
                    if (!CaptureCurrentWorldItemHolder()) _worldItemDraft = before;
                }
                _worldItemPage!.Show(); _worldItemPage.SetBusy(false);
            }), CurrentListScrollSpeed, readOfficerSelection:()=>CurrentWorldEditRules.QueryCurrentWorldItemHolderSelection(_worldEditWorld!));
    }

    private void RequestLeaveCurrentWorldItem()
    {
        if (!CanEditCurrentWorldItem) return;
        if (_worldItemDraft == _worldItemOriginal) ReturnToCurrentWorldItemSelector(clearSelection: false);
        else AskCurrentWorldEditQuestion(0x28BD, discard =>
            { if (discard) ReturnToCurrentWorldItemSelector(clearSelection: false); });
    }

    private void SubmitCurrentWorldItem()
    {
        if (!CanEditCurrentWorldItem || _worldItemDraft is not { } draft || _worldItemDraft == _worldItemOriginal) return;
        var original = _worldItemRows.Single(row => row.ItemId == draft.ItemId);
        bool warning = !draft.HolderOfficerId.HasValue && original.Lifecycle.Status!.Code != 2;
        if (warning) AskCurrentWorldEditQuestion(0x5F41, confirmed => { if (confirmed) Apply(true); });
        else Apply(false);
        void Apply(bool confirmed)
        {
            if (!IsCurrentWorldEditCurrent || _worldItemDraft != draft) return;
            _worldItemPage!.SetBusy(true);
            if (!SubmitCurrentWorldEdit(new EditCurrentWorldItemOrder(_worldEditSession!.PlayerForceId!.Value, draft, confirmed),
                () => ReturnToCurrentWorldItemSelector(clearSelection: false)))
            { _worldItemPage?.SetBusy(false); _worldItemPage?.ApplyNotice(_playMessage.Text); }
        }
    }

    private void OpenCurrentWorldItemBatch(IReadOnlyList<int> selected)
    {
        if (_repository is null || !IsCurrentWorldEditCurrent) return;
        var rules = CurrentWorldEditRules;
        var query = rules.QueryCurrentWorldItemBatchEditDraft(_worldEditWorld!, selected);
        var people = rules.QueryCurrentWorldItemEditOfficers(_worldEditWorld!);
        if (!query.Available || query.Value is not { } original || !people.Available || people.Value is not { } officers)
        { CurrentWorldItemUnavailable(query.UnresolvedInput ?? people.UnresolvedInput); _worldItemSelector?.Show(); return; }
        var view = new ItemEditorCustodyView(); _worldItemCustody = view; AddChild(view);
        view.Configure(_repository, _ui, GetItemEditorCatalog(), officers,
            new(original.HolderOfficerId, original.TransferProhibited), selected[0],
            () => IsCurrentWorldEditCurrent && _worldItemCustody == view && _worldEditQuestion is null,
            result => RunUiAction(() =>
            {
                if (!IsCurrentWorldEditCurrent || _worldItemCustody != view) return;
                if (result is null)
                { _worldItemCustody = null; view.Abandon(); view.QueueFree(); _worldItemSelector?.Show(); return; }
                bool warning = result.OfficerId is null && original.RequiresRemovalConfirmation;
                if (warning) AskCurrentWorldEditQuestion(0x5F41, confirmed => { if (confirmed) Apply(true); else view.Show(); });
                else Apply(false);
                void Apply(bool confirmed)
                {
                    if (!IsCurrentWorldEditCurrent || _worldItemCustody != view) return;
                    if (!SubmitCurrentWorldEdit(new EditCurrentWorldItemsOrder(_worldEditSession!.PlayerForceId!.Value,
                        selected.ToImmutableArray(), result.OfficerId, result.Flag8, confirmed),
                        () => ReturnToCurrentWorldItemSelector(clearSelection: true)))
                    { view.Show(); view.ApplyNotice(_playMessage.Text); }
                }
            }), CurrentListScrollSpeed, retainOnAccept: true,
                readOfficerSelection:()=>CurrentWorldEditRules.QueryCurrentWorldItemHolderSelection(_worldEditWorld!));
    }

    private void ReturnToCurrentWorldItemSelector(bool clearSelection)
    {
        if (!IsCurrentWorldEditCurrent || _worldItemSelector is not { } previous) return;
        var state = previous.CapturePageState(); IReadOnlyList<int> selected = clearSelection ? [] : previous.SelectedIds;
        RetireCurrentWorldItemPage();
        if (_worldItemCustody is { } custody) { _worldItemCustody = null; custody.Abandon(); custody.QueueFree(); }
        _worldItemSelector = null; previous.Abandon(); previous.QueueFree();
        BuildCurrentWorldItemSelector(state, selected);
    }
    private void CurrentWorldItemUnavailable(string? reason)
    {
        _playMessage.Text = _ui.Get("detail_unknown");
        _worldItemPage?.ApplyNotice(_playMessage.Text); _worldItemSelector?.ApplyNotice(_playMessage.Text);
        _worldEditMenu?.ApplyNotice(_playMessage.Text);
        if (reason is not null) GD.PushWarning(reason);
    }
    private void RetireCurrentWorldItemPage()
    {
        if (_worldItemField is { } child) { _worldItemField = null; _worldItemFieldBack = null; RetireItemControl(child); }
        var page = _worldItemPage; _worldItemPage = null;
        if (page is not null) { page.Abandon(); page.QueueFree(); }
        _worldItemOriginal = _worldItemDraft = null; _worldItemHolder = null;
    }
    private void CloseCurrentWorldItems(bool resumeParent = true)
    {
        RetireCurrentWorldItemPage();
        if (_worldItemCustody is { } custody) { _worldItemCustody = null; custody.Abandon(); custody.QueueFree(); }
        var selector = _worldItemSelector; _worldItemSelector = null;
        if (selector is not null) { selector.Abandon(); selector.QueueFree(); }
        _worldItemRows = [];
        if (resumeParent) ResumeCurrentWorldEditMenu();
    }
    private bool CancelCurrentWorldItemChild()
    {
        if (_worldItemFieldBack is { } back) { back(); return true; }
        if (_worldItemCustody is { } custody) { custody.RequestBack(); return true; }
        if (_worldItemPage is { } page) { page.RequestBack(); return true; }
        if (_worldItemSelector is { } selector) { selector.RequestCancel(); return true; }
        return false;
    }
}
