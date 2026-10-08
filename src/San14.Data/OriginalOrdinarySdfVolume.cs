using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>The actual28 bytes copied from ModelCPU SDFD0: three unsigned volume
/// dimensions, one uniform cell scale, then the three description offsets.</summary>
public readonly record struct OriginalOrdinarySdfVolumeHeader(
    uint Width0, uint Height4, uint Depth8, float CellScaleC, Vector3 Offset10)
{
    public static OriginalOrdinarySdfVolumeHeader ReadActual(ReadOnlySpan<byte> actualHeader28)
    {
        if (actualHeader28.Length != 28) throw new ArgumentException("Model SDFD0 description requires its actual28-byte header.");
        return new(BinaryPrimitives.ReadUInt32LittleEndian(actualHeader28),
            BinaryPrimitives.ReadUInt32LittleEndian(actualHeader28[4..]), BinaryPrimitives.ReadUInt32LittleEndian(actualHeader28[8..]),
            BinaryPrimitives.ReadSingleLittleEndian(actualHeader28[12..]), new(
                BinaryPrimitives.ReadSingleLittleEndian(actualHeader28[16..]), BinaryPrimitives.ReadSingleLittleEndian(actualHeader28[20..]),
                BinaryPrimitives.ReadSingleLittleEndian(actualHeader28[24..])));
    }
}

/// <summary>Actual current World getters. A disabled138 gate does not read90;
/// the source uses its literal scale1 in that branch.</summary>
public readonly record struct OriginalOrdinarySdfVolumeTransform(
    Vector4 Translation80, Quaternion Quaternion88, bool ScaleEnabled138, Vector4? Scale90);

/// <summary>The source80B oriented box used by the actual selected View culler.
/// Center0/Basis10/20/30/Extent40 are separate from the op1 output record48.</summary>
public readonly record struct OriginalOrdinarySdfVolumeViewBox(
    Vector4 Center0, Vector4 Basis10, Vector4 Basis20, Vector4 Basis30, Vector4 HalfExtent40);

/// <summary>Source written payload. Resource20 remains the actual typed owned
/// identity; it is not encoded as a declaration hash or invented pointer.
/// Source metadata has64 written matrix bytes plus a resource reference, and
/// eight unwritten tail bytes which this semantic interface does not invent.</summary>
public sealed record OriginalOrdinarySdfVolumeDescriptor(OriginalOrdinarySdfVolumeViewBox ViewBox,
    Vector4 RecordCenter0, Vector4 RecordExtent10, Quaternion RecordQuaternion20,
    Matrix4x4 MetadataMatrix0, object? ActualVolumeResource20)
{
    public byte[] WriteSourceRecord48()
    {
        byte[] bytes = new byte[48];
        OriginalOrdinarySdfOperation0Bank.WriteVector(bytes, RecordCenter0);
        OriginalOrdinarySdfOperation0Bank.WriteVector(bytes.AsSpan(16), RecordExtent10);
        OriginalOrdinarySdfOperation0Bank.WriteVector(bytes.AsSpan(32), new(RecordQuaternion20.X,
            RecordQuaternion20.Y, RecordQuaternion20.Z, RecordQuaternion20.W)); return bytes;
    }
    public byte[] WriteSourceMetadataMatrix64()
    {
        byte[] bytes = new byte[64]; var m = MetadataMatrix0;
        OriginalOrdinarySdfOperation0Bank.WriteVector(bytes, new(m.M11,m.M12,m.M13,m.M14));
        OriginalOrdinarySdfOperation0Bank.WriteVector(bytes.AsSpan(16),new(m.M21,m.M22,m.M23,m.M24));
        OriginalOrdinarySdfOperation0Bank.WriteVector(bytes.AsSpan(32),new(m.M31,m.M32,m.M33,m.M34));
        OriginalOrdinarySdfOperation0Bank.WriteVector(bytes.AsSpan(48),new(m.M41,m.M42,m.M43,m.M44)); return bytes;
    }
}

public static class OriginalOrdinarySdfVolumeMath
{
    /// <summary>D69D70's current numeric producer before/after the external
    /// View cull. This describes a candidate; it does not perform that cull or
    /// register an op1 volume merely because Model SDFD0 is present.</summary>
    public static OriginalOrdinarySdfVolumeDescriptor DescribeCurrent(
        OriginalOrdinarySdfVolumeHeader header, OriginalOrdinarySdfVolumeTransform actualWorld,
        object? actualVolumeResource20)
    {
        //1092F00 converts zero-extended DWORD dimensions through signed64 CVT.
        Vector3 max = new(A(M(header.CellScaleC, (float)header.Width0), header.Offset10.X),
            A(M(header.CellScaleC, (float)header.Height4), header.Offset10.Y),
            A(M(header.CellScaleC, (float)header.Depth8), header.Offset10.Z));
        Vector3 min = header.Offset10;
        // A12530's UCOMISS/JNE also takes the subtraction branch for unordered.
        const float epsilon = 0.0011920928955078125f; // actualTC1343B60 bits3A9C4000
        if (!(min.X < max.X || min.X > max.X)) min.X = S(min.X, epsilon);
        if (!(min.Y < max.Y || min.Y > max.Y)) min.Y = S(min.Y, epsilon);
        if (!(min.Z < max.Z || min.Z > max.Z)) min.Z = S(min.Z, epsilon);
        Vector4 localCenter = new(M(A(max.X,min.X),.5f), M(A(max.Y,min.Y),.5f), M(A(max.Z,min.Z),.5f),0);
        Vector4 extent = new(S(max.X,localCenter.X), S(max.Y,localCenter.Y), S(max.Z,localCenter.Z),0);
        Vector4 scale = actualWorld.ScaleEnabled138
            ? actualWorld.Scale90 ?? throw new ArgumentException("Enabled source World138 requires its actual scale90 getter.")
            : Vector4.One;
        extent = new(M(extent.X,scale.X),M(extent.Y,scale.Y),M(extent.Z,scale.Z),extent.W);
        //992AE0 uses the rawQ; A12330 copies the three rows and clears rowW.
        Matrix4x4 basis = OriginalUnitWorldTransformMath.QuaternionPositionA12C80(actualWorld.Quaternion88,Vector3.Zero);
        Vector4 q = new(actualWorld.Quaternion88.X,actualWorld.Quaternion88.Y,actualWorld.Quaternion88.Z,actualWorld.Quaternion88.W);
        Vector4 rotatedCenter = OriginalOrdinarySdfMath.RotateQuaternion16700(q,localCenter);
        Vector4 center = new(A(rotatedCenter.X,actualWorld.Translation80.X), A(rotatedCenter.Y,actualWorld.Translation80.Y),
            A(rotatedCenter.Z,actualWorld.Translation80.Z),rotatedCenter.W); // caller forced translationW0
        var box = new OriginalOrdinarySdfVolumeViewBox(center,
            new(basis.M11,basis.M12,basis.M13,0),new(basis.M21,basis.M22,basis.M23,0),new(basis.M31,basis.M32,basis.M33,0),extent);
        Quaternion extracted = OriginalMainMapCameraController.QuaternionFromBasis12A70(basis);
        // A10D40 grouping is (Y²+X²)+Z²+W² before SQRTSS and reciprocal.
        float norm = A(A(A(M(extracted.Y,extracted.Y),M(extracted.X,extracted.X)),M(extracted.Z,extracted.Z)),M(extracted.W,extracted.W));
        float inv = D(1,MathF.Sqrt(norm));
        extracted = new(M(extracted.X,inv),M(extracted.Y,inv),M(extracted.Z,inv),M(extracted.W,inv));
        Vector4 outputExtent = new(M(extent.X,2),A(extent.Y,extent.Y),M(extent.Z,2),extent.W);
        Matrix4x4 matrix = OriginalUnitWorldTransformMath.ScaleQuaternionPositionA23210(
            new(scale.X,scale.Y,scale.Z),actualWorld.Quaternion88,
            new(actualWorld.Translation80.X,actualWorld.Translation80.Y,actualWorld.Translation80.Z));
        return new(box,center,outputExtent,extracted,matrix,actualVolumeResource20);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a,float b)=>a/b;
}
