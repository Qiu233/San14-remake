using System.Numerics;

namespace San14.Data;

/// <summary>A terrain-picking ray restricted to the displayed camera's near/far planes.</summary>
public readonly record struct MapCameraPickSegment(
    Vector3 Origin, Vector3 Direction, double StartDistance, double MaximumDistance)
{
    /// <summary>Near/far measure view depth; terrain picking measures distance along a unit ray.</summary>
    public static bool TryCreate(MapCameraGeometry camera, MainMapCameraCursorRay ray,
        out MapCameraPickSegment segment)
    {
        segment = default;
        if (!ray.SourceReady || !Finite(ray.Origin) || !Finite(ray.Direction) ||
            !float.IsFinite(camera.Lens.Near) || !float.IsFinite(camera.Lens.Far) ||
            camera.Lens.Near <= 0 || camera.Lens.Far <= camera.Lens.Near)
            return false;
        double x = ray.Direction.X, y = ray.Direction.Y, z = ray.Direction.Z;
        double norm = Math.Sqrt(x * x + y * y + z * z);
        if (!double.IsFinite(norm) || norm <= 0) return false;
        x /= norm; y /= norm; z /= norm;

        // Row-vector world-to-view uses negative Z in front of the camera.
        // This also follows the actual adjusted view when its target or up vector differs.
        var view = camera.Matrices.WorldToView80;
        double depth = -((double)ray.Origin.X * view.M13 + (double)ray.Origin.Y * view.M23 +
            (double)ray.Origin.Z * view.M33 + view.M43);
        double depthStep = -(x * view.M13 + y * view.M23 + z * view.M33);
        if (!double.IsFinite(depth) || !double.IsFinite(depthStep) || depthStep <= 0)
            return false;
        double start = Math.Max(0, (camera.Lens.Near - depth) / depthStep);
        double end = (camera.Lens.Far - depth) / depthStep;
        if (!double.IsFinite(start) || !double.IsFinite(end) || end <= start) return false;
        var origin = new Vector3((float)(ray.Origin.X + x * start),
            (float)(ray.Origin.Y + y * start), (float)(ray.Origin.Z + z * start));
        var direction = new Vector3((float)x, (float)y, (float)z);
        if (!float.IsFinite(origin.X) || !float.IsFinite(origin.Y) || !float.IsFinite(origin.Z))
            return false;
        segment = new(origin, direction, start, end - start);
        return true;
    }

    private static bool Finite(Vector4 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
