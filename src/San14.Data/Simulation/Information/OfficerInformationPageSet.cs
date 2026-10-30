using System.Collections.Immutable;

namespace San14.Data;

/// <summary>Original list text presets for its five visual states.</summary>
public sealed record InformationTextStatePresets(int Normal, int Hovered, int Selected, int Disabled)
{
    public int ForState(int state) => state switch
    {
        0 => Normal, 1 => Hovered, 2 => Selected, 3 or 4 => Disabled,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}

public sealed record OfficerInformationColumn(int Id, int CaptionMessageId, string Caption,
    int Width, int CellFormat, bool InitialAscending, bool CanSort, bool Visible);
public sealed record OfficerInformationGroup(int Index, int CaptionMessageId, string Caption,
    ImmutableArray<OfficerInformationColumn> Columns);
public sealed record OfficerInformationCell(string? Text, int? PresetIndex, string? UnresolvedOwner)
{
    public string? SortUnresolvedOwner { get; init; }
    public string? PresetUnresolvedOwner { get; init; }
    public AreaMarkerColor? MarkerColour { get; init; }
    public InformationTextStatePresets? StatePresets { get; init; }
    public string? SourceFormattedText { get; init; }
}
public sealed record OfficerInformationRow(int SourceIndex, int OfficerId,
    ImmutableArray<OfficerInformationCell> Cells);
public sealed record OfficerInformationPage(int GroupIndex, ImmutableArray<OfficerInformationRow> Rows,
    string? UnresolvedOwner);

/// <summary>The ordinary Person list's one immutable-world reader and shared
/// caller-owned order. Preparing another group only reads its uncached cells;
/// sorting consumes cached keys, never a full detail publication.</summary>
public sealed class OfficerInformationPageSet
{
    private readonly ImmutableArray<int> originalIds;
    private readonly Func<int, int, OfficerInformationCell> readCell;
    private readonly Func<int, int, string?> sortMissing;
    private readonly Func<int, int, int, bool, bool> before;
    private readonly Dictionary<int, OfficerInformationPage> pages = [];
    private int[] order;
    private ImmutableArray<int> currentIds;
    public string Title { get; }
    public ImmutableArray<OfficerInformationGroup> Groups { get; }
    public ImmutableArray<int> CurrentOfficerIds => currentIds;
    public int? SelectedOfficerId { get; private set; }
    public bool? NameSortAscending { get; private set; }
    public bool? DynamicPaletteEnabled { get; internal init; }

    internal OfficerInformationPageSet(string title, ImmutableArray<OfficerInformationGroup> groups,
        IReadOnlyList<int> ids, Func<int, int, OfficerInformationCell> readCell,
        Func<int, int, string?> sortMissing, Func<int, int, int, bool, bool> before)
    {
        Title = title; Groups = groups; originalIds = ids.ToImmutableArray();
        this.readCell = readCell; this.sortMissing = sortMissing; this.before = before;
        order = Enumerable.Range(0, originalIds.Length).ToArray(); currentIds = originalIds;
    }

    public OfficerInformationPage ReadPage(int groupIndex)
    {
        CheckGroup(groupIndex);
        if (pages.TryGetValue(groupIndex, out var saved)) return saved;
        var columns = Groups[groupIndex].Columns;
        var issues = new HashSet<string>(StringComparer.Ordinal);
        var rows = ImmutableArray.CreateBuilder<OfficerInformationRow>(order.Length);
        foreach (int slot in order)
        {
            var cells = ImmutableArray.CreateBuilder<OfficerInformationCell>(columns.Length);
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

    public AreaAdministrationQuery<OfficerInformationPage> Sort(int groupIndex, int columnId, bool ascending)
    {
        CheckGroup(groupIndex);
        var column = Groups[groupIndex].Columns.FirstOrDefault(value => value.Id == columnId);
        if (column is not { CanSort: true })
            return AreaAdministrationQuery<OfficerInformationPage>.Unknown("officer-information.sort.column");
        try
        {
            foreach (int slot in order)
                if (sortMissing(originalIds[slot], columnId) is { } missing) throw new InvalidDataException(missing);
            int[] sorted = (int[])order.Clone();
            //This is the actual normal list sorter, including its non-strict
            //trait predicate consumer. Do not replace it with IComparer.
            DeploymentPersonTraitSort.Sort(sorted, (left, right) =>
                before(originalIds[left], originalIds[right], columnId, ascending));
            AdoptSlots(sorted);
            if (columnId == 0) NameSortAscending = ascending;
            return AreaAdministrationQuery<OfficerInformationPage>.Known(ReadPage(groupIndex));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<OfficerInformationPage>.Unknown(error.Message); }
    }

    public void SelectSourceIndex(int sourceIndex)
    {
        if ((uint)sourceIndex >= (uint)originalIds.Length) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        SelectedOfficerId = originalIds[sourceIndex];
    }

    /// <summary>The common checked-column sorter over this same retained
    /// Person order; changing a page never reconstructs membership.</summary>
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

    /// <summary>Adopts the child selector's actual final order and current
    /// person, preserving repeated caller identities by occurrence FIFO.</summary>
    public AreaAdministrationQuery<OfficerInformationPage> AdoptDetailOrder(int groupIndex,
        IReadOnlyList<int> orderedOfficerIds, int selectedOfficerId, bool? nameSortAscending)
    {
        CheckGroup(groupIndex); ArgumentNullException.ThrowIfNull(orderedOfficerIds);
        if (orderedOfficerIds.Count != order.Length)
            return AreaAdministrationQuery<OfficerInformationPage>.Unknown("officer-information.return.count");
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
            if (!occurrences.TryGetValue(orderedOfficerIds[index], out var queue) || queue.Count == 0)
                return AreaAdministrationQuery<OfficerInformationPage>.Unknown("officer-information.return.caller_member");
            next[index] = queue.Dequeue();
        }
        if (!occurrences.ContainsKey(selectedOfficerId))
            return AreaAdministrationQuery<OfficerInformationPage>.Unknown("officer-information.return.selected_member");
        AdoptSlots(next); SelectedOfficerId = selectedOfficerId; NameSortAscending = nameSortAscending;
        return AreaAdministrationQuery<OfficerInformationPage>.Known(ReadPage(groupIndex));
    }

    private void AdoptSlots(int[] next)
    {
        order = next; currentIds = order.Select(slot => originalIds[slot]).ToImmutableArray(); pages.Clear();
    }
    private void CheckGroup(int index)
    { if ((uint)index >= (uint)Groups.Length) throw new ArgumentOutOfRangeException(nameof(index)); }
}
