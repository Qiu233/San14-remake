// CPU source inputs for same-band Main pan. No device/context/resource loading
// during publication. Far geometry remains an explicit independent requirement.
#pragma once
#include <array>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <vector>

namespace source_water_camera {
using Prefix=std::array<uint8_t,0x3c0>;
struct Packets {
    // [wave VS384, wave PS864, far VS384, far PS864]. All source lanes retained.
    std::array<std::vector<char>,4> constants;
};
struct Profile {Prefix initial{};Packets source;};
struct RefractionDerived {
    std::array<float,3> position{},direction{},viewY{};
    std::array<float,2> depthToZ{};
    std::array<float,12> worldToTexture{};
};
struct Publication {
    RefractionDerived refraction;
    // 117F010/11B8320 depends on this new camera. Do not submit the old far VB.
    bool requiresFarGeometry=true;
    bool requiresVisibilityEvaluation=true;
};
template<class T> inline T get(const uint8_t* p){T v;std::memcpy(&v,p,sizeof v);return v;}
template<class T> inline T read(std::ifstream& f){T v{};f.read(reinterpret_cast<char*>(&v),sizeof v);if(!f)throw std::runtime_error("Incomplete water camera profile");return v;}
inline Profile loadProfile(const std::filesystem::path& path) {
    std::ifstream f(path,std::ios::binary);
    if(!f||read<uint32_t>(f)!=0x31434357u)throw std::runtime_error("Water camera source profile WCC1 absent");
    Profile result;result.initial=read<Prefix>(f);
    for(unsigned i=0;i<4;i++) {
        uint32_t size=read<uint32_t>(f);if(size!=(i%2?864u:384u))throw std::runtime_error("Water camera original CB size differs");
        result.source.constants[i].resize(size);f.read(result.source.constants[i].data(),size);
        if(!f)throw std::runtime_error("Incomplete water camera source CB");
    }
    if(f.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Trailing water camera source profile");return result;
}
// Source scalar boundaries preserve binary32 operations and prohibit FMA joins.
__declspec(noinline) inline float A(float a,float b){return a+b;}
__declspec(noinline) inline float S(float a,float b){return a-b;}
__declspec(noinline) inline float M(float a,float b){return a*b;}
__declspec(noinline) inline float D(float a,float b){return a/b;}
inline std::array<float,3> deltaDirection(const Prefix& camera,bool oceanOrder) {
    const auto* p=camera.data();std::array<float,3> delta{};
    for(unsigned i=0;i<3;i++)delta[i]=S(get<float>(p+0x20+4*i),get<float>(p+0x10+4*i));
    float a=M(delta[0],delta[0]),b=M(delta[1],delta[1]);
    float lengthSquared=A(oceanOrder?A(b,a):A(a,b),M(delta[2],delta[2]));float inv=D(1,std::sqrt(lengthSquared));
    for(auto& v:delta)v=M(v,inv);return delta;
}
inline RefractionDerived deriveRefraction(const Prefix& camera,float minimumDepth=0,float maximumDepth=1) {
    const auto* p=camera.data();RefractionDerived out;
    for(unsigned i=0;i<3;i++) {out.position[i]=get<float>(p+0x10+4*i);out.viewY[i]=get<float>(p+0x84+16*i);}
    out.direction=deltaDirection(camera,false); // OriginalDeferredRefractionMath9B4FF0.
    float nearClip=get<float>(p+0x140),farClip=get<float>(p+0x144);bool reverse=(get<uint32_t>(p+0x158)&2)!=0;
    float u=reverse?nearClip:farClip,v=reverse?farClip:nearClip,q=D(1,S(maximumDepth,minimumDepth));
    out.depthToZ[1]=M(D(S(v,u),M(v,u)),q);
    float first=M(minimumDepth,q);first=M(first,S(u,v));first=D(first,u);first=A(first,1);out.depthToZ[0]=M(first,D(1,v));
    // Source9A8410 uses original half literals, independent of target dimensions.
    for(unsigned i=0;i<4;i++) {
        float w=get<float>(p+0x10c+16*i),x=get<float>(p+0x100+16*i),y=get<float>(p+0x104+16*i);
        out.worldToTexture[i]=A(M(w,.5f),M(x,.5f));out.worldToTexture[4+i]=A(M(w,.5f),M(y,-.5f));out.worldToTexture[8+i]=w;
    }
    return out;
}
inline void copy(std::vector<char>& destination,unsigned offset,const void* data,size_t bytes) {
    std::memcpy(destination.data()+offset,data,bytes);
}
// Overlay existing complete CBs, preserving the actual current wave/material/time
// lanes. Caller starts them from Profile.source only once, then advances writers.
inline Publication writeCurrentCamera(Packets& packets,const Prefix& camera) {
    const auto* p=camera.data();auto derived=deriveRefraction(camera);auto oceanDirection=deltaDirection(camera,true);
    std::array<float,12> view{};
    for(unsigned row=0;row<3;row++)for(unsigned col=0;col<4;col++)view[row*4+col]=get<float>(p+0x80+(col*4+row)*4);
    float x=S(get<float>(p+0x20),get<float>(p+0x10)),z=S(get<float>(p+0x28),get<float>(p+0x18));
    float inv=D(1,std::sqrt(A(A(M(x,x),0),M(z,z))));std::array<float,2> directionXZ{M(x,inv),M(inv,z)};
    std::array<float,4> direction4{oceanDirection[0],oceanDirection[1],oceanDirection[2],S(get<float>(p+0x2c),get<float>(p+0x1c))};
    for(unsigned flat=0;flat<2;flat++) {
        auto& vs=packets.constants[flat*2];auto& ps=packets.constants[flat*2+1];
        if(vs.size()!=384||ps.size()!=864)throw std::runtime_error("Water camera requires complete selected384/864 CBs");
        copy(vs,0,p+0x10,16);copy(vs,16,p+0x100,64);copy(vs,352,directionXZ.data(),8);
        copy(ps,0,p+0x10,16);copy(ps,16,view.data(),48);copy(ps,112,derived.worldToTexture.data(),48);
        copy(ps,192,derived.depthToZ.data(),8);copy(ps,208,p+0x10,16);copy(ps,224,direction4.data(),16);
    }
    return {derived,true,true};
}
} // namespace source_water_camera
