// Current fixed-lens Main fog reduction. Import complete stage templates once;
// the accepted camera and global light supply their live lanes before drawing.
#pragma once
#include <array>
#include <cstring>
#include "../../Camera/CameraMatrix.h"
#include "../../Camera/CameraTransform.h"
#include "WeatherFogFrame.h"
namespace source_aux_camera {
struct FogAccelerationInputs {
 source_weather_fog::FogState fog;
 source_camera::V3 directionalRgb;
};
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
inline void writeCurrentFogAccelerationLight(FogAccelerationConstants& constants,
 const FogAccelerationInputs& current,bool fogEnabled) {
 source_weather_fog::Shared fields;
 if(fogEnabled)fields=source_weather_fog::shared(current.fog,current.directionalRgb);
 else {
  const auto& fog=current.fog;
  const float pivot=fog.distancePivot>source_weather_fog::MinimumFogDistance
   ?fog.distancePivot:source_weather_fog::MinimumFogDistance;
  fields.fog={pivot,source_camera::D(1,pivot),fog.blend,fog.planetRadius};
  fields.scat={source_camera::V4{0,0,0,fog.startDistance},
   source_camera::V4{0,0,0,fog.endDistance},source_camera::V4{},source_camera::V4{0,0,0,1}};
 }
 std::memcpy(constants.pixel.data()+16,&fields.fog,16);
 std::memcpy(constants.pixel.data()+32,fields.scat.data(),64);
}
}
