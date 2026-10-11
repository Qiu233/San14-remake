#pragma once
// Main118E6B0 uses the same rotation-derived MoonFace writer as WORK.
// Publication changes the named MoonFace value before the normal camera upload.
#include "AmbientFrame.h"
namespace source_current_main_sky {
inline source_camera::Matrix prepareMoonFace(const source_current_season::Frame& current) {
 const auto environment=source_current_season_atmosphere::decodeEnvironment(current);
 return source_current_ambient::moonFace(environment.moonFaceDirection);
}
inline void publishMoonFace(source_atmosphere_camera::Packet& packet,const source_current_season::Frame& current) {
 packet.sky.moonFace=prepareMoonFace(current);
}
} // namespace source_current_main_sky
#ifndef SOURCE_CURRENT_AMBIENT_CPU_ONLY
static void publishNativeCurrentMainSkyMoonFace(NativeAtmosphereCamera& owner,const source_current_season::Frame& current) {
 source_current_main_sky::publishMoonFace(owner.packet,current);
}
#endif
