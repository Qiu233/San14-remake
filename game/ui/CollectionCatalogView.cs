using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum CollectionCatalogKind { Poems, Scenery }

/// <summary>A retained title catalog and its source detail pages. The original
/// row identity and sorted order survive detail browsing and returning.</summary>
internal partial class CollectionCatalogView : Control
{
    private sealed record CatalogRow(int Id, IReadOnlyList<string> Cells,
        IReadOnlyDictionary<int, CollectionSortValue> SortValues);
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private CollectionCatalog catalog = null!;
    private CollectionCatalogKind kind;
    private MapInformationListView list = null!;
    private CollectionDetailView? detail;
    private PoemEventView? poemEvent;
    private PoemEventCatalog? poemEventCatalog;
    private IReadOnlyList<CatalogRow> sourceRows = [], orderedRows = [];
    private Func<bool>? admitted;
    private Action? returned;
    private Func<int> scrollSpeed = null!;
    private Action<Action> runUiAction = null!;
    private Action? poemEntered;
    private Action<bool>? poemLeft;
    private bool abandoned;
    internal bool HasPoemEvent => poemEvent is not null;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        CollectionCatalog catalog, CollectionCatalogKind kind, Func<bool> admitted,
        Action returned, Func<int> scrollSpeed, Action<Action> runUiAction,
        Action poemEntered, Action<bool> poemLeft)
    {
        this.repository = repository; this.text = text; this.catalog = catalog; this.kind = kind;
        this.admitted = admitted; this.returned = returned;
        this.scrollSpeed = scrollSpeed; this.runUiAction = runUiAction;
        this.poemEntered = poemEntered; this.poemLeft = poemLeft;
        Name = kind == CollectionCatalogKind.Poems ? "CollectionPoems" : "CollectionScenery";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop;
        sourceRows = kind == CollectionCatalogKind.Poems
            ? catalog.Poems.Select(row => new CatalogRow(row.ItemId, [row.Name, row.Author, row.Era],
                new Dictionary<int, CollectionSortValue> { [20] = row.NameSortKey })).ToArray()
            : catalog.Sceneries.Select(row => new CatalogRow(row.SceneryId, [row.Name, row.CityName, row.AreaName],
                new Dictionary<int, CollectionSortValue>
                { [0] = row.NameSortKey, [3] = row.CitySortKey, [1] = row.AreaSortKey })).ToArray();
        orderedRows = sourceRows;
        var columns = kind == CollectionCatalogKind.Poems ? catalog.PoemColumns : catalog.SceneryColumns;
        list = new(); AddChild(list);
        string basic = repository.LoadMessages(text.SourceLocale, MessageKind.Data)
            .Messages.Sections[1].Records[0x3CD].Text
            ?? throw new InvalidDataException("Collection basic tab caption is missing.");
        list.Configure(repository, text, kind == CollectionCatalogKind.Poems ? catalog.PoemsTitle : catalog.SceneryTitle,
            columns.Select(column => new MapInformationListColumn(column.Id.ToString(),
                column.Caption, column.Width, column.CanSort)).ToArray(), Rows(),
            () => ReadAdmission() && detail is null, RequestBack,
            id => runUiAction(() => ShowDetail(id)), scrollSpeed,
            Sort, tabCaptions: [basic], completeColumnProfile: true, normalColumnLayout: true);
        SetMeta("collection_mode", true);
        SetMeta("catalog_count", sourceRows.Count);
        SetMeta("campaign_required", false);
    }

    private IReadOnlyList<MapInformationListRow> Rows() => orderedRows.Select(row =>
        new MapInformationListRow(row.Id, row.Cells, true)).ToArray();

    private IReadOnlyList<MapInformationListRow> Sort(string key, bool ascending)
    {
        int column = int.Parse(key, System.Globalization.CultureInfo.InvariantCulture);
        var comparer = Comparer<CollectionSortValue>.Create((left, right) =>
        {
            if (left.UnresolvedOwner is not null || right.UnresolvedOwner is not null)
                throw new InvalidDataException("Collection column sort is unavailable.");
            if (left.Text is { } a && right.Text is { } b) return PersonNameOrder.Compare(a, b);
            if (left.Number is int x && right.Number is int y) return x.CompareTo(y);
            // The original invalid-location key is INT_MAX, after valid text.
            return left.Number is not null ? 1 : -1;
        });
        orderedRows = (ascending
            ? sourceRows.OrderBy(row => row.SortValues[column], comparer)
            : sourceRows.OrderByDescending(row => row.SortValues[column], comparer)).ToArray();
        return Rows();
    }

    private void ShowDetail(int id)
    {
        if (!ReadAdmission() || poemEvent is not null) return;
        list.SelectSourceIndex(id); list.Hide();
        var previous = detail; detail = null;
        if (previous is not null) { previous.Abandon(); previous.QueueFree(); }
        var page = new CollectionDetailView(); detail = page; AddChild(page);
        try
        {
            page.Configure(repository, text, catalog,
                kind == CollectionCatalogKind.Poems ? catalog.ReadPoem(id) : null,
                kind == CollectionCatalogKind.Scenery ? catalog.ReadScenery(id) : null,
                orderedRows.Select(row => new CollectionDetailEntry(row.Id, row.Cells[0])).ToArray(),
                () => ReadAdmission() && detail == page, CloseDetail,
                next => runUiAction(() => ShowDetail(next)), scrollSpeed,
                kind == CollectionCatalogKind.Poems ? () => runUiAction(() => ShowPoemEvent(id)) : null);
        }
        catch { CloseDetail(); throw; }
    }

    private void CloseDetail()
    {
        var page = detail; detail = null;
        if (page is not null) { page.Abandon(); page.QueueFree(); }
        if (!abandoned) list.Show();
    }

    private void ShowPoemEvent(int itemId)
    {
        if (!ReadAdmission() || kind != CollectionCatalogKind.Poems || detail is null || poemEvent is not null) return;
        var publication = (poemEventCatalog ??= PoemEventCatalog.Load(repository, text.SourceLocale)).Read(itemId);
        var view = new PoemEventView(); poemEvent = view; AddChild(view); detail.Hide();
        try
        {
            view.Configure(repository, text, publication,
                () => ReadAdmission() && poemEvent == view,
                () => runUiAction(() => ClosePoemEvent(restore: true)),
                error => runUiAction(() => throw error));
            poemEntered?.Invoke();
        }
        catch { ClosePoemEvent(restore: true); throw; }
    }

    private void ClosePoemEvent(bool restore)
    {
        var view = poemEvent; poemEvent = null;
        if (view is null) return;
        view.Abandon(); view.QueueFree();
        if (restore && !abandoned) detail?.Show();
        poemLeft?.Invoke(restore && !abandoned);
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    internal void RequestBack()
    {
        if (!ReadAdmission()) return;
        if (poemEvent is { } playback) playback.RequestBack();
        else if (detail is { } page) page.RequestBack(); else returned?.Invoke();
    }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null;
        ClosePoemEvent(restore: false); poemEntered = null; poemLeft = null; poemEventCatalog = null;
        CloseDetail(); list?.Abandon(); Hide();
    }
    public override void _ExitTree() => Abandon();
}
