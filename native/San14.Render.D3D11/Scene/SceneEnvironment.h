#pragma once
#include "../Camera/CameraState.h"
#include "Terrain/TreeWindEnvironment.h"

namespace San14::Render {
struct SceneCameraInputs {
    std::array<float,4> eye;
    std::array<float,3> position;
    bool ordinary;
    float weatherHeight,exposure,luminance;
};
struct InitialSceneEnvironment {
    tree2_source::WindEnvironment vegetation;
    float territoryReferenceHeight{};
    uint32_t exposureRectangleFlags{};
};

// The renderer owns these accepted values. Camera and media interchange bytes
// are decoded at publication, never used as a retained simulated Main object.
class SceneEnvironment {
public:
    SceneEnvironment()=default;
    explicit SceneEnvironment(const InitialSceneEnvironment& initial)
        {initialize(initial);}
    void initialize(const InitialSceneEnvironment& initial) {
        vegetation_=initial.vegetation;territoryReferenceHeight_=initial.territoryReferenceHeight;
        exposureRectangleFlags_=initial.exposureRectangleFlags;
    }

    void acceptCamera(const source_camera::Prefix& packet,const SceneCameraInputs& camera) {
        directCamera_=packet;camera_=camera;
    }
    void acceptVegetation(const tree2_source::WindEnvironment& environment,uint32_t exposureRectangleFlags) {
        vegetation_=environment;exposureRectangleFlags_=exposureRectangleFlags;
    }
    // This publication follows the final direct camera publication. Sparse
    // seasonal tail writers cannot replace the camera selected for vegetation.
    void publishVegetationCamera() {
        vegetation_.cameraPosition=camera_.position;vegetation_.ordinaryCamera=camera_.ordinary;
    }
    const source_camera::Prefix& directCameraPacket()const{return directCamera_;}
    const SceneCameraInputs& camera()const{return camera_;}
    const tree2_source::WindEnvironment& vegetation()const{return vegetation_;}
    float territoryReferenceHeight()const{return territoryReferenceHeight_;}
    uint32_t exposureRectangleFlags()const{return exposureRectangleFlags_;}
private:
    source_camera::Prefix directCamera_{};
    SceneCameraInputs camera_{};
    tree2_source::WindEnvironment vegetation_{};
    float territoryReferenceHeight_{};
    uint32_t exposureRectangleFlags_{};
};
} // namespace San14::Render
