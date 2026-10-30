using San14.Data;

internal static class RulerSuccessorSelectionContent
{
    internal static IntelligenceSelectionTable Create(RulerSuccessionSelection model,string unknown)
    {
        var pages=model.Pages;
        var groups=pages.Groups.Select(group=>new IntelligenceSelectionGroup(group.Index,group.Caption,
            group.Columns.Where(column=>column.Visible).Select(column=>new IntelligenceSelectionColumn(
                column.Id,column.Caption,column.Width,column.CellFormat,column.CanSort,column.InitialAscending)).ToArray())).ToArray();
        return new(0x4AB,model.Guide,groups,pages.CurrentOfficerIds.Length,Read,
            (group,column,ascending)=>pages.Sort(group,column,ascending).Available,ShowReturnButton:false);

        IReadOnlyList<IntelligenceSelectionRow> Read(int group)
        {
            int[] slots=pages.Groups[group].Columns.Select((column,index)=>(column,index))
                .Where(pair=>pair.column.Visible).Select(pair=>pair.index).ToArray();
            return pages.ReadPage(group).Rows.Select(row=>new IntelligenceSelectionRow(row.OfficerId,
                slots.Select((slot,index)=>
                {
                    var cell=row.Cells[slot];
                    return new IntelligenceSelectionCell(cell.UnresolvedOwner is null?cell.Text??"":unknown,
                        new MapInformationListCellStyle(groups[group].Columns[index].CellFormat,cell.PresetIndex,
                            cell.MarkerColour?.CpuPacked,StatePresets:cell.StatePresets ??
                                (cell.PresetIndex == 65 ? new InformationTextStatePresets(65,65,66,11) : null)),cell.SortUnresolvedOwner);
                }).ToArray(),true)).ToArray();
        }
    }
}
