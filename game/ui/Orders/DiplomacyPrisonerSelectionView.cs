using Godot;
using San14.Data;

/// <summary>The return-captive selector retains its three original tabs and
/// ordered checked chain; accepting returns to the diplomacy draft.</summary>
internal partial class DiplomacyPrisonerSelectionView : Control
{
    private sealed record RowDisplay(UiSourcePersonRow Button) { internal OfficerInformationRow? Entry; }
    private AdministrationPageChrome chrome = null!;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private OfficerInformationPageSet pages = null!;
    private IReadOnlyDictionary<int,AreaAdministrationQuery<bool>> alteredHealth = null!;
    private Control headerHost = null!, rowsHost = null!;
    private UiSourceScrollbar scroll = null!;
    private UiSourceBottomButton selectAll = null!;
    private readonly List<RowDisplay> pool = [];
    private readonly List<int> selected = [];
    private readonly Dictionary<int, UiSourceColumnButton> headers = [];
    private readonly Dictionary<int, Dictionary<int, bool>> directions = [];
    private readonly Dictionary<int, (int Column, bool Ascending)> arrows = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private Func<bool>? admitted;
    private Func<int,bool>? rowAdmission;
    private bool returnAllowed=true;
    private int eligibleCount;
    private Action<IReadOnlyList<int>?>? closed;
    private OfficerInformationPage? publication;
    private OfficerInformationColumn[] columns = [];
    private int[] sourceSlots = [];
    private HashSet<int> unsortable = [];
    private int activeGroup = -1, maximum;
    private bool nextSelectionAscending = true, abandoned, busy;
    private string selectCaption = "", clearCaption = "", defaultHint = "";
    internal IReadOnlyList<int> SelectedOfficerIds => selected.AsReadOnly();

    internal void Configure(GameResourceRepository repository, PreviewText text,
        AdministrationPageChrome.Resources resources, DiplomacyPrisonerSelection model,
        Func<bool> admitted, Action<IReadOnlyList<int>?> closed, Func<int> scrollSpeed)
    {
        this.repository = repository; this.text = text; pages = model.Pages; alteredHealth=model.AlteredHealth;
        SetMeta("source_row_health_issue",alteredHealth.Values.FirstOrDefault(value=>!value.Available)?.UnresolvedInput ?? "");
        this.admitted = admitted; this.closed = closed; maximum = model.MaximumSelected;
        selected.AddRange(model.InitialSelection); eligibleCount=pages.CurrentOfficerIds.Length;
        foreach (var group in pages.Groups) arrows[group.Index] = (-1, false);
        Name = "DiplomacyPrisonerSelection"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new UiSourceCommonBlur(); AddChild(backdrop); backdrop.Configure(repository);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x058E, RequestCancel, () => Finish(selected.ToArray()), resources: resources);
        var narrator = new AdministrationMessages(repository, text.SourceLocale);
        defaultHint = model.GuideText;
        chrome.ApplyDefaultHint(defaultHint); chrome.ApplyReturnHint(narrator.Format(0x2733));
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(chrome.Canvas, 2, shell.Rectangle(2, new(191,147)));
        shell.Art(chrome.Canvas, 3, shell.Rectangle(3, new(191,147)), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing diplomacy prisoner caption.");
        selectCaption = Message(0x3A); clearCaption = Message(0x3B);
        Vector2 mount = shell.Rectangle(4, new(191,147)).Position;
        foreach (var group in pages.Groups)
        {
            int id = group.Index; float width = group.Caption.Length * 28 + 36;
            var tab = new UiSourceTabButton { Name="DiplomacyPrisonerPersonTab"+id, Position=mount, Size=new(width,64), InputAdmission=ReadAdmission };
            chrome.Canvas.AddChild(tab); tabs.Add(tab);
            tab.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,group.Caption,
                id==0 ? 0x172EDC24u : id==pages.Groups.Length-1 ? 0xD45873AAu : 0x9AFB27B7u);
            tab.Pressed += () => { if(ReadAdmission())ShowGroup(id); };
            mount.X += width-6;
        }
        headerHost = new Control { Name="DiplomacyPrisonerPersonHeaders", Position=new(213,214), Size=new(1464,48), MouseFilter=MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(headerHost);
        rowsHost = new Control { Name="DiplomacyPrisonerPersonsViewport", Position=new(213,264), Size=new(1464,596), ClipContents=true, MouseFilter=MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(rowsHost);
        scroll = new() { Name="DiplomacyPrisonerPersonScrollbar", Position=new(1687,265), Size=new(16,597) };
        chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository,chrome.Assets,chrome.SpriteShader,Math.Max(0,pages.CurrentOfficerIds.Length*48-596),scrollSpeed(),
            _ => PositionRows(),currentScrollSpeed:scrollSpeed);
        rowsHost.GuiInput += input => {if(ReadAdmission())scroll.HandleWheel(input);};
        var footer = new UiSourceLayout(repository,0xD8913AB2);
        selectAll = new() {Name="DiplomacyPrisonerSelectAll",Position=footer.Rectangle(14,new(0,904)).Position,InputAdmission=ReadAdmission};
        chrome.Canvas.AddChild(selectAll);
        selectAll.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,selectCaption,()=>
        {
            if(!ReadAdmission())return;
            if(selected.Count>=maximum || selected.Count>=eligibleCount)selected.Clear();
            else foreach(int id in pages.CurrentOfficerIds)
                if(selected.Count<maximum&&!selected.Contains(id)&&RowAllowed(id))selected.Add(id);
            RefreshSelection();
        });
        ShowGroup(0);
        SetMeta("original_diplomacy_prisoner_columns_recovered",true);
        SetMeta("diplomacy_prisoner_page_count",pages.Groups.Length);
    }

    private void ShowGroup(int index)
    {
        if(abandoned||activeGroup==index)return;
        foreach(var heading in headers.Values){heading.InputAdmission=null;heading.Disabled=true;}
        headers.Clear();
        foreach(var display in pool){display.Button.InputAdmission=null;display.Button.Disabled=true;}
        pool.Clear();Clear(headerHost);Clear(rowsHost);
        activeGroup=index;columns=pages.Groups[index].Columns.Where(column=>column.Visible).ToArray();
        publication=pages.ReadPage(index);
        var slots=pages.Groups[index].Columns.Select((column,slot)=>(column.Id,slot)).ToDictionary(value=>value.Id,value=>value.slot);
        sourceSlots=columns.Select(column=>slots[column.Id]).ToArray();
        unsortable=columns.Where((column,slot)=>!column.CanSort||publication.Rows.Any(row=>row.Cells[sourceSlots[slot]].SortUnresolvedOwner is not null))
            .Select(column=>column.Id).ToHashSet();
        if(!directions.TryGetValue(index,out var next))directions.Add(index,next=[]);
        foreach(var column in columns)next.TryAdd(column.Id,column.InitialAscending);
        foreach(var tab in tabs)tab.Selected=tabs.IndexOf(tab)==index;
        int[] bodyWidths=columns.Select(column=>column.Width).ToArray();int x=48;
        AddHeader(-1,Message(0x628),0,48);
        for(int slot=0;slot<columns.Length;slot++)
        {
            var column=columns[slot];
            int headerWidth=column.Width;
            AddHeader(column.Id,column.Caption,x,headerWidth);x+=headerWidth;
        }
        var rowColumns=columns.Select((column,index)=>new DeploymentPersonColumn(column.Id,bodyWidths[index],column.CaptionMessageId,
            (byte)(((column.CellFormat-3)<<4)|1),(byte)(column.CellFormat-2),column.InitialAscending)).ToArray();
        for(int i=0;i<Math.Min(14,publication.Rows.Length);i++)
        {
            var button=new UiSourcePersonRow {Name="DiplomacyPrisonerVisiblePersonRow"+i,InputAdmission=ReadAdmission};
            rowsHost.AddChild(button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,Message(0x628),rowColumns,
                columns.Select(_=>new DeploymentPersonCell("")).ToArray(),1464);
            var display=new RowDisplay(button);pool.Add(display);
            button.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
            button.Pressed+=()=>
            {
                if(!ReadAdmission()||display.Entry is not { } row||button.Disabled)return;
                if(!selected.Remove(row.OfficerId)&&selected.Count<maximum)selected.Add(row.OfficerId);
                RefreshSelection();
            };
        }
        foreach(var (column,heading) in headers)
            heading.ApplySortDirection(arrows.TryGetValue(index,out var arrow)&&arrow.Column==column ? arrow.Ascending : null);
        PositionRows();RefreshSelection();
        if(publication.UnresolvedOwner is { } missing){chrome.ApplyDefaultHint(defaultHint);SetMeta("diplomacy_prisoner_page_issue",missing);}
        else {chrome.ApplyDefaultHint(defaultHint);if(HasMeta("diplomacy_prisoner_page_issue"))RemoveMeta("diplomacy_prisoner_page_issue");}
        SetMeta("diplomacy_prisoner_active_page",index);

        void AddHeader(int id,string caption,int left,int width)
        {
            var button=new UiSourceColumnButton {Name="DiplomacyPrisonerPersonColumn"+id,Position=new(left,0),Size=new(width,48),InputAdmission=ReadAdmission};
            headerHost.AddChild(button);headers.Add(id,button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,caption,personList:true);
            button.Pressed+=()=>SortColumn(id);
        }
    }
    private void SortColumn(int column)
    {
        if(!ReadAdmission()||publication is null||headers[column].Disabled)return;
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
            if(!sorted.Available){chrome.ApplyDefaultHint(text.Get("detail_unknown"));SetMeta("diplomacy_prisoner_sort_issue",sorted.UnresolvedInput??"");return;}
            directions[activeGroup][column]=!ascending;
            foreach(var group in pages.Groups)arrows[group.Index]=group.Index==activeGroup ? (column,ascending) : (-2,false);
        }
        publication=pages.ReadPage(activeGroup);
        foreach(var (id,heading) in headers)heading.ApplySortDirection(id==column?ascending:null);
        PositionRows();RefreshSelection();
    }
    private void PositionRows()
    {
        if(publication is null)return;
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
                    Emphasized:cell.StatePresets is not null||cell.PresetIndex==65,FirstGlyphColour:cell.MarkerColour?.CpuPacked,
                    SourcePreset:cell.PresetIndex==65?null:cell.PresetIndex,
                    SourceFormattedText:cell.UnresolvedOwner is null?cell.SourceFormattedText:null);
            }).ToArray());
            var health=alteredHealth[row.OfficerId];
            display.Button.ApplyHealthStyle(health.Available ? health.Value : null);

        }
        RefreshSelection();
    }
    private void RefreshSelection()
    {
        foreach(var display in pool)
            if(display.Entry is { } row){display.Button.Selected=selected.Contains(row.OfficerId);display.Button.Disabled=busy||!RowAllowed(row.OfficerId)||!display.Button.Selected&&selected.Count>=maximum;}
        foreach(var (id,heading) in headers)
            heading.Disabled=busy||unsortable.Contains(id);
        if(selectAll is not null){selectAll.Disabled=busy||maximum==0||eligibleCount==0;selectAll.ApplyCaption(selected.Count>=maximum||selected.Count>=eligibleCount?clearCaption:selectCaption);}
        chrome.ApplyReturn(!busy&&returnAllowed);chrome.ApplyDecision(!busy,true);
    }
    private string Message(int id)=>repository.LoadMessages(text.SourceLocale,San14.Data.Formats.MessageKind.Data).Messages.Sections[1].Records[id].Text
        ??throw new InvalidDataException("Missing diplomacy prisoner caption.");
    private static void Clear(Control parent){foreach(Node child in parent.GetChildren()){parent.RemoveChild(child);child.QueueFree();}}
    private bool ReadAdmission()=>!abandoned&&!busy&&closed is not null&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    internal void SetBusy(bool value){if(!abandoned){busy=value;RefreshSelection();}}
    private bool RowAllowed(int id)=>rowAdmission?.Invoke(id)??true;
    internal void ApplySelectionAdmission(Func<int,bool>? allowed,bool canReturn)
    {
        if(abandoned)return;rowAdmission=allowed;returnAllowed=canReturn;
        selected.RemoveAll(id=>!RowAllowed(id));
        eligibleCount=pages.CurrentOfficerIds.Count(RowAllowed);RefreshSelection();
    }
    internal void RequestCancel(){if(returnAllowed)Finish(null);}
    private void Finish(IReadOnlyList<int>? value){if(!ReadAdmission()||value is not null && value.Any(id=>!RowAllowed(id)))return;var callback=closed;closed=null;admitted=null;Hide();callback?.Invoke(value);}
    internal void Abandon()
    {
        if(abandoned)return;abandoned=true;closed=null;admitted=null;rowAdmission=null;
        foreach(var display in pool){display.Button.InputAdmission=null;display.Button.Disabled=true;}
        foreach(var header in headers.Values){header.InputAdmission=null;header.Disabled=true;}
        foreach(var tab in tabs){tab.InputAdmission=null;tab.Disabled=true;}
        if(selectAll is not null){selectAll.InputAdmission=null;selectAll.Disabled=true;}
        scroll?.SetProcessInput(false);chrome?.Abandon();Hide();
    }
}
