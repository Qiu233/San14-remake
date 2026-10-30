#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include <array>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <span>
#include <stdexcept>

namespace San14::Render {
struct HeightFogWaveSettings {
    float amplitude{}, frequencyX{}, velocityX{}, frequencyY{}, velocityY{};
};
struct HeightFogLayerSettings {
    uint32_t mode{}, distanceFadeEnabled{};
    float distanceStart{}, distanceEnd{}, heightStart{}, heightEnd{};
    std::array<float,4> color{};
    float velocity{};
    std::array<HeightFogWaveSettings,4> waves{};
};
struct HeightFogSettings {
    std::array<HeightFogLayerSettings,3> layers{};
    std::array<float,4> mask{};
};
namespace Interop {
inline constexpr size_t HeightFogInputBytes=392;
inline HeightFogSettings decodeHeightFogSettings(std::span<const uint8_t> bytes) {
    if(bytes.size()!=HeightFogInputBytes)throw std::runtime_error("Height Fog input length");
    size_t at=0;
    auto word=[&](){uint32_t value;std::memcpy(&value,bytes.data()+at,4);at+=4;return value;};
    auto scalar=[&](){uint32_t bits=word();float value;std::memcpy(&value,&bits,4);return value;};
    if(word()!=0x314f4648)throw std::runtime_error("Height Fog input signature");
    HeightFogSettings result;
    for(auto& layer:result.layers) {
        layer.mode=word();layer.distanceFadeEnabled=word();
        layer.distanceStart=scalar();layer.distanceEnd=scalar();
        layer.heightStart=scalar();layer.heightEnd=scalar();
        for(auto& value:layer.color)value=scalar();
        layer.velocity=scalar();
        for(auto& wave:layer.waves)
            wave={scalar(),scalar(),scalar(),scalar(),scalar()};
    }
    for(auto& value:result.mask)value=scalar();
    return result;
}
inline HeightFogSettings readHeightFogSettings(const std::filesystem::path& path) {
    NativePreparedInputFile input(path,std::ios::binary);std::array<uint8_t,HeightFogInputBytes> bytes{};
    input.read(reinterpret_cast<char*>(bytes.data()),bytes.size());
    if(!input||input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Initial Height Fog input length");
    return decodeHeightFogSettings(bytes);
}
} // namespace Interop
} // namespace San14::Render
