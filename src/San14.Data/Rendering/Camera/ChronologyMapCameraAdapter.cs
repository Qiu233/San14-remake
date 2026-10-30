using System.Numerics;

namespace San14.Data;

/// <summary>The chronology's independent controller, published through the
/// retained primary Camera. The reached state entry applies its pose immediately;
/// normal Main navigation and its bounds remain with the retained Main owner.</summary>
public sealed class ChronologyMapCameraAdapter
{
    private float retainedFov12E8;

    private ChronologyMapCameraAdapter(MainMapCameraAdapter primary)
    {
        Source = primary.Source;
        Parameters = primary.Parameters;
        InitialFocus = primary.InitialFocus;
        SavedMainState = primary.CurrentState;
        retainedFov12E8 = primary.CurrentFov12E8;
        // Generic C810 plus float PI/PI2, then8E60E0(controller,1): duration0.
        CurrentState = new(-30600, 0, 112400, 1, 240000,
            (double)3.1415927410125732f, (double)1.5707963705062866f);
    }

    public LoadedMapCameraConstructionSource Source { get; }
    public MainMapCameraControllerParameters Parameters { get; }
    public MainMapCameraFocus InitialFocus { get; }
    public MainMapCameraControllerState SavedMainState { get; }
    public MainMapCameraControllerState CurrentState { get; }
    public float CurrentFov12E8 => retainedFov12E8;
    public float CurrentFar14 => 300000;
    public int AppModeOverride28C => 4;

    /// <summary>Use only at the actual chronology entry after the primary Main,
    /// CPU and loaded resource context exist. The host retains the primary owner
    /// and restores its ordinary far/mode at the source exit boundary.</summary>
    public static ChronologyMapCameraAdapter FromPrimary(MainMapCameraAdapter primary)
    {
        ArgumentNullException.ThrowIfNull(primary);
        return new(primary);
    }

    public MainMapCameraFrame PrepareCurrent(MainMapCameraFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.FrameNumber < 1 || string.IsNullOrWhiteSpace(input.FrameProvenance))
            throw new InvalidDataException("Chronology camera publication requires an actual frame and provenance.");
        var pose = MapCameraController.Publish(CurrentState, input.CurrentScale90);
        string provenance = $"chronology-immediate-entry;active-primary-controller;{Source.ConstructorOrigin};" +
            $"owned-world={InitialFocus.OwnedWorldProvenance};frame={input.FrameProvenance}";
        var camera = MapCameraConstruction.PrepareFreshPerspective(Source, pose,
            input.ContextOwner3E4Bit29, input.NativeClipProfile, input.TangentDispatch,
            Parameters.Locale, input.FrameNumber, provenance);
        if (input.SourceSelectedFovV410 is float selected) retainedFov12E8 = selected;
        bool overrideApplied = input.OwnerFovOverride290 > 0;
        float fov = MapCameraController.ApplyPositiveFovOverride(retainedFov12E8, input.OwnerFovOverride290);
        float far = pose.Scale90.Z * CurrentFar14;
        if (BitConverter.SingleToUInt32Bits(camera.Lens.VerticalFovRadians) != BitConverter.SingleToUInt32Bits(fov) ||
            BitConverter.SingleToUInt32Bits(camera.Lens.Far) != BitConverter.SingleToUInt32Bits(far))
        {
            var lens = camera.Lens with { VerticalFovRadians = fov, Far = far };
            var geometry = camera.Geometry.WithLens(lens, input.NativeClipProfile, input.TangentDispatch);
            camera = new(camera.Prefix.ToArray(), Source, geometry, Parameters.Locale,
                input.FrameNumber, provenance + ";same-primary-retained-lens", optics: camera.Optics);
        }
        retainedFov12E8 = fov;
        float eyeY = MapCameraController.DerivePose(CurrentState).Eye10.Y;
        var zoom = MapCameraController.ClassifyZoom(Parameters, eyeY);
        return new(camera, CurrentState, zoom, InitialFocus, overrideApplied);
    }

    /// <summary>013830's actual immediate restore request. Source pitch comes
    /// from the retained Main's distance-band table, rather than saved PitchC0.
    /// Apply through that Main owner's existing immediate request consumer.</summary>
    public static MainMapCameraControllerRequest CreateRestoreRequest(
        MainMapCameraControllerState savedState, MainMapCameraControllerParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(savedState);
        ArgumentNullException.ThrowIfNull(parameters);
        var selection = MapCameraController.ClassifyZoom(parameters, (float)savedState.Distance90);
        return new(0, new((float)savedState.FocusX, (float)savedState.FocusY,
                (float)savedState.FocusZ, (float)savedState.FocusW),
            savedState.Distance90, savedState.YawA8, parameters.ZoomBandPitch(selection.Band1E0), 0);
    }
}
