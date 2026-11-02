using Godot;
using San14.Core;
using San14.Data;

/// <summary>The district city list retains the real City profile and local checks.
/// Close-list, cancel-stage and confirm are distinct actual source actions.</summary>
internal enum DistrictCitySelectionAction { CloseList, CancelStage, Confirm }
internal sealed record DistrictCitySelectionResult(DistrictCitySelectionAction Action,IReadOnlyList<int> CityIds);

internal partial class DistrictCitySelectionView : Control
{
    private sealed record RowDisplay(UiSourcePersonRow Button) { internal CityInformationRow? Entry; }
    private AdministrationPageChrome chrome = null!;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private DistrictCitySelection model = null!;
    private DistrictCityHints hints = null!;
    private DistrictCityHintKind activeHint=DistrictCityHintKind.Default;
    private CityInformationPageSet pages = null!;
    private Control headerHost = null!, rowsHost = null!;
    private UiSourceScrollbar scroll = null!;
    private UiSourceBottomButton selectAll = null!;
    private readonly Dictionary<int, UiSourceTablePage<RowDisplay>> tables = [];
    private UiSourceTablePage<RowDisplay>? activeTable;
    private readonly List<int> selected = [];
    private readonly Dictionary<int, Dictionary<int, bool>> directions = [];
    private readonly Dictionary<int, (int Column, bool Ascending)> arrows = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private Func<bool>? admitted;
    private Action<DistrictCitySelectionResult>? closed;
    private Action<int?>? hovered;
    private int? hoveredCity;
    private HashSet<int> locked=[];
    private DistrictCitySelectionAdmission currentAdmission=null!;
    private CityInformationListPage? publication;
    private CityInformationColumn[] columns = [];
    private int[] sourceSlots = [];
    private HashSet<int> unsortable = [];
    private int activeGroup = -1, maximum;
    private bool nextSelectionAscending = true, abandoned;
    private HashSet<int> eligible=[];
    private string selectCaption = "", clearCaption = "", defaultHint = "";
    internal IReadOnlyList<int> SelectedCityIds => pages.CurrentCityIds.Where(selected.Contains).ToArray();

    internal void Configure(GameResourceRepository repository, PreviewText text,
        DistrictCitySelection model, DistrictCityHints hints,
        Func<bool> admitted, Action<DistrictCitySelectionResult> closed, Func<int> scrollSpeed, Action<int?> hovered)
    {
        this.repository = repository; this.text = text; this.model = model; this.hints=hints; pages = model.Pages;
        this.admitted = admitted; this.closed = closed; this.hovered=hovered; maximum=pages.CurrentCityIds.Length;
        eligible=model.EligibleCityIds.ToHashSet();locked=model.LockedSelectedCityIds.ToHashSet();
        selected.AddRange(model.InitialSelection);
        foreach (var group in pages.Groups) arrows[group.Index] = (-1, false);
        Name = "DistrictCityList"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new UiSourceCommonBlur(); AddChild(backdrop); backdrop.Configure(repository);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x5B3, RequestCancel, () => Finish(DistrictCitySelectionAction.Confirm));
        defaultHint = model.GuideText;
        chrome.ApplyDefaultHint(defaultHint); chrome.ApplyReturnHint(ReadHint(DistrictCityHintKind.CancelStage));
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(chrome.Canvas, 2, shell.Rectangle(2, new(191,147)));
        shell.Art(chrome.Canvas, 3, shell.Rectangle(3, new(191,147)), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing district city caption.");
        selectCaption = Message(0x3A); clearCaption = Message(0x3B);
        Vector2 mount = shell.Rectangle(4, new(191,147)).Position;
        foreach (var group in pages.Groups)
        {
            int id = group.Index; float width = group.Caption.Length * 28 + 36;
            var tab = new UiSourceTabButton { Name="DistrictCityTab"+id, Position=mount, Size=new(width,64), InputAdmission=ReadAdmission };
            chrome.Canvas.AddChild(tab); tabs.Add(tab);
            tab.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,group.Caption,
                pages.Groups.Length==1 ? 0x9AFB27B7u : id==0 ? 0x172EDC24u : id==pages.Groups.Length-1 ? 0xD45873AAu : 0x9AFB27B7u);
            tab.Pressed += () => { if(ReadAdmission())ShowGroup(id); };
            mount.X += width-6;
        }
        headerHost = new Control { Name="DistrictCityHeaders", Position=new(213,214), Size=new(1464,48), MouseFilter=MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(headerHost);
        rowsHost = new Control { Name="DistrictCitiesViewport", Position=new(213,264), Size=new(1464,596), ClipContents=true, MouseFilter=MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(rowsHost);
        scroll = new() { Name="DistrictCityScrollbar", Position=new(1687,265), Size=new(16,597) };
        chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository,chrome.Assets,chrome.SpriteShader,Math.Max(0,pages.CurrentCityIds.Length*48-596),scrollSpeed(),
            _ => PositionRows(),currentScrollSpeed:scrollSpeed);
        var footer = new UiSourceLayout(repository,0xD8913AB2);
        selectAll = new() {Name="DistrictCitySelectAll",Position=footer.Rectangle(17,new(0,904)).Position,InputAdmission=ReadAdmission};
        chrome.Canvas.AddChild(selectAll);
        selectAll.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,selectCaption,()=>
        {
            if(!ReadAdmission())return;
            if(AnyEditableSelected())selected.RemoveAll(id=>eligible.Contains(id)&&!locked.Contains(id));
            else foreach(int id in pages.CurrentCityIds)
                if(selected.Count<maximum&&RowEnabled(id)&&!selected.Contains(id))selected.Add(id);
            SelectionChanged();
        });
        var close=new UiSourceBottomButton {Name="DistrictCityCloseList",Position=footer.Rectangle(14,new(0,904)).Position,InputAdmission=ReadAdmission};
        chrome.Canvas.AddChild(close);
        close.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,Message(0x5D),()=>Finish(DistrictCitySelectionAction.CloseList));
        close.MouseEntered+=()=>{if(ReadAdmission())ApplyHint(DistrictCityHintKind.CloseList);};
        close.MouseExited+=()=>{if(!abandoned)ApplyHint(DistrictCityHintKind.Default);};
        selectAll.MouseEntered+=()=>{if(ReadAdmission())ApplyHint(DistrictCityHintKind.SelectAll);};
        selectAll.MouseExited+=()=>{if(!abandoned)ApplyHint(DistrictCityHintKind.Default);};
        selectAll.Position=footer.Rectangle(17,new(0,904)).Position;
        currentAdmission=model.QuerySelection(SelectedCityIds);
        chrome.ApplyDecisionHint(ReadHint(DistrictCityHintKind.Decide));
        ShowGroup(0);
        SetMeta("original_district_city_columns_recovered",true);
        SetMeta("district_city_page_count",pages.Groups.Length);

    }

    private void ShowGroup(int index)
    {
        if(abandoned||activeGroup==index)return;
        var nextColumns=pages.Groups[index].Columns.Where(column=>column.Visible).ToArray();
        var nextPublication=pages.ReadPage(index);
        var slots=pages.Groups[index].Columns.Select((column,slot)=>(column.Id,slot)).ToDictionary(value=>value.Id,value=>value.slot);
        var nextSourceSlots=nextColumns.Select(column=>slots[column.Id]).ToArray();
        var nextUnsortable=nextColumns.Where((column,slot)=>!column.CanSort||nextPublication.Rows.Any(row=>row.Cells[nextSourceSlots[slot]].SortUnresolvedOwner is not null))
            .Select(column=>column.Id).ToHashSet();
        if(!directions.TryGetValue(index,out var next))directions.Add(index,next=[]);
        foreach(var column in nextColumns)next.TryAdd(column.Id,column.InitialAscending);
        var previous=(Table:activeTable,Group:activeGroup,Columns:columns,Publication:publication,Slots:sourceSlots,Unsortable:unsortable,SelectAllDisabled:selectAll.Disabled,Admission:currentAdmission,Hint:activeHint,Hover:hoveredCity);
        Variant? previousPageIssue=HasMeta("district_city_page_issue")?(Variant?)GetMeta("district_city_page_issue"):(Variant?)null;
        Variant? previousActivePage=HasMeta("district_city_active_page")?(Variant?)GetMeta("district_city_active_page"):(Variant?)null;
        Variant? previousHintIssue=HasMeta("district_city_hint_issue")?(Variant?)GetMeta("district_city_hint_issue"):(Variant?)null;
        Variant? previousSelectionIssue=HasMeta("district_city_selection_issue")?(Variant?)GetMeta("district_city_selection_issue"):(Variant?)null;
        bool created=false;
        if(!tables.TryGetValue(index,out var table))
        {
            table=CreateTable(index,nextColumns,nextPublication.Rows.Length);
            created=true;
        }
        try
        {
            // A hidden page cannot publish stale row events while the next page binds.
            activeTable=null;previous.Table?.SetActive(false);
            activeTable=table;activeGroup=index;columns=nextColumns;
            publication=nextPublication;sourceSlots=nextSourceSlots;unsortable=nextUnsortable;
            PublishHover(null);
            currentAdmission=model.QuerySelection(SelectedCityIds);
            chrome.ApplyDecisionHint(ReadHint(DistrictCityHintKind.Decide));
            foreach(var tab in tabs)tab.Selected=tabs.IndexOf(tab)==index;
            foreach(var (column,heading) in table.Headers)
                heading.ApplySortDirection(arrows.TryGetValue(index,out var arrow)&&arrow.Column==column ? arrow.Ascending : null);
            PositionRows();RefreshSelection();
            table.SetActive(true);
            PublishHover(ReadAdmission()?table.Items.FirstOrDefault(row=>row.Button.IsVisibleInTree()&&row.Button.IsHovered())?.Entry?.CityId:null);
            if(publication.UnresolvedOwner is { } missing){chrome.ApplyDefaultHint(defaultHint);SetMeta("district_city_page_issue",missing);}
            else {chrome.ApplyDefaultHint(defaultHint);if(HasMeta("district_city_page_issue"))RemoveMeta("district_city_page_issue");}
            SetMeta("district_city_active_page",index);

            // Only a fully bound first visit becomes a retained page.
            if(created)tables.Add(index,table);
        }
        catch
        {
            bool restoreHover=hoveredCity!=previous.Hover;
            activeTable=null;
            activeGroup=previous.Group;columns=previous.Columns;publication=previous.Publication;
            sourceSlots=previous.Slots;unsortable=previous.Unsortable;
            currentAdmission=previous.Admission;activeHint=previous.Hint;hoveredCity=previous.Hover;
            // Cleanup or presentation callbacks must not replace the original binding error.
            try { table.SetActive(false); } catch { }
            if(created)DiscardTable(table);
            activeTable=previous.Table;
            try
            {
                foreach(var tab in tabs)tab.Selected=tabs.IndexOf(tab)==activeGroup;
                selectAll.Disabled=previous.SelectAllDisabled;
                selectAll.ApplyCaption(previous.Table is not null&&AnyEditableSelected()?clearCaption:selectCaption);
                chrome.ApplyReturn(true);
                chrome.ApplyDecision(previous.Table is not null&&currentAdmission.CanConfirm==true&&SelectedCityIds.Count>0,true);

                chrome.ApplyDefaultHint(defaultHint);
                chrome.ApplyDecisionHint(ReadHint(DistrictCityHintKind.Decide));
                ApplyHint(previous.Hint);

            }
            catch { }
            try { previous.Table?.SetActive(true); } catch { }
            if(restoreHover)try { hovered?.Invoke(previous.Hover); } catch { }
            try
            {
                if(previousPageIssue is { } pageIssue)SetMeta("district_city_page_issue",pageIssue);
                else if(HasMeta("district_city_page_issue"))RemoveMeta("district_city_page_issue");
                if(previousActivePage is { } activePage)SetMeta("district_city_active_page",activePage);
                else if(HasMeta("district_city_active_page"))RemoveMeta("district_city_active_page");
                if(previousHintIssue is { } hintIssue)SetMeta("district_city_hint_issue",hintIssue);
                else if(HasMeta("district_city_hint_issue"))RemoveMeta("district_city_hint_issue");
                if(previousSelectionIssue is { } selectionIssue)SetMeta("district_city_selection_issue",selectionIssue);
                else if(HasMeta("district_city_selection_issue"))RemoveMeta("district_city_selection_issue");
            }
            catch { }
            throw;
        }
    }

    private UiSourceTablePage<RowDisplay> CreateTable(int index, CityInformationColumn[] columns, int rowCount)
    {
        var pageHeaders=new Control {Name="DistrictCityHeadersPage"+index,Size=headerHost.Size,MouseFilter=MouseFilterEnum.Pass};
        var pageRows=new Control {Name="DistrictCityRowsPage"+index,Size=rowsHost.Size,MouseFilter=MouseFilterEnum.Pass};
        var table=new UiSourceTablePage<RowDisplay>(pageHeaders,pageRows);
        table.SetActive(false);
        try
        {
            headerHost.AddChild(pageHeaders);rowsHost.AddChild(pageRows);
            pageRows.GuiInput+=input=>{if(ReferenceEquals(activeTable,table)&&ReadAdmission())scroll.HandleWheel(input);};
            BuildTable(table,columns,rowCount);
            return table;
        }
        catch
        {
            DiscardTable(table);
            throw;
        }
    }

    private static void DiscardTable(UiSourceTablePage<RowDisplay> table)
    {
        // Each host owns its partial children, including a control whose Configure failed.
        foreach(var host in new[] {table.HeaderHost,table.RowsHost})
        {
            try { host.GetParent()?.RemoveChild(host); } catch { }
            try { host.QueueFree(); } catch { }
        }
    }

    private void BuildTable(UiSourceTablePage<RowDisplay> table, CityInformationColumn[] columns, int rowCount)
    {
        var headerHost=table.HeaderHost;var rowsHost=table.RowsHost;
        var headers=table.Headers;var pool=table.Items;
        int[] bodyWidths=BodyWidths(columns);float factor=WidthFactor(columns);int x=48;
        AddHeader(-1,Message(0x628),0,48);
        for(int slot=0;slot<columns.Length;slot++)
        {
            var column=columns[slot];
            int headerWidth=slot==columns.Length-1&&factor<1 ? 1465-x : (int)((column.Width-20)*factor+20);
            AddHeader(column.Id,column.Caption,x,headerWidth);x+=headerWidth;
        }
        var rowColumns=columns.Select((column,index)=>new DeploymentPersonColumn(column.Id,bodyWidths[index],column.CaptionMessageId,
            (byte)(((column.CellFormat-3)<<4)|1),(byte)(column.CellFormat-2),column.InitialAscending)).ToArray();
        for(int i=0;i<Math.Min(14,rowCount);i++)
        {
            var button=new UiSourcePersonRow {Name="DistrictCityVisibleRow"+i,InputAdmission=()=>ReferenceEquals(activeTable,table)&&ReadAdmission()};
            rowsHost.AddChild(button);
            var display=new RowDisplay(button);pool.Add(display);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,Message(0x628),rowColumns,
                columns.Select(_=>new DeploymentPersonCell("")).ToArray(),1464);
            button.GuiInput+=input=>{if(ReferenceEquals(activeTable,table)&&ReadAdmission())scroll.HandleWheel(input);};
            button.Pressed+=()=>
            {
                if(!ReferenceEquals(activeTable,table)||!ReadAdmission()||display.Entry is not { } row||button.Disabled)return;
                if(!selected.Remove(row.CityId)&&selected.Count<maximum)selected.Add(row.CityId);
                SelectionChanged();
            };
            button.MouseEntered+=()=>{if(ReferenceEquals(activeTable,table)&&ReadAdmission())PublishHover(display.Entry?.CityId);};
            button.MouseExited+=()=>{if(ReferenceEquals(activeTable,table)&&hoveredCity==display.Entry?.CityId)PublishHover(null);};
        }

        void AddHeader(int id,string caption,int left,int width)
        {
            var button=new UiSourceColumnButton {Name="DistrictCityColumn"+id,Position=new(left,0),Size=new(width,48),InputAdmission=()=>ReferenceEquals(activeTable,table)&&ReadAdmission()};
            headerHost.AddChild(button);headers.Add(id,button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,caption,personList:true);
            button.Pressed+=()=>{if(ReferenceEquals(activeTable,table)&&ReadAdmission())SortColumn(id);};
        }
    }
    private void SortColumn(int column)
    {
        if(!ReadAdmission()||publication is null||activeTable is not { } table||table.Headers[column].Disabled)return;
        bool ascending;
        if(column<0)
        {
            ascending=nextSelectionAscending;
            pages.SortSelection(selected,selectedFirst:!ascending);nextSelectionAscending=!ascending;
            foreach(var group in pages.Groups)arrows[group.Index]=(-1,ascending);
        }
        else
        {
            ascending=directions[activeGroup][column];
            var sorted=pages.Sort(activeGroup,column,ascending);
            if(!sorted.Available){chrome.ApplyDefaultHint(text.Get("detail_unknown"));SetMeta("district_city_sort_issue",sorted.UnresolvedInput??"");return;}
            directions[activeGroup][column]=!ascending;
            foreach(var group in pages.Groups)arrows[group.Index]=group.Index==activeGroup ? (column,ascending) : (-2,false);
        }
        publication=pages.ReadPage(activeGroup);
        foreach(var (id,heading) in table.Headers)heading.ApplySortDirection(id==column?ascending:null);
        PositionRows();RefreshSelection();
    }
    private void PositionRows()
    {
        if(publication is null||activeTable is not { } table)return;
        var pool=table.Items;
        int first=scroll.Value/48;
        for(int slot=0;slot<pool.Count;slot++)
        {
            var display=pool[slot];int index=first+slot;
            if(index>=publication.Rows.Length){display.Entry=null;display.Button.Hide();continue;}
            var row=publication.Rows[index];display.Entry=row;
            display.Button.Position=new(0,index*48-scroll.Value);display.Button.Show();
            display.Button.ApplyCells(columns.Select((column,slot)=>
            {
                var cell=row.Cells[sourceSlots[slot]];
                return new DeploymentPersonCell(cell.UnresolvedOwner is null ? cell.Text??"" : text.Get("detail_unknown"),
                    Emphasized:cell.PresetIndex==65,FirstGlyphColour:cell.MarkerColour?.CpuPacked,
                    SourcePreset:cell.PresetIndex==65?null:cell.PresetIndex);
            }).ToArray());
        }
        RefreshSelection();
        PublishHover(ReadAdmission()?pool.FirstOrDefault(row=>row.Button.IsVisibleInTree()&&row.Button.IsHovered())?.Entry?.CityId:null);
    }
    private void SelectionChanged()
    {
        currentAdmission=model.QuerySelection(SelectedCityIds);
        chrome.ApplyDecisionHint(ReadHint(DistrictCityHintKind.Decide));
        ApplyHint(activeHint);RefreshSelection();
    }
    private bool AnyEditableSelected()=>selected.Any(id=>eligible.Contains(id)&&!locked.Contains(id));
    private void PublishHover(int? id)
    {
        if(hoveredCity==id)return;hoveredCity=id;hovered?.Invoke(id);
        if(!abandoned)ApplyHint(id is null?DistrictCityHintKind.Default:DistrictCityHintKind.City);
    }
    private string ReadHint(DistrictCityHintKind kind)
    {
        var query=hints.QueryHint(kind,hoveredCity,SelectedCityIds);
        if(query.Available&&query.Value is {} value)return value;
        SetMeta("district_city_hint_issue",query.UnresolvedInput??"district.city.hint");
        return text.Get("information_partial");
    }
    private void ApplyHint(DistrictCityHintKind kind)
    {
        if(abandoned)return;activeHint=kind;chrome.ApplyHoverHint(ReadHint(kind));
    }
    private void RefreshSelection()
    {
        if(activeTable is not { } table)return;
        foreach(var display in table.Items)
            if(display.Entry is { } row){display.Button.Selected=selected.Contains(row.CityId);display.Button.Disabled=!RowEnabled(row.CityId)||locked.Contains(row.CityId)||!display.Button.Selected&&selected.Count>=maximum;}
        foreach(var (id,heading) in table.Headers)
            heading.Disabled=unsortable.Contains(id);
        if(selectAll is not null){selectAll.Disabled=EligibleCount()==0;selectAll.ApplyCaption(AnyEditableSelected()?clearCaption:selectCaption);}
        chrome.ApplyReturn(true);
        chrome.ApplyDecision(currentAdmission.CanConfirm==true&&SelectedCityIds.Count>0,true);
        if(currentAdmission.UnresolvedOwner is {} missing)SetMeta("district_city_selection_issue",missing);
        else if(HasMeta("district_city_selection_issue"))RemoveMeta("district_city_selection_issue");
    }
    private static float WidthFactor(IReadOnlyList<CityInformationColumn> columns)
    {
        int total=48+columns.Sum(column=>column.Width);
        return total<1465 ? 1 : (1465-(48+20*columns.Count))/(float)columns.Sum(column=>column.Width-20);
    }
    private static int[] BodyWidths(IReadOnlyList<CityInformationColumn> columns)
    {float factor=WidthFactor(columns);return columns.Select(column=>20+(int)((column.Width-20)*factor)).ToArray();}
    private string Message(int id)=>repository.LoadMessages(text.SourceLocale,San14.Data.Formats.MessageKind.Data).Messages.Sections[1].Records[id].Text
        ??throw new InvalidDataException("Missing district city caption.");
    private bool ReadAdmission()=>!abandoned&&closed is not null&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    internal void RequestCancel()=>Finish(DistrictCitySelectionAction.CancelStage);
    private bool RowEnabled(int id)=>eligible.Contains(id)&&!locked.Contains(id);
    private int EligibleCount()=>pages.CurrentCityIds.Count(RowEnabled);
    private void Finish(DistrictCitySelectionAction action)
    {
        if(!ReadAdmission()||action==DistrictCitySelectionAction.Confirm&&
            (SelectedCityIds.Count==0||currentAdmission.CanConfirm!=true))return;
        // The Main owner retires this child after its real dispatcher admits
        // the event. A temporarily blocked play action keeps a usable list.
        PublishHover(null);
        closed?.Invoke(new(action,SelectedCityIds));
    }
    public override void _ExitTree()=>Abandon();
    internal void Abandon()
    {
        if(abandoned)return;abandoned=true;closed=null;admitted=null;PublishHover(null);hovered=null;
        activeTable=null;
        foreach(var table in tables.Values)
        {
            table.SetActive(false);
            foreach(var display in table.Items){display.Button.InputAdmission=null;display.Button.Disabled=true;}
            foreach(var header in table.Headers.Values){header.InputAdmission=null;header.Disabled=true;}
        }
        foreach(var tab in tabs){tab.InputAdmission=null;tab.Disabled=true;}
        if(selectAll is not null){selectAll.InputAdmission=null;selectAll.Disabled=true;}
        scroll?.SetProcessInput(false);chrome?.Abandon();Hide();
    }
}
