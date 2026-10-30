using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private RegisteredOfficerCityMapQuery? registeredOfficerCityQuery;
    private Func<bool>? registeredOfficerCityAdmitted;
    private Action<int>? registeredOfficerCityPicked;
    private HashSet<int>? registeredOfficerEligibleCities;
    private Dictionary<int,int>? registeredOfficerCityMarkerHexes;

    internal void BeginRegisteredOfficerCityPick(GameState expectedWorld,RegisteredOfficerCityMapQuery query,
        IReadOnlyList<int> eligible,Func<bool> admitted,Action<int> picked)
    {
        if(!ReferenceEquals(_logicalWorld,expectedWorld)||!IsPlayableMapReady)
            throw new InvalidOperationException("Registered-officer city selection requires its loaded map owner.");
        EndRegisteredOfficerCityPick();
        registeredOfficerCityQuery=query;registeredOfficerCityAdmitted=admitted;
        registeredOfficerCityPicked=picked;registeredOfficerEligibleCities=eligible.ToHashSet();
        registeredOfficerCityMarkerHexes=eligible.Where(id=>expectedWorld.Cities.GetValueOrDefault(id)?.HexId is not null)
            .ToDictionary(id=>id,id=>expectedWorld.Cities[id].HexId!.Value);
        _d3d11Markers?.SetCampaignMarkers(registeredOfficerCityMarkerHexes.Values.ToHashSet());
        SetCommandTargetPicker(PickRegisteredOfficerCity,RegisteredOfficerCityPointerAllowed,HoverRegisteredOfficerCity);
        SetMapSummaryMode(MapSummarySelectionMode.Foothold);
        ScheduleD3D11MapFrame();
    }
    private bool RegisteredOfficerCityPointerAllowed(int hex)=>
        registeredOfficerCityAdmitted?.Invoke()==true&&registeredOfficerCityQuery?.QueryHex(hex)
            is{Allowed:true,CityId:int city}&&registeredOfficerEligibleCities?.Contains(city)==true;
    private void PickRegisteredOfficerCity(int hex)
    {
        if(!RegisteredOfficerCityPointerAllowed(hex))return;
        if(registeredOfficerCityQuery!.QueryHex(hex).CityId is int city)registeredOfficerCityPicked?.Invoke(city);
    }
    private void HoverRegisteredOfficerCity(int? hex)
    {
        if(registeredOfficerCityAdmitted?.Invoke()!=true||registeredOfficerCityMarkerHexes is null)return;
        HashSet<int>? selected=null;
        if(hex is int point&&RegisteredOfficerCityPointerAllowed(point)&&
            registeredOfficerCityQuery!.QueryHex(point).CityId is int city&&
            registeredOfficerCityMarkerHexes.TryGetValue(city,out int center))selected=[center];
        _d3d11Markers?.SetCampaignMarkers(registeredOfficerCityMarkerHexes.Values.ToHashSet(),selected);
    }
    internal void EndRegisteredOfficerCityPick()
    {
        if(registeredOfficerCityQuery is null)return;
        SetCommandTargetPicker(null);SetMapSummaryMode(MapSummarySelectionMode.Normal);
        registeredOfficerCityQuery=null;registeredOfficerCityAdmitted=null;
        registeredOfficerCityPicked=null;registeredOfficerEligibleCities=null;registeredOfficerCityMarkerHexes=null;
        _d3d11Markers?.SetCampaignMarkers(null);ScheduleD3D11MapFrame();
    }
}
