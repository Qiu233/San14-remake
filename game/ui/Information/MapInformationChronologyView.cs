using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original chronology control composition over a separately owned
/// historical map snapshot. Only twelve source rows are retained while scrolling.</summary>
internal partial class MapInformationChronologyView : DetailsView
{
    private readonly List<UiSourceChronologyRow> rows=[];
    private readonly Dictionary<int,UiSourceText> headerFields=[];
    private UiAssets assets=null!;private MapFont font=null!;private Shader shader=null!;
    private Control canvas=null!,rowMount=null!;
    private UiSourceScrollbar scrollbar=null!;
    private UiSourceChronologyPlaybackButton previous=null!,next=null!,autoplay=null!;
    private ChronologyInformationPage page=null!;
    private Func<bool>? admitted;private Action<ChronologyInformationMapSnapshot>? selected;
    private bool abandoned,playing;private double elapsed;private string unknown="";
    internal void Configure(GameResourceRepository repository,PreviewText text,ChronologyInformationPage page,
        Func<bool> admitted,Action close,Action<ChronologyInformationMapSnapshot> selected,Func<int> scrollSpeed, AdministrationPageChrome.Resources resources)
    {
        this.page=page;this.admitted=admitted;this.selected=selected;unknown=text.Get("detail_unknown");
        assets=resources.Assets;font=resources.Font;shader=resources.SpriteShader;
        canvas=new(){Name="ChronologySourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore};AddChild(canvas);
        var title=new UiSourceHeader();canvas.AddChild(title);title.Configure(repository,assets,shader,font,page.Title);
        ConfigureSourcePage("InformationChronology",title,close);MouseFilter=MouseFilterEnum.Ignore;
        var shell=assets.SourceLayout("00_82_08_sum_force_offset_03");
        shell.Art(canvas,3,shell.Rectangle(3),assets,shader);
        var source=assets.SourceLayout("11_05_00_history");
        Vector2 mount=new(0,122);
        Rect2 window=source.Rectangle(1,mount);
        var island=new Control{Name="ChronologyListInput",Position=window.Position,Size=window.Size,MouseFilter=MouseFilterEnum.Stop};canvas.AddChild(island);
        source.Blur(canvas,3,source.Rectangle(3,mount));source.Blur(canvas,4,source.Rectangle(4,mount));
        source.Art(canvas,5,source.Rectangle(5,mount),assets,shader);
        var messages=(resources.Messages??throw new InvalidDataException("information.chronology.prepared_messages"))
            .Messages.Sections[1].Records;
        string Msg(int id)=>messages[id].Text??throw new InvalidDataException($"Missing chronology Data1/{id:X}.");
        Rect2 tabBox=source.Rectangle(6,mount);
        var tab=new UiSourceTabButton{Position=tabBox.Position,Size=tabBox.Size,InputAdmission=ReadAdmission};canvas.AddChild(tab);
        tab.Configure(repository,assets,shader,font,Msg(0x2E4),assets.SourceLayout("cmn_btn_tab_l").Source.Source.FileId);tab.Selected=true;
        foreach(var (node,caption) in new[]{(25,page.FirstCaption),(24,page.SecondCaption)})
        {
            Rect2 box=source.Rectangle(node,mount);
            var column=new UiSourceColumnButton{Position=box.Position,Size=box.Size,InputAdmission=ReadAdmission};canvas.AddChild(column);
            column.Configure(repository,assets,shader,font,caption,normalLayout:true);
        }
        Rect2 area=source.Rectangle(41,mount);
        rowMount=new(){Name="ChronologyRows",Position=area.Position,Size=area.Size,ClipContents=true,MouseFilter=MouseFilterEnum.Stop};canvas.AddChild(rowMount);
        for(int slot=0;slot<Math.Min(12,page.Rows.Length);slot++)
        {
            var row=new UiSourceChronologyRow{Name="ChronologyVisibleRow"+slot,InputAdmission=ReadAdmission};rowMount.AddChild(row);
            row.Configure(repository,assets,shader,font);rows.Add(row);
            row.Pressed+=()=>{if(ReadAdmission()&&row.BoundIndex>=0)Select(row.BoundIndex);};
            row.GuiInput+=input=>{if(ReadAdmission())scrollbar.HandleWheel(input);};
        }
        Rect2 scroll=source.Rectangle(16,mount);
        scrollbar=new(){Position=scroll.Position,Size=scroll.Size};canvas.AddChild(scrollbar);
        scrollbar.Configure(repository,assets,shader,Math.Max(0,page.Rows.Length*58-(int)area.Size.Y),scrollSpeed(),_=>PositionRows(),currentScrollSpeed:scrollSpeed);
        rowMount.GuiInput+=input=>{if(ReadAdmission())scrollbar.HandleWheel(input);};
        previous=Playback(63,"000_cmn_icon_vew_old",()=>Step(false));
        next=Playback(62,"000_cmn_icon_advance_play",()=>Step(true));
        autoplay=Playback(61,"000_cmn_icon_vew_onoff",()=>{playing=!playing&&page.CanNext;elapsed=0;RefreshControls();});
        var header=assets.SourceLayout("11_05_00_history_offset_02");
        foreach(var node in header.Nodes.Where(node=>node.Type is 1 or 3))header.Art(canvas,node.Index,header.Rectangle(node.Index),assets,shader);
        header.Text(canvas,3,default,page.FirstCaption,font,MapTextPresets.Create()[13]);
        header.Text(canvas,7,default,page.SecondCaption,font,MapTextPresets.Create()[13]);
        foreach(int node in new[]{4,5,6,8})headerFields[node]=header.Text(canvas,node,default,"",font);
        var footer=assets.SourceLayout(0xD8913AB2);
        var back=new UiSourceRoundButton{Position=footer.Rectangle(1,new(0,904)).Position,InputAdmission=ReadAdmission};canvas.AddChild(back);
        back.Configure(repository,assets,shader,text.SourceLocale,"000_cmn_txt_decision_modoru",RequestClose,UiAudioCue.Cancel);
        Resized+=Fit;Fit();PositionRows();RefreshSelection(false);
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SetMeta("source_order_unknown",page.SortUnresolvedOwner??"");

        UiSourceChronologyPlaybackButton Playback(int node,string icon,Action action)
        {
            var button=new UiSourceChronologyPlaybackButton{Position=source.Rectangle(node,mount).Position,InputAdmission=ReadAdmission};canvas.AddChild(button);
            button.Configure(repository,assets,shader,icon,()=>{if(ReadAdmission())action();});return button;
        }
    }
    private bool ReadAdmission()=>!abandoned&&IsVisibleInTree()&&(admitted?.Invoke()??false);
    private void Select(int index)
    {
        if(!page.Select(index))return;RefreshSelection(true);
    }
    private void Step(bool forward)
    {
        playing=false;elapsed=0;if(page.Step(forward))RefreshSelection(true);else RefreshControls();
    }
    private void RefreshSelection(bool publish)
    {
        if(page.Selected is { } row)
        {
            headerFields[4].Text=row.YearText??unknown;headerFields[5].Text=row.MonthText??unknown;headerFields[6].Text=row.PeriodText??unknown;
            headerFields[8].SetKnownColourLine(row.HeaderNarrative??unknown);
            int top=page.SelectedIndex*58,bottom=top+58;
            if(top<scrollbar.Value)scrollbar.SetValue(top);else if(bottom>scrollbar.Value+rowMount.Size.Y)scrollbar.SetValue(bottom-(int)rowMount.Size.Y);
            if(publish)selected?.Invoke(row.Map);
        }
        PositionRows();RefreshControls();
    }
    private void RefreshControls()
    {
        previous.Disabled=!page.CanPrevious;next.Disabled=autoplay.Disabled=!page.CanNext;
        if(!page.CanNext)playing=false;
        autoplay.Selected=playing;autoplay.ApplyIcon(playing?"000_cmn_icon_advance_stop":"000_cmn_icon_vew_onoff");
    }
    private void PositionRows()
    {
        int first=scrollbar.Value/58;
        for(int slot=0;slot<rows.Count;slot++)
        {
            int index=first+slot;var display=rows[slot];
            if(index>=page.Rows.Length){display.Hide();continue;}
            display.Position=new(0,index*58-scrollbar.Value);display.Show();
            if(display.BoundIndex!=index)display.Apply(index,page.Rows[index],unknown);
            display.Selected=index==page.SelectedIndex;
        }
    }
    public override void _Process(double delta)
    {
        if(!playing||!ReadAdmission())return;elapsed+=delta*1000;
        if(elapsed<ChronologyInformationPage.PlaybackStepMilliseconds)return;
        elapsed=0;if(page.Step(true))RefreshSelection(true);else{playing=false;RefreshControls();}
    }
    private void Fit()
    {
        if(canvas is null)return;float scale=Math.Min(Size.X/1920,Size.Y/1080);
        canvas.Scale=Vector2.One*scale;canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }
    public override void _ExitTree()
    {
        abandoned=true;playing=false;admitted=null;selected=null;rows.Clear();
        base._ExitTree();
    }
}
