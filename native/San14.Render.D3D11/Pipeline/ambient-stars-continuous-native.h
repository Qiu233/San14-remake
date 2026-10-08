// Fixed source Main/season, CE11902E0 Star phase writer. Include after D3D11.
// No clock is advanced here: share node3's current Common254-priorCC delta.
#pragma once
#include <array>
#include <algorithm>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <string>
#include <stdexcept>

struct NativeAmbientStarPacket {
    std::array<char,352> vs{};
    std::array<char,16> gs{},ps{};
};
struct NativeAmbientStarContinuous {
    std::array<NativeAmbientStarPacket,6> faces;
    std::array<float,6> faceHeight{};
    float clockScale=0,phase=0;
};
static float nativeStarFloat(const char* bytes) {float value;std::memcpy(&value,bytes,4);return value;}
// Explicit scalar operations match SSE mul/sub/div/add; no fused expression.
__declspec(noinline) static float nativeStarAdd(float a,float b) {return a+b;}
__declspec(noinline) static float nativeStarSub(float a,float b) {return a-b;}
__declspec(noinline) static float nativeStarMul(float a,float b) {return a*b;}
__declspec(noinline) static float nativeStarDiv(float a,float b) {return a/b;}
static NativeAmbientStarContinuous prepareNativeAmbientStarContinuous(const std::filesystem::path& source) {
    NativeAmbientStarContinuous owner;
    auto read=[&](const std::string& name,char* bytes,size_t size) {
        std::ifstream stream(source/name,std::ios::binary|std::ios::ate);
        if(!stream||stream.tellg()!=std::streamoff(size))throw std::runtime_error("Star source input size: "+name);
        stream.seekg(0);stream.read(bytes,std::streamsize(size));
        if(!stream)throw std::runtime_error("Star source input read: "+name);
    };
    std::array<char,32> parameters{};read("star-clock-parameters.bin",parameters.data(),parameters.size());
    owner.clockScale=nativeStarFloat(parameters.data());owner.phase=nativeStarFloat(parameters.data()+28);
    for(unsigned face=0;face<6;++face) {
        owner.faceHeight[face]=nativeStarFloat(parameters.data()+4+face*4);
        const auto prefix="stars-face"+std::to_string(face);
        auto& p=owner.faces[face];read(prefix+"-vs-b0.bin",p.vs.data(),p.vs.size());
        read(prefix+"-gs-b0.bin",p.gs.data(),p.gs.size());read(prefix+"-ps-b0.bin",p.ps.data(),p.ps.size());
    }
    return owner;
}
static bool prepareNativeAmbientStarFace(NativeAmbientStarContinuous& owner,unsigned face,float sharedNode3Delta) {
    if(face!=3) {
        constexpr float ceiling=80000.f; // actual CE479C4000, Star+14B8 constructor
        const float height=std::max(0.f,nativeStarSub(ceiling,owner.faceHeight.at(face)));
        // D7CED0 ->11775A0 stores controller34 once during node3 update.
        const float geometryDelta34=nativeStarMul(owner.clockScale,sharedNode3Delta);
        const float step=nativeStarMul(nativeStarDiv(nativeStarMul(height,geometryDelta34),ceiling),.5f);
        owner.phase=nativeStarAdd(owner.phase,step);
        if(owner.phase>=1.f)owner.phase=nativeStarAdd(owner.phase,-1.f); // one source subtraction, no fmod
    }
    // face3 packet is unused by E40540 but has the current shared phase.
    std::memcpy(owner.faces.at(face).vs.data()+64,&owner.phase,4);
    return face!=3;
}
template<class FaceConstants> static bool updateNativeAmbientStarFace(ID3D11DeviceContext* context,
    NativeAmbientStarContinuous& owner,const FaceConstants& constants,unsigned face,float sharedNode3Delta) {
    if(!prepareNativeAmbientStarFace(owner,face,sharedNode3Delta))return false;
    // The complete GS/PS packets stay at their same fixed source inputs.
    // Only this VS float changes; upload the retained full original352B CB.
    context->UpdateSubresource(constants.vs.Get(),0,nullptr,owner.faces[face].vs.data(),0,0);
    return true;
}
