using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private object? cityOfficerMovementMapOwner;
    private IReadOnlySet<int>? cityOfficerMovementMarkerHexes;

    internal void BeginCityOfficerMovementMapPick(object owner, GameState expectedWorld,
        CityOfficerMovementMapQuery query, Func<bool> admitted, Action<int> picked)
    {
        if (!ReferenceEquals(_logicalWorld, expectedWorld) || !IsPlayableMapReady)
            throw new InvalidOperationException("Officer movement selection requires its live map owner.");
        if (cityOfficerMovementMapOwner is { } previous) EndCityOfficerMovementMapPick(previous);
        if (_commandTargetPicked is not null) throw new InvalidOperationException("Another command owns the map picker.");
        cityOfficerMovementMapOwner = owner;
        cityOfficerMovementMarkerHexes = query.Points.Select(row => row.HexId).ToHashSet();
        var centers = query.Points.ToDictionary(row => row.CityId, row => row.HexId);
        bool Current() => ReferenceEquals(cityOfficerMovementMapOwner, owner) && admitted();
        int? hover = null;
        SetCommandTargetPicker(hex =>
        {
            if (Current() && query.QueryHex(hex) is { Allowed: true, CityId: int id }) picked(id);
        }, hex => Current() && query.QueryHex(hex).Allowed == true, hex =>
        {
            if (!Current()) return;
            int? id = hex is int point && query.QueryHex(point) is { Allowed: true } result ? result.CityId : null;
            if (hover == id) return;
            hover = id;
            IReadOnlySet<int>? selected = id is int city && centers.TryGetValue(city, out int center) ? new HashSet<int> { center } : null;
            _d3d11Markers?.SetCampaignMarkers(cityOfficerMovementMarkerHexes, selected);
            ScheduleD3D11MapFrame();
        });
        SetMapSummaryMode(MapSummarySelectionMode.Foothold);
        _d3d11Markers?.SetCampaignMarkers(cityOfficerMovementMarkerHexes);
        ScheduleD3D11MapFrame();
    }

    internal void EndCityOfficerMovementMapPick(object owner)
    {
        if (!ReferenceEquals(cityOfficerMovementMapOwner, owner)) return;
        cityOfficerMovementMapOwner = null; cityOfficerMovementMarkerHexes = null;
        SetCommandTargetPicker(null); SetMapSummaryMode(MapSummarySelectionMode.Normal);
        _d3d11Markers?.SetCampaignMarkers(null); ApplyKeyboardMapInformation(); ScheduleD3D11MapFrame();
    }
}
