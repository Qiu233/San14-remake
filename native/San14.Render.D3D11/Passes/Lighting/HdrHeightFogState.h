#pragma once
#include "../Environment/HeightFog.h"

namespace San14::Render {
// Each HDR module retains its own height-layer values. Disabled layers change
// their type while preserving the last values installed by an enabled setter.
struct HdrHeightFogState {
    source_current_height_fog::Frame height;
};
struct HdrHeightFogValues {
    std::array<std::int32_t,3> types{};
    std::array<source_camera::V4,3> colors{}, parameters{};
    std::array<float,60> fluctuations{};
};
struct HdrHeightFogWrite { unsigned kind{}, offset{}, sourceOffset{}, bytes{}; };
inline HdrHeightFogState prepareHdrHeightFog(HdrHeightFogState previous,
    const HeightFogSettings& settings, float ordinaryEyeZ, bool enabled) {
    if (!enabled) {
        for (auto& layer : previous.height.layers) layer.type = -1;
        return previous;
    }
    const auto current = source_current_height_fog::prepareCurrent(settings,ordinaryEyeZ);
    for (std::size_t i=0;i<previous.height.layers.size();++i) {
        auto& retained=previous.height.layers[i];
        if (current.layers[i].type>=0) retained=current.layers[i];
        else retained.type=-1;
    }
    return previous;
}
inline HdrHeightFogValues prepareHdrHeightFogValues(const HdrHeightFogState& state,
    float commonClock, bool temporaryDisable) {
    HdrHeightFogValues result;
    for (std::size_t layer=0;layer<state.height.layers.size();++layer) {
        const auto& source=state.height.layers[layer];
        result.types[layer]=temporaryDisable?-1:source.type;
        result.colors[layer]=source.color;result.parameters[layer]=source.parameter;
        for (std::size_t scalar=0;scalar<20;++scalar)
            result.fluctuations[layer*20+scalar]=scalar%5==2||scalar%5==4
                ?source_current_height_fog::phase(source.velocities[scalar],commonClock):source.values[scalar];
    }
    return result;
}
inline void writeHdrHeightFog(const std::vector<HdrHeightFogWrite>& writes,
    const HdrHeightFogValues& current,std::span<char> constant) {
    const void* values[]{current.types.data(),current.colors.data(),current.parameters.data(),current.fluctuations.data()};
    for (const auto& write:writes)
        std::memcpy(constant.data()+write.offset,static_cast<const char*>(values[write.kind])+write.sourceOffset,write.bytes);
}
} // namespace San14::Render
