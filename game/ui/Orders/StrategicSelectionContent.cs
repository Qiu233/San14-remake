using San14.Data;

/// <summary>Maps the current strategic selector's actual typed publication to
/// the common source list. It does not prepare a command, advice or numbers.</summary>
internal static class StrategicSelectionContent
{
    internal static IntelligenceSelectionTable Create(StrategicSelectionPage source,string unknown)
    {
        if(source.Officers is not null)return People();
        if(source.Forces is not null)return Forces();
        if(source.Cities is not null)return Cities();
        if(source.Areas is not null)return Areas();
        throw new InvalidDataException("strategic.selection.entity_publication");
        bool Enabled(int id)=>source.Eligibility.TryGetValue(id,out var row)&&row.Enabled==true;
        string? Hint(int id)=>source.Eligibility.TryGetValue(id,out var row)?row.UnresolvedOwner is null?row.DisabledReason:unknown:null;
        IntelligenceSelectionTable People()
        {
            var pages=source.Officers!;
            var groups=pages.Groups.Select(group=>new IntelligenceSelectionGroup(group.Index,group.Caption,
                group.Columns.Where(column=>column.Visible).Select(column=>new IntelligenceSelectionColumn(
                    column.Id,column.Caption,column.Width,column.CellFormat,column.CanSort,column.InitialAscending)).ToArray())).ToArray();
            return new(source.TitleMessageId,source.GuideText,groups,pages.CurrentOfficerIds.Length,Read,
                (group,column,ascending)=>pages.Sort(group,column,ascending).Available);
            IReadOnlyList<IntelligenceSelectionRow> Read(int group)
            {
                int[] slots=pages.Groups[group].Columns.Select((column,index)=>(column,index)).Where(pair=>pair.column.Visible).Select(pair=>pair.index).ToArray();
                return pages.ReadPage(group).Rows.Select(row=>
                {
                    var cells=slots.Select((slot,index)=>
                    {
                        var cell=row.Cells[slot];
                        return new IntelligenceSelectionCell(cell.UnresolvedOwner is null?cell.Text??"":unknown,
                            new MapInformationListCellStyle(groups[group].Columns[index].CellFormat,cell.PresetIndex,cell.MarkerColour?.CpuPacked,
                                StatePresets:cell.StatePresets,SourceFormattedText:cell.SourceFormattedText),cell.SortUnresolvedOwner);
                    }).ToArray();
                    bool altered=source.AlteredHealth is {} healthRows&&healthRows.TryGetValue(row.OfficerId,out var health)&&health.Available&&health.Value;
                    return new IntelligenceSelectionRow(row.OfficerId,cells,Enabled(row.OfficerId),altered?new(67,67,68,11):null,Hint(row.OfficerId));
                }).ToArray();
            }
        }
        IntelligenceSelectionTable Forces()
        {
            var pages=source.Forces!;
            var groups=pages.Groups.Select(group=>new IntelligenceSelectionGroup(group.Index,group.Caption,
                group.Columns.Where(column=>column.Visible).Select(column=>new IntelligenceSelectionColumn(
                    column.Id,column.Caption,column.Width,column.CellFormat,column.CanSort,column.InitialAscending)).ToArray())).ToArray();
            return new(source.TitleMessageId,source.GuideText,groups,pages.CurrentForceIds.Length,Read,
                (group,column,ascending)=>pages.Sort(group,column,ascending).Available);
            IReadOnlyList<IntelligenceSelectionRow> Read(int group)
            {
                int[] slots=pages.Groups[group].Columns.Select((column,index)=>(column,index)).Where(pair=>pair.column.Visible).Select(pair=>pair.index).ToArray();
                return pages.ReadPage(group).Rows.Select(row=>new IntelligenceSelectionRow(row.ForceId,
                    slots.Select((slot,index)=>{var cell=row.Cells[slot];return new IntelligenceSelectionCell(cell.UnresolvedOwner is null?cell.Text??"":unknown,
                        new MapInformationListCellStyle(groups[group].Columns[index].CellFormat,cell.PresetIndex,cell.MarkerColour?.CpuPacked),cell.SortUnresolvedOwner);}).ToArray(),
                    Enabled(row.ForceId),FocusHint:Hint(row.ForceId))).ToArray();
            }
        }
        IntelligenceSelectionTable Cities()
        {
            var pages=source.Cities!;
            var groups=pages.Groups.Select(group=>new IntelligenceSelectionGroup(group.Index,group.Caption,
                group.Columns.Where(column=>column.Visible).Select(column=>new IntelligenceSelectionColumn(
                    column.Id,column.Caption,column.Width,column.CellFormat,column.CanSort,column.InitialAscending)).ToArray())).ToArray();
            return new(source.TitleMessageId,source.GuideText,groups,pages.CurrentCityIds.Length,Read,
                (group,column,ascending)=>pages.Sort(group,column,ascending).Available);
            IReadOnlyList<IntelligenceSelectionRow> Read(int group)
            {
                int[] slots=pages.Groups[group].Columns.Select((column,index)=>(column,index)).Where(pair=>pair.column.Visible).Select(pair=>pair.index).ToArray();
                return pages.ReadPage(group).Rows.Select(row=>new IntelligenceSelectionRow(row.CityId,
                    slots.Select((slot,index)=>{var cell=row.Cells[slot];return new IntelligenceSelectionCell(cell.UnresolvedOwner is null?cell.Text??"":unknown,
                        new MapInformationListCellStyle(groups[group].Columns[index].CellFormat,cell.PresetIndex,cell.MarkerColour?.CpuPacked),cell.SortUnresolvedOwner);}).ToArray(),
                    Enabled(row.CityId),FocusHint:Hint(row.CityId))).ToArray();
            }
        }
        IntelligenceSelectionTable Areas()
        {
            var pages=source.Areas!;var names=ArmyMapUiSources.Create();
            var groups=pages.Groups.Select(group=>new IntelligenceSelectionGroup(group.Index,group.Caption,
                group.Columns.Where(column=>column.Visible).Select(column=>new IntelligenceSelectionColumn(
                    column.Id,column.Caption,column.Width,column.CellFormat,column.CanSort,column.InitialAscending)).ToArray())).ToArray();
            return new(source.TitleMessageId,source.GuideText,groups,pages.CurrentAreaIds.Length,Read,
                (group,column,ascending)=>column<0?source.SortLeadingAreaIcon?.Invoke(ascending).Available==true:pages.Sort(group,column,ascending).Available,HasLeadingAreaIcon:true);
            IReadOnlyList<IntelligenceSelectionRow> Read(int group)
            {
                int[] slots=pages.Groups[group].Columns.Select((column,index)=>(column,index)).Where(pair=>pair.column.Visible).Select(pair=>pair.index).ToArray();
                return pages.ReadPage(group).Rows.Select(row=>
                {
                    var cells=slots.Select((slot,index)=>{var cell=row.Cells[slot];return new IntelligenceSelectionCell(cell.UnresolvedOwner is null?cell.Text??"":unknown,
                        new MapInformationListCellStyle(groups[group].Columns[index].CellFormat,cell.PresetIndex,cell.MarkerColour?.CpuPacked),cell.SortUnresolvedOwner);}).ToArray();
                    AreaAdministrationQuery<int?>? icon=null;
                    if(source.LeadingAreaIcons is {} icons)icons.TryGetValue(row.AreaId,out icon);
                    string? image=icon?.Value is int id?names.IconName(new(id,0)):null;
                    return new IntelligenceSelectionRow(row.AreaId,cells,Enabled(row.AreaId),FocusHint:Hint(row.AreaId),LeadingIcon:image,
                        LeadingIconUnresolvedOwner:icon?.UnresolvedInput??(icon is null?"strategic.selection.area_icon_owner":null));
                }).ToArray();
            }
        }
    }
}
