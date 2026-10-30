#pragma once
#include "../../Camera/CameraTransform.h"
#include <array>
#include <cstdint>
#include <optional>

namespace source_current_season_post {
struct LightIdentity {uint32_t object{},groupMask{};};
struct ExposureTransition {
    std::optional<uint32_t> groupMask;
    std::optional<float> fade,luminance;
};
// One accepted season publication. Optional values retain unavailable imported
// knowledge until the operation that actually requires it; they are not defaults.
struct CurrentOptics {
    uint32_t apertureChanged{},blades{},diaphragm{},width{},height{};
    std::array<float,11> values{};
};
struct Settings {
    std::optional<CurrentOptics> currentOptics;
    source_camera::CameraTransform camera;
    std::array<LightIdentity,5> lights{};
    std::array<float,3> selectedSunDirection{};
    std::array<float,2> adaptation{},exposureRange{};
    std::array<std::optional<float>,2> heat{};
    std::optional<float> heatCoefficient,spriteThreshold;
    float userExposure{};
    std::optional<uint32_t> depthOfFieldMode;
    std::optional<bool> automaticExposure;
    ExposureTransition transition;
};
} // namespace source_current_season_post
