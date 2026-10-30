using San14.Data;

internal enum CampaignTargetMapStage { TargetProvince,District,Foothold }

public partial class MapSceneView
{
    private CampaignDeploymentPage? campaignTargetPage;
    private IReadOnlyList<CampaignFootholdChoice>? campaignTargetMarkers;
    private Func<bool>? campaignTargetAdmitted;
    private Action<int>? campaignTargetPicked;
    private Action<int?>? campaignTargetHovered;
    private Func<int,int?>? campaignTargetIdentity;
    private HashSet<int>? campaignMarkerHexes;
    private HashSet<int>? campaignAllowedIdentities;
    private Dictionary<int,int>? campaignMarkerIdentities;
    private int? campaignHoverIdentity;
    private D3D11CommandMapData? campaignFocus;

    /// <summary>One of the original map-only destination selectors. The page
    /// owns current source inputs; camera requests and hover do not capture it.</summary>
    internal void BeginCampaignTargetMapPick(CampaignDeploymentPage page,CampaignTargetMapStage stage,
        int? provinceId,int? districtId,IReadOnlyList<int> selectedIds,Func<bool> admitted,
        Action<int> picked,Action<int?>? hovered=null)
    {
        if(!IsPlayableMapReady||!page.Available)
            throw new InvalidOperationException(page.UnresolvedOwner??"Campaign map selection requires an available map.");
        EndCampaignTargetMapPick();
        if(_commandMapDisplay is {Present:true})
            throw new InvalidOperationException("Another command map display is active.");
        IReadOnlyList<CampaignFootholdChoice> markers;
        Func<int,int?> identity;
        D3D11CommandMapData? focus=null;
        switch(stage)
        {
            case CampaignTargetMapStage.TargetProvince:
                markers=page.QueryTargetMapFootholds();identity=page.ResolveProvinceAtHex;
                focus=D3D11CommandMapData.CaptureCampaignFocus(D3D11CommandMapDisplayKind.CampaignProvinceFocus,
                    page.ResolveProvinceFocusAtHex);
                break;
            case CampaignTargetMapStage.District:
                markers=page.QueryDistrictMapFootholds(provinceId??throw new ArgumentNullException(nameof(provinceId)));
                identity=page.ResolveDistrictAtHex;
                focus=D3D11CommandMapData.CaptureCampaignFocus(D3D11CommandMapDisplayKind.CampaignDistrictFocus,
                    page.ResolveDistrictFocusAtHex);
                break;
            case CampaignTargetMapStage.Foothold:
                markers=page.QueryFootholds(provinceId??throw new ArgumentNullException(nameof(provinceId)),
                    districtId??throw new ArgumentNullException(nameof(districtId)));
                identity=page.ResolveFootholdAtHex;
                break;
            default:throw new ArgumentOutOfRangeException(nameof(stage));
        }
        //Build before replacing any renderer owner, so a failed source read
        //does not leave a partly installed selection/predicate publication.
        var hexes=markers.Where(row=>row.HexId is not null).Select(row=>row.HexId!.Value).ToHashSet();
        var allowed=new HashSet<int>();var markerIdentities=new Dictionary<int,int>();
        foreach(var row in markers)
            if(row.HexId is int hex&&identity(hex) is int id)
            {markerIdentities[hex]=id;if(row.Available==true)allowed.Add(id);}
        campaignTargetPage=page;campaignTargetMarkers=markers;
        campaignTargetIdentity=identity;campaignTargetAdmitted=admitted;
        campaignTargetPicked=picked;campaignTargetHovered=hovered;campaignMarkerHexes=hexes;
        campaignAllowedIdentities=allowed;campaignMarkerIdentities=markerIdentities;
        campaignHoverIdentity=null;campaignFocus=focus;
        if(focus is not null)
        {
            ulong revision=checked(++_commandMapRevision);
            _commandMapDisplay=D3D11CommandMapPublication.ForCampaignFocus(focus,revision,revision,
                checked(++_commandMapProviderGeneration));
        }
        SetMapSummaryMode(stage switch
        {
            CampaignTargetMapStage.TargetProvince => MapSummarySelectionMode.Province,
            CampaignTargetMapStage.Foothold => MapSummarySelectionMode.Foothold,
            _ => MapSummarySelectionMode.Normal
        });
        ApplyCampaignTargetMapPick(selectedIds);
        SetCommandTargetPicker(PickCampaignTarget,CampaignPointerAllowed,HoverCampaignTarget);
        ScheduleD3D11MapFrame();
    }

    internal void ApplyCampaignTargetMapPick(IReadOnlyList<int> selectedIds)
    {
        if(campaignMarkerIdentities is null)return;
        var selected=selectedIds.ToHashSet();
        var selectedHexes=new HashSet<int>();
        foreach(var row in campaignMarkerIdentities)
            if(selected.Contains(row.Value))selectedHexes.Add(row.Key);
        _d3d11Markers?.SetCampaignMarkers(campaignMarkerHexes,selectedHexes);
    }

    private bool CampaignPointerAllowed(int hex)
    {
        //The original selector hovers every admitted terrain Hex, including
        //an unavailable destination whose explanatory hint is still shown.
        return campaignTargetAdmitted?.Invoke()==true&&campaignTargetPage?.CanPickMapHex(hex)==true;
    }

    private void PickCampaignTarget(int hex)
    {
        if(!CampaignPointerAllowed(hex))return;
        if(campaignTargetIdentity!(hex) is int id&&campaignAllowedIdentities?.Contains(id)==true)
            campaignTargetPicked?.Invoke(id);
    }

    private void HoverCampaignTarget(int? hex)
    {
        if(campaignTargetAdmitted?.Invoke()!=true||campaignTargetIdentity is null)return;
        int? id=hex is int h?campaignTargetIdentity(h):null;
        if(id!=campaignHoverIdentity){campaignHoverIdentity=id;campaignTargetHovered?.Invoke(id);}
        if(hex is not int point||campaignFocus is null||_commandMapDisplay is not {Data:{} data} display||
            !ReferenceEquals(data,campaignFocus))return;
        ushort group=data.RawAreas[point];
        ushort old=display.HoverHex<D3D11CommandMapData.CellCount?data.RawAreas[display.HoverHex]:(ushort)0;
        if(group==old)return;
        _commandMapDisplay=D3D11CommandMapPublication.ForCampaignFocus(data,checked(++_commandMapRevision),
            display.DataRevision,display.ProviderGeneration,(uint)point);
        ScheduleD3D11MapFrame();
    }

    internal void EndCampaignTargetMapPick()
    {
        if(campaignTargetPage is null)return;
        SetCommandTargetPicker(null);
        SetMapSummaryMode(MapSummarySelectionMode.Normal);
        if(campaignFocus is not null&&ReferenceEquals(_commandMapDisplay?.Data,campaignFocus))
            _commandMapDisplay=D3D11CommandMapPublication.ForCampaignFocus(null,checked(++_commandMapRevision),0,
                checked(++_commandMapProviderGeneration));
        campaignTargetPage=null;campaignTargetMarkers=null;campaignTargetIdentity=null;
        campaignTargetAdmitted=null;campaignTargetPicked=null;campaignTargetHovered=null;
        campaignMarkerHexes=null;campaignFocus=null;campaignHoverIdentity=null;
        campaignAllowedIdentities=null;campaignMarkerIdentities=null;
        _d3d11Markers?.SetCampaignMarkers(null);ApplyKeyboardMapInformation();
        ScheduleD3D11MapFrame();
    }
}
