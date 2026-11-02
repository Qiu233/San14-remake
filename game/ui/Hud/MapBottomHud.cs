using Godot;
using San14.Data;

/// <summary>The independent ordinary CMainAdvice/CMainLog pages. The current
/// world provides actual messages and log records; this owner retains only UI state.</summary>
internal partial class MapBottomHud : Control
{
    private MapSummaryResources resources=null!;
    private Control advice=null!,log=null!,filter=null!;
    private UiSourceParagraph paragraph=null!;
    private UiSourcePortrait? portrait;
    private (string?,string?) portraitIdentity;
    private int portraitVersion=-1;
    private MapAdvicePublication? publication;
    private MapBottomLogPublication? logPublication;
    private IReadOnlyDictionary<int,OfficerPortraitRoute> logPortraits=new Dictionary<int,OfficerPortraitRoute>();
    private MapBottomSwitchButton adviceSwitch=null!,logSwitch=null!;
    private MapBottomFunctionButton next=null!,run=null!,expand=null!,filterSwitch=null!;
    private UiSourceSimpleTooltip tooltip=null!;
    private Action<MapAdviceEntry> execute=null!;
    private Action<int> focus=null!;
    private Func<bool> admitted=null!;
    private readonly bool[] filters=new bool[6];
    private readonly List<Control> logRows=[];
    private readonly MapBottomFilterButton[] filterButtons=new MapBottomFilterButton[6];
    private UiSourceScrollbar? scrollbar;
    private bool logMode,expanded;
    private int selected,scroll;
    private double elapsed;
    private readonly Dictionary<int,string> data=[];

    internal void Configure(MapSummaryResources resources,string locale,Func<bool> admitted,
        Action<MapAdviceEntry> execute,Action<int> focus)
    {
        this.resources=resources;this.admitted=admitted;this.execute=execute;this.focus=focus;
        Name="OrdinaryMapBottomHud";Size=new(1920,1080);MouseFilter=MouseFilterEnum.Ignore;
        var messages=resources.Repository.LoadMessages(locale,San14.Data.Formats.MessageKind.Data).Messages.Sections[1].Records;
        foreach(int id in new[]{0x61A,0x61B,0x602,0x603,0x604,0x605,0x5F7,0x3FB,2,1,3,0x563})data[id]=messages[id].Text!;
        var adviceSource=resources.Layout("00_02_00_mein_offset_50");
        advice=new(){Name="OrdinaryAdvice",Size=Size,MouseFilter=MouseFilterEnum.Ignore};AddChild(advice);
        var background=adviceSource.Rectangle(2);
        var capture=new Control{Name="AdviceInputSurface",Position=background.Position,Size=background.Size,MouseFilter=MouseFilterEnum.Stop};advice.AddChild(capture);
        // CMainAdviceLayout884580 explicitly applies source sprite palette4 to node2.
        adviceSource.Art(advice,2,background,resources.Assets,resources.Shader,resources.Palettes[4]);
        var box=adviceSource.Rectangle(3);
        paragraph=new(){Name="CurrentAdvice",Position=box.Position,Size=box.Size};advice.AddChild(paragraph);
        paragraph.ConfigureFormattedMessage(resources.Font,MapFontProfile.FromNode(adviceSource.Nodes[3],adviceSource.Source.Source.ResourceUri));
        adviceSwitch=new(){Name="ShowAdvice",Position=adviceSource.Rectangle(15).Position,InputAdmission=admitted};AddChild(adviceSwitch);
        adviceSwitch.Configure(resources,"00_02_00_mein_offset_44",()=>SelectPage(false));
        logSwitch=new(){Name="ShowMapLog",Position=adviceSource.Rectangle(21).Position,InputAdmission=admitted};AddChild(logSwitch);
        logSwitch.Configure(resources,"00_02_00_mein_offset_45",()=>SelectPage(true));
        next=Function(adviceSource.Rectangle(10).Position,"000_cmn_icon_jogen",NextAdvice);
        run=Function(adviceSource.Rectangle(27).Position,"000_cmn_icon_jogen_run",ExecuteAdvice,expansion:true);
        log=new(){Name="OrdinaryMapLog",Size=Size,MouseFilter=MouseFilterEnum.Ignore,Visible=false};AddChild(log);
        var logSource=resources.Layout("00_02_00_mein_offset_40");
        expand=Function(logSource.Rectangle(91).Position,"000_cmn_icon_log_add",()=>{expanded=!expanded;scroll=0;RefreshLog();RefreshMode();},true);
        filterSwitch=Function(logSource.Rectangle(96).Position,"000_cmn_icon_log_filter",()=>{filter.Visible=!filter.Visible;filterSwitch.Selected=filter.Visible;},true);
        filter=new(){Name="LogCategoryFilter",Position=logSource.Rectangle(63).Position,Size=new(348,102),MouseFilter=MouseFilterEnum.Stop,Visible=false};AddChild(filter);
        var filterSource=resources.Layout("00_02_00_mein_offset_43");
        filterSource.Blur(filter,1,filterSource.Rectangle(1));filterSource.Art(filter,2,filterSource.Rectangle(2),resources.Assets,resources.Shader);
        int[] nodes=[3,7,11,15,19,23],captions=[0x5F7,0x3FB,2,1,3,0x563];
        for(int i=0;i<6;i++)
        {
            int index=i;var b=new MapBottomFilterButton{Name="LogCategory"+i,Position=filterSource.Rectangle(nodes[i]).Position,Size=filterSource.Rectangle(nodes[i]).Size,InputAdmission=admitted};filter.AddChild(b);
            b.Configure(resources,data[captions[i]],()=>{filters[index]=!filters[index];b.Selected=filters[index];scroll=0;RefreshLog();});filterButtons[i]=b;
        }
        tooltip=new();AddChild(tooltip);tooltip.Configure(resources.Repository,resources.Assets,resources.Shader,resources.Font);
        var hint=new AdministrationMessages(resources.Repository,locale);
        Hover(adviceSwitch,()=>data[0x61A]);Hover(logSwitch,()=>data[0x61B]);
        Hover(next,()=>hint.Format(0x5BDC));Hover(run,()=>hint.Format(0x5BDD));
        Hover(expand,()=>data[expanded?0x603:0x602]);Hover(filterSwitch,()=>data[filter.Visible?0x605:0x604]);
        RefreshMode();
        MapBottomFunctionButton Function(Vector2 position,string icon,Action pressed,bool checkable=false,bool expansion=false)
        {var b=new MapBottomFunctionButton{Position=position,InputAdmission=admitted};AddChild(b);b.Configure(resources,icon,pressed,checkable,expansion);return b;}
        void Hover(Control owner,Func<string> text)
        {owner.MouseEntered+=()=>{if(admitted())tooltip.DisplayKnownColourText(text());};owner.MouseExited+=()=>tooltip.DisplayText(null);}
    }

    internal void Apply(MapAdvicePublication? advicePublication,MapBottomLogPublication? logPublication,
        IReadOnlyDictionary<int,OfficerPortraitRoute> portraits)
    {
        publication=advicePublication;this.logPublication=logPublication;logPortraits=portraits;selected=0;elapsed=0;
        ApplyAdvice();RefreshLog();RefreshMode();
    }
    internal void Tick(double delta)
    {
        if(!IsVisibleInTree()||logMode||publication?.Entries.Count is not >1)return;
        elapsed+=delta;if(elapsed>=10){elapsed=0;NextAdvice();}
    }
    private void SelectPage(bool logPage)
    {logMode=logPage;filter.Hide();tooltip.DisplayText(null);RefreshMode();RefreshLog();}
    private void NextAdvice()
    {if(publication?.Entries.Count is not >0)return;selected=(selected+1)%publication.Entries.Count;elapsed=0;ApplyAdvice();}
    private void ExecuteAdvice()
    {if(admitted()&&publication is{} p&&selected<p.Entries.Count&&p.Entries[selected].Action is >=1 and <=4)execute(p.Entries[selected]);}
    private void ApplyAdvice()
    {
        var entry=publication is{} p&&selected<p.Entries.Count?p.Entries[selected]:null;
        paragraph.Apply(entry?.Body??"");run.Disabled=entry?.Action is not (>=1 and <=4);next.Disabled=publication?.Entries.Count is not >1;
        var route=publication?.Portrait;var key=route is{Kind:OfficerPortraitRouteKind.StockNamed}?(route.ResourceName,route.SpriteName):(null,null);
        if(key!=portraitIdentity||portraitVersion!=resources.Repository.PortraitResourceVersion)
        {
            if(portrait is not null){portrait.Hide();portrait.GetParent()?.RemoveChild(portrait);portrait.QueueFree();portrait=null;}
            portraitIdentity=key;portraitVersion=resources.Repository.PortraitResourceVersion;
            if(key.Item1 is string pack&&key.Item2 is string sprite)
            {
                var box=resources.Layout("00_02_00_mein_offset_50").Rectangle(5);
                portrait=new(){Name="CurrentMapAdviser",Position=box.Position,Size=box.Size};advice.AddChild(portrait);
                portrait.ConfigureFace3(resources.Repository,resources.Assets,resources.Shader,resources.Assets.Portraits.RequireSource(pack,sprite));
            }
        }
        SetMeta("source_advice_message",entry?.MessageId??-1);SetMeta("source_advice_person",publication?.PersonId??-1);
        SetMeta("source_advice_issue",publication is{} q?string.Join(";",q.UnresolvedOwners):"");
        RefreshMode();
    }
    private void RefreshMode()
    {
        adviceSwitch.Selected=!logMode;logSwitch.Selected=logMode;
        advice.Visible=!logMode&&publication?.Entries.Count is >0;log.Visible=logMode;
        next.Visible=run.Visible=!logMode;expand.Visible=filterSwitch.Visible=logMode;
        expand.Selected=expanded;filterSwitch.Selected=filter.Visible;
        if(!logMode)filter.Hide();
    }
    private bool Matches(MapBottomLogEntry row)
    {
        if(!filters.Any(x=>x))return true;
        int index=row.Category switch{1=>0,5=>1,3=>2,4=>3,2=>4,_=>5};return filters[index];
    }
    private void RefreshLog()
    {
        foreach(var row in logRows){log.RemoveChild(row);row.QueueFree();}logRows.Clear();
        if(scrollbar is not null){log.RemoveChild(scrollbar);scrollbar.QueueFree();scrollbar=null;}
        if(logPublication is null)return;
        var rows=logPublication.Entries.Where(Matches).Reverse().ToArray();int visible=expanded?18:1;scroll=Math.Clamp(scroll,0,Math.Max(0,rows.Length-visible));
        var source=resources.Layout("00_02_00_mein_offset_41");
        for(int i=0;i<Math.Min(visible,rows.Length-scroll);i++)
        {
            var entry=rows[scroll+i];var row=new Control{Name="MapLogRecord"+entry.Slot,Position=new(0,991-i*38),Size=source.Rectangle(0).Size,MouseFilter=MouseFilterEnum.Stop};log.AddChild(row);logRows.Add(row);
            source.Art(row,5,source.Rectangle(5),resources.Assets,resources.Shader);
            source.Text(row,6,default,entry.PersonName,resources.Font);
            var textBox=source.Rectangle(7);var text=new UiSourceParagraph{Position=textBox.Position,Size=textBox.Size};row.AddChild(text);
            text.ConfigureFormattedMessage(resources.Font,MapFontProfile.FromNode(source.Nodes[7],source.Source.Source.ResourceUri));text.Apply(entry.Body);
            if(entry.HexId is >=0 and <48400)
            {
                source.Art(row,8,source.Rectangle(8),resources.Assets,resources.Shader);
                row.GuiInput+=input=>{if(input is InputEventMouseButton{ButtonIndex:MouseButton.Left,Pressed:false}&&admitted()){focus(entry.HexId);row.AcceptEvent();}};
            }
            if(logPortraits.GetValueOrDefault(entry.PersonId) is{Kind:OfficerPortraitRouteKind.StockNamed,ResourceName:{} pack,SpriteName:{} sprite})
            {var b=source.Rectangle(10);var face=new UiSourcePortrait{Position=b.Position,Size=b.Size};row.AddChild(face);face.ConfigureFace2(resources.Repository,resources.Assets,resources.Shader,resources.Assets.Portraits.RequireSource(pack,sprite));}
            var tint=MapSpritePalettes.ForceFill(entry.ForceColour);
            source.Art(row,12,source.Rectangle(12),resources.Assets,resources.Shader,new(tint,tint,tint,tint));
            source.Text(row,13,default,entry.ForceName,resources.Font);
            row.GuiInput+=input=>{if(input is InputEventMouseButton{Pressed:true} mouse&&expanded){int direction=mouse.ButtonIndex switch{MouseButton.WheelUp=>1,MouseButton.WheelDown=>-1,_=>0};if(direction!=0){scroll=Math.Clamp(scroll+direction,0,Math.Max(0,rows.Length-visible));RefreshLog();row.AcceptEvent();}}};
        }
        if(expanded&&rows.Length>visible)
        {
            scrollbar=new(){Name="MapLogScrollbar",Position=new(30,307),Size=new(16,684)};log.AddChild(scrollbar);
            scrollbar.Configure(resources.Repository,resources.Assets,resources.Shader,(rows.Length-visible)*38,0,pixel=>{int value=pixel/38;if(scroll!=value){scroll=value;RefreshLog();}},stepPixels:38);
            scrollbar.SetValue(scroll*38);
        }
    }
}
