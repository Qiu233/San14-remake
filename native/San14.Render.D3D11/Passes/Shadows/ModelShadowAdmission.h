#pragma once
#include "../../Scene/Models/CityModelAdmission.h"
#include <vector>

struct NativeCityGateShadowLight {
    unsigned mask{},f1C{},f1F{},f20{},wrapper0{};
};
struct NativeCityGateShadowAdmission {
    unsigned commonExclusion404{},selectedRecord1BD0{};
    std::vector<NativeCityGateShadowLight> lights;
};
inline unsigned nativeCityGateFirstCasterMask(const NativeCityGateShadowAdmission& source,
        const NativeModelWorldAdmission& world,const NativeModelCollectionCaller& caller) {
    const auto masks=nativeCityModelCurrentMasks(world,caller);
    if(!masks.shadowSlot||!source.selectedRecord1BD0)return 0;
    for(const auto& light:source.lights)if((light.mask&(world.exclusion78|source.commonExclusion404))==0) {
        // The first eligible directional light supplies this caster rule.
        const bool bit0=(unsigned(world.caster10C)&1)!=0;
        return (light.f1C&1)&&((light.f1C&2)?!bit0:bit0)?15u:0u;
    }
    return 0;
}
