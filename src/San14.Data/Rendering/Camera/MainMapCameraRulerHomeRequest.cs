using System.Numerics;

namespace San14.Data;

/// <summary>The normal toolbar's500ms ruler-home camera command. Height
/// ownership and progress pause belong to the actual scene/application caller.</summary>
public static class MainMapCameraRulerHomeRequest
{
    /// <param name="ownedHeight1889A0">When Y override is true, the already
    /// resolved result of the scene's real height owner: positive Hex cache,
    /// otherwise ground sampled with the source445 floor, or known0 when that
    /// native scene host is absent. Null is unknown and is never guessed0.</param>
    public static MainMapCameraControllerRequest Create(int physicalHexId,
        bool currentMaximumYOverride, float? ownedHeight1889A0 = null)
    {
        var point = CeHexWorldCoordinates.Project(physicalHexId);
        float y = 0;
        if (currentMaximumYOverride)
        {
            if (ownedHeight1889A0 is not float height || !float.IsFinite(height))
                throw new InvalidDataException("hud.ruler_home_camera.height1889A0_unavailable");
            y = height;
        }
        return new(500, new Vector4((float)point.X, y, (float)point.Z, 1f),
            -1, 62.83185308, -1, 1);
    }
}
