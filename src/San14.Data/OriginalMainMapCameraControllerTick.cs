using System.Numerics;

namespace San14.Data;

public static partial class OriginalMainMapCameraController
{
    /// <summary>13E00/13CF0 capture a previously A960-materialized target.
    /// The wheel's selector1 blocks further V10 requests until completion.</summary>
    public static OriginalMainMapCameraControllerRuntimeState QueuePreparedTimedRequest(
        OriginalMainMapCameraControllerRuntimeState retained,OriginalMainMapCameraControllerRequest prepared,int sourceClock)
    {
        if(retained.RequestMode188!=0)return retained;
        if(prepared.DurationMilliseconds<=0)throw new InvalidDataException("Native timed request requires positive duration.");
        Vector4 start=Vector4.Zero,target=Vector4.Zero;
        if(MathF.Abs(prepared.Focus10.W)>=NativeFocusEpsilon){start=FocusFloat(retained.Pose);target=prepared.Focus10;}
        double distanceStart=prepared.Distance20>=0?retained.Pose.Distance90:-1,
            distanceEnd=prepared.Distance20>=0?prepared.Distance20:-1;
        double yawStart=prepared.Yaw28<NativeYawSentinel?WrapYaw(retained.Pose.YawA8):-1,
            yawEnd=prepared.Yaw28<NativeYawSentinel?WrapYaw(prepared.Yaw28):-1,
            delta=yawStart>=0&&yawEnd>=0?ShortestYawDelta(yawStart,yawEnd):0;
        double pitchStart=prepared.Pitch30>=0?retained.Pose.PitchC0:-1,pitchEnd=prepared.Pitch30>=0?prepared.Pitch30:-1;
        return retained with {VelocityF0=new(0,0,0,1),VelocityStart100=0,VelocityLast104=0,RequestMeta228=0,
            RequestMode188=prepared.Selector38,Transition=new(prepared.DurationMilliseconds,0,sourceClock,start,target,
                distanceStart,distanceEnd,yawStart,delta,pitchStart,pitchEnd,prepared.Selector38)};
    }

    /// <summary>Source sample-one pan predicate is supplied by the input owner.
    /// Native stores this velocity only while mode188 is zero.</summary>
    public static OriginalMainMapCameraControllerRuntimeState SavePanVelocity(
        OriginalMainMapCameraControllerRuntimeState state,OriginalMainMapCameraPanResult pan,
        bool sourceSampleOnePredicate,int sourceClock) => sourceSampleOnePredicate&&state.RequestMode188==0?
            state with {VelocityF0=pan.SavedVelocityF0,VelocityStart100=sourceClock,VelocityLast104=sourceClock}:state;

    /// <summary>15BB0 finite inertial and timed branches. Clock samples and the
    /// current timeout global are explicit owned inputs. Rejected candidates
    /// retain earlier clock, band and direction metadata writes.</summary>
    public static OriginalMainMapCameraControllerRuntimeState Tick(
        OriginalMainMapCameraControllerRuntimeState retained,OriginalMainMapCameraControllerParameters parameters,
        int inertiaClock,int animationClock,int currentInertiaTimeoutMilliseconds,
        bool currentMaximumYOverride=false,
        Func<OriginalMainMapCameraTerrainPitchInput,OriginalMainMapCameraTerrainPitchResult>? lowBandTerrainQualifier=null)
    {
        var state=retained with {DistanceDirection1E8=0,
            DirtyCountdownD8=retained.DirtyCountdownD8==0?0:retained.DirtyCountdownD8-1};
        uint end=unchecked((uint)(state.VelocityStart100+currentInertiaTimeoutMilliseconds));
        if(state.VelocityStart100!=0&&(uint)state.VelocityLast104<end)
        {
            state=state with {VelocityLast104=inertiaClock};
            float decay=(uint)inertiaClock<end?Math.Clamp(S(1f,D(unchecked((uint)(inertiaClock-state.VelocityStart100)),currentInertiaTimeoutMilliseconds)),0f,1f):0f;
            var f=FocusFloat(state.Pose);var v=state.VelocityF0;
            var candidate=ClampFocus(state.Pose with {FocusX=A(f.X,M(decay,v.X)),FocusY=f.Y,
                FocusZ=A(f.Z,M(decay,v.Z)),FocusW=1},state.Bounds.MinimumFocus70,state.Bounds.MaximumFocus80);
            state=QualifyAndCommit(state,candidate,state.Bounds,lowBandTerrainQualifier);
            if(state.VelocityStart100==0||(uint)state.VelocityLast104>=end)
                state=state with {VelocityF0=new(0,0,0,1),VelocityStart100=0,VelocityLast104=0};
        }
        var transition=state.Transition;
        if(transition is not null&&transition.Elapsed<transition.Duration)
        {
            int elapsed=Math.Min(transition.Duration,unchecked(transition.Elapsed+animationClock-transition.LastClock));
            transition=transition with {Elapsed=elapsed,LastClock=animationClock};
            state=state with {Transition=transition};
            float time=Math.Min(D(elapsed,transition.Duration),1f),ease=S(1f,M(S(1f,time),S(1f,time))),s=Math.Clamp(ease,0f,1f);
            double blend=state.Pose.Distance90;
            if(transition.StartDistance>=0&&transition.TargetDistance>=0)blend=LerpDouble(transition.StartDistance,transition.TargetDistance,s);
            double distance=state.Bounds.MinimumDistance98>=0&&state.Bounds.MaximumDistanceA0>=0?
                Clamp(blend,state.Bounds.MinimumDistance98,state.Bounds.MaximumDistanceA0):blend;
            var selected=ClassifyZoom(parameters,distance);
            var oldBounds=state.Bounds;
            var bounds=parameters.Bounds(selected,currentMaximumYOverride) with {MinimumDistance98=oldBounds.MinimumDistance98,MaximumDistanceA0=oldBounds.MaximumDistanceA0};
            var p=state.Pose with {Distance90=distance};
            p=ClampFocus(p,bounds.MinimumFocus70,oldBounds.MaximumFocus80);p=ClampFocus(p,bounds.MinimumFocus70,bounds.MaximumFocus80);
            p=p with {YawA8=ClampMinimumThenMaximum(p.YawA8,oldBounds.MinimumYawB0,oldBounds.MaximumYawB8,bounds.MinimumYawB0,bounds.MaximumYawB8),
                PitchC0=ClampMinimumThenMaximum(p.PitchC0,oldBounds.MinimumPitchC8,oldBounds.MaximumPitchD0,bounds.MinimumPitchC8,bounds.MaximumPitchD0)};
            int direction=Math.Abs(DS(state.Pose.Distance90,blend))<NativeFocusEpsilon?0:state.Pose.Distance90<=blend?2:1;
            state=state with {Selection=selected,DistanceDirection1E8=direction};
            if(MathF.Abs(transition.StartFocus.W)>=NativeFocusEpsilon&&MathF.Abs(transition.TargetFocus.W)>=NativeFocusEpsilon)
            {
                var a=transition.StartFocus;var b=transition.TargetFocus;
                p=ClampFocus(p with {FocusX=InterpolateFloat(a.X,b.X,s),FocusY=InterpolateFloat(a.Y,b.Y,s),
                    FocusZ=InterpolateFloat(a.Z,b.Z,s),FocusW=1},bounds.MinimumFocus70,bounds.MaximumFocus80);
            }
            if(Math.Abs(transition.YawDelta)>=NativeFocusEpsilon)
                p=p with {YawA8=DA(transition.StartYaw,DM(s,transition.YawDelta))};
            p=p with {YawA8=Clamp(p.YawA8,bounds.MinimumYawB0,bounds.MaximumYawB8)};
            double requestedPitch=transition.StartPitch>=0&&transition.TargetPitch>=0?
                LerpDouble(transition.StartPitch,transition.TargetPitch,s):p.PitchC0;
            state=QualifyAndCommit(state,p,bounds,lowBandTerrainQualifier,requestedPitch);
            if(elapsed>=transition.Duration)state=state with {Transition=null,RequestMode188=0};
        }
        return UpdatePitchAnchors(state,parameters);
    }
    private static OriginalMainMapCameraControllerRuntimeState QualifyAndCommit(
        OriginalMainMapCameraControllerRuntimeState state,OriginalMainMapCameraControllerState candidate,
        OriginalMainMapCameraControllerBounds bounds,
        Func<OriginalMainMapCameraTerrainPitchInput,OriginalMainMapCameraTerrainPitchResult>? terrain,double? requestedPitch=null)
    {
        double pitch=requestedPitch??candidate.PitchC0;
        if(state.Selection.Band1E0<2)
        {
            if(terrain is null)throw new InvalidDataException("Native low camera band requires explicit current terrain pitch qualification.");
            var result=terrain(new(state.Selection.Band1E0,candidate,bounds.MinimumPitchC8,bounds.MaximumPitchD0,pitch));
            if(string.IsNullOrWhiteSpace(result.Provenance)||!double.IsFinite(result.Pitch))
                throw new InvalidDataException("Terrain pitch qualification requires finite output and owned source provenance.");
            if(!result.Accepted)return state;
            pitch=result.Pitch;
        }
        candidate=candidate with {PitchC0=Clamp(pitch,bounds.MinimumPitchC8,bounds.MaximumPitchD0)};
        return CandidateEyeAllowed(candidate)?state with {Pose=candidate,Bounds=bounds,DirtyCountdownD8=2}:state;
    }
    private static double LerpDouble(double a,double b,float t)=>DA(DM(S(1f,t),a),DM(t,b));
    private static double ShortestYawDelta(double a,double b)
    {
        double delta=DS(b,a);
        return Math.Abs(delta)<3.141592654?delta:delta>0?DS(delta,6.283185308):DA(delta,6.283185308);
    }
    private static OriginalMainMapCameraControllerRuntimeState UpdatePitchAnchors(
        OriginalMainMapCameraControllerRuntimeState state,OriginalMainMapCameraControllerParameters parameters)
    {
        if(state.DirtyCountdownD8==0||state.Selection.Fraction1E4!=0)return state;
        int band=state.Selection.Band1E0;var anchors=state.BandPitch.SetItem(band,state.Pose.PitchC0);
        double Get(int i)=>anchors.TryGetValue(i,out double v)?v:parameters.ZoomBandPitch(i);
        void Adjust(int target,int start,int end)
        {
            double slope=DM(DS(Get(end),Get(start)),.5),predicted=DA(DS(Get(start),DM(start,slope)),DM(target,slope)),difference=DS(Get(target),predicted);
            if(Math.Abs(difference)<0.0872664675116539)return;
            double requested=DA(predicted,difference>0?0.0872664675116539:-0.0872664675116539);
            var bounds=parameters.Bounds(new(target,0,target),false);
            anchors=anchors.SetItem(target,Clamp((float)requested,bounds.MinimumPitchC8,bounds.MaximumPitchD0));
        }
        switch(band)
        {
            case 0:Adjust(1,0,2);break;
            case 1:Adjust(2,1,3);Adjust(0,1,2);break;
            case 2:Adjust(3,1,2);Adjust(1,0,2);break;
            case 3:Adjust(2,1,3);break;
        }
        return state with {BandPitch=anchors};
    }
}
