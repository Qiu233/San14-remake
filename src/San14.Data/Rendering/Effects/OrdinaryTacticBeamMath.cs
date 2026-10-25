using System.Numerics;

namespace San14.Data;

/// <summary>Ordinary tactic placement from actual renderer view positions.
/// This has no map lookup: a Hex center is not a substitute for a view.</summary>
public static class OrdinaryTacticBeamMath
{
    private const float Epsilon = 1.1920928955078125e-5f;

    /// <summary>172910/17A760 test the unraised primary view position first.</summary>
    public static bool HasOrigin(Vector4 position)
        => Epsilon <= MathF.Abs(Length(position.X, position.Y, position.Z));

    /// <summary>16E340 followed by3F7490 and17B070. Both endpoints are raised
    /// by200; the beam's uniform scale is endpoint distance/6000. The fourth
    /// row retains the primary view's W. Coincident endpoints retain the
    /// source's degenerate result; allocation has its own transform gate.</summary>
    public static bool TryCreate(Vector4 primaryViewPosition, Vector4 linkedViewPosition,
        out Matrix4x4 transform)
    {
        var origin = primaryViewPosition;
        origin.Y += 200f;
        float originLength = Length(origin.X, origin.Y, origin.Z);
        if (!(Epsilon <= MathF.Abs(originLength)))
        {
            transform = default;
            return false;
        }

        float dx = linkedViewPosition.X - origin.X;
        float dy = (linkedViewPosition.Y + 200f) - origin.Y;
        float dz = linkedViewPosition.Z - origin.Z;
        float distance = Length(dx, dy, dz);
        float scale = distance / 6000f;
        float inverseDistance = 1f / distance;
        dx *= inverseDistance;
        dy *= inverseDistance;
        dz *= inverseDistance;
        float inverseOriginLength = 1f / originLength;
        dx *= inverseOriginLength;
        dy *= inverseOriginLength;
        dz *= inverseOriginLength;

        //3F7490 normalizes again. Retain the original scalar operations,
        //including the alternate up vector for a vertical direction.
        float inverseDirectionLength = 1f / Length(dx, dy, dz);
        float fx = dx * inverseDirectionLength;
        float fy = dy * inverseDirectionLength;
        float fz = dz * inverseDirectionLength;
        float ux = 0f, uy = 1f, uz = 0f;
        if (!(Epsilon <= fx || fx <= -Epsilon || Epsilon <= fz || fz <= -Epsilon))
        {
            uy = 0f;
            uz = -fy;
        }
        float rx = fz * uy - fy * uz;
        float ry = fx * uz - fz * ux;
        float rz = fy * ux - fx * uy;
        float inverseRightLength = 1f / Length(rx, ry, rz);
        rx *= inverseRightLength;
        rz *= inverseRightLength;
        ry *= inverseRightLength;
        var basis = new Matrix4x4(
            rx, ry, rz, 0f,
            rz * fy - ry * fz, rx * fz - rz * fx, ry * fx - rx * fy, 0f,
            fx, fy, fz, 0f,
            0f, 0f, 0f, 1f);
        var scaling = new Matrix4x4(
            scale, 0f, 0f, 0f,
            0f, scale, 0f, 0f,
            0f, 0f, scale, 0f,
            0f, 0f, 0f, 1f);
        transform = UnitWorldTransformMath.ComposeAffine17ADD0(basis, scaling);
        transform.M41 = origin.X;
        transform.M42 = origin.Y;
        transform.M43 = origin.Z;
        transform.M44 = origin.W;
        return true;
    }

    /// <summary>37E940's separately rounded upper-left3x3 determinant.
    /// Actual effect allocation remains the native effect owner's operation.</summary>
    public static float TransformDeterminant(Matrix4x4 value)
    {
        float first = (value.M12 * value.M23 - value.M13 * value.M22) * value.M31;
        float second = (value.M21 * value.M13 - value.M11 * value.M23) * value.M32;
        float third = (value.M11 * value.M22 - value.M21 * value.M12) * value.M33;
        return (second + first) + third;
    }

    /// <summary>37E920 and37E8A0 partition the handle's unallocated/allocated
    /// object state. Their union accepts either state with an actual kind&lt;137.</summary>
    public static bool IsValidHandle(bool hasHandle, bool reservedSlotValid, bool allocatedObjectSlotValid)
        => hasHandle && (reservedSlotValid || allocatedObjectSlotValid);

    /// <summary>37E1B0's candidate-slot gate. Passing it only authorizes an
    /// allocation attempt; it does not mean that a native object was allocated.
    /// EffectKind and LifecycleFlags come from the actual shared effect owner.</summary>
    public static bool CanReuseManagerSlot(int effectKind, uint lifecycleFlags)
        => (lifecycleFlags & 2u) == 0 && unchecked((uint)effectKind) > 136u && (lifecycleFlags & 4u) == 0;

    /// <summary>The normal effect_info row controls quality and which existing
    /// manager pool is used. It is not a separate tactic-local pool.</summary>
    public static bool AcceptsQuality(G1EffectInfoRow declaration, float sourceQuality)
        => declaration.AcceptsQuality(sourceQuality);

    /// <summary>17A760 follows only a native-valid linked Army. Base actors
    /// and affected result targets do not keep a tactic node alive.</summary>
    public static bool ShouldFollow(Vector4 primaryViewPosition, OrdinaryTacticViewIdentity linkedActor,
        bool linkedActorNativeValid, bool hasHandle, bool reservedSlotValid, bool allocatedObjectSlotValid)
        => HasOrigin(primaryViewPosition) && linkedActor.Kind == OrdinaryTacticViewKind.Army &&
            linkedActorNativeValid && IsValidHandle(hasHandle, reservedSlotValid, allocatedObjectSlotValid);

    private static float Length(float x, float y, float z)
    {
        float xy = x * x + y * y;
        return MathF.Sqrt(xy + z * z);
    }
}
