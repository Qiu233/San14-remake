#pragma once
// Current Main numeric writes for the already selected normal193 owner.
// No Begin/Init/history/Exposure mutation. Full optics, LUT and pass selection
// still belong to their actual producers; see the companion source report.
#include "../Environment/SeasonFrame.h"
#include "PostProcessCamera.h"
#include "../../Camera/CameraState.h"

namespace source_current_season_post {
template<class T> inline T mainWord(const source_current_season::Frame& current,int offset) {
    if(offset>=0&&size_t(offset)+sizeof(T)<=current.main.size())
        return source_current_season::read<T>(current.main,size_t(offset));
    for(const auto& word:current.words)if(word.offset==offset){T value;std::memcpy(&value,&word.bits,4);return value;}
    throw std::runtime_error("Current normal post requires its actual Main word");
}
struct Selection {
    // Actual source CommonAux3F2 and current kind1 admission, not MainA00.
    uint32_t auxiliarySelector{};
    bool recordPresent{};
    uint32_t sourceType10B0{};
};
struct SourceLight {uint32_t object{},mask{},type10B0{};};
struct SeasonCallerSource {uint32_t season{},groupMask13F0{},fadeBits13F4{},lumiBits13F8{};std::vector<SourceLight> lights;};
struct CallerSources {uint32_t auxiliarySelector{},commonFlags8{},lockedAdaptationBits{};std::vector<SeasonCallerSource> seasons;};
inline CallerSources readCallerSources(const std::filesystem::path& path) {
    CallerSources out;std::ifstream input(path,std::ios::binary);uint32_t count{};
    auto word=[&](){uint32_t value{};input.read(reinterpret_cast<char*>(&value),4);if(!input)throw std::runtime_error("Current post caller source truncated");return value;};
    count=word();out.auxiliarySelector=word();out.commonFlags8=word();out.lockedAdaptationBits=word();
    out.seasons.resize(count);
    for(auto& season:out.seasons){season.season=word();season.groupMask13F0=word();season.fadeBits13F4=word();season.lumiBits13F8=word();
        season.lights.resize(word());for(auto& light:season.lights){light.object=word();light.mask=word();light.type10B0=word();}}
    if(input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Current post caller source trailing bytes");return out;
}
inline const SeasonCallerSource& currentCallerSource(const CallerSources& source,const source_current_season::Frame& current) {
    for(const auto& season:source.seasons)if(season.season==current.season)return season;
    throw std::runtime_error("Current post requires the installed season's actual caller source");
}
// The once-imported source supplies the selected p1020/1021/1022 branch for
// this no-later-edit caller scope. Any explicit later current words take their
// original priority; they are never overwritten by the loaded source values.
inline uint32_t currentLateWord(const source_current_season::Frame& current,int offset,uint32_t loadedSource) {
    for(const auto& word:current.words)if(word.offset==offset)return word.bits;return loadedSource;
}
inline const source_current_season::Light* firstSubset(const source_current_season::Frame& current,uint32_t selector) {
    if(!selector)return nullptr;
    const uint32_t mask=1u<<((selector-1)&31);
    for(const auto& light:current.lights)if((light.mask&~mask)==0)return &light;
    return nullptr;
}
struct Caller {
    Selection selection;uint32_t selectedObject{},lockedAdaptationBits{};
    bool adaptationLocked{},autoExposure{};
};
inline Caller prepareCaller(const CallerSources& source,const source_current_season::Frame& current,bool actualSelectedRecordAdmitted) {
    const auto& season=currentCallerSource(source,current);Caller out;
    out.selection.auxiliarySelector=source.auxiliarySelector;out.selection.recordPresent=actualSelectedRecordAdmitted;
    out.lockedAdaptationBits=source.lockedAdaptationBits;
    if(actualSelectedRecordAdmitted){
        const auto* selected=firstSubset(current,source.auxiliarySelector);
        if(!selected)throw std::runtime_error("Current post admitted View1B20 has no selected current record");
        out.selectedObject=selected->object;bool found=false;
        for(const auto& light:season.lights)if(light.object==selected->object&&light.mask==selected->mask){out.selection.sourceType10B0=light.type10B0;found=true;break;}
        if(!found)throw std::runtime_error("Current post selected record has no installed actual type10B0 source");
    }
    uint32_t mask=currentLateWord(current,0x13f0,season.groupMask13F0);
    uint32_t fadeBits=currentLateWord(current,0x13f4,season.fadeBits13F4),lumiBits=currentLateWord(current,0x13f8,season.lumiBits13F8);
    float fade,lumi;std::memcpy(&fade,&fadeBits,4);std::memcpy(&lumi,&lumiBits,4);
    out.adaptationLocked=(fade>0||lumi!=1)&&(mask&0x40000000u)!=0;
    out.autoExposure=(source.commonFlags8&(1u<<25))!=0||(mainWord<uint32_t>(current,0x1510)&1)!=0;
    return out;
}
// Caller supplies true current admission and the installed selected record's
// CBC770 mode. In mode6 the original source copies Main2B8 xyz to LightF80.
// Other modes need their actual selected F80; numeric328 does not contain it.
inline uint32_t publishMode6SelectedSun(const source_current_season::Frame& current,const Selection& selected,
    NativeNormalPostCamera& postCamera) {
    if(!selected.recordPresent){postCamera.selectedLightPresent=0;return 0;}
    const auto* light=firstSubset(current,selected.auxiliarySelector);
    if(!light)throw std::runtime_error("Current post selected View1B20 record absent");
    if(selected.sourceType10B0!=6)throw std::runtime_error("Current post selected F80 needs its separate source mode writer");
    std::memcpy(postCamera.selectedSunF80.data(),current.main.data()+0x2b8,12);
    postCamera.selectedLightPresent=1;return light->object;
}
struct Inputs {
    std::array<uint32_t,2> adaptationBits{},evBits{};
    std::array<float,4> nearFar{}; // CB18 difference,invNear,invDifference,-ratio.
    std::array<float,2> heat{};
    float heatCoefficient{},spriteThreshold{},userExposure{};
    uint32_t gatherC0{},enableDofA4{};
};
inline Inputs prepareSimpleCurrent(const source_current_season::Frame& current,bool actualAdaptationLock,
    uint32_t actualLockedAdaptationBits,bool actualAutoExposure) {
    Inputs out;
    out.adaptationBits={actualAdaptationLock?actualLockedAdaptationBits:mainWord<uint32_t>(current,0x1c4),
        actualAdaptationLock?actualLockedAdaptationBits:mainWord<uint32_t>(current,0x1c8)};
    out.evBits={mainWord<uint32_t>(current,0x1b8),mainWord<uint32_t>(current,0x1bc)};
    const bool swap=(mainWord<uint32_t>(current,0x158)&2)!=0;
    float clipNear=mainWord<float>(current,swap?0x144:0x140),clipFar=mainWord<float>(current,swap?0x140:0x144);
    float invNear=postCameraDiv(1,clipNear),invFar=postCameraDiv(1,clipFar);
    float difference=postCameraSub(invFar,invNear),ratio=postCameraDiv(invNear,difference);
    uint32_t sign;std::memcpy(&sign,&ratio,4);sign^=0x80000000u;std::memcpy(&ratio,&sign,4);
    out.nearFar={difference,invNear,postCameraDiv(1,difference),ratio};
    out.heat={mainWord<float>(current,0x13c0),mainWord<float>(current,0x13c4)};
    out.heatCoefficient=mainWord<float>(current,0x13c8);
    out.spriteThreshold=mainWord<float>(current,0x1324);
    out.userExposure=actualAutoExposure?-1:mainWord<float>(current,0x170);
    out.gatherC0=out.enableDofA4=mainWord<uint32_t>(current,0x1320)&7;
    return out;
}
inline bool cbFile(const std::string& key,const char* name){
    const std::string suffix=name;return key.size()>=suffix.size()&&key.compare(key.size()-suffix.size(),suffix.size(),suffix)==0;
}
// The held source normal scope retains optics-derived focus/coefficient,
// complete gather/Shape producer and Eye log/pow fields. This function updates
// only named lanes with these direct current writers. Invoke before Begin;
// its existing full-buffer uploader then consumes the modified templates.
template<class ContinuousPostOwner>
inline void publishSimpleCurrent(const Inputs& current,ContinuousPostOwner& post) {
    if(post.awaitingReturn)throw std::runtime_error("Current post must return before its next Main publication");
    post.heatCoefficient=current.heatCoefficient;
    post.gatherC0=current.gatherC0;post.enableDofA4=current.enableDofA4;
    for(auto& item:post.staticConstants){auto& bytes=item.second;
        if(cbFile(item.first,"cb18.bin")){
            if(bytes.size()!=144)throw std::runtime_error("Current normal DOF CB18 extent");
            std::memcpy(bytes.data()+12,&current.userExposure,4);
            std::memcpy(bytes.data()+32,&current.spriteThreshold,4);
            std::memcpy(bytes.data()+48,current.nearFar.data(),16);
            std::memcpy(bytes.data()+112,current.heat.data(),8);
        }
        if(cbFile(item.first,"cb24.bin")){
            if(bytes.size()!=64)throw std::runtime_error("Current normal Eye CB24 extent");
            std::memcpy(bytes.data(),current.adaptationBits.data(),8);
            std::memcpy(bytes.data()+8,current.evBits.data(),4); // CB24 retains only EV minimum here.
            float lo,hi;std::memcpy(&lo,&current.evBits[0],4);std::memcpy(&hi,&current.evBits[1],4);
            float scale=postCameraDiv(1,postCameraSub(hi,lo)),negative=postCameraMul(lo,scale);
            uint32_t sign;std::memcpy(&sign,&negative,4);sign^=0x80000000u;std::memcpy(&negative,&sign,4);
            std::memcpy(bytes.data()+32,&scale,4);std::memcpy(bytes.data()+36,&negative,4);
        }
    }
}
// Reuse the already source-tested projection/CB22 writer on this final Main,
// after publishing current selectedSun and other current Composite inputs.
template<class ContinuousPostOwner>
inline NativeNormalPostCameraProjection publishProjectionBeforeBegin(const source_current_season::Frame& current,
    NativeNormalPostCamera& postCamera,ContinuousPostOwner& post) {
    // The season publication is a distinct prepared-media packet: its camera
    // need not match the motion rig's currently accepted transform. Decode at
    // this input adapter; projection mathematics only receives the typed value.
    return updateNativeNormalPostCameraBeforeBegin(postCamera,
        source_camera::decodeCameraTransform(current.main),post);
}
} // namespace source_current_season_post
