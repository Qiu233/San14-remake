using Godot;
using San14.Data;

/// <summary>Source-authored information groups share one
/// prepared order. Only the current group owns a fourteen-row display pool.</summary>
internal partial class PagedInformationListView : DetailsView
{
    private sealed record GroupState(int Scroll, string? SortedColumn, bool Ascending);
    private readonly Dictionary<int, GroupState> groupStates = [];
    private readonly Dictionary<int, Dictionary<string, bool>> groupSortDirections = [];
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private IPagedInformationListContent pages = null!;
    protected MapInformationListView? list;
    private UiAssets assets = null!;
    private bool ownsResources;
    private AdministrationPageChrome.Resources? borrowedResources;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!;
    private Func<bool>? admitted;
    private Func<int>? scrollSpeed;
    private Action<int>? inspect;
    private Action? refreshCurrent;
    private string groupMetadata = "";
    private int group;
    private bool abandoned;
    private bool sourcePersonCells;
    private float? rowExtentWidth;
    internal int CurrentGroup => group;
    internal bool? NameSortAscending => list?.NameSortAscending;

    protected void ConfigureList(GameResourceRepository repository, PreviewText text,
        IPagedInformationListContent pages, Func<bool> admitted, Action close,
        Action<int> inspect, Func<int> scrollSpeed, string viewName, string groupMetadata,
        AdministrationPageChrome.Resources? resources = null,
        bool sourcePersonCellBounds = false, float? rowExtentWidth = null)
    {
        this.repository = repository; this.text = text; this.pages = pages;
        this.admitted = admitted; this.inspect = inspect; this.scrollSpeed = scrollSpeed;
        this.groupMetadata = groupMetadata;
        sourcePersonCells = sourcePersonCellBounds; this.rowExtentWidth = rowExtentWidth;
        borrowedResources=resources;ownsResources=resources is null;
        assets=resources?.Assets??new(repository);
        font=resources?.Font??new(repository,text.SourceLocale);
        shader=resources?.SpriteShader??UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceHeaderCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(canvas);
        var header = new UiSourceHeader(); canvas.AddChild(header);
        header.Configure(repository, assets, shader, font, pages.Title);
        ConfigureSourcePage(viewName, header, close);
        if (pages.NameSortAscending is bool ascending) groupStates[0] = new(0, "name", ascending);
        ShowGroup(0);
        Resized += Fit; Fit();
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    private static string Key(int id) => id == 0 ? "name" : id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private void ShowGroup(int index)
    {
        if (abandoned || (uint)index >= (uint)pages.Groups.Count) return;
        if (list is { } previous)
        {
            var state = groupStates.GetValueOrDefault(group) ?? new(0, null, true);
            groupStates[group] = state with { Scroll = previous.ScrollValue };
            previous.Abandon(); RemoveChild(previous); previous.QueueFree();
        }
        group = index;
        var current = pages.ReadPage(index);
        var descriptor = pages.Groups[index];
        var columns = descriptor.Columns.Where(column => column.Visible).ToArray();
        var columnMap = descriptor.Columns.Select((column, slot) => (column.Id, slot)).ToDictionary(value => value.Id, value => value.slot);
        var restored = groupStates.GetValueOrDefault(index) ?? new(0, null, true);
        if (!groupSortDirections.TryGetValue(index, out var directions))
            groupSortDirections.Add(index, directions = []);
        var next = new MapInformationListView(); list = next; AddChild(next); MoveChild(next, canvas.GetIndex());
        next.Configure(repository, text, pages.Title,
            columns.Select(column => new MapInformationListColumn(Key(column.Id), column.Caption,
                column.Width, column.CanSort, column.InitialAscending)).ToArray(), Rows(current),
            () => ReadAdmission() && list == next, RequestClose, sourceIndex =>
            {
                if (!ReadAdmission()) return;
                pages.SelectSourceIndex(sourceIndex);
                inspect?.Invoke(current.Rows.First(row => row.SourceIndex == sourceIndex).Identity);
            }, scrollSpeed!, (key, ascending) =>
            {
                int id = key == "name" ? 0 : int.Parse(key, System.Globalization.CultureInfo.InvariantCulture);
                var result = pages.Sort(index, id, ascending);
                if (!result.Available || result.Value is not { } sorted)
                { GD.PushWarning(result.UnresolvedInput ?? "information-list.sort"); return null; }
                current = sorted;
                groupStates[index] = new(next.ScrollValue, key, ascending);
                return Rows(current);
            }, current.UnresolvedOwner is null ? null : text.Get("information_partial"),
            pages.Groups.Select(value => value.Caption).ToArray(), index, ShowGroup,
            includeHeader: false, completeColumnProfile: true, normalColumnLayout: true,
            initialSortColumn: restored.SortedColumn, initialSortAscending: restored.Ascending,
            retainSortDirections: true, retainedSortDirections: directions, resources: borrowedResources,
            sourcePersonCellBounds: sourcePersonCells, rowExtentWidth: rowExtentWidth);
        if (pages.SelectedIdentity is int selected)
        {
            int slot = current.Rows.First(row => row.Identity == selected).SourceIndex;
            next.ApplyDetailReturn(Rows(current), slot, null);
        }
        next.RestoreScroll(restored.Scroll);
        ConfigureListExtras(next);
        SetMeta(groupMetadata, index);
        refreshCurrent = () =>
        {
            current = pages.ReadPage(index);
            int selected = current.Rows.First(row => row.Identity == pages.SelectedIdentity).SourceIndex;
            next.ApplyDetailReturn(Rows(current), selected, pages.NameSortAscending);
        };

        IReadOnlyList<MapInformationListRow> Rows(PagedInformationPage page) => page.Rows.Select(row =>
        {
            var cells = columns.Select(column => row.Cells[columnMap[column.Id]]).ToArray();
            return new MapInformationListRow(row.SourceIndex,
                cells.Select(cell => cell.UnresolvedOwner is null ? cell.Text ?? "" : text.Get("detail_unknown")).ToArray(),
                true, cells.Select((cell, slot) => new MapInformationListCellStyle(columns[slot].CellFormat,
                    cell.PresetIndex, cell.FirstGlyphColour, cell.IconSpriteId, cell.StatePresets)).ToArray());
        }).ToArray();
    }

    protected virtual void ConfigureListExtras(MapInformationListView currentList) { }

    protected void ApplySelectionReturn(IReadOnlyList<int> orderedIdentities, int selectedIdentity, bool? nameSortAscending)
    {
        var result = pages.AdoptDetailOrder(group, orderedIdentities, selectedIdentity, nameSortAscending);
        if (!result.Available) { GD.PushWarning(result.UnresolvedInput!); return; }
        if (nameSortAscending is bool ascending)
        {
            var previous = groupStates.GetValueOrDefault(group) ?? new(0, null, true);
            groupStates[group] = previous with { SortedColumn = "name", Ascending = ascending };
        }
        // The child owns row order and selection. The parent retains its own
        // scroll instead of assuming the child's independent scroll position.
        refreshCurrent?.Invoke();
    }

    protected void AdoptContent(IPagedInformationListContent replacement)
    {
        if (pages.SelectedIdentity is int selected)
        {
            var result = replacement.AdoptDetailOrder(group, pages.CurrentIdentities, selected, NameSortAscending);
            if (!result.Available) throw new InvalidDataException(result.UnresolvedInput);
        }
        pages = replacement; refreshCurrent?.Invoke();
    }

    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }

    public override void _ExitTree()
    {
        abandoned = true; admitted = null; scrollSpeed = null; inspect = null; refreshCurrent = null;
        list?.Abandon(); list = null; base._ExitTree();
        if(ownsResources){font?.Dispose();assets?.Dispose();shader?.Dispose();}
        borrowedResources=null;
    }
}
