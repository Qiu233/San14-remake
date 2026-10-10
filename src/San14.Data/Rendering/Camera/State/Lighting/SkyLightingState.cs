using System.Numerics;

namespace San14.Data;

public enum SkyLightingTransitionPhase : uint
{
    SunPrimary,
    SunToMoonFirst,
    SunToMoonSecond,
    MoonPrimary,
    MoonToSunFirst,
    MoonToSunSecond
}

public sealed record SkyCelestialLightState(Vector3 SelectedDirection, Vector4 Direction,
    Vector4 BaseColor, Vector4 CurrentColor, Vector3 PreviousColor);

public sealed record SkyLightingTransitionState(SkyLightingTransitionPhase Phase,
    bool SunPrimary, float Elapsed, float Continuation, float Strength);

/// <summary>Retained celestial lighting, independent of the camera transport layout.</summary>
public sealed record SkyLightingState(SkyCelestialLightState Sun, SkyCelestialLightState Moon,
    SkyLightingTransitionState Transition);

/// <summary>Only a completed Main frame's celestial history is carried into the next request.</summary>
public sealed record AcceptedSkyLightingState(long FrameNumber, string CameraResourceUri,
    string CameraResourceSha256, MainMapCameraFocus InitialFocus, SkyLightingState Lighting)
{
    public static AcceptedSkyLightingState Capture(SkySeasonMainCameraRgbFrame frame)
    {
        var main = frame.Inputs.MainFrame;
        return new(main.Camera.FrameNumber, main.Camera.Source.Resource.Source.ResourceUri,
            main.Camera.Source.Resource.Source.DecodedSha256, main.InitialFocus, frame.SunMoonRgb.Lighting);
    }

    internal void RequirePreceding(MainMapCameraFrame current)
    {
        if (FrameNumber >= current.Camera.FrameNumber ||
            CameraResourceUri != current.Camera.Source.Resource.Source.ResourceUri ||
            CameraResourceSha256 != current.Camera.Source.Resource.Source.DecodedSha256 ||
            InitialFocus != current.InitialFocus)
            throw new InvalidDataException("Retained celestial lighting belongs to another or nonpreceding Main owner frame.");
    }
}
