using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary>The actual D49EE0 Camera and selected View ambient inputs. The
/// Camera matrices, STA and current main eye.Y are not inputs to this writer.</summary>
public readonly record struct OriginalSkyPatternProjectionInputs(float EyeX10, float EyeZ18,
    float Far144, float VerticalFov148, float Aspect14C, float Height210,
    Vector3 SelectedAmbientDirection)
{
    public static OriginalSkyPatternProjectionInputs ReadCurrentCamera(ReadOnlySpan<byte> actualCamera,
        Vector3 actualSelectedAmbientDirection)
    {
        if (actualCamera.Length < 0x214)
            throw new InvalidDataException("Sky pattern projection needs actual Camera10/18/144/148/14C/210.");
        return new(F(actualCamera, 0x10), F(actualCamera, 0x18), F(actualCamera, 0x144),
            F(actualCamera, 0x148), F(actualCamera, 0x14C), F(actualCamera, 0x210), actualSelectedAmbientDirection);
    }
    private static float F(ReadOnlySpan<byte> value, int at) => BinaryPrimitives.ReadSingleLittleEndian(value.Slice(at, 4));
}

/// <summary>Numerical results after actual ready gates passed. Matrix180 is
/// published before the later D77B10 draw gate. The auxiliary eye/target/up
/// belong to the temporary generation Camera, not the map camera.</summary>
public sealed record OriginalSkyPatternProjectionMatrixWrite(Matrix4x4 Matrix180,
    Vector4 AuxiliaryEye10, Vector4 AuxiliaryTarget20, Vector4 AuxiliaryUp30,
    float FarFrustumRadius, bool AffineInverseWrote, string SourceEvidence)
{
    public void WriteMatrix180(Span<byte> actualMatrix180)
    {
        if (actualMatrix180.Length != 64)
            throw new InvalidDataException("Actual Sky180 matrix storage must contain64 bytes.");
        float[] words = [Matrix180.M11,Matrix180.M12,Matrix180.M13,Matrix180.M14,
            Matrix180.M21,Matrix180.M22,Matrix180.M23,Matrix180.M24,
            Matrix180.M31,Matrix180.M32,Matrix180.M33,Matrix180.M34,
            Matrix180.M41,Matrix180.M42,Matrix180.M43,Matrix180.M44];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(actualMatrix180[(i * 4)..], words[i]);
    }
}

internal sealed record OriginalSkyPatternProjectionConstants(float Half, float HeightMultiplier,
    float DegenerateThreshold, float Twice, float NegativeTwice, float NegativeOne,
    Vector4 UpNegativeY, Vector4 UpPositiveY, Vector4 UpPositiveX, Vector4 UpNegativeZ,
    Matrix4x4 InitialAffine, Vector4 TextureBias);

/// <summary>Loaded-once TC D49EE0 / SC D4AC10 numerical matrix producer.
/// The caller owns the actual resource readiness gates and retained Sky180.
/// An attempted generation must not invoke this writer after a source early exit.</summary>
public sealed class LoadedOriginalSkyPatternProjectionMatrixSource
{
    private readonly OriginalSkyPatternProjectionConstants c;
    internal LoadedOriginalSkyPatternProjectionMatrixSource(string locale, string evidence,
        OriginalSkyPatternProjectionConstants constants) => (Locale, SourceEvidence, c) = (locale, evidence, constants);
    public string Locale { get; }
    public string SourceEvidence { get; }
    public static LoadedOriginalSkyPatternProjectionMatrixSource Load(OriginalGameRepository repository, string locale)
        => OriginalDeferredSamplerSources.LoadSkyPatternProjectionMatrixSource(repository.SourceDirectory, locale);

    public OriginalSkyPatternProjectionMatrixWrite Prepare(OriginalSkyPatternProjectionInputs current,
        OriginalNativeCameraTangentDispatch actualTangentDispatch)
        => PrepareFromTangent(current, OriginalNativeCameraTangent.Tangent1020A90(
            M(current.VerticalFov148, c.Half), actualTangentDispatch));

    /// <summary>The explicit tangent is the native tanf boundary for
    /// float32(Camera148 * .5). This overload also supports original opcode
    /// comparisons without substituting host MathF.Tan.</summary>
    public OriginalSkyPatternProjectionMatrixWrite PrepareFromTangent(OriginalSkyPatternProjectionInputs current,
        float nativeTangentOfHalfFov)
    {
        float[] inputs = [current.EyeX10,current.EyeZ18,current.Far144,current.VerticalFov148,
            current.Aspect14C,current.Height210,current.SelectedAmbientDirection.X,
            current.SelectedAmbientDirection.Y,current.SelectedAmbientDirection.Z,nativeTangentOfHalfFov];
        if (inputs.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException("Sky pattern projection requires finite actual scalar inputs.");
        // D4A0D6..FD: each square and both additions round independently.
        float vertical = M(nativeTangentOfHalfFov, current.Far144);
        float horizontal = M(vertical, current.Aspect14C);
        float radius = Sqrt(A(A(M(horizontal,horizontal),M(vertical,vertical)),M(current.Far144,current.Far144)));
        float dx = Neg(current.SelectedAmbientDirection.X), dy = Neg(current.SelectedAmbientDirection.Y),
            dz = Neg(current.SelectedAmbientDirection.Z);
        float height = M(current.Height210,c.HeightMultiplier);
        var eye = new Vector4(current.EyeX10,height,current.EyeZ18,0);
        Vector4 up;
        float horizontalLength = Sqrt(A(A(M(dx,dx),0),M(dz,dz)));
        if (horizontalLength > c.DegenerateThreshold)
        {
            float reciprocal = D(1,horizontalLength);
            float hx = M(reciprocal,dx), hy = M(reciprocal,0), hz = M(reciprocal,dz);
            float cx = S(M(hz,dy),M(hy,dz)), cy = S(M(hx,dz),M(hz,dx)), cz = S(M(hy,dx),M(hx,dy));
            float crossLength = Sqrt(A(A(M(cy,cy),M(cx,cx)),M(cz,cz)));
            if (crossLength > c.DegenerateThreshold)
                up = new(S(M(cz,dy),M(cy,dz)), S(M(cx,dz),M(cz,dx)), S(M(cy,dx),M(cx,dy)),0);
            else up = dx > 0 || dz > 0 ? c.UpPositiveY : c.UpNegativeY;
        }
        else up = dy > 0 ? c.UpPositiveX : c.UpNegativeZ;
        var target = new Vector4(A(eye.X,M(dx,radius)),A(eye.Y,M(dy,radius)),A(eye.Z,M(dz,radius)),0);

        // AB6510/AB5BD0 force the separate temporary camera update here.
        // Its matrices do not feed the following Sky180 affine writer.
        Matrix4x4 affine = c.InitialAffine;
        float width = M(radius,c.Twice), flippedHeight = M(radius,c.NegativeTwice);
        affine.M11 = M(S(M(up.Z,dy),M(up.Y,dz)),width);
        affine.M12 = M(S(M(up.X,dz),M(up.Z,dx)),width);
        affine.M13 = M(S(M(up.Y,dx),M(up.X,dy)),width); affine.M14 = 0;
        affine.M21 = M(flippedHeight,up.X); affine.M22 = M(flippedHeight,up.Y);
        affine.M23 = M(flippedHeight,up.Z); affine.M24 = 0;
        affine.M31 = M(dx,c.NegativeOne); affine.M32 = M(dy,c.NegativeOne);
        affine.M33 = M(dz,c.NegativeOne); affine.M34 = 0;
        // Original subtracts the same current210 product, retaining float order.
        affine = Translate98B630(affine,new(eye.X,S(height,M(current.Height210,c.HeightMultiplier)),eye.Z,0));
        Matrix4x4 inverse = AffineInverse11510(affine,out bool inverseWrote);
        var texture = new Matrix4x4(c.Half,0,0,0,0,c.Half,0,0,0,0,1,0,0,0,0,1);
        texture = Translate98B630(texture,c.TextureBias);
        Matrix4x4 matrix = OriginalUnitWorldTransformMath.ComposeAffine17ADD0(inverse,texture);
        return new(matrix,eye,target,up,radius,inverseWrote,
            SourceEvidence+";actual-ready-numeric-write;180-published-before-D77B10;temporary-camera-matrices-independent");
    }

    private static Matrix4x4 Translate98B630(Matrix4x4 m, Vector4 v) => new(
        A(M(m.M14,v.X),m.M11),A(M(m.M14,v.Y),m.M12),A(M(m.M14,v.Z),m.M13),m.M14,
        A(M(m.M24,v.X),m.M21),A(M(m.M24,v.Y),m.M22),A(M(m.M24,v.Z),m.M23),m.M24,
        A(M(m.M34,v.X),m.M31),A(M(m.M34,v.Y),m.M32),A(M(m.M34,v.Z),m.M33),m.M34,
        A(M(m.M44,v.X),m.M41),A(M(m.M44,v.Y),m.M42),A(M(m.M44,v.Z),m.M43),m.M44);

    private static Matrix4x4 AffineInverse11510(Matrix4x4 m, out bool wrote)
    {
        float a=m.M11,b=m.M12,c=m.M13,d=m.M21,e=m.M22,f=m.M23,g=m.M31,h=m.M32,i=m.M33;
        float c00=S(M(e,i),M(f,h)),c01=S(M(c,h),M(b,i)),c02=S(M(b,f),M(c,e));
        float determinant=A(A(M(a,c00),M(d,c01)),M(g,c02));
        // The source destination aliases the source. det==0 writes nothing,
        // so the already published affine is retained and then bias-multiplied.
        wrote=determinant!=0;
        if (!wrote) return m;
        float c10=S(M(g,f),M(d,i)),c11=S(M(a,i),M(g,c)),c12=S(M(d,c),M(a,f)),
            c20=S(M(d,h),M(g,e)),c21=S(M(g,b),M(a,h)),c22=S(M(a,e),M(d,b)),factor=D(1,determinant);
        float tx=Neg(m.M41),ty=Neg(m.M42),tz=Neg(m.M43);
        return new(M(factor,c00),M(factor,c01),M(factor,c02),0,
            M(c10,factor),M(c11,factor),M(c12,factor),0,M(c20,factor),M(c21,factor),M(c22,factor),0,
            M(A(A(M(ty,c10),M(tx,c00)),M(tz,c20)),factor),
            M(A(A(M(ty,c11),M(tx,c01)),M(tz,c21)),factor),
            M(A(A(M(ty,c12),M(tx,c02)),M(tz,c22)),factor),1);
    }
    private static float Sqrt(float value)
    {
        if (!Sse.IsSupported) throw new PlatformNotSupportedException("Original Sky finite sqrt requires SSE.");
        return Sse.SqrtScalar(Vector128.CreateScalar(value)).GetElement(0);
    }
    private static float Neg(float value) => BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(value)^0x80000000u);
    private static float M(float a,float b) => OriginalNativeFloat.Multiply(a,b);
    private static float A(float a,float b) => OriginalNativeFloat.Add(a,b);
    private static float S(float a,float b) => OriginalNativeFloat.Subtract(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a,float b) => a/b;
}
