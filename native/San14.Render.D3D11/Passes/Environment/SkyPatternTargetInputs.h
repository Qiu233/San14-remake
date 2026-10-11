#pragma once
#include <array>
#include <cstddef>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <string>

namespace San14::Render {
struct SkyPatternTargetDescription {
    std::uint32_t key{},width{},height{},levels{};
    std::uint32_t resourceFormat{},shaderFormat{},targetFormat{},samples{};
};

struct SkyPatternTargetInputs {
    std::array<SkyPatternTargetDescription,4> descriptions;
    std::array<std::array<std::uint8_t,52>,4> samplers;
};

namespace Interop {
// These are the four ordered target declarations, not current palette selection
// or evidence that a Pattern operation has produced any pixels.
inline SkyPatternTargetInputs readSkyPatternTargetInputs(const std::filesystem::path& directory) {
    std::ifstream input(directory/"targets.bin",std::ios::binary);
    auto word=[&]() {
        std::uint32_t value{};
        input.read(reinterpret_cast<char*>(&value),sizeof(value));
        if(!input)throw std::runtime_error("Incomplete Pattern PTP1 target declarations");
        return value;
    };
    if(word()!=0x31505450u||word()!=4)
        throw std::runtime_error("Pattern PTP1 target declarations version");
    SkyPatternTargetInputs result;
    for(auto& target:result.descriptions) {
        target={word(),word(),word(),word(),word(),word(),word(),word()};
        if(!target.key||!target.width||!target.height||!target.levels||target.samples!=1)
            throw std::runtime_error("Unsupported Pattern target description");
        auto extent=target.width>target.height?target.width:target.height;
        std::uint32_t fullLevels=1;
        while(extent>1){extent>>=1;++fullLevels;}
        if(target.levels>fullLevels)
            throw std::runtime_error("Pattern target mip count exceeds its dimensions");
    }
    if(input.peek()!=std::char_traits<char>::eof())
        throw std::runtime_error("Unexpected Pattern PTP1 target declarations tail");
    for(std::size_t index=0;index<result.samplers.size();++index) {
        std::ifstream sampler(directory/("target"+std::to_string(index)+"-sampler-desc.bin"),std::ios::binary);
        auto& bytes=result.samplers[index];
        sampler.read(reinterpret_cast<char*>(bytes.data()),bytes.size());
        if(!sampler||sampler.peek()!=std::char_traits<char>::eof())
            throw std::runtime_error("Pattern target sampler description extent");
    }
    return result;
}
} // namespace Interop
} // namespace San14::Render
