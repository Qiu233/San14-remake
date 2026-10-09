#pragma once
// CE 13E00/13CF0/15640/15BB0/175E0: retained CPU input/tick state.
// No frame clock, quaternion interpolation, disk lookup or graphics objects.
#include "CameraState.h"
#include <functional>
#include <optional>

namespace source_camera_motion {
using namespace source_camera;
constexpr float epsilon=1.1920928955078125e-5f;
constexpr double yawSentinel=62.83185308;
struct Profile {std::array<double,7> bandPitch{};int32_t inertiaTimeout=0;};
struct Request {int32_t duration=0;V4 focus{};double distance=-1,yaw=yawSentinel,pitch=-1;int32_t selector=0;};
struct Transition {int32_t duration=0,elapsed=0,lastClock=0;V4 startFocus{},targetFocus{};
    double startDistance=-1,targetDistance=-1,startYaw=-1,yawDelta=0,startPitch=-1,targetPitch=-1;int32_t selector=0;};
struct Runtime {source_camera::Runtime camera{};V4 velocity{0,0,0,1};int32_t velocityStart=0,velocityLast=0;
    int32_t requestMode=0,distanceDirection=0,requestMeta=0;std::array<double,7> bandPitch{};uint32_t bandPitchMask=0;
    std::optional<Transition> transition;};
// The band is the retained selector for A960 and the freshly classified selector
// for live immediate/tick. The ground owner decides whether to accept and pitch.
using TerrainQualifier=std::function<bool(int,const State&,double,double,double,double&)>;
inline Profile loadProfile(const std::filesystem::path& path){
    std::ifstream stream(path,std::ios::binary);if(!stream||read<uint32_t>(stream)!=0x31504d43u)throw std::runtime_error("Camera motion source profile absent");
    Profile result;result.bandPitch=read<std::array<double,7>>(stream);result.inertiaTimeout=read<int32_t>(stream);
    if(stream.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Camera motion source profile trailing data");return result;
}
inline Runtime createRuntime(const source_camera::Profile& camera){Runtime result;result.camera=source_camera::createRuntime(camera);return result;}
inline double wrapYaw(double yaw){while(yaw>6.283185308)yaw=DS(yaw,6.283185308);while(yaw<0)yaw=DA(yaw,6.283185308);return yaw;}
inline V4 focus(const State& pose){return {float(pose.x),float(pose.y),float(pose.z),float(pose.w)};}
inline bool candidateEyeAllowed(const State& pose){auto eye=derivePose(pose)[0];return !(eye.Y<=3000&&eye.Z>S(346780.15625f,M(eye.X,.8549618124961853f)));}
inline bool qualify(Runtime& state,State candidate,const Bounds& limits,int band,double requestedPitch,const TerrainQualifier& terrain){
    double pitch=requestedPitch;
    if(band<2){if(!terrain)throw std::runtime_error("Native camera band0/1 requires current ground qualifier");
        if(!terrain(band,candidate,limits.minPitch,limits.maxPitch,requestedPitch,pitch))return false;}
    candidate.pitch=clamp(pitch,limits.minPitch,limits.maxPitch);
    if(!candidateEyeAllowed(candidate))return false;
    state.camera.pose=candidate;state.camera.bounds=limits;state.camera.dirtyCountdown=2;return true;
}
inline bool applyCandidate(const source_camera::Profile& profile,Runtime& state,const Request& request,
    bool liveSelection,const TerrainQualifier& terrain){
    auto candidate=state.camera.pose;auto limits=state.camera.bounds;auto selected=state.camera.zoom;
    if(request.distance>=0){double distance=limits.minDistance>=0&&limits.maxDistance>=0?clamp(request.distance,limits.minDistance,limits.maxDistance):request.distance;
        candidate.distance=distance;selected=classify(profile,distance);auto next=bounds(profile,selected);
        next.minDistance=limits.minDistance;next.maxDistance=limits.maxDistance;
        clampFocus(candidate,next.minimum,limits.maximum);clampFocus(candidate,next.minimum,next.maximum);
        candidate.yaw=clampMinimumThenMaximum(candidate.yaw,limits.maxYaw,next.minYaw,next.maxYaw);
        candidate.pitch=clampMinimumThenMaximum(candidate.pitch,limits.maxPitch,next.minPitch,next.maxPitch);limits=next;}
    if(std::fabs(request.focus.W)>=epsilon){candidate.x=request.focus.X;candidate.y=request.focus.Y;candidate.z=request.focus.Z;candidate.w=1;clampFocus(candidate,limits.minimum,limits.maximum);}
    if(request.yaw<yawSentinel)candidate.yaw=clamp(wrapYaw(request.yaw),limits.minYaw,limits.maxYaw);
    double requested=request.pitch>=0?request.pitch:candidate.pitch;
    if(liveSelection)state.camera.zoom=selected;
    return qualify(state,candidate,limits,state.camera.zoom.band,requested,terrain);
}
inline bool applyImmediate(const source_camera::Profile& profile,Runtime& state,const Request& request,const TerrainQualifier& terrain={}){
    if(state.requestMode!=0)return false;
    if(request.duration>0)throw std::runtime_error("Immediate camera input needs nonpositive duration");
    state.velocity={0,0,0,1};state.velocityStart=state.velocityLast=state.requestMeta=0;
    return applyCandidate(profile,state,request,true,terrain);
}
inline Request materialize(const source_camera::Profile& profile,const Runtime& retained,Request request,const TerrainQualifier& terrain={}){
    Runtime local=retained;applyCandidate(profile,local,request,false,terrain);
    request.focus=focus(local.camera.pose);request.distance=local.camera.pose.distance;request.yaw=local.camera.pose.yaw;request.pitch=local.camera.pose.pitch;return request;
}
inline bool queueTimed(Runtime& state,const Request& request,int32_t sourceClock){
    if(state.requestMode!=0)return false;
    if(request.duration<=0)throw std::runtime_error("Native timed camera input needs positive duration");
    Transition t;t.duration=request.duration;t.lastClock=sourceClock;t.selector=request.selector;
    if(std::fabs(request.focus.W)>=epsilon){t.startFocus=focus(state.camera.pose);t.targetFocus=request.focus;}
    if(request.distance>=0){t.startDistance=state.camera.pose.distance;t.targetDistance=request.distance;}
    if(request.yaw<yawSentinel){t.startYaw=wrapYaw(state.camera.pose.yaw);auto end=wrapYaw(request.yaw);t.yawDelta=DS(end,t.startYaw);
        if(std::fabs(t.yawDelta)>=3.141592654)t.yawDelta=t.yawDelta>0?DS(t.yawDelta,6.283185308):DA(t.yawDelta,6.283185308);}
    if(request.pitch>=0){t.startPitch=state.camera.pose.pitch;t.targetPitch=request.pitch;}
    state.velocity={0,0,0,1};state.velocityStart=state.velocityLast=state.requestMeta=0;state.requestMode=request.selector;state.transition=t;return true;
}
// DWORD addition/subtraction followed by source signed32 interpretation. Avoid
// C++ signed overflow while retaining the actual source wrap and negative delta.
inline int32_t signedWord(uint32_t word){int32_t result;std::memcpy(&result,&word,4);return result;}
inline int32_t elapsedAt(int32_t elapsed,int32_t clock,int32_t last){return signedWord(uint32_t(elapsed)+uint32_t(clock)-uint32_t(last));}
inline double lerpDouble(double a,double b,float t){return DA(DM(S(1,t),a),DM(t,b));}
inline float lerpFloat(float a,float b,float t){return A(M(S(1,t),a),M(t,b));}
inline void updatePitchAnchors(const source_camera::Profile& camera,const Profile& profile,Runtime& state){
    if(state.camera.dirtyCountdown==0||state.camera.zoom.fraction!=0)return;
    int band=state.camera.zoom.band;state.bandPitch[band]=state.camera.pose.pitch;state.bandPitchMask|=1u<<band;
    auto getPitch=[&](int b){return state.bandPitchMask&(1u<<b)?state.bandPitch[b]:profile.bandPitch[b];};
    auto adjust=[&](int target,int start,int end){double slope=DM(DS(getPitch(end),getPitch(start)),.5),
        predicted=DA(DS(getPitch(start),DM(start,slope)),DM(target,slope)),difference=DS(getPitch(target),predicted);
        if(std::fabs(difference)<.0872664675116539)return;
        double requested=DA(predicted,difference>0?.0872664675116539:-.0872664675116539);auto limits=bounds(camera,{target,0,float(target)});
        state.bandPitch[target]=clamp(float(requested),limits.minPitch,limits.maxPitch);state.bandPitchMask|=1u<<target;};
    switch(band){case 0:adjust(1,0,2);break;case 1:adjust(2,1,3);adjust(0,1,2);break;
        case 2:adjust(3,1,2);adjust(1,0,2);break;case 3:adjust(2,1,3);break;}
}
inline void tick(const source_camera::Profile& camera,const Profile& profile,Runtime& state,
    int32_t inertiaClock,int32_t animationClock,int32_t inertiaTimeout,const TerrainQualifier& terrain={}){
    state.distanceDirection=0;if(state.camera.dirtyCountdown)state.camera.dirtyCountdown--;
    uint32_t end=uint32_t(state.velocityStart)+uint32_t(inertiaTimeout);
    if(state.velocityStart!=0&&uint32_t(state.velocityLast)<end){
        state.velocityLast=inertiaClock;float decay=uint32_t(inertiaClock)<end?
            std::clamp(S(1,D(float(uint32_t(inertiaClock)-uint32_t(state.velocityStart)),float(inertiaTimeout))),0.f,1.f):0.f;
        auto f=focus(state.camera.pose),v=state.velocity;auto candidate=state.camera.pose;
        candidate.x=A(f.X,M(decay,v.X));candidate.y=f.Y;candidate.z=A(f.Z,M(decay,v.Z));candidate.w=1;
        clampFocus(candidate,state.camera.bounds.minimum,state.camera.bounds.maximum);
        qualify(state,candidate,state.camera.bounds,state.camera.zoom.band,candidate.pitch,terrain);
        if(state.velocityStart==0||uint32_t(state.velocityLast)>=end){state.velocity={0,0,0,1};state.velocityStart=state.velocityLast=0;}
    }
    if(state.transition&&state.transition->elapsed<state.transition->duration){
        auto& t=*state.transition;t.elapsed=std::min(t.duration,elapsedAt(t.elapsed,animationClock,t.lastClock));t.lastClock=animationClock;
        float time=std::min(D(float(t.elapsed),float(t.duration)),1.f),ease=S(1,M(S(1,time),S(1,time))),s=std::clamp(ease,0.f,1.f);
        double blend=state.camera.pose.distance;if(t.startDistance>=0&&t.targetDistance>=0)blend=lerpDouble(t.startDistance,t.targetDistance,s);
        auto old=state.camera.bounds;double distance=old.minDistance>=0&&old.maxDistance>=0?clamp(blend,old.minDistance,old.maxDistance):blend;
        auto selected=classify(camera,distance);auto limits=bounds(camera,selected);limits.minDistance=old.minDistance;limits.maxDistance=old.maxDistance;
        auto candidate=state.camera.pose;candidate.distance=distance;clampFocus(candidate,limits.minimum,old.maximum);clampFocus(candidate,limits.minimum,limits.maximum);
        candidate.yaw=clampMinimumThenMaximum(candidate.yaw,old.maxYaw,limits.minYaw,limits.maxYaw);
        candidate.pitch=clampMinimumThenMaximum(candidate.pitch,old.maxPitch,limits.minPitch,limits.maxPitch);
        state.distanceDirection=std::fabs(DS(state.camera.pose.distance,blend))<epsilon?0:state.camera.pose.distance<=blend?2:1;
        state.camera.zoom=selected;
        if(std::fabs(t.startFocus.W)>=epsilon&&std::fabs(t.targetFocus.W)>=epsilon){candidate.x=lerpFloat(t.startFocus.X,t.targetFocus.X,s);
            candidate.y=lerpFloat(t.startFocus.Y,t.targetFocus.Y,s);candidate.z=lerpFloat(t.startFocus.Z,t.targetFocus.Z,s);candidate.w=1;clampFocus(candidate,limits.minimum,limits.maximum);}
        if(std::fabs(t.yawDelta)>=epsilon)candidate.yaw=DA(t.startYaw,DM(s,t.yawDelta));candidate.yaw=clamp(candidate.yaw,limits.minYaw,limits.maxYaw);
        double requested=t.startPitch>=0&&t.targetPitch>=0?lerpDouble(t.startPitch,t.targetPitch,s):candidate.pitch;
        qualify(state,candidate,limits,state.camera.zoom.band,requested,terrain);
        if(t.elapsed>=t.duration){state.transition.reset();state.requestMode=0;}
    }
    updatePitchAnchors(camera,profile,state);
}
inline Frame prepareCurrent(const source_camera::Profile& camera,const Runtime& state){return source_camera::prepareCurrent(camera,state.camera);}

struct CursorRay {V4 origin{},direction{};bool ready=false;};
struct EyeForward {V4 eye{},forward{};};
inline Matrix affineInverse(Matrix m){
    float a=m[0],b=m[1],c=m[2],d=m[4],e=m[5],f=m[6],g=m[8],h=m[9],i=m[10];
    float c00=S(M(e,i),M(f,h)),c01=S(M(c,h),M(b,i)),c02=S(M(b,f),M(c,e));
    float det=A(A(M(a,c00),M(d,c01)),M(g,c02));if(det==0)throw std::runtime_error("Source cursor inverse singular stack branch unclosed");
    float c10=S(M(g,f),M(d,i)),c11=S(M(a,i),M(g,c)),c12=S(M(d,c),M(a,f)),
        c20=S(M(d,h),M(g,e)),c21=S(M(g,b),M(a,h)),c22=S(M(a,e),M(d,b)),factor=D(1,det);
    float tx=flip(m[12]),ty=flip(m[13]),tz=flip(m[14]);
    return {M(factor,c00),M(factor,c01),M(factor,c02),0,M(c10,factor),M(c11,factor),M(c12,factor),0,
        M(c20,factor),M(c21,factor),M(c22,factor),0,
        M(A(A(M(ty,c10),M(tx,c00)),M(tz,c20)),factor),M(A(A(M(ty,c11),M(tx,c01)),M(tz,c21)),factor),
        M(A(A(M(ty,c12),M(tx,c02)),M(tz,c22)),factor),1};
}
inline CursorRay cursorRay(const Prefix& prefix,int32_t canonicalX,int32_t canonicalY){
    auto inv=affineInverse(get<Matrix>(prefix.data()+0x100));
    float x=D(float(signedWord(uint32_t(canonicalX)-960u)),960),y=D(float(signedWord(540u-uint32_t(canonicalY))),540);
    float ratio=D(get<float>(prefix.data()+0x144),get<float>(prefix.data()+0x140)),farX=M(ratio,x),farY=M(ratio,y);
    auto nearValue=[&](int c){return A(A(A(M(inv[4+c],y),M(inv[c],x)),M(inv[8+c],0)),inv[12+c]);};
    auto farValue=[&](int c){return A(S(A(M(inv[4+c],farY),M(inv[c],farX)),inv[8+c]),inv[12+c]);};
    float dx=S(farValue(0),nearValue(0)),dy=S(farValue(1),nearValue(1)),dz=S(farValue(2),nearValue(2)),
        factor=D(1,std::sqrt(A(A(M(dy,dy),M(dx,dx)),M(dz,dz))));
    CursorRay result{get<V4>(prefix.data()+0x10),{M(dx,factor),M(dy,factor),M(dz,factor),farValue(3)},true};
    for(float v:{result.origin.X,result.origin.Y,result.origin.Z,result.origin.W,result.direction.X,result.direction.Y,result.direction.Z,result.direction.W})
        if(std::isnan(v))return {{0,0,0,1},{0,0,0,1},false};return result;
}
inline EyeForward eyeForward(const Frame& frame){auto forward=normalizeDirection(rotateVector(frame.quaternion,{0,0,1,0}));return {frame.eye,{forward.X,forward.Y,forward.Z,1}};}
inline float planeParameter(V4 origin,V4 direction){return D(A(A(M(S(0,origin.X),0),S(0,origin.Y)),M(S(0,origin.Z),0)),A(A(M(direction.X,0),direction.Y),M(direction.Z,0)));}
inline V4 planeOrFallback(V4 origin,V4 direction,V4 fallback){float t=planeParameter(origin,direction);return t<0?fallback:V4{A(M(direction.X,t),origin.X),A(M(direction.Y,t),origin.Y),A(M(direction.Z,t),origin.Z),1};}
inline std::optional<Request> buildWheel(const source_camera::Profile& camera,const Profile& profile,const Runtime& state,
    int32_t signedDelta,const CursorRay& cursor,const EyeForward& current,const TerrainQualifier& terrain={}){
    if(!cursor.ready||signedDelta==0||planeParameter(cursor.origin,cursor.direction)<0)return {};
    float index=A(float(state.camera.zoom.band),D(float(state.camera.zoom.fraction),10000));int rounded=int(std::round(index));
    int band=std::clamp(rounded+(signedDelta>0?-1:1),0,6);if(band==rounded)return {};
    float distance=camera.anchors[band];if(distance>float(state.camera.bounds.maxDistance)||float(state.camera.bounds.minDistance)>distance)return {};
    auto target=focus(state.camera.pose);float old=float(state.camera.pose.distance);
    if(old>distance){auto point=planeOrFallback(cursor.origin,cursor.direction,current.forward),center=planeOrFallback(current.eye,current.forward,current.forward);
        float factor=S(1,D(distance,old));target={A(target.X,M(S(point.X,center.X),factor)),A(target.Y,M(S(point.Y,center.Y),factor)),A(target.Z,M(S(point.Z,center.Z),factor)),target.W};}
    double pitch=state.bandPitchMask&(1u<<band)?state.bandPitch[band]:profile.bandPitch[band];
    return materialize(camera,state,{500,target,distance,yawSentinel,pitch,1},terrain);
}
inline bool queueWheel(const source_camera::Profile& camera,const Profile& profile,Runtime& state,int32_t delta,
    const Frame& current,int32_t canonicalX,int32_t canonicalY,int32_t sourceClock,const TerrainQualifier& terrain={}){
    if(state.requestMode!=0)return false;
    auto request=buildWheel(camera,profile,state,delta,cursorRay(current.prefix,canonicalX,canonicalY),eyeForward(current),terrain);
    return request&&queueTimed(state,*request,sourceClock);
}
inline void savePanVelocity(Runtime& state,V4 saved,bool sampleOne,int32_t sourceClock){if(sampleOne&&state.requestMode==0){state.velocity=saved;state.velocityStart=state.velocityLast=sourceClock;}}
struct CanonicalPoint {int32_t x,y;};
inline CanonicalPoint canonicalCursor(int32_t clientX,int32_t clientY,int32_t clientWidth,int32_t clientHeight,int32_t targetWidth,int32_t targetHeight){
    if(clientWidth==0)return {clientX,clientY};float w=float(targetWidth),h=float(targetHeight);
    float x=D(M(float(clientX),w),float(clientWidth)),y=D(M(float(clientY),h),float(clientHeight));
    float nineW=M(w,9),sixteenH=M(h,16);
    if(nineW>sixteenH){float offset=M(S(w,D(M(h,16),9)),.5f);x=D(M(S(x,offset),1080),h);y=D(M(y,1080),h);}
    else if(nineW<sixteenH){x=D(M(x,1920),w);float offset=M(S(h,M(M(w,9),.0625f)),.5f);y=D(M(S(y,offset),1920),w);}
    else{x=D(M(x,1920),w);y=D(M(y,1080),h);}
    return {_mm_cvttss_si32(_mm_set_ss(x)),_mm_cvttss_si32(_mm_set_ss(y))};
}
inline CanonicalPoint canonicalDelta(CanonicalPoint current,CanonicalPoint previous,bool enabled){return enabled?
    CanonicalPoint{signedWord(uint32_t(current.x)-uint32_t(previous.x)),signedWord(uint32_t(current.y)-uint32_t(previous.y))}:CanonicalPoint{0,0};}
// Original 934EE0: selected timer manager counter*1000/frequency, returned as
// DWORD by caller. The held-QPC caller owns the raw counter and frequency.
inline int32_t sourceClock(uint64_t counter,uint64_t frequency){return signedWord(uint32_t((counter*uint64_t(1000))/frequency));}
}
