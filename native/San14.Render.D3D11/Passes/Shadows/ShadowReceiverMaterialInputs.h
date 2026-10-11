#pragma once
#include "ShadowReceiverMaterial.h"
#include <algorithm>
#include <cmath>
#include <filesystem>
#include <fstream>
#include <vector>

namespace San14::Render {
struct ShadowReceiverMaterialWrite {
    std::uint32_t field{}, destination{}, source{}, bytes{};
};
struct ShadowReceiverStageLayout {
    std::int32_t slot{-1};
    std::uint32_t bytes{};
    std::vector<ShadowReceiverMaterialWrite> writes;
    void pack(const ShadowReceiverMaterial& material, std::span<char> output) const {
        if(output.size()!=bytes)throw std::runtime_error("Receiver material stage extent");
        std::fill(output.begin(),output.end(),char{});
        for(const auto& write:writes) {
            const auto value=shadowReceiverField(material,write.field);
            if(!value.integer)for(std::uint32_t offset=0;offset<write.bytes;offset+=4) {
                float scalar;std::memcpy(&scalar,static_cast<const char*>(value.data)+write.source+offset,4);
                if(!std::isfinite(scalar))throw std::runtime_error("Receiver used material field is nonfinite");
            }
            std::memcpy(output.data()+write.destination,
                static_cast<const char*>(value.data)+write.source,write.bytes);
        }
    }
};
enum class ShadowReceiverTextureRole : std::uint32_t {Shadow,Lit,SubShadow,Depth,GBuffer0,ShadowPointSampler};
struct ShadowReceiverTextureRoute {ShadowReceiverTextureRole role;std::uint32_t slot{},dimension{};};
struct ShadowReceiverSamplerRoute {ShadowReceiverTextureRole role;std::uint32_t slot{},textureSlot{};};
struct ShadowReceiverMaterialLayout {
    ShadowReceiverStageLayout vertex,pixel;
    std::vector<ShadowReceiverTextureRoute> textures;
    std::vector<ShadowReceiverSamplerRoute> samplers;
};
namespace Interop {
inline ShadowReceiverMaterialLayout readShadowReceiverMaterialLayout(const std::filesystem::path& path) {
    std::ifstream input(path,std::ios::binary);
    auto word=[&] {
        std::uint32_t value{};input.read(reinterpret_cast<char*>(&value),4);
        if(!input)throw std::runtime_error("Incomplete receiver RML1 layout");
        return value;
    };
    if(word()!=0x314C4D52u)throw std::runtime_error("Receiver RML1 layout version");
    ShadowReceiverMaterialLayout result;
    result.vertex.slot=static_cast<std::int32_t>(word());result.vertex.bytes=word();const auto vertexCount=word();
    result.pixel.slot=static_cast<std::int32_t>(word());result.pixel.bytes=word();const auto pixelCount=word();
    const auto textureCount=word(),samplerCount=word();
    if(textureCount>128||samplerCount>16)throw std::runtime_error("Receiver material binding counts");
    auto stage=[&](ShadowReceiverStageLayout& layout,std::uint32_t count) {
        if(layout.slot==-1) {
            if(layout.bytes||count)throw std::runtime_error("Receiver absent constant stage extent");
            return;
        }
        if(layout.slot<0||layout.slot>=14||!layout.bytes||layout.bytes>65536||layout.bytes%16||count>4096)
            throw std::runtime_error("Receiver constant stage format");
        for(std::uint32_t index=0;index<count;++index) {
            ShadowReceiverMaterialWrite write{word(),word(),word(),word()};
            if(write.field>=ShadowReceiverFieldBytes.size()||!write.bytes||
                (write.destination|write.source|write.bytes)%4||
                std::uint64_t(write.source)+write.bytes>ShadowReceiverFieldBytes[write.field]||
                std::uint64_t(write.destination)+write.bytes>layout.bytes)
                throw std::runtime_error("Receiver material semantic range");
            layout.writes.push_back(write);
        }
    };
    stage(result.vertex,vertexCount);stage(result.pixel,pixelCount);
    for(std::uint32_t index=0;index<textureCount;++index) {
        const auto role=word(),slot=word(),dimension=word();
        if(role>=5||slot>=128||dimension!=(role==2?5u:4u)||
            std::any_of(result.textures.begin(),result.textures.end(),[&](const auto& route){return route.slot==slot;}))
            throw std::runtime_error("Receiver texture role or slot");
        result.textures.push_back({static_cast<ShadowReceiverTextureRole>(role),slot,dimension});
    }
    for(std::uint32_t index=0;index<samplerCount;++index) {
        const auto role=word(),slot=word(),textureSlot=word();
        const bool associated=role==5?textureSlot==UINT32_MAX:
            std::any_of(result.textures.begin(),result.textures.end(),[&](const auto& route){
                return std::uint32_t(route.role)==role&&route.slot==textureSlot;});
        if(role>5||slot>=16||!associated||
            std::any_of(result.samplers.begin(),result.samplers.end(),[&](const auto& route){return route.slot==slot;}))
            throw std::runtime_error("Receiver sampler association");
        result.samplers.push_back({static_cast<ShadowReceiverTextureRole>(role),slot,textureSlot});
    }
    if(input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Unexpected receiver layout tail");
    return result;
}
} // namespace Interop
} // namespace San14::Render
