using Godot;
using San14.Data;

public readonly record struct TerrainPick(Vector3 Position, double Distance, int PatchIndex, int TriangleIndex);

/// <summary>Adapts viewport rays to the CPU triangles supplied to terrain uploads.
/// A captured picker remains valid when its display meshes are retired.</summary>
public sealed class TerrainPicker
{
    private readonly TerrainRayPicker _picker;
    private TerrainPicker(TerrainRayPicker picker) => _picker = picker;

    public bool TryPick(Camera3D camera, Vector2 viewportPoint, out TerrainPick hit)
    {
        hit = default;
        if (!viewportPoint.IsFinite()) return false;
        Vector3 origin = camera.ProjectRayOrigin(viewportPoint), ray = camera.ProjectRayNormal(viewportPoint);
        Vector3 forward = -camera.GlobalBasis.Z.Normalized();
        if (!origin.IsFinite() || !ray.IsFinite() || !forward.IsFinite()) return false;
        double length = Math.Sqrt((double)ray.X * ray.X + (double)ray.Y * ray.Y + (double)ray.Z * ray.Z);
        if (length <= 0 || !double.IsFinite(length)) return false;
        double dx = ray.X / length, dy = ray.Y / length, dz = ray.Z / length;
        double cosine = dx * forward.X + dy * forward.Y + dz * forward.Z;
        if (cosine <= 0) return false;
        Vector3 position = camera.GlobalPosition;
        double depth = ((double)origin.X - position.X) * forward.X + ((double)origin.Y - position.Y) * forward.Y +
            ((double)origin.Z - position.Z) * forward.Z;
        double start = Math.Max(0, (camera.Near - depth) / cosine), end = (camera.Far - depth) / cosine;
        if (!double.IsFinite(start) || !double.IsFinite(end) || end <= start) return false;
        Vector3 clippedOrigin = new((float)(origin.X + dx * start), (float)(origin.Y + dy * start), (float)(origin.Z + dz * start));
        return TryPick(clippedOrigin, ray, end - start, out hit);
    }

    public static TerrainPicker Build(TerrainMeshResult terrain)
        => Build(terrain.Patches);

    public static TerrainPicker Build(IReadOnlyList<TerrainPatch> terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        return new(TerrainRayPicker.Create(terrain.Select(patch => patch.Geometry)));
    }

    public bool TryPick(Vector3 origin, Vector3 direction, double maximumDistance, out TerrainPick hit)
    {
        hit = default;
        if (!_picker.TryPick(new(origin.X, origin.Y, origin.Z), new(direction.X, direction.Y, direction.Z),
            maximumDistance, out TerrainRayHit result)) return false;
        hit = new(new(result.Position.X, result.Position.Y, result.Position.Z), result.Distance,
            result.PatchIndex, result.TriangleIndex);
        return true;
    }
}
