#pragma once
#include "../OceanBubbleMaterialSettings.h"
#include <cstring>
#include <filesystem>
#include <fstream>
#include <span>
#include <stdexcept>

namespace San14::Render::Interop {
inline constexpr std::size_t OceanBubbleMaterialInputBytes = 284;
inline OceanBubbleMaterialSettings decodeOceanBubbleMaterialSettings(std::span<const std::uint8_t> bytes) {
    if (bytes.size()!=OceanBubbleMaterialInputBytes) throw std::runtime_error("Ocean bubble material OBM1 extent");
    std::uint32_t magic;
    std::memcpy(&magic,bytes.data(),4);
    if (magic!=0x314D424Fu) throw std::runtime_error("Ocean bubble material OBM1 version");
    std::size_t at=4;
    auto scalar = [&]() { float value; std::memcpy(&value,bytes.data()+at,4); at+=4; return value; };
    auto values = [&](auto& destination) { for (auto& value : destination) value=scalar(); };
    OceanBubbleMaterialSettings result;
    for (auto& layer : result.layers) {
        values(layer.color); values(layer.reflectance);
        layer.alphaThreshold=scalar(); layer.normalThreshold=scalar();
    }
    values(result.surfaceBand); values(result.innerBand); values(result.shoreParameter);
    values(result.shoalingAdjustment); result.depth=scalar(); values(result.scattering);
    values(result.scaleLengths); values(result.horizontalLengths); values(result.animationSpeeds);
    result.spawn={scalar(),scalar(),scalar(),scalar(),scalar()}; values(result.maskScales);
    result.generatorPeriod=scalar();
    return result;
}
inline OceanBubbleMaterialSettings readOceanBubbleMaterialSettings(const std::filesystem::path& path) {
    std::ifstream stream(path,std::ios::binary);
    std::array<std::uint8_t,OceanBubbleMaterialInputBytes> bytes;
    stream.read(reinterpret_cast<char*>(bytes.data()),bytes.size());
    if (!stream || stream.peek()!=std::char_traits<char>::eof()) throw std::runtime_error("Ocean bubble material OBM1 absent");
    return decodeOceanBubbleMaterialSettings(bytes);
}
} // namespace San14::Render::Interop
