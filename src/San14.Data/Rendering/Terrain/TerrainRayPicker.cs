using System.Numerics;

namespace San14.Data;

/// <summary>The CPU triangles supplied to a terrain upload. Retaining this
/// object keeps a picking snapshot alive independently of graphics resources.</summary>
public sealed class TerrainTriangleGeometry
{
    internal TerrainTriangleGeometry(Vector3[] positions, int[] indices)
    {
        Positions = positions;
        Indices = indices;
        Vector3 minimum = positions[0], maximum = positions[0];
        foreach (Vector3 position in positions)
        {
            minimum = Vector3.Min(minimum, position);
            maximum = Vector3.Max(maximum, position);
        }
        Minimum = minimum;
        // A float AABB end is reconstructed from its origin and size. Keep
        // that rounding conservative rather than moving a known vertex outside.
        Maximum = Vector3.Max(maximum, minimum + (maximum - minimum));
    }

    public ReadOnlyMemory<Vector3> Positions { get; }
    public ReadOnlyMemory<int> Indices { get; }
    public Vector3 Minimum { get; }
    public Vector3 Maximum { get; }
}

public readonly record struct TerrainRayHit(Vector3 Position, double Distance, int PatchIndex, int TriangleIndex);

/// <summary>An ordered triangle snapshot. Its geometry is shared with terrain
/// uploads; creating a new snapshot copies references, not vertex/index arrays.</summary>
public sealed class TerrainRayPicker
{
    private const double BoundsPadding = 0.02;
    private const double IntersectionTolerance = 1e-12;
    private readonly TerrainTriangleGeometry[] patches;

    private TerrainRayPicker(TerrainTriangleGeometry[] patches) => this.patches = patches;

    public static TerrainRayPicker Create(IEnumerable<TerrainTriangleGeometry> geometries)
    {
        ArgumentNullException.ThrowIfNull(geometries);
        TerrainTriangleGeometry[] snapshot = geometries.ToArray();
        if (snapshot.Any(geometry => geometry is null))
            throw new ArgumentException("Terrain picking requires prepared triangle geometry.", nameof(geometries));
        return new(snapshot);
    }

    public bool TryPick(Vector3 origin, Vector3 direction, double maximumDistance, out TerrainRayHit hit)
    {
        hit = default;
        if (!IsFinite(origin) || !IsFinite(direction) || !double.IsFinite(maximumDistance) || maximumDistance <= 0)
            return false;
        double norm = Math.Sqrt((double)direction.X * direction.X + (double)direction.Y * direction.Y + (double)direction.Z * direction.Z);
        if (norm <= 0 || !double.IsFinite(norm)) return false;
        double dx = direction.X / norm, dy = direction.Y / norm, dz = direction.Z / norm;
        double nearest = maximumDistance;
        bool found = false;
        for (int patchIndex = 0; patchIndex < patches.Length; patchIndex++)
        {
            TerrainTriangleGeometry patch = patches[patchIndex];
            if (!IntersectsBounds(patch, origin, dx, dy, dz, nearest)) continue;
            ReadOnlySpan<Vector3> positions = patch.Positions.Span;
            ReadOnlySpan<int> indices = patch.Indices.Span;
            for (int index = 0; index < indices.Length; index += 3)
            {
                Vector3 a = positions[indices[index]], b = positions[indices[index + 1]], c = positions[indices[index + 2]];
                if (!IntersectTriangle(origin, dx, dy, dz, a, b, c, nearest, out double distance)) continue;
                Vector3 position = new((float)(origin.X + dx * distance), (float)(origin.Y + dy * distance),
                    (float)(origin.Z + dz * distance));
                if (!IsFinite(position)) continue;
                nearest = distance;
                hit = new(position, distance, patchIndex, index / 3);
                found = true;
            }
        }
        return found;
    }

    private static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);

    private static bool IntersectsBounds(TerrainTriangleGeometry bounds, Vector3 origin,
        double dx, double dy, double dz, double maximum)
    {
        double minimum = 0;
        return Axis(origin.X, dx, bounds.Minimum.X, bounds.Maximum.X) &&
            Axis(origin.Y, dy, bounds.Minimum.Y, bounds.Maximum.Y) && Axis(origin.Z, dz, bounds.Minimum.Z, bounds.Maximum.Z);
        bool Axis(double from, double direction, double lower, double upper)
        {
            lower -= BoundsPadding; upper += BoundsPadding;
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
        if (Math.Abs(determinant) < IntersectionTolerance) return false;
        double tx = (double)from.X - a.X, ty = (double)from.Y - a.Y, tz = (double)from.Z - a.Z;
        double u = (tx * px + ty * py + tz * pz) / determinant;
        if (u < -IntersectionTolerance || u > 1 + IntersectionTolerance) return false;
        double qx = ty * ez - tz * ey, qy = tz * ex - tx * ez, qz = tx * ey - ty * ex;
        double v = (dx * qx + dy * qy + dz * qz) / determinant;
        if (v < -IntersectionTolerance || u + v > 1 + IntersectionTolerance) return false;
        distance = (fx * qx + fy * qy + fz * qz) / determinant;
        return double.IsFinite(distance) && distance >= 0 && distance <= maximum;
    }
}
