#pragma once
#include "HdrLightingState.h"
#include <cstring>

namespace San14::Render {
struct HdrMaterialWrite { unsigned kind{},offset{},sourceOffset{},bytes{}; };

// A shader's two UV float3 rows retain the intervening four padding bytes.
// These values are per selected association, never a replacement material pool.
struct HdrHatchingValues {
    HdrVector ramp,occlusion;
    std::array<float,7> uv;
};

// The actual shader module retains this Time triple across associations,
// selected programs and frames. A modifier's data reset does not reset it.
struct HdrHatchingPhase {
    std::array<float,4> heldTime{};
    std::array<float,4> select(float interval,float clock) {
        using namespace source_camera;
        const float cycles=std::floor(D(clock,3600.f));
        const std::array<float,4> current{S(clock,M(cycles,3600.f)),cycles,3600.f,0};
        if(interval<=0)return current;
        const float delta=A(M(S(current[1],heldTime[1]),current[2]),S(current[0],heldTime[0]));
        if(interval<std::fabs(delta)) {
            heldTime=current;
            // Source quantizes the elapsed delta, including its sign. It
            // does not snap the absolute current clock to an interval grid.
            heldTime[0]=S(heldTime[0],std::fmod(delta,interval));
        }
        return heldTime;
    }
};

inline HdrHatchingValues prepareHdrHatching(const HdrMaterialModifier& input,float clock,HdrHatchingPhase& owner) {
    using namespace source_camera;
    // D724C0's current Generic Time triple and 9A00E0's signed fractions.
    // This consumer borrows Common time and never advances any clock.
    auto fraction=[](float value) {float whole;return std::modf(value,&whole);};
    const auto time=owner.select(input.updateInterval,clock);
    auto phase=[&](float velocity) {
        return fraction(A(M(velocity,time[0]),M(fraction(M(time[2],velocity)),time[1])));
    };
    const auto rotation=sinCos(M(input.rotationDegrees,0.017453292f));
    const float sine=rotation[0],cosine=rotation[1];
    return {{input.rampScale,input.rampStart,D(1,input.rampFadeDistance),input.indexedRampScale},
        {input.occlusionScale,input.occlusionStart,D(1,input.occlusionFadeDistance),0},
        {M(cosine,input.repeat[0]),M(sine,input.repeat[1]),flip(M(phase(input.velocity[0]),input.repeat[0])),0,
         flip(M(sine,input.repeat[0])),M(cosine,input.repeat[1]),M(phase(input.velocity[1]),input.repeat[1])}};
}

inline void writeHdrMaterialModifiers(std::span<const HdrMaterialWrite> writes,
    const HdrLightState& selected,bool fadeAllowed,const std::optional<HdrHatchingValues>& hatching,
    std::span<char> constants) {
    for(const auto& write:writes) {
        const bool fresh=write.kind<3?selected.preservesFreshHatching:
            selected.preservesFreshShadowFade||!fadeAllowed;
        if(!fresh&&!selected.modifier.material)
            throw std::runtime_error("Current HDR material plugin has no selected modifier publication");
        HdrMaterialModifier reset;
        reset.rampStart=-1;reset.repeat={1,1};
        const auto& input=fresh?reset:*selected.modifier.material;
        const void* source{};
        switch(write.kind) {
            case 0:
                if(!hatching)throw std::runtime_error("HDR ramp has no actual shader Hatching module");
                source=&hatching->ramp;break;
            case 1:
                if(!hatching)throw std::runtime_error("HDR occlusion has no actual shader Hatching module");
                source=&hatching->occlusion;break;
            case 2:
                if(!hatching)throw std::runtime_error("HDR UV has no actual shader Hatching module");
                source=hatching->uv.data();
                break;
            case 3:source=&input.shadowMaskFade;break;
            default:throw std::runtime_error("HDR material semantic kind");
        }
        std::memcpy(constants.data()+write.offset,static_cast<const char*>(source)+write.sourceOffset,write.bytes);
    }
}
} // namespace San14::Render
