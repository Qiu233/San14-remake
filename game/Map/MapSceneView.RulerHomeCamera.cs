using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private Func<GameState, AreaAdministrationQuery<RulerHomeCameraTarget>>? _queryRulerHomeCamera;
    private MapStaticModelGroundHeightSource? _rulerHomeHeightField;
    private readonly Dictionary<int, float> _rulerHomeHeights = [];

    internal void ConfigureRulerHomeCamera(
        Func<GameState, AreaAdministrationQuery<RulerHomeCameraTarget>> query)
        => _queryRulerHomeCamera = query;

    private AreaAdministrationQuery<RulerHomeCameraTarget> CaptureRulerHomeCameraTarget(GameState? ownedWorld)
        => ownedWorld is not null && _queryRulerHomeCamera is { } query
            ? query(ownedWorld)
            : new(null, "hud.ruler_home_camera.current_world_unavailable");

    internal bool RequestRulerHomeFocus()
    {
        if (!D3D11MapInteractive || _detailsOpen ||
            (_periodProgressFrameDriver is not null && !_periodMapActive) ||
            _sourceMainCamera is not { } camera ||
            _sourceMainCameraInputs is not { } inputs) return false;

        // A playback worker owns its mutable candidate. Only the tiny fact
        // captured during an actual borrowed checkpoint may escape that borrow.
        var target = _periodMapActive
            ? _periodMapFrame?.RulerHome ?? _periodInitialRulerHome
            : CaptureRulerHomeCameraTarget(_logicalWorld);
        if (target is null || !target.Available)
        {
            SetMeta("ruler_home_camera_unavailable", target?.UnresolvedInput ?? "checkpoint_unavailable");
            SetSourceCameraStatus("ruler_home_unavailable");
            return false;
        }
        if (target.Value?.PhysicalHexId is not int hex) return false;

        float? height = null;
        if (inputs.CurrentMaximumYOverride)
        {
            if (_sourceMainCameraTerrain?.CurrentField is not { } field)
            {
                SetMeta("ruler_home_camera_unavailable", "height1889A0_unavailable");
                SetSourceCameraStatus("ruler_home_unavailable");
                return false;
            }
            if (!ReferenceEquals(field, _rulerHomeHeightField))
            {
                _rulerHomeHeights.Clear();
                _rulerHomeHeightField = field;
            }
            if (!_rulerHomeHeights.TryGetValue(hex, out float cached) || cached <= 0)
            {
                var point = CeHexWorldCoordinates.Project(hex);
                if (field.Query((float)point.X, (float)point.Z) is not float sampled || !float.IsFinite(sampled))
                {
                    SetMeta("ruler_home_camera_unavailable", "height1889A0_unavailable");
                    SetSourceCameraStatus("ruler_home_unavailable");
                    return false;
                }
                cached = MathF.Max(445, sampled);
                _rulerHomeHeights[hex] = cached;
            }
            height = cached;
        }

        var request = MainMapCameraRulerHomeRequest.Create(hex, inputs.CurrentMaximumYOverride, height);
        camera.QueueTimedRequest(request, unchecked((int)Time.GetTicksMsec()));
        RemoveMeta("ruler_home_camera_unavailable");
        SetSourceCameraStatus("ready");
        // Busy rejection still consumes the source command and must pause
        // progress. The application owns that side effect, not the camera.
        return true;
    }
}
