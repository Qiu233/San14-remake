#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "../../Camera/CameraState.h"
#include "OctreeAdmission.h"

// Main source domains only. No GPU, alternate camera, source I/O per request,
// shader override or claim that these direct stores prepare current lighting.
namespace source_far_main {
using source_camera::Prefix;using source_camera::Zoom;using source_camera::CurrentRenderMode;
struct Word {uint32_t offset{},bits{};};
struct RegistryEntry {uint32_t sourceIndex{},objectId{},group{},type{},modelOrdinal{},primitiveCount{};};
struct Profile {
    int quality{};uint32_t initialAppFlags{},selectedCloudy{},initialPriorCloudy{},changedAltitude{};
    int phaseStart{},phaseEnd{},phaseDenominator{};
    std::array<std::vector<Word>,5> direct;
    std::vector<RegistryEntry> registry;
};
struct Owner {
    uint32_t priorCloudy{};uint16_t appFlags{};
    source_mode2_octree::DisplayConfig displayConfig{1,1,1,1,1,1};
};
struct Current {
    CurrentRenderMode mode{};Prefix directPrefix{};std::vector<Word> preparedWords;
    uint16_t appFlags{};uint32_t sceneTypeMask{};bool sceneConfigPublished{};
    std::array<bool,7> resetGroups{};
};
inline Profile loadProfile(const std::filesystem::path& path){
    NativePreparedInputFile in(path,std::ios::binary);using source_camera::read;
    if(!in||read<std::array<char,8>>(in)!=std::array<char,8>{'F','A','R','M','A','I','N','1'})throw std::runtime_error("Far Main source profile");
    Profile p;p.quality=read<int>(in);p.initialAppFlags=read<uint32_t>(in);p.selectedCloudy=read<uint32_t>(in);p.initialPriorCloudy=read<uint32_t>(in);p.changedAltitude=read<uint32_t>(in);
    p.phaseStart=read<int>(in);p.phaseEnd=read<int>(in);p.phaseDenominator=read<int>(in);
    for(auto& mode:p.direct){auto count=read<uint32_t>(in);mode.resize(count);for(auto& word:mode)word=read<Word>(in);}
    auto count=read<uint32_t>(in);p.registry.resize(count);for(auto& entry:p.registry)entry=read<RegistryEntry>(in);
    if(in.peek()!=std::char_traits<char>::eof()||p.phaseDenominator==0)throw std::runtime_error("Far Main profile size/phase domain");return p;
}
inline Owner createOwner(const Profile& p){return {p.initialPriorCloudy,uint16_t(p.initialAppFlags),{1,1,1,1,1,1}};}
inline CurrentRenderMode currentMode(Zoom zoom){
    return {source_camera::classifyRenderMode(zoom),source_camera::A(float(zoom.band),source_camera::D(float(zoom.fraction),10000.f))};
}
inline Current prepareCurrent(const Profile& p,Owner& owner,Zoom zoom,const Prefix& actualRawCamera){
    Current out;out.mode=currentMode(zoom);out.directPrefix=actualRawCamera;
    out.preparedWords=p.direct.at(out.mode.sceneMode);
    uint32_t cloudy=p.selectedCloudy;
    for(auto& word:out.preparedWords)if(word.offset==0x1D0)cloudy=word.bits;
    const uint32_t altitude=source_camera::get<float>(reinterpret_cast<const uint8_t*>(&owner.priorCloudy))==source_camera::get<float>(reinterpret_cast<const uint8_t*>(&cloudy))?0:p.changedAltitude;
    for(auto& word:out.preparedWords){if(word.offset==0x210)word.bits=altitude;
        if(word.offset+4<=out.directPrefix.size())source_camera::put(out.directPrefix.data()+word.offset,word.bits);}
    if(out.mode.sceneMode>=3){
        using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
        const float t=S(out.mode.zoomLevel,5.f),start=D(float(p.phaseStart),float(p.phaseDenominator)),end=D(float(p.phaseEnd),float(p.phaseDenominator));
        const float date=A(M(start,S(1.f,t)),M(end,t));source_camera::put(out.directPrefix.data()+0x204,date);
        out.preparedWords.push_back({0x204,source_camera::get<uint32_t>(reinterpret_cast<const uint8_t*>(&date))});
    }
    owner.priorCloudy=cloudy;
    out.sceneConfigPublished=source_mode2_octree::publishAfterV418(owner.displayConfig,owner.appFlags,p.quality,out.mode.sceneMode);
    out.appFlags=owner.appFlags;out.sceneTypeMask=source_mode2_octree::sceneTypeMask(owner.displayConfig);
    out.resetGroups[1]=p.quality>=2||out.mode.sceneMode>=2;
    out.resetGroups[2]=out.mode.sceneMode>=3;out.resetGroups[3]=out.resetGroups[6]=p.quality>=2;
    return out;
}
struct CurrentModelMasks {uint32_t first{},second{},shadow{};};
// Only imported entries registered into this FieldScene group directory use
// the reset. City/Army/procedural trees retain their independent actual owners.
inline CurrentModelMasks captureCurrentModelMasks(const RegistryEntry& entry,const Current& frame,CurrentModelMasks actualPrior){
    if(entry.type!=1||entry.group>=frame.resetGroups.size())throw std::runtime_error("Far Main registered Model group/type");
    return frame.resetGroups[entry.group]?CurrentModelMasks{}:actualPrior;
}
inline uint32_t captureCurrentEffectMask(const RegistryEntry& entry,const Current& frame,uint32_t actualPriorD8){
    if(entry.type!=0x100||entry.group>=frame.resetGroups.size())throw std::runtime_error("Far Main registered Effect group/type");
    return frame.resetGroups[entry.group]?0:actualPriorD8;
}
// DB7770's normal end/reset follows consumption of the same request snapshot
// by ordinary, secondary and shadow. It is not permanent catalog deletion.
inline void restoreNormalWorldMasks(CurrentModelMasks& current){current={0xFFFFFFFFu,0xFFFFFFFFu,0xFFFFFFFFu};}
} // namespace source_far_main
