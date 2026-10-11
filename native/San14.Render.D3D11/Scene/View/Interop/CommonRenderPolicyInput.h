#pragma once
#include "../../CommonRenderPolicy.h"
#include <array>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <span>
#include <stdexcept>

namespace San14::Render::Interop {
inline constexpr std::size_t CommonRenderPolicyInputBytes = 16;
inline CommonRenderPolicy decodeCommonRenderPolicy(std::span<const std::uint8_t> bytes) {
    if(bytes.size()!=CommonRenderPolicyInputBytes)
        throw std::runtime_error("Common render policy packet length");
    std::array<std::uint32_t,4> words{};
    std::memcpy(words.data(),bytes.data(),bytes.size());
    if(words[0]!=0x32505243u || words[1]>1 || words[2]>1 || words[3]>1)
        throw std::runtime_error("Common render policy packet format");
    return {words[1]!=0,words[2]!=0,words[3]!=0};
}
inline CommonRenderPolicy readCommonRenderPolicy(const std::filesystem::path& path) {
    std::array<std::uint8_t,CommonRenderPolicyInputBytes> bytes{};
    std::ifstream input(path,std::ios::binary);
    input.read(reinterpret_cast<char*>(bytes.data()),bytes.size());
    if(!input || input.peek()!=std::char_traits<char>::eof())
        throw std::runtime_error("Initial Common render policy is absent or malformed");
    return decodeCommonRenderPolicy(bytes);
}
} // namespace San14::Render::Interop
