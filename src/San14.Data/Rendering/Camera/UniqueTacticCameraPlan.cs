using System.Numerics;

namespace San14.Data;

/// <summary>The three actual Main-controller commands built when a Direction
/// scene is created. Its focus is supplied by the map's current view lookup.</summary>
public sealed record UniqueTacticCameraPlan(MainMapCameraControllerRequest Approach,
    MainMapCameraControllerRequest Orbit, MainMapCameraControllerRequest Restore)
{
    public static UniqueTacticCameraPlan Create(MainMapCameraFrame frame, Vector4 casterFocus)
    {
        var saved = frame.ControllerState;
        var eye = frame.Camera.CurrentPose.Position80;
        float x = (float)saved.FocusX - eye.X, z = (float)saved.FocusZ - eye.Z;
        float inverse = 1f / (float)Math.Sqrt(x * x + 0f + z * z);
        var rotation = NativeTrigonometry.SinCos014590(BitConverter.UInt32BitsToSingle(0xBF060A92));
        float a = rotation.Sin * z * inverse, b = rotation.Cos * x * inverse;
        float c = rotation.Cos * z * inverse, d = x * inverse * rotation.Sin;
        float offset = UniqueTacticEffectPublication.SecondCameraFocusOffset;
        Vector4 orbitFocus = new(offset * (a + b) + casterFocus.X,
            offset * inverse * 0f + casterFocus.Y, offset * (c - d) + casterFocus.Z, casterFocus.W);
        return new(new(1000, casterFocus, UniqueTacticEffectPublication.InitialCameraDistance,
                62.83185308, BitConverter.Int64BitsToDouble(0x3FD6571860000000), 1),
            new(300, orbitFocus, -1, saved.YawA8 - 0.5235987901687622, -1, 1),
            new(300, new((float)saved.FocusX, (float)saved.FocusY, (float)saved.FocusZ, (float)saved.FocusW),
                saved.Distance90, saved.YawA8, saved.PitchC0, 1));
    }
}
