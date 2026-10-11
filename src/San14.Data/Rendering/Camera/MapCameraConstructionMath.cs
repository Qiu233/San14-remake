using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary>The original raw and offset-adjusted row-vector view matrices.</summary>
public readonly record struct MapCameraViewMatrices(
    Matrix4x4 RawWorldToView40, Matrix4x4 WorldToView80);

/// <summary>Source Camera world pose after the original quaternion forward/up helpers.</summary>
public readonly record struct MapCameraQuaternionPose(
    Vector4 Eye10, Vector4 Target20, Vector4 Up30, float EffectiveLookDistance)
{
    public void WriteEyeTargetUp(Span<byte> cameraPrefix)
    {
        if (cameraPrefix.Length < 0x40)
            throw new InvalidDataException("Camera pose destination does not include the native eye/target/up slots.");
        Write(cameraPrefix[0x10..],Eye10); Write(cameraPrefix[0x20..],Target20); Write(cameraPrefix[0x30..],Up30);
    }
    private static void Write(Span<byte> destination,Vector4 v)
    {
        BinaryPrimitives.WriteSingleLittleEndian(destination,v.X);
        BinaryPrimitives.WriteSingleLittleEndian(destination[4..],v.Y);
        BinaryPrimitives.WriteSingleLittleEndian(destination[8..],v.Z);
        BinaryPrimitives.WriteSingleLittleEndian(destination[12..],v.W);
    }
}

/// <summary>Four complete CKIDSCamera matrices produced by a forced native update.</summary>
public readonly record struct MapCameraConstructionMatrices(
    Matrix4x4 RawWorldToView40, Matrix4x4 WorldToView80,
    Matrix4x4 ProjectionC0, Matrix4x4 WorldToProjection100)
{
    /// <summary>Downstream original full inverse; this is not an early Camera matrix slot.</summary>
    public Matrix4x4 ViewToWorld3F6EB0() =>
        NativeMatrixMath.Invert(WorldToView80, out _);

    /// <summary>Downstream original full inverse; this is not an early Camera matrix slot.</summary>
    public Matrix4x4 ProjectionToWorld3F6EB0() =>
        NativeMatrixMath.Invert(WorldToProjection100, out _);

}

/// <summary>Original eye/target/up, view-offset, projection and forced matrix update kernels.</summary>
public static partial class MapCameraConstructionMath
{
    /// <summary>SC C11350 / TC C0FE40: rotate the exact source +Z axis, then normalize XYZ in native scalar order.</summary>
    public static Vector4 ForwardC11350(Quaternion sourceQuaternion) =>
        NormalizeDirectionC11350(RotateVector016700(sourceQuaternion,new(0,0,1,0)));

    /// <summary>SC C113F0 / TC C0FEE0: rotate the exact source +Y axis, then normalize XYZ in native scalar order.</summary>
    public static Vector4 UpC113F0(Quaternion sourceQuaternion) =>
        NormalizeDirectionC11350(RotateVector016700(sourceQuaternion,new(0,1,0,0)));

    /// <summary>SC C45956..C45A00 / TC C442A6..C44350. Target uses the current native scale.Z getter result.
    /// The optional lens-preset call writes separate post-effect outputs and preserves that scale.
    /// Quaternion input is the actual world getter value.</summary>
    public static MapCameraQuaternionPose PoseFromQuaternionC45956(Vector4 sourceEye,
        Quaternion sourceQuaternion,float sourceLookDistance0C,float currentNativeScaleZ)
    {
        if (!Finite(sourceEye) || !float.IsFinite(sourceLookDistance0C) || !float.IsFinite(currentNativeScaleZ))
            throw new InvalidDataException("Source camera pose requires finite explicit eye, look distance and current scale.Z.");
        Vector4 forward=ForwardC11350(sourceQuaternion),up=UpC113F0(sourceQuaternion);
        float distance=NativeFloat.Multiply(currentNativeScaleZ,sourceLookDistance0C);
        Vector4 target=new(
            NativeFloat.Add(NativeFloat.Multiply(forward.X,distance),sourceEye.X),
            NativeFloat.Add(NativeFloat.Multiply(forward.Y,distance),sourceEye.Y),
            NativeFloat.Add(NativeFloat.Multiply(forward.Z,distance),sourceEye.Z),
            NativeFloat.Add(NativeFloat.Multiply(forward.W,distance),sourceEye.W));
        return new(sourceEye,target,up,distance);
    }

    /// <summary>Complete original scalar quaternion-vector kernel, q*v*conjugate(q). The raw quaternion is retained;
    /// unlike a unit-quaternion matrix shortcut, this preserves nonunit behavior. Vector W is copied directly.</summary>
    public static Vector4 RotateVector016700(Quaternion rotation,Vector4 vector)
    {
        if (!Finite(new(rotation.X,rotation.Y,rotation.Z,rotation.W)) || !Finite(vector))
            throw new InvalidDataException("Original quaternion-vector kernel requires finite explicit inputs.");
        float x0=0,x1=0,x2=0,x3=0,x4=0,x5=0,x6=0,x7=0,x8=0,x9=0,x10=0,x11=0,x12=0;
        x2 = vector.Y; // SC 00016707
        x3 = vector.X; // SC 0001670D
        x0 = x2; // SC 00016712
        x4 = vector.Z; // SC 00016715
        x1 = x4; // SC 00016720
        x7 = x4; // SC 00016728
        x8 = x3; // SC 00016730
        x9 = rotation.Y; // SC 00016739
        x0 = NativeFloat.Multiply(x0, x9); // SC 0001673F
        x10 = rotation.Z; // SC 00016749
        x1 = NativeFloat.Multiply(x1, x10); // SC 0001674F
        x6 = x10; // SC 00016754
        x6 = NativeFloat.Multiply(x6, x3); // SC 00016758
        x11 = rotation.X; // SC 00016761
        x7 = NativeFloat.Multiply(x7, x9); // SC 00016766
        x5 = x11; // SC 0001676B
        x5 = NativeFloat.Multiply(x5, x2); // SC 0001676F
        x8 = NativeFloat.Multiply(x8, x11); // SC 00016773
        x12 = rotation.W; // SC 0001677D
        x8 = NativeFloat.Add(x8, x0); // SC 00016783
        x0 = x10; // SC 00016788
        x0 = NativeFloat.Multiply(x0, x2); // SC 0001678C
        x8 = NativeFloat.Add(x8, x1); // SC 00016790
        x1 = x3; // SC 00016795
        x7 = NativeFloat.Subtract(x7, x0); // SC 00016798
        x1 = NativeFloat.Multiply(x1, x12); // SC 0001679C
        x3 = NativeFloat.Multiply(x3, x9); // SC 000167A1
        x0 = x4; // SC 000167A6
        x0 = NativeFloat.Multiply(x0, x11); // SC 000167A9
        x7 = NativeFloat.Add(x7, x1); // SC 000167AE
        x4 = NativeFloat.Multiply(x4, x12); // SC 000167B2
        x5 = NativeFloat.Subtract(x5, x3); // SC 000167B7
        x1 = x2; // SC 000167BB
        x6 = NativeFloat.Subtract(x6, x0); // SC 000167BE
        x1 = NativeFloat.Multiply(x1, x12); // SC 000167C2
        x3 = x9; // SC 000167C7
        x0 = x10; // SC 000167CB
        x2 = x10; // SC 000167CF
        x10 = NativeFloat.Multiply(x10, x8); // SC 000167D3
        x2 = NativeFloat.Multiply(x2, x7); // SC 000167D8
        x5 = NativeFloat.Add(x5, x4); // SC 000167DC
        x6 = NativeFloat.Add(x6, x1); // SC 000167E0
        x1 = x11; // SC 000167E4
        x1 = NativeFloat.Multiply(x1, x8); // SC 000167E8
        x3 = NativeFloat.Multiply(x3, x5); // SC 000167ED
        x0 = NativeFloat.Multiply(x0, x6); // SC 000167F1
        x3 = NativeFloat.Subtract(x3, x0); // SC 000167F5
        x0 = x11; // SC 000167F9
        x11 = NativeFloat.Multiply(x11, x6); // SC 000167FD
        x0 = NativeFloat.Multiply(x0, x5); // SC 00016802
        x3 = NativeFloat.Add(x3, x1); // SC 00016806
        x6 = NativeFloat.Multiply(x6, x12); // SC 0001680A
        x1 = x9; // SC 0001680F
        x5 = NativeFloat.Multiply(x5, x12); // SC 00016813
        x9 = NativeFloat.Multiply(x9, x7); // SC 00016818
        x2 = NativeFloat.Subtract(x2, x0); // SC 0001681D
        x1 = NativeFloat.Multiply(x1, x8); // SC 00016821
        x11 = NativeFloat.Subtract(x11, x9); // SC 0001682B
        x7 = NativeFloat.Multiply(x7, x12); // SC 00016835
        x2 = NativeFloat.Add(x2, x1); // SC 0001683A
        x11 = NativeFloat.Add(x11, x10); // SC 0001683E
        x7 = NativeFloat.Add(x7, x3); // SC 00016843
        x6 = NativeFloat.Add(x6, x2); // SC 00016847
        x5 = NativeFloat.Add(x5, x11); // SC 0001684B
        return new(x7,x6,x5,vector.W);
    }

    private static Vector4 NormalizeDirectionC11350(Vector4 v)
    {
        if (!Sse.IsSupported) throw new PlatformNotSupportedException("Original camera direction normalization requires SSE.");
        float ySquared=NativeFloat.Multiply(v.Y,v.Y),xSquared=NativeFloat.Multiply(v.X,v.X),
            zSquared=NativeFloat.Multiply(v.Z,v.Z);
        float squared=NativeFloat.Add(NativeFloat.Add(ySquared,xSquared),zSquared);
        // Positive finite source CRT sqrtf reaches the single sqrtss instruction.
        float length=Sse.SqrtScalar(Vector128.CreateScalar(squared)).GetElement(0);
        float inverse=Divide(1,length);
        return new(NativeFloat.Multiply(inverse,v.X),NativeFloat.Multiply(inverse,v.Y),
            NativeFloat.Multiply(inverse,v.Z),v.W);
    }

    /// <summary>SC AB6BC0 / TC AB5B30. A zero offset copies +40 to +80, including signed zero bits.</summary>
    public static MapCameraViewMatrices ViewAB6BC0(
        Vector4 eye, Vector4 target, Vector4 up, float viewOffset = 0)
    {
        if (!Finite(eye) || !Finite(target) || !Finite(up) || !float.IsFinite(viewOffset))
            throw new InvalidDataException("Original camera view requires explicit finite eye, target, up and offset.");
        Matrix4x4 raw = LookTo97EE50(eye, target, up);
        Matrix4x4 adjusted = viewOffset == 0 ? raw : ViewOffset573A10(raw, viewOffset);
        return new(raw, adjusted);
    }

    /// <summary>Complete numeric body SC 97EE50 / TC 97DCC0. Native zero-length and parallel-up results are retained.</summary>
    public static Matrix4x4 LookTo97EE50(Vector4 eye, Vector4 target, Vector4 up)
    {
        if (!Sse.IsSupported) throw new PlatformNotSupportedException("Original camera view requires SSE.");
        if (!Finite(eye) || !Finite(target) || !Finite(up))
            throw new InvalidDataException("Original camera look-to requires finite explicit vectors.");
        var x0 = Vector128.Create(eye.X, eye.Y, eye.Z, eye.W);
        var x1 = Sse.Subtract(x0, Vector128.Create(target.X, target.Y, target.Z, target.W));
        var x2 = Vector128.Create(up.X, up.Y, up.Z, up.W);
        Vector128<float> x3 = default, x4 = default, x5 = default, x6 = default,
            x7 = default, x8 = default, x9 = default, x10 = default;
        var c0 = Vector128.Create(0x7F800000u, 0x7F800000u, 0x7F800000u, 0x7F800000u).AsSingle();
        var c1 = Vector128.Create(0x7FC00000u, 0x7FC00000u, 0x7FC00000u, 0x7FC00000u).AsSingle();
        var c2 = Vector128.Create(0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu, 0x00000000u).AsSingle();
        var c3 = Vector128.Create(0x00000000u, 0x00000000u, 0x00000000u, 0x3F800000u).AsSingle();
        x5 = x1; // SC 0097EE7E
        x2 = Sse.Shuffle(x2, x2, 0xC9); // SC 0097EE81
        x5 = Sse.Multiply(x5, x1); // SC 0097EE85
        x6 = x1; // SC 0097EE88
        x1 = c0; // SC 0097EE8B
        x8 = x0; // SC 0097EE92
        x9 = Sse.Xor(x9, x9); // SC 0097EE96
        x10 = x9; // SC 0097EE9A
        x4 = x5; // SC 0097EE9E
        x4 = Sse.Shuffle(x4, x5, 0x99); // SC 0097EEA1
        x5 = Sse.AddScalar(x5, x4); // SC 0097EEA5
        x3 = x4; // SC 0097EEA9
        x3 = Sse.Shuffle(x3, x4, 0x55); // SC 0097EEAC
        x5 = Sse.AddScalar(x5, x3); // SC 0097EEB0
        x5 = Sse.Shuffle(x5, x5, 0x00); // SC 0097EEB4
        x1 = Sse.CompareNotEqual(x1, x5); // SC 0097EEB8
        x0 = Sse.Sqrt(x5); // SC 0097EEBC
        x6 = Sse.Divide(x6, x0); // SC 0097EEBF
        x10 = Sse.CompareNotEqual(x10, x0); // SC 0097EEC2
        x0 = x2; // SC 0097EEC7
        x10 = Sse.And(x10, x6); // SC 0097EECA
        x0 = Sse.Shuffle(x0, x2, 0xC9); // SC 0097EECE
        x10 = Sse.And(x10, x1); // SC 0097EED2
        x1 = Sse.AndNot(x1, c1); // SC 0097EED6
        x6 = x9; // SC 0097EEDD
        x10 = Sse.Or(x10, x1); // SC 0097EEE1
        x9 = Sse.Subtract(x9, x8); // SC 0097EEE5
        x3 = x10; // SC 0097EEE9
        x3 = Sse.Shuffle(x3, x10, 0xD2); // SC 0097EEED
        x1 = x3; // SC 0097EEF2
        x1 = Sse.Shuffle(x1, x3, 0xD2); // SC 0097EEF5
        x3 = Sse.Multiply(x3, x2); // SC 0097EEF9
        x1 = Sse.Multiply(x1, x0); // SC 0097EEFC
        x3 = Sse.Subtract(x3, x1); // SC 0097EEFF
        x3 = Sse.And(x3, c2); // SC 0097EF02
        x2 = x3; // SC 0097EF09
        x2 = Sse.Multiply(x2, x3); // SC 0097EF0C
        x1 = x2; // SC 0097EF0F
        x1 = Sse.Shuffle(x1, x2, 0x99); // SC 0097EF12
        x2 = Sse.AddScalar(x2, x1); // SC 0097EF16
        x0 = x1; // SC 0097EF1A
        x0 = Sse.Shuffle(x0, x1, 0x55); // SC 0097EF1D
        x2 = Sse.AddScalar(x2, x0); // SC 0097EF21
        x2 = Sse.Shuffle(x2, x2, 0x00); // SC 0097EF25
        x0 = Sse.Sqrt(x2); // SC 0097EF29
        x2 = Sse.CompareNotEqual(x2, c0); // SC 0097EF2C
        x3 = Sse.Divide(x3, x0); // SC 0097EF34
        x6 = Sse.CompareNotEqual(x6, x0); // SC 0097EF37
        x6 = Sse.And(x6, x3); // SC 0097EF3B
        x3 = x9; // SC 0097EF3E
        x6 = Sse.And(x6, x2); // SC 0097EF42
        x2 = Sse.AndNot(x2, c1); // SC 0097EF45
        x6 = Sse.Or(x6, x2); // SC 0097EF4C
        x2 = x10; // SC 0097EF4F
        x2 = Sse.Shuffle(x2, x10, 0xC9); // SC 0097EF53
        x5 = x6; // SC 0097EF58
        x5 = Sse.Shuffle(x5, x6, 0xD2); // SC 0097EF5B
        x0 = x2; // SC 0097EF5F
        x0 = Sse.Shuffle(x0, x2, 0xC9); // SC 0097EF62
        x1 = x5; // SC 0097EF66
        x1 = Sse.Shuffle(x1, x5, 0xD2); // SC 0097EF69
        x1 = Sse.Multiply(x1, x0); // SC 0097EF6D
        x5 = Sse.Multiply(x5, x2); // SC 0097EF70
        x2 = x9; // SC 0097EF73
        x2 = Sse.Multiply(x2, x6); // SC 0097EF77
        x9 = Sse.Multiply(x9, x10); // SC 0097EF7A
        x5 = Sse.Subtract(x5, x1); // SC 0097EF7E
        x1 = x2; // SC 0097EF81
        x1 = Sse.Shuffle(x1, x2, 0x99); // SC 0097EF84
        x0 = x1; // SC 0097EF88
        x2 = Sse.AddScalar(x2, x1); // SC 0097EF8B
        x0 = Sse.Shuffle(x0, x1, 0x55); // SC 0097EF8F
        x5 = Sse.And(x5, c2); // SC 0097EF93
        x3 = Sse.Multiply(x3, x5); // SC 0097EF9A
        x2 = Sse.AddScalar(x2, x0); // SC 0097EF9D
        x1 = x3; // SC 0097EFA1
        x1 = Sse.Shuffle(x1, x3, 0x99); // SC 0097EFA4
        x3 = Sse.AddScalar(x3, x1); // SC 0097EFA8
        x0 = x1; // SC 0097EFAC
        x0 = Sse.Shuffle(x0, x1, 0x55); // SC 0097EFAF
        x3 = Sse.AddScalar(x3, x0); // SC 0097EFB3
        x2 = Sse.Shuffle(x2, x2, 0x00); // SC 0097EFB7
        x1 = x9; // SC 0097EFBB
        x1 = Sse.Shuffle(x1, x9, 0x99); // SC 0097EFBF
        x9 = Sse.AddScalar(x9, x1); // SC 0097EFC4
        x3 = Sse.Shuffle(x3, x3, 0x00); // SC 0097EFC9
        x0 = x1; // SC 0097EFCD
        x0 = Sse.Shuffle(x0, x1, 0x55); // SC 0097EFD0
        x1 = c2; // SC 0097EFD4
        x7 = x1; // SC 0097EFDB
        x9 = Sse.AddScalar(x9, x0); // SC 0097EFDE
        x7 = Sse.AndNot(x7, x2); // SC 0097EFE3
        x0 = x1; // SC 0097EFE6
        x2 = x1; // SC 0097EFE9
        x0 = Sse.And(x0, x6); // SC 0097EFEC
        x2 = Sse.AndNot(x2, x3); // SC 0097EFEF
        x7 = Sse.Or(x7, x0); // SC 0097EFF2
        x6 = x1; // SC 0097EFF5
        x0 = x1; // SC 0097EFF8
        x1 = Sse.And(x1, x10); // SC 0097EFFB
        x0 = Sse.And(x0, x5); // SC 0097EFFF
        x9 = Sse.Shuffle(x9, x9, 0x00); // SC 0097F002
        x2 = Sse.Or(x2, x0); // SC 0097F007
        x5 = x7; // SC 0097F00A
        x5 = Sse.Shuffle(x5, x2, 0x44); // SC 0097F00D
        x6 = Sse.AndNot(x6, x9); // SC 0097F011
        x6 = Sse.Or(x6, x1); // SC 0097F015
        x7 = Sse.Shuffle(x7, x2, 0xEE); // SC 0097F018
        x0 = x6; // SC 0097F01C
        x4 = x5; // SC 0097F01F
        x0 = Sse.Shuffle(x0, c3, 0x44); // SC 0097F022
        x2 = x7; // SC 0097F02A
        x6 = Sse.Shuffle(x6, c3, 0xEE); // SC 0097F02D
        x5 = Sse.Shuffle(x5, x0, 0xDD); // SC 0097F035
        x4 = Sse.Shuffle(x4, x0, 0x88); // SC 0097F039
        x1 = x5; // SC 0097F03D
        x7 = Sse.Shuffle(x7, x6, 0xDD); // SC 0097F040
        x0 = x4; // SC 0097F044
        x2 = Sse.Shuffle(x2, x6, 0x88); // SC 0097F047
        x3 = x7; // SC 0097F04B
        return Rows(x0, x1, x2, x3);
    }

    /// <summary>SC 573A10 / paired TC 573AB0. Camera passes the sign-bit-negated offset to native 014590.</summary>
    public static Matrix4x4 ViewOffset573A10(Matrix4x4 rawView, float viewOffset)
    {
        if (!float.IsFinite(viewOffset))
            throw new InvalidDataException("Original camera view offset is not finite.");
        float angle = BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(viewOffset) ^ 0x80000000u);
        NativeSinCos t = NativeTrigonometry.SinCos014590(angle);
        Vector4 a = Turn(new(rawView.M11,rawView.M12,rawView.M13,rawView.M14), t);
        Vector4 b = Turn(new(rawView.M21,rawView.M22,rawView.M23,rawView.M24), t);
        Vector4 c = Turn(new(rawView.M31,rawView.M32,rawView.M33,rawView.M34), t);
        Vector4 d = Turn(new(rawView.M41,rawView.M42,rawView.M43,rawView.M44), t);
        return new(a.X,a.Y,a.Z,a.W,b.X,b.Y,b.Z,b.W,c.X,c.Y,c.Z,c.W,d.X,d.Y,d.Z,d.W);
    }

    /// <summary>SC AB7040 / TC AB5FB0, with an explicit original tanf result at its one native call boundary.
    /// Arguments near/far are already ordered by the caller; reverse-depth swaps them.
    /// Clip profile bit1 chooses [0,1] depth, otherwise the source [-1,1] branch is used.</summary>
    public static Matrix4x4 ProjectionFromTangentAB7040(float verticalFovRadians, float aspect,
        float nearArgument, float farArgument, uint nativeClipProfile, float projectionScale,
        float nativeTangentOfHalfFov)
    {
        if (!float.IsFinite(verticalFovRadians) || verticalFovRadians <= 0 || verticalFovRadians >= MathF.PI ||
            !float.IsFinite(aspect) || aspect <= 0 || !float.IsFinite(nearArgument) || nearArgument <= 0 ||
            !float.IsFinite(farArgument) || farArgument <= 0 || nearArgument == farArgument ||
            !float.IsFinite(projectionScale) || projectionScale <= 0 ||
            !float.IsFinite(nativeTangentOfHalfFov) || nativeTangentOfHalfFov <= 0)
            throw new InvalidDataException("Original camera perspective inputs are outside the finite source contract.");
        float denominator = NativeFloat.Multiply(nativeTangentOfHalfFov, projectionScale);
        float yScale = Divide(1, denominator);
        float range = NativeFloat.Subtract(nearArgument, farArgument);
        float depthScale, depthOffset;
        if ((nativeClipProfile & 2) != 0)
        {
            depthScale = Divide(farArgument, range);
            depthOffset = NativeFloat.Multiply(depthScale, nearArgument);
        }
        else
        {
            float twiceNear = NativeFloat.Add(nearArgument, nearArgument);
            float sum = NativeFloat.Add(nearArgument, farArgument);
            float product = NativeFloat.Multiply(twiceNear, farArgument);
            float inverseRange = Divide(1, range);
            depthScale = NativeFloat.Multiply(sum, inverseRange);
            depthOffset = NativeFloat.Multiply(product, inverseRange);
        }
        float xScale = Divide(yScale, aspect);
        return new(xScale,0,0,0,0,yScale,0,0,0,0,depthScale,-1,0,0,depthOffset,0);
    }

    /// <summary>Native perspective with original float32 half-FOV multiply and explicit original CRT dispatch.</summary>
    public static Matrix4x4 ProjectionAB7040(float verticalFovRadians,float aspect,
        float nearArgument,float farArgument,uint nativeClipProfile,float projectionScale,
        NativeCameraTangentDispatch tangentDispatch)
    {
        float half=NativeFloat.Multiply(verticalFovRadians,.5f);
        float tangent=NativeCameraTangent.Tangent1020A90(half,tangentDispatch);
        return ProjectionFromTangentAB7040(verticalFovRadians,aspect,nearArgument,farArgument,
            nativeClipProfile,projectionScale,tangent);
    }

    /// <summary>Fresh/forced SC AB6C60 / TC AB5BD0 update of both matrices. Perspective uses literal scale1;
    /// orthographic projection reads Camera +154. The tangent belongs to float32(FOV * .5f).</summary>
    public static MapCameraConstructionMatrices CompleteFromTangentAB6C60(
        Vector4 eye, Vector4 target, Vector4 up, CameraLens lens,
        uint nativeClipProfile, float nativeTangentOfHalfFov)
    {
        if (!float.IsFinite(lens.Near) || !float.IsFinite(lens.Far) || lens.Near <= 0 || lens.Far <= lens.Near)
            throw new InvalidDataException("Original camera lens near/far values are not ordered positive distances.");
        MapCameraViewMatrices view = ViewAB6BC0(eye,target,up,lens.ViewOffset);
        Matrix4x4 projection = lens.IsOrthographic
            ? OrthographicFromTangent(eye, target, lens, nativeClipProfile, nativeTangentOfHalfFov)
            : ProjectionFromTangentAB7040(lens.VerticalFovRadians, lens.Aspect,
                lens.ReverseDepth ? lens.Far : lens.Near, lens.ReverseDepth ? lens.Near : lens.Far,
                nativeClipProfile, 1, nativeTangentOfHalfFov);
        Matrix4x4 combined = UnitWorldTransformMath.ComposeAffine17ADD0(view.WorldToView80, projection);
        return new(view.RawWorldToView40, view.WorldToView80, projection, combined);
    }

    /// <summary>Fresh/forced native update, including the exact original tangent implementation.
    /// Explicit dispatch and clip profile avoid assuming the current runtime CPU/context branches.</summary>
    public static MapCameraConstructionMatrices CompleteAB6C60(
        Vector4 eye,Vector4 target,Vector4 up,CameraLens lens,uint nativeClipProfile,
        NativeCameraTangentDispatch tangentDispatch)
    {
        float half=NativeFloat.Multiply(lens.VerticalFovRadians,.5f);
        float tangent=NativeCameraTangent.Tangent1020A90(half,tangentDispatch);
        return CompleteFromTangentAB6C60(eye,target,up,lens,nativeClipProfile,tangent);
    }

    private static Vector4 Turn(Vector4 row, NativeSinCos t)
    {
        float x = NativeFloat.Subtract(NativeFloat.Multiply(row.X,t.Cos),
            NativeFloat.Multiply(row.Y,t.Sin));
        float y = NativeFloat.Add(NativeFloat.Multiply(row.X,t.Sin),
            NativeFloat.Multiply(row.Y,t.Cos));
        return new(x,y,row.Z,row.W);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static float Divide(float numerator, float denominator) => numerator / denominator;

    private static Matrix4x4 Rows(Vector128<float> a, Vector128<float> b, Vector128<float> c, Vector128<float> d) => new(
        a.GetElement(0),a.GetElement(1),a.GetElement(2),a.GetElement(3),
        b.GetElement(0),b.GetElement(1),b.GetElement(2),b.GetElement(3),
        c.GetElement(0),c.GetElement(1),c.GetElement(2),c.GetElement(3),
        d.GetElement(0),d.GetElement(1),d.GetElement(2),d.GetElement(3));

    private static bool Finite(Vector4 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) && float.IsFinite(v.W);
}
