using San14.Data;

internal sealed record DistrictCommandMapPoint(int Identity, int HexId, bool Enabled);

public partial class MapSceneView
{
    private object? districtCommandMapOwner;
    private IReadOnlySet<int>? districtCommandMarkerHexes;
    private IReadOnlyDictionary<int, int>? districtCommandMarkerIdentities;

    /// <summary>District commands share the normal city markers and terrain
    /// picker. Their draft supplies captured identities and permissions.</summary>
    internal void BeginDistrictCommandMapPick(object owner, IReadOnlyList<DistrictCommandMapPoint> points,
        Func<int, int?> resolveIdentity, Func<bool> admitted, Action<int> picked, Action<int?>? hovered = null)
    {
        if (!IsPlayableMapReady) throw new InvalidOperationException("District selection requires its live map.");
        if (districtCommandMapOwner is { } previous) EndDistrictCommandMapPick(previous);
        if (_commandTargetPicked is not null)
            throw new InvalidOperationException("Another command owns the map picker.");
        var markers = points.GroupBy(point => point.HexId).ToDictionary(group => group.Key, group => group.First().Identity);
        var allowed = points.Where(point => point.Enabled).Select(point => point.Identity).ToHashSet();
        districtCommandMapOwner = owner;
        districtCommandMarkerIdentities = markers; districtCommandMarkerHexes = markers.Keys.ToHashSet();
        bool Current() => ReferenceEquals(districtCommandMapOwner, owner) && admitted();
        int? lastHover = null;
        SetCommandTargetPicker(hex =>
        {
            if (Current() && resolveIdentity(hex) is int identity && allowed.Contains(identity)) picked(identity);
        }, hex => Current() && resolveIdentity(hex) is not null, hex =>
        {
            if (!Current()) return;
            int? identity = hex is int value ? resolveIdentity(value) : null;
            if (identity == lastHover) return;
            lastHover = identity; hovered?.Invoke(identity);
        });
        ApplyDistrictCommandMapPick(owner, []);
    }

    internal void ApplyDistrictCommandMapPick(object owner, IReadOnlyList<int> selectedIdentities)
    {
        if (!ReferenceEquals(districtCommandMapOwner, owner) || districtCommandMarkerIdentities is null) return;
        var selected = selectedIdentities.ToHashSet();
        var hexes = districtCommandMarkerIdentities.Where(point => selected.Contains(point.Value)).Select(point => point.Key).ToHashSet();
        _d3d11Markers?.SetCampaignMarkers(districtCommandMarkerHexes, hexes);
        ScheduleD3D11MapFrame();
    }

    internal void EndDistrictCommandMapPick(object owner)
    {
        if (!ReferenceEquals(districtCommandMapOwner, owner)) return;
        districtCommandMapOwner = null; districtCommandMarkerHexes = null; districtCommandMarkerIdentities = null;
        SetCommandTargetPicker(null);
        _d3d11Markers?.SetCampaignMarkers(null); ApplyKeyboardMapInformation(); ScheduleD3D11MapFrame();
    }
}
