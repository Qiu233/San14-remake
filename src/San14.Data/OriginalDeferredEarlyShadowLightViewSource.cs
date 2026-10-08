using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>The actual A06350/A07370 directional light-space view before
/// frustum intersection/effective-pass publication and final projection.</summary>
public readonly record struct OriginalDeferredEarlyShadowLightViewNumericFields(
    Vector4 SelectedUp, Matrix4x4 LightView, Matrix4x4 LightToWorldRigid);

public sealed class OriginalDeferredEarlyShadowCurrentLightViewFields
{
    internal OriginalDeferredEarlyShadowCurrentLightViewFields(OriginalDeferredEarlyShadowCurrentOwnCameraFields ownCamera,
        OriginalDeferredEarlyShadowLightViewNumericFields numeric) => (OwnCamera, Numeric) = (ownCamera, numeric);
    public OriginalDeferredEarlyShadowCurrentOwnCameraFields OwnCamera { get; }
    public OriginalDeferredEarlyShadowLightViewNumericFields Numeric { get; }
    public string SourceEvidence => OwnCamera.SourceEvidence + ";TC:A063D8..A06478-actual-own-direction160/up-014C4D00/01394900;" +
        "A07370-raw-direction/light-view-at-own-eye;3F66E0-own-rigid-inverse;finite-before-BC4-effective/final5D0";
}

public static class OriginalDeferredEarlyShadowLightViewSource
{
    public static OriginalDeferredEarlyShadowCurrentLightViewFields PrepareCurrent(
        OriginalDeferredEarlyShadowCurrentOwnCameraFields actualOwnCamera)
    {
        ArgumentNullException.ThrowIfNull(actualOwnCamera);
        var main = actualOwnCamera.Main;
        OriginalDeferredEarlyShadowCurrentMainFields.RequireActualMain(main.ActualMainInputs, main.PreparedMain);
        var prefix = actualOwnCamera.Numeric.OwnCameraPrefix150.Span;
        Vector4 eye = new(BinaryPrimitives.ReadSingleLittleEndian(prefix), BinaryPrimitives.ReadSingleLittleEndian(prefix[4..]),
            BinaryPrimitives.ReadSingleLittleEndian(prefix[8..]), BinaryPrimitives.ReadSingleLittleEndian(prefix[12..]));
        var numeric = PrepareNumericFields(eye, actualOwnCamera.Direction.Numeric.Camera160);
        return new(actualOwnCamera, numeric);
    }

    /// <summary>Actual A06350 up selector and complete A07370 matrix. Raw
    /// source light direction is retained as the third view column.</summary>
    public static OriginalDeferredEarlyShadowLightViewNumericFields PrepareNumericFields(Vector4 actualOwnEye,
        Vector4 actualOwnDirection160)
    {
        float yDot = A(A(M(actualOwnDirection160.X, 0), actualOwnDirection160.Y), M(actualOwnDirection160.Z, 0));
        float abs = MathF.Abs(yDot);
        float low = BitConverter.UInt32BitsToSingle(0x3F7FB1E0), high = BitConverter.UInt32BitsToSingle(0x3F802710);
        Vector4 up = abs >= low && abs <= high ? new(0,0,1,0) : new(0,1,0,0);
        Matrix4x4 view = LightViewA07370(actualOwnEye, actualOwnDirection160, up);
        return new(up, view, OriginalUnitWorldParentMath.InverseRigid3F6D20(view));
    }

    public static Matrix4x4 LightViewA07370(Vector4 eye, Vector4 direction, Vector4 up)
    {
        float rx = S(M(up.Y, direction.Z), M(up.Z, direction.Y));
        float ry = S(M(direction.X, up.Z), M(up.X, direction.Z));
        float rz = S(M(up.X, direction.Y), M(direction.X, up.Y));
        float inverseRight = D(1, MathF.Sqrt(A(A(M(ry,ry), M(rx,rx)), M(rz,rz))));
        rx = M(rx,inverseRight); ry = M(ry,inverseRight); rz = M(rz,inverseRight);
        float ux = S(M(direction.Y,rz), M(direction.Z,ry));
        float uy = S(M(direction.Z,rx), M(direction.X,rz));
        float uz = S(M(direction.X,ry), M(direction.Y,rx));
        float inverseUp = D(1, MathF.Sqrt(A(A(M(uy,uy), M(ux,ux)), M(uz,uz))));
        ux = M(ux,inverseUp); uy = M(uy,inverseUp); uz = M(uz,inverseUp);
        float tx = Neg(A(A(M(ry,eye.Y), M(rx,eye.X)), M(rz,eye.Z)));
        float ty = Neg(A(A(M(uy,eye.Y), M(ux,eye.X)), M(uz,eye.Z)));
        // Native A075A0 accumulates the direction dot X, then Y, then Z.
        float tz = Neg(A(A(M(direction.X,eye.X), M(direction.Y,eye.Y)), M(eye.Z,direction.Z)));
        return new(rx,ux,direction.X,0,ry,uy,direction.Y,0,rz,uz,direction.Z,0,tx,ty,tz,1);
    }
    private static float Neg(float value) => BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(value) ^ 0x80000000u);
    private static float M(float a, float b) => OriginalNativeFloat.Multiply(a,b);
    private static float A(float a, float b) => OriginalNativeFloat.Add(a,b);
    private static float S(float a, float b) => OriginalNativeFloat.Subtract(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a, float b) => a / b;
}
