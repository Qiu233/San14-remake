#pragma once
#include "../../../../Runtime/PreparedFileInput.h"
#include "../OceanWaveSettings.h"
#include <cstdint>
#include <filesystem>
#include <fstream>

namespace San14::Render::Interop {
inline OceanWaveSettings readOceanWaveSettings(const std::filesystem::path& path) {
    NativePreparedInputFile input(path,std::ios::binary);std::uint32_t magic{};OceanWaveSettings result;
    auto read=[&](auto& value){input.read(reinterpret_cast<char*>(&value),sizeof(value));};
    read(magic);read(result.windX);read(result.windZ);read(result.amplitude);
    read(result.choppyFactor);read(result.smallWaveSuppressionLength);read(result.gravity);
    if(!input||magic!=0x3153574Fu||input.peek()!=std::char_traits<char>::eof())
        throw std::runtime_error("Ocean wave declaration OWS1 absent or malformed");
    validateOceanWaveSettings(result);return result;
}
} // namespace San14::Render::Interop
