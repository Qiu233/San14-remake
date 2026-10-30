using Godot;
using San14.Data;
using System.Collections.Immutable;

internal partial class ForeignTradeSelectionRow : UiSourceListRow
{
    internal bool CheckedFront { get; init; }
    internal override int VisualState => CheckedFront && Selected ? 2 : base.VisualState;
}

/// <summary>The trade caller's real mode1 and mode2 data lists. All groups
/// retain one publication; only fourteen rows are mounted at any time.</summary>
internal partial class ForeignTradeSelectionView : Control
{
    private sealed record Row(ForeignTradeSelectionRow Button,UiSourceText? Check,UiSourceText[] Cells)
    { internal ForeignTradeSelectorRow? Entry { get; set; } }
    private AdministrationPageChrome chrome=null!;
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;
    private ForeignTradeSelectorPublication model=null!;
    private UiSourceScrollbar scroll=null!;
    private MapFontProfile checkProfile=null!;
    private UiSourceBottomButton? selectAll;
    private Control headings=null!,body=null!;
    private readonly List<Row> pool=[];
    private readonly List<UiSourceTabButton> tabs=[];
    private readonly Dictionary<int,UiSourceColumnButton> headers=[];
    private readonly Dictionary<(int,int),bool> nextDirections=[];
    private readonly Dictionary<int,(int Column,bool Ascending)> arrows=[];
    private readonly Dictionary<int,int> scrollPositions=[];
    private readonly HashSet<int> selected=[];
    private ImmutableArray<ForeignTradeSelectorRow> publication=[];
    private ImmutableArray<ForeignTradeSelectorColumn> columns=[];
    private readonly MapTextPresets presets=MapTextPresets.Create();
    private Func<bool>? admitted;
    private Func<int,bool>? rowAdmission;
    private Action<IReadOnlyList<int>?>? closed;
    private string defaultHint="",selectCaption="",clearCaption="",checkGlyph="";
    private int group=-1;
    private bool abandoned,finishing,returnAllowed=true,nextSelectedAscending;
    internal IReadOnlyList<int> SelectedIds=>model.CurrentIds.Where(selected.Contains).ToArray();

    internal void Configure(GameResourceRepository repository,PreviewText text,ForeignTradeSelectorPublication model,
        Func<bool> admitted,Action<IReadOnlyList<int>?> closed,Func<int> scrollSpeed)
    {
        this.repository=repository;this.text=text;this.model=model;this.admitted=admitted;this.closed=closed;
        foreach(int id in model.InitialSelection)selected.Add(id);
        Name="ForeignTrade"+model.Kind;ZIndex=5;MouseFilter=MouseFilterEnum.Stop;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop=new UiSourceCommonBlur();AddChild(backdrop);backdrop.Configure(repository);
        chrome=new();AddChild(chrome);
        chrome.Configure(repository,text,model.TitleMessage,RequestCancel,RequestApply,returnOnly:!model.Multiple);
        var hints=new AdministrationMessages(repository,text.SourceLocale);defaultHint=hints.Format(model.GuideMessage);
        chrome.ApplyDefaultHint(defaultHint);chrome.ApplyReturnHint(hints.Format(0x2733));
        var shell=new UiSourceLayout(repository,0xA3B4A82E);
        shell.Blur(chrome.Canvas,2,shell.Rectangle(2,new(191,147)));
        shell.Art(chrome.Canvas,3,shell.Rectangle(3,new(191,147)),chrome.Assets,chrome.SpriteShader);
        Vector2 mount=shell.Rectangle(4,new(191,147)).Position;
        foreach(var descriptor in model.Groups)
        {
            int id=descriptor.Index;float width=descriptor.Caption.Length*28+36;
            var tab=new UiSourceTabButton{Name="ForeignTradeTab"+id,Position=mount,Size=new(width,64),InputAdmission=ReadAdmission};
            chrome.Canvas.AddChild(tab);tabs.Add(tab);tab.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,descriptor.Caption,
                model.Groups.Length==1?0x9AFB27B7u:id==0?0x172EDC24u:0xD45873AAu);
            tab.Pressed+=()=>{if(ReadAdmission())ShowGroup(id);};mount.X+=width-6;
        }
        headings=new(){Name="ForeignTradeHeaders",Position=new(213,214),Size=new(1464,48),MouseFilter=MouseFilterEnum.Pass};
        body=new(){Name="ForeignTradeRows",Position=new(213,264),Size=new(1464,596),ClipContents=true,MouseFilter=MouseFilterEnum.Pass};
        chrome.Canvas.AddChild(headings);chrome.Canvas.AddChild(body);
        scroll=new(){Name="ForeignTradeScrollbar",Position=new(1687,265),Size=new(16,597)};chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository,chrome.Assets,chrome.SpriteShader,Math.Max(0,model.CurrentIds.Count*48-596),scrollSpeed(),
            _=>PositionRows(),currentScrollSpeed:scrollSpeed);
        body.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
        if(model.Multiple)
        {
            checkGlyph=Message(0x628);selectCaption=Message(0x3A);clearCaption=Message(0x3B);
            var footer=new UiSourceLayout(repository,0xD8913AB2);
            selectAll=new(){Name="ForeignTradeSelectAll",Position=footer.Rectangle(14,new(0,904)).Position,InputAdmission=ReadAdmission};
            chrome.Canvas.AddChild(selectAll);selectAll.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,selectCaption,()=>
            {
                if(!ReadAdmission())return;
                var eligible=publication.Where(Allowed).Select(row=>row.Id).ToArray();
                if(selected.Count>=model.MaximumSelected||eligible.All(selected.Contains))selected.Clear();
                else foreach(int id in eligible){if(selected.Count>=model.MaximumSelected)break;selected.Add(id);}
                RefreshSelection();
            });
        }
        ShowGroup(0);Resized+=Fit;Fit();
        SetMeta("source_selection_mode",model.Multiple?2:1);SetMeta("source_group_count",model.Groups.Length);
    }
    private void ShowGroup(int index)
    {
        if(abandoned||group==index)return;
        if(group>=0)scrollPositions[group]=scroll.Value;
        foreach(var row in pool)row.Button.InputAdmission=null;
        foreach(var header in headers.Values)header.InputAdmission=null;
        Clear(headings);Clear(body);pool.Clear();headers.Clear();group=index;
        columns=model.Groups[index].Columns;publication=model.ReadPage(index);
        int left=model.Multiple?48:0;
        if(model.Multiple)AddHeader(-1,checkGlyph,0,48,true);
        foreach(var column in columns)
        {
            int id=column.Id;if(!nextDirections.ContainsKey((group,id)))nextDirections[(group,id)]=column.InitialAscending;
            AddHeader(id,column.Caption,left,column.Width,publication.All(row=>row.Cells[columns.IndexOf(column)].SortUnresolvedOwner is null));
            left+=column.Width;
        }
        var source=new UiSourceLayout(repository,0xF2C06161);
        checkProfile=MapFontProfile.FromNode(source.Nodes[17],source.Source.Source.ResourceUri);
        for(int slot=0;slot<Math.Min(14,publication.Length);slot++)
        {
            var button=new ForeignTradeSelectionRow{Name="ForeignTradeVisibleRow"+slot,CheckedFront=model.Multiple,
                InputAdmission=ReadAdmission,ActionMode=BaseButton.ActionModeEnum.Release};body.AddChild(button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,"",model.Multiple?1416:1464,model.Multiple);
            UiSourceText? check=model.Multiple?source.Text(button,17,default,"",chrome.Font):null;
            int x=model.Multiple?48:0;var cells=new List<UiSourceText>();
            foreach(var column in columns)
            {
                var label=new UiSourceText{Name="ForeignTradeCell"+column.Id,Position=new(x+10,10),Size=new(column.Width-20,28)};
                button.AddChild(label);cells.Add(label);x+=column.Width;
            }
            var row=new Row(button,check,cells.ToArray());pool.Add(row);
            button.TextVisualStateChanged+=_=>ApplyCells(row);
            button.Pressed+=()=>
            {
                if(!ReadAdmission()||row.Entry is not {} entry||!Allowed(entry))return;
                if(!model.Multiple){selected.Clear();selected.Add(entry.Id);Finish([entry.Id]);return;}
                if(!selected.Remove(entry.Id)&&selected.Count<model.MaximumSelected)selected.Add(entry.Id);
                RefreshSelection();
            };
            button.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
        }
        foreach(var (id,header) in headers)header.ApplySortDirection(arrows.TryGetValue(group,out var arrow)&&arrow.Column==id?arrow.Ascending:null);
        foreach(var tab in tabs.Select((view,id)=>(view,id)))tab.view.Selected=tab.id==group;
        scroll.SetValue(scrollPositions.GetValueOrDefault(group));PositionRows();
        void AddHeader(int id,string caption,int x,int width,bool sortable)
        {
            var header=new UiSourceColumnButton{Name="ForeignTradeColumn"+id,Position=new(x,0),Size=new(width,48),
                InputAdmission=ReadAdmission,Disabled=!sortable};headings.AddChild(header);headers[id]=header;
            header.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,caption,personList:true);header.Pressed+=()=>Sort(id);
        }
    }
    private void Sort(int column)
    {
        if(!ReadAdmission()||headers[column].Disabled)return;
        bool ascending=column<0?nextSelectedAscending:nextDirections[(group,column)];
        if(column<0){model.SortSelection(SelectedIds,!ascending);nextSelectedAscending=!ascending;}
        else
        {
            var result=model.Sort(group,column,ascending);if(!result.Available)return;
            nextDirections[(group,column)]=!ascending;
        }
        arrows[group]=(column,ascending);publication=model.ReadPage(group);
        foreach(var (id,header) in headers)header.ApplySortDirection(id==column?ascending:null);
        PositionRows();
    }
    private void PositionRows()
    {
        if(group<0||abandoned)return;int first=scroll.Value/48;
        for(int slot=0;slot<pool.Count;slot++)
        {
            var row=pool[slot];int index=first+slot;
            if(index>=publication.Length){row.Entry=null;row.Button.Hide();continue;}
            row.Entry=publication[index];row.Button.Position=new(0,index*48-scroll.Value);row.Button.Show();ApplyCells(row);
        }
        RefreshSelection();
    }
    private void ApplyCells(Row row)
    {
        if(row.Entry is not {} entry)return;int state=row.Button.VisualState;
        int ordinary=entry.AlteredHealth==true&&state<3?state==2?68:67:8+Math.Min(state,3);
        if(row.Check is {} check)
        {
            check.Configure(chrome.Font,checkProfile.WithPreset(presets[ordinary]));
            check.Text=selected.Contains(entry.Id)?checkGlyph:"";
        }
        for(int slot=0;slot<columns.Length;slot++)
        {
            var column=columns[slot];var cell=entry.Cells[slot];var label=row.Cells[slot];
            var profile=new MapFontProfile("foreign-trade/dynamic-cell",0,0,.875f,.875f,0,0,
                uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue,0,(byte)(((column.Format-3)<<4)|1),(byte)(column.Format-2),
                0,0,0xFF000000,0xFF000000,0xFF000000,0xFF000000){WidthFitMode=2,StrokeColour=0xFF000000,StrokeWidth=3};
            label.Configure(chrome.Font,profile.WithPreset(presets[cell.Preset??cell.StatePresets?.ForState(state)??ordinary]));
            if(cell.SourceFormattedText is {} formatted&&cell.UnresolvedOwner is null)label.SetPersonnelPreviewLine(formatted);
            else label.Text=cell.UnresolvedOwner is null?cell.Text??"":text.Get("detail_unknown");
            label.FirstGlyphColour=cell.FirstGlyphColour;label.QueueRedraw();
        }
    }
    private bool Allowed(ForeignTradeSelectorRow row)=>row.Enabled&&(rowAdmission?.Invoke(row.Id)??true);
    private void RefreshSelection()
    {
        if(abandoned||chrome is null)return;
        foreach(var row in pool)if(row.Entry is {} entry)
        {row.Button.Selected=selected.Contains(entry.Id);row.Button.Disabled=!Allowed(entry)||model.Multiple&&!selected.Contains(entry.Id)&&selected.Count>=model.MaximumSelected;ApplyCells(row);}
        chrome.ApplyReturn(returnAllowed);chrome.ApplyDecision(true,true);
        if(selectAll is not null)
        {selectAll.Disabled=!publication.Any(Allowed);selectAll.ApplyCaption(selected.Count>=model.MaximumSelected||publication.Where(Allowed).All(row=>selected.Contains(row.Id))?clearCaption:selectCaption);}
    }
    internal void ApplySelectionAdmission(Func<int,bool>? allowed,bool canReturn)
    {if(abandoned)return;rowAdmission=allowed;returnAllowed=canReturn;RefreshSelection();}
    internal void ApplyNotice(string value){if(abandoned)return;finishing=false;Show();chrome.ApplyDefaultHint(value);RefreshSelection();}
    internal void RequestCancel(){if(returnAllowed)Finish(null);}
    private void RequestApply(){if(model.Multiple)Finish(SelectedIds);}
    private void Finish(IReadOnlyList<int>? value)
    {
        if(!ReadAdmission()||value is not null&&value.Any(id=>!publication.Any(row=>row.Id==id&&Allowed(row))))return;
        finishing=true;Hide();closed?.Invoke(value);
    }
    private bool ReadAdmission()=>!abandoned&&!finishing&&closed is not null&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    private string Message(int id)=>repository.LoadMessages(text.SourceLocale,San14.Data.Formats.MessageKind.Data).Messages.Sections[1].Records[id].Text
        ??throw new InvalidDataException("Missing foreign-trade selection caption.");
    private static void Clear(Control owner){foreach(Node child in owner.GetChildren()){owner.RemoveChild(child);child.QueueFree();}}
    private void Fit(){float scale=Math.Min(Size.X/1920,Size.Y/1080);chrome.Canvas.Scale=Vector2.One*scale;chrome.Canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;}
    public override void _ExitTree()=>Abandon();
    internal void Abandon()
    {
        if(abandoned)return;abandoned=true;closed=null;admitted=null;rowAdmission=null;
        foreach(var row in pool){row.Button.InputAdmission=null;row.Button.Disabled=true;}
        foreach(var header in headers.Values){header.InputAdmission=null;header.Disabled=true;}
        foreach(var tab in tabs){tab.InputAdmission=null;tab.Disabled=true;}
        if(selectAll is not null){selectAll.InputAdmission=null;selectAll.Disabled=true;}
        scroll?.SetProcessInput(false);chrome?.Abandon();Hide();
    }
}
