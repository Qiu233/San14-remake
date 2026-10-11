#pragma once
#include <cmath>
#include <stdexcept>

namespace San14::Render {
struct OceanWaveSettings {
    float windX{}, windZ{}, amplitude{}, choppyFactor{}, smallWaveSuppressionLength{}, gravity{};
    bool operator==(const OceanWaveSettings&) const = default;
};
inline void validateOceanWaveSettings(const OceanWaveSettings& wave) {
    if(!std::isfinite(wave.windX)||!std::isfinite(wave.windZ)||!std::isfinite(wave.amplitude)||
       !std::isfinite(wave.choppyFactor)||!std::isfinite(wave.smallWaveSuppressionLength)||
       !std::isfinite(wave.gravity)||wave.gravity<=0||wave.smallWaveSuppressionLength<0)
        throw std::runtime_error("Ocean waves require finite scalars, positive gravity and nonnegative suppression");
    const float windSquared=(0.0f+wave.windX*wave.windX)+wave.windZ*wave.windZ;
    if(!std::isfinite(windSquared)||!std::isfinite(windSquared*windSquared)||
       !std::isfinite(wave.gravity*wave.gravity)||wave.gravity*wave.gravity==0)
        throw std::runtime_error("Ocean wave wind or gravity exceeds the supported arithmetic domain");
}
} // namespace San14::Render
