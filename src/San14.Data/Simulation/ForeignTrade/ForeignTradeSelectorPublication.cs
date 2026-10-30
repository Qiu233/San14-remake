using System.Collections.Immutable;

namespace San14.Data;

public enum ForeignTradeSelectorKind { Country, Executors, Item }
public sealed record ForeignTradeSummary(string? Friendship, string? Rivals, string? UnresolvedOwner);
public sealed record ForeignTradeSelectorColumn(int Id, string Caption, int Width, int Format, bool InitialAscending);
public sealed record ForeignTradeSelectorGroup(int Index, string Caption, ImmutableArray<ForeignTradeSelectorColumn> Columns);
public sealed record ForeignTradeSelectorCell(string? Text, string? UnresolvedOwner = null,
    string? SortUnresolvedOwner = null, int? Preset = null, InformationTextStatePresets? StatePresets = null,
    string? SourceFormattedText = null, uint? FirstGlyphColour = null);
public sealed record ForeignTradeSelectorRow(int Id, ImmutableArray<ForeignTradeSelectorCell> Cells,
    bool Enabled, bool? AlteredHealth = false);

/// <summary>One actual foreign-trade child publication. Page switches, row
/// hover and header input retain its captured candidates and current owner.</summary>
public sealed class ForeignTradeSelectorPublication
{
    private readonly Func<int, ImmutableArray<ForeignTradeSelectorRow>> read;
    private readonly Func<int, int, bool, AreaAdministrationQuery<bool>> sort;
    private readonly Action<IReadOnlyList<int>, bool> selectedSort;
    private readonly Func<IReadOnlyList<int>> ids;
    public ForeignTradeSelectorKind Kind { get; }
    public ImmutableArray<ForeignTradeSelectorGroup> Groups { get; }
    public ImmutableArray<int> InitialSelection { get; }
    public int MaximumSelected { get; }
    public bool Multiple => Kind == ForeignTradeSelectorKind.Executors;
    public int TitleMessage => Kind == ForeignTradeSelectorKind.Country ? 0x58D : Multiple ? 0x58F : 0x58C;
    public int GuideMessage => Kind == ForeignTradeSelectorKind.Country ? 0x5BE7 : Multiple ? 0x5BE8 : 0x5BE9;
    public IReadOnlyList<int> CurrentIds => ids();
    internal ForeignTradeSelectorPublication(ForeignTradeSelectorKind kind, ImmutableArray<ForeignTradeSelectorGroup> groups,
        IReadOnlyList<int> initial, int maximum, Func<int, ImmutableArray<ForeignTradeSelectorRow>> read,
        Func<int, int, bool, AreaAdministrationQuery<bool>> sort, Action<IReadOnlyList<int>, bool> selectedSort,
        Func<IReadOnlyList<int>> ids)
    { Kind=kind; Groups=groups; InitialSelection=initial.ToImmutableArray(); MaximumSelected=maximum;
        this.read=read; this.sort=sort; this.selectedSort=selectedSort; this.ids=ids; }
    public ImmutableArray<ForeignTradeSelectorRow> ReadPage(int group) => read(group);
    public AreaAdministrationQuery<bool> Sort(int group, int column, bool ascending) => sort(group,column,ascending);
    public void SortSelection(IReadOnlyList<int> selected, bool first) => selectedSort(selected,first);
}
