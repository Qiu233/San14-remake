#pragma once
// Main118E6B0 uses the same rotation-derived MoonFace writer as WORK.
// Publication changes only PS336[192..255], before the normal camera upload.
#include "current-ambient-frame-native.h"
namespace source_current_main_sky {
inline source_camera::Matrix prepareMoonFace(const source_current_season::Frame& current) {
 const auto* atmosphere=reinterpret_cast<const uint8_t*>(current.atmosphere.data());
 return source_current_ambient::moonFace(source_camera::get<source_camera::V4>(atmosphere+280));
}
inline void publishMoonFace(source_atmosphere_camera::Packet& packet,const source_current_season::Frame& current) {
 source_camera::put(packet.skyPixel.data()+192,prepareMoonFace(current));
}
} // namespace source_current_main_sky
#ifndef SOURCE_CURRENT_AMBIENT_CPU_ONLY
static void publishNativeCurrentMainSkyMoonFace(NativeAtmosphereCamera& owner,const source_current_season::Frame& current) {
 source_current_main_sky::publishMoonFace(owner.packet,current);
}
#endif
