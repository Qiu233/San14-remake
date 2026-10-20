#pragma once
#include "AmbientEnvironment.h"
#include <optional>

namespace source_current_ambient {
// Only the consumed subset of D3A850's retained LightData. The current Camera,
// celestial controller, package-blend scalar and Cloud phases are not retained.
struct SelectedAmbientLight {
    source_weather_fog::FogState fog;
    source_camera::V3 radiance;
    source_camera::V4 direction;
};
inline SelectedAmbientLight decodeSelectedAmbientLight(const source_current_season::Frame& current) {
    const auto& light=source_current_season_atmosphere::selected(current,0x80);
    return {source_current_season_atmosphere::fogRecord(light),
        source_current_season_atmosphere::lightRgb(light),
        source_camera::get<source_camera::V4>(light.data.data())};
}
class GenerationLightSnapshot {
public:
    void beginBaseCall(unsigned firstFace,const SelectedAmbientLight& current) {
        if(firstFace==0)retained_=current;
        if(!retained_)throw std::runtime_error("Ambient continuation has no generation light");
    }
    void apply(Environment& current) const {
        if(!retained_)throw std::runtime_error("Ambient generation light is absent");
        current.selectedFog=retained_->fog;
        // D3A850 reads the current Camera's package blend after fetching the
        // retained Fog96. Do not latch its already blended result.
        const float blend=current.sky.direct.fogBlend;
        if(current.selectedFog.mode==source_weather_fog::FogMode::Scattering&&blend>0)
            current.selectedFog.blend=source_camera::A(
                source_camera::M(source_camera::S(1,blend),current.selectedFog.blend),blend);
        current.selectedRadiance=retained_->radiance;
        current.selectedDirection=retained_->direction;
    }
    // Only replacement/destruction of the SkyAmbient owner discards this
    // state. A WORK-only reset, disabled call, filter call or completion does
    // not reset the original SkyAmbient+60 allocation/content.
    void resetForNewOwner(){retained_.reset();}
private:
    std::optional<SelectedAmbientLight> retained_;
};
} // namespace source_current_ambient
