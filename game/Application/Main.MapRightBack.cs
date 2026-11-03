using Godot;

public partial class Main
{
    private enum MapRightBackKind { Page, AreaMenu, CityMenu, GeographicView, Reporting, Focus }
    private readonly record struct MapRightBackPress(MapSceneView Map, object Owner, MapRightBackKind Kind);
    private MapRightBackPress? _mapRightBackPress;

    private void HandleMapRightBack(InputEventMouseButton button, MapSceneView map, object owner,
        MapRightBackKind kind, Action back, bool allowDuringPeriod = false)
    {
        bool click = map.SourceRightCancelReleased(button);
        if (button.Pressed)
        {
            _mapRightBackPress = new(map, owner, kind);
            // The map surface admits this same press and its subsequent orbit.
            return;
        }
        var pressed = _mapRightBackPress;
        _mapRightBackPress = null;
        if (!click || pressed is not { } prior || !ReferenceEquals(prior.Map, map) ||
            !ReferenceEquals(prior.Owner, owner) || prior.Kind != kind) return;
        GetViewport().SetInputAsHandled();
        PlayUiAudio(cancel: true);
        RunUiAction(back, allowDuringPeriod);
    }
}
