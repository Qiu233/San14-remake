using System.Collections.Immutable;

namespace San14.Data;

public sealed record FacilitySelectionColumn(int Id, string Caption, int Width,
    int CellFormat, bool InitialAscending, bool CanSort);
public sealed record FacilitySelectionGroup(int Index, string Caption, ImmutableArray<FacilitySelectionColumn> Columns);
public sealed record FacilitySelectionCell(string? Text, string? UnresolvedOwner, string? SortUnresolvedOwner);
public sealed record FacilitySelectionRow(int SourceIndex, string DefinitionKey, ImmutableArray<FacilitySelectionCell> Cells);
public sealed record FacilitySelectionPage(int GroupIndex, ImmutableArray<FacilitySelectionRow> Rows, string? UnresolvedOwner);
internal sealed record FacilitySelectionKey(int Number, string? Name = null, bool Empty = false)
{
    internal bool Before(FacilitySelectionKey other, bool ascending)
    {
        if(Empty!=other.Empty)return !Empty;
        if(Empty)return false;
        return Name is not null && other.Name is not null
            ? (ascending?PersonNameOrder.Compare(Name,other.Name):PersonNameOrder.Compare(other.Name,Name))<0
            : ascending?Number<other.Number:Number>other.Number;
    }
}
internal sealed record PreparedFacilitySelectionCell(FacilitySelectionCell Display, FacilitySelectionKey? Key);

/// <summary>One retained facility directory over one current capture. Cell
/// preparation is cached; sorting and tab/scroll input never capture a world.</summary>
public sealed class FacilitySelectionPageSet
{
    private readonly ImmutableArray<UnitFacilityDirectoryRow> sourceRows;
    private readonly Func<UnitFacilityDirectoryRow,int,PreparedFacilitySelectionCell> read;
    private readonly Dictionary<(int,int),PreparedFacilitySelectionCell> cells=[];
    private readonly Dictionary<int,FacilitySelectionPage> pages=[];
    private int[] order;
    public ImmutableArray<FacilitySelectionGroup> Groups {get;}
    public IReadOnlyList<string> CurrentDefinitionKeys=>order.Select(i=>sourceRows[i].DefinitionKey).ToArray();
    internal FacilitySelectionPageSet(IReadOnlyList<UnitFacilityDirectoryRow> rows,
        ImmutableArray<FacilitySelectionGroup> groups,Func<UnitFacilityDirectoryRow,int,PreparedFacilitySelectionCell> read)
    {sourceRows=rows.ToImmutableArray();Groups=groups;this.read=read;order=Enumerable.Range(0,rows.Count).ToArray();}
    private PreparedFacilitySelectionCell Cell(int slot,int column)
    {
        var key=(slot,column);
        if(!cells.TryGetValue(key,out var result))cells[key]=result=read(sourceRows[slot],column);
        return result;
    }
    public FacilitySelectionPage ReadPage(int group)
    {
        if((uint)group>=Groups.Length)throw new ArgumentOutOfRangeException(nameof(group));
        if(pages.TryGetValue(group,out var saved))return saved;
        var issues=new HashSet<string>(StringComparer.Ordinal);
        var rows=ImmutableArray.CreateBuilder<FacilitySelectionRow>(order.Length);
        foreach(int slot in order)
        {
            var values=Groups[group].Columns.Select(c=>Cell(slot,c.Id).Display).ToImmutableArray();
            foreach(var value in values)if(value.UnresolvedOwner is {} missing)issues.Add(missing);
            rows.Add(new(slot,sourceRows[slot].DefinitionKey,values));
        }
        return pages[group]=new(group,rows.MoveToImmutable(),issues.Count==0?null:string.Join(";",issues));
    }
    /// <summary>The fresh Facility selector's negative initial sort uses
    /// selected physical-pointer membership. Ties retain the caller order.</summary>
    internal void PrioritizeSelectedDefinition(int selectedPhysical)
    {
        order=order.OrderBy(slot=>sourceRows[slot].DefinitionPhysicalSlot==selectedPhysical?0:1).ToArray();
        pages.Clear();
    }
    public AreaAdministrationQuery<FacilitySelectionPage> Sort(int group,int column,bool ascending)
    {
        if((uint)group>=Groups.Length||!Groups[group].Columns.Any(c=>c.Id==column&&c.CanSort))
            return AreaAdministrationQuery<FacilitySelectionPage>.Unknown("facility.selection.sort.column");
        try
        {
            foreach(int slot in order)
            {
                var cell=Cell(slot,column);
                if(cell.Display.SortUnresolvedOwner is {} missing)throw new InvalidDataException(missing);
                if(cell.Key is null)throw new InvalidDataException("facility.selection.sort.key");
            }
            order=order.OrderBy(slot=>slot,Comparer<int>.Create((a,b)=>
                Cell(a,column).Key!.Before(Cell(b,column).Key!,ascending)?-1:
                Cell(b,column).Key!.Before(Cell(a,column).Key!,ascending)?1:0)).ToArray();
            pages.Clear();return AreaAdministrationQuery<FacilitySelectionPage>.Known(ReadPage(group));
        }
        catch(InvalidDataException error){return AreaAdministrationQuery<FacilitySelectionPage>.Unknown(error.Message);}
    }
}
