#pragma once
#include <array>
#include <cstdint>

namespace source_mode2_octree {
using DisplayConfig = std::array<std::uint32_t, 6>;

// CE17BD0 after V418; only the retained App bits6/7 trigger publication.
inline bool publishAfterV418(DisplayConfig& config, std::uint16_t& appFlags,
                            int quality, int mainMode) {
    const std::uint16_t bit7 = mainMode < 2 ? 1 : 0;
    const std::uint16_t bit6 = quality < 2 && mainMode < 2 ? 1 : 0;
    if (((appFlags >> 6) & 1) == bit6 && ((appFlags >> 7) & 1) == bit7) return false;
    appFlags = static_cast<std::uint16_t>((appFlags & ~std::uint16_t(0xC0)) | (bit6 << 6) | (bit7 << 7));
    config[2] = bit7;
    config[5] = bit6;
    return true;
}

// CE3FFFB0 publishes the result to the resolved named Octree CPU+120.
inline std::uint32_t sceneTypeMask(const DisplayConfig& c) {
    return (c[0] ? 1u : 0u) | (c[4] ? 4u : 0u) | (c[1] ? 8u : 0u)
         | (c[2] ? 0x100u : 0u) | (c[5] ? 0x4000u : 0u) | 0x8000u;
}

// Normal collection and update use this integer gate before World callbacks.
inline bool admitsEntry(std::uint32_t rawTypeBits, std::uint32_t publishedMask) {
    return (rawTypeBits & publishedMask) != 0;
}
} // namespace source_mode2_octree
