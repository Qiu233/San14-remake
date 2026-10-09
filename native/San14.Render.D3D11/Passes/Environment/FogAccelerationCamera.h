// Current fixed-lens normal Main FogAccel camera lanes. Import complete source
// templates once; preserve fog/scattering/interval/lens fields from their owner.
#pragma once
#include <array>
#include <cstring>
#include "../../Camera/CameraMatrix.h"
namespace source_aux_camera {
struct FogAccelerationConstants {
 std::array<char,144> vertex;
 std::array<char,112> pixel;
};
inline void writeCurrentFogAcceleration(FogAccelerationConstants& constants,const unsigned char* mainPrefix) {
 source_matrix::Matrix view;
 std::memcpy(view.data(),mainPrefix+0x80,64);
 auto inverse=source_matrix::inverse(view);
 std::memcpy(constants.vertex.data(),mainPrefix+0x100,64); // selected VS1 mW2P
 std::memcpy(constants.vertex.data()+80,inverse.data(),64); // selected VS1 mV2W
 std::memcpy(constants.pixel.data(),mainPrefix+0x10,16); // selected PS3 vEye
}
}
