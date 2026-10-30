using System.Collections.Immutable;

namespace San14.Data;

public sealed record CityInformationColumn(int Id, int CaptionMessageId, string Caption,
    int Width, int CellFormat, bool InitialAscending, bool CanSort, bool Visible);
public sealed record CityInformationGroup(int Index, int CaptionMessageId, string Caption,
    ImmutableArray<CityInformationColumn> Columns);
public sealed record CityInformationCell(string? Text, int? PresetIndex, string? UnresolvedOwner)
{
    public string? SortUnresolvedOwner { get; init; }
    public string? PresetUnresolvedOwner { get; init; }
    public AreaMarkerColor? MarkerColour { get; init; }
}
public sealed record CityInformationRow(int SourceIndex, int CityId,
    ImmutableArray<CityInformationCell> Cells);
public sealed record CityInformationListPage(int GroupIndex, ImmutableArray<CityInformationRow> Rows,
    string? UnresolvedOwner);

/// <summary>The ordinary City list's one immutable-world reader and shared
/// caller-owned order. Preparing another group only reads its uncached cells;
/// sorting consumes cached keys, never a full detail publication.</summary>
public sealed class CityInformationPageSet
{
    private readonly ImmutableArray<int> originalIds;
    private readonly Func<int, int, CityInformationCell> readCell;
    private readonly Func<int, int, string?> sortMissing;
    private readonly Func<int, int, int, bool, bool> before;
    private readonly Dictionary<int, CityInformationListPage> pages = [];
    private int[] order;
    private ImmutableArray<int> currentIds;
    public string Title { get; }
    public ImmutableArray<CityInformationGroup> Groups { get; }
    public ImmutableArray<int> CurrentCityIds => currentIds;
    public int? SelectedCityId { get; private set; }
    public bool? NameSortAscending { get; private set; }

    internal CityInformationPageSet(string title, ImmutableArray<CityInformationGroup> groups,
        IReadOnlyList<int> ids, Func<int, int, CityInformationCell> readCell,
        Func<int, int, string?> sortMissing, Func<int, int, int, bool, bool> before)
    {
        Title = title; Groups = groups; originalIds = ids.ToImmutableArray();
        this.readCell = readCell; this.sortMissing = sortMissing; this.before = before;
        order = Enumerable.Range(0, originalIds.Length).ToArray(); currentIds = originalIds;
    }

    public CityInformationListPage ReadPage(int groupIndex)
    {
        CheckGroup(groupIndex);
        if (pages.TryGetValue(groupIndex, out var saved)) return saved;
        var columns = Groups[groupIndex].Columns;
        var issues = new HashSet<string>(StringComparer.Ordinal);
        var rows = ImmutableArray.CreateBuilder<CityInformationRow>(order.Length);
        foreach (int slot in order)
        {
            var cells = ImmutableArray.CreateBuilder<CityInformationCell>(columns.Length);
            foreach (var column in columns)
            {
                var cell = readCell(originalIds[slot], column.Id);
                if (cell.UnresolvedOwner is { } missing) issues.Add(missing);
                if (cell.PresetUnresolvedOwner is { } paletteMissing) issues.Add(paletteMissing);
                cells.Add(cell);
            }
            rows.Add(new(slot, originalIds[slot], cells.MoveToImmutable()));
        }
        return pages[groupIndex] = new(groupIndex, rows.MoveToImmutable(),
            issues.Count == 0 ? null : string.Join(";", issues));
    }

    public AreaAdministrationQuery<CityInformationListPage> Sort(int groupIndex, int columnId, bool ascending)
    {
        CheckGroup(groupIndex);
        var column = Groups[groupIndex].Columns.FirstOrDefault(value => value.Id == columnId);
        if (column is not { CanSort: true })
            return AreaAdministrationQuery<CityInformationListPage>.Unknown("city-information.sort.column");
        try
        {
            foreach (int slot in order)
                if (sortMissing(originalIds[slot], columnId) is { } missing) throw new InvalidDataException(missing);
            int[] sorted = (int[])order.Clone();
            //The normal City sorter is a stable merge/insertion family.
            //All comparison values were prepared above; no query runs here.
            sorted = sorted.OrderBy(slot => slot, Comparer<int>.Create((left, right) =>
                before(originalIds[left], originalIds[right], columnId, ascending) ? -1 :
                before(originalIds[right], originalIds[left], columnId, ascending) ? 1 : 0)).ToArray();
            AdoptSlots(sorted);
            if (columnId == 0) NameSortAscending = ascending;
            return AreaAdministrationQuery<CityInformationListPage>.Known(ReadPage(groupIndex));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<CityInformationListPage>.Unknown(error.Message); }
    }

    /// <summary>Stable checked-column order over this same caller list.</summary>
    public void SortSelection(IReadOnlyList<int> selectedIds, bool selectedFirst)
    {
        ArgumentNullException.ThrowIfNull(selectedIds);
        var selected = selectedIds.ToHashSet();
        int[] sorted = (int[])order.Clone();
        DeploymentPersonTraitSort.Sort(sorted, (left, right) =>
        {
            bool a = selected.Contains(originalIds[left]), b = selected.Contains(originalIds[right]);
            return selectedFirst ? a && !b : !a && b;
        });
        AdoptSlots(sorted);
    }

    public void SelectSourceIndex(int sourceIndex)
    {
        if ((uint)sourceIndex >= (uint)originalIds.Length) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        SelectedCityId = originalIds[sourceIndex];
    }

    /// <summary>Adopts the child selector's actual final order and current
    /// city, preserving repeated caller identities by occurrence FIFO.</summary>
    public AreaAdministrationQuery<CityInformationListPage> AdoptDetailOrder(int groupIndex,
        IReadOnlyList<int> orderedCityIds, int selectedCityId, bool? nameSortAscending)
    {
        CheckGroup(groupIndex); ArgumentNullException.ThrowIfNull(orderedCityIds);
        if (orderedCityIds.Count != order.Length)
            return AreaAdministrationQuery<CityInformationListPage>.Unknown("city-information.return.count");
        var occurrences = new Dictionary<int, Queue<int>>();
        foreach (int slot in order)
        {
            int id = originalIds[slot];
            if (!occurrences.TryGetValue(id, out var queue)) occurrences.Add(id, queue = new());
            queue.Enqueue(slot);
        }
        var next = new int[order.Length];
        for (int index = 0; index < next.Length; index++)
        {
            if (!occurrences.TryGetValue(orderedCityIds[index], out var queue) || queue.Count == 0)
                return AreaAdministrationQuery<CityInformationListPage>.Unknown("city-information.return.caller_member");
            next[index] = queue.Dequeue();
        }
        if (!occurrences.ContainsKey(selectedCityId))
            return AreaAdministrationQuery<CityInformationListPage>.Unknown("city-information.return.selected_member");
        AdoptSlots(next); SelectedCityId = selectedCityId; NameSortAscending = nameSortAscending;
        return AreaAdministrationQuery<CityInformationListPage>.Known(ReadPage(groupIndex));
    }

    private void AdoptSlots(int[] next)
    {
        order = next; currentIds = order.Select(slot => originalIds[slot]).ToImmutableArray(); pages.Clear();
    }
    private void CheckGroup(int index)
    { if ((uint)index >= (uint)Groups.Length) throw new ArgumentOutOfRangeException(nameof(index)); }
}
