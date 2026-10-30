#pragma once
#include "../../../../Runtime/PreparedFileInput.h"
#include "../OceanOpticsSettings.h"
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <stdexcept>

namespace San14::Render::Interop {
inline OceanOpticsSettings readOceanOpticsSettings(const std::filesystem::path& path) {
    NativePreparedInputFile input(path,std::ios::binary);std::uint32_t magic{};OceanOpticsSettings result;
    auto read=[&](auto& value){input.read(reinterpret_cast<char*>(&value),sizeof(value));};
    read(magic);read(result.refractionIndex);read(result.refractionTransparency);
    for(auto& value:result.albedo)read(value);
    for(auto& value:result.reflectance)read(value);
    read(result.roughness);read(result.diffuseTransparency);
    if(!input||magic!=0x31504F4Fu||input.peek()!=std::char_traits<char>::eof())
        throw std::runtime_error("Ocean optics declaration OOP1 absent or malformed");
    return result;
}
} // namespace San14::Render::Interop
