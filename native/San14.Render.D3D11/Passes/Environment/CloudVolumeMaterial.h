#pragma once
#include "CloudWeatherInputs.h"

namespace San14::Render {
// One accepted weather and animation snapshot supplies the common volume
// values. Each draw adds its own camera, atmosphere radius and lighting.
struct CloudVolumeMaterial {
    std::array<float, 3> baseOffset{}, baseScale{}, detailOffset{}, detailScale{};
    std::array<float, 2> distortOffset{}, distortScale{}, raySampleSlopeAndMaximum{};
    std::array<float, 4> layerGeometry{}, densityParameters{}, distortionScale{};
    std::array<float, 2> erosionScale{};
    float cirrusAltitude{};
};

using CloudAnimationSnapshot = std::array<float, 16>;
inline std::optional<float> cloudVolumeOffset(const CloudVolumeMaterial& volume,
    source_current_cloud_weather::AnimationChannel channel) {
    using source_current_cloud_weather::AnimationChannel;
    switch (channel) {
    case AnimationChannel::BaseX: return volume.baseOffset[0];
    case AnimationChannel::BaseY: return volume.baseOffset[1];
    case AnimationChannel::BaseZ: return volume.baseOffset[2];
    case AnimationChannel::DetailX: return volume.detailOffset[0];
    case AnimationChannel::DetailY: return volume.detailOffset[1];
    case AnimationChannel::DetailZ: return volume.detailOffset[2];
    case AnimationChannel::DistortX: return volume.distortOffset[0];
    case AnimationChannel::DistortY: return volume.distortOffset[1];
    default: return std::nullopt;
    }
}
} // namespace San14::Render
