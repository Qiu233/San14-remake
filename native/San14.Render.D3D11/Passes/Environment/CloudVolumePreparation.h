#pragma once
#include "CloudVolumeMaterial.h"
#include "CloudWeather.h"

namespace San14::Render {
inline CloudVolumeMaterial prepareCloudVolumeMaterial(
    const source_current_cloud_weather::Frame& weather, const CloudAnimationSnapshot& phases) {
    using namespace source_current_cloud_weather;
    CloudVolumeMaterial result;
    auto layer = [&]<std::size_t N>(const NoiseLayer<N>& input, AnimationChannel first,
        std::array<float, N>& offsets, std::array<float, N>& scales) {
        for (std::size_t index = 0; index < N; ++index) {
            scales[index] = D(1, input.period[index]);
            offsets[index] = M(A(phases[std::size_t(first) + index], input.position[index]), scales[index]);
        }
    };
    layer(weather.base, AnimationChannel::BaseX, result.baseOffset, result.baseScale);
    layer(weather.detail, AnimationChannel::DetailX, result.detailOffset, result.detailScale);
    layer(weather.distort, AnimationChannel::DistortX, result.distortOffset, result.distortScale);
    layer(weather.cirrus, AnimationChannel::CirrusX, result.cirrusOffset, result.cirrusScale);
    layer(weather.cirrusDistort, AnimationChannel::CirrusDistortX, result.cirrusDistortOffset, result.cirrusDistortScale);
    const auto samples = field(weather, ShaderField::RaySampleNum, false, 0);
    result.raySampleSlopeAndMaximum = {samples[0], samples[1]};
    result.layerGeometry = field(weather, ShaderField::CloudParams0, false, 0);
    result.densityParameters = field(weather, ShaderField::CloudParams1, false, 0);
    result.distortionScale = field(weather, ShaderField::DistortScale, false, 0);
    const auto erosion = field(weather, ShaderField::ErodeScale, false, 0);
    result.erosionScale = {erosion[0], erosion[1]};
    result.cirrusParameters = field(weather, ShaderField::CirrusParams, false, 0);
    const auto cirrusDistortion = field(weather, ShaderField::CirrusDistortScale, false, 0);
    result.cirrusDistortionScale = {cirrusDistortion[0], cirrusDistortion[1]};
    result.cirrusAltitude = weather.cirrusAltitude;
    return result;
}
} // namespace San14::Render
