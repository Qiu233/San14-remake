using System.Numerics;
using F = San14.Data.UnitWorldGroundHeightFloat;

namespace San14.Data;

public readonly record struct DeploymentTargetScreenPoint(int X, int Y);

/// <summary>The ordinary clipped 00F430 projection with the current native
/// surface's fixed 1920x1080 viewport. Returns the integer canonical pixel,
/// before menu-edge writers and application Control scaling.</summary>
public static class DeploymentTargetProjection
{
    public static DeploymentTargetScreenPoint? ProjectCanonical1920(Vector3 menuWorld,
        Matrix4x4 worldToProjection)
    {
        float x = menuWorld.X, y = menuWorld.Y, z = menuWorld.Z;
        float Column(float a, float b, float c, float d) =>
            F.Add(F.Add(F.Add(F.Multiply(b, y), F.Multiply(a, x)), F.Multiply(c, z)), F.Multiply(d, 1f));
        float w = Column(worldToProjection.M14, worldToProjection.M24, worldToProjection.M34, worldToProjection.M44);
        float reciprocal = F.Divide(1f, w);
        float nx = F.Multiply(reciprocal, Column(worldToProjection.M11, worldToProjection.M21, worldToProjection.M31, worldToProjection.M41));
        float ny = F.Multiply(reciprocal, Column(worldToProjection.M12, worldToProjection.M22, worldToProjection.M32, worldToProjection.M42));
        float nz = F.Multiply(reciprocal, Column(worldToProjection.M13, worldToProjection.M23, worldToProjection.M33, worldToProjection.M43));
        if (!(nx > -1f && nx < 1f && ny > -1f && ny < 1f && nz > 0f && nz < 1f)) return null;
        float nw = F.Multiply(reciprocal, w);
        // Source viewport transform preserves the individual zero products,
        // the X/Y accumulation orders and computed homogeneous W.
        float px = F.Add(F.Add(F.Add(F.Multiply(ny, 0f), F.Multiply(nx, 960f)), F.Multiply(nz, 0f)), F.Multiply(nw, 960f));
        float py = F.Add(F.Add(F.Add(F.Multiply(nx, 0f), F.Multiply(ny, -540f)), F.Multiply(nz, 0f)), F.Multiply(nw, 540f));
        return new(BattleUnitSourceMath.TruncateSingleToInt32(px), BattleUnitSourceMath.TruncateSingleToInt32(py));
    }
}
