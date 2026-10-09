#pragma once
#include "TreeWindEnvironment.h"

namespace ground_details {
struct VisibilityPolicy {
    uint32_t placementFlags{},frustumKind{},cullingMode{},scanningOrder{};
    uint16_t baseAppFlags{};
};
struct ViewPolicy {
    bool present{};
    uint32_t categoryFlags{},excludedCategories{};
};
struct DisplayState {
    int mode{};
    float zoomLevel{};
    bool hostPlacementCallbackExecuted{},overlaySelected{};
    uint32_t placementOverlay{};
};
struct PopulationDispatch {
    uint32_t firstViews{},secondViews{},shadowViews{},registeredViews{},successfulViews{},selectedView{};
};
struct CameraMatrices {
    std::array<float,16> worldToProjection;
    std::array<float,12> worldToView;
};
// Shader matrix packing uses the camera's mathematical transform directly.
inline CameraMatrices cameraMatrices(const source_camera::Frame& camera) {
    CameraMatrices result;
    result.worldToProjection=camera.worldToClip;
    for(size_t row=0;row<3;++row)for(size_t column=0;column<4;++column)
        result.worldToView[row*4+column]=camera.view[column*4+row];
    return result;
}
struct FrameInputs {
    const source_camera::Frame& camera;
    tree2_source::WindEnvironment environment;
    const NativeFrameClockPublication& clock;
    ViewPolicy view;
    uint32_t commonSettingsFlags{};
    PopulationDispatch population;
    DisplayState display;
    uint32_t season{};
};
} // namespace ground_details
