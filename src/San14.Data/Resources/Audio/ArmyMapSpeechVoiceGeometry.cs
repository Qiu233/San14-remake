namespace San14.Data;

/// <summary>402A50/403C40's finite listener projection and one-shot distance
/// admission. It does not define an audio-engine attenuation curve.</summary>
public static class ArmyMapSpeechVoiceGeometry
{
    public static NarrationSpatialVoicePosition QueryListener(MainMapCameraFrame frame,
        MainMapCameraControllerBounds bounds, bool currentCameraResourcePresent)
    {
        ArgumentNullException.ThrowIfNull(frame);
        //011040 owns CMainCameraCtrl at+50; the fallback vector at+70
        //is the B6A0-computed Eye(+20), not its double-precision focus.
        var eye = MapCameraController.DerivePose(frame.ControllerState).Eye10;
        var fallback = new NarrationSpatialVoicePosition(eye.X, eye.Y, eye.Z);
        if (!currentCameraResourcePresent) return fallback;
        //The singleton110/118/120 fields map to controllerC0/C8/D0:
        //current pitch and its actual bounds, rather than zoom distance.
        float pitch = (float)frame.ControllerState.PitchC0;
        float minimum = (float)bounds.MinimumPitchC8, maximum = (float)bounds.MaximumPitchD0;
        float numerator = pitch - minimum, denominator = maximum - minimum;
        float fraction = numerator / denominator;
        float displacement = (fraction - 1f) * 540f;
        int pixelY = unchecked(540 - (int)displacement);
        var ray = frame.CursorRay(960, pixelY);
        if (!ray.SourceReady) return fallback;
        //011380 leaves its output unchanged for a negative intersection.
        float distance = (445f - ray.Origin.Y) / ray.Direction.Y;
        if (distance < 0f) return fallback;
        return new(ray.Direction.X * distance + ray.Origin.X,
            distance * ray.Direction.Y + ray.Origin.Y,
            distance * ray.Direction.Z + ray.Origin.Z);
    }

    /// <summary>Use only after the actual service/three-slot capacity gates.
    /// Y1 belongs to the local distance copy; the emitter keeps source XYZ.</summary>
    public static bool WithinSourceDistance(NarrationSpatialVoicePosition source,
        NarrationSpatialVoicePosition listener)
    {
        float dx = source.X - listener.X, dz = source.Z - listener.Z;
        float square = dx * dx + 1f + dz * dz;
        return MathF.Sqrt(square) < 50000f;
    }
}
