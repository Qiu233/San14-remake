#pragma once
#include <cstdint>

namespace San14::Render {
enum class SkyAlphaBlendMode : std::uint32_t { Replace, Add, Maximum };
// The actual Common owner publishes this view option. An absent Common differs
// from a present Common that explicitly allows or suppresses Ocean drawing.
struct CommonRenderPolicy {
    bool present{};
    bool suppressOceanDrawing{};
    bool enableSkyDrawing{};
    SkyAlphaBlendMode skyAlphaBlend{SkyAlphaBlendMode::Maximum};
};
inline bool skyCloudDrawingEnabled(const CommonRenderPolicy& policy) {
    return !policy.present||policy.enableSkyDrawing;
}
} // namespace San14::Render
