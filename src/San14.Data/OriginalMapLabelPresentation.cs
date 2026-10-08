using System.Numerics;

namespace San14.Data;

public readonly record struct OriginalAreaMapLabelPresentation(int CameraLevel, int Display,
    float DistanceFade, byte TextAlpha, byte FlagAlpha)
{
    public bool Visible => Display != -1 && !(CameraLevel < 4 && DistanceFade > 1f);
    public bool ShowName => Display == 0;
}

public readonly record struct OriginalCityGateMapLabelPresentation(int CameraLevel, int Display,
    float DistanceFade, byte PanelAlpha, byte FlagAlpha)
{
    public bool Visible => Display != -1 && !(CameraLevel < 4 && DistanceFade > 1f);
}

public readonly record struct OriginalArmyMapLabelPresentation(int CameraLevel, int Display,
    float DistanceFade, byte PortraitAlpha, byte DetailsAlpha, byte SimpleAlpha, byte GroupAlpha)
{
    public bool Visible => Display != -1 && !(CameraLevel < 4 && DistanceFade > 1f);
}

/// <summary>Ordinary MapInfoMode0, settled camera labels. Transition animations,
/// alternate camera owners and interaction overrides have separate source paths.
/// This leaf reads the same accepted frame as the terrain, with no resource IO.</summary>
public static class OriginalMapLabelPresentation
{
    public static int SettledCameraLevel(OriginalMainMapCameraZoomSelection selection)
    {
        int level = (int)MathF.Floor((float)selection.Band1E0 + (float)selection.Fraction1E4 / 10000f);
        if ((uint)level >= 7) throw new InvalidDataException("Original map label camera level is outside its seven source rows.");
        return level;
    }

    public static OriginalAreaMapLabelPresentation Area(OriginalMainMapCameraFrame frame, Vector3 unitPosition)
    {
        int level = SettledCameraLevel(frame.ZoomSelection);
        var eye = frame.Camera.CurrentPose.Position80;
        float fade = DistanceFade(unitPosition, new(eye.X, eye.Y, eye.Z), frame.ControllerState.Distance90);
        return new(level, OriginalAreaMapMarkerSources.NormalMapSettledDisplayLevel(level), fade,
            TextAlpha(level, fade), FlagAlpha(level, fade));
    }

    public static OriginalCityGateMapLabelPresentation CityGate(OriginalMainMapCameraFrame frame, Vector3 unitPosition)
    {
        int level = SettledCameraLevel(frame.ZoomSelection);
        var eye = frame.Camera.CurrentPose.Position80;
        float fade = DistanceFade(unitPosition, new(eye.X, eye.Y, eye.Z), frame.ControllerState.Distance90);
        return new(level, OriginalCityGateMapMarkerSources.NormalMapSettledDisplayLevel(level), fade,
            TextAlpha(level, fade), FlagAlpha(level, fade));
    }

    public static OriginalArmyMapLabelPresentation Army(OriginalMainMapCameraFrame frame, Vector3 unitPosition)
    {
        int level = SettledCameraLevel(frame.ZoomSelection);
        var eye = frame.Camera.CurrentPose.Position80;
        float fade = DistanceFade(unitPosition, new(eye.X, eye.Y, eye.Z), frame.ControllerState.Distance90);
        // 1AA280/1AA3D0: UnitAction portrait fades as a flag; UI, details and
        // morale fade as text. Simple's parent remains255 in this normal path.
        return new(level, OriginalArmyMapMarkerSources.NormalMapSettledDisplayLevel(level), fade,
            FlagAlpha(level, fade), TextAlpha(level, fade), 255, FlagAlpha(level, fade));
    }

    // 19EEA0 measures v278's unoffset Unit position. The v260 world-height
    // offset belongs only to projection and must not enter this distance.
    public static float DistanceFade(Vector3 unitPosition, Vector3 eye, double cameraDistance90)
    {
        float x = eye.X - unitPosition.X, y = eye.Y - unitPosition.Y, z = eye.Z - unitPosition.Z;
        float distance = MathF.Sqrt(x * x + y * y + z * z);
        float fade = (distance - (float)cameraDistance90) / 25000f;
        return fade >= 0f ? fade : 0f;
    }

    // 19DFF0 and 19E040 use scalar float32 at each step, then truncate to
    // Int64 and publish AL. In particular the flag denominator is not 0.2f.
    public static byte TextAlpha(int cameraLevel, float fade)
    {
        if (cameraLevel < 4 && fade >= 0.3f)
            return fade < 0.6f ? (byte)(long)((1f - (fade - 0.3f) / 0.3f) * 255f) : (byte)0;
        return 255;
    }

    public static byte FlagAlpha(int cameraLevel, float fade)
    {
        if (cameraLevel < 4 && fade >= 0.8f)
            return fade < 1f ? (byte)(long)((1f - (fade - 0.8f) / 0.19999999f) * 255f) : (byte)0;
        return 255;
    }
}
