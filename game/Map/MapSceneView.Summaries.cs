using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private Action<GameState,SourceWorldContext>? _bindSummaryCurrent;
    private Func<GameState,SourceWorldContext,HexReference?,int,MapSummaryViewOptions?,MapSummarySelection>? _querySummaryReceivers;
    private Func<GameState,SourceWorldContext,MapSummaryReceiver,MapSummaryPage>? _querySummaryPage;
    private Func<bool>? _summaryCanQuery;
    private Control? _summaryRoot;
    private MapSummaryTicker? _summaryTicker;
    private MapSummaryResources? _summaryResources;
    private readonly Dictionary<int,SummarySlotView> _summaryViews=[];
    private SourceWorldContext? _summaryInputCurrent, _summaryBoundCurrent, _summaryCachedCurrent;
    private GameState? _summaryInputWorld, _summaryBoundWorld, _summaryCachedWorld;
    private SceneryMapHexSelection _summaryInputSelection=new(false,null);
    private D3D11FacilityPublication? _summaryModels;
    private readonly HashSet<int> _summaryFacilityModels=[];
    private readonly HashSet<int> _summaryFacilityAbsent=[];
    private bool _summaryDirty=true, _summaryCachedKnown, _summaryGeographySelected, _summarySuspended;
    private int? _summaryCachedHex, _summaryCachedBand, _summaryPreviousBand, _summaryCachedProvince;
    private bool _summaryCachedGeography;
    private MapSummaryTargetIdentity? _summaryTargetIdentity;
    private uint _summaryTargetClock;
    private int? _summaryVisibleMask;
    internal bool? MapSummaryVisible {get;private set;}
    internal event Action<bool?>? MapSummaryVisibilityChanged;

    private sealed class SummarySlotView
    {
        internal MapSummaryReceiver? Receiver;
        internal MapSummaryPage? Page;
        internal MapSummaryPanel? Panel;
    }
    private partial class MapSummaryTicker : Node
    {
        internal Action? Tick;
        public override void _Process(double delta)=>Tick?.Invoke();
    }

    internal void SetMapSummaryQueries(Action<GameState,SourceWorldContext> bindCurrent,
        Func<GameState,SourceWorldContext,HexReference?,int,MapSummaryViewOptions?,MapSummarySelection> receivers,
        Func<GameState,SourceWorldContext,MapSummaryReceiver,MapSummaryPage> page, Func<bool> canQuery,
        Func<GameState,SourceWorldContext,MapSummaryIndicatorFrame>? indicators=null)
    {
        _bindSummaryCurrent=bindCurrent;_querySummaryReceivers=receivers;_querySummaryPage=page;
        _summaryCanQuery=canQuery;_querySummaryIndicators=indicators;InvalidateMapSummaries();
    }

    internal void SetSummaryGeographySelected(bool selected)
    {
        if(_summaryGeographySelected==selected)return;
        _summaryGeographySelected=selected;InvalidateMapSummaries();
        if(!selected)
        {
            //These panels are mounted in the geographical view. Detach them
            //before that owner may retire its entire control subtree.
            ReleaseSummarySlot(12);ReleaseSummarySlot(13);PublishMapSummaryVisibility();
        }
    }
    internal void InvalidateMapSummaries()=>_summaryDirty=true;

    private void InitializeMapSummaries()
    {
        if(_summaryRoot is not null||_querySummaryReceivers is null||_querySummaryPage is null)return;
        var parent=StrategicMapOverlayParent();
        var root=new Control{Name="MapSummaryManager",MouseFilter=Control.MouseFilterEnum.Ignore,ZIndex=5};
        parent.AddChild(root);root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);_summaryRoot=root;
        var ticker=new MapSummaryTicker{Name="MapSummaryClock",Tick=TickMapSummaries};root.AddChild(ticker);_summaryTicker=ticker;
        _summaryTargetClock=SummaryUiMilliseconds;PublishMapSummaryVisibility();
    }

    //Retains the existing accepted-Scene-hover caller; this now serves all
    //sixteen source summary leaves rather than recapturing a Scenery detail.
    private void UpdateSceneryMapSummary(SourceWorldContext current,SceneryMapHexSelection selection)
    {
        _summaryInputCurrent=current;_summaryInputWorld=_logicalWorld;_summaryInputSelection=selection;
        TickMapSummaries();
    }

    private void TickMapSummaries()
    {
        if(_summaryRoot is null)return;
        //A dialog stops camera input, while the normal UI manager retains
        //its independent logical indicator callbacks, including hidden pages.
        if(_detailsOpen&&IsVisibleInTree()&&_summaryCanQuery?.Invoke()==true&&
            _d3d11AnimationPacer is{} pacer&&pacer.TryAccept())
            AdvanceMapSummaryIndicators(pacer.FramesPerSecond(pacer.FpsMode));
        if((_detailsOpen&&_summaryHoverTarget is null)||_periodMapActive||!IsVisibleInTree()||_summaryCanQuery?.Invoke()!=true)
        {SuspendMapSummaries();return;}
        if(_logicalWorld is not{} world||_summaryInputCurrent is not{} current||
            !ReferenceEquals(_summaryInputWorld,world))
        {SuspendMapSummaries();return;}
        if(MainCommandZoomLevel is not float zoom||!float.IsFinite(zoom))return;
        int band=(int)MathF.Floor(zoom);
        if(band is <0 or >6){SetMeta("source_summary_receiver_issue","map.summary.zoom_band");return;}
        if(_summaryGeographySelected&&_summaryPreviousBand==6&&band!=6)
        {
            //Actual camera-floor departure retires geography without queuing
            //the toolbar toggle's saved-camera return over this new zoom.
            ClearGeographicAdvantages();ApplyKeyboardMapInformation();ScheduleD3D11MapFrame();
        }
        _summaryPreviousBand=band;
        if(_summarySuspended){_summarySuspended=false;_summaryTargetClock=SummaryUiMilliseconds;}
        bool known=_summaryHoverTarget is not null||_summaryInputSelection.Known;
        bool geographic=band==6&&_summaryGeographySelected;
        int? province=SelectedSummaryProvinceId;
        //The global indicator manager advances before any hover/visibility gate.
        if(!ReferenceEquals(_summaryBoundWorld,world)||!ReferenceEquals(_summaryBoundCurrent,current))
        {
            try
            {
                _bindSummaryCurrent!(world,current);_summaryBoundWorld=world;_summaryBoundCurrent=current;
                if(_querySummaryIndicators is{} indicators)_summaryIndicators.Bind(indicators(world,current));
            }
            catch(Exception error)when(error is InvalidDataException or NotSupportedException)
            {
                SetMeta("source_summary_receiver_issue",error.Message);SuspendMapSummaries();return;
            }
        }
        //An unavailable ray/filter is not a known empty ordinary hover. The
        //geographical pair has its independent current selection and display gate.
        if(!known&&!geographic){UpdateMapSummaryVisibility(band,false);return;}
        int? hex=known?_summaryInputSelection.Hex?.PhysicalSlot:null;
        bool modelChanged=!ReferenceEquals(_summaryModels,_acceptedFacilityModels);
        bool refresh=_summaryDirty||!ReferenceEquals(_summaryCachedWorld,world)||!ReferenceEquals(_summaryCachedCurrent,current)||
            _summaryCachedKnown!=known||_summaryCachedHex!=hex||_summaryCachedBand!=band||
            _summaryCachedGeography!=_summaryGeographySelected||_summaryCachedProvince!=province||modelChanged;
        if(refresh)
        {
            try
            {
                if(modelChanged)
                {
                    _summaryModels=_acceptedFacilityModels;_summaryFacilityModels.Clear();_summaryFacilityAbsent.Clear();
                    if(_summaryModels is{} publication)
                    {
                        foreach(var row in publication.Rows)
                            if(row.Identity=="facility:"+row.ReceiverId&&row.Instance.SourceWorldState is not null)
                                _summaryFacilityModels.Add(row.ReceiverId);
                        var unresolved=publication.Missing.Select(item=>item.InstanceKey).ToHashSet();
                        foreach(int id in publication.Source.KnownReceivers)
                            if(!_summaryFacilityModels.Contains(id)&&!unresolved.Contains("facility:"+id))
                                _summaryFacilityAbsent.Add(id);
                    }
                }
                var options=new MapSummaryViewOptions
                {
                    Mode=(int)_summaryMode,HoverTarget=_summaryHoverTarget,
                    GeographySelected=_summaryGeographySelected,GeographicSelection=province,
                    ObjectPresentationAvailable=id=>_summaryFacilityModels.Contains(id)?true:
                        _summaryFacilityAbsent.Contains(id)?false:null
                };
                var selection=_querySummaryReceivers!(world,current,known?_summaryInputSelection.Hex:null,band,options);
                try {UpdateSummaryFocus(world,current,selection,band);RemoveMeta("source_summary_focus_issue");}
                catch(InvalidDataException error)
                {summaryFocusActive=false;SetMeta("source_summary_focus_issue",error.Message);}
                _summaryHoverValid=selection.HoverTargetValid;
                if(known)AcceptSummaryTargetIdentity(selection.TargetIdentity);
                var resolved=selection.Receivers.ToDictionary(receiver=>receiver.Slot);
                for(int slot=0;slot<16;slot++)
                {
                    if(!known&&slot is not (12 or 13))continue;
                    if(selection.UnresolvedSlots.TryGetValue(slot,out var issue))
                    {SetMeta("source_summary_slot_issue_"+slot,issue);continue;}
                    RemoveMeta("source_summary_slot_issue_"+slot);
                    if(!resolved.TryGetValue(slot,out var receiver))
                    {
                        if(_summaryViews.TryGetValue(slot,out var empty)){empty.Receiver=null;empty.Page=null;HideSummaryPanel(empty);}
                        continue;
                    }
                    if(!_summaryViews.TryGetValue(slot,out var view))_summaryViews.Add(slot,view=new());
                    var page=_querySummaryPage!(world,current,receiver);
                    if(page.ReceiverValid is null)
                    {SetMeta("source_summary_slot_issue_"+slot,page.UnresolvedOwner??"map.summary.receiver_unknown");continue;}
                    view.Receiver=receiver;view.Page=page;
                    if(page.ReceiverValid==false){HideSummaryPanel(view);continue;}
                    ApplySummaryPage(view,page);
                }
                _summaryCachedWorld=world;_summaryCachedCurrent=current;_summaryCachedKnown=known;_summaryCachedHex=hex;
                _summaryCachedBand=band;_summaryCachedGeography=_summaryGeographySelected;_summaryCachedProvince=province;
                _summaryDirty=false;RemoveMeta("source_summary_receiver_issue");
            }
            catch(Exception error)when(error is InvalidDataException or NotSupportedException)
            {
                SetMeta("source_summary_receiver_issue",error.Message);
                _summaryCachedWorld=world;_summaryCachedCurrent=current;_summaryCachedKnown=known;_summaryCachedHex=hex;
                _summaryCachedBand=band;_summaryCachedGeography=_summaryGeographySelected;_summaryCachedProvince=province;
                _summaryDirty=false;
            }
        }
        if(_detailsOpen&&_summaryHoverValid!=true){SuspendMapSummaries();return;}
        UpdateMapSummaryVisibility(band,geographic);
    }

    private void AcceptSummaryTargetIdentity(MapSummaryTargetIdentity? target)
    {
        if(target is null)return;
        if(_summaryTargetIdentity is not{} previous)
        {_summaryTargetIdentity=target;_summaryTargetClock=SummaryUiMilliseconds;return;}
        static bool Changed(int? a,int? b)=>a.HasValue&&b.HasValue&&a!=b;
        bool changed=Changed(previous.ArmySlot,target.ArmySlot)||Changed(previous.GroupArmySlot,target.GroupArmySlot)||
            Changed(previous.AreaSlot,target.AreaSlot)||Changed(previous.ObjectSlot,target.ObjectSlot)||
            Changed(previous.BaseObjectId,target.BaseObjectId)||Changed(previous.ForceSlot,target.ForceSlot)||
            Changed(previous.ScenerySlot,target.ScenerySlot)||Changed(previous.StateSlot,target.StateSlot);
        _summaryTargetIdentity=new(target.ArmySlot??previous.ArmySlot,target.GroupArmySlot??previous.GroupArmySlot,
            target.AreaSlot??previous.AreaSlot,target.ObjectSlot??previous.ObjectSlot,target.BaseObjectId??previous.BaseObjectId,
            target.ForceSlot??previous.ForceSlot,target.ScenerySlot??previous.ScenerySlot,target.StateSlot??previous.StateSlot);
        if(changed)_summaryTargetClock=SummaryUiMilliseconds;
    }

    private void ApplySummaryPage(SummarySlotView view,MapSummaryPage page)
    {
        Control? mount=page.Slot is 12 or 13?GeographicSummaryMount(page.Slot):_summaryRoot;
        if(mount is null||!GodotObject.IsInstanceValid(mount)){HideSummaryPanel(view);return;}
        if(view.Panel is not null&&!GodotObject.IsInstanceValid(view.Panel))view.Panel=null;
        if(view.Panel is{} previous&&(previous.LayoutName!=page.LayoutName||previous.GetParent()!=mount))
        {DetachSummaryPanel(previous);view.Panel=null;}
        if(view.Panel is null)
        {
            _summaryResources??=new(_repository,_text.SourceLocale);
            _summaryResources.IndicatorPlayback=_summaryIndicators;
            var panel=new MapSummaryPanel{Name="MapSummarySlot"+page.Slot,Visible=false};mount.AddChild(panel);
            try
            {
                panel.Configure(_summaryResources,page.LayoutName);
                if(page.Slot is not (12 or 13))panel.Position=new(SceneryMapSummarySources.MountX,SceneryMapSummarySources.MountY);
                view.Panel=panel;
            }
            catch{DetachSummaryPanel(panel);throw;}
        }
        try {view.Panel.Apply(page);}
        catch {HideSummaryPanel(view);view.Page=null;throw;}
    }

    private void UpdateMapSummaryVisibility(int band,bool geographic)
    {
        bool elapsed=unchecked(SummaryUiMilliseconds-_summaryTargetClock)>
            SceneryMapSummarySources.ShowDelayMilliseconds(band);
        foreach(var(slot,view) in _summaryViews)
        {
            if(view.Panel is not{} panel||!GodotObject.IsInstanceValid(panel))continue;
            bool available=view.Receiver is not null&&view.Page?.ReceiverValid==true;
            bool admitted=(MapSummaryAdmissionRules.VisibleMask((int)_summaryMode,band,_summaryGeographySelected)&(1<<slot))!=0;
            bool show=available&&admitted&&((geographic&&slot is (12 or 13))||elapsed);
            if(show&&!panel.Visible)panel.RefreshIndicators();
            panel.Visible=show;
        }
        PublishMapSummaryVisibility();
    }

    private void PublishMapSummaryVisibility()
    {
        int mask=0;
        foreach(var(slot,view) in _summaryViews)
            if(view.Panel is{} panel&&GodotObject.IsInstanceValid(panel)&&panel.IsVisibleInTree())mask|=1<<slot;
        bool? visible=_summaryRoot is null?null:mask!=0;
        if(_summaryVisibleMask!=mask){_summaryVisibleMask=mask;SetMeta("source_map_summary_visible_mask",mask);}
        if(MapSummaryVisible==visible)return;
        MapSummaryVisible=visible;MapSummaryVisibilityChanged?.Invoke(visible);
    }
    private static void HideSummaryPanel(SummarySlotView view)
    {if(view.Panel is{} panel&&GodotObject.IsInstanceValid(panel))panel.Hide();}
    private static void DetachSummaryPanel(MapSummaryPanel panel)
    {if(!GodotObject.IsInstanceValid(panel))return;panel.GetParent()?.RemoveChild(panel);panel.Free();}
    private void ReleaseSummarySlot(int slot)
    {if(_summaryViews.Remove(slot,out var view)&&view.Panel is{} panel)DetachSummaryPanel(panel);}
    private void SuspendMapSummaries()
    {
        foreach(var view in _summaryViews.Values)HideSummaryPanel(view);
        _summarySuspended=true;PublishMapSummaryVisibility();
    }
    private void ReleaseMapSummaries()
    {
        summaryFocusActive=false;summaryFocusWorld=null;summaryCityFocus=summaryAreaFocus=summaryFocusData=null;
        if(_summaryTicker is{} ticker&&GodotObject.IsInstanceValid(ticker))ticker.Tick=null;
        foreach(int slot in _summaryViews.Keys.ToArray())ReleaseSummarySlot(slot);
        if(_summaryRoot is{} root&&GodotObject.IsInstanceValid(root)){root.GetParent()?.RemoveChild(root);root.Free();}
        _summaryRoot=null;_summaryTicker=null;
        //Every panel, including externally mounted geographical panels, has
        //left the tree and been freed before shared fonts/textures retire.
        _summaryResources?.Dispose();_summaryResources=null;
        _summaryInputCurrent=_summaryBoundCurrent=_summaryCachedCurrent=null;
        _summaryInputWorld=_summaryBoundWorld=_summaryCachedWorld=null;
        _summaryInputSelection=new(false,null);_summaryModels=null;_summaryFacilityModels.Clear();_summaryFacilityAbsent.Clear();
        _summaryTargetIdentity=null;_summaryVisibleMask=null;_summaryPreviousBand=null;
        _summaryCachedBand=_summaryCachedHex=_summaryCachedProvince=null;_summaryDirty=true;_summarySuspended=false;
        _summaryMode=MapSummarySelectionMode.Normal;_summaryHoverOwner=null;_summaryHoverTarget=null;_summaryHoverValid=null;_summaryIndicators.Reset();_summaryGeographySelected=false;
        PublishMapSummaryVisibility();
    }
}
