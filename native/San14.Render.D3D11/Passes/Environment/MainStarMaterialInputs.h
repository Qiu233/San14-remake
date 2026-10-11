#pragma once
#include "MainStarMaterialState.h"
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <span>
#include <stdexcept>
#include <vector>

namespace San14::Render::Interop {
struct MainStarFieldView { const void* data; std::size_t bytes; };
inline MainStarFieldView mainStarField(const MainStarMaterialState& state,std::uint32_t id) {
    switch(id) {
        case 0:return {&state.eyeToClip,sizeof(state.eyeToClip)};
        case 1:return {&state.phase,sizeof(state.phase)};
        case 2:return {state.twinkleTable.data(),sizeof(state.twinkleTable)};
        case 3:return {&state.starInfo,sizeof(state.starInfo)};
        case 4:return {&state.pointScale,sizeof(state.pointScale)};
        case 5:return {&state.showerInfo0,sizeof(state.showerInfo0)};
        case 6:return {&state.showerInfo1,sizeof(state.showerInfo1)};
        default:throw std::runtime_error("Unknown Main Star material semantic");
    }
}
struct MainStarMaterialWrite { std::uint32_t field,destination,source,bytes; };
struct MainStarStageLayout {
    std::uint32_t slot{},byteSize{};
    std::vector<MainStarMaterialWrite> writes;
    void pack(const MainStarMaterialState& state,std::span<char> destination) const {
        for(const auto& write:writes) {
            const auto field=mainStarField(state,write.field);
            std::memcpy(destination.data()+write.destination,
                static_cast<const char*>(field.data)+write.source,write.bytes);
        }
    }
};
struct MainStarPassLayout {
    MainStarStageLayout vertex,geometry,pixel;
};
using MainStarMaterialLayout=std::array<MainStarPassLayout,2>;

inline MainStarMaterialLayout readMainStarMaterialLayout(const std::filesystem::path& path) {
    std::ifstream stream(path,std::ios::binary);
    auto word=[&]() {
        std::uint32_t value{};stream.read(reinterpret_cast<char*>(&value),sizeof(value));
        if(!stream)throw std::runtime_error("Incomplete Main Star material layout");
        return value;
    };
    if(word()!=0x314C5453u||word()!=2)
        throw std::runtime_error("Main Star material STL1 version");
    MainStarMaterialLayout result;unsigned seenPasses=0;
    const MainStarMaterialState shape;
    for(unsigned passIndex=0;passIndex<2;++passIndex) {
        const auto pass=word();
        if(pass>1||(seenPasses&(1u<<pass))||word()!=3)
            throw std::runtime_error("Main Star material pass layout");
        seenPasses|=1u<<pass;unsigned seenStages=0;
        for(unsigned stageIndex=0;stageIndex<3;++stageIndex) {
            const auto stage=word();
            if(stage>2||(seenStages&(1u<<stage)))
                throw std::runtime_error("Main Star material shader stage");
            seenStages|=1u<<stage;
            auto& layout=stage==1?result[pass].vertex:stage==2?result[pass].geometry:result[pass].pixel;
            layout.slot=word();layout.byteSize=word();const auto count=word();
            if(layout.slot>=14||!layout.byteSize||layout.byteSize%16||layout.byteSize>65536||count>256)
                throw std::runtime_error("Main Star constant-buffer layout");
            layout.writes.reserve(count);
            for(std::uint32_t i=0;i<count;++i) {
                MainStarMaterialWrite write{word(),word(),word(),word()};
                const auto field=mainStarField(shape,write.field);
                if(!write.bytes||write.source>field.bytes||write.bytes>field.bytes-write.source||
                    write.destination>layout.byteSize||write.bytes>layout.byteSize-write.destination)
                    throw std::runtime_error("Main Star semantic write range");
                layout.writes.push_back(write);
            }
        }
    }
    if(stream.peek()!=std::char_traits<char>::eof())
        throw std::runtime_error("Trailing Main Star material layout");
    return result;
}
inline std::array<source_camera::V4,16> readMainStarTwinkleTable(const std::filesystem::path& path) {
    std::array<source_camera::V4,16> table{};
    std::ifstream stream(path,std::ios::binary);
    stream.read(reinterpret_cast<char*>(table.data()),sizeof(table));
    if(!stream||stream.peek()!=std::char_traits<char>::eof())
        throw std::runtime_error("Main Star twinkle table extent");
    return table;
}
} // namespace San14::Render::Interop
