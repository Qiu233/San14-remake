// Fixed source-created61 City/Gate World scope; camera requests are current.
// Include after model-continuous-native.h for its original OBB kernel.
#pragma once
#include "../../../Runtime/PreparedFileInput.h"
namespace source_accessory_population {
struct Unit {std::uint32_t index{},requested{};std::int32_t force{};std::array<float,3> position{};};
struct Placement {std::uint32_t unitIndex{},ordinal{};std::array<float,12> root{};std::array<float,20> obb{};};
struct Profile {std::vector<Unit> units;std::vector<Placement> placements;};
struct Request {std::uint32_t placementIndex{},unitIndex{},ordinal{};bool mainVisible{},animateNear{};};
struct Frame {std::vector<Request> requests;std::vector<std::uint32_t> admittedUnits;};
inline Profile loadProfile(const fs::path& path){
    NativePreparedInputFile file(path,std::ios::binary);if(!file)throw std::runtime_error("Accessory population source profile absent");
    std::uint32_t unitCount{},placementCount{};file.read(reinterpret_cast<char*>(&unitCount),4);file.read(reinterpret_cast<char*>(&placementCount),4);
    Profile p;p.units.resize(unitCount);p.placements.resize(placementCount);
    for(auto& u:p.units){file.read(reinterpret_cast<char*>(&u.index),4);file.read(reinterpret_cast<char*>(&u.force),4);
        file.read(reinterpret_cast<char*>(&u.requested),4);file.read(reinterpret_cast<char*>(u.position.data()),12);}
    for(auto& g:p.placements){file.read(reinterpret_cast<char*>(&g.unitIndex),4);file.read(reinterpret_cast<char*>(&g.ordinal),4);
        file.read(reinterpret_cast<char*>(g.root.data()),48);file.read(reinterpret_cast<char*>(g.obb.data()),80);}
    if(!file)throw std::runtime_error("Accessory population source profile truncated");return p;
}
inline float A(float a,float b){return modelContinuousAdd(a,b);}
inline float S(float a,float b){return modelContinuousSub(a,b);}
inline float M(float a,float b){return modelContinuousMul(a,b);}
inline float D(float a,float b){return _mm_cvtss_f32(_mm_div_ss(_mm_set_ss(a),_mm_set_ss(b)));}
inline std::array<float,4> project(const std::array<float,3>& v,const char* prefix){
    float m[16];std::memcpy(m,prefix+0x100,64);return {
        A(A(A(M(v[1],m[4]),M(v[0],m[0])),M(v[2],m[8])),m[12]),
        A(A(A(M(v[0],m[1]),M(v[1],m[5])),M(v[2],m[9])),m[13]),
        A(A(A(M(v[0],m[2]),M(v[1],m[6])),M(v[2],m[10])),m[14]),
        A(A(A(M(v[0],m[3]),M(v[1],m[7])),M(v[2],m[11])),m[15])};
}
// This is the real caller gate. actualResourceCameraClone is an explicitly
// published clone, independently of which Camera currently renders Main.
// Returned requests become successful groups only after the caller's real
// shared Model/action/material creation. Never turn requested into live count.
struct UnitAdmission {bool visible{},animateNear{};};
inline UnitAdmission unitAdmission(const std::array<float,3>& position,const char* actualAppCameraPrefix,
    const char* actualResourceCameraClone,int actualAppMode,float actualEcB0,double actualControllerDistance90,
    bool actualGlobalClipBypass=false){
    if(actualAppMode>=3||!actualResourceCameraClone)return {};
    float eye[3];std::memcpy(eye,actualAppCameraPrefix+0x10,12);float distanceLimit=float(actualControllerDistance90);
    int floor=int(std::floor(actualEcB0));float margin=floor<2?1.5f:1.1000000238418579f;
    float x=S(eye[0],position[0]),y=S(eye[1],position[1]),z=S(eye[2],position[2]);
    float distance=_mm_cvtss_f32(_mm_sqrt_ss(_mm_set_ss(A(A(M(x,x),M(y,y)),M(z,z)))));
    if(!(distance<=A(distanceLimit,distanceLimit)))return {};
    auto clip=project(position,actualResourceCameraClone);float reciprocal=D(1.f,clip[3]);
    x=M(clip[0],reciprocal);y=M(clip[1],reciprocal);z=M(clip[2],reciprocal);
    if(!actualGlobalClipBypass&&(x<=-margin||margin<=x||y<=-margin||margin<=y||z<=0.f||margin<=z))return {};
    return {true,floor<2||(floor<4&&distance<M(distanceLimit,1.5f))};
}
inline Frame prepare(const Profile& p,const char* actualAppCameraPrefix,const char* actualResourceCameraClone,
    int actualAppMode,float actualEcB0,double actualControllerDistance90,
    const std::array<std::array<float,4>,6>& mainPlanes,bool actualGlobalClipBypass){
    Frame f;std::vector<bool> admitted(p.units.size()),animate(p.units.size());
    if(actualAppMode>=3||!actualResourceCameraClone)return f;
    for(const auto& u:p.units){
        if(u.force<1||u.force>51)continue;
        auto gate=unitAdmission(u.position,actualAppCameraPrefix,actualResourceCameraClone,actualAppMode,actualEcB0,actualControllerDistance90,actualGlobalClipBypass);
        if(!gate.visible)continue;
        admitted[u.index]=true;animate[u.index]=gate.animateNear;f.admittedUnits.push_back(u.index);
    }
    for(std::uint32_t i=0;i<p.placements.size();++i){const auto& g=p.placements[i];if(!admitted[g.unitIndex])continue;
        NativeModelContinuousInstance instance;instance.mode=0;instance.obb=g.obb;
        f.requests.push_back({i,g.unitIndex,g.ordinal,modelContinuousVisible(instance,mainPlanes),animate[g.unitIndex]});}
    return f;
}
// Called only for a completed original palette. Root is already in these rows;
// the original source48B affine instance stream is independently retained.
inline std::array<char,6256> vertex(const char* currentCameraPrefix,const float* activeWorldRows,unsigned rowCount){
    std::array<char,6256> b{};std::memcpy(b.data(),currentCameraPrefix+0x100,64);std::memcpy(b.data()+64,activeWorldRows,rowCount*16);
    std::uint32_t skin[3]{1,1,0};for(unsigned i=0;i<3;++i)std::memcpy(b.data()+6208+i*16,skin+i,4);return b;
}
inline std::array<char,80> regularPixel(const char* currentCameraPrefix,const float* actualSourceSpec,int actualSourceMtrID){
    std::array<char,80> b{};std::memcpy(b.data(),actualSourceSpec,16);
    const char* source=currentCameraPrefix+0x80;
    for(unsigned r=0;r<3;++r)for(unsigned c=0;c<4;++c)std::memcpy(b.data()+16+r*16+c*4,source+(c*4+r)*4,4);
    std::memcpy(b.data()+64,&actualSourceMtrID,4);return b;
}
}
