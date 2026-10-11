#pragma once
#include "../Camera/CameraTransform.h"
#include <array>

namespace San14::Render {
// Host clipping is independent of camera-specific horizon and Ocean material
// planes. A fresh frontend disables all eight planes; a disabled shader input
// is neutral even when a previous raw plane value exists.
class GraphicsClipState {
public:
    void setPlane(std::size_t index, source_camera::V4 plane) {
        planes_.at(index).value = plane;
    }
    void enablePlane(std::size_t index, bool enabled) {
        planes_.at(index).enabled = enabled;
    }
    source_camera::V4 shaderPlane(std::size_t index) const {
        const auto& plane = planes_.at(index);
        return plane.enabled ? plane.value : source_camera::V4{};
    }
private:
    struct Plane { source_camera::V4 value{}; bool enabled{}; };
    std::array<Plane, 8> planes_{};
};
} // namespace San14::Render
