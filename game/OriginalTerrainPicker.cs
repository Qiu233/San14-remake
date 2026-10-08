using Godot;

public readonly record struct TerrainPick(Vector3 Position, double Distance, int PatchIndex, int TriangleIndex);

/// <summary>CPU picking against the actual displayed mesh. No simulation passability or physics state.</summary>
public sealed class OriginalTerrainPicker
{
    private sealed record Patch(Aabb Bounds, Vector3[] Vertices, int[] Indices);
    private readonly Patch[] _patches;
    private OriginalTerrainPicker(Patch[] patches) => _patches = patches;

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

    public static OriginalTerrainPicker Build(OriginalTerrainMeshResult terrain)
        => Build(terrain.Patches);

    public static OriginalTerrainPicker Build(IReadOnlyList<OriginalTerrainPatch> terrain)
    {
        var patches = new List<Patch>();
        foreach (OriginalTerrainPatch source in terrain)
        {
            if (source.Mesh.GetSurfaceCount() != 1)
                throw new InvalidDataException("Terrain picking requires the displayed single-surface patch.");
            using Godot.Collections.Array arrays = source.Mesh.SurfaceGetArrays(0);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            int[] indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            if (vertices.Length == 0 || indices.Length == 0 || indices.Length % 3 != 0 ||
                vertices.Any(vertex => !vertex.IsFinite()) || indices.Any(index => (uint)index >= vertices.Length))
                throw new InvalidDataException("Terrain picking received invalid displayed triangle arrays.");
            patches.Add(new(source.Mesh.GetAabb(), vertices, indices));
        }
        return new(patches.ToArray());
    }

    public bool TryPick(Vector3 origin, Vector3 direction, double maximumDistance, out TerrainPick hit)
    {
        hit = default;
        if (!origin.IsFinite() || !direction.IsFinite() || !double.IsFinite(maximumDistance) || maximumDistance <= 0)
            return false;
        double norm = Math.Sqrt((double)direction.X * direction.X + (double)direction.Y * direction.Y + (double)direction.Z * direction.Z);
        if (norm <= 0 || !double.IsFinite(norm)) return false;
        double dx = direction.X / norm, dy = direction.Y / norm, dz = direction.Z / norm;
        double nearest = maximumDistance;
        bool found = false;
        for (int patchIndex = 0; patchIndex < _patches.Length; patchIndex++)
        {
            Patch patch = _patches[patchIndex];
            if (!IntersectsBounds(patch.Bounds, origin, dx, dy, dz, nearest)) continue;
            for (int index = 0; index < patch.Indices.Length; index += 3)
            {
                Vector3 a = patch.Vertices[patch.Indices[index]], b = patch.Vertices[patch.Indices[index + 1]],
                    c = patch.Vertices[patch.Indices[index + 2]];
                if (!IntersectTriangle(origin, dx, dy, dz, a, b, c, nearest, out double distance)) continue;
                Vector3 position = new((float)(origin.X + dx * distance), (float)(origin.Y + dy * distance),
                    (float)(origin.Z + dz * distance));
                if (!position.IsFinite()) continue;
                nearest = distance;
                hit = new(position, distance, patchIndex, index / 3);
                found = true;
            }
        }
        return found;
    }

    private static bool IntersectsBounds(Aabb bounds, Vector3 origin, double dx, double dy, double dz, double maximum)
    {
        double minimum = 0;
        Vector3 end = bounds.End;
        // Include rounded mesh bounds at shared patch edges.
        return Axis(origin.X, dx, bounds.Position.X, end.X) &&
            Axis(origin.Y, dy, bounds.Position.Y, end.Y) && Axis(origin.Z, dz, bounds.Position.Z, end.Z);
        bool Axis(double from, double direction, double lower, double upper)
        {
            lower -= 0.02; upper += 0.02;
            if (direction == 0) return from >= lower && from <= upper;
            double a = (lower - from) / direction, b = (upper - from) / direction;
            if (a > b) (a, b) = (b, a);
            minimum = Math.Max(minimum, a); maximum = Math.Min(maximum, b);
            return maximum >= minimum;
        }
    }

    private static bool IntersectTriangle(Vector3 from, double dx, double dy, double dz,
        Vector3 a, Vector3 b, Vector3 c, double maximum, out double distance)
    {
        distance = 0;
        double ex = (double)b.X - a.X, ey = (double)b.Y - a.Y, ez = (double)b.Z - a.Z;
        double fx = (double)c.X - a.X, fy = (double)c.Y - a.Y, fz = (double)c.Z - a.Z;
        double px = dy * fz - dz * fy, py = dz * fx - dx * fz, pz = dx * fy - dy * fx;
        double determinant = ex * px + ey * py + ez * pz;
        if (Math.Abs(determinant) < 1e-12) return false;
        double tx = (double)from.X - a.X, ty = (double)from.Y - a.Y, tz = (double)from.Z - a.Z;
        double u = (tx * px + ty * py + tz * pz) / determinant;
        if (u < -1e-12 || u > 1 + 1e-12) return false;
        double qx = ty * ez - tz * ey, qy = tz * ex - tx * ez, qz = tx * ey - ty * ex;
        double v = (dx * qx + dy * qy + dz * qz) / determinant;
        if (v < -1e-12 || u + v > 1 + 1e-12) return false;
        distance = (fx * qx + fy * qy + fz * qz) / determinant;
        return double.IsFinite(distance) && distance >= 0 && distance <= maximum;
    }
}
