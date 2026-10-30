#pragma once
#include "../../../../Runtime/PreparedFileInput.h"
#include "../OceanGrid.h"
#include <filesystem>
#include <fstream>
#include <stdexcept>

namespace San14::Render::Interop {
inline OceanGridSettings readOceanGridSettings(const std::filesystem::path& path) {
    NativePreparedInputFile input(path,std::ios::binary);std::uint32_t magic{};OceanGridSettings result;
    auto read=[&](auto& value) { input.read(reinterpret_cast<char*>(&value),sizeof(value)); };
    read(magic);read(result.columns);read(result.rows);read(result.sizeX);read(result.sizeZ);
    read(result.cycle);read(result.declaredLodCount);read(result.maximumDistance);
    if(!input||magic!=0x3152474Fu||input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Ocean grid declaration OGR1 absent");
    validateOceanGridSettings(result);
    return result;
}
} // namespace San14::Render::Interop
