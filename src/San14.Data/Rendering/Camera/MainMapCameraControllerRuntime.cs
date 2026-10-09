using System.Collections.Immutable;
using System.Numerics;

namespace San14.Data;

/// <summary>Owned current controller inputs and persistent request/tick metadata.</summary>
public sealed record MainMapCameraControllerRuntimeState(MainMapCameraControllerState Pose,
    MainMapCameraControllerBounds Bounds,MainMapCameraZoomSelection Selection)
{
    public Vector4 VelocityF0 {get;init;}=new(0,0,0,1);
    public int VelocityStart100 {get;init;}
    public int VelocityLast104 {get;init;}
    public int RequestMode188 {get;init;}
    public uint DirtyCountdownD8 {get;init;}=2;
    public int DistanceDirection1E8 {get;init;}
    public int RequestMeta228 {get;init;}
    public ImmutableDictionary<int,double> BandPitch {get;init;}=ImmutableDictionary<int,double>.Empty;
    public MainMapCameraControllerTransition? Transition {get;init;}
}
public sealed record MainMapCameraControllerTransition(int Duration, int Elapsed, int LastClock,
    Vector4 StartFocus,Vector4 TargetFocus,double StartDistance,double TargetDistance,
    double StartYaw,double YawDelta,double StartPitch,double TargetPitch,int Selector38);
public readonly record struct MainMapCameraTerrainPitchResult(bool Accepted,double Pitch,string Provenance);
public readonly record struct MainMapCameraTerrainPitchInput(int NativeBand,
    MainMapCameraControllerState CandidateBeforeRequestedPitch,double MinimumPitch,double MaximumPitch,double RequestedPitch);
public readonly record struct MainMapCameraControllerApplyResult(
    MainMapCameraControllerRuntimeState State,bool PoseCommitted);
public readonly record struct MainMapCameraCursorRay(Vector4 Origin,Vector4 Direction,bool SourceReady);
public readonly record struct MainMapCameraEyeForward(Vector4 Eye,Vector4 Forward);
public readonly record struct MainMapCameraPanResult(MainMapCameraControllerRequest Request,
    Vector4 Delta,Vector4 SavedVelocityF0);

public static partial class MapCameraController
{
    private const float NativeFocusEpsilon=1.1920928955078125e-5f;
    private const double NativeYawSentinel=62.83185308;
    public static MainMapCameraControllerRuntimeState CreateRuntime(
        MainMapCameraControllerParameters parameters,MainMapCameraControllerState pose,
        bool currentMaximumYOverride=false)
    {
        var selection=ClassifyZoom(parameters,pose.Distance90);
        return new(pose,parameters.Bounds(selection,currentMaximumYOverride),selection);
    }

    /// <summary>142A0/AB70. Rejection keeps the prior pose/bounds while retaining
    /// the earlier velocity/timestamp reset and live zoom selection stores.</summary>
    public static MainMapCameraControllerApplyResult ApplyImmediate(
        MainMapCameraControllerRuntimeState retained,MainMapCameraControllerParameters parameters,
        MainMapCameraControllerRequest request,bool currentMaximumYOverride=false,
        Func<MainMapCameraTerrainPitchInput,MainMapCameraTerrainPitchResult>? lowBandTerrainQualifier=null)
    {
        if(request.DurationMilliseconds>0)throw new InvalidDataException("Immediate native request requires nonpositive duration.");
        if(retained.RequestMode188!=0)return new(retained,false);
        var reset=retained with {VelocityF0=new(0,0,0,1),VelocityStart100=0,VelocityLast104=0,RequestMeta228=0};
        return ApplyCandidate(reset,parameters,request,currentMaximumYOverride,lowBandTerrainQualifier,true);
    }

    /// <summary>A960 applies AB70 to a local pose and local selection pointers,
    /// then replaces all four request targets with the resulting local pose.</summary>
    public static MainMapCameraControllerRequest MaterializeRequest(
        MainMapCameraControllerRuntimeState retained,MainMapCameraControllerParameters parameters,
        MainMapCameraControllerRequest request,bool currentMaximumYOverride=false,
        Func<MainMapCameraTerrainPitchInput,MainMapCameraTerrainPitchResult>? lowBandTerrainQualifier=null)
    {
        var local=ApplyCandidate(retained,parameters,request,currentMaximumYOverride,lowBandTerrainQualifier,false).State.Pose;
        return request with {Focus10=FocusFloat(local),Distance20=local.Distance90,Yaw28=local.YawA8,Pitch30=local.PitchC0};
    }

    private static MainMapCameraControllerApplyResult ApplyCandidate(
        MainMapCameraControllerRuntimeState retained,MainMapCameraControllerParameters parameters,
        MainMapCameraControllerRequest request,bool maxY,
        Func<MainMapCameraTerrainPitchInput,MainMapCameraTerrainPitchResult>? terrain,bool liveSelection)
    {
        var p=retained.Pose;var b=retained.Bounds;var selected=retained.Selection;
        if(request.Distance20>=0)
        {
            double distance=b.MinimumDistance98>=0&&b.MaximumDistanceA0>=0?
                Clamp(request.Distance20,b.MinimumDistance98,b.MaximumDistanceA0):request.Distance20;
            p=p with {Distance90=distance};selected=ClassifyZoom(parameters,distance);
            var next=parameters.Bounds(selected,maxY) with {MinimumDistance98=b.MinimumDistance98,MaximumDistanceA0=b.MaximumDistanceA0};
            p=ClampFocus(p,next.MinimumFocus70,b.MaximumFocus80);p=ClampFocus(p,next.MinimumFocus70,next.MaximumFocus80);
            p=p with {YawA8=ClampMinimumThenMaximum(p.YawA8,b.MinimumYawB0,b.MaximumYawB8,next.MinimumYawB0,next.MaximumYawB8),
                PitchC0=ClampMinimumThenMaximum(p.PitchC0,b.MinimumPitchC8,b.MaximumPitchD0,next.MinimumPitchC8,next.MaximumPitchD0)};
            b=next;
        }
        if(MathF.Abs(request.Focus10.W)>=NativeFocusEpsilon)
            p=ClampFocus(p with {FocusX=request.Focus10.X,FocusY=request.Focus10.Y,FocusZ=request.Focus10.Z,FocusW=1},b.MinimumFocus70,b.MaximumFocus80);
        if(request.Yaw28<NativeYawSentinel)p=p with {YawA8=Clamp(WrapYaw(request.Yaw28),b.MinimumYawB0,b.MaximumYawB8)};
        double pitch=request.Pitch30>=0?request.Pitch30:p.PitchC0;
        var metadata=retained with {Selection=liveSelection?selected:retained.Selection};
        int terrainBand=liveSelection?selected.Band1E0:retained.Selection.Band1E0;
        if(terrainBand<2)
        {
            if(terrain is null)throw new InvalidDataException("Native low camera band requires explicit current terrain pitch qualification.");
            var q=terrain(new(terrainBand,p,b.MinimumPitchC8,b.MaximumPitchD0,pitch));
            if(string.IsNullOrWhiteSpace(q.Provenance)||!double.IsFinite(q.Pitch))
                throw new InvalidDataException("Terrain pitch qualification requires finite output and owned source provenance.");
            if(!q.Accepted)return new(metadata,false);
            pitch=q.Pitch;
        }
        p=p with {PitchC0=Clamp(pitch,b.MinimumPitchC8,b.MaximumPitchD0)};
        if(!CandidateEyeAllowed(p))return new(metadata,false);
        return new(metadata with {Pose=p,Bounds=b,DirtyCountdownD8=2},true);
    }
    private static double ClampMinimumThenMaximum(double value,double oldMin,double oldMax,double min,double max)
    {
        value=value>=oldMax?oldMax:Math.Max(value,min);
        return value>=max?max:Math.Max(value,min);
    }
    private static MainMapCameraControllerState ClampFocus(MainMapCameraControllerState p,Vector4 min,Vector4 max)=>
        min.W>0&&max.W>0?p with {FocusX=Clamp(p.FocusX,min.X,max.X),FocusY=Clamp(p.FocusY,min.Y,max.Y),FocusZ=Clamp(p.FocusZ,min.Z,max.Z)}:p;
    private static Vector4 FocusFloat(MainMapCameraControllerState p)=>new((float)p.FocusX,(float)p.FocusY,(float)p.FocusZ,(float)p.FocusW);

    /// <summary>175E0 pan's two explicit current Camera rays. A behind-plane
    /// ray retains its native direction and W; it does not reject the request.</summary>
    public static MainMapCameraPanResult PanRequest(MainMapCameraControllerState state,
        Vector4 sharedSecondRayOrigin,Vector4 displacedRayDirection,Vector4 centerRayDirection)
    {
        Vector4 a=PlaneOrFallback(sharedSecondRayOrigin,displacedRayDirection,displacedRayDirection),
            b=PlaneOrFallback(sharedSecondRayOrigin,centerRayDirection,centerRayDirection);
        Vector4 delta=new(S(a.X,b.X),S(a.Y,b.Y),S(a.Z,b.Z),S(a.W,b.W)),f=FocusFloat(state);
        return new(new(0,new(A(f.X,delta.X),A(f.Y,delta.Y),A(f.Z,delta.Z),f.W),-1,NativeYawSentinel,-1,0),
            delta,new(M(delta.X,.5f),M(delta.Y,.5f),M(delta.Z,.5f),delta.W));
    }

    /// <summary>175E0 wheel gates, 16A00 cursor compensation and actual A960
    /// target materialization. SourceReady is the original EFB0 success gate.</summary>
    public static MainMapCameraControllerRequest? BuildWheelRequest(
        MainMapCameraControllerRuntimeState retained,MainMapCameraControllerParameters parameters,
        int signedWheelDelta,MainMapCameraCursorRay cursor,MainMapCameraEyeForward currentCamera,
        bool currentMaximumYOverride=false,
        Func<MainMapCameraTerrainPitchInput,MainMapCameraTerrainPitchResult>? lowBandTerrainQualifier=null)
    {
        if(!cursor.SourceReady||signedWheelDelta==0)return null;
        if(PlaneParameter(cursor.Origin,cursor.Direction)<0f)return null;
        var target=SelectWheelZoomTarget(parameters,retained.Selection,signedWheelDelta);
        float index=A(retained.Selection.Band1E0,D(retained.Selection.Fraction1E4,10000f));
        if(target.Band==(int)MathF.Round(index,MidpointRounding.AwayFromZero))return null;
        float distance=(float)target.Distance;
        if(distance>(float)retained.Bounds.MaximumDistanceA0||(float)retained.Bounds.MinimumDistance98>distance)return null;
        Vector4 focus=FocusFloat(retained.Pose);float old=(float)retained.Pose.Distance90;
        if(old>distance)
        {
            Vector4 point=PlaneOrFallback(cursor.Origin,cursor.Direction,currentCamera.Forward),
                center=PlaneOrFallback(currentCamera.Eye,currentCamera.Forward,currentCamera.Forward);
            float factor=S(1f,D(distance,old));
            focus=new(A(focus.X,M(S(point.X,center.X),factor)),A(focus.Y,M(S(point.Y,center.Y),factor)),
                A(focus.Z,M(S(point.Z,center.Z),factor)),focus.W);
        }
        double pitch=retained.BandPitch.TryGetValue(target.Band,out double cached)?cached:target.Pitch;
        var raw=new MainMapCameraControllerRequest(500,focus,distance,NativeYawSentinel,pitch,1);
        return MaterializeRequest(retained,parameters,raw,currentMaximumYOverride,lowBandTerrainQualifier);
    }
    private static Vector4 PlaneOrFallback(Vector4 origin,Vector4 direction,Vector4 fallback)
    {
        float t=PlaneParameter(origin,direction);
        return t<0?fallback:new(A(M(direction.X,t),origin.X),A(M(direction.Y,t),origin.Y),A(M(direction.Z,t),origin.Z),1);
    }
    private static float PlaneParameter(Vector4 origin,Vector4 direction)
    {
        float numerator=A(A(M(S(0,origin.X),0),S(0,origin.Y)),M(S(0,origin.Z),0));
        float denominator=A(A(M(direction.X,0),direction.Y),M(direction.Z,0));
        return D(numerator,denominator);
    }
}
