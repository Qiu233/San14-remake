#pragma once
#include "../../../../Runtime/PreparedFileInput.h"
#include "../OceanDrawMaterialState.h"
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <span>
#include <stdexcept>
#include <vector>

namespace San14::Render::Interop {
struct OceanMaterialFieldView { const void* data; std::size_t bytes; };
inline OceanMaterialFieldView oceanMaterialField(const OceanDrawMaterialState& state, std::uint32_t id) {
#define OCEAN_FIELD(number, member) case number: return {&state.member, sizeof(state.member)}
    switch (id) {
        OCEAN_FIELD(0,eye); OCEAN_FIELD(1,worldToProjection); OCEAN_FIELD(2,clipPlanes);
        OCEAN_FIELD(3,normalUv); OCEAN_FIELD(4,normalNoise); OCEAN_FIELD(5,heightUv);
        OCEAN_FIELD(6,waterLevel); OCEAN_FIELD(7,waveDirection); OCEAN_FIELD(8,waveParameter);
        OCEAN_FIELD(9,waveParameter2); OCEAN_FIELD(10,waveFigure); OCEAN_FIELD(11,waveNoise);
        OCEAN_FIELD(12,shoreBubbleParameter); OCEAN_FIELD(13,bubbleShoalingAdjustment);
        OCEAN_FIELD(14,cameraDirectionXZ); OCEAN_FIELD(15,shoalingScale); OCEAN_FIELD(16,worldView);
        OCEAN_FIELD(17,refractionWorldToTexture); OCEAN_FIELD(18,refractionIndex);
        OCEAN_FIELD(19,refractionTransparency); OCEAN_FIELD(20,diffuseTransparency);
        OCEAN_FIELD(21,refractionParameter); OCEAN_FIELD(22,depthToZ);
        OCEAN_FIELD(23,refractionCameraPosition); OCEAN_FIELD(24,refractionCameraDirection);
        OCEAN_FIELD(25,bubbleColorScale); OCEAN_FIELD(26,surfaceBubbleColor);
        OCEAN_FIELD(27,surfaceBubbleParameter); OCEAN_FIELD(28,innerBubbleColor);
        OCEAN_FIELD(29,innerBubbleParameter); OCEAN_FIELD(30,shoreBubbleColor);
        OCEAN_FIELD(31,scattering); OCEAN_FIELD(32,spawnPoints); OCEAN_FIELD(33,bubbleDepth);
        OCEAN_FIELD(34,bubbleFrames); OCEAN_FIELD(35,surfaceBubbleScale);
        OCEAN_FIELD(36,innerBubbleScale); OCEAN_FIELD(37,shoreBubbleScale);
        OCEAN_FIELD(38,bubbleOffset); OCEAN_FIELD(39,albedo); OCEAN_FIELD(40,reflectance);
        default: throw std::runtime_error("Unknown Ocean material semantic");
    }
#undef OCEAN_FIELD
}
struct OceanMaterialWrite { std::uint32_t field, destination, source, bytes; };
struct OceanMaterialStageLayout {
    std::uint32_t slot{}, byteSize{};
    std::vector<OceanMaterialWrite> writes;
    void pack(const OceanDrawMaterialState& state, std::span<char> destination) const {
        for (const auto& write : writes) {
            const auto field = oceanMaterialField(state,write.field);
            std::memcpy(destination.data()+write.destination,
                static_cast<const char*>(field.data)+write.source,write.bytes);
        }
    }
};
struct OceanMaterialLayout { OceanMaterialStageLayout vertex, pixel; };
inline OceanMaterialLayout readOceanMaterialLayout(const std::filesystem::path& path) {
    NativePreparedInputFile stream(path,std::ios::binary);
    auto word = [&]() {
        std::uint32_t value{};
        stream.read(reinterpret_cast<char*>(&value),sizeof(value));
        if (!stream) throw std::runtime_error("Incomplete Ocean material layout");
        return value;
    };
    if (word()!=0x314C4D4Fu || word()!=2) throw std::runtime_error("Ocean material layout OML1 absent");
    OceanMaterialLayout result;
    unsigned seen = 0;
    const OceanDrawMaterialState shape;
    for (unsigned i=0;i<2;++i) {
        const auto stage = word();
        if (stage>1 || (seen&(1u<<stage))) throw std::runtime_error("Ocean material stage layout");
        seen |= 1u<<stage;
        auto& layout = stage ? result.vertex : result.pixel;
        layout.slot = word(); layout.byteSize = word(); const auto count = word();
        if (layout.slot>=14 || !layout.byteSize || layout.byteSize%16 || layout.byteSize>65536 || count>512)
            throw std::runtime_error("Ocean material constant-buffer layout");
        layout.writes.reserve(count);
        for (std::uint32_t row=0;row<count;++row) {
            OceanMaterialWrite write{word(),word(),word(),word()};
            const auto field = oceanMaterialField(shape,write.field);
            if (!write.bytes || write.source>field.bytes || write.bytes>field.bytes-write.source ||
                write.destination>layout.byteSize || write.bytes>layout.byteSize-write.destination)
                throw std::runtime_error("Ocean material write range");
            layout.writes.push_back(write);
        }
    }
    if (stream.peek()!=std::char_traits<char>::eof()) throw std::runtime_error("Trailing Ocean material layout");
    return result;
}
} // namespace San14::Render::Interop
