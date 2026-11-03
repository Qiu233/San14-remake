using Godot;
using San14.Data;

/// <summary>The title's original person encyclopedia and its retained list.
/// All tabs consume one resource publication without creating a campaign.</summary>
internal partial class CollectionOfficersView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private CollectionOfficerCatalog catalog = null!;
    private MapInformationListView? list;
    private CollectionOfficerDetailView? detail;
    private IReadOnlyList<CollectionOfficerRow> sourceRows = [], orderedRows = [];
    private Func<bool>? admitted;
    private Action? returned;
    private Func<int> scrollSpeed = null!;
    private Action<Action> runUiAction = null!;
    private Func<CollectionOfficerDetail, int, int>? playVoice;
    private int previousVoiceEvent;
    private readonly Dictionary<int, int> appearanceVariants = [];
    private int tabIndex;
    private readonly Dictionary<int, IReadOnlyList<CollectionOfficerRow>> retainedRows = [];
    private readonly Dictionary<(int Tab, int Column), bool> nextSortDirections = [];
    private readonly Dictionary<int, (int Column, bool Ascending)> activeSorts = [];
    private bool abandoned;
    private AdministrationPageChrome.Resources? resources;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        CollectionOfficerCatalog catalog, Func<bool> admitted, Action returned,
        Func<int> scrollSpeed, Action<Action> runUiAction, Func<CollectionOfficerDetail, int, int> playVoice,
        AdministrationPageChrome.Resources? resources = null)
    {
        this.repository = repository; this.text = text; this.catalog = catalog;
        this.admitted = admitted; this.returned = returned; this.scrollSpeed = scrollSpeed;
        this.runUiAction = runUiAction; this.playVoice = playVoice; this.resources = resources;
        Name = "CollectionOfficers"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ShowTab(0);
        SetMeta("collection_mode", true); SetMeta("campaign_required", false);
    }

    private IReadOnlyList<CollectionColumn> VisibleColumns =>
        catalog.Tabs[tabIndex].Columns.Where(column => column.Width > 0).ToArray();

    private void ShowTab(int index)
    {
        if ((uint)index >= catalog.Tabs.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (list is { } previous) { previous.Abandon(); previous.QueueFree(); }
        tabIndex = index;
        var tab = catalog.Tabs[index];
        sourceRows = retainedRows.TryGetValue(index, out var retained) ? retained : catalog.ReadRows(tab.Id);
        orderedRows = FilterRows(sourceRows);
        bool hasSort = activeSorts.TryGetValue(index, out var active);
        var page = new MapInformationListView(); list = page; AddChild(page);
        page.Configure(repository, text, catalog.Title,
            VisibleColumns.Select(column => new MapInformationListColumn(column.Id.ToString(),
                column.Caption, column.Width, column.CanSort,
                nextSortDirections.GetValueOrDefault((index, column.Id), tab.InitialSortDirections.GetValueOrDefault(column.Id, true)))).ToArray(), Rows(),
            () => ReadAdmission() && detail is null && list == page, RequestBack,
            id => runUiAction(() => ShowDetail(id)), scrollSpeed, Sort,
            tabCaptions: catalog.Tabs.Select(value => value.Caption).ToArray(), selectedTab: index,
            selectTab: next => runUiAction(() => { if (next != tabIndex && ReadAdmission() && detail is null) ShowTab(next); }),
            completeColumnProfile: true, normalColumnLayout: true,
            initialSortColumn: hasSort ? active.Column.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
            initialSortAscending: hasSort ? active.Ascending : true, retainSortDirections: true, resources: resources);
        SetMeta("catalog_count", orderedRows.Count); SetMeta("catalog_tab", tab.Id);
    }

    private IReadOnlyList<MapInformationListRow> Rows()
    {
        var columns = VisibleColumns;
        return orderedRows.Select(row => new MapInformationListRow(row.OfficerId,
            columns.Select(column => row.Cells[column.Id].Text ?? text.Get("detail_unknown")).ToArray(), true)).ToArray();
    }

    private IReadOnlyList<MapInformationListRow> Sort(string key, bool ascending)
    {
        int column = int.Parse(key, System.Globalization.CultureInfo.InvariantCulture);
        sourceRows = catalog.SortRows(sourceRows, column, ascending);
        retainedRows[tabIndex] = sourceRows;
        orderedRows = FilterRows(sourceRows);
        nextSortDirections[(tabIndex, column)] = !ascending;
        activeSorts[tabIndex] = (column, ascending);
        return Rows();
    }

    private void ShowDetail(int officerId, int pageIndex = 0)
    {
        if (!ReadAdmission()) return;
        list!.SelectSourceIndex(officerId); list.Hide();
        if (detail is { } previous) { previous.Abandon(); previous.QueueFree(); }
        var publication = catalog.ReadOfficer(officerId, appearanceVariants.GetValueOrDefault(officerId));
        var view = new CollectionOfficerDetailView(); detail = view; AddChild(view);
        try
        {
            view.Configure(repository, text, catalog, publication,
                orderedRows.Select(row => new CollectionDetailEntry(row.OfficerId,
                    row.Cells.TryGetValue(182, out var name) ? name.Text ?? text.Get("detail_unknown") :
                    row.Cells[0].Text ?? text.Get("detail_unknown"))).ToArray(),
                () => ReadAdmission() && detail == view, CloseDetail,
                next => runUiAction(() => ShowDetail(next)), scrollSpeed,
                action => runUiAction(() => SelectDetailAction(officerId, action)), pageIndex, resources);
        }
        catch { CloseDetail(); throw; }
    }

    private void SelectDetailAction(int officerId, int action)
    {
        if (!ReadAdmission() || detail is null) return;
        switch (action)
        {
            case 0: OpenFilter(officerId); break;
            case 1: OpenExpansion(officerId); break;
            case 2:
                if (playVoice is { } preview)
                    previousVoiceEvent = preview(catalog.ReadOfficer(officerId,
                        appearanceVariants.GetValueOrDefault(officerId)), previousVoiceEvent);
                break;
            case 3:
                int count = catalog.GetVariantCount(officerId);
                if (count < 1) throw new InvalidDataException("Collection officer portrait variants are unavailable.");
                appearanceVariants[officerId] = (appearanceVariants.GetValueOrDefault(officerId) + 1) % count;
                ShowDetail(officerId, detail.SelectedPage); break;
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private void CloseDetail()
    {
        var page = detail; detail = null;
        if (page is not null) { page.Abandon(); page.QueueFree(); }
        appearanceVariants.Clear();
        previousVoiceEvent = 0;
        if (!abandoned) list?.Show();
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void RequestBack()
    {
        if (!ReadAdmission()) return;
        if (RequestOverlayBack()) return;
        if (detail is { } page) page.RequestBack(); else returned?.Invoke();
    }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null; playVoice = null;
        CloseOverlays(); CloseDetail(); list?.Abandon(); Hide();
    }
    public override void _ExitTree() => Abandon();
}
