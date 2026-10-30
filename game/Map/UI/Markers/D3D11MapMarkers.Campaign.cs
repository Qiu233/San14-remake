using Godot;

internal partial class D3D11MapMarkers
{
    private IReadOnlySet<int>? campaignMarkerHexes;
    private IReadOnlySet<int>? campaignSelectedHexes;

    //1B5200 controls the existing Foothold marker population. It does not
    //paint a candidate Hex bitmap or create a marker at an invented center.
    internal void SetCampaignMarkers(IReadOnlySet<int>? visibleHexes,IReadOnlySet<int>? selectedHexes=null)
    {
        campaignMarkerHexes=visibleHexes;campaignSelectedHexes=selectedHexes;
        PositionFoodWarnings();
        PositionCityAttention();
        PositionFacilityInfo();
        if(_armyLabels is not null)_armyLabels.Visible=visibleHexes is null&&_armyInformationVisible;
        ClearPointer();QueueMarkerRedraw();
    }

    private bool CampaignMarkerVisible(Marker marker)=>campaignMarkerHexes is null||
        marker.Kind!=Kind.Area&&campaignMarkerHexes.Contains(marker.Hex);
}
