#pragma once
#include "OceanBubbleRuntime.h"

namespace San14::Render {
struct OceanBubbleLayerSettings {
    std::array<float,4> color{}, reflectance{};
    float alphaThreshold{}, normalThreshold{};
};
struct OceanBubbleMaterialSettings {
    std::array<OceanBubbleLayerSettings,3> layers;
    std::array<float,4> surfaceBand{}, innerBand{}, shoreParameter{}, shoalingAdjustment{};
    float depth{};
    std::array<float,4> scattering{};
    std::array<float,6> scaleLengths{};
    std::array<float,2> horizontalLengths{};
    std::array<float,3> animationSpeeds{};
    OceanBubbleSpawnSettings spawn;
    std::array<float,2> maskScales{};
    float generatorPeriod{};
};
} // namespace San14::Render
