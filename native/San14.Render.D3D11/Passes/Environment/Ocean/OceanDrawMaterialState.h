#pragma once
#include <array>

namespace San14::Render {
// Named values are independent of the selected shader's constant-buffer order.
// Near composition supplies the shared values; far composition applies only
// the source's explicit overrides.
struct OceanDrawMaterialState {
    using V4 = std::array<float,4>;
    V4 eye{};
    std::array<float,16> worldToProjection{};
    std::array<V4,6> clipPlanes{};
    V4 normalUv{};
    std::array<float,3> normalNoise{};
    V4 heightUv{};
    float waterLevel{};
    V4 waveDirection{}, waveParameter{}, waveParameter2{}, waveFigure{}, waveNoise{};
    V4 shoreBubbleParameter{}, bubbleShoalingAdjustment{}, cameraDirectionXZ{};
    float shoalingScale{};
    std::array<float,12> worldView{}, refractionWorldToTexture{};
    float refractionIndex{}, refractionTransparency{}, diffuseTransparency{};
    V4 refractionParameter{};
    std::array<float,2> depthToZ{};
    V4 refractionCameraPosition{}, refractionCameraDirection{}, bubbleColorScale{1,1,1,1};
    std::array<V4,3> surfaceBubbleColor{}, innerBubbleColor{}, shoreBubbleColor{};
    V4 surfaceBubbleParameter{}, innerBubbleParameter{}, scattering{};
    std::array<V4,16> spawnPoints{};
    V4 bubbleDepth{}, bubbleFrames{}, surfaceBubbleScale{}, innerBubbleScale{}, shoreBubbleScale{};
    std::array<float,2> bubbleOffset{};
    V4 albedo{}, reflectance{};
};
} // namespace San14::Render
