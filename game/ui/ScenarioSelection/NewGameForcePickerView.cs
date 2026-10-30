using Godot;
using San14.Data;
using System.Globalization;

/// <summary>The source footer14 single-release Force selector. Its seven
/// authored groups share one order and current scroll, with a fourteen-row pool.</summary>
internal partial class NewGameForcePickerView : Control
{
    private MapInformationListView? list;
    private Action<int?>? completed;
    private Func<bool>? admitted;
    private Func<int> scrollSpeed=()=>3;
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;
    private ForceInformationPageSet pages=null!;
    private int group,currentScroll;
    private int? sortedGroup;
    private string? sortedColumn;
    private bool sortedAscending;
    private readonly Dictionary<int,Dictionary<string,bool>> directions=[];
    private readonly Dictionary<BaseButton,bool> buttonStates=[];
    private bool abandoned,busy;
    internal NewGameForcePickerPreview Publication {get;private set;}=null!;
    internal IReadOnlyList<NewGameForcePreview> Rows=>Publication.NativeSortedRows??Publication.Rows;
    internal int? SelectedForceId=>pages.SelectedForceId??Publication.InitialSelectedForceId;

    internal void Configure(GameResourceRepository repository,PreviewText text,
        NewGameForcePickerPreview publication,Action<int?> completed,
        Func<int>? scrollSpeed=null,Func<bool>? admitted=null)
    {
        this.repository=repository;this.text=text;Publication=publication;
        pages=publication.Pages;this.completed=completed;this.admitted=admitted;
        if(scrollSpeed is not null)this.scrollSpeed=scrollSpeed;
        Name="NewGameForcePicker";ZIndex=3;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect{Color=new("#292d2c"),AnchorRight=1,AnchorBottom=1,
            MouseFilter=MouseFilterEnum.Ignore});
        ShowGroup(0);
        SetMeta("original_force_column_groups",pages.Groups.Length);
    }
    private bool ReadAdmission()=>!abandoned&&!busy&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??true);
    private static string Key(int column)=>column==0?"name":column.ToString(CultureInfo.InvariantCulture);

    private void ShowGroup(int index)
    {
        if(abandoned||(uint)index>=(uint)pages.Groups.Length)return;
        if(list is {} previous)
        {
            //50F850 copies the active body's current scroll into the next body.
            currentScroll=previous.ScrollValue;previous.Abandon();RemoveChild(previous);previous.QueueFree();
        }
        group=index;var descriptor=pages.Groups[index];
        var all=descriptor.Columns;var visible=all.Where(column=>column.Visible).ToArray();
        var slots=all.Select((column,slot)=>(column.Id,slot)).ToDictionary(value=>value.Id,value=>value.slot);
        var current=pages.ReadPage(index);
        if(!directions.TryGetValue(index,out var nextDirections))directions.Add(index,nextDirections=[]);
        var next=new MapInformationListView();list=next;AddChild(next);
        string hint=new AdministrationMessages(repository,text.SourceLocale).Format(0x2739);
        next.Configure(repository,text,pages.Title,
            visible.Select(column=>new MapInformationListColumn(Key(column.Id),column.Caption,column.Width,
                column.CanSort,column.InitialAscending)).ToArray(),RowsFor(current),
            ()=>ReadAdmission()&&list==next,RequestBack,sourceIndex=>
            {
                if(!ReadAdmission()||list!=next)return;
                pages.SelectSourceIndex(sourceIndex);
                completed?.Invoke(pages.SelectedForceId);
            },scrollSpeed,(key,ascending)=>
            {
                int column=key=="name"?0:int.Parse(key,CultureInfo.InvariantCulture);
                var result=pages.Sort(index,column,ascending);
                if(!result.Available||result.Value is not {} sorted)
                {GD.PushWarning(result.UnresolvedInput??"new-game.force-picker.sort");return null;}
                current=sorted;sortedGroup=index;sortedColumn=key;sortedAscending=ascending;
                return RowsFor(current);
            },notice:hint,tabCaptions:pages.Groups.Select(value=>value.Caption).ToArray(),
            selectedTab:index,selectTab:nextGroup=>{if(ReadAdmission())ShowGroup(nextGroup);},
            completeColumnProfile:true,normalColumnLayout:false,
            initialSortColumn:sortedGroup==index?sortedColumn:null,initialSortAscending:sortedAscending,
            retainSortDirections:true,retainedSortDirections:nextDirections,rowExtentWidth:1464);
        if(pages.SelectedForceId is int selected)
        {
            var row=current.Rows.FirstOrDefault(row=>row.ForceId==selected);
            if(row is not null)next.ApplyDetailReturn(RowsFor(current),row.SourceIndex,null);
        }
        next.RestoreScroll(currentScroll);
        // Capture only this page's finite controls once; busy changes no row
        // data, order, resource ownership or gameplay state.
        buttonStates.Clear();
        foreach(var button in next.FindChildren("*","BaseButton",true,false).OfType<BaseButton>())
            buttonStates.Add(button,button.Disabled);
        ApplyBusy();SetMeta("new_game_force_picker_group",index);

        IReadOnlyList<MapInformationListRow> RowsFor(ForceInformationListPage page)=>page.Rows.Select(row=>
        {
            var cells=visible.Select(column=>row.Cells[slots[column.Id]]).ToArray();
            return new MapInformationListRow(row.SourceIndex,
                cells.Select(cell=>cell.UnresolvedOwner is null?cell.Text??"":text.Get("detail_unknown")).ToArray(),true,
                cells.Select((cell,slot)=>new MapInformationListCellStyle(visible[slot].CellFormat,
                    cell.PresetIndex,cell.MarkerColour?.CpuPacked)).ToArray());
        }).ToArray();
    }
    internal void SetBusy(bool value){if(abandoned)return;busy=value;ApplyBusy();}
    private void ApplyBusy()
    {
        foreach(var (button,disabled) in buttonStates)
            if(GodotObject.IsInstanceValid(button))button.Disabled=busy||disabled;
    }
    internal void RequestBack(){if(ReadAdmission())completed?.Invoke(null);}
    internal void Abandon()
    {
        if(abandoned)return;abandoned=true;completed=null;admitted=null;
        list?.Abandon();buttonStates.Clear();Hide();
    }
    public override void _ExitTree(){Abandon();}
}
