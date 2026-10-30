using Godot;
using San14.Data;
using System.Globalization;

/// <summary>The district governor's actual three-group single-release Person list.
/// It retains the valid old governor without a checked front or decision button.</summary>
internal partial class DistrictLeaderSelectionView : Control
{
    private MapInformationListView? list;
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;
    private DistrictLeaderSelection model=null!;
    private OfficerInformationPageSet pages=null!;
    private Func<bool>? admitted;
    private Action<int?>? completed;
    private Func<int> scrollSpeed=null!;
    private readonly Dictionary<int,Dictionary<string,bool>> directions=[];
    private int currentScroll;
    private int? sortedGroup;
    private string? sortedColumn;
    private bool sortedAscending,abandoned;

    internal void Configure(GameResourceRepository repository,PreviewText text,DistrictLeaderSelection model,
        Func<bool> admitted,Action<int?> completed,Func<int> scrollSpeed)
    {
        this.repository=repository;this.text=text;this.model=model;pages=model.Pages;
        this.admitted=admitted;this.completed=completed;this.scrollSpeed=scrollSpeed;
        Name="DistrictLeaderPersons";ZIndex=5;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop=new UiSourceCommonBlur();AddChild(backdrop);backdrop.Configure(repository);
        ShowGroup(0);
        SetMeta("original_district_leader_groups",pages.Groups.Length);
    }
    private bool ReadAdmission()=>!abandoned&&completed is not null&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    private static string Key(int id)=>id==0?"name":id.ToString(CultureInfo.InvariantCulture);
    private void ShowGroup(int index)
    {
        if(abandoned||(uint)index>=(uint)pages.Groups.Length)return;
        if(list is {} previous)
        {currentScroll=previous.ScrollValue;previous.Abandon();RemoveChild(previous);previous.QueueFree();}
        var descriptor=pages.Groups[index];var columns=descriptor.Columns.Where(column=>column.Visible).ToArray();
        var slots=descriptor.Columns.Select((column,slot)=>(column.Id,slot)).ToDictionary(value=>value.Id,value=>value.slot);
        var current=pages.ReadPage(index);
        if(!directions.TryGetValue(index,out var nextDirections))directions.Add(index,nextDirections=[]);
        var view=new MapInformationListView();list=view;AddChild(view);
        view.Configure(repository,text,pages.Title,
            columns.Select(column=>new MapInformationListColumn(Key(column.Id),column.Caption,column.Width,
                column.CanSort,column.InitialAscending)).ToArray(),Rows(current),
            ()=>ReadAdmission()&&list==view,RequestCancel,sourceIndex=>
            {
                if(!ReadAdmission()||list!=view)return;
                pages.SelectSourceIndex(sourceIndex);
                if(pages.SelectedOfficerId is int id&&model.EligibleOfficerIds.Contains(id))Finish(id);
            },scrollSpeed,(key,ascending)=>
            {
                int column=key=="name"?0:int.Parse(key,CultureInfo.InvariantCulture);
                var result=pages.Sort(index,column,ascending);
                if(!result.Available||result.Value is not {} sorted)
                {if(result.UnresolvedInput is {} missing)GD.PushWarning(missing);return null;}
                current=sorted;sortedGroup=index;sortedColumn=key;sortedAscending=ascending;return Rows(current);
            },notice:model.GuideText,tabCaptions:pages.Groups.Select(group=>group.Caption).ToArray(),selectedTab:index,
            selectTab:group=>{if(ReadAdmission())ShowGroup(group);},completeColumnProfile:true,normalColumnLayout:false,
            initialSortColumn:sortedGroup==index?sortedColumn:null,initialSortAscending:sortedAscending,
            retainSortDirections:true,retainedSortDirections:nextDirections,disableUninspectableRows:true,
            rowExtentWidth:1464,sourcePersonCellBounds:true);
        if(model.InitialLeaderId is int leader&&current.Rows.FirstOrDefault(row=>row.OfficerId==leader) is {} initial)
            view.ApplyDetailReturn(Rows(current),initial.SourceIndex,null);
        view.RestoreScroll(currentScroll);
        SetMeta("district_leader_active_page",index);

        IReadOnlyList<MapInformationListRow> Rows(OfficerInformationPage page)=>page.Rows.Select(row=>
        {
            var cells=columns.Select(column=>row.Cells[slots[column.Id]]).ToArray();
            return new MapInformationListRow(row.SourceIndex,
                cells.Select(cell=>cell.UnresolvedOwner is null?cell.Text??"":text.Get("detail_unknown")).ToArray(),model.EligibleOfficerIds.Contains(row.OfficerId),
                cells.Select((cell,slot)=>new MapInformationListCellStyle(columns[slot].CellFormat,
                    cell.PresetIndex,cell.MarkerColour?.CpuPacked,
                    StatePresets:cell.StatePresets??(cell.PresetIndex is null&&model.AlteredHealth[row.OfficerId] is {Available:true,Value:true}
                        ?new InformationTextStatePresets(67,67,68,11):null),
                    SourceFormattedText:cell.UnresolvedOwner is null?cell.SourceFormattedText:null)).ToArray());
        }).ToArray();
    }
    internal void RequestCancel()=>Finish(null);
    private void Finish(int? id)
    {
        if(!ReadAdmission())return;var callback=completed;completed=null;admitted=null;Hide();callback?.Invoke(id);
    }
    public override void _ExitTree()=>Abandon();
    internal void Abandon()
    {if(abandoned)return;abandoned=true;completed=null;admitted=null;list?.Abandon();Hide();}
}
