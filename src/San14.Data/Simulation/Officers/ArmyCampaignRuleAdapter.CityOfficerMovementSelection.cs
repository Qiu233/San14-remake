using System.Collections.Immutable;
namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private DiplomacyTargetSelectionPage CreateCityOfficerMovementTargetSelection(
        IReadOnlyList<CityOfficerMovementCity> cities,DiplomacyTargetMapQuery mapSelection)
    {
        var rows=cities.ToDictionary(row=>row.CityId);
        var ids=cities.Select(row=>row.CityId).ToArray();
        var columns=CityOfficerMovementCity.Columns.Select(column=>new DiplomacyTargetColumn(column.Id,
            column.Width,column.CaptionId,ReadHistoryDataText(1,column.CaptionId),column.Id is 0 or 1?1:column.Id<=5?0x20000002:2,column.InitialAscending)).ToImmutableArray();
        CityOfficerMovementDestinationCell Cell(int id,int column)=>rows[id].Cells.First(cell=>cell.ColumnId==column);
        return new(ids,[new(0,ReadHistoryDataText(1,0x3CD),columns)],(id,column)=>
        {
            var cell=Cell(id,column);
            return new(cell.Text,cell.UnresolvedInput){SortUnresolvedInput=cell.CanSort?null:cell.UnresolvedInput??"city-officer-movement.destination_sort_key"};
        },(left,right,column,ascending)=>CityOfficerMovementCity.CompareCells(Cell(ascending?left:right,column),
            Cell(ascending?right:left,column),column)<0)
        {MapSelection=mapSelection};
    }
}
