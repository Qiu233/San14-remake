using Godot;
using San14.Data;

/// <summary>The title's resource-backed item catalog. Its tab, order and
/// selected row survive inspecting an item without creating a campaign.</summary>
internal partial class CollectionItemsView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private CollectionItemCatalog catalog = null!;
    private MapInformationListView? list;
    private CollectionItemDetailView? detail;
    private IReadOnlyList<CollectionItemRow> sourceRows = [], orderedRows = [];
    private Func<bool>? admitted;
    private Action? returned;
    private Func<int> scrollSpeed = null!;
    private Action<Action> runUiAction = null!;
    private int tabIndex;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        CollectionItemCatalog catalog, Func<bool> admitted, Action returned,
        Func<int> scrollSpeed, Action<Action> runUiAction,
        Action poemEntered, Action<bool> poemLeft, Func<Task> poemAppearanceSound)
    {
        this.repository = repository; this.text = text; this.catalog = catalog;
        this.admitted = admitted; this.returned = returned;
        this.scrollSpeed = scrollSpeed; this.runUiAction = runUiAction;
        this.poemEntered = poemEntered; this.poemLeft = poemLeft; this.poemAppearanceSound = poemAppearanceSound;
        Name = "CollectionItems"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ShowTab(0);
        SetMeta("collection_mode", true); SetMeta("campaign_required", false);
    }

    private void ShowTab(int index)
    {
        if ((uint)index >= catalog.Tabs.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (list is { } previous) { previous.Abandon(); previous.QueueFree(); }
        tabIndex = index;
        var tab = catalog.Tabs[index]; sourceRows = catalog.ReadRows(tab.Id); orderedRows = sourceRows;
        var page = new MapInformationListView(); list = page; AddChild(page);
        page.Configure(repository, text, catalog.Title,
            tab.Columns.Select(column => new MapInformationListColumn(column.Id.ToString(),
                column.Caption, column.Width, column.CanSort)).ToArray(), Rows(),
            () => ReadAdmission() && detail is null && list == page, RequestBack,
            id => runUiAction(() => ShowDetail(id)), scrollSpeed, Sort,
            tabCaptions: catalog.Tabs.Select(value => value.Caption).ToArray(), selectedTab: index,
            selectTab: next => runUiAction(() => { if (next != tabIndex && ReadAdmission() && detail is null) ShowTab(next); }),
            completeColumnProfile: true, normalColumnLayout: true);
        SetMeta("catalog_count", sourceRows.Count); SetMeta("catalog_tab", tab.Id);
    }

    private IReadOnlyList<MapInformationListRow> Rows()
    {
        var columns = catalog.Tabs[tabIndex].Columns;
        return orderedRows.Select(row => new MapInformationListRow(row.ItemId,
            columns.Select(column => row.Cells[column.Id].Text ?? text.Get("detail_unknown")).ToArray(), true)).ToArray();
    }

    private IReadOnlyList<MapInformationListRow> Sort(string key, bool ascending)
    {
        int column = int.Parse(key, System.Globalization.CultureInfo.InvariantCulture);
        var comparer = Comparer<CollectionSortValue>.Create((left, right) =>
        {
            if (left.UnresolvedOwner is not null || right.UnresolvedOwner is not null)
                throw new InvalidDataException("Collection item column sort is unavailable.");
            if (left.Text is { } a && right.Text is { } b) return PersonNameOrder.Compare(a, b);
            if (left.Number is int x && right.Number is int y) return x.CompareTo(y);
            return left.Number is not null ? 1 : right.Number is not null ? -1 : 0;
        });
        orderedRows = (ascending
            ? sourceRows.OrderBy(row => row.Cells[column].SortValue, comparer)
            : sourceRows.OrderByDescending(row => row.Cells[column].SortValue, comparer)).ToArray();
        return Rows();
    }

    private void ShowDetail(int itemId)
    {
        if (!ReadAdmission() || poemDetail is not null) return;
        list!.SelectSourceIndex(itemId); list.Hide();
        if (detail is { } previous) { previous.Abandon(); previous.QueueFree(); }
        var page = new CollectionItemDetailView(); detail = page; AddChild(page);
        try
        {
            page.Configure(repository, text, catalog, catalog.ReadItem(itemId),
                orderedRows.Select(row => new CollectionDetailEntry(row.ItemId, catalog.ReadItem(row.ItemId).Name)).ToArray(),
                () => ReadAdmission() && detail == page, CloseDetail,
                next => runUiAction(() => ShowDetail(next)), scrollSpeed,
                () => runUiAction(() => ShowPoemDetail(itemId)));
        }
        catch { CloseDetail(); throw; }
    }

    private void CloseDetail()
    {
        var page = detail; detail = null;
        if (page is not null) { page.Abandon(); page.QueueFree(); }
        if (!abandoned) list?.Show();
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void RequestBack()
    {
        if (!ReadAdmission()) return;
        if (poemEvent is { } playback) playback.RequestBack();
        else if (poemDetail is { } poem) poem.RequestBack();
        else if (detail is { } page) page.RequestBack(); else returned?.Invoke();
    }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null;
        ClosePoemDetail(); poemEntered = null; poemLeft = null; poemAppearanceSound = null;
        poemCatalog = null; poemEventCatalog = null;
        CloseDetail(); list?.Abandon(); Hide();
    }
    public override void _ExitTree() => Abandon();
}
