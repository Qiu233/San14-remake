#pragma once
#include "../../Camera/CameraTransform.h"
#include <array>

namespace San14::Render {
// Each real Main Star draw prepares these named values. The two passes share
// the twinkle table and phase owner, while their geometry and material inputs
// remain separate from WORK's six cameras and complete constant packets.
struct MainStarMaterialState {
    source_camera::Matrix eyeToClip{};
    source_camera::V4 phase{};
    std::array<source_camera::V4,16> twinkleTable{};
    source_camera::V4 starInfo{};
    source_camera::V4 pointScale{};
    source_camera::V4 showerInfo0{},showerInfo1{};
};
} // namespace San14::Render
