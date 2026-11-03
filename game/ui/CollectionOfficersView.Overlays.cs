using Godot;
using San14.Data;

internal partial class CollectionOfficersView
{
    private readonly bool[] enabledGroups = [true, true, true];
    private CollectionOfficerFilterView? filter;
    private CollectionOfficerExpansionView? expansion;

    private IReadOnlyList<CollectionOfficerRow> FilterRows(IReadOnlyList<CollectionOfficerRow> rows) =>
        rows.Where(row => enabledGroups[row.GroupIndex]).ToArray();

    private void OpenFilter(int officerId)
    {
        if (filter is not null || expansion is not null || detail is null) return;
        int selectedPage = detail.SelectedPage;
        var view = new CollectionOfficerFilterView(); filter = view; AddChild(view); detail.Hide();
        try
        {
            view.Configure(repository, text, enabledGroups,
                () => ReadAdmission() && filter == view,
                () => runUiAction(() => CloseFilter(true)),
                groups => runUiAction(() =>
                {
                    if (!ReadAdmission() || filter != view) return;
                    for (int index = 0; index < enabledGroups.Length; index++) enabledGroups[index] = groups[index];
                    CloseFilter(false);
                    var previous = detail; detail = null;
                    if (previous is not null) { previous.Abandon(); previous.QueueFree(); }
                    ShowTab(tabIndex);
                    if (orderedRows.Count == 0) { appearanceVariants.Clear(); previousVoiceEvent = 0; return; }
                    bool retained = orderedRows.Any(row => row.OfficerId == officerId);
                    ShowDetail(retained ? officerId : orderedRows[0].OfficerId, retained ? selectedPage : 0);
                }), resources);
        }
        catch { CloseFilter(true); throw; }
    }

    private void OpenExpansion(int officerId)
    {
        if (filter is not null || expansion is not null || detail is null) return;
        var view = new CollectionOfficerExpansionView(); expansion = view; AddChild(view); detail.Hide();
        try
        {
            view.Configure(repository, text, catalog, officerId, appearanceVariants.GetValueOrDefault(officerId),
                () => ReadAdmission() && expansion == view,
                () => runUiAction(() => CloseExpansion(true)), resources);
        }
        catch { CloseExpansion(true); throw; }
    }

    private bool RequestOverlayBack()
    {
        if (filter is { } filtering) { filtering.RequestBack(); return true; }
        if (expansion is { } enlarging) { enlarging.RequestBack(); return true; }
        return false;
    }

    private void CloseFilter(bool restore)
    {
        var view = filter; filter = null;
        if (view is null) return;
        view.Abandon(); view.QueueFree();
        if (restore && !abandoned) detail?.Show();
    }

    private void CloseExpansion(bool restore)
    {
        var view = expansion; expansion = null;
        if (view is null) return;
        view.Abandon(); view.QueueFree();
        if (restore && !abandoned) detail?.Show();
    }

    private void CloseOverlays() { CloseFilter(false); CloseExpansion(false); }
}
