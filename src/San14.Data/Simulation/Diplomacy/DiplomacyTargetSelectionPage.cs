using System.Collections.Immutable;

namespace San14.Data;

public enum DiplomacyTargetSelectionKind { Force, City, Item, Area }
public sealed record DiplomacyTargetColumn(int Id, int Width, int CaptionMessageId, string Caption,
    int Flags, bool InitialAscending);
public sealed record DiplomacyTargetGroup(int Index, string Caption, ImmutableArray<DiplomacyTargetColumn> Columns);
public sealed record DiplomacyTargetCell(string? Text, string? UnresolvedInput = null)
{
    public string? SortUnresolvedInput { get; init; }
    public uint? FirstGlyphColour { get; init; }
    public int? AreaIconSpriteId { get; init; }
    public string? AreaIconUnresolvedInput { get; init; }
}
public sealed record DiplomacyTargetRow(int SourceIndex, int Id, ImmutableArray<DiplomacyTargetCell> Cells);
public sealed record DiplomacyTargetList(int GroupIndex, ImmutableArray<DiplomacyTargetRow> Rows);

/// <summary>A diplomacy caller's retained candidates, prepared against its existing current.
/// Only requested groups are materialized; header comparison reads cached typed keys.</summary>
public sealed class DiplomacyTargetSelectionPage
{
    private readonly ImmutableArray<int> ids;
    private int[] order;
    private readonly Func<int, int, DiplomacyTargetCell> read;
    private readonly Func<int, int, int, bool, bool> before;
    private readonly Dictionary<int, DiplomacyTargetList> pages = [];
    public ImmutableArray<DiplomacyTargetGroup> Groups { get; }
    public bool HasLeadingAreaIcon { get; init; }
    public DiplomacyTargetMapQuery? MapSelection { get; internal set; }
    internal DiplomacyTargetSelectionPage(IReadOnlyList<int> ids, ImmutableArray<DiplomacyTargetGroup> groups,
        Func<int, int, DiplomacyTargetCell> read, Func<int, int, int, bool, bool> before)
    {
        this.ids = ids.ToImmutableArray(); order = Enumerable.Range(0, ids.Count).ToArray();
        Groups = groups; this.read = read; this.before = before;
    }
    public DiplomacyTargetList ReadPage(int group)
    {
        if ((uint)group >= (uint)Groups.Length) throw new ArgumentOutOfRangeException(nameof(group));
        if (pages.TryGetValue(group, out var saved)) return saved;
        return pages[group] = new(group, order.Select(slot => new DiplomacyTargetRow(slot, ids[slot],
            Groups[group].Columns.Select(column => read(ids[slot], column.Id)).ToImmutableArray())).ToImmutableArray());
    }
    public AreaAdministrationQuery<DiplomacyTargetList> Sort(int group, int column, bool ascending)
    {
        if ((uint)group >= (uint)Groups.Length || !Groups[group].Columns.Any(value => value.Id == column))
            throw new ArgumentOutOfRangeException(nameof(column));
        foreach (int slot in order)
            if (read(ids[slot], column).SortUnresolvedInput is { } issue)
                return AreaAdministrationQuery<DiplomacyTargetList>.Unknown(issue);
        try
        {
            order = order.OrderBy(slot => slot, Comparer<int>.Create((left, right) =>
                before(ids[left], ids[right], column, ascending) ? -1 :
                before(ids[right], ids[left], column, ascending) ? 1 : 0)).ToArray();
            pages.Clear(); return AreaAdministrationQuery<DiplomacyTargetList>.Known(ReadPage(group));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<DiplomacyTargetList>.Unknown(error.Message); }
    }
}
