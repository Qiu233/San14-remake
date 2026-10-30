using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record CampaignEventCommandPerson(int Id,string Name,LoadedSprite? Portrait);
internal sealed record CampaignEventCommandViewState(int Category,byte FilterMask,int SortColumn,
    bool SortAscending,int DetailTab,int? SelectedEventId,int ScrollOffset);

/// <summary>The ordinary mode-zero event directory. Immutable catalog rows
/// publish eligibility; selection and presentation never run an event.</summary>
internal partial class CampaignEventCommandView : Control
{
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;
    private CampaignEventCommandCatalog catalog=null!;
    private IReadOnlyList<MessageRecord> captions=null!;
    private UiAssets assets=null!;
    private MapFont font=null!;
    private Shader shader=null!;
    private UiSourceLayout source=null!,eventRowSource=null!,detailSource=null!,personFaceSource=null!;
    private MapMarkerLayout eventBarSource=null!;
    private UiSourceHeader header=null!;
    private Control canvas=null!,rowsHost=null!,contentHost=null!,peopleHost=null!;
    private UiSourceParagraph summary=null!;
    private UiSourceText count=null!,hint=null!;
    private UiSourceRoundButton back=null!;
    private CreatedEventExecuteButton execute=null!;
    private UiSourceScrollbar? scroll,contentScroll;
    private readonly List<UiSourceTabButton> categories=[],detailTabs=[];
    private readonly List<CampaignEventCommandButton> cells=[];
    private readonly List<(int Column,UiSourceCommandButton Button,Control Down,Control Up)> columns=[];
    private IReadOnlyList<CampaignEventCommandRow> rows=[];
    private CampaignEventCommandRow? selected;
    private Func<bool>? admitted;
    private Action<int>? executeRequested;
    private Action? closed;
    private Func<int> scrollSpeed=null!;
    private Action<Action> runUiAction=null!;
    private Func<int,CampaignEventCommandPerson?> personProvider=null!;
    private int category,detailTab,sortColumn,firstRow=-1;
    private bool sortAscending=true,busy,abandoned;
    private string notice="";
    private const int RowPitch=121,ConditionPitch=52;
    internal int? SelectedEventId=>selected?.EventId;
    internal event Action<int>? CategorySelected;
    internal CampaignEventCommandViewState CaptureState()=>new(category,filterMask,sortColumn,
        sortAscending,detailTab,selected?.EventId,scroll?.Value??0);
    internal void RestoreState(CampaignEventCommandViewState state)
    {
        if(abandoned)return;
        if(state.Category is <0 or >4||state.SortColumn is <0 or >1||state.DetailTab is <0 or >1)
            throw new ArgumentOutOfRangeException(nameof(state));
        category=state.Category;filterMask=state.FilterMask;sortColumn=state.SortColumn;
        sortAscending=state.SortAscending;detailTab=state.DetailTab;
        RebuildRows();scroll?.SetValue(state.ScrollOffset);
        if(state.SelectedEventId is int id&&rows.Any(row=>row.EventId==id))Select(id);
        RefreshFilterInput();
    }

    internal void Configure(GameResourceRepository repository,PreviewText text,CampaignEventCommandCatalog catalog,
        Func<bool> admitted,Action<int> execute,Action close,Func<int> scrollSpeed,Action<Action> runUiAction,
        Func<int,CampaignEventCommandPerson?> personProvider)
    {
        this.repository=repository;this.text=text;this.catalog=catalog;this.admitted=admitted;
        this.captions=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records;
        executeRequested=execute;closed=close;this.scrollSpeed=scrollSpeed;this.runUiAction=runUiAction;
        this.personProvider=personProvider;
        Name="CampaignEventCommand";MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        canvas=new(){Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Stop};AddChild(canvas);
        source=new(MapMarkerLayout.LoadNamed(repository,"09_01_01_event_list_pk_cn"));
        eventRowSource=new(MapMarkerLayout.LoadNamed(repository,"09_01_01_event_list_offset_00_cn"));
        detailSource=new(MapMarkerLayout.LoadNamed(repository,"09_01_01_event_list_offset_02"));
        eventBarSource=CityGateMapMarkerSources.LoadBar(repository);
        personFaceSource=new(MapMarkerLayout.LoadNamed(repository,CampaignEventCommandPresentation.PersonFaceLayoutName));
        foreach(int node in new[]{3,61,63,64,66,68,78,105,57})
            if(source.Nodes[node].Type==6)source.Blur(canvas,node,source.Rectangle(node));
            else source.Art(canvas,node,source.Rectangle(node),assets,shader);
        header=new(){Name="CampaignEventHeader"};canvas.AddChild(header);
        header.Configure(repository,assets,shader,font,Caption(0x249));
        int[] mounts=[4,6,8,12,14],captions=[0x49D,0x49A,0xAE,0x49B,0x49C];
        for(int index=0;index<mounts.Length;index++)
        {
            int target=index;
            var tab=Tab(mounts[index],captions[index],"CampaignEventCategory"+index,
                index==0?0x172EDC24u:index==4?0xD45873AAu:0x9AFB27B7u);
            categories.Add(tab);tab.Pressed+=()=>Run(()=>
            {
                if(!CanUse())return;
                bool changed=category!=target;category=target;
                CategorySelected?.Invoke(target);
                if(changed)RebuildRows();
            });
        }
        Column(22,0x498,0);Column(27,0x499,1);
        count=source.Text(canvas,58,default,"",font);
        var listBox=source.Rectangle(39);
        rowsHost=new(){Name="CampaignEventRows",Position=listBox.Position,Size=listBox.Size,
            ClipContents=true,MouseFilter=MouseFilterEnum.Pass};canvas.AddChild(rowsHost);
        rowsHost.GuiInput+=input=>{if(CanUse())scroll?.HandleWheel(input);};
        var summaryBox=source.Rectangle(67);
        summary=new(){Name="CampaignEventDescription",Position=summaryBox.Position,Size=summaryBox.Size};canvas.AddChild(summary);
        summary.Configure(font,MapFontProfile.FromNode(source.Nodes[67],source.Source.Source.ResourceUri));
        for(int index=0;index<2;index++)
        {
            int target=index;var tab=Tab(index==0?69:71,index==0?0x3CE:0x4A2,"CampaignEventDetailTab"+index,
                index==0?0x172EDC24u:0xD45873AAu);detailTabs.Add(tab);
            tab.Pressed+=()=>Run(()=>{if(CanUse()&&detailTab!=target){detailTab=target;PublishContent();}});
        }
        var first=source.Rectangle(83);var last=source.Rectangle(101);
        contentHost=new(){Name="CampaignEventContent",Position=first.Position,
            Size=new(first.Size.X,last.End.Y-first.Position.Y),ClipContents=true,MouseFilter=MouseFilterEnum.Pass};canvas.AddChild(contentHost);
        contentHost.GuiInput+=input=>{if(CanUse())contentScroll?.HandleWheel(input);};
        peopleHost=new(){Name="CampaignEventPeople",Position=source.Rectangle(79).Position,Size=source.Rectangle(79).Size,
            MouseFilter=MouseFilterEnum.Ignore};canvas.AddChild(peopleHost);
        BuildFilter();
        var footer=new UiSourceLayout(repository,0xD8913AB2);
        back=new(){Name="ReturnFromCampaignEvents",Position=footer.Rectangle(4,new(0,904)).Position,InputAdmission=CanUse};canvas.AddChild(back);
        back.Configure(repository,assets,shader,text.SourceLocale,"000_cmn_txt_decision_modoru",RequestBack,UiAudioCue.Cancel);
        this.execute=new(){Name="ExecuteCampaignEvent",Position=footer.Rectangle(5,new(0,904)).Position,InputAdmission=CanUse};
        canvas.AddChild(this.execute);this.execute.Configure(repository,assets,shader,text.SourceLocale,Confirm);
        var help=new UiSourceLayout(repository,0xAB6ADAA7);var mount=footer.Rectangle(19,new(0,904)).Position;
        foreach(var node in help.Nodes.Where(node=>node.Type is 1 or 3))help.Art(canvas,node.Index,help.Rectangle(node.Index,mount),assets,shader);
        hint=help.Text(canvas,help.Nodes.Single(node=>node.Type==2).Index,mount,"",font);
        RebuildRows();Resized+=Fit;Fit();SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("source_mode",0);
    }
    private string Caption(int id)=>captions[id].Text
        ??throw new InvalidDataException("campaign_event.caption."+id.ToString("X"));
    private UiSourceTabButton Tab(int node,int caption,string name,uint layout)
    {
        var box=source.Rectangle(node);var tab=new UiSourceTabButton{Name=name,Position=box.Position,Size=box.Size,InputAdmission=CanUse};
        canvas.AddChild(tab);tab.Configure(repository,assets,shader,font,Caption(caption),layout);return tab;
    }
    private void Column(int node,int caption,int column)
    {
        var box=source.Rectangle(node);var button=new UiSourceCommandButton{Name="CampaignEventSort"+column,
            Position=box.Position,Size=box.Size,InputAdmission=CanUse};canvas.AddChild(button);
        button.Configure(repository,assets,shader,font,Caption(caption),()=>Run(()=>
        {if(!CanUse())return;sortAscending=sortColumn==column?!sortAscending:true;sortColumn=column;RebuildRows(keepSelection:true);}));
        var down=source.Rectangle(node+3);down.Position-=box.Position;
        var up=source.Rectangle(node+4);up.Position-=box.Position;
        columns.Add((column,button,source.Art(button,node+3,down,assets,shader),source.Art(button,node+4,up,assets,shader)));
    }
    private void Run(Action action)=>runUiAction(action);
    private bool CanUse()=>!abandoned&&!busy&&IsVisibleInTree()&&admitted?.Invoke()==true;
    private void Confirm()=>Run(()=>
    {if(CanUse()&&selected is{CanExecute:true} row)executeRequested?.Invoke(row.EventId);});
    internal void RequestBack(){if(!abandoned)Run(()=>{if(CanUse())closed?.Invoke();});}
    internal void SetBusy(bool value){if(!abandoned){busy=value;PublishInputState();}}
    internal void ApplyNotice(string value){if(!abandoned){notice=value;PublishHint();}}
    internal void RegisterHeader(UiSourceHeaderStack stack)=>header.Publish(stack);
    internal void ApplyCatalog(CampaignEventCommandCatalog value)
    {if(!abandoned){catalog=value;RebuildRows(keepSelection:true);}}
    private void PublishInputState()
    {
        if(back is not null)back.Disabled=busy;
        if(execute is not null)execute.Disabled=busy||selected?.CanExecute!=true;
        foreach(var tab in categories.Concat(detailTabs))tab.Disabled=busy;
        foreach(var (_,button,_,_) in columns)button.Disabled=busy;
        foreach(var cell in cells)cell.Disabled=busy;
        if(scroll is not null)scroll.ProcessMode=busy?ProcessModeEnum.Disabled:ProcessModeEnum.Inherit;
        if(contentScroll is not null)contentScroll.ProcessMode=busy?ProcessModeEnum.Disabled:ProcessModeEnum.Inherit;
        RefreshFilterInput();
    }
    private void PublishHint()
    {if(hint is not null)hint.Text=notice.Length!=0?notice:selected?.MissingOwner is not null?text.Get("detail_unknown"):"";}
    internal void Abandon()
    {
        if(abandoned)return;abandoned=true;admitted=null;executeRequested=null;closed=null;CategorySelected=null;
        foreach(var cell in cells)cell.Abandon();
        foreach(var filter in filters)filter.Abandon();
        if(back is not null){back.Disabled=true;back.InputAdmission=null;}
        if(execute is not null){execute.Disabled=true;execute.InputAdmission=null;}
        if(scroll is not null)scroll.ProcessMode=ProcessModeEnum.Disabled;
        if(contentScroll is not null)contentScroll.ProcessMode=ProcessModeEnum.Disabled;
        Hide();
    }
    private void Fit()
    {if(canvas is null)return;float scale=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*scale;canvas.Position=(Size-canvas.Size*scale)/2;}
    public override void _ExitTree(){Abandon();font?.Dispose();assets?.Dispose();shader?.Dispose();}
}
