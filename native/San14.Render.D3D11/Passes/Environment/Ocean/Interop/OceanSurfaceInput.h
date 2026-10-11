#pragma once
#include "../OceanSurfaceSettings.h"
#include <filesystem>
#include <fstream>

namespace San14::Render::Interop {
inline OceanSurfaceSettings readOceanSurfaceSettings(const std::filesystem::path& path) {
    std::ifstream input(path,std::ios::binary);std::uint32_t magic{};OceanSurfaceSettings result;
    auto read=[&](auto& value){input.read(reinterpret_cast<char*>(&value),sizeof(value));};
    read(magic);read(result.waterHeight);read(result.cutoffDistance);read(result.directionX);
    read(result.directionZ);read(result.displacement);read(result.normalSpeed);read(result.bubbleMode);
    read(result.bubblePeriods[0]);read(result.bubblePeriods[1]);
    if(!input||magic!=0x3153534Fu||input.peek()!=std::char_traits<char>::eof())
        throw std::runtime_error("Ocean surface declaration OSS1 absent or malformed");
    validateOceanSurfaceSettings(result);return result;
}
} // namespace San14::Render::Interop
