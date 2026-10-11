#pragma once
#include <array>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <utility>
#include <vector>

namespace San14::Render {
struct ShaderFeatureKey {
    std::uint32_t flags{}, directionalLights{}, pointLights{}, spotLights{}, hemisphereLights{};
    std::uint32_t directionalMode{}, fogMode{}, fogQuality{}, shadowMode{}, shadowQuality{};
    std::uint32_t localShadowMode{}, materialMode{};
};
// Shared actual G1 feature ranking for HDR and Cloud Pattern selection.
class ShaderFeatureSelection {
    static std::int32_t difference(std::uint32_t left, std::uint32_t right) {
        const auto value = left - right;
        return static_cast<std::int32_t>((value & 0x80000000u) ? 0u - value : value);
    }
    static std::pair<std::int32_t, std::int32_t> score(const ShaderFeatureKey& c, const ShaderFeatureKey& r) {
        const auto request = c.flags & 0xF77FFu;
        const auto missing = request - (request & r.flags);
        std::uint32_t penalty = ((missing & ~0x80u) << 16) + ((missing >> 7) & 1);
        std::int32_t excess = (r.flags & ~request) != 0 ? 1 : 0;
        const std::array counts{c.directionalLights, c.pointLights, c.spotLights, c.hemisphereLights};
        const std::array available{r.directionalLights, r.pointLights, r.spotLights, r.hemisphereLights};
        for (std::size_t i = 0; i < counts.size(); ++i) {
            if (available[i] < counts[i]) penalty += (counts[i] - available[i]) << 8;
            else if (r.flags & (1u << i)) excess += std::int32_t(available[i] - counts[i]);
            else penalty += (available[i] - counts[i]) << 4;
        }
        if (c.fogMode == 0) excess += difference(r.fogMode, 0);
        else {
            if (r.fogMode != 5 || c.fogMode == 4 || c.fogMode == 7) penalty += difference(r.fogMode, c.fogMode);
            penalty += difference(r.fogQuality, c.fogQuality);
        }
        penalty += std::uint32_t(difference(c.directionalMode, r.directionalMode)) << 12;
        if ((r.flags & 0x100) == 0) penalty += difference(c.shadowMode, r.shadowMode);
        else if (std::int32_t(r.shadowMode) >= 7 && std::int32_t(c.shadowMode) >= 7 && r.shadowMode != c.shadowMode)
            penalty += difference(c.shadowMode, r.shadowMode);
        else if (std::int32_t(r.shadowMode) < std::int32_t(c.shadowMode) || ((c.shadowMode & 4) && std::int32_t(r.shadowMode) > 7))
            penalty += c.shadowMode - r.shadowMode;
        else excess += difference(c.shadowMode, r.shadowMode);
        if (c.shadowMode != 0) {
            penalty += std::uint32_t(difference(c.shadowQuality, r.shadowQuality)) << 12;
            if (r.localShadowMode != 5 || c.localShadowMode == 2)
                penalty += std::uint32_t(difference(c.localShadowMode, r.localShadowMode)) << 8;
        }
        return {static_cast<std::int32_t>(penalty), excess};
    }
public:
    static std::size_t select(const ShaderFeatureKey& current, const std::vector<ShaderFeatureKey>& features) {
        std::size_t best = 0;
        auto penalty = std::numeric_limits<std::int32_t>::max();
        auto excess = std::numeric_limits<std::int32_t>::max();
        for (std::size_t i = 0; i < features.size(); ++i) {
            const auto [p, e] = score(current, features[i]);
            if (p == 0) {
                if (e < excess) { excess = e; best = i; }
                if (e == 0) break;
                penalty = 0;
            } else if (p < penalty) { penalty = p; best = i; }
        }
        return best;
    }
};
} // namespace San14::Render
