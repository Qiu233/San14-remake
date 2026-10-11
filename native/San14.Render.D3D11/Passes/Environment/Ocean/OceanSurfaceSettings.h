#pragma once
#include <array>
#include <cmath>
#include <cstdint>
#include <stdexcept>

namespace San14::Render {
// Selected/current material values. Consumer-specific clamps are applied only
// when publishing the effective surface and animation state.
struct OceanSurfaceSettings {
    float waterHeight{},cutoffDistance{},directionX{},directionZ{},displacement{},normalSpeed{};
    std::uint32_t bubbleMode{};
    std::array<float,2> bubblePeriods{};
};
inline void validateOceanSurfaceSettings(const OceanSurfaceSettings& settings) {
    const float values[]{settings.waterHeight,settings.cutoffDistance,settings.directionX,
        settings.directionZ,settings.displacement,settings.normalSpeed,
        settings.bubblePeriods[0],settings.bubblePeriods[1]};
    for(float value:values)if(!std::isfinite(value))
        throw std::runtime_error("Ocean surface settings must be finite");
    const float lengthSquared=settings.directionX*settings.directionX+settings.directionZ*settings.directionZ;
    if(!std::isfinite(lengthSquared)||lengthSquared<=0)
        throw std::runtime_error("Ocean texture-wave direction must have a finite nonzero length");
}
inline std::array<float,2> oceanTextureWaveDirection(const OceanSurfaceSettings& settings) {
    float x=settings.directionX,z=settings.directionZ;
    const float lengthSquared=x*x+z*z;
    if(std::abs(lengthSquared-1.0f)>0.01f) {
        const float inverseLength=1.0f/std::sqrt(lengthSquared);
        x*=inverseLength;z*=inverseLength;
    }
    return {x,z};
}
inline float oceanNormalAnimationSpeed(const OceanSurfaceSettings& settings) {
    return settings.normalSpeed<=1e-6f?1e-6f:settings.normalSpeed;
}
} // namespace San14::Render
