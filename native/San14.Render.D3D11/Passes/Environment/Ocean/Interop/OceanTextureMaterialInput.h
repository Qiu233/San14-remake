#pragma once
#include "../OceanTextureMaterialSettings.h"
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <span>
#include <stdexcept>

namespace San14::Render::Interop {
inline constexpr std::size_t OceanTextureMaterialInputBytes=60;
// Own versioned named block, shared by initial source preparation and current
// frame publication. No original CPU image or address table enters this type.
inline OceanTextureMaterialSettings decodeOceanTextureMaterialSettings(std::span<const std::uint8_t> bytes) {
    if(bytes.size()!=OceanTextureMaterialInputBytes)
        throw std::runtime_error("Ocean texture material OUM2 extent differs");
    std::size_t at=0;
    auto read=[&]<class T>() {T value;std::memcpy(&value,bytes.data()+at,sizeof(value));at+=sizeof(value);return value;};
    if(read.operator()<std::uint32_t>()!=0x324D554Fu)
        throw std::runtime_error("Ocean texture material OUM2 version differs");
    auto scalar=[&](){return read.operator()<float>();};
    OceanTextureMaterialSettings result;
    result.uv={scalar(),scalar(),scalar(),scalar(),scalar(),scalar(),scalar()};
    result.wave={scalar(),scalar(),scalar(),scalar(),scalar(),scalar(),scalar()};
    return result;
}
inline OceanTextureMaterialSettings readOceanTextureMaterialSettings(const std::filesystem::path& path) {
    std::ifstream input(path,std::ios::binary);
    std::array<std::uint8_t,OceanTextureMaterialInputBytes> bytes;
    input.read(reinterpret_cast<char*>(bytes.data()),bytes.size());
    if(!input||input.peek()!=std::char_traits<char>::eof())
        throw std::runtime_error("Ocean texture material OUM2 absent or malformed");
    return decodeOceanTextureMaterialSettings(bytes);
}
} // namespace San14::Render::Interop
