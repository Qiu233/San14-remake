#pragma once
#include "../../Camera/CameraTransform.h"
#include <array>
#include <cstdint>
#include <optional>

namespace source_current_season_shadow {
enum class DirectionChannel : uint32_t {Primary,FirstAlternate,SecondAlternate,ThirdAlternate};
enum class CompactMode : uint32_t {Single=0,TwoCascades=9,ThreeCascades=10,FourCascades=11};
// Missing imported settings remain unknown until the selected shadow route
// needs them. A disabled selection does not require cascade configuration.
struct Settings {
    std::optional<int32_t> selectedLightGroup;
    std::optional<DirectionChannel> directionChannel;
    std::optional<float> planeScale;
    std::optional<float> maximumDistance, nearDistance, farDistanceOverride;
    std::optional<uint32_t> sampleLoopCount, qualityLevel, rectangleOverride;
    std::optional<CompactMode> compactMode;
    std::array<std::optional<float>,3> cascadeSplits;
    float halfFovTangent{};
};

// A light publishes four direction/color pairs. Direction channel selection
// keeps their authored order; unsupported channel numbers use the first pair.
struct LightGeometry {
    std::array<source_camera::V4,4> directions{};
    float receiverAmbientAlpha{};
};

struct ReceiverStyle {
    int32_t patternCameraBranch{};
    float patternScale{}, depthFade{};
};
} // namespace source_current_season_shadow
