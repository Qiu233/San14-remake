using System.Numerics;

namespace San14.Data;

/// <summary>Ordinary facility placement. Hex operands are retained independently
/// of the optional renderer Army-view positions used by other feedback.</summary>
public static class FacilityEffectGeometry
{
    private const float Epsilon = 1.1920928955078125e-5f;

    public static Vector3 HexPosition(int hexOperand, MainMapCameraTerrainSource terrain)
    {
        if ((uint)hexOperand > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(hexOperand));
        int column = hexOperand % 220, row = hexOperand / 220;
        float root = MathF.Sqrt(3f);
        float x = column * 702f + 37594f;
        float z = root * 468f * row;
        z += column % 2 == 0 ? root * 468f * .5f : 0;
        z += root * .5f * 468f + 12930f;
        float y = 0;
        if (hexOperand < 48400)
        {
            float ground = terrain.Query(x, z);
            y = 445f <= ground ? ground : 445f;
        }
        return new(x, y, z);
    }

    public static Matrix4x4 Projectile(Vector3 source, Vector3 target, int definition)
    {
        float factor = definition switch
        {
            5 or 15 => 1f / (MathF.Sqrt(3f) * 468f),
            6 => 0.0005341880605556071f,
            _ => throw new ArgumentOutOfRangeException(nameof(definition))
        };
        Vector3 delta = target - source;
        float distance = Length(delta);
        if (MathF.Abs(distance) < Epsilon) delta = new(1, 0, 0);
        float scale = distance * factor;
        if (definition is 5 or 15) source.Y += scale * 500f;
        float originLength = Length(source);
        Matrix4x4 transform = Matrix4x4.Identity;
        //37E3B0's near-zero origin branch keeps identity without scaling.
        if (Epsilon <= MathF.Abs(originLength))
        {
            var direction = delta * (1f / originLength);
            var forward = direction * (1f / Length(direction));
            var up = new Vector3(0, 1, 0);
            if (!(Epsilon <= forward.X || forward.X <= -Epsilon ||
                  Epsilon <= forward.Z || forward.Z <= -Epsilon)) up = new(0, 0, -forward.Y);
            var right = Vector3.Cross(up, forward);
            right *= 1f / Length(right);
            var second = Vector3.Cross(forward, right);
            var basis = new Matrix4x4(right.X, right.Y, right.Z, 0,
                second.X, second.Y, second.Z, 0, forward.X, forward.Y, forward.Z, 0, 0, 0, 0, 1);
            transform = UnitWorldTransformMath.ComposeAffine17ADD0(basis, Matrix4x4.CreateScale(scale));
        }
        transform.M41 = source.X; transform.M42 = source.Y; transform.M43 = source.Z; transform.M44 = 1;
        return transform;
    }

    public static Vector3 StatisticOffset(Vector3 origin, int dimension)
    {
        if ((uint)dimension > 4) throw new ArgumentOutOfRangeException(nameof(dimension));
        float angle = dimension * 72f * 0.017453292f;
        return origin + new Vector3(MathF.Cos(angle) * 234f, 250f, -MathF.Sin(angle) * 234f);
    }

    private static float Length(Vector3 point) => MathF.Sqrt((point.X * point.X + point.Y * point.Y) + point.Z * point.Z);
}
