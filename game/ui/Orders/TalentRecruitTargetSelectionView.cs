using Godot;
using San14.Data;
using System.Globalization;

/// <summary>The Recruit target's real three-group single-release Person list.
/// It has no checked-column front, initial selection or decision button.</summary>
internal partial class TalentRecruitTargetSelectionView : Control
{
    private MapInformationListView? list;
    private AdministrationPageChrome.Resources? resources;
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;
    private TalentRecruitPersonSelection model=null!;
    private OfficerInformationPageSet pages=null!;
    private Func<bool>? admitted;
    private Action<int?>? completed;
    private Func<int> scrollSpeed=null!;
    private readonly Dictionary<int,Dictionary<string,bool>> directions=[];
    private int currentScroll;
    private int? sortedGroup;
    private string? sortedColumn;
    private bool sortedAscending,abandoned;

    internal void Configure(GameResourceRepository repository,PreviewText text,TalentRecruitPersonSelection model,
        Func<bool> admitted,Action<int?> completed,Func<int> scrollSpeed, AdministrationPageChrome.Resources? resources = null)
    {
        this.repository=repository;this.text=text;this.model=model;pages=model.Pages;
        this.resources=resources;
        this.admitted=admitted;this.completed=completed;this.scrollSpeed=scrollSpeed;
        Name="TalentRecruitTargets";ZIndex=5;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop=new UiSourceCommonBlur();AddChild(backdrop);backdrop.Configure(repository);
        ShowGroup(0);
        SetMeta("original_recruit_target_groups",pages.Groups.Length);
    }
    private bool ReadAdmission()=>!abandoned&&completed is not null&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    private static string Key(int id)=>id==0?"name":id.ToString(CultureInfo.InvariantCulture);
    private void ShowGroup(int index)
    {
        if(abandoned||(uint)index>=(uint)pages.Groups.Length)return;
        if(list is {} previous)currentScroll=previous.ScrollValue;
        var descriptor=pages.Groups[index];var columns=descriptor.Columns.Where(column=>column.Visible).ToArray();
        var slots=descriptor.Columns.Select((column,slot)=>(column.Id,slot)).ToDictionary(value=>value.Id,value=>value.slot);
        var current=pages.ReadPage(index);
        if(!directions.TryGetValue(index,out var nextDirections))directions.Add(index,nextDirections=[]);
        if(list is {} retained)
        {
            retained.PresentGroup(index,Columns(),Rows(current),Sort,
                sortedGroup==index?sortedColumn:null,sortedAscending,nextDirections);
            SetMeta("recruit_target_active_page",index);
            return;
        }
        var view=new MapInformationListView();list=view;AddChild(view);
        view.Configure(repository,text,pages.Title,
            Columns(),Rows(current),
            ()=>ReadAdmission()&&list==view,RequestCancel,sourceIndex=>
            {
                if(!ReadAdmission()||list!=view)return;
                pages.SelectSourceIndex(sourceIndex);Finish(pages.SelectedOfficerId);
            },scrollSpeed,Sort,notice:model.GuideText,tabCaptions:pages.Groups.Select(group=>group.Caption).ToArray(),selectedTab:index,
            selectTab:group=>{if(ReadAdmission())ShowGroup(group);},completeColumnProfile:true,normalColumnLayout:false,
            initialSortColumn:sortedGroup==index?sortedColumn:null,initialSortAscending:sortedAscending,
            retainSortDirections:true,retainedSortDirections:nextDirections,rowExtentWidth:1464,sourcePersonCellBounds:true, resources: resources);
        view.RestoreScroll(currentScroll);SetMeta("recruit_target_active_page",index);

        MapInformationListColumn[] Columns()=>columns.Select(column=>
            new MapInformationListColumn(Key(column.Id),column.Caption,column.Width,
                column.CanSort,column.InitialAscending)).ToArray();
        IReadOnlyList<MapInformationListRow>? Sort(string key,bool ascending)
        {
            int column=key=="name"?0:int.Parse(key,CultureInfo.InvariantCulture);
            var result=pages.Sort(index,column,ascending);
            if(!result.Available||result.Value is not {} sorted)
            {if(result.UnresolvedInput is {} missing)GD.PushWarning(missing);return null;}
            current=sorted;sortedGroup=index;sortedColumn=key;sortedAscending=ascending;
            return Rows(current);
        }
        IReadOnlyList<MapInformationListRow> Rows(OfficerInformationPage page)=>page.Rows.Select(row=>
        {
            var cells=columns.Select(column=>row.Cells[slots[column.Id]]).ToArray();
            return new MapInformationListRow(row.SourceIndex,
                cells.Select(cell=>cell.UnresolvedOwner is null?cell.Text??"":text.Get("detail_unknown")).ToArray(),true,
                cells.Select((cell,slot)=>new MapInformationListCellStyle(columns[slot].CellFormat,
                    cell.PresetIndex,cell.MarkerColour?.CpuPacked,StatePresets:cell.StatePresets)).ToArray());
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
