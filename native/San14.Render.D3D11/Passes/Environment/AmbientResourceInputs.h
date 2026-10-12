#pragma once
#include "../../Camera/CameraTransform.h"
#include <array>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <span>
#include <stdexcept>
#include <vector>

namespace San14::Render {
struct AmbientTargetDescription {
    std::uint32_t role{},key{},width{},height{},levels{},resourceFormat{},shaderFormat{},targetFormat{},depthFormat{},samples{};
    bool operator==(const AmbientTargetDescription&) const = default;
};
struct AmbientResourceDescription {
    std::uint32_t filterSamples{};
    bool skyEnabled{};
    std::array<AmbientTargetDescription,8> targets;
};
struct AmbientMaterialState {
    source_camera::V4 mipInfo{};
    source_camera::Matrix clipToWorld{};
    source_camera::V4 interpolation{};
};
namespace Interop {
inline AmbientResourceDescription readAmbientResourceDescription(const std::filesystem::path& path) {
    std::ifstream stream(path,std::ios::binary);
    auto word=[&](){std::uint32_t value{};stream.read(reinterpret_cast<char*>(&value),4);
        if(!stream)throw std::runtime_error("Incomplete ambient ASR1 resources");return value;};
    if(word()!=0x31525341u)throw std::runtime_error("Ambient ASR1 resources version");
    AmbientResourceDescription result;result.filterSamples=word();const auto enabled=word();
    if(!result.filterSamples||enabled>1||word()!=8)throw std::runtime_error("Ambient ASR1 resources header");
    result.skyEnabled=enabled!=0;
    for(unsigned role=0;role<8;++role) {
        auto& target=result.targets[role];
        target={word(),word(),word(),word(),word(),word(),word(),word(),word(),word()};
        if(target.role!=role||!target.width||target.width!=target.height||!target.levels||target.samples!=1)
            throw std::runtime_error("Ambient target description");
        if(role<6&&target.levels!=7)
            throw std::runtime_error("Ambient normal generation requires the seven-level source schedule");
    }
    if(stream.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Ambient ASR1 resources tail");
    return result;
}
struct AmbientMaterialField {const void* data;std::size_t bytes;};
inline AmbientMaterialField ambientMaterialField(const AmbientMaterialState& material,std::uint32_t id) {
    switch(id) {
        case 0:return {&material.mipInfo,sizeof(material.mipInfo)};
        case 1:return {&material.clipToWorld,sizeof(material.clipToWorld)};
        case 2:return {&material.interpolation,sizeof(material.interpolation)};
        default:throw std::runtime_error("Unknown ambient material semantic");
    }
}
struct AmbientMaterialWrite {std::uint32_t field,destination,source,bytes;};
struct AmbientMaterialStage {
    std::uint32_t slot{},byteSize{};
    std::vector<AmbientMaterialWrite> writes;
    void pack(const AmbientMaterialState& material,std::span<char> destination) const {
        for(const auto& write:writes) {
            const auto field=ambientMaterialField(material,write.field);
            std::memcpy(destination.data()+write.destination,
                static_cast<const char*>(field.data)+write.source,write.bytes);
        }
    }
};
struct AmbientMaterialBinding {std::uint32_t stage,role,slot;};
struct AmbientProgramLayout {
    AmbientMaterialStage vertex,pixel;
    std::vector<AmbientMaterialBinding> resources,samplers;
};
inline std::array<AmbientProgramLayout,3> readAmbientMaterialLayout(const std::filesystem::path& path) {
    std::ifstream input(path,std::ios::binary);
    auto word=[&](){std::uint32_t value{};input.read(reinterpret_cast<char*>(&value),4);
        if(!input)throw std::runtime_error("Incomplete ambient AML1 material");return value;};
    if(word()!=0x314c4d41u||word()!=3)throw std::runtime_error("Ambient AML1 material version");
    std::array<AmbientProgramLayout,3> result;unsigned operations=0;const AmbientMaterialState shape;
    for(unsigned i=0;i<3;++i) {
        const auto operation=word();
        if(operation>=3||(operations&(1u<<operation))||word()!=2)
            throw std::runtime_error("Ambient material operation");
        operations|=1u<<operation;auto& layout=result[operation];unsigned stages=0;
        for(unsigned j=0;j<2;++j) {
            const auto stage=word();if(stage>1||(stages&(1u<<stage)))throw std::runtime_error("Ambient material stage");
            stages|=1u<<stage;auto& target=stage?layout.vertex:layout.pixel;
            target.slot=word();target.byteSize=word();const auto count=word();
            if(target.slot>=14||!target.byteSize||target.byteSize%16||target.byteSize>65536||count>256)
                throw std::runtime_error("Ambient constant-buffer layout");
            for(std::uint32_t row=0;row<count;++row) {
                AmbientMaterialWrite write{word(),word(),word(),word()};
                const auto field=ambientMaterialField(shape,write.field);
                if(!write.bytes||write.source>field.bytes||write.bytes>field.bytes-write.source||
                    write.destination>target.byteSize||write.bytes>target.byteSize-write.destination)
                    throw std::runtime_error("Ambient material write range");
                target.writes.push_back(write);
            }
        }
        auto bindings=[&](std::vector<AmbientMaterialBinding>& rows,bool sampler) {
            const auto count=word();if(count>128)throw std::runtime_error("Ambient material binding count");
            for(std::uint32_t row=0;row<count;++row) {
                AmbientMaterialBinding binding{word(),word(),word()};
                if(binding.stage>1||binding.role>3||binding.slot>=(sampler?16u:128u)||
                    (sampler&&binding.role>1))throw std::runtime_error("Ambient material resource binding");
                rows.push_back(binding);
            }
        };
        bindings(layout.resources,false);bindings(layout.samplers,true);
    }
    if(input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Ambient AML1 material tail");
    return result;
}
inline std::array<source_camera::Matrix,6> readAmbientBlendCameras(const std::filesystem::path& path) {
    std::array<source_camera::Matrix,6> result{};std::ifstream input(path,std::ios::binary);
    input.read(reinterpret_cast<char*>(result.data()),sizeof(result));
    if(!input||input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Ambient blend camera extent");
    return result;
}
} // namespace Interop
} // namespace San14::Render
