using San14.Data;

internal sealed record PagedInformationColumn(int Id, string Caption, int Width,
    int CellFormat, bool InitialAscending, bool CanSort, bool Visible);
internal sealed record PagedInformationGroup(int Index, string Caption, IReadOnlyList<PagedInformationColumn> Columns);
internal sealed record PagedInformationCell(string? Text, string? UnresolvedOwner,
    int? PresetIndex, uint? FirstGlyphColour, int? IconSpriteId = null, InformationTextStatePresets? StatePresets = null);
internal sealed record PagedInformationRow(int SourceIndex, int Identity, IReadOnlyList<PagedInformationCell> Cells);
internal sealed record PagedInformationPage(int GroupIndex, IReadOnlyList<PagedInformationRow> Rows, string? UnresolvedOwner);

/// <summary>The two actual grouped information lists expose prepared values
/// and caller-owned identities. The display never captures a game world.</summary>
internal interface IPagedInformationListContent
{
    string Title { get; }
    IReadOnlyList<PagedInformationGroup> Groups { get; }
    IReadOnlyList<int> CurrentIdentities { get; }
    int? SelectedIdentity { get; }
    bool? NameSortAscending { get; }
    PagedInformationPage ReadPage(int index);
    AreaAdministrationQuery<PagedInformationPage> Sort(int group, int column, bool ascending);
    void SelectSourceIndex(int index);
    AreaAdministrationQuery<PagedInformationPage> AdoptDetailOrder(int group,
        IReadOnlyList<int> identities, int selected, bool? nameSortAscending);
}

internal sealed class OfficerInformationListContent(OfficerInformationPageSet source) : IPagedInformationListContent
{
    public string Title => source.Title;
    public IReadOnlyList<PagedInformationGroup> Groups { get; } = source.Groups.Select(group =>
        new PagedInformationGroup(group.Index, group.Caption, group.Columns.Select(column =>
            new PagedInformationColumn(column.Id, column.Caption, column.Width, column.CellFormat,
                column.InitialAscending, column.CanSort, column.Visible)).ToArray())).ToArray();
    public IReadOnlyList<int> CurrentIdentities => source.CurrentOfficerIds;
    public int? SelectedIdentity => source.SelectedOfficerId;
    public bool? NameSortAscending => source.NameSortAscending;
    public PagedInformationPage ReadPage(int index) => Convert(source.ReadPage(index));
    public void SelectSourceIndex(int index) => source.SelectSourceIndex(index);
    public AreaAdministrationQuery<PagedInformationPage> Sort(int group, int column, bool ascending)
        => Convert(source.Sort(group, column, ascending));
    public AreaAdministrationQuery<PagedInformationPage> AdoptDetailOrder(int group,
        IReadOnlyList<int> identities, int selected, bool? nameSortAscending)
        => Convert(source.AdoptDetailOrder(group, identities, selected, nameSortAscending));
    private static AreaAdministrationQuery<PagedInformationPage> Convert(AreaAdministrationQuery<OfficerInformationPage> result)
        => result.Available && result.Value is { } page
            ? new(Convert(page), null)
            : new(null, result.UnresolvedInput);
    private static PagedInformationPage Convert(OfficerInformationPage page) => new(page.GroupIndex,
        page.Rows.Select(row => new PagedInformationRow(row.SourceIndex, row.OfficerId,
            row.Cells.Select(cell => new PagedInformationCell(cell.Text, cell.UnresolvedOwner,
                cell.PresetIndex, cell.MarkerColour?.CpuPacked, StatePresets: cell.StatePresets)).ToArray())).ToArray(), page.UnresolvedOwner);
}
