// Current fixed-lens normal Main FogAccel camera lanes. Import complete source
// templates once; preserve fog/scattering/interval/lens fields from their owner.
#pragma once
#include <array>
#include <cstring>
#include "../../Camera/CameraMatrix.h"
#include "../../Camera/CameraTransform.h"
namespace source_aux_camera {
struct FogAccelerationConstants {
 std::array<char,144> vertex;
 std::array<char,112> pixel;
};
inline void writeCurrentFogAcceleration(FogAccelerationConstants& constants,const source_camera::CameraTransform& camera) {
 auto inverse=source_matrix::inverse(camera.view);
 std::memcpy(constants.vertex.data(),camera.worldToClip.data(),64); // selected VS1 mW2P
 std::memcpy(constants.vertex.data()+80,inverse.data(),64); // selected VS1 mV2W
 std::memcpy(constants.pixel.data(),&camera.eye,16); // selected PS3 vEye
}
}
