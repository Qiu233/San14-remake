using Godot;
using San14.Data;

internal sealed record CurrentWorldItemSelectionState(int Scroll,int? SortedColumn,bool Ascending,
    IReadOnlyDictionary<int,bool> NextDirections,IReadOnlyList<int> Order);

/// <summary>The live-world editor's Item selector owns its checked mode,
/// retained order and return state independently of the title Item library.</summary>
internal partial class CurrentWorldItemSelectionView : Control
{
    private sealed class Row(UiSourceListRow button)
    {
        internal UiSourceListRow Button { get; }=button;
        internal List<UiSourceText> Cells { get; }=[];
        internal UiSourceText? Check { get; set; }
        internal DefinitionInformationRow? Entry { get; set; }
    }
    private AdministrationPageChrome chrome=null!;
    private UiSourceScrollbar scroll=null!;
    private UiSourceBottomButton? selectAll;
    private CurrentWorldItemEditSelection source=null!;
    private IReadOnlyList<DefinitionInformationColumn> columns=[];
    private IReadOnlyList<DefinitionInformationRow> rows=[];
    private readonly List<Row> pool=[];
    private readonly List<int> selected=[];
    private readonly Dictionary<int,UiSourceColumnButton> headers=[];
    private readonly Dictionary<int,bool> nextDirections=[];
    private readonly MapTextPresets presets=MapTextPresets.Create();
    private readonly Dictionary<int,CurrentWorldItemEditRow> candidates=[];
    private Func<bool>? admitted;
    private Action<IReadOnlyList<int>?>? closed;
    private string checkGlyph="",unknown="",selectCaption="",clearCaption="";
    private MapFontProfile? checkProfile;
    private int? sortedColumn;
    private bool ascending,abandoned;
    internal IReadOnlyList<int> SelectedIds=>selected.ToArray();

    internal void Configure(GameResourceRepository repository,PreviewText text,CurrentWorldItemEditSelection source,
        IReadOnlyList<int> initial,Func<bool> admitted,Action<IReadOnlyList<int>?> closed,Func<int> scrollSpeed,
        CurrentWorldItemSelectionState? retained=null)
    {
        this.source=source;this.admitted=admitted;this.closed=closed;unknown=text.Get("detail_unknown");
        foreach(var candidate in source.Rows)candidates.Add(candidate.ItemId,candidate);
        columns=source.Pages.Groups[0].Columns.Where(column=>column.Visible).ToArray();
        rows=source.Pages.ReadPage(0).Rows;
        foreach(int id in initial)if(candidates.TryGetValue(id,out var row)&&!row.IsUnavailable&&!selected.Contains(id))selected.Add(id);
        Name=source.Batch?"CurrentWorldItemsSelection":"CurrentWorldItemSelection";
        ZIndex=5;MouseFilter=MouseFilterEnum.Stop;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop=new UiSourceCommonBlur();AddChild(backdrop);backdrop.Configure(repository);
        chrome=new();AddChild(chrome);chrome.Configure(repository,text,source.TitleMessageId,RequestCancel,Accept,returnOnly:!source.Batch);
        chrome.ApplyDefaultHint(source.GuideText);
        chrome.ApplyReturnHint(new AdministrationMessages(repository,text.SourceLocale).Format(0x2733));
        var shell=new UiSourceLayout(repository,0xA3B4A82E);
        shell.Blur(chrome.Canvas,2,shell.Rectangle(2,new(191,147)));
        shell.Art(chrome.Canvas,3,shell.Rectangle(3,new(191,147)),chrome.Assets,chrome.SpriteShader);
        var group=source.Pages.Groups[0];
        var tab=new UiSourceTabButton{Name="CurrentWorldItemBasic",Position=shell.Rectangle(4,new(191,147)).Position,
            Size=new(group.Caption.Length*28+36,64),InputAdmission=ReadAdmission,Selected=true};
        chrome.Canvas.AddChild(tab);tab.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,group.Caption,0x172EDC24u);
        var header=new Control{Name="CurrentWorldItemColumns",Position=new(213,214),Size=new(1464,48),MouseFilter=MouseFilterEnum.Pass};
        var body=new Control{Name="CurrentWorldItemRows",Position=new(213,264),Size=new(1464,596),ClipContents=true,MouseFilter=MouseFilterEnum.Pass};
        chrome.Canvas.AddChild(header);chrome.Canvas.AddChild(body);
        var data=repository.LoadMessages(text.SourceLocale,San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
        checkGlyph=data.Records[0x628].Text??throw new InvalidDataException("Missing Item check glyph.");
        selectCaption=data.Records[0x3A].Text??throw new InvalidDataException("Missing Item select-all caption.");
        clearCaption=data.Records[0x3B].Text??throw new InvalidDataException("Missing Item clear-all caption.");
        int left=source.Batch?48:0;
        foreach(var column in columns)
        {
            int id=column.Id;
            var button=new UiSourceColumnButton{Name="CurrentWorldItemColumn"+id,Position=new(left,0),Size=new(column.Width,48),
                InputAdmission=ReadAdmission,Disabled=!column.CanSort||rows.Any(row=>row.Cells[CellIndex(id)].SortUnresolvedOwner is not null)};
            header.AddChild(button);headers.Add(id,button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,column.Caption,personList:true);
            nextDirections[id]=retained?.NextDirections.GetValueOrDefault(id)??column.InitialAscending;
            button.Pressed+=()=>Sort(id);left+=column.Width;
        }
        if(source.Batch)
        {
            var checkedHeader=new UiSourceColumnButton{Name="CurrentWorldItemCheckedColumn",Size=new(48,48),InputAdmission=ReadAdmission};
            header.AddChild(checkedHeader);checkedHeader.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,checkGlyph,personList:true);
            headers.Add(-1,checkedHeader);nextDirections[-1]=retained?.NextDirections.GetValueOrDefault(-1)??false;
            checkedHeader.Pressed+=()=>Sort(-1);
            var footer=new UiSourceLayout(repository,0xD8913AB2);
            selectAll=new(){Name="CurrentWorldItemSelectAll",Position=footer.Rectangle(14,new(0,904)).Position,InputAdmission=ReadAdmission};
            chrome.Canvas.AddChild(selectAll);selectAll.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,
                selectCaption,()=>
                {
                    if(!ReadAdmission())return;
                    var eligible=rows.Where(row=>Enabled(row.DefinitionId)).Select(row=>row.DefinitionId).ToArray();
                    if(eligible.All(selected.Contains))selected.Clear();else{selected.Clear();selected.AddRange(eligible);}
                    RefreshRows();
                });
        }
        scroll=new(){Name="CurrentWorldItemScrollbar",Position=new(1687,265),Size=new(16,597)};chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository,chrome.Assets,chrome.SpriteShader,Math.Max(0,rows.Count*48-596),scrollSpeed(),_=>PositionRows(),currentScrollSpeed:scrollSpeed);
        body.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
        var rowSource=new UiSourceLayout(repository,0xF2C06161);
        checkProfile=MapFontProfile.FromNode(rowSource.Nodes[17],rowSource.Source.Source.ResourceUri);
        for(int index=0;index<Math.Min(14,rows.Count);index++)
        {
            var button=new UiSourceListRow{Name="CurrentWorldItemVisibleRow"+index,InputAdmission=ReadAdmission,ActionMode=BaseButton.ActionModeEnum.Release};
            body.AddChild(button);button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,"",source.Batch?1416:1464,source.Batch);
            var row=new Row(button);pool.Add(row);
            if(source.Batch)row.Check=rowSource.Text(button,17,default,"",chrome.Font);
            button.TextVisualStateChanged+=_=>ApplyCells(row);
            button.Pressed+=()=>
            {
                if(!ReadAdmission()||row.Entry is not {} entry||!Enabled(entry.DefinitionId))return;
                if(!source.Batch){selected.Clear();selected.Add(entry.DefinitionId);Finish(selected.ToArray());return;}
                if(!selected.Remove(entry.DefinitionId))selected.Add(entry.DefinitionId);RefreshRows();
            };
            button.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
        }
        if(retained is {} state){sortedColumn=state.SortedColumn;ascending=state.Ascending;scroll.SetValue(state.Scroll);}
        foreach(var(id,button)in headers)button.ApplySortDirection(id==sortedColumn?ascending:null);
        SetMeta("source_selection_mode",source.Batch?2:1);SetMeta("source_initial_selection_empty",initial.Count==0);
        PositionRows();
    }
    private int CellIndex(int id)
    {
        var all=source.Pages.Groups[0].Columns;
        for(int index=0;index<all.Length;index++)if(all[index].Id==id)return index;
        throw new InvalidDataException("current-world-item.selector.column");
    }
    private bool Enabled(int id)=>candidates.TryGetValue(id,out var row)&&!row.IsUnavailable;
    private bool ReadAdmission()=>!abandoned&&closed is not null&&IsVisibleInTree()&&CanProcess()&&admitted?.Invoke()==true;
    private void Sort(int id)
    {
        if(!ReadAdmission()||headers[id].Disabled)return;
        bool direction=nextDirections[id];var result=id<0?source.SortSelection(selected,direction):source.Pages.Sort(0,id,direction);
        if(!result.Available)return;
        rows=result.Value!.Rows;nextDirections[id]=!direction;sortedColumn=id;ascending=direction;
        foreach(var(key,button)in headers)button.ApplySortDirection(key==id?direction:null);
        PositionRows();
    }
    private void PositionRows()
    {
        int first=scroll.Value/48;
        for(int slot=0;slot<pool.Count;slot++)
        {
            var row=pool[slot];int index=first+slot;
            if(index>=rows.Count){row.Entry=null;row.Button.Hide();continue;}
            row.Entry=rows[index];row.Button.Position=new(0,index*48-scroll.Value);row.Button.Show();
        }
        RefreshRows();
    }
    private void RefreshRows()
    {
        foreach(var row in pool)if(row.Entry is {} entry)
        {row.Button.Selected=selected.Contains(entry.DefinitionId);row.Button.Disabled=!Enabled(entry.DefinitionId);ApplyCells(row);}
        if(selectAll is not null)
        {
            var eligible=rows.Where(row=>Enabled(row.DefinitionId)).ToArray();
            selectAll.Disabled=eligible.Length==0;
            selectAll.ApplyCaption(eligible.All(row=>selected.Contains(row.DefinitionId))?clearCaption:selectCaption);
        }
        chrome.ApplyDecision(selected.Count>0,true);
    }
    private void ApplyCells(Row row)
    {
        if(row.Entry is not {} entry)return;
        int state=row.Button.VisualState,preset=8+Math.Min(state,3),left=source.Batch?48:0;
        if(row.Check is {} check){check.Configure(chrome.Font,checkProfile!.WithPreset(presets[preset]));check.Text=selected.Contains(entry.DefinitionId)?checkGlyph:"";}
        for(int index=0;index<columns.Count;index++)
        {
            var column=columns[index];var cell=entry.Cells[CellIndex(column.Id)];
            if(index==row.Cells.Count){var label=new UiSourceText{Name="CurrentWorldItemCell"+column.Id};row.Button.AddChild(label);row.Cells.Add(label);}
            var text=row.Cells[index];text.Position=new(left+10,10);text.Size=new(column.Width-20,28);
            var profile=new MapFontProfile("current-world-item-selection/dynamic-cell",0,0,.875f,.875f,0,0,
                uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue,0,(byte)(((column.CellFormat-3)<<4)|1),
                (byte)(column.CellFormat-2),0,0,0xFF000000,0xFF000000,0xFF000000,0xFF000000)
                {WidthFitMode=2,WidthReserve=0,StrokeColour=0xFF000000,StrokeWidth=3};
            text.Configure(chrome.Font,profile.WithPreset(presets[cell.PresetIndex??preset]));
            text.Text=cell.UnresolvedOwner is null?cell.Text??"":unknown;text.FirstGlyphColour=cell.MarkerColour?.CpuPacked;
            left+=column.Width;
        }
    }
    internal CurrentWorldItemSelectionState CapturePageState()=>new(scroll.Value,sortedColumn,ascending,new Dictionary<int,bool>(nextDirections),rows.Select(row=>row.DefinitionId).ToArray());
    internal void ApplyNotice(string value){if(!abandoned)chrome.ApplyDefaultHint(value);}
    private void Accept(){if(source.Batch&&selected.Count>0&&selected.All(Enabled))Finish(selected.ToArray());}
    private void Finish(IReadOnlyList<int>? result){if(!ReadAdmission())return;Hide();closed?.Invoke(result);}
    internal void RequestCancel()=>Finish(null);
    internal void Abandon(){abandoned=true;admitted=null;closed=null;chrome?.Abandon();Hide();}
}
