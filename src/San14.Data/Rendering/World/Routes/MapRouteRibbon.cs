using System.Numerics;
using System.Runtime.InteropServices;
using F = San14.Data.UnitWorldGroundHeightFloat;

namespace San14.Data;

/// <summary>The source route strip's position/UV record. The renderer consumes
/// the pairs as a triangle strip with a twenty-byte vertex stride.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct MapRouteRibbonVertex(float X, float Y, float Z, float U, float V);

/// <summary>Expands an already sampled, terrain-grounded spine into its source
/// ribbon. Path search, surface trimming and corner/cap decoration belong to
/// the caller; camera changes do not require this geometry to be rebuilt.</summary>
public sealed class MapRouteRibbon
{
    private readonly MapRouteRibbonVertex[] vertices;
    public ReadOnlyMemory<MapRouteRibbonVertex> Vertices => vertices;
    private MapRouteRibbon(MapRouteRibbonVertex[] vertices) => this.vertices = vertices;

    public static MapRouteRibbon Build(ReadOnlySpan<Vector3> spine, float fullWidth)
    {
        if (!float.IsFinite(fullWidth) || fullWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(fullWidth));
        if (spine.Length < 2) return new([]);
        var output = new MapRouteRibbonVertex[checked(spine.Length * 2)];
        var (sin, cos) = ArmyAnimationSampler.SourceSinCos(BitConverter.Int32BitsToSingle(0x3FC90FDB));
        float dx = 0, dy = 0, dz = 0;
        for (int i = 0; i < spine.Length; i++)
        {
            Vector3 point = spine[i];
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
                throw new InvalidDataException("route.ribbon.nonfinite_spine");
            if (i + 1 < spine.Length)
            {
                float x = F.Subtract(spine[i + 1].X, point.X);
                float z = F.Subtract(spine[i + 1].Z, point.Z);
                float length = Root(F.Add(F.Add(F.Multiply(x, x), 0f), F.Multiply(z, z)));
                if (!(length > 0) || !float.IsFinite(length))
                    throw new InvalidDataException("route.ribbon.degenerate_spine_span");
                float reciprocal = F.Divide(1f, length);
                dx = F.Multiply(x, reciprocal); dy = F.Multiply(reciprocal, 0f); dz = F.Multiply(z, reciprocal);
            }
            // The last pair retains the preceding forward direction. Preserve
            // the source scalar rotation/normalization, including signed zero.
            float sideX = F.Add(F.Multiply(dz, sin), F.Multiply(dx, cos));
            float sideZ = F.Subtract(F.Multiply(dz, cos), F.Multiply(dx, sin));
            float inverse = F.Divide(1f, Root(F.Add(F.Add(F.Multiply(sideX, sideX), F.Multiply(dy, dy)),
                F.Multiply(sideZ, sideZ))));
            float v = (i & 1) == 0 ? 0f : 1f;
            output[i * 2] = Expand(F.Multiply(fullWidth, .5f), 0f);
            output[i * 2 + 1] = Expand(F.Multiply(fullWidth, -.5f), 1f);

            MapRouteRibbonVertex Expand(float half, float u) => new(
                F.Add(F.Multiply(F.Multiply(sideX, inverse), half), point.X),
                F.Add(F.Multiply(F.Multiply(inverse, dy), half), point.Y),
                F.Add(F.Multiply(F.Multiply(sideZ, inverse), half), point.Z), u, v);
        }
        return new(output);
    }

    private static float Root(float value) => (float)Math.Sqrt(value);
}
