using Godot;
using San14.Data;

internal partial class CampaignEventCommandView
{
    private void RebuildRows(bool keepSelection=false)
    {
        int? old=selected?.EventId;int oldScroll=scroll?.Value??0;
        rows=ReadFilteredRows();selected=null;firstRow=-1;
        foreach(var cell in cells)cell.Hide();
        for(int i=0;i<categories.Count;i++)categories[i].Selected=i==category;
        foreach(var (column,_,down,up) in columns)
        {down.Visible=column==sortColumn&&!sortAscending;up.Visible=column==sortColumn&&sortAscending;}
        if(scroll is not null){scroll.Hide();scroll.QueueFree();}
        var box=source.Rectangle(32);scroll=new(){Name="CampaignEventScrollbar",Position=box.Position,Size=box.Size};canvas.AddChild(scroll);
        scroll.Configure(repository,assets,shader,Math.Max(0,rows.Count*RowPitch-(int)rowsHost.Size.Y),scrollSpeed(),
            _=>PositionRows(),currentScrollSpeed:scrollSpeed);
        count.Text=rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        PositionRows();summary.Apply("");PublishContent();PublishPeople();
        if(keepSelection)
        {
            scroll.SetValue(oldScroll);
            if(old.HasValue&&rows.Any(row=>row.EventId==old.Value))Select(old.Value);
        }
        PublishInputState();PublishHint();
        SetMeta("catalog_category",category);SetMeta("catalog_count",rows.Count);
        SetMeta("selected_event",selected?.EventId??-1);
        SetMeta("sort_column",sortColumn);SetMeta("sort_ascending",sortAscending);
    }
    private void PositionRows()
    {
        if(scroll is null)return;
        int first=scroll.Value/RowPitch;
        if(first!=firstRow)
        {
            firstRow=first;
            int visible=Math.Max(0,Math.Min(rows.Count-first,6));
            //The page owns one six-row pool. Scrolling binds current data,
            //without reloading Common/layout resources or recreating artwork.
            while(cells.Count<visible)
            {
                var cell=new CampaignEventCommandButton{InputAdmission=CanUse};
                rowsHost.AddChild(cell);cells.Add(cell);
                cell.Configure(eventRowSource,eventBarSource,assets,shader,font);
                cell.GuiInput+=input=>{if(CanUse())scroll?.HandleWheel(input);};
            }
            for(int i=0;i<cells.Count;i++)
            {
                var cell=cells[i];
                if(i>=visible){cell.Hide();continue;}
                var row=rows[first+i];
                cell.Bind(row,()=>Run(()=>{if(CanUse())Select(row.EventId);}));
                cell.Selected=selected?.EventId==row.EventId;cell.Disabled=busy;cell.Show();
            }
        }
        for(int i=0;i<cells.Count;i++)cells[i].Position=new(0,(first+i)*RowPitch-scroll.Value);
    }
    private void Select(int eventId)
    {
        selected=rows.First(row=>row.EventId==eventId);
        foreach(var cell in cells)cell.Selected=cell.EventId==eventId;
        summary.Apply(selected.Description);
        if(selected.MissingOwner is{} missing)summary.SetMeta("unresolved_owner",missing);else summary.RemoveMeta("unresolved_owner");
        notice="";PublishContent();PublishPeople();PublishInputState();PublishHint();SetMeta("selected_event",eventId);
    }
}

internal partial class CampaignEventCommandButton:UiSourceButton
{
    internal int EventId{get;private set;}
    private Action? selected;
    private UiSourceText number=null!,name=null!;
    private UiSourceLayout source=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private Control thumbnailLock=null!;
    private UiSourceText progressCaption=null!,progressCount=null!,cooldown=null!;
    private UiSourceBarGauge progressBar=null!;
    private Control? thumbnail;
    private CampaignEventCommandImage? shownThumbnail;
    private readonly Dictionary<int,(CampaignEventCommandImage Image,Control Pane)> markers=[];
    internal void Configure(UiSourceLayout source,MapMarkerLayout bar,UiAssets assets,Shader shader,MapFont font)
    {
        this.source=source;this.assets=assets;this.shader=shader;
        Size=source.Rectangle(0).Size;ActionMode=ActionModeEnum.Release;
        int[][] artwork=[[3],[7,8],[10],[12],[14,15]];var states=new Control[5];
        for(int state=0;state<5;state++)
        {
            var group=new Control{Name="EventRowState"+state,Size=Size,MouseFilter=MouseFilterEnum.Ignore};AddChild(group);states[state]=group;
            foreach(int node in artwork[state])source.Art(group,node,source.Rectangle(node),assets,shader,
                blend:node is 8 or 15?UiSourceSprite.Blend.Additive:UiSourceSprite.Blend.Ordinary);
        }
        number=source.Text(this,25,default,"",font);
        name=source.Text(this,26,default,"",font);ConfigureStates(states);
        number.ZIndex=25;name.ZIndex=26;
        thumbnailLock=source.Art(this,18,source.Rectangle(18),assets,shader);
        thumbnailLock.ZIndex=18;thumbnailLock.Hide();
        progressCaption=source.Text(this,28,default,"",font);progressCaption.Name="EventProgressCaption";
        progressCount=source.Text(this,31,default,"",font);progressCount.Name="EventProgressCount";
        cooldown=source.Text(this,32,default,"",font);cooldown.Name="EventCooldown";
        var box=source.Rectangle(29);
        progressBar=new(){Name="EventProgressBar",Position=box.Position};AddChild(progressBar);
        progressBar.Configure(bar,assets,shader,2);progressBar.Scale=box.Size/progressBar.Size;
        progressCaption.ZIndex=28;progressBar.ZIndex=29;progressCount.ZIndex=31;cooldown.ZIndex=32;
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)selected?.Invoke();};
    }
    internal void Bind(CampaignEventCommandRow row,Action selected)
    {
        this.selected=selected;EventId=row.EventId;Name="CampaignEventRow"+row.EventId;
        number.Text=row.Presentation?.DisplayNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"";
        name.Text=row.Name;
        BindPresentation(row.Presentation);
        SetMeta("event_id",row.EventId);SetMeta("eligibility_known",row.Status.HasValue);SetMeta("can_execute",row.CanExecute);
        if(row.MissingOwner is{} missing)SetMeta("unresolved_owner",missing);else RemoveMeta("unresolved_owner");
    }
    private void BindPresentation(CampaignEventCommandRowPresentation? presentation)
    {
        BindProgress(presentation?.Progress);
        thumbnailLock.Visible=presentation?.ShowThumbnailLock==true;
        if(shownThumbnail!=presentation?.Thumbnail)
        {
            if(thumbnail is not null){RemoveChild(thumbnail);thumbnail.QueueFree();thumbnail=null;}
            shownThumbnail=presentation?.Thumbnail;
            if(shownThumbnail is{} image)
            {
                thumbnail=source.Art(this,17,source.Rectangle(17),assets,shader,
                    commonSpriteOverride:image.SpriteName,commonTexturePack:image.TexturePack);
                thumbnail.ZIndex=17;thumbnail.Name="EventThumbnail";
            }
        }
        foreach(var (_,pane) in markers.Values)pane.Hide();
        if(presentation is null)
        {SetMeta("presentation_unresolved_owner","campaign_event.row_presentation");return;}
        if(presentation.UnresolvedOwners.Count==0)RemoveMeta("presentation_unresolved_owner");
        else SetMeta("presentation_unresolved_owner",string.Join(";",presentation.UnresolvedOwners));
        foreach(var marker in presentation.Markers)
        {
            if(!markers.TryGetValue(marker.NodeIndex,out var cached)||cached.Image!=marker.Image)
            {
                if(cached.Pane is{} old){RemoveChild(old);old.QueueFree();}
                var pane=source.Art(this,marker.NodeIndex,source.Rectangle(marker.NodeIndex),assets,shader,
                    commonSpriteOverride:marker.Image.SpriteName,commonTexturePack:marker.Image.TexturePack);
                pane.ZIndex=marker.NodeIndex;pane.Name="EventMarker"+marker.NodeIndex;
                cached=(marker.Image,pane);markers[marker.NodeIndex]=cached;
            }
            cached.Pane.Show();
        }
    }
    private void BindProgress(CampaignEventCommandRowProgress? progress)
    {
        bool show=progress?.ShowProgress==true;
        progressCaption.Visible=progressCount.Visible=progressBar.Visible=show;
        cooldown.Visible=progress?.CooldownText is not null;
        progressCaption.Text=progress?.Caption??"";progressCount.Text=progress?.CountText??"";
        cooldown.Text=progress?.CooldownText??"";
        if(show&&progress?.BarNumerator is int current&&progress.BarDenominator is int maximum)
            progressBar.Apply(current,maximum);
        SetMeta("progress_unresolved_owner",progress?.UnresolvedOwner??"");
    }
    internal void Abandon(){selected=null;InputAdmission=null;Disabled=true;}
}
