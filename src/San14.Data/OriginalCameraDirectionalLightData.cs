using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

public sealed record OriginalCameraDirectionalLightDataPacket(byte[] Bytes, bool Applied,
    bool SourceHighBitSwappedRows);

/// <summary>
/// CBC770's explicit prepared-Camera local-copy branch. The caller supplies the
/// actual scratch copied byB992A0 and complete LightData, never persistent CPU
/// cache substitutes. This does not establish the publisher/View/GPU identity.
/// </summary>
public static class OriginalCameraDirectionalLightData
{
    public const int LightDataBytes = 0x10C0;
    public const int CameraPrefixBytes = 0x3C0;

    /// <summary>Native context mode low2=1, LightData10B0=6. Other lighting
    /// modes have their separate current modifier branches.</summary>
    public static OriginalCameraDirectionalLightDataPacket Apply(ReadOnlySpan<byte> explicitLightData,
        ReadOnlySpan<byte> explicitPreparedCameraPrefix, uint explicitContextFlags3E4)
    {
        if (explicitLightData.Length != LightDataBytes)
            throw new InvalidDataException("Camera lighting preparation requires the complete explicit10C0 LightData.");
        if ((explicitContextFlags3E4 & 3) != 1)
            return new(explicitLightData.ToArray(), false, false);
        if (explicitPreparedCameraPrefix.Length != CameraPrefixBytes)
            throw new InvalidDataException("Camera lighting preparation requires the explicit prepared3C0 Camera prefix.");
        if (explicitLightData[0x10B0] != 6)
            throw new NotSupportedException("Original Camera lighting mode10B0 other than6 requires its separate modifier branch.");
        bool swap = (BinaryPrimitives.ReadUInt32LittleEndian(explicitPreparedCameraPrefix.Slice(0x3B0, 4)) & 0x80000000) != 0;
        int firstQuaternion = swap ? 0xA0 : 0xB0;
        int secondQuaternion = swap ? 0xB0 : 0xA0;
        Vector4 qFirst = QuaternionFromPositiveZ(ReadThree(explicitPreparedCameraPrefix, 0x2AC));
        Vector4 qSecond = QuaternionFromPositiveZ(ReadThree(explicitPreparedCameraPrefix, 0x2F0));
        var quaternions = new Vector4[4];
        quaternions[firstQuaternion == 0xA0 ? 0 : 1] = qFirst;
        quaternions[secondQuaternion == 0xA0 ? 0 : 1] = qSecond;
        quaternions[2] = ReadFour(explicitLightData, 0xC0);
        quaternions[3] = ReadFour(explicitLightData, 0xD0);
        Vector4[] directions = quaternions.Select(q => OriginalDirectionalLightSource.RotatePreparedQuaternion(q)).ToArray();
        Vector4 hemiDirection = OriginalDirectionalLightSource.RotatePreparedQuaternion(ReadFour(explicitLightData, 0xF0), true);
        Vector3 firstDirection = ReadThree(explicitPreparedCameraPrefix, 0x2B8);
        Vector3 secondDirection = ReadThree(explicitPreparedCameraPrefix, 0x2FC);

        // All unsupported branches and finite direction inputs are checked
        // before committing the independently owned copy.
        byte[] bytes = explicitLightData.ToArray();
        bytes[0xF1E] = 2;
        Write(bytes, 0xF80, new(firstDirection, 0));
        Write(bytes, 0xF90, new(secondDirection, 0));
        explicitPreparedCameraPrefix.Slice(0x2C4, 16).CopyTo(bytes.AsSpan(0xFA0, 16));
        explicitPreparedCameraPrefix.Slice(0x308, 16).CopyTo(bytes.AsSpan(0xFB0, 16));
        // These are four copied words. Their fourth words are not inferred RGB
        // alpha values from the neighboring persistent CPU cache layout.
        explicitPreparedCameraPrefix.Slice(0x2D4, 16).CopyTo(bytes.AsSpan(swap ? 0x10 : 0x30, 16));
        explicitPreparedCameraPrefix.Slice(0x318, 16).CopyTo(bytes.AsSpan(swap ? 0x30 : 0x10, 16));
        Write(bytes, firstQuaternion, qFirst); Write(bytes, secondQuaternion, qSecond);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0xFDC, 4), swap ? 0 : 2);
        for (int i = 0; i < 4; i++) Write(bytes, i * 32, directions[i]);
        Write(bytes, 0x120, hemiDirection);
        explicitPreparedCameraPrefix.Slice(0x340, 96).CopyTo(bytes.AsSpan(0x150, 96));
        if (bytes[0x10B2] == 8)
            bytes = OriginalCameraDirectionalLightColorMixing.ApplyPreparedMode6ThirdColor(bytes, explicitPreparedCameraPrefix);
        return new(bytes, true, swap);
    }

    /// <summary>A11EF0 with the original fixed first vector(0,0,1,0).
    /// Uses the native cross/dot order and exact near-parallel thresholds.</summary>
    public static Vector4 QuaternionFromPositiveZ(Vector3 target)
    {
        Finite(target.X); Finite(target.Y); Finite(target.Z);
        float dot = Add(Add(Mul(target.X, 0), Mul(target.Y, 0)), Mul(target.Z, 1));
        float x = Sub(Mul(target.Z, 0), Mul(1, target.Y));
        float y = Sub(Mul(1, target.X), Mul(target.Z, 0));
        float z = Sub(Mul(0, target.Y), Mul(target.X, 0));
        float minimum = BitConverter.UInt32BitsToSingle(0xB7480000), maximum = BitConverter.UInt32BitsToSingle(0x37480000);
        bool parallel = x >= minimum && x <= maximum && y >= minimum && y <= maximum && z >= minimum && z <= maximum;
        if (dot > 1 || (parallel && dot >= 0)) return new(0, 0, 0, 1);
        if ((parallel && dot < 0) || dot < BitConverter.UInt32BitsToSingle(0xBF7FB1E0))
            return new(1, 0, 0, 0);
        float norm = Mul(Add(dot, 1), .5f);
        if (!float.IsFinite(norm) || norm <= 0)
            throw new InvalidDataException("Camera direction quaternion would produce a nonfinite normalization.");
        if (!Sse.IsSupported) throw new PlatformNotSupportedException("Original Camera direction quaternion requires SSE.");
        float w = Sse.SqrtScalar(Vector128.CreateScalar(norm)).GetElement(0);
        float inverse = Div(.5f, w);
        Vector4 result = new(Mul(inverse, x), Mul(inverse, y), Mul(inverse, z), w);
        Finite(result.X); Finite(result.Y); Finite(result.Z); Finite(result.W);
        return result;
    }

    private static Vector3 ReadThree(ReadOnlySpan<byte> bytes, int offset) =>
        new(ReadFloat(bytes, offset), ReadFloat(bytes, offset + 4), ReadFloat(bytes, offset + 8));
    private static Vector4 ReadFour(ReadOnlySpan<byte> bytes, int offset) =>
        new(ReadFloat(bytes, offset), ReadFloat(bytes, offset + 4), ReadFloat(bytes, offset + 8), ReadFloat(bytes, offset + 12));
    private static float ReadFloat(ReadOnlySpan<byte> bytes, int offset)
    { float value = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset, 4)); Finite(value); return value; }
    private static void Write(byte[] bytes, int offset, Vector4 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset, 4), value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 4, 4), value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 8, 4), value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 12, 4), value.W);
    }
    private static void Finite(float value)
    { if (!float.IsFinite(value)) throw new InvalidDataException("Camera light direction requires finite consumed floats."); }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Mul(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Sub(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Div(float a, float b) => a / b;
}
