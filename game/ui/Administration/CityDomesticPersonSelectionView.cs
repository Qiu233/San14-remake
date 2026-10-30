using Godot;
using San14.Data;

internal sealed record CityDomesticPersonEntry(int Occurrence, CityDomesticCandidate Candidate,
    IReadOnlyList<DeploymentPersonCell> Cells, bool? AlteredHealth)
{
    internal string? StyleUnresolvedInput { get; init; }
}

internal sealed class CityDomesticPersonPageSortState
{
    internal readonly Dictionary<int, bool> NextAscending = [];
    internal int? ActiveColumn;
    internal bool? ActiveAscending;
}

/// <summary>One role retains one occurrence order and scroll across its six
/// pages. Ordinary sort directions belong to their original pages.</summary>
internal sealed class CityDomesticPersonListState
{
    internal IReadOnlyList<CityDomesticCandidate>? Candidates;
    internal readonly List<int> Order = [];
    internal readonly CityDomesticPersonPageSortState[] Pages =
        Enumerable.Range(0, CityDomesticPersonColumns.Captions.Count).Select(_ => new CityDomesticPersonPageSortState()).ToArray();
    internal int Scroll, ActivePage;
}

/// <summary>Immediate single-person selection. Every page uses the same
/// publication occurrences; only the host stages a selected physical receiver.</summary>
internal partial class CityDomesticPersonSelectionView : Control
{
    private sealed class Row(UiSourceListRow button)
    {
        internal UiSourceListRow Button { get; } = button;
        internal Dictionary<int,UiSourceText> Labels { get; } = [];
        internal CityDomesticPersonEntry? Entry;
    }
    private AdministrationPageChrome chrome = null!;
    private GameResourceRepository repository = null!;
    private CityDomesticPersonListState state = null!;
    private CityDomesticDuty duty;
    private UiSourceScrollbar scroll = null!;
    private Control headersRoot = null!, body = null!;
    private readonly List<Row> pool = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private readonly Dictionary<int,UiSourceColumnButton> headers = [];
    private readonly MapTextPresets presets = MapTextPresets.Create();
    private IReadOnlyList<DeploymentPersonColumn> columns = [];
    private CityDomesticPersonEntry[] entries = [];
    private Func<int,IReadOnlyList<int>,int,bool,AreaAdministrationQuery<IReadOnlyList<int>>>? order;
    private Func<int,IReadOnlyList<int>,AreaAdministrationQuery<IReadOnlyList<CityDomesticPersonEntry>>>? queryPage;
    private Action<CityDomesticCandidate?>? complete;
    private Action<string> unavailable = null!;
    private Func<bool>? admitted;
    private int? selectedOccurrence;
    private string locale = "";
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, CityDomesticDuty duty,
        IReadOnlyList<DeploymentPersonColumn> columns, IReadOnlyList<CityDomesticPersonEntry> entries,
        CityDomesticPersonListState state, int? preselectedOccurrence, int scrollSpeed,
        Func<int,IReadOnlyList<int>,int,bool,AreaAdministrationQuery<IReadOnlyList<int>>> order,
        Action<CityDomesticCandidate?> complete, Action<string> unavailable,
        Func<int,IReadOnlyList<int>,AreaAdministrationQuery<IReadOnlyList<CityDomesticPersonEntry>>>? queryPage = null,
        Func<int>? currentScrollSpeed = null, UiSourceCommonBlur? suppliedBackdrop = null, Func<bool>? admitted = null)
    {
        this.repository=repository; this.duty=duty; this.state=state; this.order=order;
        this.complete=complete; this.unavailable=unavailable; this.queryPage=queryPage; this.admitted=admitted;
        locale=text.SourceLocale;
        if(!columns.SequenceEqual(CityDomesticPersonColumns.For(duty,state.ActivePage)))
            throw new InvalidDataException("City active-page columns do not match the role preset.");
        var original=entries.OrderBy(e=>e.Occurrence).ToArray();
        if(state.Candidates is null)
        {
            state.Candidates=original.Select(e=>e.Candidate).ToArray(); state.Order.AddRange(original.Select(e=>e.Occurrence));
            for(int page=0;page<state.Pages.Length;page++)
                foreach(var column in CityDomesticPersonColumns.For(duty,page))
                    state.Pages[page].NextAscending.Add(column.Id,column.InitialAscending);
        }
        else if(!state.Candidates.SequenceEqual(original.Select(e=>e.Candidate)))
            throw new InvalidDataException("City person publication changed its retained physical candidates.");
        ValidateOrder(state.Order,state.Candidates.Count);
        selectedOccurrence=preselectedOccurrence is int chosen ? FirstPhysical(state.Candidates[chosen].PhysicalSlot) : null;
        Name="CityPersonSelection";ZIndex=4;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if(suppliedBackdrop is null)
        {var backdrop=new UiSourceCommonBlur();AddChild(backdrop);backdrop.Configure(repository);}
        else if(!GodotObject.IsInstanceValid(suppliedBackdrop)||suppliedBackdrop.IsQueuedForDeletion()||!suppliedBackdrop.IsInsideTree())
            throw new InvalidOperationException("The supplied city person background has retired.");
        chrome=new();AddChild(chrome);
        chrome.Configure(repository,text,0x032E,RequestCancel,()=>{},returnOnly:true);
        var hints=new AdministrationMessages(repository,locale);
        chrome.ApplyDefaultHint(hints.Format(duty==CityDomesticDuty.Recruiter?0x575C:0x5774));
        chrome.ApplyReturnHint(hints.Format(0x2733));
        var shell=new UiSourceLayout(repository,0xA3B4A82E);
        shell.Blur(chrome.Canvas,2,shell.Rectangle(2,new(191,147)));
        shell.Art(chrome.Canvas,3,shell.Rectangle(3,new(191,147)),chrome.Assets,chrome.SpriteShader);
        var messages=repository.LoadMessages(locale,San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
        float tabX=230;
        for(int i=0;i<CityDomesticPersonColumns.Captions.Count;i++)
        {
            int page=i;string caption=messages.Records[CityDomesticPersonColumns.Captions[i]].Text
                ??throw new InvalidDataException("Missing City person tab caption.");
            float width=caption.Length*28+36;
            var tab=new UiSourceTabButton{Name="CityPersonTab"+i,Position=new(tabX,147),Size=new(width,64),InputAdmission=ReadAdmission};
            chrome.Canvas.AddChild(tab);tabs.Add(tab);
            tab.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,caption,
                i==0?0x172EDC24u:i==CityDomesticPersonColumns.Captions.Count-1?0xD45873AAu:0x9AFB27B7u);
            tab.Pressed+=()=>SwitchPage(page);tabX+=width-6;
        }
        headersRoot=new(){Name="CityPersonHeaders",Position=new(213,214),Size=new(1464,48),MouseFilter=MouseFilterEnum.Pass};
        body=new(){Name="PersonListViewport",Position=new(213,264),Size=new(1464,596),ClipContents=true,MouseFilter=MouseFilterEnum.Pass};
        chrome.Canvas.AddChild(headersRoot);chrome.Canvas.AddChild(body);
        int initialScroll=state.Scroll;
        scroll=new(){Name="PersonScrollbar",Position=new(1687,265),Size=new(16,597)};chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository,chrome.Assets,chrome.SpriteShader,Math.Max(0,entries.Count*48-596),scrollSpeed,
            value=>{state.Scroll=value;PositionRows();},currentScrollSpeed:currentScrollSpeed);
        body.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
        for(int i=0;i<Math.Min(14,entries.Count);i++)
        {
            var button=new UiSourceListRow{Name="CityPersonVisibleRow"+i,InputAdmission=ReadAdmission,
                ActionMode=BaseButton.ActionModeEnum.Release};
            body.AddChild(button);button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,"",1464,false);
            var row=new Row(button);pool.Add(row);
            button.TextVisualStateChanged+=_=>ApplyCells(row);
            button.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
            button.Pressed+=()=>{if(ReadAdmission()&&row.Entry is {} entry)Finish(entry.Candidate);};
        }
        PublishPage(state.ActivePage,entries);scroll.SetValue(initialScroll);PositionRows();
        SetMeta("source_selection_mode",1);SetMeta("source_maximum_selection",1);SetMeta("source_minimum_selection",1);
    }
    private bool ReadAdmission()=>!abandoned&&complete is not null&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??true);
    private int? FirstPhysical(int physical)=>state.Order.Where(o=>state.Candidates![o].PhysicalSlot==physical).Select(o=>(int?)o).FirstOrDefault();
    private void SwitchPage(int page)
    {
        if(!ReadAdmission()||page==state.ActivePage||queryPage is null)return;
        var callback=complete;var query=queryPage(page,state.Order.ToArray());
        if(complete!=callback||!ReadAdmission())return;
        if(!query.Available){unavailable(query.UnresolvedInput!);return;}
        PublishPage(page,query.Value!);
    }
    private void PublishPage(int page,IReadOnlyList<CityDomesticPersonEntry> publication)
    {
        columns=CityDomesticPersonColumns.For(duty,page);
        var original=publication.OrderBy(e=>e.Occurrence).ToArray();
        if(original.Length!=state.Candidates!.Count||original.Where((entry,index)=>entry.Occurrence!=index||entry.Candidate!=state.Candidates[index]||entry.Cells.Count!=columns.Count).Any())
            throw new InvalidDataException("City page rows do not match their role publication and visible columns.");
        entries=original;state.ActivePage=page;
        foreach(Node child in headersRoot.GetChildren()){headersRoot.RemoveChild(child);child.QueueFree();}
        headers.Clear();int x=0;
        var messages=repository.LoadMessages(locale,San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
        foreach(var column in columns)
        {
            int id=column.Id;
            var header=new UiSourceColumnButton{Name="CityPersonHeader"+id,Position=new(x,0),Size=new(column.Width,48),InputAdmission=ReadAdmission};
            headersRoot.AddChild(header);headers.Add(id,header);
            header.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,
                messages.Records[column.CaptionMessage].Text??throw new InvalidDataException("Missing City column caption."),personList:true);
            header.Pressed+=()=>Sort(id);x+=column.Width;
        }
        for(int i=0;i<tabs.Count;i++)tabs[i].Selected=i==page;
        ApplyArrows();PositionRows();
        string? styleIssue=entries.Select(entry=>entry.StyleUnresolvedInput).FirstOrDefault(issue=>issue is not null);
        SetMeta("city_person_style_issue",styleIssue ?? "");
        if(styleIssue is not null)unavailable(styleIssue);
        else if(entries.Any(e=>e.AlteredHealth is null))unavailable("city-domestic.person_row_style");
    }
    private void Sort(int column)
    {
        if(!ReadAdmission())return;
        var page=state.Pages[state.ActivePage];bool ascending=page.NextAscending[column];var callback=complete;
        var query=order!(state.ActivePage,state.Order.ToArray(),column,ascending);
        if(complete!=callback||!ReadAdmission())return;
        if(!query.Available){unavailable(query.UnresolvedInput!);return;}
        var sorted=query.Value!;ValidateOrder(sorted,state.Candidates!.Count);
        int? physical=selectedOccurrence is int old?state.Candidates[old].PhysicalSlot:null;
        state.Order.Clear();state.Order.AddRange(sorted);
        selectedOccurrence=physical is int receiver?FirstPhysical(receiver):null;
        page.NextAscending[column]=!ascending;
        foreach(var other in state.Pages){other.ActiveColumn=other==page?column:null;other.ActiveAscending=other==page?ascending:null;}
        ApplyArrows();PositionRows();
    }
    private static void ValidateOrder(IReadOnlyList<int> order,int count)
    {
        if(order.Count!=count||!order.Order().SequenceEqual(Enumerable.Range(0,count)))
            throw new InvalidDataException("City person order must preserve all publication occurrences.");
    }
    private void ApplyArrows()
    {
        var page=state.Pages[state.ActivePage];
        foreach(var(id,header)in headers)header.ApplySortDirection(page.ActiveColumn==id?page.ActiveAscending:null);
    }
    private void PositionRows()
    {
        if(entries.Length==0)return;
        int first=scroll.Value/48;
        for(int slot=0;slot<pool.Count;slot++)
        {
            var row=pool[slot];int index=first+slot;
            if(index>=state.Order.Count){row.Entry=null;row.Button.Hide();continue;}
            row.Entry=entries[state.Order[index]];row.Button.Position=new(0,index*48-scroll.Value);
            row.Button.Selected=row.Entry.Occurrence==selectedOccurrence;row.Button.Show();ApplyCells(row);
        }
    }
    private void ApplyCells(Row row)
    {
        if(row.Entry is not {} entry)return;
        foreach(var label in row.Labels.Values)label.Hide();
        int visual=row.Button.VisualState;
        int ordinary=entry.AlteredHealth==true&&visual<3?visual==2?68:67:8+Math.Min(visual,3);
        int x=0;
        for(int slot=0;slot<columns.Count;slot++)
        {
            var column=columns[slot];var cell=entry.Cells[slot];
            if(!row.Labels.TryGetValue(column.Id,out var label))
            {label=new(){Name="CityPersonColumn"+column.Id};row.Button.AddChild(label);row.Labels.Add(column.Id,label);}
            label.Position=new(x+10,10);label.Size=new(column.Width-20,28);label.Show();
            var profile=new MapFontProfile("city-domestic/dynamic-cell",0,0,.875f,.875f,0,0,
                uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue,0,column.Align,column.LineArrange,0,0,
                0xFF000000,0xFF000000,0xFF000000,0xFF000000)
                {WidthFitMode=2,WidthReserve=0,StrokeColour=0xFF000000,StrokeWidth=3};
            int preset=cell.SourcePreset??(cell.Emphasized?visual is 0 or 1?65:visual==2?66:11:ordinary);
            label.Configure(chrome.Font,profile.WithPreset(presets[preset]));
            if(cell.SourceFormattedText is {} formatted)label.SetPersonnelPreviewLine(formatted);else label.Text=cell.Text;
            label.FirstGlyphColour=cell.FirstGlyphColour;label.QueueRedraw();x+=column.Width;
        }
    }
    private void Finish(CityDomesticCandidate? result)
    {if(!ReadAdmission())return;var callback=complete;complete=null;Hide();callback?.Invoke(result);}
    internal void RequestCancel()=>Finish(null);
    internal void Abandon()
    {
        if(abandoned)return;abandoned=true;complete=null;order=null;queryPage=null;admitted=null;
        foreach(var row in pool){row.Entry=null;row.Button.InputAdmission=null;row.Button.Disabled=true;}
        foreach(var header in headers.Values)header.InputAdmission=null;
        foreach(var tab in tabs)tab.InputAdmission=null;
        scroll?.SetProcessInput(false);chrome?.Abandon();Hide();
    }
    public override void _Input(InputEvent input)
    {
        if(ReadAdmission()&&input is InputEventMouseButton{ButtonIndex:MouseButton.Right,Pressed:true})
        {GetViewport().SetInputAsHandled();RequestCancel();}
    }
    public override void _ExitTree()=>Abandon();
}
