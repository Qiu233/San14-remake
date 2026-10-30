using Godot;
using San14.Data;

/// <summary>Transport's single mode1 Person page. A legal row release returns
/// immediately; candidates, cells and sorting retain the same current owner.</summary>
internal partial class TransportPersonSelectionView : Control
{
    private sealed class Row(UiSourceListRow button)
    {
        internal UiSourceListRow Button{get;}=button;
        internal readonly Dictionary<int,UiSourceText> Cells=[];
        internal OfficerInformationRow? Entry;
    }
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas=null!,body=null!,headings=null!;
    private UiSourceHeader title=null!;
    private UiSourceScrollbar scroll=null!;
    private UiSourceRoundButton back=null!;
    private UiSourceText guide=null!;
    private TransportPersonSelection model=null!;
    private OfficerInformationPage publication=null!;
    private OfficerInformationColumn[] columns=[];
    private Dictionary<int,int> slots=[];
    private readonly List<Row> rows=[];
    private readonly Dictionary<int,UiSourceColumnButton> headers=[];
    private readonly Dictionary<int,bool> nextAscending=[];
    private readonly MapTextPresets presets=MapTextPresets.Create();
    private Func<bool>? admitted;
    private Action<int?>? complete;
    private PreviewText text=null!;
    private string disabledHint="",returnHint="";
    private bool abandoned;

    internal void Configure(GameResourceRepository repository,PreviewText text,TransportPersonSelection model,
        Func<bool> admitted,Action<int?> complete,Func<int> scrollSpeed)
    {
        this.text=text;this.model=model;this.admitted=admitted;this.complete=complete;
        Name="TransportPersonSelection";ZIndex=5;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop=new UiSourceCommonBlur();AddChild(backdrop);backdrop.Configure(repository);
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        canvas=new(){Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Stop};AddChild(canvas);
        title=new(){Name="TransportPersonTitle"};canvas.AddChild(title);
        title.Configure(repository,assets,shader,font,model.Pages.Title);
        var shell=new UiSourceLayout(repository,0xA3B4A82E);
        shell.Blur(canvas,2,shell.Rectangle(2,new(191,147)));
        shell.Art(canvas,3,shell.Rectangle(3,new(191,147)),assets,shader);
        var group=model.Pages.Groups.Single();
        var tab=new UiSourceTabButton{Name="TransportPersonBasic",Position=shell.Rectangle(4,new(191,147)).Position,
            Size=new(group.Caption.Length*28+36,64),Selected=true,InputAdmission=ReadAdmission};
        canvas.AddChild(tab);tab.Configure(repository,assets,shader,font,group.Caption,0x9AFB27B7);
        publication=model.Pages.ReadPage(0);columns=group.Columns.Where(column=>column.Visible).ToArray();
        slots=group.Columns.Select((column,slot)=>(column.Id,slot)).ToDictionary(item=>item.Id,item=>item.slot);
        headings=new(){Name="TransportPersonHeaders",Position=new(213,214),Size=new(1464,48),MouseFilter=MouseFilterEnum.Pass};
        body=new(){Name="TransportPersonRows",Position=new(213,264),Size=new(1464,596),ClipContents=true,MouseFilter=MouseFilterEnum.Pass};
        canvas.AddChild(headings);canvas.AddChild(body);
        int left=0;
        foreach(var column in columns)
        {
            int id=column.Id;nextAscending[id]=column.InitialAscending;
            var header=new UiSourceColumnButton{Name="TransportPersonColumn"+id,Position=new(left,0),Size=new(column.Width,48),
                InputAdmission=ReadAdmission,Disabled=!column.CanSort||publication.Rows.Any(row=>row.Cells[slots[id]].SortUnresolvedOwner is not null)};
            headings.AddChild(header);headers.Add(id,header);
            header.Configure(repository,assets,shader,font,column.Caption,personList:true);header.Pressed+=()=>Sort(id);left+=column.Width;
        }
        scroll=new(){Name="TransportPersonScrollbar",Position=new(1687,265),Size=new(16,597)};canvas.AddChild(scroll);
        scroll.Configure(repository,assets,shader,Math.Max(0,publication.Rows.Length*48-596),scrollSpeed(),
            _=>PositionRows(),currentScrollSpeed:scrollSpeed);
        body.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
        for(int index=0;index<Math.Min(14,publication.Rows.Length);index++)
        {
            var button=new UiSourceListRow{Name="TransportPersonRow"+index,InputAdmission=ReadAdmission,ActionMode=BaseButton.ActionModeEnum.Release};
            body.AddChild(button);button.Configure(repository,assets,shader,font,"",1464,false);
            var row=new Row(button);rows.Add(row);
            button.TextVisualStateChanged+=_=>ApplyCells(row);
            button.MouseEntered+=()=>ShowRowHint(row);button.MouseExited+=()=>{if(ReadAdmission())guide.Text=model.GuideText;};
            button.Pressed+=()=>{if(ReadAdmission()&&row.Entry is {} entry&&Allowed(entry.OfficerId))Finish(entry.OfficerId);};
            button.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
        }
        var footer=new UiSourceLayout(repository,0xD8913AB2);Vector2 footerMount=new(0,904);
        back=new(){Name="ReturnTransportPerson",Position=footer.Rectangle(1,footerMount).Position,InputAdmission=ReadAdmission};canvas.AddChild(back);
        back.Configure(repository,assets,shader,text.SourceLocale,"000_cmn_txt_decision_modoru",RequestCancel,UiAudioCue.Cancel);
        var narration=new UiSourceLayout(repository,0xAB6ADAA7);Vector2 hintMount=footer.Rectangle(19,footerMount).Position;
        foreach(var node in narration.Nodes.Where(node=>node.Type is 1 or 3))narration.Art(canvas,node.Index,narration.Rectangle(node.Index,hintMount),assets,shader);
        guide=narration.Text(canvas,narration.Nodes.Single(node=>node.Type==2).Index,hintMount,model.GuideText,font);
        var hints=new AdministrationMessages(repository,text.SourceLocale);disabledHint=hints.Format(0x5694);returnHint=hints.Format(0x2733);
        back.MouseEntered+=()=>{if(ReadAdmission())guide.Text=returnHint;};back.MouseExited+=()=>{if(ReadAdmission())guide.Text=model.GuideText;};
        SetMeta("source_selection_mode",1);SetMeta("source_person_page_count",1);SetMeta("source_candidate_count",publication.Rows.Length);
        PositionRows();Resized+=Fit;Fit();
    }
    private void Sort(int column)
    {
        if(!ReadAdmission()||headers[column].Disabled)return;
        bool ascending=nextAscending[column];var sorted=model.Pages.Sort(0,column,ascending);
        if(!sorted.Available||sorted.Value is not {} page){guide.Text=text.Get("detail_unknown");return;}
        publication=page;nextAscending[column]=!ascending;
        foreach(var (id,header) in headers)header.ApplySortDirection(id==column?ascending:null);
        PositionRows();
    }
    private void PositionRows()
    {
        int first=scroll.Value/48;
        for(int slot=0;slot<rows.Count;slot++)
        {
            var row=rows[slot];int index=first+slot;
            if(index>=publication.Rows.Length){row.Entry=null;row.Button.Hide();continue;}
            row.Entry=publication.Rows[index];row.Button.Position=new(0,index*48-scroll.Value);
            row.Button.Selected=row.Entry.OfficerId==model.InitialOfficerId;
            row.Button.Disabled=!Allowed(row.Entry.OfficerId);row.Button.Show();ApplyCells(row);
            if(row.Button.IsHovered())ShowRowHint(row);
        }
    }
    private void ApplyCells(Row row)
    {
        if(row.Entry is not {} entry)return;
        int state=row.Button.VisualState;var health=model.AlteredHealth[entry.OfficerId];
        int ordinary=health.Available&&health.Value&&state<3?state==2?68:67:8+Math.Min(state,3);int left=0;
        foreach(var column in columns)
        {
            if(!row.Cells.TryGetValue(column.Id,out var label))
            {label=new(){Name="TransportPersonCell"+column.Id};row.Button.AddChild(label);row.Cells.Add(column.Id,label);}
            var cell=entry.Cells[slots[column.Id]];
            label.Position=new(left+10,10);label.Size=new(column.Width-20,28);
            var profile=new MapFontProfile("transport-person/dynamic-cell",0,0,.875f,.875f,0,0,
                uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue,0,(byte)(((column.CellFormat-3)<<4)|1),
                (byte)(column.CellFormat-2),0,0,0xFF000000,0xFF000000,0xFF000000,0xFF000000)
                {WidthFitMode=2,WidthReserve=0,StrokeColour=0xFF000000,StrokeWidth=3};
            label.Configure(font!,profile.WithPreset(presets[cell.StatePresets?.ForState(state)??cell.PresetIndex??ordinary]));
            label.Text=cell.UnresolvedOwner is null?cell.Text??"":text.Get("detail_unknown");
            label.FirstGlyphColour=cell.MarkerColour?.CpuPacked;label.QueueRedraw();left+=column.Width;
        }
    }
    private void ShowRowHint(Row row)
    {if(ReadAdmission())guide.Text=row.Entry is {} entry&&!Allowed(entry.OfficerId)?disabledHint:model.GuideText;}
    private bool Allowed(int id)=>model.Enabled.GetValueOrDefault(id);
    private bool ReadAdmission()=>!abandoned&&complete is not null&&IsInsideTree()&&IsVisibleInTree()&&CanProcess()&&admitted?.Invoke()==true;
    internal void RegisterHeader(UiSourceHeaderStack stack)=>title.Publish(stack);
    internal void RequestCancel()=>Finish(null);
    private void Finish(int? id)
    {if(!ReadAdmission())return;var callback=complete;complete=null;admitted=null;Hide();callback?.Invoke(id);}
    internal void Abandon()
    {if(abandoned)return;abandoned=true;complete=null;admitted=null;foreach(var row in rows){row.Button.InputAdmission=null;row.Button.Disabled=true;}Hide();}
    private void Fit()
    {float scale=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*scale;canvas.Position=(Size-canvas.Size*scale)/2;}
    public override void _ExitTree(){Abandon();font?.Dispose();assets?.Dispose();shader?.Dispose();}
}
