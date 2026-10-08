using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Immutable current native controller fields. Focus is state+50..68,
/// distance is +90, yaw is +A8 and pitch is +C0; all seven inputs are binary64.</summary>
public sealed record OriginalMainMapCameraControllerState(double FocusX, double FocusY,
    double FocusZ, double FocusW, double Distance90, double YawA8, double PitchC0);

/// <summary>Exact intermediate controller axes and source setter inputs.</summary>
public readonly record struct OriginalMainMapCameraControllerPose(Vector4 Eye10,
    Vector4 Forward20, Vector4 Up30, Matrix4x4 Basis11800, Quaternion Quaternion12A70)
{
    /// <summary>Scale is the explicit current source getter90 result. The
    /// controller writes position and quaternion through V68/V70 only.</summary>
    public OriginalMapCameraCurrentPose WithCurrentScale(Vector3 currentScale90) =>
        new(new(Eye10.X, Eye10.Y, Eye10.Z), Quaternion12A70, currentScale90);
}

/// <summary>Original Main map controller publication, SC/TC 15B50/15A80.
/// Camera construction, lens preparation and matrices remain with their source owners.</summary>
public static partial class OriginalMainMapCameraController
{
    public const string ContractId = "ce-20261002-main-map-camera-controller";

    /// <summary>B6A0/E470 produce eye and axes; 11800/12A70 produce the exact
    /// quaternion passed to source Camera.V70, followed by position at V68.</summary>
    public static OriginalMapCameraCurrentPose Publish(OriginalMainMapCameraControllerState state,
        Vector3 explicitCurrentScale90) => DerivePose(state).WithCurrentScale(explicitCurrentScale90);

    /// <summary>Native owner+290 only overrides sourceCPU+12E8 when positive.
    /// 17BD0 then invokes Camera.V418 again in the same update.</summary>
    public static float ApplyPositiveFovOverride(float retainedSourceFov12E8, float ownerOverride290) =>
        ownerOverride290 > 0f ? ownerOverride290 : retainedSourceFov12E8;

    public static OriginalMainMapCameraControllerPose DerivePose(OriginalMainMapCameraControllerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (double value in new[] {state.FocusX,state.FocusY,state.FocusZ,state.FocusW,
            state.Distance90,state.YawA8,state.PitchC0})
            if (!double.IsFinite(value)) throw new InvalidDataException("Main map camera publication requires finite explicit controller fields.");
        var yaw = OriginalNativeTrigonometry.SinCos014590((float)state.YawA8);
        float negativePitch = FlipSign((float)state.PitchC0);
        var pitch = OriginalNativeTrigonometry.SinCos014590(negativePitch);
        Vector3 forward = Normalize(new(M(pitch.Cos,yaw.Sin),pitch.Sin,M(pitch.Cos,yaw.Cos)));
        var quarter = OriginalNativeTrigonometry.SinCos014590(A(negativePitch,1.5707963705062866f));
        Vector3 up = Normalize(new(M(quarter.Cos,yaw.Sin),quarter.Sin,M(quarter.Cos,yaw.Cos)));
        Vector3 right = Normalize(Cross(up,forward));
        up = Normalize(Cross(forward,right));
        var eye = new Vector4((float)DS(state.FocusX,DM(forward.X,state.Distance90)),
            (float)DS(state.FocusY,DM(forward.Y,state.Distance90)),
            (float)DS(state.FocusZ,DM(forward.Z,state.Distance90)),1f);
        // 11800 computes its right cross from the raw forward input, then
        // stores normalized forward and recomputes up without normalization.
        Vector3 basisForward = Normalize(forward);
        Vector3 basisRight = Normalize(Cross(up,forward));
        Vector3 basisUp = Cross(basisForward,basisRight);
        var basis = new Matrix4x4(basisRight.X,basisRight.Y,basisRight.Z,0,
            basisUp.X,basisUp.Y,basisUp.Z,0,basisForward.X,basisForward.Y,basisForward.Z,0,0,0,0,1);
        Quaternion q = QuaternionFromBasis12A70(basis);
        return new(eye,new(forward,1f),new(up,0f),basis,q);
    }

    /// <summary>The original matrix-to-quaternion branches, with scalar binary32
    /// grouping and the source largest-diagonal tie choices.</summary>
    public static Quaternion QuaternionFromBasis12A70(Matrix4x4 basis)
    {
        float[] m = [basis.M11,basis.M12,basis.M13,basis.M14,basis.M21,basis.M22,basis.M23,basis.M24,
            basis.M31,basis.M32,basis.M33,basis.M34,basis.M41,basis.M42,basis.M43,basis.M44];
        float trace = A(A(m[0],m[5]),m[10]);
        if (trace > 0f)
        {
            float root = MathF.Sqrt(A(trace,1f)), factor=D(.5f,root);
            return new(M(S(m[6],m[9]),factor),M(S(m[8],m[2]),factor),
                M(S(m[1],m[4]),factor),M(root,.5f));
        }
        int largest=0, second=1, third=2;
        if (m[5]>m[0]) {largest=1;second=2;third=0;}
        if (m[10]>m[largest*5]) {largest=2;second=0;third=1;}
        float s=MathF.Sqrt(A(S(m[largest*5],A(m[second*5],m[third*5])),1f));
        float f=D(.5f,s);float[] q=new float[4];
        q[largest]=M(s,.5f);
        q[3]=M(S(m[third+second*4],m[second+third*4]),f);
        q[second]=M(A(m[largest+second*4],m[second+largest*4]),f);
        q[third]=M(A(m[largest+third*4],m[third+largest*4]),f);
        return new(q[0],q[1],q[2],q[3]);
    }

    private static Vector3 Cross(Vector3 a,Vector3 b) => new(
        S(M(a.Y,b.Z),M(a.Z,b.Y)),S(M(a.Z,b.X),M(a.X,b.Z)),S(M(a.X,b.Y),M(a.Y,b.X)));
    private static Vector3 Normalize(Vector3 v)
    {
        float f=D(1f,MathF.Sqrt(A(A(M(v.X,v.X),M(v.Y,v.Y)),M(v.Z,v.Z))));
        return new(M(v.X,f),M(v.Y,f),M(v.Z,f));
    }
    private static float FlipSign(float v) => BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(v)^0x80000000u);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float M(float a,float b) => a*b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float A(float a,float b) => a+b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float S(float a,float b) => a-b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a,float b) => a/b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double DM(double a,double b) => a*b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double DS(double a,double b) => a-b;
}
