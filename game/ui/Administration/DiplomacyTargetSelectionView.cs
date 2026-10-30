using Godot;
using San14.Data;

/// <summary>The ordinary diplomacy caller's real grouped, single-selection list.
/// The view owns only retained publication, scrolling, selection and header input.</summary>
internal partial class DiplomacyTargetSelectionView : Control
{
    private sealed record VisibleRow(UiSourceListRow Button,UiSourceText[] Cells)
    { internal DiplomacyTargetRow? Entry {get;set;} }
    internal sealed record MapMode(int TitleMessage,bool InitialMap,Action<bool> ModeChanged,
        Func<InputEventMouseButton,bool> CancelReleased,int? DefaultHintMessage=null,int? ReturnHintMessage=null);
    private AdministrationPageChrome chrome=null!;
    private AdministrationPageChrome? mapChrome;
    private Control listRoot=null!;
    private MapMode? mapMode;
    private UiSourceBottomButton? mapButton,listButton;
    private bool listMode=true;
    internal bool IsMapMode=>mapMode is not null&&!listMode&&!abandoned&&closed is not null;
    private UiSourceScrollbar scroll=null!;
    private Control groupOwner=null!,clip=null!;
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;
    private DiplomacyTargetSelectionPage page=null!;
    private DiplomacyTargetList publication=null!;
    private readonly List<VisibleRow> rows=[];
    private readonly List<(int Id,UiSourceColumnButton View)> headers=[];
    private readonly List<UiSourceTabButton> tabs=[];
    private readonly Dictionary<(int Group,int Column),bool> nextDirections=[];
    private (int Column,bool Ascending)? sortedColumn;
    private readonly Dictionary<int,int> scrollPositions=[];
    private readonly ArmyMapUiSources iconNames=ArmyMapUiSources.Create();
    private readonly MapTextPresets presets=MapTextPresets.Create();
    private Func<bool>? admitted;
    private Func<int> scrollSpeed=null!;
    private Action<int?>? closed;
    private int? selected;
    private int group=-1;
    private bool abandoned;
    private Func<int,bool>? rowAdmission;
    private bool returnAllowed=true;
    internal int? SelectedId=>selected;
    internal int CurrentGroup=>group;

    internal void Configure(GameResourceRepository repository,PreviewText text,
        DiplomacyTargetSelectionPage page,int? selected,int titleMessage,
        Func<bool> admitted,Action<int?> closed,Func<int> scrollSpeed,MapMode? mapMode=null)
    {
        this.repository=repository;this.text=text;this.page=page;this.admitted=admitted;
        this.closed=closed;this.scrollSpeed=scrollSpeed;this.mapMode=mapMode;
        this.selected=mapMode is null&&selected.HasValue&&page.ReadPage(0).Rows.Any(row=>row.Id==selected.Value)?selected:null;
        Name="DiplomacyTargetSelection";ZIndex=5;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        listRoot=new(){Name="DiplomacyTargetListPanel",MouseFilter=MouseFilterEnum.Stop};AddChild(listRoot);
        listRoot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop=new UiSourceCommonBlur();listRoot.AddChild(backdrop);backdrop.Configure(repository);
        chrome=new();listRoot.AddChild(chrome);
        chrome.Configure(repository,text,titleMessage,RequestCancel,RequestApply,returnOnly:mapMode is not null,showFooterHint:mapMode?.DefaultHintMessage is not null);
        var shell=new UiSourceLayout(repository,0xA3B4A82E);
        shell.Blur(chrome.Canvas,2,shell.Rectangle(2,new(191,147)));
        shell.Art(chrome.Canvas,3,shell.Rectangle(3,new(191,147)),chrome.Assets,chrome.SpriteShader);
        Vector2 mount=shell.Rectangle(4,new(191,147)).Position;
        foreach(var descriptor in page.Groups)
        {
            int index=descriptor.Index;float width=descriptor.Caption.Length*28+36;
            var tab=new UiSourceTabButton{Name="DiplomacyTargetGroup"+index,Position=mount,
                Size=new(width,64),InputAdmission=ReadAdmission};
            chrome.Canvas.AddChild(tab);tabs.Add(tab);
            tab.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,descriptor.Caption,
                page.Groups.Length==1?0x9AFB27B7u:index==0?0x172EDC24u:0xD45873AAu);
            tab.Pressed+=()=>{if(ReadAdmission())ShowGroup(index);};mount.X+=width-6;
        }
        SetMeta("original_diplomacy_target_columns_recovered",true);
        SetMeta("original_diplomacy_target_mode",1);
        ShowGroup(0);
        if(mapMode is not null)
        {
            mapChrome=new();AddChild(mapChrome);
            mapChrome.Configure(repository,text,mapMode.TitleMessage,RequestCancel,()=>{},returnOnly:true,showFooterHint:mapMode.DefaultHintMessage is not null);
            if(mapMode.DefaultHintMessage is int hint)
            {
                var narrator=new AdministrationMessages(repository,text.SourceLocale);
                string message=narrator.Format(hint);chrome.ApplyDefaultHint(message);mapChrome.ApplyDefaultHint(message);
                if(mapMode.ReturnHintMessage is int returned)
                {message=narrator.Format(returned);chrome.ApplyReturnHint(message);mapChrome.ApplyReturnHint(message);}
            }
            var footer=new UiSourceLayout(repository,0xD8913AB2);
            var messages=repository.LoadMessages(text.SourceLocale,San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
            mapButton=ModeButton(chrome,"DiplomacyTargetMap",0x5D,false);
            listButton=ModeButton(mapChrome,"DiplomacyTargetList",0x61,true);
            SetListMode(!mapMode.InitialMap,notify:false);
            UiSourceBottomButton ModeButton(AdministrationPageChrome owner,string name,int caption,bool list)
            {
                var button=new UiSourceBottomButton{Name=name,Position=footer.Rectangle(14,new(0,904)).Position,InputAdmission=ReadAdmission};
                owner.Canvas.AddChild(button);
                button.Configure(repository,owner.Assets,owner.SpriteShader,owner.Font,
                    messages.Records[caption].Text??throw new InvalidDataException("Missing diplomacy selection mode caption."),
                    ()=>{if(ReadAdmission())SetListMode(list);});
                return button;
            }
        }
    }
    private void ShowGroup(int index)
    {
        if(abandoned||(uint)index>=(uint)page.Groups.Length)return;
        if(groupOwner is not null)
        {
            scrollPositions[group]=scroll.Value;
            foreach(var row in rows)row.Button.InputAdmission=null;
            foreach(var header in headers)header.View.InputAdmission=null;
            chrome.Canvas.RemoveChild(groupOwner);groupOwner.Free();rows.Clear();headers.Clear();
        }
        group=index;publication=page.ReadPage(index);
        groupOwner=new(){Name="DiplomacyTargetGroupContents",MouseFilter=MouseFilterEnum.Ignore};
        chrome.Canvas.AddChild(groupOwner);
        var shell=new UiSourceLayout(repository,0xA3B4A82E);
        Vector2 origin=shell.Rectangle(14,new(191,147)).Position;
        int left=page.HasLeadingAreaIcon?48:0;
        if(page.HasLeadingAreaIcon)Header(-1,"",0,48,iconNames.IconName(new(0x1C3,0)));
        foreach(var column in page.Groups[group].Columns)
        {
            Header(column.Id,column.Caption,left,column.Width);left+=column.Width;
            nextDirections.TryAdd((group,column.Id),column.InitialAscending);
        }
        Rect2 rowsBox=shell.Rectangle(17,new(191,147));
        clip=new(){Name="DiplomacyTargetsViewport",Position=rowsBox.Position,Size=new(1464,596),
            ClipContents=true,MouseFilter=MouseFilterEnum.Pass};groupOwner.AddChild(clip);
        clip.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
        Rect2 scrollBox=shell.Rectangle(32,new(191,147));
        scroll=new(){Name="DiplomacyTargetScrollbar",Position=scrollBox.Position,Size=scrollBox.Size};groupOwner.AddChild(scroll);
        scroll.Configure(repository,chrome.Assets,chrome.SpriteShader,Math.Max(0,publication.Rows.Length*48-596),
            scrollSpeed(),_=>PositionRows(),currentScrollSpeed:scrollSpeed);
        for(int slot=0;slot<Math.Min(14,publication.Rows.Length);slot++)
        {
            var row=new UiSourceListRow{Name="DiplomacyTargetVisibleRow"+slot,InputAdmission=ReadAdmission,
                ActionMode=BaseButton.ActionModeEnum.Release};clip.AddChild(row);
            row.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,"",page.HasLeadingAreaIcon?1416:1464,page.HasLeadingAreaIcon);
            var labels=new List<UiSourceText>();float x=page.HasLeadingAreaIcon?48:0;
            foreach(var column in page.Groups[group].Columns)
            {
                var cell=new UiSourceText{Name="DiplomacyTargetColumn"+column.Id,Position=new(x+10,10),
                    Size=new(column.Width-20,28),MouseFilter=MouseFilterEnum.Ignore};row.AddChild(cell);labels.Add(cell);x+=column.Width;
            }
            var display=new VisibleRow(row,labels.ToArray());rows.Add(display);
            row.TextVisualStateChanged+=_=>ApplyCells(display);
            row.Pressed+=()=>
            {
                if(!ReadAdmission()||row.Disabled||display.Entry is not {} entry)return;
                selected=entry.Id;RefreshSelection();
                if(mapMode is not null)Finish(selected);
            };
            row.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
        }
        foreach(var tab in tabs)tab.Selected=tabs.IndexOf(tab)==group;
        scroll.SetValue(scrollPositions.GetValueOrDefault(group));PositionRows();RefreshSelection();
        SetMeta("diplomacy_target_group",group);

        void Header(int id,string caption,int x,int width,string? icon=null)
        {
            int columnSlot=Enumerable.Range(0,page.Groups[group].Columns.Length).FirstOrDefault(slot=>page.Groups[group].Columns[slot].Id==id);
            bool known=id>=0&&publication.Rows.All(row=>row.Cells[columnSlot].SortUnresolvedInput is null);
            var header=new UiSourceColumnButton{Name="DiplomacyTargetHeader"+id,Position=origin+new Vector2(x,0),Size=new(width,48),
                InputAdmission=ReadAdmission,Disabled=!known};groupOwner.AddChild(header);headers.Add((id,header));
            header.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,caption,icon,personList:true);
            if(sortedColumn is {} indicator&&indicator.Column==id)header.ApplySortDirection(indicator.Ascending);
            header.Pressed+=()=>
            {
                if(!ReadAdmission()||header.Disabled)return;
                bool direction=nextDirections[(group,id)];var result=page.Sort(group,id,direction);
                if(result.Value is not {} sortedPage)return;
                publication=sortedPage;nextDirections[(group,id)]=!direction;sortedColumn=(id,direction);
                foreach(var entry in headers)entry.View.ApplySortDirection(entry.Id==id?direction:null);
                PositionRows();
            };
        }
    }
    private bool ReadAdmission()=>!abandoned&&closed is not null&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    private void PositionRows()
    {
        int first=scroll.Value/48;
        for(int slot=0;slot<rows.Count;slot++)
        {
            var row=rows[slot];int index=first+slot;
            if(index>=publication.Rows.Length){row.Entry=null;row.Button.Hide();continue;}
            row.Entry=publication.Rows[index];row.Button.Position=new(0,index*48-scroll.Value);row.Button.Show();
            row.Button.Selected=row.Entry.Id==selected;row.Button.Disabled=!(rowAdmission?.Invoke(row.Entry.Id)??true);
            ApplyCells(row);
        }
    }
    private void ApplyCells(VisibleRow row)
    {
        if(row.Entry is not {} entry)return;
        if(page.HasLeadingAreaIcon)
        {
            int? icon=entry.Cells[0].AreaIconSpriteId;
            row.Button.ApplyContents("",icon is int id?iconNames.IconName(new(id,0)):null);
        }
        for(int slot=0;slot<row.Cells.Length;slot++)
        {
            var cell=entry.Cells[slot];var column=page.Groups[group].Columns[slot];
            int format=(column.Flags&2)!=0?5:(column.Flags&8)!=0?4:3;
            var profile=new MapFontProfile("diplomacy-target/dynamic-cell",0,0,.875f,.875f,0,0,
                uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue,0,(byte)(((format-3)<<4)|1),(byte)(format-2),
                0,0,0xFF000000,0xFF000000,0xFF000000,0xFF000000)
                {WidthFitMode=2,WidthReserve=0,StrokeColour=0xFF000000,StrokeWidth=3};
            row.Cells[slot].Configure(chrome.Font,profile.WithPreset(presets[8+Math.Min(row.Button.VisualState,3)]));
            row.Cells[slot].Text=cell.UnresolvedInput is null?cell.Text??"":text.Get("detail_unknown");
            row.Cells[slot].FirstGlyphColour=cell.FirstGlyphColour;row.Cells[slot].QueueRedraw();
        }
    }
    private void RefreshSelection()
    {
        foreach(var row in rows)
        {
            row.Button.Selected=row.Entry?.Id==selected;
            row.Button.Disabled=row.Entry is {} entry&&!(rowAdmission?.Invoke(entry.Id)??true);
            ApplyCells(row);
        }
        chrome.ApplyReturn(returnAllowed);chrome.ApplyDecision(selected.HasValue,true);
        mapChrome?.ApplyReturn(returnAllowed);
    }
    internal void ApplySelectionAdmission(Func<int,bool>? allowed,bool canReturn)
    {
        if(abandoned)return;rowAdmission=allowed;returnAllowed=canReturn;
        if(selected is int id&&!(allowed?.Invoke(id)??true))selected=null;
        RefreshSelection();
    }
    private void SetListMode(bool list,bool notify=true)
    {
        if(abandoned||closed is null||mapMode is null)return;
        listMode=list;listRoot.Visible=list;mapChrome!.Visible=!list;
        MouseFilter=list?MouseFilterEnum.Stop:MouseFilterEnum.Ignore;
        if(notify)mapMode.ModeChanged(list);
    }
    internal bool CanChooseTarget(int id)=>ReadAdmission()&&(rowAdmission?.Invoke(id)??true);
    internal void PickMapTarget(int id)
    {
        if(IsMapMode&&page.ReadPage(group).Rows.Any(row=>row.Id==id))Finish(id);
    }
    public override void _Input(InputEvent input)
    {
        if(!IsMapMode||!ReadAdmission()||input is not InputEventMouseButton{ButtonIndex:MouseButton.Right} button)return;
        if(mapMode!.CancelReleased(button))RequestCancel();
        GetViewport().SetInputAsHandled();
    }
    internal void RequestApply(){if(selected.HasValue)Finish(selected);}
    internal void RequestCancel(){if(returnAllowed)Finish(null);}
    private void Finish(int? result)
    {
        if(!ReadAdmission()||result is int id&&!(rowAdmission?.Invoke(id)??true))return;
        var callback=closed;closed=null;admitted=null;Hide();callback?.Invoke(result);
    }
    internal void Abandon()
    {
        abandoned=true;closed=null;admitted=null;mapMode=null;
        foreach(var row in rows){row.Button.InputAdmission=null;row.Button.Disabled=true;}
        foreach(var header in headers){header.View.InputAdmission=null;header.View.Disabled=true;}
        foreach(var tab in tabs){tab.InputAdmission=null;tab.Disabled=true;}
        scroll?.SetProcessInput(false);chrome?.Abandon();mapChrome?.Abandon();SetProcessInput(false);Hide();
    }
}
