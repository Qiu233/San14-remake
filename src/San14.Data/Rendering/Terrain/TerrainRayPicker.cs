using System.Numerics;

namespace San14.Data;

/// <summary>The CPU triangles supplied to a terrain upload. Retaining this
/// object keeps a picking snapshot alive independently of graphics resources.</summary>
public sealed class TerrainTriangleGeometry
{
    // Contiguous source triangles keep their original traversal and tie order.
    // The balanced bounds tree is built with the immutable upload geometry.
    internal const int TrianglesPerLeaf = 32;
    private readonly TerrainTriangleBounds[] triangleBounds;
    internal int TriangleLeafBase { get; }
    internal ReadOnlySpan<TerrainTriangleBounds> TriangleBounds => triangleBounds;

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
        if (indices.Length % 3 != 0)
            throw new InvalidDataException("Terrain picking requires complete source triangles.");
        int leafCount = (indices.Length / 3 + TrianglesPerLeaf - 1) / TrianglesPerLeaf;
        int leafBase = 1;
        while (leafBase < leafCount) leafBase = checked(leafBase * 2);
        TriangleLeafBase = leafBase;
        triangleBounds = new TerrainTriangleBounds[checked(leafBase * 2)];
        Array.Fill(triangleBounds, new(new(float.PositiveInfinity), new(float.NegativeInfinity)));
        for (int leaf = 0; leaf < leafCount; leaf++)
        {
            Vector3 lower = new(float.PositiveInfinity), upper = new(float.NegativeInfinity);
            int begin = leaf * TrianglesPerLeaf * 3;
            int end = begin + Math.Min(TrianglesPerLeaf * 3, indices.Length - begin);
            for (int index = begin; index < end; index++)
            {
                var point = positions[indices[index]];
                lower = Vector3.Min(lower, point);
                upper = Vector3.Max(upper, point);
            }
            triangleBounds[leafBase + leaf] = new(lower, upper);
        }
        for (int node = leafBase - 1; node > 0; node--)
        {
            var left = triangleBounds[node * 2];
            var right = triangleBounds[node * 2 + 1];
            triangleBounds[node] = new(Vector3.Min(left.Minimum, right.Minimum),
                Vector3.Max(left.Maximum, right.Maximum));
        }
    }

    public ReadOnlyMemory<Vector3> Positions { get; }
    public ReadOnlyMemory<int> Indices { get; }
    public Vector3 Minimum { get; }
    public Vector3 Maximum { get; }
}

internal readonly record struct TerrainTriangleBounds(Vector3 Minimum, Vector3 Maximum);

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
        // An int-indexed balanced tree has fewer than 32 levels. Push the
        // right child first so leaves retain source triangle order.
        Span<int> pending = stackalloc int[32];
        for (int patchIndex = 0; patchIndex < patches.Length; patchIndex++)
        {
            TerrainTriangleGeometry patch = patches[patchIndex];
            if (!IntersectsBounds(patch.Minimum, patch.Maximum, origin, dx, dy, dz, nearest)) continue;
            ReadOnlySpan<Vector3> positions = patch.Positions.Span;
            ReadOnlySpan<int> indices = patch.Indices.Span;
            var bounds = patch.TriangleBounds;
            int pendingCount = 1;
            pending[0] = 1;
            while (pendingCount > 0)
            {
                int node = pending[--pendingCount];
                var box = bounds[node];
                if (box.Minimum.X == float.PositiveInfinity ||
                    !IntersectsBounds(box.Minimum, box.Maximum, origin, dx, dy, dz, nearest)) continue;
                if (node < patch.TriangleLeafBase)
                {
                    pending[pendingCount++] = node * 2 + 1;
                    pending[pendingCount++] = node * 2;
                    continue;
                }
                int begin = (node - patch.TriangleLeafBase) * TerrainTriangleGeometry.TrianglesPerLeaf * 3;
                int end = begin + Math.Min(TerrainTriangleGeometry.TrianglesPerLeaf * 3, indices.Length - begin);
                for (int index = begin; index < end; index += 3)
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
        }
        return found;
    }

    private static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);

    private static bool IntersectsBounds(Vector3 lower, Vector3 upper, Vector3 origin,
        double dx, double dy, double dz, double maximum)
    {
        double minimum = 0;
        return Axis(origin.X, dx, lower.X, upper.X) &&
            Axis(origin.Y, dy, lower.Y, upper.Y) && Axis(origin.Z, dz, lower.Z, upper.Z);
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
