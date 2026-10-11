#pragma once
#include "../../Camera/CameraTransform.h"
#include <array>

namespace San14::Render {
// Main and WORK stage zero use the same material writer with their actual
// cameras. The generated second stream remains exclusive to Main stage one.
struct StarDrawCamera {
    float eyeHeight{},viewSpan{};
    source_camera::Matrix worldToClip{};
    static StarDrawCamera from(const source_camera::CameraTransform& camera) {
        return {camera.eye.Y,camera.optics.viewSpan,camera.worldToClip};
    }
};

struct MainStarMaterialState {
    source_camera::Matrix eyeToClip{};
    source_camera::V4 phase{};
    std::array<source_camera::V4,16> twinkleTable{};
    source_camera::V4 starInfo{};
    source_camera::V4 pointScale{};
    source_camera::V4 showerInfo0{},showerInfo1{};
};
} // namespace San14::Render
