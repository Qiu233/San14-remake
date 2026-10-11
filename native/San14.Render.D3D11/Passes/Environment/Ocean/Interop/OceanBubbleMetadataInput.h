#pragma once
#include "../OceanBubbleRuntime.h"
#include <cstring>
#include <filesystem>
#include <fstream>
#include <optional>
#include <span>
#include <stdexcept>

namespace San14::Render::Interop {
template<std::size_t Size>
inline std::array<std::uint8_t,Size> readOceanMetadata(const std::filesystem::path& path) {
    std::ifstream stream(path,std::ios::binary);
    std::array<std::uint8_t,Size> bytes;
    stream.read(reinterpret_cast<char*>(bytes.data()),bytes.size());
    if (!stream || stream.peek()!=std::char_traits<char>::eof()) throw std::runtime_error("Ocean metadata input extent");
    return bytes;
}
inline std::optional<OceanBubbleFrameRanges> decodeOceanBubbleFrameRanges(std::span<const std::uint8_t> bytes) {
    if (bytes.size()!=32) throw std::runtime_error("Ocean bubble frames OBF1 extent");
    auto word=[&](std::size_t at) { std::uint32_t value;std::memcpy(&value,bytes.data()+at,4);return value; };
    if (word(0)!=0x3146424Fu || word(4)>1) throw std::runtime_error("Ocean bubble frames OBF1 version");
    if (!word(4)) return std::nullopt;
    return OceanBubbleFrameRanges{{word(8),word(12),word(16)},{word(20),word(24),word(28)}};
}
inline bool decodeOceanGeneratorEnabled(std::span<const std::uint8_t> bytes) {
    if (bytes.size()!=8) throw std::runtime_error("Ocean material features OMF1 extent");
    std::uint32_t magic,enabled;
    std::memcpy(&magic,bytes.data(),4);std::memcpy(&enabled,bytes.data()+4,4);
    if (magic!=0x31464D4Fu || enabled>1) throw std::runtime_error("Ocean material features OMF1 version");
    return enabled!=0;
}
} // namespace San14::Render::Interop
