using System.Collections.Immutable;

namespace San14.Data;

public sealed record AreaInformationColumn(int Id, int CaptionMessageId, string Caption,
    int Width, int CellFormat, bool InitialAscending, bool CanSort, bool Visible);
public sealed record AreaInformationGroup(int Index, int CaptionMessageId, string Caption,
    ImmutableArray<AreaInformationColumn> Columns);
public sealed record AreaInformationCell(string? Text, int? PresetIndex, string? UnresolvedOwner)
{
    public string? SortUnresolvedOwner { get; init; }
    public string? PresetUnresolvedOwner { get; init; }
    public AreaMarkerColor? MarkerColour { get; init; }
}
public sealed record AreaInformationRow(int SourceIndex, int AreaId,
    ImmutableArray<AreaInformationCell> Cells);
public sealed record AreaInformationListPage(int GroupIndex, ImmutableArray<AreaInformationRow> Rows,
    string? UnresolvedOwner);

/// <summary>The ordinary Area list's one immutable-world reader and shared
/// caller-owned order. Preparing another group only reads its uncached cells;
/// sorting consumes cached keys, never a full detail publication.</summary>
public sealed class AreaInformationPageSet
{
    private readonly ImmutableArray<int> originalIds;
    private readonly Func<int, int, AreaInformationCell> readCell;
    private readonly Func<int, int, string?> sortMissing;
    private readonly Func<int, int, int, bool, bool> before;
    private readonly Dictionary<int, AreaInformationListPage> pages = [];
    private int[] order;
    private ImmutableArray<int> currentIds;
    public string Title { get; }
    public ImmutableArray<AreaInformationGroup> Groups { get; }
    public ImmutableArray<int> CurrentAreaIds => currentIds;
    public int? SelectedAreaId { get; private set; }
    public bool? NameSortAscending { get; private set; }

    internal AreaInformationPageSet(string title, ImmutableArray<AreaInformationGroup> groups,
        IReadOnlyList<int> ids, Func<int, int, AreaInformationCell> readCell,
        Func<int, int, string?> sortMissing, Func<int, int, int, bool, bool> before)
    {
        Title = title; Groups = groups; originalIds = ids.ToImmutableArray();
        this.readCell = readCell; this.sortMissing = sortMissing; this.before = before;
        order = Enumerable.Range(0, originalIds.Length).ToArray(); currentIds = originalIds;
    }

    public AreaInformationListPage ReadPage(int groupIndex)
    {
        CheckGroup(groupIndex);
        if (pages.TryGetValue(groupIndex, out var saved)) return saved;
        var columns = Groups[groupIndex].Columns;
        var issues = new HashSet<string>(StringComparer.Ordinal);
        var rows = ImmutableArray.CreateBuilder<AreaInformationRow>(order.Length);
        foreach (int slot in order)
        {
            var cells = ImmutableArray.CreateBuilder<AreaInformationCell>(columns.Length);
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

    public AreaAdministrationQuery<AreaInformationListPage> Sort(int groupIndex, int columnId, bool ascending)
    {
        CheckGroup(groupIndex);
        var column = Groups[groupIndex].Columns.FirstOrDefault(value => value.Id == columnId);
        if (column is not { CanSort: true })
            return AreaAdministrationQuery<AreaInformationListPage>.Unknown("area-information.sort.column");
        try
        {
            foreach (int slot in order)
                if (sortMissing(originalIds[slot], columnId) is { } missing) throw new InvalidDataException(missing);
            int[] sorted = (int[])order.Clone();
            //The normal Area sorter is a stable merge/insertion family.
            //All comparison values were prepared above; no query runs here.
            sorted = sorted.OrderBy(slot => slot, Comparer<int>.Create((left, right) =>
                before(originalIds[left], originalIds[right], columnId, ascending) ? -1 :
                before(originalIds[right], originalIds[left], columnId, ascending) ? 1 : 0)).ToArray();
            AdoptSlots(sorted);
            if (columnId == 0) NameSortAscending = ascending;
            return AreaAdministrationQuery<AreaInformationListPage>.Known(ReadPage(groupIndex));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<AreaInformationListPage>.Unknown(error.Message); }
    }

    // A real caller-owned auxiliary header sorts the same retained row order.
    // It has no descriptor column and does not select an initial row.
    internal AreaAdministrationQuery<AreaInformationListPage> SortAuxiliary(int groupIndex,
        Func<int,int,bool> before)
    {
        CheckGroup(groupIndex); ArgumentNullException.ThrowIfNull(before);
        try
        {
            int[] sorted = order.OrderBy(slot => slot, Comparer<int>.Create((left,right) =>
                before(originalIds[left],originalIds[right]) ? -1 :
                before(originalIds[right],originalIds[left]) ? 1 : 0)).ToArray();
            AdoptSlots(sorted);
            return AreaAdministrationQuery<AreaInformationListPage>.Known(ReadPage(groupIndex));
        }
        catch(InvalidDataException error)
        { return AreaAdministrationQuery<AreaInformationListPage>.Unknown(error.Message); }
    }

    public void SelectSourceIndex(int sourceIndex)
    {
        if ((uint)sourceIndex >= (uint)originalIds.Length) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        SelectedAreaId = originalIds[sourceIndex];
    }

    /// <summary>Adopts the child selector's actual final order and current
    /// area, preserving repeated caller identities by occurrence FIFO.</summary>
    public AreaAdministrationQuery<AreaInformationListPage> AdoptDetailOrder(int groupIndex,
        IReadOnlyList<int> orderedAreaIds, int selectedAreaId, bool? nameSortAscending)
    {
        CheckGroup(groupIndex); ArgumentNullException.ThrowIfNull(orderedAreaIds);
        if (orderedAreaIds.Count != order.Length)
            return AreaAdministrationQuery<AreaInformationListPage>.Unknown("area-information.return.count");
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
            if (!occurrences.TryGetValue(orderedAreaIds[index], out var queue) || queue.Count == 0)
                return AreaAdministrationQuery<AreaInformationListPage>.Unknown("area-information.return.caller_member");
            next[index] = queue.Dequeue();
        }
        if (!occurrences.ContainsKey(selectedAreaId))
            return AreaAdministrationQuery<AreaInformationListPage>.Unknown("area-information.return.selected_member");
        AdoptSlots(next); SelectedAreaId = selectedAreaId; NameSortAscending = nameSortAscending;
        return AreaAdministrationQuery<AreaInformationListPage>.Known(ReadPage(groupIndex));
    }

    private void AdoptSlots(int[] next)
    {
        order = next; currentIds = order.Select(slot => originalIds[slot]).ToImmutableArray(); pages.Clear();
    }
    private void CheckGroup(int index)
    { if ((uint)index >= (uint)Groups.Length) throw new ArgumentOutOfRangeException(nameof(index)); }
}
