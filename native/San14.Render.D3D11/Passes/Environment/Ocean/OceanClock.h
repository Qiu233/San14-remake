#pragma once
#include "OceanSurfaceSettings.h"
#include <xmmintrin.h>

namespace San14::Render {
// Fresh Ocean lifetime. Elapsed grid time and shader wave time deliberately
// retain separate double and float accumulators.
struct OceanClock {
    double accumulatedTime{};
    float waveTime{},lastCommonClock{};
    void advance(float currentCommonClock) {
        float delta=_mm_cvtss_f32(_mm_sub_ss(_mm_set_ss(currentCommonClock),_mm_set_ss(lastCommonClock)));
        if(lastCommonClock>currentCommonClock)
            delta=_mm_cvtss_f32(_mm_add_ss(_mm_set_ss(delta),_mm_set_ss(3600.0f)));
        lastCommonClock=currentCommonClock;
        accumulatedTime+=double(delta);waveTime=delta+waveTime;
    }
};
struct OceanAnimationPhases {
    float normal{},waveTime{};
    std::array<float,2> bubbles{};
};
inline OceanAnimationPhases deriveOceanAnimationPhases(const OceanClock& clock,
    const OceanSurfaceSettings& settings,std::uint32_t normalVolumeDepth) {
    OceanAnimationPhases result;result.waveTime=clock.waveTime;
    float integral;
    const float rate=oceanNormalAnimationSpeed(settings)/float(normalVolumeDepth);
    const float scaled=rate*float(clock.accumulatedTime);
    result.normal=std::modf(scaled,&integral);
    for(unsigned layer=0;layer<2;++layer) {
        float phase=0;
        if(settings.bubbleMode<2)phase=float(settings.bubbleMode);
        else if(const float period=settings.bubblePeriods[layer];period>0) {
            phase=float(std::fmod(clock.accumulatedTime,double(period))/double(period));
            if(static_cast<long long>(clock.accumulatedTime/double(period))&1)phase=1.0f-phase;
            phase=((phase*6.0f-15.0f)*phase+10.0f)*phase*phase*phase;
        }
        result.bubbles[layer]=phase;
    }
    return result;
}
} // namespace San14::Render
