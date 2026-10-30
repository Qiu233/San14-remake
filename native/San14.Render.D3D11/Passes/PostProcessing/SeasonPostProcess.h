#pragma once
#include "../../Runtime/PreparedFileInput.h"
// Seasonal post-processing selection and current settings. Imported packet
// layout belongs to SeasonFrame; retained optics, history and GPU resources
// remain with their ordinary post-processing owners.
#include "../Environment/SeasonFrame.h"
#include "PostProcessCamera.h"

namespace source_current_season_post {
enum class SelectedLightMode:uint32_t {CameraSun=6};
inline constexpr uint32_t AdaptationTransitionGroup=1u<<30;
template<class T> inline T requireSetting(const std::optional<T>& value){
    if(!value)throw std::runtime_error("Current normal post requires its actual Main word");
    return *value;
}
struct Selection {
    // Actual source CommonAux3F2 and current kind1 admission, not MainA00.
    uint32_t auxiliarySelector{};
    bool recordPresent{};
    SelectedLightMode lightMode{};
};
struct SourceLight {uint32_t object{},groupMask{};SelectedLightMode mode{};};
struct SeasonCallerSource {uint32_t season{},transitionGroups{};float fade{},luminance{};std::vector<SourceLight> lights;};
struct CallerSources {uint32_t auxiliarySelector{};bool automaticExposure{};float lockedAdaptation{};std::vector<SeasonCallerSource> seasons;};
inline CallerSources readCallerSources(const std::filesystem::path& path) {
    CallerSources out;NativePreparedInputFile input(path,std::ios::binary);uint32_t count{};
    auto word=[&](){uint32_t value{};input.read(reinterpret_cast<char*>(&value),4);if(!input)throw std::runtime_error("Current post caller source truncated");return value;};
    auto scalar=[&](){const auto bits=word();float value;std::memcpy(&value,&bits,4);return value;};
    count=word();out.auxiliarySelector=word();out.automaticExposure=(word()&(1u<<25))!=0;out.lockedAdaptation=scalar();
    out.seasons.resize(count);
    for(auto& season:out.seasons){season.season=word();season.transitionGroups=word();season.fade=scalar();season.luminance=scalar();
        season.lights.resize(word());for(auto& light:season.lights){light.object=word();light.groupMask=word();light.mode=static_cast<SelectedLightMode>(word());}}
    if(input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Current post caller source trailing bytes");return out;
}
inline const SeasonCallerSource& currentCallerSource(const CallerSources& source,uint32_t currentSeason) {
    for(const auto& season:source.seasons)if(season.season==currentSeason)return season;
    throw std::runtime_error("Current post requires the installed season's actual caller source");
}
// Selection uses ordered group-subset membership, independently of shadow's
// first-intersecting query. A zero selector deliberately admits no record.
inline const LightIdentity* firstSubset(const Settings& current,uint32_t selector) {
    if(!selector)return nullptr;
    const uint32_t mask=1u<<((selector-1)&31);
    for(const auto& light:current.lights)if((light.groupMask&~mask)==0)return &light;
    return nullptr;
}
struct Caller {
    Selection selection;uint32_t selectedObject{};float lockedAdaptation{};
    bool adaptationLocked{},autoExposure{};
};
inline Caller prepareCaller(const CallerSources& source,uint32_t currentSeason,const Settings& current,bool actualSelectedRecordAdmitted) {
    const auto& season=currentCallerSource(source,currentSeason);Caller out;
    out.selection.auxiliarySelector=source.auxiliarySelector;out.selection.recordPresent=actualSelectedRecordAdmitted;
    out.lockedAdaptation=source.lockedAdaptation;
    if(actualSelectedRecordAdmitted){
        const auto* selected=firstSubset(current,source.auxiliarySelector);
        if(!selected)throw std::runtime_error("Current post admitted View1B20 has no selected current record");
        out.selectedObject=selected->object;bool found=false;
        for(const auto& light:season.lights)if(light.object==selected->object&&light.groupMask==selected->groupMask){out.selection.lightMode=light.mode;found=true;break;}
        if(!found)throw std::runtime_error("Current post selected record has no installed actual type10B0 source");
    }
    const uint32_t mask=current.transition.groupMask.value_or(season.transitionGroups);
    const float fade=current.transition.fade.value_or(season.fade),lumi=current.transition.luminance.value_or(season.luminance);
    out.adaptationLocked=(fade>0||lumi!=1)&&(mask&AdaptationTransitionGroup)!=0;
    // Keep source flag short-circuit: a missing current flag is irrelevant when
    // the common caller already enables automatic exposure.
    out.autoExposure=source.automaticExposure||requireSetting(current.automaticExposure);
    return out;
}
// Caller supplies true current admission and the installed selected record's
// CBC770 mode. In mode6 the original source copies Main2B8 xyz to LightF80.
// Other modes need their actual selected F80; numeric328 does not contain it.
inline uint32_t publishMode6SelectedSun(const Settings& current,const Selection& selected,
    NativeNormalPostCamera& postCamera) {
    if(!selected.recordPresent){postCamera.selectedLightPresent=0;return 0;}
    const auto* light=firstSubset(current,selected.auxiliarySelector);
    if(!light)throw std::runtime_error("Current post selected View1B20 record absent");
    if(selected.lightMode!=SelectedLightMode::CameraSun)throw std::runtime_error("Current post selected F80 needs its separate source mode writer");
    postCamera.selectedSunF80=current.selectedSunDirection;
    postCamera.selectedLightPresent=1;return light->object;
}
struct Inputs {
    std::array<float,2> adaptation{},exposureRange{};
    std::array<float,4> nearFar{}; // CB18 difference,invNear,invDifference,-ratio.
    std::array<float,2> heat{};
    float heatCoefficient{},spriteThreshold{},userExposure{};
    uint32_t gatherMode{},depthOfFieldMode{};
};
inline Inputs prepareSimpleCurrent(const Settings& current,bool actualAdaptationLock,
    float actualLockedAdaptation,bool actualAutoExposure) {
    Inputs out;
    out.adaptation={actualAdaptationLock?actualLockedAdaptation:current.adaptation[0],
        actualAdaptationLock?actualLockedAdaptation:current.adaptation[1]};
    out.exposureRange=current.exposureRange;
    const auto& optics=current.camera.optics;
    float clipNear=optics.reversedDepth?optics.farPlane:optics.nearPlane;
    float clipFar=optics.reversedDepth?optics.nearPlane:optics.farPlane;
    float invNear=postCameraDiv(1,clipNear),invFar=postCameraDiv(1,clipFar);
    float difference=postCameraSub(invFar,invNear),ratio=postCameraDiv(invNear,difference);
    uint32_t sign;std::memcpy(&sign,&ratio,4);sign^=0x80000000u;std::memcpy(&ratio,&sign,4);
    out.nearFar={difference,invNear,postCameraDiv(1,difference),ratio};
    out.heat={requireSetting(current.heat[0]),requireSetting(current.heat[1])};
    out.heatCoefficient=requireSetting(current.heatCoefficient);
    out.spriteThreshold=requireSetting(current.spriteThreshold);
    out.userExposure=actualAutoExposure?-1:current.userExposure;
    out.gatherMode=out.depthOfFieldMode=requireSetting(current.depthOfFieldMode)&7;
    return out;
}
inline bool cbFile(const std::string& key,const char* name){
    const std::string suffix=name;return key.size()>=suffix.size()&&key.compare(key.size()-suffix.size(),suffix.size(),suffix)==0;
}
template<class PostTimeline>
inline void publishCurrentOptics(const Settings& current,bool autoExposure,int64_t cameraIdentity,PostTimeline& post) {
    if(!current.currentOptics)return;
    const auto& source=*current.currentOptics;const auto& v=source.values;
    auto fields=post.templates.dofProfile;
    if(source.width!=fields.scene.width||source.height!=fields.contextHeight)
        throw std::runtime_error("Current optics needs its real resized post resource owner");
    const auto flags=requireSetting(current.depthOfFieldMode);
    if(flags&0x30u)throw std::runtime_error("Current ordinary post has no FC/FD extent transition owner");
    fields.enabled=(flags&7)!=0;fields.gatherType=int(flags&7);fields.changeFStop=source.apertureChanged!=0;
    fields.spriteEnabled=requireSetting(current.spriteThreshold)>=0;fields.diaphragmType=int(source.diaphragm);
    if(fields.enabled&&(!fields.spriteEnabled||fields.diaphragmType!=0))
        throw std::runtime_error("Current ordinary post has no declared non-sprite/image resource union");
    fields.bladeCount=int(source.blades);fields.gatherMaximum=v[0];fields.focusDistance=v[1];fields.lensCoefficient=v[2];
    fields.screenAspect=v[3];fields.anamorphic={v[4],v[5]};fields.stopPolyRate=v[6];
    fields.automaticExposure=autoExposure?1:0;fields.userExposure=current.userExposure;
    fields.spriteThreshold=requireSetting(current.spriteThreshold);
    fields.nearFar={current.camera.optics.reversedDepth?current.camera.optics.farPlane:current.camera.optics.nearPlane,
        current.camera.optics.reversedDepth?current.camera.optics.nearPlane:current.camera.optics.farPlane};
    fields.heat={requireSetting(current.heat[0]),requireSetting(current.heat[1]),0};
    post.templates.currentDof=fields;post.templates.currentOpticsCamera=cameraIdentity;
    // Composite's other fields and Eye/exposure histories remain current.
    for(auto& item:post.templates.constants)if(cbFile(item.first,"cb22.bin")) {
        auto& bytes=item.second;
        if(bytes.size()!=208)throw std::runtime_error("Current optics Composite CB22 extent");
        std::memcpy(bytes.data()+16,&v[3],4);
        const std::array<float,4> limb{v[7],v[8],postCameraDiv(1,v[9]),v[10]};
        std::memcpy(bytes.data()+80,limb.data(),16);
    }
}
// The held source normal scope retains optics-derived focus/coefficient,
// complete gather/Shape producer and Eye log/pow fields. This function updates
// only named lanes with these direct current writers. Invoke before Begin;
// its existing full-buffer uploader then consumes the modified templates.
template<class PostTimeline>
inline void publishSimpleCurrent(const Inputs& current,PostTimeline& post) {
    if(post.history.applicationInProgress())throw std::runtime_error("Current post must return before its next Main publication");
    post.templates.heatCoefficient=current.heatCoefficient;
    post.templates.gatherMode=current.gatherMode;post.templates.depthOfFieldMode=current.depthOfFieldMode;
    for(auto& item:post.templates.constants){auto& bytes=item.second;
        if(cbFile(item.first,"cb18.bin")){
            if(bytes.size()!=144)throw std::runtime_error("Current normal DOF CB18 extent");
            std::memcpy(bytes.data()+12,&current.userExposure,4);
            std::memcpy(bytes.data()+32,&current.spriteThreshold,4);
            std::memcpy(bytes.data()+48,current.nearFar.data(),16);
            std::memcpy(bytes.data()+112,current.heat.data(),8);
        }
        if(cbFile(item.first,"cb24.bin")){
            if(bytes.size()!=64)throw std::runtime_error("Current normal Eye CB24 extent");
            std::memcpy(bytes.data(),current.adaptation.data(),8);
            std::memcpy(bytes.data()+8,current.exposureRange.data(),4); // CB24 retains only EV minimum here.
            const float lo=current.exposureRange[0],hi=current.exposureRange[1];
            float scale=postCameraDiv(1,postCameraSub(hi,lo)),negative=postCameraMul(lo,scale);
            uint32_t sign;std::memcpy(&sign,&negative,4);sign^=0x80000000u;std::memcpy(&negative,&sign,4);
            std::memcpy(bytes.data()+32,&scale,4);std::memcpy(bytes.data()+36,&negative,4);
        }
    }
}
// Reuse the already source-tested projection/CB22 writer on this final Main,
// after publishing current selectedSun and other current Composite inputs.
template<class PostTimeline>
inline NativeNormalPostCameraProjection publishProjectionBeforeBegin(const Settings& current,
    NativeNormalPostCamera& postCamera,PostTimeline& post) {
    // Season optics are independently published; never substitute the motion rig.
    return updateNativeNormalPostCameraBeforeBegin(postCamera,current.camera,post);
}
} // namespace source_current_season_post
