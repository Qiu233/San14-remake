using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private Func<GameState, AreaAdministrationQuery<HexFirePresentationPublication>>? _hexFirePresentationQuery;
    private ushort?[]? _hexFireCaptureDurations;
    private bool _hexFireCaptureInitialized;
    private HexFirePresentationPublication? _hexFireCapture;

    internal void SetHexFirePresentationQuery(
        Func<GameState, AreaAdministrationQuery<HexFirePresentationPublication>> query)
    {
        _hexFirePresentationQuery = query;
        ResetHexFirePublicationCapture();
    }

    private HexFirePresentationPublication? CaptureHexFirePresentation(GameState world) =>
        _hexFirePresentationQuery?.Invoke(world).Value;

    private HexFirePresentationPublication? CaptureNormalHexFirePresentation(GameState world)
    {
        var durations = world.NativeBaseCaptureCurrent?.HexWords16;
        if (!_hexFireCaptureInitialized || !ReferenceEquals(durations, _hexFireCaptureDurations))
        {
            _hexFireCapture = CaptureHexFirePresentation(world);
            _hexFireCaptureDurations = durations;
            _hexFireCaptureInitialized = true;
        }
        return _hexFireCapture;
    }

    private void ResetHexFirePublicationCapture()
    {
        _hexFireCaptureDurations = null;
        _hexFireCaptureInitialized = false;
        _hexFireCapture = null;
    }
}
