using System.Collections.Immutable;

namespace San14.Data;

public sealed record ArmyInformationColumn(int Id, int CaptionMessageId, string Caption,
    int Width, int CellFormat, bool InitialAscending, bool CanSort, bool Visible);
public sealed record ArmyInformationGroup(int Index, int CaptionMessageId, string Caption,
    ImmutableArray<ArmyInformationColumn> Columns);
public sealed record ArmyInformationCell(string? Text, int? PresetIndex, string? UnresolvedOwner)
{
    public string? SortUnresolvedOwner { get; init; }
    public string? PresetUnresolvedOwner { get; init; }
    public AreaMarkerColor? MarkerColour { get; init; }
}
public sealed record ArmyInformationRow(int SourceIndex, int UnitId,
    ImmutableArray<ArmyInformationCell> Cells);
public sealed record ArmyInformationPage(int GroupIndex, ImmutableArray<ArmyInformationRow> Rows,
    string? UnresolvedOwner);

/// <summary>The ordinary Army list's one immutable-world reader and shared
/// caller-owned order. Preparing another group only reads its uncached cells;
/// sorting consumes cached keys, never a full detail publication.</summary>
public sealed class ArmyInformationPageSet
{
    private readonly ImmutableArray<int> originalIds;
    private readonly Func<int, int, ArmyInformationCell> readCell;
    private readonly Func<int, int, string?> sortMissing;
    private readonly Func<int, int, int, bool, bool> before;
    private readonly Dictionary<int, ArmyInformationPage> pages = [];
    private int[] order;
    private ImmutableArray<int> currentIds;
    public string Title { get; }
    public ImmutableArray<ArmyInformationGroup> Groups { get; }
    public ImmutableArray<int> CurrentUnitIds => currentIds;
    public int? SelectedUnitId { get; private set; }
    public bool? NameSortAscending { get; private set; }

    internal ArmyInformationPageSet(string title, ImmutableArray<ArmyInformationGroup> groups,
        IReadOnlyList<int> ids, Func<int, int, ArmyInformationCell> readCell,
        Func<int, int, string?> sortMissing, Func<int, int, int, bool, bool> before)
    {
        Title = title; Groups = groups; originalIds = ids.ToImmutableArray();
        this.readCell = readCell; this.sortMissing = sortMissing; this.before = before;
        order = Enumerable.Range(0, originalIds.Length).ToArray(); currentIds = originalIds;
    }

    public ArmyInformationPage ReadPage(int groupIndex)
    {
        CheckGroup(groupIndex);
        if (pages.TryGetValue(groupIndex, out var saved)) return saved;
        var columns = Groups[groupIndex].Columns;
        var issues = new HashSet<string>(StringComparer.Ordinal);
        var rows = ImmutableArray.CreateBuilder<ArmyInformationRow>(order.Length);
        foreach (int slot in order)
        {
            var cells = ImmutableArray.CreateBuilder<ArmyInformationCell>(columns.Length);
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

    public AreaAdministrationQuery<ArmyInformationPage> Sort(int groupIndex, int columnId, bool ascending)
    {
        CheckGroup(groupIndex);
        var column = Groups[groupIndex].Columns.FirstOrDefault(value => value.Id == columnId);
        if (column is not { CanSort: true })
            return AreaAdministrationQuery<ArmyInformationPage>.Unknown("army-information.sort.column");
        try
        {
            foreach (int slot in order)
                if (sortMissing(originalIds[slot], columnId) is { } missing) throw new InvalidDataException(missing);
            int[] sorted = (int[])order.Clone();
            //The normal Army sorter is a stable merge/insertion family.
            //All comparison values were prepared above; no query runs here.
            sorted = sorted.OrderBy(slot => slot, Comparer<int>.Create((left, right) =>
                before(originalIds[left], originalIds[right], columnId, ascending) ? -1 :
                before(originalIds[right], originalIds[left], columnId, ascending) ? 1 : 0)).ToArray();
            AdoptSlots(sorted);
            if (columnId == 0) NameSortAscending = ascending;
            return AreaAdministrationQuery<ArmyInformationPage>.Known(ReadPage(groupIndex));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<ArmyInformationPage>.Unknown(error.Message); }
    }

    public void SelectSourceIndex(int sourceIndex)
    {
        if ((uint)sourceIndex >= (uint)originalIds.Length) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        SelectedUnitId = originalIds[sourceIndex];
    }

    /// <summary>Adopts the child selector's actual final order and current
    /// person, preserving repeated caller identities by occurrence FIFO.</summary>
    public AreaAdministrationQuery<ArmyInformationPage> AdoptDetailOrder(int groupIndex,
        IReadOnlyList<int> orderedUnitIds, int selectedUnitId, bool? nameSortAscending)
    {
        CheckGroup(groupIndex); ArgumentNullException.ThrowIfNull(orderedUnitIds);
        if (orderedUnitIds.Count != order.Length)
            return AreaAdministrationQuery<ArmyInformationPage>.Unknown("army-information.return.count");
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
            if (!occurrences.TryGetValue(orderedUnitIds[index], out var queue) || queue.Count == 0)
                return AreaAdministrationQuery<ArmyInformationPage>.Unknown("army-information.return.caller_member");
            next[index] = queue.Dequeue();
        }
        if (!occurrences.ContainsKey(selectedUnitId))
            return AreaAdministrationQuery<ArmyInformationPage>.Unknown("army-information.return.selected_member");
        AdoptSlots(next); SelectedUnitId = selectedUnitId; NameSortAscending = nameSortAscending;
        return AreaAdministrationQuery<ArmyInformationPage>.Known(ReadPage(groupIndex));
    }

    private void AdoptSlots(int[] next)
    {
        order = next; currentIds = order.Select(slot => originalIds[slot]).ToImmutableArray(); pages.Clear();
    }
    private void CheckGroup(int index)
    { if ((uint)index >= (uint)Groups.Length) throw new ArgumentOutOfRangeException(nameof(index)); }
}
