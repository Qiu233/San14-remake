using Godot;
using San14.Core;
using San14.Data;
using System.Collections.Immutable;

/// <summary>The captive batch's checked name list and ordinary three-page
/// inspector. A treatment only submits the real pending batch's formal order.</summary>
internal partial class ArmyReturnCaptiveSelectionView : Control
{
    private sealed class Row(UiSourcePersonRow button) { internal UiSourcePersonRow Button=button; internal OfficerInformationRow? Entry; }
    private readonly List<Row> pool=[];
    private readonly List<int> selected=[];
    private readonly List<UiSourceCaptionButton> actions=[];
    private readonly Dictionary<int,OfficerDetailPublication> details=[];
    private ArmyReturnCaptiveBatchRequest request=null!;
    private IReadOnlyDictionary<int,ArmyReturnCaptiveTrial> trials=null!;
    private OfficerInformationPageSet pages=null!;
    private OfficerInformationPage page=null!;
    private IReadOnlyDictionary<int,AreaAdministrationQuery<bool>> health=null!;
    private Func<int,OfficerDetailPublication>? readDetail;
    private Func<OfficerDetailPublication>? readNeutralDetail;
    private OfficerDetailPublication? neutralDetail;
    private Action<int>? inspectCurrent;
    private Action? refreshHoverGuide;
    private bool focusRefreshQueued;
    private bool hasFocusedList;
    private Func<bool>? admitted;
    private Action<ConfirmArmyReturnCaptiveBatchOrder>? submitted;
    private AdministrationPageChrome chrome=null!;
    private OfficerDetailsView detail=null!;
    private UiSourceScrollbar scroll=null!;
    private UiSourceColumnButton nameHeader=null!;
    private UiSourceBottomButton selectAll=null!;
    private bool busy,abandoned,nextAscending=true,nextSelectedFirst=true;
    private int inspectedId=-1;
    private string selectCaption="",clearCaption="",hint="";
    internal IReadOnlyList<int> SelectedOfficerIds=>selected.AsReadOnly();

    internal void Configure(GameResourceRepository repository,PreviewText text,
        AdministrationPageChrome.Resources resources,ArmyReturnCaptiveBatchRequest request,
        OfficerInformationPageSet pages,IReadOnlyDictionary<int,AreaAdministrationQuery<bool>> alteredHealth,
        Func<int,OfficerDetailPublication> readDetail,Func<OfficerDetailPublication> readNeutralDetail,string destinationName,string destinationGold,
        Func<bool> admitted,Action<ConfirmArmyReturnCaptiveBatchOrder> submitted,Func<int> scrollSpeed)
    {
        this.request=request;trials=request.RecruitmentTrials.ToDictionary(row=>row.OfficerId);this.pages=pages;health=alteredHealth;this.readDetail=readDetail;this.readNeutralDetail=readNeutralDetail;
        this.admitted=admitted;this.submitted=submitted;
        inspectCurrent=id=>Inspect(id,repository,text,resources);
        page=pages.ReadPage(0);
        Name="ArmyReturnCaptiveSelection";ZIndex=96;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var blur=new UiSourceCommonBlur();AddChild(blur);blur.Configure(repository);
        detail=new(){Size=new(1920,1080)};AddChild(detail);
        Inspect(request.RemainingOfficerIds[0],repository,text,resources);
        // This inspector is embedded below the caller's list and chrome.
        // Its shell blur must finish before those later siblings are drawn.
        detail.ZIndex=0;
        // Navigation stays above the later help strip, while the inspector
        // body and its blur remain below the caller's list and chrome.
        detail.SetPageNavigationLayer(1);
        chrome=new();AddChild(chrome);
        chrome.Configure(repository,text,0x4AC,()=>{},()=>{},resources:resources);
        chrome.ApplyReturnVisible(false);chrome.ApplyDecision(false,false);
        var narrator=new AdministrationMessages(repository,text.SourceLocale);
        hint=narrator.Format(0x2C50);chrome.ApplyDefaultHint(hint);
        var data=resources.Messages??repository.LoadMessages(text.SourceLocale,San14.Data.Formats.MessageKind.Data);
        string Message(int id)=>data.Messages.Sections[1].Records[id].Text??throw new InvalidDataException("Missing captive treatment caption.");
        selectCaption=Message(0x3A);clearCaption=Message(0x3B);
        var shell=resources.Assets.SourceLayout(0xA3B4A82E);
        var heading=shell.Rectangle(14,new(191,147));
        nameHeader=new(){Name="CaptiveNameHeader",Position=heading.Position+new Vector2(48,0),Size=new(132,48),InputAdmission=ReadAdmission};
        chrome.Canvas.AddChild(nameHeader);nameHeader.Configure(repository,resources.Assets,resources.SpriteShader,resources.Font,Message(0x3EC),personList:true);
        nameHeader.Pressed+=()=>
        {
            if(!ReadAdmission())return;
            var result=pages.Sort(0,135,nextAscending);
            if(!result.Available)return;
            page=pages.ReadPage(0);nameHeader.ApplySortDirection(nextAscending);nextAscending=!nextAscending;PositionRows();
        };
        var checkHeader=new UiSourceColumnButton {Name="CaptiveSelectionHeader",Position=heading.Position,Size=new(48,48),InputAdmission=ReadAdmission};
        chrome.Canvas.AddChild(checkHeader);checkHeader.Configure(repository,resources.Assets,resources.SpriteShader,resources.Font,Message(0x628),personList:true);
        checkHeader.Pressed+=()=>{if(ReadAdmission()){pages.SortSelection(selected,nextSelectedFirst);nextSelectedFirst=!nextSelectedFirst;page=pages.ReadPage(0);PositionRows();}};
        var box=shell.Rectangle(17,new(191,147));
        var rows=new Control {Name="CaptiveRows",Position=box.Position,Size=new(180,box.Size.Y),ClipContents=true,MouseFilter=MouseFilterEnum.Pass};
        chrome.Canvas.AddChild(rows);
        var bar=shell.Rectangle(32,new(191,147));
        scroll=new(){Name="CaptiveScrollbar",Position=new(191+180+26,bar.Position.Y),Size=bar.Size};
        chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository,resources.Assets,resources.SpriteShader,Math.Max(0,page.Rows.Length*48-(int)rows.Size.Y),scrollSpeed(),_=>PositionRows(),currentScrollSpeed:scrollSpeed);
        rows.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
        for(int slot=0;slot<Math.Min(14,page.Rows.Length);slot++)
        {
            var button=new UiSourcePersonRow {Name="CaptiveRow"+slot,InputAdmission=ReadAdmission};rows.AddChild(button);
            button.Configure(repository,resources.Assets,resources.SpriteShader,resources.Font,Message(0x628),
                [new DeploymentPersonColumn(135,132,0x3EC,1,1,true)],[new DeploymentPersonCell("")],180);
            var row=new Row(button);pool.Add(row);
            button.Pressed+=()=>{if(!ReadAdmission()||row.Entry is not {} entry)return;hasFocusedList=true;if(!selected.Remove(entry.OfficerId))selected.Add(entry.OfficerId);RefreshSelection();};
            button.MouseEntered+=QueueFocusRefresh;button.MouseExited+=QueueFocusRefresh;
            button.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
        }
        var root=resources.Assets.SourceLayout("00_11_08_00_shogu");
        var summary=resources.Assets.SourceLayout("00_11_08_00_offset_00");
        Vector2 summaryMount=root.Rectangle(1).Position;
        summary.Art(chrome.Canvas,1,summary.Rectangle(1,summaryMount),resources.Assets,resources.SpriteShader);
        summary.Text(chrome.Canvas,2,summaryMount,Message(0x205),resources.Font);
        summary.Text(chrome.Canvas,3,summaryMount,destinationName,resources.Font);
        summary.Text(chrome.Canvas,4,summaryMount,Message(0x20C),resources.Font);
        summary.Text(chrome.Canvas,5,summaryMount,destinationGold,resources.Font);
        summary.Text(chrome.Canvas,6,summaryMount,MonthlyMaintenanceCaption(data,text.SourceLocale),resources.Font);
        summary.Text(chrome.Canvas,7,summaryMount,Message(0x697),resources.Font);
        var count=resources.Assets.SourceLayout("00_11_08_00_offset_03");Vector2 countMount=root.Rectangle(25).Position;
        count.Art(chrome.Canvas,1,count.Rectangle(1,countMount),resources.Assets,resources.SpriteShader);
        count.Text(chrome.Canvas,2,countMount,Message(0x543),resources.Font);
        count.Text(chrome.Canvas,3,countMount,request.RemainingOfficerIds.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),resources.Font);
        var footer=resources.Assets.SourceLayout("00_11_08_00_offset_01");Vector2 footerMount=root.Rectangle(3).Position;
        footer.Art(chrome.Canvas,1,footer.Rectangle(1,footerMount),resources.Assets,resources.SpriteShader);
        int[] mounts=[2,4,6,8],captions=[0x12,0x3C,0x3D,0x3E];
        for(int index=0;index<4;index++)
        {
            var choice=(ArmyReturnCaptiveDisposition)index;var rect=footer.Rectangle(mounts[index],footerMount);
            var button=new UiSourceCaptionButton {Name="Captive"+choice,Position=rect.Position,Size=rect.Size,InputAdmission=ReadAdmission};
            chrome.Canvas.AddChild(button);actions.Add(button);
            button.ConfigureFooterText(repository,resources.Assets,resources.SpriteShader,resources.Font,Message(captions[index]),()=>Submit(choice));
            button.MouseEntered+=()=>ShowActionHint(choice,button);
            button.MouseExited+=()=>chrome.ApplyDefaultHint(hint);
        }
        selectAll=new(){Name="CaptiveSelectAll",Position=footer.Rectangle(13,footerMount).Position,InputAdmission=ReadAdmission};chrome.Canvas.AddChild(selectAll);
        selectAll.Configure(repository,resources.Assets,resources.SpriteShader,resources.Font,selectCaption,()=>
        {
            if(!ReadAdmission())return;hasFocusedList=true;if(selected.Count==request.RemainingOfficerIds.Length)selected.Clear();
            else foreach(int id in pages.CurrentOfficerIds)if(!selected.Contains(id))selected.Add(id);RefreshSelection();
        });
        selectAll.MouseEntered+=ShowAllHint;
        selectAll.MouseExited+=()=>chrome.ApplyDefaultHint(hint);
        refreshHoverGuide=()=>
        {
            if(!ReadAdmission())return;
            if(selectAll.IsHovered()){ShowAllHint();return;}
            for(int index=0;index<actions.Count;index++)
                if(actions[index].IsHovered()){ShowActionHint((ArmyReturnCaptiveDisposition)index,actions[index]);return;}
        };
        PositionRows();Resized+=FitDetail;FitDetail();
        SetMeta("source_layout",root.Source.Source.ResourceUri);SetMeta("captive_batch_round",request.Round);

        void ShowAllHint()
        {if(ReadAdmission())chrome.ApplyDefaultHint(narrator.Format(selected.Count<request.RemainingOfficerIds.Length?0x2C4D:0x2C4E));}
        void ShowActionHint(ArmyReturnCaptiveDisposition choice,UiSourceCaptionButton button)
        {
            if(!ReadAdmission())return;
            int id=selected.Count==0?0x2C44:choice switch
            {
                ArmyReturnCaptiveDisposition.Recruit=>button.Disabled?0x2C42:0x2C43,
                ArmyReturnCaptiveDisposition.Execute=>button.Disabled?0x3249:selected.Count<2?0x2C47:0x2C48,
                ArmyReturnCaptiveDisposition.Release=>selected.Count<2?0x2C49:0x2C4A,
                _=>selected.Count<2?0x2C4B:0x2C4C
            };
            chrome.ApplyDefaultHint(narrator.Format(id));
        }
    }
    // Both startup text preparation and the actual page use the same source
    // caption. This is monthly upkeep per captive, independent of selection.
    internal static string MonthlyMaintenanceCaption(LoadedMessages messages,string locale)
    {
        var data=messages.Messages.Sections[1].Records;
        string format=data[0x3AB].Text??throw new InvalidDataException("Missing captive upkeep quantity caption.");
        string suffix=data[0x56C].Text??throw new InvalidDataException("Missing captive upkeep interval caption.");
        string[] parts=format.Split("%d",StringSplitOptions.None);
        if(parts.Length!=3)throw new InvalidDataException("Unexpected captive upkeep quantity format.");
        string amount=ArmyCampaignRuleAdapter.ReturnCaptiveMonthlyPay(locale).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return parts[0]+"1"+parts[1]+amount+parts[2]+suffix;
    }
    private void Inspect(int id,GameResourceRepository repository,PreviewText text,AdministrationPageChrome.Resources resources)
    {
        if(inspectedId==id)return;
        if(!details.TryGetValue(id,out var publication))details.Add(id,publication=readDetail!(id));
        string? advice=trials[id].AdviceTextAtSource;
        if(inspectedId<0)detail.Configure(repository,publication,text,()=>{},ReadAdmission,showNavigation:false,resources:resources,showPageButtons:true,captiveAdviceAtSource:advice);
        else detail.SelectCurrent(publication,advice);
        inspectedId=id;
    }
    private void FitDetail(){if(detail is not null)detail.Size=Size;}
    private void PositionRows()
    {
        int first=scroll.Value/48;
        for(int slot=0;slot<pool.Count;slot++)
        {
            var row=pool[slot];int index=first+slot;
            if(index>=page.Rows.Length){row.Entry=null;row.Button.Hide();continue;}
            var entry=page.Rows[index];row.Entry=entry;row.Button.Position=new(0,index*48-scroll.Value);row.Button.Show();
            var cell=entry.Cells[0];row.Button.ApplyCells([new DeploymentPersonCell(cell.Text??"",SourcePreset:cell.PresetIndex,SourceFormattedText:cell.SourceFormattedText)]);
            var altered=health[entry.OfficerId];row.Button.ApplyHealthStyle(altered.Available?altered.Value:null);
        }
        RefreshSelection();
    }
    private void RefreshSelection()
    {
        foreach(var row in pool)if(row.Entry is {} entry){row.Button.Selected=selected.Contains(entry.OfficerId);row.Button.Disabled=busy;}
        if(selectAll is not null){selectAll.Disabled=busy;selectAll.ApplyCaption(selected.Count==request.RemainingOfficerIds.Length?clearCaption:selectCaption);}
        if(nameHeader is not null)nameHeader.Disabled=busy;
        for(int index=0;index<actions.Count;index++)
        {
            bool available=selected.Count>0;
            if(index==0)available=selected.Any(id=>!trials[id].FailedRecruitNoticeShown);
            if(index==1)available=selected.Any(id=>!trials[id].ExecutionProtectionNoticeShown);
            actions[index].Disabled=busy||!available;
        }
        QueueFocusRefresh();refreshHoverGuide?.Invoke();
    }
    // 66E130 follows the current row after scroll/reorder, then 671780
    // restores the first checked person on leaving the list. Coalesce real
    // UI events; this is not a per-frame query or world scan.
    private void QueueFocusRefresh()
    {
        if(abandoned||focusRefreshQueued)return;
        focusRefreshQueued=true;Callable.From(RefreshFocus).CallDeferred();
    }
    private void RefreshFocus()
    {
        focusRefreshQueued=false;if(!ReadAdmission())return;
        foreach(var row in pool)
            if(row.Entry is {} entry&&row.Button.IsVisibleInTree()&&row.Button.IsHovered())
            {hasFocusedList=true;inspectCurrent?.Invoke(entry.OfficerId);return;}
        foreach(int id in pages.CurrentOfficerIds)
            if(selected.Contains(id)){hasFocusedList=true;inspectCurrent?.Invoke(id);return;}
        // The dedicated Data query reads the actual source placeholder.
        // It does not admit Officer0 into World or through the normal query.
        // 66D790 supplies the real initial head before any row focus or
        // selection event; do not replace that constructor input on setup.
        if(!hasFocusedList||inspectedId==0)return;
        neutralDetail??=readNeutralDetail!();
        detail.SelectCurrent(neutralDetail);inspectedId=0;
    }
    private bool ReadAdmission()=>!abandoned&&!busy&&submitted is not null&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    private void Submit(ArmyReturnCaptiveDisposition disposition)
    {
        if(!ReadAdmission()||selected.Count==0||actions[(int)disposition].Disabled)return;
        busy=true;RefreshSelection();submitted?.Invoke(new(request.CapturingForceId,request.BatchId,request.Round,disposition,selected.ToImmutableArray()));
    }
    internal void SetBusy(bool value){if(!abandoned){busy=value;RefreshSelection();}}
    internal void Abandon(){abandoned=true;submitted=null;admitted=null;readDetail=null;readNeutralDetail=null;inspectCurrent=null;refreshHoverGuide=null;chrome?.Abandon();Hide();}
    public override void _ExitTree(){abandoned=true;submitted=null;admitted=null;readDetail=null;readNeutralDetail=null;inspectCurrent=null;refreshHoverGuide=null;}
    public override void _Input(InputEvent input)
    {
        if(ReadAdmission()&&(input is InputEventKey {Keycode:Key.Escape} or InputEventMouseButton {ButtonIndex:MouseButton.Right}))GetViewport().SetInputAsHandled();
    }
}
