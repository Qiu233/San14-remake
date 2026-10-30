using System.Collections.Immutable;

namespace San14.Data;

public sealed record DefinitionInformationColumn(int Id, int CaptionMessageId, string Caption,
    int Width, int CellFormat, bool InitialAscending, bool CanSort, bool Visible, bool ComparesRows = true);
public sealed record DefinitionInformationGroup(int Index, int CaptionMessageId, string Caption,
    ImmutableArray<DefinitionInformationColumn> Columns);
public sealed record DefinitionInformationCell(string? Text, int? PresetIndex, string? UnresolvedOwner)
{
    public string? SortUnresolvedOwner { get; init; }
    public string? PresetUnresolvedOwner { get; init; }
    public AreaMarkerColor? MarkerColour { get; init; }
}
public sealed record DefinitionInformationRow(int SourceIndex, int DefinitionId,
    ImmutableArray<DefinitionInformationCell> Cells);
public sealed record DefinitionInformationPage(int GroupIndex, ImmutableArray<DefinitionInformationRow> Rows,
    string? UnresolvedOwner);

/// <summary>The ordinary definition list's one immutable-world reader and shared
/// caller-owned order. Preparing another group only reads its uncached cells;
/// sorting consumes cached keys, never a full detail publication.</summary>
public sealed class DefinitionInformationPageSet
{
    private readonly ImmutableArray<int> originalIds;
    private readonly Func<int, int, DefinitionInformationCell> readCell;
    private readonly Func<int, int, string?> sortMissing;
    private readonly Func<int, int, int, bool, bool> before;
    private readonly Dictionary<int, DefinitionInformationPage> pages = [];
    private int[] order;
    private ImmutableArray<int> currentIds;
    public string Title { get; }
    public ImmutableArray<DefinitionInformationGroup> Groups { get; }
    public ImmutableArray<int> CurrentDefinitionIds => currentIds;
    public int? SelectedDefinitionId { get; private set; }
    public bool? NameSortAscending { get; private set; }
    public InformationListKind Kind { get; internal init; }
    public string OfficersCaption { get; internal init; } = "";
    public string? CandidateUnresolvedOwner { get; internal init; }
    internal Func<int, bool> ReadCanBrowseOfficers { get; init; } = _ => false;
    public bool CanBrowseOfficers(int formationId) => ReadCanBrowseOfficers(formationId);

    internal DefinitionInformationPageSet(string title, ImmutableArray<DefinitionInformationGroup> groups,
        IReadOnlyList<int> ids, Func<int, int, DefinitionInformationCell> readCell,
        Func<int, int, string?> sortMissing, Func<int, int, int, bool, bool> before)
    {
        Title = title; Groups = groups; originalIds = ids.ToImmutableArray();
        this.readCell = readCell; this.sortMissing = sortMissing; this.before = before;
        order = Enumerable.Range(0, originalIds.Length).ToArray(); currentIds = originalIds;
    }

    public DefinitionInformationPage ReadPage(int groupIndex)
    {
        CheckGroup(groupIndex);
        if (pages.TryGetValue(groupIndex, out var saved)) return saved;
        var columns = Groups[groupIndex].Columns;
        var issues = new HashSet<string>(StringComparer.Ordinal);
        if (CandidateUnresolvedOwner is { } candidateIssue) issues.Add(candidateIssue);
        var rows = ImmutableArray.CreateBuilder<DefinitionInformationRow>(order.Length);
        foreach (int slot in order)
        {
            var cells = ImmutableArray.CreateBuilder<DefinitionInformationCell>(columns.Length);
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

    public AreaAdministrationQuery<DefinitionInformationPage> Sort(int groupIndex, int columnId, bool ascending)
    {
        CheckGroup(groupIndex);
        var column = Groups[groupIndex].Columns.FirstOrDefault(value => value.Id == columnId);
        if (column is not { CanSort: true })
            return AreaAdministrationQuery<DefinitionInformationPage>.Unknown("definition-information.sort.column");
        // Original mode2 headers remain interactive and retain direction,
        // while both comparator callbacks return false without reading keys.
        if (!column.ComparesRows)
        {
            if (columnId == 0) NameSortAscending = ascending;
            return AreaAdministrationQuery<DefinitionInformationPage>.Known(ReadPage(groupIndex));
        }
        try
        {
            foreach (int slot in order)
                if (sortMissing(originalIds[slot], columnId) is { } missing) throw new InvalidDataException(missing);
            int[] sorted = (int[])order.Clone();
            //The normal Formation sorter is a stable merge/insertion family.
            //All comparison values were prepared above; no query runs here.
            DeploymentPersonTraitSort.Sort(sorted, (left, right) =>
                before(originalIds[left], originalIds[right], columnId, ascending));
            AdoptSlots(sorted);
            if (columnId == 0) NameSortAscending = ascending;
            return AreaAdministrationQuery<DefinitionInformationPage>.Known(ReadPage(groupIndex));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<DefinitionInformationPage>.Unknown(error.Message); }
    }

    // A caller-owned auxiliary header reorders the same retained slots.
    // No descriptor column, selected row or name-sort state is synthesized.
    internal AreaAdministrationQuery<DefinitionInformationPage> SortAuxiliary(int groupIndex,
        Func<int,int,bool> before)
    {
        CheckGroup(groupIndex); ArgumentNullException.ThrowIfNull(before);
        try
        {
            int[] sorted=(int[])order.Clone();
            DeploymentPersonTraitSort.Sort(sorted,(left,right)=>
                before(originalIds[left],originalIds[right]));
            AdoptSlots(sorted);
            return AreaAdministrationQuery<DefinitionInformationPage>.Known(ReadPage(groupIndex));
        }
        catch(InvalidDataException error)
        { return AreaAdministrationQuery<DefinitionInformationPage>.Unknown(error.Message); }
    }

    public void SelectSourceIndex(int sourceIndex)
    {
        if ((uint)sourceIndex >= (uint)originalIds.Length) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        SelectedDefinitionId = originalIds[sourceIndex];
    }

    /// <summary>Adopts the child selector's actual final order and current
    /// definition, preserving repeated caller identities by occurrence FIFO.</summary>
    public AreaAdministrationQuery<DefinitionInformationPage> AdoptDetailOrder(int groupIndex,
        IReadOnlyList<int> orderedDefinitionIds, int selectedDefinitionId, bool? nameSortAscending)
    {
        CheckGroup(groupIndex); ArgumentNullException.ThrowIfNull(orderedDefinitionIds);
        if (orderedDefinitionIds.Count != order.Length)
            return AreaAdministrationQuery<DefinitionInformationPage>.Unknown("definition-information.return.count");
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
            if (!occurrences.TryGetValue(orderedDefinitionIds[index], out var queue) || queue.Count == 0)
                return AreaAdministrationQuery<DefinitionInformationPage>.Unknown("definition-information.return.caller_member");
            next[index] = queue.Dequeue();
        }
        if (!occurrences.ContainsKey(selectedDefinitionId))
            return AreaAdministrationQuery<DefinitionInformationPage>.Unknown("definition-information.return.selected_member");
        AdoptSlots(next); SelectedDefinitionId = selectedDefinitionId; NameSortAscending = nameSortAscending;
        return AreaAdministrationQuery<DefinitionInformationPage>.Known(ReadPage(groupIndex));
    }

    private void AdoptSlots(int[] next)
    {
        order = next; currentIds = order.Select(slot => originalIds[slot]).ToImmutableArray(); pages.Clear();
    }
    private void CheckGroup(int index)
    { if ((uint)index >= (uint)Groups.Length) throw new ArgumentOutOfRangeException(nameof(index)); }
}
