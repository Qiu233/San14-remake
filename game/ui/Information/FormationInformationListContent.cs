using San14.Data;

internal sealed class FormationInformationListContent(FormationInformationPageSet source) : IPagedInformationListContent
{
    public string Title => source.Title;
    public IReadOnlyList<PagedInformationGroup> Groups { get; } = source.Groups.Select(group =>
        new PagedInformationGroup(group.Index, group.Caption, group.Columns.Select(column =>
            new PagedInformationColumn(column.Id, column.Caption, column.Width, column.CellFormat,
                column.InitialAscending, column.CanSort, column.Visible)).ToArray())).ToArray();
    public IReadOnlyList<int> CurrentIdentities => source.CurrentFormationIds;
    public int? SelectedIdentity => source.SelectedFormationId;
    public bool? NameSortAscending => source.NameSortAscending;
    public PagedInformationPage ReadPage(int index) => Convert(source.ReadPage(index));
    public void SelectSourceIndex(int index) => source.SelectSourceIndex(index);
    public AreaAdministrationQuery<PagedInformationPage> Sort(int group, int column, bool ascending)
        => Convert(source.Sort(group, column, ascending));
    public AreaAdministrationQuery<PagedInformationPage> AdoptDetailOrder(int group,
        IReadOnlyList<int> identities, int selected, bool? nameSortAscending)
        => Convert(source.AdoptDetailOrder(group, identities, selected, nameSortAscending));
    private static AreaAdministrationQuery<PagedInformationPage> Convert(AreaAdministrationQuery<FormationInformationPage> result)
        => result.Available && result.Value is { } page
            ? new(Convert(page), null)
            : new(null, result.UnresolvedInput);
    private static PagedInformationPage Convert(FormationInformationPage page) => new(page.GroupIndex,
        page.Rows.Select(row => new PagedInformationRow(row.SourceIndex, row.FormationId,
            row.Cells.Select(cell => new PagedInformationCell(cell.Text, cell.UnresolvedOwner,
                cell.PresetIndex, cell.MarkerColour?.CpuPacked)).ToArray())).ToArray(), page.UnresolvedOwner);
}
