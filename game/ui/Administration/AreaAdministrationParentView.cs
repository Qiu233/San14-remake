using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Two views over one Domestic draft. It begins on the map; list
/// sorting changes display order and preserves selection and scroll.</summary>
internal partial class AreaAdministrationParentView : Control
{
    private AdministrationPageChrome chrome=null!;
    private AreaAdministrationResourceHeader resourceHeader=null!;
    private Control list=null!;
    private UiSourceScrollbar scroll=null!;
    private UiSourceBottomButton mode=null!,automatic=null!,dismissAll=null!;
    private readonly Dictionary<int,AreaAdministrationListRow> rows=[];
    private readonly List<int> ordered=[];
    private readonly Dictionary<int,UiSourceColumnButton> headers=[];
    private readonly Dictionary<int,bool> nextAscending=[];
    private Action? returned,accepted,autoRequested,dismissRequested;
    private Action<int>? regionRequested;
    private Action<bool>? modeChanged;
    private Func<IReadOnlyList<int>,int,bool,AreaAdministrationListOrder>? order;
    private Action<string>? unavailable;
    private int? selected;
    private bool listMode,suspended,inputBlocked,abandoned;
    private string mapTitle="",listTitle="",mapCaption="",listCaption="",defaultHint="";
    internal bool IsMapMode=>!listMode&&Active;
    internal IReadOnlyList<int> DisplayOrder=>ordered.ToArray();
    internal int? SelectedAreaId=>selected;

    internal void Configure(GameResourceRepository repository,PreviewText text,IReadOnlyList<AreaAdministrationListEntry> entries,
        int scrollSpeedSetting,Action<int> regionRequested,Action returned,Action accepted,Action<bool> modeChanged,
        Func<IReadOnlyList<int>,int,bool,AreaAdministrationListOrder>? order=null,Action<string>? unavailable=null,
        Action? autoRequested=null,Action? dismissRequested=null,Func<int>? currentScrollSpeed=null)
    {
        this.regionRequested=regionRequested;this.returned=returned;this.accepted=accepted;this.modeChanged=modeChanged;
        this.order=order;this.unavailable=unavailable;this.autoRequested=autoRequested;this.dismissRequested=dismissRequested;
        Name="AreaAdministrationParent";ZIndex=3;MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome=new AdministrationPageChrome {Name="DomesticChrome"};AddChild(chrome);
        chrome.Configure(repository,text,0x04B7,RequestBack,()=>{if(Active)this.accepted?.Invoke();});
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id)=>messages.Records[id].Text??throw new InvalidDataException("Missing Domestic caption.");
        mapTitle=Message(0x04B7);listTitle=Message(0x05AF);mapCaption=Message(0x005D);listCaption=Message(0x0061);
        var narration=new AdministrationMessages(repository,text.SourceLocale);
        defaultHint=narration.Format(0x56CE);chrome.ApplyDefaultHint(defaultHint);chrome.ApplyReturnHint(narration.Format(0x2733));
        resourceHeader=new AreaAdministrationResourceHeader {Name="DomesticResourceHeader"};chrome.Canvas.AddChild(resourceHeader);
        resourceHeader.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,text);
        list=new Control {Name="DomesticAreaList",Size=new(1920,904),MouseFilter=MouseFilterEnum.Stop};chrome.Canvas.AddChild(list);
        var shell=new UiSourceLayout(repository,0xA3B4A82E);
        shell.Art(list,3,shell.Rectangle(3,new(191,147)),chrome.Assets,chrome.SpriteShader);
        var clip=new Control {Name="AreaListViewport",Position=new(213,264),Size=new(1464,596),ClipContents=true,MouseFilter=MouseFilterEnum.Pass};list.AddChild(clip);
        scroll=new UiSourceScrollbar {Name="AreaScrollbar",Position=new(1687,265),Size=new(16,597)};list.AddChild(scroll);
        scroll.Configure(repository,chrome.Assets,chrome.SpriteShader,Math.Max(0,entries.Count*48-596),scrollSpeedSetting,_=>PositionRows(),currentScrollSpeed:currentScrollSpeed);
        clip.GuiInput+=scroll.HandleWheel;
        int x=261;
        var iconHeader=new UiSourceColumnButton {Name="AreaPlaceHeader",Position=new(213,214),Size=new(48,48),Disabled=order is null};list.AddChild(iconHeader);
        iconHeader.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,"","000_cmn_icon_kyoten",personList:true);
        headers.Add(-1,iconHeader);nextAscending[-1]=true;iconHeader.Pressed+=()=>Sort(-1);
        foreach(var column in AreaAdministrationListContent.Columns)
        {
            int id=column.Id;nextAscending[id]=column.InitialAscending;
            var header=new UiSourceColumnButton {Name="AreaHeader"+id,Position=new(x,214),Size=new(column.Width,48),Disabled=order is null};list.AddChild(header);
            header.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,Message(column.CaptionMessage),personList:true);
            header.Pressed+=()=>Sort(id);headers.Add(id,header);x+=column.Width;
        }
        foreach(var entry in entries)
        {
            int id=entry.AreaId;var row=new AreaAdministrationListRow {Name="AreaRow"+id};clip.AddChild(row);
            row.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,text,entry,()=>
            {
                if(!Active||!listMode)return;selected=id;RefreshSelection();this.regionRequested?.Invoke(id);
            });
            row.GuiInput+=scroll.HandleWheel;rows.Add(id,row);ordered.Add(id);
        }
        PositionRows();
        var footer=new UiSourceLayout(repository,0xD8913AB2);Vector2 footerMount=new(0,904);
        mode=Bottom("DomesticMapList",14,listCaption,()=>SetListMode(!listMode));
        automatic=Bottom("DomesticAutomatic",17,Message(0x04B8),()=>{if(Active)this.autoRequested?.Invoke();});
        dismissAll=Bottom("DomesticDismissAll",18,Message(0x002A),()=>{if(Active)this.dismissRequested?.Invoke();});
        automatic.Disabled=true;dismissAll.Disabled=true;
        SetListMode(false,notify:false);
        // The source generic list initializes its mode1 front-column sort
        // while the list is still hidden. This never changes draft held order.
        Sort(-1,initial:true);

        UiSourceBottomButton Bottom(string name,int node,string caption,Action action)
        {
            var button=new UiSourceBottomButton {Name=name,Position=footer.Rectangle(node,footerMount).Position};chrome.Canvas.AddChild(button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,caption,()=>{if(Active)action();});return button;
        }
    }
    private bool Active=>!suspended&&!inputBlocked&&!abandoned;
    internal void SetListMode(bool value,bool notify=true)
    {
        if(!Active)return;listMode=value;list.Visible=value;resourceHeader.Visible=!value;MouseFilter=value?MouseFilterEnum.Stop:MouseFilterEnum.Ignore;
        mode.ApplyCaption(value?mapCaption:listCaption);chrome.ApplyTitle(value?listTitle:mapTitle);
        if(notify)modeChanged?.Invoke(value);
    }
    internal void ApplyHeader(AreaAdministrationHeaderValues values){if(!abandoned)resourceHeader.Apply(values);}
    internal void ApplyHint(string? value){if(!abandoned&&chrome is not null)chrome.ApplyDefaultHint(value??defaultHint);}
    internal void ApplyCommands(bool canAccept,bool canAuto,bool canDismissAll)
    {
        if(abandoned)return;chrome.ApplyDecision(canAccept,true);
        automatic.Disabled=!canAuto||autoRequested is null;dismissAll.Disabled=!canDismissAll||dismissRequested is null;
    }
    internal void ApplyEntries(IReadOnlyList<AreaAdministrationListEntry> entries)
    {
        if(abandoned)return;
        if(entries.Count!=rows.Count||entries.Any(e=>!rows.ContainsKey(e.AreaId)))throw new InvalidDataException("Domestic publication no longer matches its held areas.");
        foreach(var entry in entries)rows[entry.AreaId].Apply(entry);
    }
    internal UiSourcePulldownMenu CreateAutomaticMenu(GameResourceRepository repository,PreviewText text,Action<int?> selected)
    {
        var captions=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        int[] ids=[0x0333,0x0253,0x02B3,0x02B4,0x02B5,0x0286];
        var items=ids.Select(id=>new UiSourcePulldownItem(captions.Records[id].Text??
            throw new InvalidDataException("Missing Domestic automatic caption."),true)).ToArray();
        var menu=new UiSourcePulldownMenu{Name="DomesticAutomaticMenu",ZIndex=10};chrome.Canvas.AddChild(menu);
        try
        {
            menu.Configure(repository,text.SourceLocale,new(new(356,754),new(148,228)),items,
                UiSourcePulldownOutsideClick.ConfirmConsumed,selected);
            return menu;
        }
        catch{menu.QueueFree();throw;}
    }
    internal void SetSuspended(bool value)
    {if(abandoned)return;suspended=value;Visible=!value;}
    internal void SetInputBlocked(bool value)
    {if(!abandoned)inputBlocked=value;}
    private void RefreshSelection(){foreach(var (id,row) in rows)row.Selected=selected==id;}
    private void Sort(int id,bool initial=false)
    {
        if(!Active||!listMode&&!initial||order is null)return;
        bool ascending=nextAscending[id];var result=order(ordered.ToArray(),id,ascending);
        if(result.AreaIds is null){unavailable?.Invoke(result.UnresolvedInput??"area-draft.parent_order");return;}
        ordered.Clear();ordered.AddRange(result.AreaIds);nextAscending[id]=!ascending;PositionRows();
        foreach(var (column,header) in headers)header.ApplySortDirection(column==id?ascending:null);
    }
    private void PositionRows(){for(int i=0;i<ordered.Count;i++)rows[ordered[i]].Position=new(0,i*48-scroll.Value);}
    public override void _Process(double delta){if(Active)chrome.ApplyHoverHint(null);}
    internal void RequestBack(){if(Active)returned?.Invoke();}
    internal void Abandon()
    {
        abandoned=true;returned=null;accepted=null;autoRequested=null;dismissRequested=null;regionRequested=null;modeChanged=null;order=null;unavailable=null;
        foreach(var row in rows.Values)row.Abandon();foreach(var header in headers.Values)header.Disabled=true;
        if(mode is not null)mode.Disabled=true;
        if(automatic is not null)automatic.Disabled=true;
        if(dismissAll is not null)dismissAll.Disabled=true;
        chrome?.Abandon();Hide();
    }
}
