#pragma once
// CPU-only source Main mode0 publication + ordinary Landscape producer.
// Ported from San14.Data's audited CE scalar/SSE kernels; no DirectX camera substitute.
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <vector>
#include <immintrin.h>
#include "CameraTransform.h"

namespace source_camera {
using Prefix=std::array<uint8_t,0x3c0>;
struct State {double x,y,z,w,distance,yaw,pitch;};
struct Zoom {int band,fraction;float nativeReturnedIndex;};
struct Bounds {V3 minimum,maximum;double minDistance,maxDistance,minYaw,maxYaw,minPitch,maxPitch;};
struct Request {V4 focus;double distance,yaw,pitch;}; // source duration=0 / RequestMode188=0 only.
struct Runtime {State pose;Bounds bounds;Zoom zoom;uint32_t dirtyCountdown=2;};
struct Node {int index,level,x,z;float minimum,maximum,size;};
struct Profile {
    Prefix staticPrefix{};CameraOptics optics{};float halfFovTangent=0,lookDistance=0,lodScale=0;
    ClipDepthRange clipDepthRange=ClipDepthRange::MinusOneToOne;int heightWidth=0,controlWidth=0,levels=0;
    std::array<float,7> anchors{};std::array<std::array<float,10>,7> bounds{};
    State initial{};std::vector<Node> nodes;
};
struct Plane {V4 equation;int supportX,supportY,supportZ;};
struct Instance {float origin[3],extent[3],uv[4];uint32_t edgeFlags,gridSize;float controlFrequency,controlCount;};
static_assert(sizeof(Instance)==56,"Original Terrain instance ABI");
struct DrawRow {int node,group;float distance;uint32_t edgeFlags;};
static_assert(sizeof(DrawRow)==16,"CPU comparison draw row ABI");
struct Batch {int group,start,count,coarse;};
struct Frame:CameraTransform {
    // Compatibility packet for existing pass writers; CPU camera consumers use
    // CameraTransform. Prepared once when a frame is published.
    Prefix prefix{};std::array<Plane,6> planes{};V4 quaternion{};
    int selectedCount=0,culledCount=0;std::vector<Instance> instances;std::vector<DrawRow> rows;std::vector<Batch> batches;};
struct GroundHeightProfile {
    uint32_t preparationBranch=0,outerOwnerPresent=1;
    float heightScale=0,originX=0,originZ=0,spacing=0,minimum=0,maximum=0;
    int width=0,height=0;std::vector<uint32_t> packed;
};
struct TerrainPitchInput {int nativeBand;State candidateBeforeRequestedPitch;double minimumPitch,maximumPitch,requestedPitch;};
struct TerrainPitchResult {bool accepted;double pitch;};

// Separate operations retain source roundings even in an optimized consumer.
__declspec(noinline) inline float A(float a,float b){return a+b;}
__declspec(noinline) inline float S(float a,float b){return a-b;}
__declspec(noinline) inline float M(float a,float b){return a*b;}
__declspec(noinline) inline float D(float a,float b){return a/b;}
__declspec(noinline) inline double DA(double a,double b){return a+b;}
__declspec(noinline) inline double DS(double a,double b){return a-b;}
__declspec(noinline) inline double DM(double a,double b){return a*b;}
inline float flip(float f){uint32_t b;std::memcpy(&b,&f,4);b^=0x80000000u;std::memcpy(&f,&b,4);return f;}
template<class T> inline T get(const uint8_t* p){T v;std::memcpy(&v,p,sizeof v);return v;}
template<class T> inline void put(uint8_t* p,const T& v){std::memcpy(p,&v,sizeof v);}
template<class T> inline T read(std::ifstream& s){T v{};s.read(reinterpret_cast<char*>(&v),sizeof v);if(!s)throw std::runtime_error("Incomplete camera source profile");return v;}
// CCP1 and published-camera packet decoding is confined to this boundary.
inline CameraOptics decodeCameraOptics(const Prefix& packet){
    const auto flags=get<uint32_t>(packet.data()+0x158);
    return {get<float>(packet.data()+0x140),get<float>(packet.data()+0x144),
        get<float>(packet.data()+0x14c),get<float>(packet.data()+0x150),
        flags&1?ProjectionKind::Orthographic:ProjectionKind::Perspective,(flags&2)!=0};
}
inline CameraTransform decodeCameraTransform(const Prefix& packet){
    return {get<V4>(packet.data()+0x10),get<V4>(packet.data()+0x20),get<V4>(packet.data()+0x30),
        get<Matrix>(packet.data()+0x40),get<Matrix>(packet.data()+0x80),get<Matrix>(packet.data()+0xc0),
        get<Matrix>(packet.data()+0x100),decodeCameraOptics(packet)};
}
inline Prefix encodePreparedCamera(const Prefix& formatTemplate,const CameraTransform& camera){
    Prefix packet=formatTemplate;
    put(packet.data()+0x10,camera.eye);put(packet.data()+0x20,camera.target);put(packet.data()+0x30,camera.up);
    put(packet.data()+0x40,camera.unrolledView);put(packet.data()+0x80,camera.view);
    put(packet.data()+0xc0,camera.projection);put(packet.data()+0x100,camera.worldToClip);
    put(packet.data()+0x140,camera.optics.nearPlane);put(packet.data()+0x144,camera.optics.farPlane);
    put(packet.data()+0x14c,camera.optics.aspectRatio);put(packet.data()+0x150,camera.optics.roll);
    auto flags=get<uint32_t>(packet.data()+0x158)&~3u;
    if(camera.optics.projectionKind==ProjectionKind::Orthographic)flags|=1;
    if(camera.optics.reversedDepth)flags|=2;
    put(packet.data()+0x158,flags);
    packet[0x160]=packet[0x161]=0;
    return packet;
}
inline Profile loadProfile(const std::filesystem::path& path){
    std::ifstream s(path,std::ios::binary);if(!s||read<uint32_t>(s)!=0x31504343u)throw std::runtime_error("Camera source profile CCP1 absent");
    Profile p;p.staticPrefix=read<Prefix>(s);p.optics=decodeCameraOptics(p.staticPrefix);p.halfFovTangent=read<float>(s);p.lookDistance=read<float>(s);
    const auto clipFlags=read<uint32_t>(s);
    p.clipDepthRange=clipFlags&2?ClipDepthRange::ZeroToOne:ClipDepthRange::MinusOneToOne;
    p.lodScale=read<float>(s);p.heightWidth=read<int>(s);p.controlWidth=read<int>(s);
    p.anchors=read<std::array<float,7>>(s);p.bounds=read<std::array<std::array<float,10>,7>>(s);p.initial=read<State>(s);
    auto h=read<std::array<uint8_t,20>>(s);p.levels=get<uint16_t>(h.data()+18);int count=get<uint16_t>(h.data()+16);
    if(p.levels!=6||count!=1365||p.heightWidth<2||p.controlWidth<2)throw std::runtime_error("Unsupported source tree/profile shape");
    for(int i=0;i<count;i++){auto b=read<std::array<uint8_t,32>>(s);p.nodes.push_back({i,b[1],b[2],b[3],get<float>(b.data()+8),get<float>(b.data()+12),get<float>(b.data()+16)});}
    if(s.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Trailing camera source profile data");return p;
}
// App/Main owner+288 is not Camera prefix+288 (the latter is TransitionSpan).
// This publication stays separate from prepareCurrent's raw camera schema.
struct CurrentRenderMode {int sceneMode;float zoomLevel;};
inline int classifyRenderMode(Zoom zoom){
    const float index=A(static_cast<float>(zoom.band),D(static_cast<float>(zoom.fraction),10000));
    return index<2.5f?0:index<3.5f?1:index<4.5f?2:index<5.5f?3:4;
}
inline CurrentRenderMode prepareCurrentRenderMode(Zoom zoom){
    const float index=A(static_cast<float>(zoom.band),D(static_cast<float>(zoom.fraction),10000));
    const int mode=classifyRenderMode(zoom);
    if(mode>1)throw std::runtime_error("Main render mode2+ App/group mutation is outside current mode0/1 publication");
    return {mode,index};
}
inline int currentRenderMode(Zoom zoom){return prepareCurrentRenderMode(zoom).sceneMode;}
struct RenderModePrefixProfile {uint32_t adaptedScale,bloomWeight,lensFlareWeight,selectedCloudy,cloudyChangedAltitude,initialPriorCloudy;};
struct RenderModePrefixState {uint32_t priorCloudy430=0;};
inline RenderModePrefixProfile loadRenderModePrefixProfile(const std::filesystem::path& path){
    std::ifstream input(path,std::ios::binary);if(!input||read<uint32_t>(input)!=0x31504d52u)throw std::runtime_error("Main render direct prefix profile absent");
    auto result=read<RenderModePrefixProfile>(input);if(input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Main render direct prefix profile trailing bytes");return result;
}
inline RenderModePrefixState createRenderModePrefixState(const RenderModePrefixProfile& p){return {p.initialPriorCloudy};}
inline Prefix prepareCurrentRenderModeDirectPrefix(const RenderModePrefixProfile& profile,
    RenderModePrefixState& state,CurrentRenderMode mode,const Prefix& rawCamera){
    if(mode.sceneMode<0||mode.sceneMode>1)throw std::runtime_error("Main direct prefix needs supported current render mode0/1");
    Prefix result=rawCamera;float previous,current;
    std::memcpy(&previous,&state.priorCloudy430,4);std::memcpy(&current,&profile.selectedCloudy,4);
    // Original17BD0/ProjectDirectWrites under held quality0/AppFlagsE2/selected season.
    put(result.data()+0x1c4,profile.adaptedScale);put(result.data()+0x180,profile.bloomWeight);
    put(result.data()+0x188,profile.lensFlareWeight);put(result.data()+0x1d0,profile.selectedCloudy);
    const uint32_t altitude=previous==current?0:profile.cloudyChangedAltitude;put(result.data()+0x210,altitude);
    state.priorCloudy430=profile.selectedCloudy;return result;
}
inline GroundHeightProfile loadGroundHeightProfile(const std::filesystem::path& directory){
    std::ifstream input(directory/"profile.bin",std::ios::binary);if(!input)throw std::runtime_error("Explicit current Field GroundHeight profile absent");
    GroundHeightProfile p;p.preparationBranch=read<uint32_t>(input);p.outerOwnerPresent=read<uint32_t>(input);
    p.heightScale=read<float>(input);p.originX=read<float>(input);p.originZ=read<float>(input);
    p.spacing=read<float>(input);p.minimum=read<float>(input);p.maximum=read<float>(input);p.width=read<int>(input);p.height=read<int>(input);
    if(p.preparationBranch>2||p.outerOwnerPresent!=1||p.width<1||p.height<1||p.width>65535||p.height>65535||
        !std::isfinite(p.spacing)||p.spacing<=0||!std::isfinite(p.heightScale)||!std::isfinite(p.originX)||!std::isfinite(p.originZ)||!std::isfinite(p.minimum))
        throw std::runtime_error("Unsupported current GroundHeight source fields");
    std::ifstream samples(directory/"packed24.bin",std::ios::binary);p.packed.resize((size_t(p.width)+1)*(size_t(p.height)+1));
    samples.read(reinterpret_cast<char*>(p.packed.data()),std::streamsize(p.packed.size()*4));
    if(!samples||samples.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Incomplete current Field packed24 grid");return p;
}
inline int groundCellIndex(float local,float spacing,int extent){
    if(local<0)return 0;const float q=D(local,spacing);
    // Exact CVTTSS2SI indefinite result on overflow, followed by signed clamp.
    const int index=_mm_cvttss_si32(_mm_set_ss(q));return std::clamp(index,0,extent-1);
}
inline float groundRemainder(float value,float spacing){
    // Original1033298 finite fmod path widens binary32 to binary64, retains
    // signed remainder even when the independently chosen cell was clamped.
    return static_cast<float>(std::fmod(static_cast<double>(value),static_cast<double>(spacing)));
}
inline float queryGroundHeight(const GroundHeightProfile& p,float x,float z){
    const float localX=A(x,p.originX),localZ=A(z,p.originZ);
    if(!std::isfinite(localX)||!std::isfinite(localZ))throw std::runtime_error("GroundHeight query coordinates must be finite");
    const int column=groundCellIndex(localX,p.spacing,p.width),row=groundCellIndex(localZ,p.spacing,p.height),stride=p.width+1;
    const size_t at=size_t(row)*size_t(stride)+size_t(column);const uint32_t packedA=p.packed[at];const bool opposite=(packedA&255)!=0;
    float rx=groundRemainder(localX,p.spacing),rz=groundRemainder(localZ,p.spacing);uint32_t base,first,second;
    if(!opposite){
        if(A(rz,rx)<p.spacing){base=packedA>>8;first=p.packed[at+stride]>>8;second=p.packed[at+1]>>8;}
        else{base=p.packed[at+stride+1]>>8;first=p.packed[at+1]>>8;second=p.packed[at+stride]>>8;rx=S(p.spacing,rx);rz=S(p.spacing,rz);}
    }else{const float complementX=S(p.spacing,rx);
        if(A(rz,complementX)<p.spacing){base=p.packed[at+1]>>8;first=packedA>>8;second=p.packed[at+stride+1]>>8;rx=complementX;}
        else{base=p.packed[at+stride]>>8;first=p.packed[at+stride+1]>>8;second=packedA>>8;rz=S(p.spacing,rz);}
    }
    const float firstHeight=A(M(static_cast<float>(first),p.heightScale),p.minimum),baseline=A(M(static_cast<float>(base),p.heightScale),p.minimum);
    const float inverse=D(1,p.spacing),secondHeight=A(M(static_cast<float>(second),p.heightScale),p.minimum);
    const float weightZ=M(inverse,rz),weightX=M(inverse,rx),firstDelta=S(firstHeight,baseline),secondDelta=S(secondHeight,baseline);
    const float termX=M(weightX,opposite?firstDelta:secondDelta),termZ=M(weightZ,opposite?secondDelta:firstDelta);return A(baseline,A(termX,termZ));
}
inline float sin5(float a,bool positive){float q=M(a,a),q2=M(q,q);float b=positive?A(M(S(6,q),20),q2):S(M(S(q,6),20),q2);return M(M(b,a),0.008333333767950535f);}
inline float cos6(float a,bool positive){float q=M(a,a),q2plus=A(M(q,q),360);float b=positive?S(M(q2plus,S(30,q)),10080):A(M(q2plus,S(q,30)),10080);return M(b,0.0013888889225199819f);}
inline std::array<float,2> sinCos(float angle){
    if(angle==0)return {0,1};bool ps=angle>=0,pc=true;float a=std::fabs(angle);
    while(a>=6.2831854820251465f)a=A(a,-6.2831854820251465f);
    if(a>=3.1415927410125732f){a=A(a,-3.1415927410125732f);ps=!ps;pc=false;}
    if(a>=1.5707963705062866f){a=S(3.1415927410125732f,a);pc=!pc;}
    if(a<0.668318510055542f)return {sin5(a,ps),cos6(a,pc)};
    float complement=S(1.5707963705062866f,a);
    if(a<0.9083185195922852f)return {cos6(complement,ps),cos6(a,pc)};
    return {cos6(complement,ps),sin5(complement,pc)};
}
inline V3 cross(V3 a,V3 b){return {S(M(a.Y,b.Z),M(a.Z,b.Y)),S(M(a.Z,b.X),M(a.X,b.Z)),S(M(a.X,b.Y),M(a.Y,b.X))};}
inline V3 normalize(V3 v){float f=D(1,std::sqrt(A(A(M(v.X,v.X),M(v.Y,v.Y)),M(v.Z,v.Z))));return {M(v.X,f),M(v.Y,f),M(v.Z,f)};}
inline V4 quaternionFromBasis(Matrix m){
    float trace=A(A(m[0],m[5]),m[10]);
    if(trace>0){float root=std::sqrt(A(trace,1)),factor=D(.5f,root);return {M(S(m[6],m[9]),factor),M(S(m[8],m[2]),factor),M(S(m[1],m[4]),factor),M(root,.5f)};}
    int largest=0,second=1,third=2;if(m[5]>m[0]){largest=1;second=2;third=0;}if(m[10]>m[largest*5]){largest=2;second=0;third=1;}
    float root=std::sqrt(A(S(m[largest*5],A(m[second*5],m[third*5])),1)),f=D(.5f,root);std::array<float,4> q{};
    q[largest]=M(root,.5f);q[3]=M(S(m[third+second*4],m[second+third*4]),f);
    q[second]=M(A(m[largest+second*4],m[second+largest*4]),f);q[third]=M(A(m[largest+third*4],m[third+largest*4]),f);return {q[0],q[1],q[2],q[3]};
}
inline std::array<V4,2> derivePose(const State& s){
    auto yaw=sinCos(static_cast<float>(s.yaw));float negativePitch=flip(static_cast<float>(s.pitch));auto pitch=sinCos(negativePitch);
    V3 forward=normalize({M(pitch[1],yaw[0]),pitch[0],M(pitch[1],yaw[1])});
    auto quarter=sinCos(A(negativePitch,1.5707963705062866f));V3 up=normalize({M(quarter[1],yaw[0]),quarter[0],M(quarter[1],yaw[1])});
    V3 right=normalize(cross(up,forward));up=normalize(cross(forward,right));
    V4 eye{static_cast<float>(DS(s.x,DM(forward.X,s.distance))),static_cast<float>(DS(s.y,DM(forward.Y,s.distance))),static_cast<float>(DS(s.z,DM(forward.Z,s.distance))),1};
    V3 bf=normalize(forward),br=normalize(cross(up,forward)),bu=cross(bf,br);
    return {eye,quaternionFromBasis({br.X,br.Y,br.Z,0,bu.X,bu.Y,bu.Z,0,bf.X,bf.Y,bf.Z,0,0,0,0,1})};
}
inline TerrainPitchResult qualifyTerrainPitch(const GroundHeightProfile& ground,TerrainPitchInput input){
    double pitch=input.requestedPitch;if(input.nativeBand>=2||!ground.outerOwnerPresent)return {true,pitch};
    auto boundedPitch=[&](double value){return value>=input.maximumPitch?input.maximumPitch:input.minimumPitch>=value?input.minimumPitch:value;};
    auto clearance=[&](State pose){const auto eye=derivePose(pose)[0];return S(S(eye.Y,queryGroundHeight(ground,eye.X,eye.Z)),1000);};
    const State candidate=input.candidateBeforeRequestedPitch;const float oldClearance=clearance(candidate);
    if(oldClearance>=0&&oldClearance<200&&candidate.pitch>pitch)return {true,candidate.pitch};
    State pose=candidate;pose.pitch=boundedPitch(pitch);if(clearance(pose)>0)return {true,pitch};
    for(;;){const double next=std::min(DA(pitch,0.0174533),input.maximumPitch);
        if(next==pitch&&std::fabs(DS(pitch,input.maximumPitch))>=1.1920928955078125e-5f)throw std::runtime_error("Source terrain pitch increment cannot progress");
        pitch=next;pose.pitch=boundedPitch(pitch);(void)derivePose(pose);
        // Original D550 rejects at maximum without another height query.
        if(std::fabs(DS(pitch,input.maximumPitch))<1.1920928955078125e-5f)return {false,pitch};
        if(clearance(pose)>0)return {true,pitch};
    }
}
inline Zoom classify(const Profile& p,double distance){
    float v=static_cast<float>(distance);int upper=0;while(upper<=6&&p.anchors[upper]<v)upper++;
    if(upper==7)return {6,0,6};int lower=upper==0?0:upper-1;
    float t=upper==0?0:D(S(v,p.anchors[lower]),S(p.anchors[upper],p.anchors[lower]));
    int fraction=static_cast<int>(M(t,10000)),band=lower;
    if(fraction<0)fraction=0;else{fraction=std::min(fraction,10000);if(fraction==10000){band++;fraction=0;}}
    return {band,fraction,A(static_cast<float>(lower),D(static_cast<float>(fraction),10000))};
}
inline Bounds bounds(const Profile& p,Zoom z){
    std::array<float,10> v{};float t=D(static_cast<float>(z.fraction),10000);
    for(int i=0;i<10;i++)v[i]=z.band==6?p.bounds[6][i]:A(M(S(1,t),p.bounds[z.band][i]),M(t,p.bounds[z.band+1][i]));
    return {{v[0],v[1],v[2]},{v[3],v[4],v[5]},p.anchors[0],p.anchors[6],v[6],v[7],v[8],v[9]};
}
inline Runtime createRuntime(const Profile& p){auto z=classify(p,p.initial.distance);return {p.initial,bounds(p,z),z,2};}
inline double clamp(double x,double lo,double hi){return x>=hi?hi:lo>=x?lo:x;}
inline void clampFocus(State& s,V3 lo,V3 hi){s.x=clamp(s.x,lo.X,hi.X);s.y=clamp(s.y,lo.Y,hi.Y);s.z=clamp(s.z,lo.Z,hi.Z);}
inline double clampMinimumThenMaximum(double v,double oldMax,double lo,double hi){v=v>=oldMax?oldMax:std::max(v,lo);return v>=hi?hi:std::max(v,lo);}
inline bool applyImmediateWithGround(const Profile& profile,Runtime& r,const Request& request,const GroundHeightProfile* ground){
    for(double v:{static_cast<double>(request.focus.X),static_cast<double>(request.focus.Y),static_cast<double>(request.focus.Z),static_cast<double>(request.focus.W),request.distance,request.yaw,request.pitch})
        if(!std::isfinite(v))throw std::runtime_error("Main request must be finite");
    State p=r.pose;Bounds b=r.bounds;Zoom z=r.zoom;
    if(request.distance>=0){p.distance=clamp(request.distance,b.minDistance,b.maxDistance);z=classify(profile,p.distance);
        Bounds next=bounds(profile,z);next.minDistance=b.minDistance;next.maxDistance=b.maxDistance;
        clampFocus(p,next.minimum,b.maximum);clampFocus(p,next.minimum,next.maximum);
        p.yaw=clampMinimumThenMaximum(p.yaw,b.maxYaw,next.minYaw,next.maxYaw);
        p.pitch=clampMinimumThenMaximum(p.pitch,b.maxPitch,next.minPitch,next.maxPitch);b=next;}
    if(z.band<2&&!ground)throw std::runtime_error("Source band0/1 requires explicit current Field GroundHeight profile");
    if(std::fabs(request.focus.W)>=1.1920928955078125e-5f){p.x=request.focus.X;p.y=request.focus.Y;p.z=request.focus.Z;p.w=1;clampFocus(p,b.minimum,b.maximum);}
    if(request.yaw<62.83185308){double yaw=request.yaw;if(std::fabs(yaw)>6.283185308*65536)throw std::runtime_error("Original yaw wrap outside finite bounded contract");
        while(yaw>6.283185308)yaw=DS(yaw,6.283185308);while(yaw<0)yaw=DA(yaw,6.283185308);p.yaw=clamp(yaw,b.minYaw,b.maxYaw);}
    double requestedPitch=request.pitch>=0?request.pitch:p.pitch;
    if(z.band<2){r.zoom=z;const auto result=qualifyTerrainPitch(*ground,{z.band,p,b.minPitch,b.maxPitch,requestedPitch});if(!result.accepted)return false;requestedPitch=result.pitch;}
    p.pitch=clamp(requestedPitch,b.minPitch,b.maxPitch);
    r.zoom=z;auto eye=derivePose(p)[0];if(eye.Y<=3000&&eye.Z>S(346780.15625f,M(eye.X,0.8549618124961853f)))return false;
    r.pose=p;r.bounds=b;r.dirtyCountdown=2;return true;
}
inline bool applyImmediate(const Profile& profile,Runtime& r,const Request& request){return applyImmediateWithGround(profile,r,request,nullptr);}
inline bool applyImmediate(const Profile& profile,Runtime& r,const Request& request,const GroundHeightProfile& ground){return applyImmediateWithGround(profile,r,request,&ground);}

// Rotate the vector by q * v * conjugate(q). Explicit scalar stages retain
// the established multiplication and addition order, including operand order.
inline V4 rotateVector(V4 rotation,V4 vector){
    const float dot=A(A(M(vector.X,rotation.X),M(vector.Y,rotation.Y)),M(vector.Z,rotation.Z));
    const float productX=A(S(M(vector.Z,rotation.Y),M(rotation.Z,vector.Y)),M(vector.X,rotation.W));
    const float productY=A(S(M(rotation.Z,vector.X),M(vector.Z,rotation.X)),M(vector.Y,rotation.W));
    const float productZ=A(S(M(rotation.X,vector.Y),M(vector.X,rotation.Y)),M(vector.Z,rotation.W));
    const float conjugateX=A(S(M(rotation.Y,productZ),M(rotation.Z,productY)),M(rotation.X,dot));
    const float conjugateY=A(S(M(rotation.Z,productX),M(rotation.X,productZ)),M(rotation.Y,dot));
    const float conjugateZ=A(S(M(rotation.X,productY),M(rotation.Y,productX)),M(rotation.Z,dot));
    return {A(M(productX,rotation.W),conjugateX),A(M(productY,rotation.W),conjugateY),
        A(M(productZ,rotation.W),conjugateZ),vector.W};
}

inline Matrix lookTo(V4 eye,V4 target,V4 up){
    __m128 x0=_mm_setr_ps(eye.X,eye.Y,eye.Z,eye.W),x1=_mm_sub_ps(x0,_mm_setr_ps(target.X,target.Y,target.Z,target.W)),x2=_mm_setr_ps(up.X,up.Y,up.Z,up.W);
    __m128 x3{},x4{},x5{},x6{},x7{},x8{},x9{},x10{};
    auto c0=_mm_castsi128_ps(_mm_set1_epi32(0x7f800000)),c1=_mm_castsi128_ps(_mm_set1_epi32(0x7fc00000));
    auto c2=_mm_castsi128_ps(_mm_setr_epi32(-1,-1,-1,0)),c3=_mm_setr_ps(0,0,0,1);
        x5 = x1; // SC 0097EE7E
        x2 = _mm_shuffle_ps(x2, x2, 0xC9); // SC 0097EE81
        x5 = _mm_mul_ps(x5, x1); // SC 0097EE85
        x6 = x1; // SC 0097EE88
        x1 = c0; // SC 0097EE8B
        x8 = x0; // SC 0097EE92
        x9 = _mm_xor_ps(x9, x9); // SC 0097EE96
        x10 = x9; // SC 0097EE9A
        x4 = x5; // SC 0097EE9E
        x4 = _mm_shuffle_ps(x4, x5, 0x99); // SC 0097EEA1
        x5 = _mm_add_ss(x5, x4); // SC 0097EEA5
        x3 = x4; // SC 0097EEA9
        x3 = _mm_shuffle_ps(x3, x4, 0x55); // SC 0097EEAC
        x5 = _mm_add_ss(x5, x3); // SC 0097EEB0
        x5 = _mm_shuffle_ps(x5, x5, 0x00); // SC 0097EEB4
        x1 = _mm_cmpneq_ps(x1, x5); // SC 0097EEB8
        x0 = _mm_sqrt_ps(x5); // SC 0097EEBC
        x6 = _mm_div_ps(x6, x0); // SC 0097EEBF
        x10 = _mm_cmpneq_ps(x10, x0); // SC 0097EEC2
        x0 = x2; // SC 0097EEC7
        x10 = _mm_and_ps(x10, x6); // SC 0097EECA
        x0 = _mm_shuffle_ps(x0, x2, 0xC9); // SC 0097EECE
        x10 = _mm_and_ps(x10, x1); // SC 0097EED2
        x1 = _mm_andnot_ps(x1, c1); // SC 0097EED6
        x6 = x9; // SC 0097EEDD
        x10 = _mm_or_ps(x10, x1); // SC 0097EEE1
        x9 = _mm_sub_ps(x9, x8); // SC 0097EEE5
        x3 = x10; // SC 0097EEE9
        x3 = _mm_shuffle_ps(x3, x10, 0xD2); // SC 0097EEED
        x1 = x3; // SC 0097EEF2
        x1 = _mm_shuffle_ps(x1, x3, 0xD2); // SC 0097EEF5
        x3 = _mm_mul_ps(x3, x2); // SC 0097EEF9
        x1 = _mm_mul_ps(x1, x0); // SC 0097EEFC
        x3 = _mm_sub_ps(x3, x1); // SC 0097EEFF
        x3 = _mm_and_ps(x3, c2); // SC 0097EF02
        x2 = x3; // SC 0097EF09
        x2 = _mm_mul_ps(x2, x3); // SC 0097EF0C
        x1 = x2; // SC 0097EF0F
        x1 = _mm_shuffle_ps(x1, x2, 0x99); // SC 0097EF12
        x2 = _mm_add_ss(x2, x1); // SC 0097EF16
        x0 = x1; // SC 0097EF1A
        x0 = _mm_shuffle_ps(x0, x1, 0x55); // SC 0097EF1D
        x2 = _mm_add_ss(x2, x0); // SC 0097EF21
        x2 = _mm_shuffle_ps(x2, x2, 0x00); // SC 0097EF25
        x0 = _mm_sqrt_ps(x2); // SC 0097EF29
        x2 = _mm_cmpneq_ps(x2, c0); // SC 0097EF2C
        x3 = _mm_div_ps(x3, x0); // SC 0097EF34
        x6 = _mm_cmpneq_ps(x6, x0); // SC 0097EF37
        x6 = _mm_and_ps(x6, x3); // SC 0097EF3B
        x3 = x9; // SC 0097EF3E
        x6 = _mm_and_ps(x6, x2); // SC 0097EF42
        x2 = _mm_andnot_ps(x2, c1); // SC 0097EF45
        x6 = _mm_or_ps(x6, x2); // SC 0097EF4C
        x2 = x10; // SC 0097EF4F
        x2 = _mm_shuffle_ps(x2, x10, 0xC9); // SC 0097EF53
        x5 = x6; // SC 0097EF58
        x5 = _mm_shuffle_ps(x5, x6, 0xD2); // SC 0097EF5B
        x0 = x2; // SC 0097EF5F
        x0 = _mm_shuffle_ps(x0, x2, 0xC9); // SC 0097EF62
        x1 = x5; // SC 0097EF66
        x1 = _mm_shuffle_ps(x1, x5, 0xD2); // SC 0097EF69
        x1 = _mm_mul_ps(x1, x0); // SC 0097EF6D
        x5 = _mm_mul_ps(x5, x2); // SC 0097EF70
        x2 = x9; // SC 0097EF73
        x2 = _mm_mul_ps(x2, x6); // SC 0097EF77
        x9 = _mm_mul_ps(x9, x10); // SC 0097EF7A
        x5 = _mm_sub_ps(x5, x1); // SC 0097EF7E
        x1 = x2; // SC 0097EF81
        x1 = _mm_shuffle_ps(x1, x2, 0x99); // SC 0097EF84
        x0 = x1; // SC 0097EF88
        x2 = _mm_add_ss(x2, x1); // SC 0097EF8B
        x0 = _mm_shuffle_ps(x0, x1, 0x55); // SC 0097EF8F
        x5 = _mm_and_ps(x5, c2); // SC 0097EF93
        x3 = _mm_mul_ps(x3, x5); // SC 0097EF9A
        x2 = _mm_add_ss(x2, x0); // SC 0097EF9D
        x1 = x3; // SC 0097EFA1
        x1 = _mm_shuffle_ps(x1, x3, 0x99); // SC 0097EFA4
        x3 = _mm_add_ss(x3, x1); // SC 0097EFA8
        x0 = x1; // SC 0097EFAC
        x0 = _mm_shuffle_ps(x0, x1, 0x55); // SC 0097EFAF
        x3 = _mm_add_ss(x3, x0); // SC 0097EFB3
        x2 = _mm_shuffle_ps(x2, x2, 0x00); // SC 0097EFB7
        x1 = x9; // SC 0097EFBB
        x1 = _mm_shuffle_ps(x1, x9, 0x99); // SC 0097EFBF
        x9 = _mm_add_ss(x9, x1); // SC 0097EFC4
        x3 = _mm_shuffle_ps(x3, x3, 0x00); // SC 0097EFC9
        x0 = x1; // SC 0097EFCD
        x0 = _mm_shuffle_ps(x0, x1, 0x55); // SC 0097EFD0
        x1 = c2; // SC 0097EFD4
        x7 = x1; // SC 0097EFDB
        x9 = _mm_add_ss(x9, x0); // SC 0097EFDE
        x7 = _mm_andnot_ps(x7, x2); // SC 0097EFE3
        x0 = x1; // SC 0097EFE6
        x2 = x1; // SC 0097EFE9
        x0 = _mm_and_ps(x0, x6); // SC 0097EFEC
        x2 = _mm_andnot_ps(x2, x3); // SC 0097EFEF
        x7 = _mm_or_ps(x7, x0); // SC 0097EFF2
        x6 = x1; // SC 0097EFF5
        x0 = x1; // SC 0097EFF8
        x1 = _mm_and_ps(x1, x10); // SC 0097EFFB
        x0 = _mm_and_ps(x0, x5); // SC 0097EFFF
        x9 = _mm_shuffle_ps(x9, x9, 0x00); // SC 0097F002
        x2 = _mm_or_ps(x2, x0); // SC 0097F007
        x5 = x7; // SC 0097F00A
        x5 = _mm_shuffle_ps(x5, x2, 0x44); // SC 0097F00D
        x6 = _mm_andnot_ps(x6, x9); // SC 0097F011
        x6 = _mm_or_ps(x6, x1); // SC 0097F015
        x7 = _mm_shuffle_ps(x7, x2, 0xEE); // SC 0097F018
        x0 = x6; // SC 0097F01C
        x4 = x5; // SC 0097F01F
        x0 = _mm_shuffle_ps(x0, c3, 0x44); // SC 0097F022
        x2 = x7; // SC 0097F02A
        x6 = _mm_shuffle_ps(x6, c3, 0xEE); // SC 0097F02D
        x5 = _mm_shuffle_ps(x5, x0, 0xDD); // SC 0097F035
        x4 = _mm_shuffle_ps(x4, x0, 0x88); // SC 0097F039
        x1 = x5; // SC 0097F03D
        x7 = _mm_shuffle_ps(x7, x6, 0xDD); // SC 0097F040
        x0 = x4; // SC 0097F044
        x2 = _mm_shuffle_ps(x2, x6, 0x88); // SC 0097F047
        x3 = x7; // SC 0097F04B
        Matrix out; _mm_storeu_ps(out.data(),x0);_mm_storeu_ps(out.data()+4,x1);_mm_storeu_ps(out.data()+8,x2);_mm_storeu_ps(out.data()+12,x3);return out;
}

inline V4 normalizeDirection(V4 v){float f=D(1,std::sqrt(A(A(M(v.Y,v.Y),M(v.X,v.X)),M(v.Z,v.Z))));return {M(f,v.X),M(f,v.Y),M(f,v.Z),v.W};}
inline Matrix compose(const Matrix& a,const Matrix& b){Matrix out{};for(int row=0;row<4;row++)for(int col=0;col<4;col++)
    out[row*4+col]=A(A(M(a[row*4+2],b[8+col]),M(a[row*4],b[col])),A(M(a[row*4+3],b[12+col]),M(a[row*4+1],b[4+col])));return out;}
inline std::array<Plane,6> makePlanes(Matrix m,bool reverse){
    V4 c[4];for(int i=0;i<4;i++)c[i]={m[i],m[4+i],m[8+i],m[12+i]};
    auto sub=[](V4 a,V4 b){return V4{S(a.X,b.X),S(a.Y,b.Y),S(a.Z,b.Z),S(a.W,b.W)};};
    auto neg=[](V4 a){return V4{flip(a.X),flip(a.Y),flip(a.Z),flip(a.W)};};
    std::array<V4,6> raw{sub(neg(c[3]),c[0]),sub(c[0],c[3]),sub(neg(c[3]),c[1]),sub(c[1],c[3]),neg(c[2]),sub(c[2],c[3])};
    if(reverse)std::swap(raw[4],raw[5]);std::array<Plane,6> result{};
    for(int i=0;i<6;i++){V4 n=raw[i];float f=D(1,std::sqrt(A(A(M(n.X,n.X),M(n.Y,n.Y)),M(n.Z,n.Z))));V4 e{M(f,n.X),M(f,n.Y),M(f,n.Z),M(f,n.W)};
        result[i]={e,e.X<0?1:0,e.Y<0?1:0,e.Z<0?1:0};}return result;
}
inline bool intersects(const std::array<Plane,6>& planes,V3 lo,V3 hi){
    for(int i:{0,1,4,2,3,5}){auto p=planes[i];float x=p.supportX==0?lo.X:hi.X,y=p.supportY==0?lo.Y:hi.Y,z=p.supportZ==0?lo.Z:hi.Z;
        if(!(A(A(A(M(y,p.equation.Y),M(x,p.equation.X)),M(z,p.equation.Z)),p.equation.W)<0))return false;}return true;
}
inline float planarDistance(Node n,V4 eye){float minX=M(static_cast<float>(n.x),n.size),minZ=M(static_cast<float>(n.z),n.size);
    float dx=S(std::clamp(eye.X,minX,A(minX,n.size)),eye.X),dz=S(std::clamp(eye.Z,minZ,A(minZ,n.size)),eye.Z);
    return std::sqrt(A(A(M(dx,dx),0),M(dz,dz)));}
inline Frame prepareTerrainFromFrame(const Profile& p,Frame f);
inline Frame prepareCurrent(const Profile& p,const Runtime& r){
    Frame f;f.optics=p.optics;auto pose=derivePose(r.pose);f.eye=pose[0];f.quaternion=pose[1];
    V4 fw=normalizeDirection(rotateVector(f.quaternion,{0,0,1,0})),up=normalizeDirection(rotateVector(f.quaternion,{0,1,0,0}));
    V4 target{A(M(fw.X,p.lookDistance),f.eye.X),A(M(fw.Y,p.lookDistance),f.eye.Y),A(M(fw.Z,p.lookDistance),f.eye.Z),A(M(fw.W,p.lookDistance),f.eye.W)};
    f.target=target;f.up=up;
    Matrix raw=lookTo(f.eye,target,up),view=raw;
    float offset=f.optics.roll;if(offset!=0){auto t=sinCos(flip(offset));for(int i=0;i<4;i++){
        float x=raw[i*4],y=raw[i*4+1];view[i*4]=S(M(x,t[1]),M(y,t[0]));view[i*4+1]=A(M(x,t[0]),M(y,t[1]));}}
    if(f.optics.projectionKind!=ProjectionKind::Perspective)throw std::runtime_error("Map camera requires perspective");bool reverse=f.optics.reversedDepth;
    float sourceNear=f.optics.nearPlane,sourceFar=f.optics.farPlane,aspect=f.optics.aspectRatio;
    float na=reverse?sourceFar:sourceNear,fa=reverse?sourceNear:sourceFar,yScale=D(1,M(p.halfFovTangent,1)),range=S(na,fa),ds,dd;
    if(p.clipDepthRange==ClipDepthRange::ZeroToOne){ds=D(fa,range);dd=M(ds,na);}else{float inv=D(1,range);ds=M(A(na,fa),inv);dd=M(M(A(na,na),fa),inv);}
    Matrix projection{D(yScale,aspect),0,0,0,0,yScale,0,0,0,0,ds,-1,0,0,dd,0},combined=compose(view,projection);
    f.unrolledView=raw;f.view=view;f.projection=projection;f.worldToClip=combined;
    f.prefix=encodePreparedCamera(p.staticPrefix,f);
    f.planes=makePlanes(combined,reverse);
    return prepareTerrainFromFrame(p,std::move(f));
}
// The client publishes its original prepared Camera. Reuse the terrain/LOD
// kernel without rebuilding a second camera with separate optics or matrices.
inline Frame preparePublished(const Profile& p,const Prefix& prefix,const State& controller){
    Frame f;static_cast<CameraTransform&>(f)=decodeCameraTransform(prefix);
    f.prefix=prefix;f.quaternion=derivePose(controller)[1];
    f.planes=makePlanes(f.worldToClip,f.optics.reversedDepth);
    return prepareTerrainFromFrame(p,std::move(f));
}
inline Frame prepareTerrainFromFrame(const Profile& p,Frame f){
    struct Candidate {int index;float distance;};std::vector<Candidate> candidates;
    auto visit=[&](auto&& self,int index,int depth)->void{Node n=p.nodes[index];float distance=planarDistance(n,f.eye);
        float threshold=M(p.lodScale,M(std::sqrt(2.f),M(n.size,.5f)));
        if(candidates.size()<1024&&(depth==p.levels-1||distance>threshold)){candidates.push_back({index,distance});return;}
        if(depth!=p.levels-1)for(int child=0;child<4;child++)self(self,index*4+1+child,depth+1);};visit(visit,0,0);
    f.selectedCount=static_cast<int>(candidates.size());
    std::sort(candidates.begin(),candidates.end(),[&](Candidate a,Candidate b){int al=p.nodes[a.index].level,bl=p.nodes[b.index].level;
        if(al!=bl)return al<bl;if(a.distance!=b.distance)return a.distance<b.distance;return a.index<b.index;});
    int group0=0;
    for(auto& candidate:candidates){Node n=p.nodes[candidate.index];float x=M(static_cast<float>(n.x),n.size),z=M(static_cast<float>(n.z),n.size);
        if(!intersects(f.planes,{x,S(n.minimum,1000),z},{A(x,n.size),A(n.maximum,1000),A(z,n.size)})){candidate.distance=-1;f.culledCount++;continue;}
        int group=n.level==0?(group0<4?0:1):2;if(group==0)group0++;
        uint32_t edges=0;for(auto neighbor:candidates){Node a=p.nodes[neighbor.index];int d=a.level-n.level;if(neighbor.distance<0||(d!=1&&d!=2))continue;
            int k=1<<d,nx=n.x>>d,nz=n.z>>d,mx=n.x&(k-1),mz=n.z&(k-1);uint32_t value=1u<<(d-1);
            if(mz==0&&a.x==nx&&a.z==nz-1)edges|=value;if(mz==k-1&&a.x==nx&&a.z==nz+1)edges|=value<<4;
            if(mx==0&&a.x==nx-1&&a.z==nz)edges|=value<<8;if(mx==k-1&&a.x==nx+1&&a.z==nz)edges|=value<<12;}
        int side=1<<(p.levels-n.level-1),grid=p.heightWidth/(1<<(p.levels-1))+1;
        Instance instance{{x,0,z},{n.size,1,n.size},{D(static_cast<float>(n.x),static_cast<float>(side)),D(static_cast<float>(n.z),static_cast<float>(side)),
            D(static_cast<float>(n.x+1),static_cast<float>(side)),D(static_cast<float>(n.z+1),static_cast<float>(side))},edges,static_cast<uint32_t>(grid),
            static_cast<float>((p.controlWidth-1)/side),static_cast<float>(p.controlWidth)};
        f.instances.push_back(instance);f.rows.push_back({n.index,group,candidate.distance,edges});
        int at=static_cast<int>(f.rows.size())-1;if(f.batches.empty()||f.batches.back().group!=group)f.batches.push_back({group,at,1,group==2?1:0});else f.batches.back().count++;
    }
    return f;
}
} // namespace source_camera
