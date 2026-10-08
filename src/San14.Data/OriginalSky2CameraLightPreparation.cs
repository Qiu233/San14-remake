using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

public sealed record OriginalSky2CameraPacket(Matrix4x4 CameraToLocal,
    Matrix4x4? LocalToGlobalOverride, float ProjectionFarZ);
public sealed record OriginalSky2RgbCache(Vector3 NormalizedRgb, float Maximum);
public sealed record OriginalSky2PreparedLight(Vector4 Rgb, Vector4 Direction,
    Vector4? PreparedNegatedSunPosition);

/// <summary>Closed actual Sky2 camera/light outputs from explicit prepared
/// source fields. This does not resolve current owners, fallback selection,
/// renderer state or cache timing. Null overrides retain caller cache values.</summary>
public static class OriginalSky2CameraLightPreparation
{
    public static OriginalSky2CameraPacket PrepareCamera(Matrix4x4 explicitCameraMatrix70,
        Matrix4x4 explicitCameraMatrixB0, Vector4 explicitCameraPosition,
        ReadOnlySpan<float> explicitSkyTransform16, uint explicitPreparedCameraFlags148,
        int explicitNativeClipProfile, Matrix4x4? explicitControllerMatrix1B0)
    {
        Finite(explicitCameraMatrix70);Finite(explicitCameraPosition);
        Matrix4x4 sky = Matrix(explicitSkyTransform16);
        // 3F66E0 transposes the upper3x3 and explicitly constructs affine W.
        var c = explicitCameraMatrix70;
        float tx = Neg(A(A(M(c.M42,c.M12),M(c.M41,c.M11)),M(c.M43,c.M13)));
        float ty = Neg(A(A(M(c.M42,c.M22),M(c.M41,c.M21)),M(c.M43,c.M23)));
        float tz = Neg(A(A(M(c.M42,c.M32),M(c.M41,c.M31)),M(c.M43,c.M33)));
        float[] rigid = [c.M11,c.M21,c.M31,0,c.M12,c.M22,c.M32,0,
            c.M13,c.M23,c.M33,0,tx,ty,tz,1];
        float x = M(explicitCameraPosition.X,-1), y = M(explicitCameraPosition.Y,-1),
            z = M(explicitCameraPosition.Z,-1);
        // 98B630 adds row.W * translation to XYZ, product first in each ADDSS.
        for (int row = 0; row < 4; row++)
        {
            int a = row*4;float w = rigid[a+3];
            rigid[a] = A(M(w,x),rigid[a]);rigid[a+1] = A(M(w,y),rigid[a+1]);
            rigid[a+2] = A(M(w,z),rigid[a+2]);
        }
        Matrix4x4 inverse = OriginalNativeMatrixMath.Inverse3F6EB0(explicitCameraMatrixB0,out _);
        Matrix4x4 cameraToLocal = Multiply17B070(Multiply17B070(inverse,Matrix(rigid)),sky);
        if (explicitControllerMatrix1B0 is Matrix4x4 localToGlobal) Finite(localToGlobal);
        bool reverse = (explicitPreparedCameraFlags148 & 2u) != 0;
        float far = reverse ? (explicitNativeClipProfile == 2 ? 0 : -1)
            : (explicitNativeClipProfile is 2 or 4 ? 0 : 1);
        return new(cameraToLocal,explicitControllerMatrix1B0,far);
    }

    /// <summary>DA90/D7E0 setter cache: RGB/max and max. A zero max would
    /// generate nonfinite cache values in native code; this finite packet rejects it.</summary>
    public static OriginalSky2RgbCache PrepareRgbCache(Vector3 explicitRawRgb)
    {
        Finite(explicitRawRgb);
        float maximum = explicitRawRgb.X > explicitRawRgb.Y ? explicitRawRgb.X : explicitRawRgb.Y;
        if (maximum < explicitRawRgb.Z) maximum = explicitRawRgb.Z;
        if (maximum == 0) throw new InvalidDataException("Original RGB max zero would divide by zero; finite cache preparation is unsupported.");
        float inverse = D(1,maximum);
        return new(new(M(inverse,explicitRawRgb.X),M(inverse,explicitRawRgb.Y),M(inverse,explicitRawRgb.Z)),maximum);
    }

    /// <summary>Exact nonnull source Moon setter's zero branch. SSE DIVSS and
    /// MULSS retain native +Inf and invalid-product NaN bits in the cache.</summary>
    public static OriginalSky2RgbCache PrepareSourceZeroMoonCache(Vector3 rgb, OriginalSky2SourceZeroMoon proof)
    {
        ArgumentNullException.ThrowIfNull(proof); proof.ValidateZeroRgb(rgb, 0);
        if (!Sse.IsSupported) throw new PlatformNotSupportedException("Source Moon zero cache requires SSE.");
        var inverse = Sse.DivideScalar(Vector128.CreateScalar(1f), Vector128.CreateScalar(0f));
        float n = Sse.MultiplyScalar(inverse, Vector128.CreateScalar(0f)).GetElement(0);
        return new(new(n, n, n), 0f);
    }

    public static OriginalSky2PreparedLight? PrepareSun(bool explicitSunLightPresent,
        Vector4 explicitRawSunPosition, OriginalSky2RgbCache explicitSunCache1460,
        ReadOnlySpan<float> explicitSkyTransform16, float explicitIntensity1494,
        float explicitSize1490, bool explicitSuppressDirection1600, float? explicitControllerScalar38)
    {
        if (!explicitSunLightPresent) return null;
        ArgumentNullException.ThrowIfNull(explicitSunCache1460);
        Finite(explicitRawSunPosition);Finite(explicitSunCache1460.NormalizedRgb);Finite(explicitSunCache1460.Maximum);
        Finite(explicitIntensity1494);Finite(explicitSize1490);
        float maximum = explicitSunCache1460.Maximum;
        if (maximum == 0) throw new InvalidDataException("Original Sun RGB amplitude zero would divide by zero.");
        float scale = ControllerScale(explicitControllerScalar38,0x4FDEA278);
        float rgbW = D(M(scale,explicitIntensity1494),maximum);
        Vector3 normalizedRgb = explicitSunCache1460.NormalizedRgb;
        Vector4 rgb = new(M(maximum,normalizedRgb.X),M(maximum,normalizedRgb.Y),M(maximum,normalizedRgb.Z),rgbW);
        Vector4 negative = Negative(explicitRawSunPosition);
        Vector3 screen = Normalize(negative.X,negative.Y,negative.Z);
        Vector3 direction = RotateNormalize(negative,Matrix(explicitSkyTransform16));
        return new(rgb,new(direction,explicitSuppressDirection1600 ? 0 : explicitSize1490),
            new Vector4(screen,negative.W));
    }

    public static OriginalSky2PreparedLight? PrepareMoon(bool explicitMoonLightPresent,
        Vector4 explicitRawMoonPosition, Vector3 explicitRawMoonRgb,
        float explicitMoonMaximum147C, ReadOnlySpan<float> explicitSkyTransform16,
        float explicitIntensity149C, float explicitSize1498,
        bool explicitSuppressDirection1600, float? explicitControllerScalar38)
    {
        if (!explicitMoonLightPresent) return null;
        Finite(explicitRawMoonPosition);Finite(explicitRawMoonRgb);Finite(explicitMoonMaximum147C);
        Finite(explicitIntensity149C);Finite(explicitSize1498);
        if (explicitMoonMaximum147C == 0) throw new InvalidDataException("Original Moon RGB amplitude zero would divide by zero.");
        float scale = ControllerScale(explicitControllerScalar38,0x4689D436);
        Vector4 rgb = new(explicitRawMoonRgb,D(M(scale,explicitIntensity149C),explicitMoonMaximum147C));
        Vector3 direction = RotateNormalize(Negative(explicitRawMoonPosition),Matrix(explicitSkyTransform16));
        return new(rgb,new(direction,explicitSuppressDirection1600 ? 0 : explicitSize1498),null);
    }

    /// <summary>Pure original nonnull Moon setter math including its source
    /// zero-amplitude division. The consumer's actual PS selection remains
    /// separate; the returned +Inf W is not replaced with an invented zero.</summary>
    public static OriginalSky2PreparedLight PrepareCurrentMoonIncludingZeroAmplitude(
        Vector4 rawPosition, Vector3 rawRgb, float maximum, ReadOnlySpan<float> transform,
        float intensity, float size, bool suppress, float? controllerScalar)
    {
        Finite(rawPosition); Finite(rawRgb); Finite(maximum); Finite(intensity); Finite(size);
        float scale = ControllerScale(controllerScalar, 0x4689D436);
        Vector4 rgb = new(rawRgb, D(M(scale, intensity), maximum));
        Vector3 direction = RotateNormalize(Negative(rawPosition), Matrix(transform));
        return new(rgb, new(direction, suppress ? 0 : size), null);
    }

    public static OriginalSky2PreparedLight PrepareSourceZeroMoon(Vector4 rawPosition, Vector3 rawRgb,
        float maximum, ReadOnlySpan<float> transform, float intensity, float size, bool suppress,
        float? controllerScalar, OriginalSky2SourceZeroMoon proof)
    {
        ArgumentNullException.ThrowIfNull(proof); proof.ValidateZeroRgb(rawRgb, maximum);
        Finite(rawPosition); Finite(intensity); Finite(size);
        float scale = ControllerScale(controllerScalar, 0x4689D436);
        var rgb = new Vector4(rawRgb, D(M(scale, intensity), maximum));
        proof.ValidatePreparedRgb(rgb);
        Vector3 direction = RotateNormalize(Negative(rawPosition), Matrix(transform));
        return new(rgb, new(direction, suppress ? 0 : size), null);
    }

    private static Vector3 RotateNormalize(Vector4 v,Matrix4x4 m)
    {
        float x = A(M(v.Y,m.M21),M(v.X,m.M11));x = A(x,M(v.Z,m.M31));x = A(x,M(v.W,m.M41));
        float y = A(M(v.Y,m.M22),M(v.X,m.M12));y = A(y,M(v.Z,m.M32));y = A(y,M(v.W,m.M42));
        float z = A(M(v.Y,m.M23),M(v.X,m.M13));z = A(z,M(v.Z,m.M33));z = A(z,M(v.W,m.M43));
        return Normalize(x,y,z);
    }
    private static Vector3 Normalize(float x,float y,float z)
    {
        float lengthSquared = A(A(M(y,y),M(x,x)),M(z,z));
        if (!Sse.IsSupported) throw new PlatformNotSupportedException("Original finite sqrt path requires SSE.");
        if (!float.IsFinite(lengthSquared) || lengthSquared <= 0)
            throw new InvalidDataException("Original light length would produce zero-divisor/nonfinite normalization; finite preparation is unsupported.");
        // Native101FAD8 finite nonnegative branch101FB6C uses SQRTSS.
        float length = Sse.SqrtScalar(Vector128.CreateScalar(lengthSquared)).GetElement(0);
        float inverse = D(1,length);
        return new(M(x,inverse),M(y,inverse),M(z,inverse));
    }
    private static Matrix4x4 Multiply17B070(Matrix4x4 left,Matrix4x4 right)
    {
        float[] a = Words(left), b = Words(right), output = new float[16];
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 4; col++)
                // Packed source ADDPS groups (Z+X)+(W+Y), independently per lane.
                output[row*4+col] = A(A(M(a[row*4+2],b[8+col]),M(a[row*4],b[col])),
                    A(M(a[row*4+3],b[12+col]),M(a[row*4+1],b[4+col])));
        return Matrix(output);
    }
    private static Vector4 Negative(Vector4 v) => new(M(v.X,-1),M(v.Y,-1),M(v.Z,-1),M(v.W,-1));
    private static float ControllerScale(float? value,uint constant)
    {
        float scale = BitConverter.UInt32BitsToSingle(constant);
        if (value is float v) { Finite(v);scale = M(v,scale); }
        return scale;
    }
    private static Matrix4x4 Matrix(ReadOnlySpan<float> v)
    {
        if (v.Length != 16) throw new InvalidDataException("Original Sky2 matrix requires exactly16 native-order floats.");
        foreach (float f in v) Finite(f);
        return new(v[0],v[1],v[2],v[3],v[4],v[5],v[6],v[7],v[8],v[9],v[10],v[11],v[12],v[13],v[14],v[15]);
    }
    private static float[] Words(Matrix4x4 m) => [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,
        m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
    private static void Finite(Matrix4x4 m) { foreach (float v in Words(m)) Finite(v); }
    private static void Finite(Vector4 v) { Finite(v.X);Finite(v.Y);Finite(v.Z);Finite(v.W); }
    private static void Finite(Vector3 v) { Finite(v.X);Finite(v.Y);Finite(v.Z); }
    private static void Finite(float v)
    {
        if (!float.IsFinite(v)) throw new InvalidDataException("Original Sky2 camera/light inputs must be finite.");
    }
    private static float Neg(float v) => BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(v)^0x80000000);
    private static float M(float a,float b) => OriginalNativeFloat.Multiply(a,b);
    private static float A(float a,float b) => OriginalNativeFloat.Add(a,b);
    private static float S(float a,float b) => OriginalNativeFloat.Subtract(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a,float b) => a/b;
}
