using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public static partial class OriginalMainMapCameraController
{
    /// <summary>C880 source startup endpoint after its focus and angle clamps.</summary>
    public static OriginalMainMapCameraControllerState ConstructSourceDefaultState(
        OriginalMainMapCameraControllerParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new(102400,0,102400,1,parameters.ZoomAnchor(4),
            (double)3.1415927410125732f,(double)1.5707963705062866f);
    }

    /// <summary>Exact CTitleState.V10 immediate focus request, index2/PI/PI4.
    /// Native D550 returns success directly for band2. The final eye guard is
    /// checked after the original focus and angle clamps.</summary>
    public static OriginalMainMapCameraControllerState InitialFocus(
        OriginalMainMapCameraControllerParameters parameters,Vector3 sourceRulerHexFocus,
        bool currentMaximumYOverride=false)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if(!float.IsFinite(sourceRulerHexFocus.X)||!float.IsFinite(sourceRulerHexFocus.Y)||!float.IsFinite(sourceRulerHexFocus.Z))
            throw new InvalidDataException("Initial Main camera focus requires the explicit finite source ruler Hex position.");
        double distance=parameters.ZoomAnchor(2);
        var bounds=parameters.Bounds(ClassifyZoom(parameters,distance),currentMaximumYOverride);
        var result=new OriginalMainMapCameraControllerState(
            Clamp(sourceRulerHexFocus.X,bounds.MinimumFocus70.X,bounds.MaximumFocus80.X),
            Clamp(0,bounds.MinimumFocus70.Y,bounds.MaximumFocus80.Y),
            Clamp(sourceRulerHexFocus.Z,bounds.MinimumFocus70.Z,bounds.MaximumFocus80.Z),1,distance,
            Clamp((double)3.1415927410125732f,bounds.MinimumYawB0,bounds.MaximumYawB8),
            Clamp((double)0.7853981852531433f,bounds.MinimumPitchC8,bounds.MaximumPitchD0));
        if(!CandidateEyeAllowed(result))
            throw new InvalidDataException("Original Main initial focus candidate was rejected by its native eye boundary.");
        return result;
    }

    /// <summary>00EAB0. The integer band advances at fraction10000; its returned
    /// float still uses the previous lower band, preserving the native quirk.</summary>
    public static OriginalMainMapCameraZoomSelection ClassifyZoom(
        OriginalMainMapCameraControllerParameters parameters,double distance90)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if(!double.IsFinite(distance90))throw new InvalidDataException("Original zoom classification requires finite source distance.");
        float v=(float)distance90;int upper=0;
        while(upper<=6&&parameters.ZoomAnchor(upper)<v)upper++;
        if(upper==7)return new(6,0,6);
        int lower=upper==0?0:upper-1;
        float t=upper==0?0:D(S(v,parameters.ZoomAnchor(lower)),S(parameters.ZoomAnchor(upper),parameters.ZoomAnchor(lower)));
        int fraction=(int)M(t,10000f),band=lower;
        if(fraction<0)fraction=0;
        else {fraction=Math.Min(fraction,10000);if(fraction==10000){band++;fraction=0;}}
        return new(band,fraction,A(lower,D(fraction,10000f)));
    }

    /// <summary>175E0 wheel branch: one adjacent rounded band regardless of
    /// wheel magnitude. Returns the original 500 ms timed request.</summary>
    public static OriginalMainMapCameraZoomTarget SelectWheelZoomTarget(
        OriginalMainMapCameraControllerParameters parameters,OriginalMainMapCameraZoomSelection retainedSelection,
        int signedWheelDelta)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if(signedWheelDelta==0)throw new InvalidDataException("Native wheel writer requires a nonzero wheel event.");
        float index=A(retainedSelection.Band1E0,D(retainedSelection.Fraction1E4,10000f));
        int rounded=(int)MathF.Round(index,MidpointRounding.AwayFromZero);
        int selected=Math.Clamp(rounded+(signedWheelDelta>0?-1:1),0,6);
        return new(selected,parameters.ZoomAnchor(selected),parameters.ZoomBandPitch(selected),500);
    }

    /// <summary>175E0 orbit request, using signed native pointer delta words.
    /// The pitch request is subsequently clamped by the native request consumer.</summary>
    public static OriginalMainMapCameraControllerRequest OrbitRequest(
        OriginalMainMapCameraControllerState state,int deltaX44,int deltaY48) =>
        new(0,Vector4.Zero,-1,WrapYaw(DS(state.YawA8,DM(deltaX44,.005))),
            DA(state.PitchC0,DM(deltaY48,.005)),0);

    public static double WrapYaw(double yaw)
    {
        if(!double.IsFinite(yaw)||Math.Abs(yaw)>6.283185308*65536)
            throw new InvalidDataException("Original yaw wrapping requires a finite bounded source angle.");
        while(yaw>6.283185308)yaw=DS(yaw,6.283185308);
        while(yaw<0)yaw=DA(yaw,6.283185308);
        return yaw;
    }

    /// <summary>AB70 final whole-candidate boundary, using actual derived eye.
    /// A false result suppresses pose commit; earlier request metadata writes are separate.</summary>
    public static bool CandidateEyeAllowed(OriginalMainMapCameraControllerState candidate)
    {
        var eye=DerivePose(candidate).Eye10;
        return !(eye.Y<=3000f&&eye.Z>S(346780.15625f,M(eye.X,0.8549618124961853f)));
    }
    internal static float DivideFloat(float a,float b)=>D(a,b);
    internal static float InterpolateFloat(float a,float b,float t)=>A(M(S(1f,t),a),M(t,b));
    private static double Clamp(double value,double min,double max)=>value>=max?max:min>=value?min:value;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DA(double a,double b)=>a+b;
}
