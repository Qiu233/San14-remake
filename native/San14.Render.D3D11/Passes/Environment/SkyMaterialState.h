#pragma once
#include "../../Camera/CameraTransform.h"
#include <array>
#include <cstdint>
#include <stdexcept>

namespace San14::Render {
struct SkyDrawPolicy {
    bool suppressStars{};
};
struct SkyMaterialState {
    source_camera::Matrix cameraToLocal{};
    float projectionFarZ{};
    source_camera::Matrix localToGlobal{};
    source_camera::V4 fog{};
    std::array<source_camera::V4,4> scattering{};
    source_camera::V4 viewPosition{},skyInfo0{},skyInfo1{};
    source_camera::V4 sunRgb{},sunDirection{},moonRgb{},moonDirection{};
    source_camera::Matrix moonFace{};
    source_camera::V4 sunScreenPosition{},moonScreenPosition{},altitudeInfo{},stableDimensions{},clampValue{};
};
inline SkyDrawPolicy skyDrawPolicy(std::uint32_t preparedTransitionOrder) {
    return {(preparedTransitionOrder&0x80000000u)!=0};
}
inline void requireSupportedMainSkyPolicy(const SkyDrawPolicy& policy) {
    if(!policy.suppressStars)throw std::runtime_error("Active Main Star drawing needs its actual producer");
}
} // namespace San14::Render
