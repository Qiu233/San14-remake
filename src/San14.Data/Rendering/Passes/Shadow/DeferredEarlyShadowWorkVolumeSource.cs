using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>One CF4C90/D19B10 bounded work-volume matrix before the actual
/// ShadowCPU130 inventory publication. This does not infer its count/capacity.</summary>
public readonly record struct DeferredEarlyShadowWorkVolumeNumericFields(
    Vector4 ViewMinimum, Vector4 ViewMaximum, Matrix4x4 ViewToWorld, Matrix4x4 WorkMatrix)
{
    public void WriteWorkMatrix(Span<byte> actualOwnedMatrixSlot)
    {
        if (actualOwnedMatrixSlot.Length != 64)
            throw new ArgumentException("Shadow work volume requires its actual64-byte matrix slot.", nameof(actualOwnedMatrixSlot));
        float[] values = [WorkMatrix.M11,WorkMatrix.M12,WorkMatrix.M13,WorkMatrix.M14,
            WorkMatrix.M21,WorkMatrix.M22,WorkMatrix.M23,WorkMatrix.M24,
            WorkMatrix.M31,WorkMatrix.M32,WorkMatrix.M33,WorkMatrix.M34,
            WorkMatrix.M41,WorkMatrix.M42,WorkMatrix.M43,WorkMatrix.M44];
        for (int i = 0; i < 16; i++)
            BinaryPrimitives.WriteSingleLittleEndian(actualOwnedMatrixSlot[(i * 4)..], values[i]);
    }
}

public sealed class DeferredEarlyShadowCurrentWorkVolumeFields
{
    internal DeferredEarlyShadowCurrentWorkVolumeFields(DeferredEarlyShadowCurrentOwnCameraFields ownCamera,
        DeferredEarlyShadowWorkVolumeNumericFields numeric) => (OwnCamera, Numeric) = (ownCamera, numeric);
    public DeferredEarlyShadowCurrentOwnCameraFields OwnCamera { get; }
    public DeferredEarlyShadowWorkVolumeNumericFields Numeric { get; }
    public string SourceEvidence => OwnCamera.SourceEvidence + ";TC:CF4E5E..CF4F73-localView80->rigid3F66E0/" +
        "lens-near140/far9EC/FOV148/aspect14C;epsilon01343B98=1;CF4F84..CF4F9A->D19B10-work-matrix;" +
        "finite-numeric-volume-not-work130-pointer/count-publication";
}

/// <summary>The original own-camera work box that compact1061760/A06350
/// consumes. Its basis inverse is the original rigid transpose, not a general
/// determinant inverse. The actual130 inventory and record7C gate stay with
/// the source frontend that publishes this matrix.</summary>
public static class DeferredEarlyShadowWorkVolumeSource
{
    public static DeferredEarlyShadowCurrentWorkVolumeFields PrepareCurrent(
        DeferredEarlyShadowCurrentOwnCameraFields actualOwnCamera,
        NativeCameraTangentDispatch tangentDispatch)
    {
        ArgumentNullException.ThrowIfNull(actualOwnCamera);
        var main = actualOwnCamera.Main;
        DeferredEarlyShadowCurrentMainFields.RequireActualMain(main.ActualMainInputs, main.PreparedMain);
        var prefix = actualOwnCamera.Numeric.OwnCameraPrefix150.Span;
        float half = NativeFloat.Multiply(BinaryPrimitives.ReadSingleLittleEndian(prefix[0x138..]), .5f);
        float tangent = NativeCameraTangent.Tangent1020A90(half, tangentDispatch);
        return new(actualOwnCamera, PrepareNumericFieldsFromTangent(prefix, tangent));
    }

    public static DeferredEarlyShadowWorkVolumeNumericFields PrepareNumericFieldsFromTangent(
        ReadOnlySpan<byte> actualOwnCameraPrefix150, float nativeTangentOfHalfFov)
    {
        if (actualOwnCameraPrefix150.Length != 0x150)
            throw new ArgumentException("Shadow work volume requires its actual stripped150-byte own Camera.", nameof(actualOwnCameraPrefix150));
        float near = BinaryPrimitives.ReadSingleLittleEndian(actualOwnCameraPrefix150[0x130..]);
        float far = BinaryPrimitives.ReadSingleLittleEndian(actualOwnCameraPrefix150[0x134..]);
        float aspect = BinaryPrimitives.ReadSingleLittleEndian(actualOwnCameraPrefix150[0x13C..]);
        float halfHeight = M(nativeTangentOfHalfFov, far), halfWidth = M(aspect, halfHeight);
        // The source constant01343B98 is float32 1, not a fitted epsilon.
        Vector4 minimum = new(S(Neg(halfWidth), 1), S(Neg(halfHeight), 1), Neg(far), 1);
        Vector4 maximum = new(A(halfWidth, 1), A(halfHeight, 1), Neg(near), 1);
        Matrix4x4 view = ReadMatrix(actualOwnCameraPrefix150, 0x70);
        Matrix4x4 inverse = UnitWorldParentMath.InverseRigid3F6D20(view);
        return new(minimum, maximum, inverse, BoundedMatrixD19B10(minimum, maximum, inverse));
    }

    /// <summary>Complete nonnull orientation branch of D19B10. Row products
    /// and translation dot grouping follow its scalar SSE stores.</summary>
    public static Matrix4x4 BoundedMatrixD19B10(Vector4 minimum, Vector4 maximum, Matrix4x4 orientation)
    {
        float hx = M(S(maximum.X, minimum.X), .5f), hy = M(S(maximum.Y, minimum.Y), .5f),
            hz = M(S(maximum.Z, minimum.Z), .5f);
        float cx = A(hx, minimum.X), cy = A(hy, minimum.Y), cz = A(hz, minimum.Z);
        float tx = A(A(A(M(cy, orientation.M21), M(cx, orientation.M11)), M(cz, orientation.M31)), orientation.M41);
        float ty = A(A(A(M(cy, orientation.M22), M(cx, orientation.M12)), M(cz, orientation.M32)), orientation.M42);
        float tz = A(A(A(M(cy, orientation.M23), M(cx, orientation.M13)), M(cz, orientation.M33)), orientation.M43);
        return new(M(hx,orientation.M11),M(hx,orientation.M12),M(hx,orientation.M13),M(hx,orientation.M14),
            M(hy,orientation.M21),M(hy,orientation.M22),M(hy,orientation.M23),M(hy,orientation.M24),
            M(hz,orientation.M31),M(hz,orientation.M32),M(hz,orientation.M33),M(hz,orientation.M34),tx,ty,tz,1);
    }

    private static Matrix4x4 ReadMatrix(ReadOnlySpan<byte> bytes, int at)
    {
        float[] values = new float[16];
        for (int i = 0; i < 16; i++) values[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes[(at + i * 4)..]);
        return new(values[0],values[1],values[2],values[3],values[4],values[5],values[6],values[7],
            values[8],values[9],values[10],values[11],values[12],values[13],values[14],values[15]);
    }
    private static float Neg(float value) => BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(value) ^ 0x80000000u);
    private static float M(float a, float b) => NativeFloat.Multiply(a,b);
    private static float A(float a, float b) => NativeFloat.Add(a,b);
    private static float S(float a, float b) => NativeFloat.Subtract(a,b);
}
