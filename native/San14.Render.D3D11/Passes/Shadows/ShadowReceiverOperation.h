#pragma once
#include "ShadowReceiverMaterial.h"
#include "ShadowReceiverProgramBank.h"
#include "PatternTransform.h"
#include "../Environment/SeasonFrame.h"
#include "../../Scene/View/PreparedMapView.h"
#include <algorithm>

namespace San14::Render {
inline std::uint32_t receiverShadowQuery(std::uint32_t mode) {
    if(mode>18)mode=0;
    constexpr std::array<std::uint32_t,12> values{11,8,2,3,4,5,8,8,8,11,11,11};
    return mode<values.size()?values[mode]:mode;
}
inline ShaderFeatureKey prepareShadowReceiverFeature(const source_current_season::Light& light,
    const source_current_season::Frame& current,const PreparedMapView& view) {
    if(!current.hdrLighting)throw std::runtime_error("Receiver requires current typed light modifiers");
    const auto& lighting=light.lighting;
    auto upper=lighting.hemisphere.upperDiffuse,lower=lighting.hemisphere.lowerDiffuse;
    if(lighting.modifier.present) {
        const auto factor=lighting.modifier.hemisphereFactor;
        if(factor!=1) {
            auto scale=[&](ReceiverVector value) {return ReceiverVector{
                source_camera::M(value.X,factor),source_camera::M(value.Y,factor),
                source_camera::M(value.Z,factor),value.W};};
            upper=scale(upper);lower=scale(lower);
        }
    }
    const auto hemi=positiveHdrRgb(upper)||positiveHdrRgb(lower)?1u:0u;
    if(!current.shadow.selectedLightGroup)throw std::runtime_error("Receiver current light-group input is unresolved");
    std::uint32_t flags=0,shadow=0,query=0;
    if(*current.shadow.selectedLightGroup>0&&(view.frame().activeGroups&0x40000000u)&&light.shadowModes.receive) {
        shadow=light.shadowModes.receive;
        if(shadow==0x6c){shadow=8;flags|=0x4000;}
        if(shadow==8||shadow==9) {
            if(!current.shadow.planeScale)
                throw std::runtime_error("Receiver active shadow needs its current plane scale");
            if(*current.shadow.planeScale<=0)shadow=1;
        }
        if(!current.shadow.compactMode||!view.hdrInputs().compactShadowProvider)
            throw std::runtime_error("Receiver active shadow needs its actual compact CPU provider");
        query=receiverShadowQuery(std::uint32_t(*current.shadow.compactMode));
        if(shadow<=3)flags|=0x100;
        flags|=0x400;
    }
    const auto fog=std::uint32_t(lighting.fog.mode);
    return {flags,1,0,0,hemi,1,fog,fog==7?2u:1u,shadow,query,0,1};
}
inline std::vector<std::size_t> prepareShadowReceiverRecords(const source_current_season::Frame& current) {
    if(!current.shadow.selectedLightGroup)throw std::runtime_error("Receiver current light-group input is unresolved");
    const auto exclusion=~(1u<<((std::uint32_t(*current.shadow.selectedLightGroup)-1u)&31u));
    std::vector<std::size_t> records;
    for(std::size_t index=0;index<current.lights.size();++index)
        if(!(current.lights[index].mask&exclusion))records.push_back(index);
    return records;
}
struct ShadowReceiverPatternGroup {
    std::uint32_t mask{};
    const PatternLightModifier* modifier{};
};
inline std::vector<ShadowReceiverPatternGroup> prepareShadowReceiverPatternGroups(
    const source_current_season::Frame& current,std::size_t outer,std::uint32_t activeGroups) {
    std::vector<ShadowReceiverPatternGroup> groups;
    const PatternLightModifier absent;
    for(std::size_t index=0;index<current.lights.size();++index) {
        const auto& inner=current.lights[index];
        if(index==outer||!(inner.mask&activeGroups))continue;
        const auto& modifier=inner.patternModifier;
        auto found=std::find_if(groups.begin(),groups.end(),[&](const auto& group) {
            return samePatternModifierGroup(group.modifier?*group.modifier:absent,modifier);
        });
        if(found!=groups.end())found->mask|=inner.mask;
        else {
            if(groups.size()==4)throw std::runtime_error("Receiver supports at most four distinct Pattern modifier groups");
            groups.push_back({inner.mask,modifier.present?&modifier:nullptr});
        }
    }
    if(groups.empty())groups.push_back({15,nullptr});
    return groups;
}
inline void installShadowReceiverRecord(ShadowReceiverMaterial& material,const source_current_season::Light& light,
    const source_current_season_shadow::ReceiverStyle& style) {
    material.direction=light.lighting.directional[0].direction;
    material.ambient={0,0,0,light.lighting.ambient.W};
    material.depthFade=style.depthFade;
    if(light.receiverBias.scale>0)material.receiverBias=light.receiverBias;
}
} // namespace San14::Render
