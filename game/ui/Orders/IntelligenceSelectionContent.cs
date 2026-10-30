using San14.Data;

/// <summary>Only adapts the retained command publication to its source list.
/// Domain identities and sort keys remain owned by the three typed readers.</summary>
internal static class IntelligenceSelectionContent
{
    internal static IntelligenceSelectionTable Create(IntelligencePersonSelection source, string unknown)
    {
        var pages = source.Pages;
        var groups = pages.Groups.Select(group => new IntelligenceSelectionGroup(group.Index,group.Caption,
            group.Columns.Where(column => column.Visible).Select(column => new IntelligenceSelectionColumn(
                column.Id,column.Caption,column.Width,column.CellFormat,column.CanSort,column.InitialAscending)).ToArray())).ToArray();
        return new(0x58F,source.GuideText,groups,pages.CurrentOfficerIds.Length,Read,
            (group,column,ascending) => pages.Sort(group,column,ascending).Available);
        IReadOnlyList<IntelligenceSelectionRow> Read(int group)
        {
            int[] slots = pages.Groups[group].Columns.Select((column,index) => (column,index))
                .Where(pair => pair.column.Visible).Select(pair => pair.index).ToArray();
            return pages.ReadPage(group).Rows.Select(row =>
            {
                bool altered = source.AlteredHealth.TryGetValue(row.OfficerId,out var health) && health.Available && health.Value;
                var cells = slots.Select((slot,index) =>
                {
                    var cell = row.Cells[slot];
                    return new IntelligenceSelectionCell(cell.UnresolvedOwner is null ? cell.Text ?? "" : unknown,
                        new MapInformationListCellStyle(groups[group].Columns[index].CellFormat,cell.PresetIndex,
                            cell.MarkerColour?.CpuPacked,StatePresets:cell.StatePresets,SourceFormattedText:cell.SourceFormattedText),
                        cell.SortUnresolvedOwner);
                }).ToArray();
                return new IntelligenceSelectionRow(row.OfficerId,cells,
                    source.Eligibility.TryGetValue(row.OfficerId,out var candidate) && candidate.Enabled,
                    altered ? new(67,67,68,11) : null);
            }).ToArray();
        }
    }

    internal static IntelligenceSelectionTable Create(IntelligenceForceSelection source, string unknown)
    {
        var pages = source.Pages;
        var groups = pages.Groups.Select(group => new IntelligenceSelectionGroup(group.Index,group.Caption,
            group.Columns.Where(column => column.Visible).Select(column => new IntelligenceSelectionColumn(
                column.Id,column.Caption,column.Width,column.CellFormat,column.CanSort,column.InitialAscending)).ToArray())).ToArray();
        return new(0x58D,source.GuideText,groups,pages.CurrentForceIds.Length,Read,
            (group,column,ascending) => pages.Sort(group,column,ascending).Available);
        IReadOnlyList<IntelligenceSelectionRow> Read(int group)
        {
            int[] slots = pages.Groups[group].Columns.Select((column,index) => (column,index))
                .Where(pair => pair.column.Visible).Select(pair => pair.index).ToArray();
            return pages.ReadPage(group).Rows.Select(row =>
            {
                var cells = slots.Select((slot,index) =>
                {
                    var cell = row.Cells[slot];
                    return new IntelligenceSelectionCell(cell.UnresolvedOwner is null ? cell.Text ?? "" : unknown,
                        new MapInformationListCellStyle(groups[group].Columns[index].CellFormat,cell.PresetIndex,
                            cell.MarkerColour?.CpuPacked),cell.SortUnresolvedOwner);
                }).ToArray();
                return new IntelligenceSelectionRow(row.ForceId,cells,
                    source.Eligibility.TryGetValue(row.ForceId,out var candidate) && candidate.Enabled,
                    FocusHint:source.FocusHints.GetValueOrDefault(row.ForceId));
            }).ToArray();
        }
    }

    internal static IntelligenceSelectionTable Create(IntelligenceCitySelection source, string unknown)
    {
        var pages = source.Pages;
        var groups = pages.Groups.Select(group => new IntelligenceSelectionGroup(group.Index,group.Caption,
            group.Columns.Where(column => column.Visible).Select(column => new IntelligenceSelectionColumn(
                column.Id,column.Caption,column.Width,column.CellFormat,column.CanSort,column.InitialAscending)).ToArray())).ToArray();
        return new(0x58D,source.GuideText,groups,pages.CurrentCityIds.Length,Read,
            (group,column,ascending) => pages.Sort(group,column,ascending).Available);
        IReadOnlyList<IntelligenceSelectionRow> Read(int group)
        {
            int[] slots = pages.Groups[group].Columns.Select((column,index) => (column,index))
                .Where(pair => pair.column.Visible).Select(pair => pair.index).ToArray();
            return pages.ReadPage(group).Rows.Select(row =>
            {
                var cells = slots.Select((slot,index) =>
                {
                    var cell = row.Cells[slot];
                    return new IntelligenceSelectionCell(cell.UnresolvedOwner is null ? cell.Text ?? "" : unknown,
                        new MapInformationListCellStyle(groups[group].Columns[index].CellFormat,cell.PresetIndex,
                            cell.MarkerColour?.CpuPacked),cell.SortUnresolvedOwner);
                }).ToArray();
                return new IntelligenceSelectionRow(row.CityId,cells,
                    source.Eligibility.TryGetValue(row.CityId,out var candidate) && candidate.Enabled);
            }).ToArray();
        }
    }
}
