#pragma once
#include <cstdint>
#include <optional>

namespace San14::Render {
// These are accepted Main values. The actual weather producer supplies the
// Cloud and Cirrus draw gates separately from this camera/settings subset.
struct SkyPatternCurrentInputs {
    std::int32_t cameraKind{};
    float scale{}, height{};
    std::optional<std::uint32_t> lowCostLevel, rayExponent, skyDomeLightGroupId;
};
} // namespace San14::Render
