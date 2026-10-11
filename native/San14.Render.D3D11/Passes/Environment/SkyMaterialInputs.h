#pragma once
#include "SkyMaterialState.h"
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <span>
#include <stdexcept>
#include <vector>

namespace San14::Render::Interop {
struct SkySamplingPresence { bool earth{},moon{},milky{}; };
inline SkySamplingPresence decodeSkySamplingPresence(std::span<const std::uint8_t> bytes) {
    if(bytes.size()!=8)throw std::runtime_error("Main Sky sampling presence SMB1 extent");
    std::array<std::uint32_t,2> words{};
    std::memcpy(words.data(),bytes.data(),bytes.size());
    if(words[0]!=0x31424D53u||(words[1]&~7u))
        throw std::runtime_error("Main Sky sampling presence SMB1 format");
    return {(words[1]&1u)!=0,(words[1]&2u)!=0,(words[1]&4u)!=0};
}
inline SkySamplingPresence readSkySamplingPresence(const std::filesystem::path& path) {
    std::ifstream stream(path,std::ios::binary);std::array<std::uint8_t,8> bytes{};
    stream.read(reinterpret_cast<char*>(bytes.data()),bytes.size());
    if(!stream||stream.peek()!=std::char_traits<char>::eof())
        throw std::runtime_error("Main Sky sampling presence absent or malformed");
    return decodeSkySamplingPresence(bytes);
}
struct SkyMaterialFieldView { const void* data; std::size_t bytes; };
inline SkyMaterialFieldView skyMaterialField(const SkyMaterialState& state,std::uint32_t id) {
#define SKY_FIELD(number,member) case number: return {&state.member,sizeof(state.member)}
    switch(id) {
        SKY_FIELD(0,cameraToLocal); SKY_FIELD(1,projectionFarZ); SKY_FIELD(2,localToGlobal);
        SKY_FIELD(3,fog); SKY_FIELD(4,scattering); SKY_FIELD(5,viewPosition);
        SKY_FIELD(6,skyInfo0); SKY_FIELD(7,skyInfo1); SKY_FIELD(8,sunRgb);
        SKY_FIELD(9,sunDirection); SKY_FIELD(10,moonRgb); SKY_FIELD(11,moonDirection);
        SKY_FIELD(12,moonFace); SKY_FIELD(13,sunScreenPosition); SKY_FIELD(14,moonScreenPosition);
        SKY_FIELD(15,altitudeInfo); SKY_FIELD(16,stableDimensions); SKY_FIELD(17,clampValue);
        default: throw std::runtime_error("Unknown Main Sky material semantic");
    }
#undef SKY_FIELD
}
inline SkyMaterialState decodeSkyInitialState(std::span<const std::uint8_t> bytes) {
    if(bytes.size()!=472)throw std::runtime_error("Main Sky initial state SMI1 extent");
    std::uint32_t magic;std::memcpy(&magic,bytes.data(),4);
    if(magic!=0x31494D53u)throw std::runtime_error("Main Sky initial state SMI1 version");
    SkyMaterialState result;std::size_t at=4;
    for(std::uint32_t id=0;id<18;++id) {
        const auto field=skyMaterialField(result,id);
        std::memcpy(const_cast<void*>(field.data),bytes.data()+at,field.bytes);at+=field.bytes;
    }
    return result;
}
inline SkyMaterialState readSkyInitialState(const std::filesystem::path& path) {
    std::ifstream stream(path,std::ios::binary);std::array<std::uint8_t,472> bytes;
    stream.read(reinterpret_cast<char*>(bytes.data()),bytes.size());
    if(!stream||stream.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Main Sky initial state absent or malformed");
    return decodeSkyInitialState(bytes);
}
struct SkyMaterialWrite { std::uint32_t field,destination,source,bytes; };
struct SkyMaterialStageLayout {
    std::uint32_t slot{},byteSize{};
    std::vector<SkyMaterialWrite> writes;
    void pack(const SkyMaterialState& state,std::span<char> bytes) const {
        for(const auto& write:writes) {
            const auto field=skyMaterialField(state,write.field);
            std::memcpy(bytes.data()+write.destination,static_cast<const char*>(field.data)+write.source,write.bytes);
        }
    }
};
struct SkyMaterialLayout { SkyMaterialStageLayout vertex,pixel; };
inline SkyMaterialLayout readSkyMaterialLayout(const std::filesystem::path& path) {
    std::ifstream stream(path,std::ios::binary);
    auto word=[&]() {std::uint32_t value{};stream.read(reinterpret_cast<char*>(&value),4);
        if(!stream)throw std::runtime_error("Incomplete Main Sky layout");return value;};
    if(word()!=0x314C4D53u||word()!=2)throw std::runtime_error("Main Sky material layout SML1 version");
    SkyMaterialLayout result;unsigned seen=0;const SkyMaterialState shape;
    for(unsigned stageIndex=0;stageIndex<2;++stageIndex) {
        const auto stage=word();
        if(stage>1||(seen&(1u<<stage)))throw std::runtime_error("Main Sky material stage layout");
        seen|=1u<<stage;
        auto& layout=stage?result.vertex:result.pixel;
        layout.slot=word();layout.byteSize=word();const auto count=word();
        if(layout.slot>=14||!layout.byteSize||layout.byteSize%16||layout.byteSize>65536||count>256)
            throw std::runtime_error("Main Sky constant-buffer layout");
        layout.writes.reserve(count);
        for(std::uint32_t i=0;i<count;++i) {
            SkyMaterialWrite write{word(),word(),word(),word()};
            const auto field=skyMaterialField(shape,write.field);
            if(!write.bytes||write.source>field.bytes||write.bytes>field.bytes-write.source||
                write.destination>layout.byteSize||write.bytes>layout.byteSize-write.destination)
                throw std::runtime_error("Main Sky semantic write range");
            layout.writes.push_back(write);
        }
    }
    if(stream.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Trailing Main Sky material layout");
    return result;
}
} // namespace San14::Render::Interop
