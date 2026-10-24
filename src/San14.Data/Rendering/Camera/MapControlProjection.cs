using System.Numerics;
using F = San14.Data.UnitWorldGroundHeightFloat;

namespace San14.Data;

public readonly record struct MapControlScreenPoint(int X, int Y);
public readonly record struct MapControlSurfaceExtent(uint Width, uint Height);

/// <summary>Source map-control float4 projection. The caller supplies the
/// current camera, viewport transform, clip selection and optional application
/// surface. These values belong to the same accepted presentation.</summary>
/// <remarks>A null camera or surface denotes known absence. Missing knowledge
/// must be retained by the owner instead of being passed as known absence.</remarks>
public static class MapControlProjection
{
    public static MapControlScreenPoint? Project(Vector4 unitPosition, Vector3 worldOffset,
        Matrix4x4? currentCamera, Matrix4x4 viewport, bool clipBypass,
        MapControlSurfaceExtent? applicationSurface)
    {
        if (currentCamera is not { } camera) return null;
        var p = new Vector4(F.Add(unitPosition.X, worldOffset.X),
            F.Add(unitPosition.Y, worldOffset.Y), F.Add(unitPosition.Z, worldOffset.Z), unitPosition.W);
        if (!clipBypass)
        {
            // The clip gate is a separate transform with Y-first sums in
            // all four columns. NaN fails the strict ordered comparisons.
            float reciprocal = F.Divide(1f, Column(p,camera.M14,camera.M24,camera.M34,camera.M44,true));
            float x = F.Multiply(Column(p,camera.M11,camera.M21,camera.M31,camera.M41,true),reciprocal);
            float y = F.Multiply(Column(p,camera.M12,camera.M22,camera.M32,camera.M42,true),reciprocal);
            float z = F.Multiply(Column(p,camera.M13,camera.M23,camera.M33,camera.M43,true),reciprocal);
            if (!(x > -1f && x < 1f && y > -1f && y < 1f && z > 0f && z < 1f)) return null;
        }
        // Projection repeats the source transform: X sums Y first; YZW
        // sum X first. Normalize every component before viewport products.
        float w = Column(p,camera.M14,camera.M24,camera.M34,camera.M44,false);
        float inverse = F.Divide(1f,w);
        var normalized = new Vector4(
            F.Multiply(Column(p,camera.M11,camera.M21,camera.M31,camera.M41,true),inverse),
            F.Multiply(Column(p,camera.M12,camera.M22,camera.M32,camera.M42,false),inverse),
            F.Multiply(Column(p,camera.M13,camera.M23,camera.M33,camera.M43,false),inverse),
            F.Multiply(w,inverse));
        int px = BattleUnitSourceMath.TruncateSingleToInt32(Column(normalized,
            viewport.M11,viewport.M21,viewport.M31,viewport.M41,true));
        int py = BattleUnitSourceMath.TruncateSingleToInt32(Column(normalized,
            viewport.M12,viewport.M22,viewport.M32,viewport.M42,true));
        if (applicationSurface is { } surface)
        {
            if (surface.Width == 0 || surface.Height == 0)
                throw new InvalidDataException("Current map-control surface has zero extent.");
            px = unchecked((int)(unchecked((uint)(px * 1920)) / surface.Width));
            py = unchecked((int)(unchecked((uint)(py * 1080)) / surface.Height));
        }
        return new(px,py);
    }

    private static float Column(Vector4 p,float a,float b,float c,float d,bool yFirst)
    {
        float x=F.Multiply(p.X,a),y=F.Multiply(p.Y,b);
        return F.Add(F.Add(yFirst?F.Add(y,x):F.Add(x,y),F.Multiply(p.Z,c)),F.Multiply(p.W,d));
    }
}
