using System.Numerics;
using F = San14.Data.UnitWorldGroundHeightFloat;

namespace San14.Data;

/// <summary>Page-owned route geometry before ribbon expansion. Corners retain
/// the terminal entry; the original decal consumer draws only Count - 1.</summary>
public sealed class MapRouteSpine
{
    private readonly Vector3[] spine;
    private readonly Vector3[] corners;
    public ReadOnlyMemory<Vector3> Spine => spine;
    public ReadOnlyMemory<Vector3> Corners => corners;
    public Vector3? StartEndpoint { get; }
    public Vector3? EndEndpoint { get; }

    private MapRouteSpine(Vector3[] spine, Vector3[] corners, Vector3? start, Vector3? end)
    { this.spine = spine; this.corners = corners; StartEndpoint = start; EndEndpoint = end; }

    /// <summary>Build the ordinary active-group route from already resolved
    /// source planar centers (Y == 0), endpoint surfaces and the actual camera display band.
    /// The caller supplies its current terrain owner, never interpolated heights.
    /// Unknown centers/surfaces/band/terrain must remain unresolved at that owner;
    /// this method does not supply defaults or resolve Hex identities.</summary>
    public static MapRouteSpine Build(ReadOnlySpan<Vector3> centers, int startSurface,
        int endSurface, int displayBand, Func<float, float, float> terrainHeight)
    {
        ArgumentNullException.ThrowIfNull(terrainHeight);
        if ((uint)startSurface > byte.MaxValue || (uint)endSurface > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(startSurface), "route.spine.endpoint_surface");
        if (centers.Length < 2) return new([], [], null, null);
        Vector3[] points = centers.ToArray();
        foreach (Vector3 point in points)
        {
            RequireFinite(point, "center");
            if (point.Y != 0f) throw new InvalidDataException("route.spine.planar_center_required");
        }
        int count = points.Length;

        // The source's special terminal simplification precedes both trim and
        // endpoint-height publication. It replaces only the penultimate point.
        if (count > 2 && endSurface == 16)
        {
            float previous = LengthXyz(Subtract(points[count - 2], points[count - 3]));
            float last = LengthXyz(Subtract(points[count - 1], points[count - 2]));
            float direct = LengthXyz(Subtract(points[count - 1], points[count - 3]));
            if (direct < F.Add(last, previous))
            { points[count - 2] = points[count - 1]; count--; }
        }
        Vector3 endEndpoint = Ground(points[count - 1]);
        int first = 0;
        bool firstBends = false;
        if (startSurface == 16 && displayBand < 3 && count > 2)
        {
            Vector3 one = Normalize(Subtract(points[1], points[0]), false);
            Vector3 two = Normalize(Subtract(points[2], points[0]), true);
            float dot = F.Add(F.Add(F.Multiply(two.X, one.X), F.Multiply(two.Y, one.Y)),
                F.Multiply(two.Z, one.Z));
            firstBends = !(Abs(F.Subtract(dot, 1f)) < CollinearTolerance);
            first = 1;
        }
        Vector3 startEndpoint = Ground(points[0]);
        if (IsSpecial(startSurface))
        {
            Vector3 delta = Subtract(points[first + 1], points[first]);
            float inverse = F.Divide(1f, LengthYxz(delta));
            float trim = startSurface == 16 ? first == 0 ? 1450f : firstBends ? 850f : 700f : 450f;
            // Normalize before multiplying by trim, as in the actual scalar ASM.
            Vector3 p = points[first];
            points[first] = new(F.Add(F.Multiply(trim, F.Multiply(delta.X, inverse)), p.X), p.Y,
                F.Add(F.Multiply(trim, F.Multiply(delta.Z, inverse)), p.Z));
            RequireFinite(points[first], "start_trim");
        }

        var samples = new List<Vector3> { points[first] };
        var bends = new List<Vector3>();
        for (int begin = first, end = first + 1; end < count; begin = end, end++)
        {
            Vector3 direction = NormalizeUnlessZero(Subtract(points[begin + 1], points[begin]));
            // This replaces only the source loop unrolling, not its comparisons:
            // each following adjacent span is compared with the initial direction.
            while (end + 1 < count)
            {
                Vector3 next = Normalize(Subtract(points[end + 1], points[end]), true);
                float dot = F.Add(F.Add(F.Multiply(next.Y, direction.Y),
                    F.Multiply(next.X, direction.X)), F.Multiply(next.Z, direction.Z));
                if (!(Abs(F.Subtract(dot, 1f)) < CollinearTolerance)) break;
                end++;
            }
            bool bend = end != count - 1;
            if (!bend && IsSpecial(endSurface))
            {
                float trim = endSurface == 16 ? 1450f : 450f;
                Vector3 p = points[end];
                points[end] = new(F.Subtract(p.X, F.Multiply(trim, direction.X)), p.Y,
                    F.Subtract(p.Z, F.Multiply(trim, direction.Z)));
            }
            Vector3 current = points[begin], shortened = points[end];
            if (bend)
            {
                float backoff = Math.Min(LengthXyz(Subtract(shortened, current)), 10f);
                float negative = F.Multiply(backoff, -1f);
                shortened = new(F.Add(shortened.X, F.Multiply(negative, direction.X)),
                    F.Add(shortened.Y, F.Multiply(negative, direction.Y)),
                    F.Add(shortened.Z, F.Multiply(negative, direction.Z)));
            }
            Vector3 shortDelta = Subtract(shortened, current);
            float originalLength = LengthXyz(shortDelta);
            // The second normalization uses Y+X+Z order and normalizes only X/Z.
            // It supplies the quadratic control direction, not the next span.
            Vector3 shortDirection = shortDelta;
            if (shortDelta.X != 0f || shortDelta.Y != 0f || shortDelta.Z != 0f)
            {
                float inverse = F.Divide(1f, LengthYxz(shortDelta));
                shortDirection = new(F.Multiply(shortDelta.X, inverse), shortDelta.Y,
                    F.Multiply(shortDelta.Z, inverse));
            }
            float remaining = originalLength;
            while (remaining > 900f)
            {
                if (originalLength < remaining) break;
                current = new(F.Add(F.Multiply(900f, direction.X), current.X),
                    F.Add(F.Multiply(900f, direction.Y), current.Y),
                    F.Add(F.Multiply(900f, direction.Z), current.Z));
                samples.Add(current);
                remaining = LengthXyz(Subtract(shortened, current));
            }
            samples.Add(shortened);
            bends.Add(points[end]); // Includes final endpoint; independent of samples.
            if (bend)
            {
                float backoff = Math.Min(LengthYxz(Subtract(points[end + 1], points[end])), 10f);
                Vector3 corner = points[end];
                float controlX = F.Add(F.Multiply(backoff, shortDirection.X), corner.X);
                float controlZ = F.Add(F.Multiply(backoff, shortDirection.Z), corner.Z);
                // Ordinary active groups have exactly one subdivision. Keep the
                // original three-term float expression even for t == 1.
                float t = F.Divide(1f, 1f), complement = F.Subtract(1f, t);
                samples.Add(new(Quadratic(corner.X, shortened.X, controlX, t, complement), 0f,
                    Quadratic(corner.Z, shortened.Z, controlZ, t, complement)));
            }
        }
        Vector3 terminal = points[count - 1];
        for (int i = 0; i < samples.Count; i++) samples[i] = GroundResult(samples[i]);
        for (int i = 0; i < bends.Count; i++) bends[i] = GroundResult(bends[i]);
        return new(samples.ToArray(), bends.ToArray(), startEndpoint, endEndpoint);

        Vector3 GroundResult(Vector3 p) => endSurface == 16 && p == terminal
            ? new(p.X, endEndpoint.Y, p.Z) : Ground(p);
        Vector3 Ground(Vector3 p)
        {
            RequireFinite(p, "sample");
            float y = terrainHeight(p.X, p.Z);
            if (!float.IsFinite(y)) throw new InvalidDataException("route.spine.terrain_height_unknown");
            return new(p.X, F.Add(y < 445f ? 445f : y, 200f), p.Z);
        }
    }

    private const float CollinearTolerance = 1.1920929e-05f;
    private static bool IsSpecial(int surface) => surface is >= 15 and <= 18;
    private static float Abs(float value) => value < 0f ? -value : value;
    private static Vector3 Subtract(Vector3 a, Vector3 b) =>
        new(F.Subtract(a.X, b.X), F.Subtract(a.Y, b.Y), F.Subtract(a.Z, b.Z));
    private static float LengthXyz(Vector3 p) => Root(F.Add(F.Add(F.Multiply(p.X, p.X),
        F.Multiply(p.Y, p.Y)), F.Multiply(p.Z, p.Z)));
    private static float LengthYxz(Vector3 p) => Root(F.Add(F.Add(F.Multiply(p.Y, p.Y),
        F.Multiply(p.X, p.X)), F.Multiply(p.Z, p.Z)));
    private static float Root(float value) => (float)Math.Sqrt(value);
    private static Vector3 Normalize(Vector3 p, bool yFirst)
    {
        float inverse = F.Divide(1f, yFirst ? LengthYxz(p) : LengthXyz(p));
        return new(F.Multiply(p.X, inverse), F.Multiply(p.Y, inverse), F.Multiply(p.Z, inverse));
    }
    private static Vector3 NormalizeUnlessZero(Vector3 p) =>
        p.X != 0f || p.Y != 0f || p.Z != 0f ? Normalize(p, true) : p;
    private static float Quadratic(float corner, float shortened, float control, float t, float complement) =>
        F.Add(F.Add(F.Multiply(F.Multiply(F.Multiply(corner, 2f), t), complement),
            F.Multiply(F.Multiply(complement, shortened), complement)),
            F.Multiply(F.Multiply(control, t), t));
    private static void RequireFinite(Vector3 value, string field)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            throw new InvalidDataException($"route.spine.{field}_unknown");
    }
}
