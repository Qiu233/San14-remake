using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private D3D11CommandMapData? diplomacyTerritory;
    private DiplomacyTargetMapQuery? diplomacyTargetQuery;
    private Control? diplomacyTargetMapOwner;
    private MapSummarySelectionMode diplomacyTargetPreviousSummary;
    private SceneryMapPolicy? diplomacyTargetPreviousPresentation;
    private bool diplomacyTargetGeographyVisible;
    internal void BeginDiplomacyTargetPick(Control owner,GameState expectedWorld,
        DiplomacyTargetMapQuery query,Func<bool> admitted,Func<int,bool> allowed,Action<int> picked)
    {
        if(!ReferenceEquals(_logicalWorld,expectedWorld)||!IsPlayableMapReady)
            throw new InvalidOperationException("Diplomacy target selection requires its live map owner.");
        if(diplomacyTargetMapOwner is {} previous)EndDiplomacyTargetPick(previous);
        if(_commandTargetPicked is not null)throw new InvalidOperationException("Another command owns the map picker.");
        if(_commandMapDisplay is {Present:true})throw new InvalidOperationException("Another command owns the territory overlay.");
        var overlay=D3D11CommandMapData.CaptureDiplomacyFocus(query);
        ulong overlayRevision=checked(++_commandMapRevision);
        _commandMapDisplay=D3D11CommandMapPublication.ForCampaignFocus(overlay,overlayRevision,overlayRevision,
            checked(++_commandMapProviderGeneration));
        diplomacyTerritory=overlay;
        diplomacyTargetMapOwner=owner;diplomacyTargetQuery=query;diplomacyTargetPreviousSummary=_summaryMode;
        diplomacyTargetGeographyVisible=geographicView?.Visible==true;geographicView?.Hide();
        diplomacyTargetPreviousPresentation=_mapPresentation?.BeginDiplomacyTargetMode(query.Kind==DiplomacyTargetSelectionKind.Force,query.Presentation);
        _d3d11Markers?.SetDiplomacyTargetSelection(query.Kind,id=>query.Contains(id)&&query.DisabledIds?.Contains(id)!=true&&allowed(id),query.Presentation);
        informationCanvas?.SetDiplomacyTargetSelection(query.Kind==DiplomacyTargetSelectionKind.Force?
            id=>query.Contains(id)&&query.DisabledIds?.Contains(id)!=true&&allowed(id):null,query.Kind==DiplomacyTargetSelectionKind.Force?picked:null);
        informationCanvas?.SetIntelligenceForceMarkers(query.IntelligenceSpyMarkers,query.Presentation==MapTargetPresentation.IntelligenceAwayForce&&!_mapPresentation!.IntelligenceForceRelationTags);
        ApplyKeyboardMapInformation();
        _d3d11Markers?.SetInformationVisibility(query.Kind==DiplomacyTargetSelectionKind.City,false,
            query.Presentation==MapTargetPresentation.IntelligenceAwayForce?11:query.Kind==DiplomacyTargetSelectionKind.Force?7:9);
        bool Current()=>ReferenceEquals(diplomacyTargetMapOwner,owner)&&admitted();
        SetCommandTargetPicker(hex=>
        {
            if(Current()&&query.QueryHex(hex) is {Allowed:true,Id:int id}&&allowed(id))picked(id);
        },hex=>Current()&&query.QueryHex(hex) is {Allowed:true,Id:int id}&&allowed(id),hex=>
        {
            if(!Current()||query.Presentation!=MapTargetPresentation.IntelligenceInspectCity)return;
            MapSummaryHoverTarget? target=hex is int point&&query.QueryHex(point) is {Allowed:true,Id:int city}
                ?new(MapSummaryHoverKind.Foothold,city):null;
            SetMapSummaryHover(owner,target);
        });
        // Ordinary recipient modes retain normal summaries; special Force
        // summary mode23 is not this caller. Type2 owns the foothold summary.
        SetMapSummaryMode(query.Kind==DiplomacyTargetSelectionKind.Force?MapSummarySelectionMode.Normal:MapSummarySelectionMode.Foothold);
        ScheduleD3D11MapFrame();
    }
    internal bool GetIntelligenceForceRelationTags(Control owner)
    {
        if(diplomacyTargetMapOwner is not null&&!ReferenceEquals(diplomacyTargetMapOwner,owner))
            throw new InvalidOperationException("Another selection owns the map tag mode.");
        return _mapPresentation?.IntelligenceForceRelationTags??throw new InvalidOperationException("Map presentation is not bound.");
    }
    internal void SetIntelligenceForceRelationTags(Control owner,bool relationshipMode)
    {
        if(!ReferenceEquals(diplomacyTargetMapOwner,owner)||diplomacyTargetQuery is not{Presentation:MapTargetPresentation.IntelligenceAwayForce} query)return;
        _mapPresentation!.SetIntelligenceForceRelationTags(relationshipMode);
        informationCanvas?.SetIntelligenceForceMarkers(query.IntelligenceSpyMarkers,!relationshipMode);
    }
    // Invalid pointers preserve the last source focus. Candidate membership
    // never participates in the separate territory provider.
    private void HoverDiplomacyTerritory(int? point)
    {
        if(diplomacyTargetMapOwner is null||point is not int hex||diplomacyTerritory is not {} data||
            _commandMapDisplay is not {Data:{} visible} display||!ReferenceEquals(data,visible))return;
        ushort previous=display.HoverHex<D3D11CommandMapData.CellCount?data.DiplomacyFocusAt((int)display.HoverHex):(ushort)0;
        if(data.DiplomacyFocusAt(hex)==previous)return;
        _commandMapDisplay=D3D11CommandMapPublication.ForCampaignFocus(data,checked(++_commandMapRevision),
            display.DataRevision,display.ProviderGeneration,(uint)hex);
        ScheduleD3D11MapFrame();
    }
    internal void EndDiplomacyTargetPick(Control owner)
    {
        if(!ReferenceEquals(diplomacyTargetMapOwner,owner))return;
        if(diplomacyTerritory is {} data&&ReferenceEquals(_commandMapDisplay?.Data,data))
            _commandMapDisplay=D3D11CommandMapPublication.ForCampaignFocus(null,checked(++_commandMapRevision),0,
                checked(++_commandMapProviderGeneration));
        diplomacyTerritory=null;diplomacyTargetQuery=null;SetMapSummaryHover(owner,null);
        diplomacyTargetMapOwner=null;SetCommandTargetPicker(null);
        _d3d11Markers?.SetDiplomacyTargetSelection(null,null);
        informationCanvas?.SetDiplomacyTargetSelection(null,null);
        informationCanvas?.SetIntelligenceForceMarkers(null,false);
        if(diplomacyTargetPreviousPresentation is {} previous)_mapPresentation?.RestoreDiplomacyTargetMode(previous);
        diplomacyTargetPreviousPresentation=null;
        if(geographicView is not null)geographicView.Visible=diplomacyTargetGeographyVisible;
        SetMapSummaryMode(diplomacyTargetPreviousSummary);ApplyKeyboardMapInformation();ScheduleD3D11MapFrame();
    }
}
