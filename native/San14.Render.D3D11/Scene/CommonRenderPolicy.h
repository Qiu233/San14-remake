#pragma once

namespace San14::Render {
// The actual Common owner publishes this view option. An absent Common differs
// from a present Common that explicitly allows or suppresses Ocean drawing.
struct CommonRenderPolicy {
    bool present{};
    bool suppressOceanDrawing{};
    bool enableSkyDrawing{};
};
inline bool skyCloudDrawingEnabled(const CommonRenderPolicy& policy) {
    return !policy.present||policy.enableSkyDrawing;
}
} // namespace San14::Render
