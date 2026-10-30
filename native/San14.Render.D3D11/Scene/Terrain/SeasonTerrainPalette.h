#pragma once
#include "../../Runtime/PreparedFileInput.h"
// CE C460 changes LandscapePalette rows10/4. The actual first-name lookup
// indexes below belong to one loaded Landscape, not a synthetic texture bank.
#include <array>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <vector>

namespace source_current_season_terrain {
struct PaletteBank {std::array<std::vector<uint32_t>,4> seasons;};
inline PaletteBank readPaletteBank(const std::filesystem::path& directory) {
    NativePreparedInputFile input(directory/"season-palette.bin",std::ios::binary);
    if(!input)throw std::runtime_error("Actual seasonal Landscape palette absent");
    auto word=[&](){uint32_t v{};input.read(reinterpret_cast<char*>(&v),4);
        if(!input)throw std::runtime_error("Actual seasonal Landscape palette truncated");return v;};
    uint32_t seasons=word(),count=word();
    if(seasons!=4||count>255)throw std::runtime_error("Actual seasonal Landscape palette extent");
    PaletteBank out;std::array<bool,4> seen{};
    for(uint32_t i=0;i<4;++i){uint32_t season=word();if(season>=4)throw std::runtime_error("Actual seasonal palette index");
        if(seen[season])throw std::runtime_error("Actual seasonal palette duplicated season");seen[season]=true;
        auto& rows=out.seasons[season];rows.resize(count);for(auto& row:rows)row=word();}
    if(input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Actual seasonal palette trailing bytes");
    return out;
}
// Layout belongs to the loaded camera constants. Each pixel program may have
// its own index column; absence is allowed and no offsets are borrowed.
template<class TerrainGpuOwner>
inline void prepareNativeCurrent(const PaletteBank& bank,const TerrainGpuOwner& terrain) {
    for(const auto& constant:terrain.constants)if(!constant.vertex&&constant.detailTextureIndex>=0)
        for(const auto& rows:bank.seasons)if(rows.size()>constant.detailTextureCapacity)
            throw std::runtime_error("Seasonal terrain palette exceeds imported index column");
}
// Patch before camera upload. Only the two existing seasonal rule rows change;
// all other material, camera, palette and padding bytes remain retained.
template<class TerrainGpuOwner>
inline void patchNativeCurrent(const PaletteBank& bank,uint32_t season,TerrainGpuOwner& terrain) {
    if(season>=4)throw std::runtime_error("Current seasonal terrain palette index");
    const auto& rows=bank.seasons[season];
    for(auto& constant:terrain.constants)if(!constant.vertex&&constant.detailTextureIndex>=0)
        for(unsigned index:{10u,4u})if(index<rows.size())
            std::memcpy(constant.bytes.data()+constant.detailTextureIndex+index*16,&rows[index],4);
}
} // namespace source_current_season_terrain
