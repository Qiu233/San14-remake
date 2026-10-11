#pragma once
#include "../OceanGrid.h"
#include <filesystem>
#include <fstream>
#include <stdexcept>

namespace San14::Render::Interop {
inline OceanGridSettings readOceanGridSettings(const std::filesystem::path& path) {
    std::ifstream input(path,std::ios::binary);std::uint32_t magic{};OceanGridSettings result;
    auto read=[&](auto& value) { input.read(reinterpret_cast<char*>(&value),sizeof(value)); };
    read(magic);read(result.columns);read(result.rows);read(result.sizeX);read(result.sizeZ);
    read(result.cycle);read(result.declaredLodCount);read(result.maximumDistance);
    if(!input||magic!=0x3152474Fu||input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Ocean grid declaration OGR1 absent");
    validateOceanGridSettings(result);
    return result;
}
// Only unresolved current surface inputs are retained from this legacy media.
// Construction dimensions, capacity, thresholds and topology have no authority here.
inline OceanSurfaceState readOceanSurfaceState(const std::filesystem::path& path) {
    std::ifstream input(path,std::ios::binary);std::uint32_t magic{};std::array<float,8> values{};
    input.read(reinterpret_cast<char*>(&magic),sizeof(magic));
    input.read(reinterpret_cast<char*>(values.data()),sizeof(values));
    if(!input||magic!=0x31474357u)throw std::runtime_error("Ocean current surface state WCG1 absent");
    return {values[0],values[3],values[5],values[6],values[7]};
}
} // namespace San14::Render::Interop
