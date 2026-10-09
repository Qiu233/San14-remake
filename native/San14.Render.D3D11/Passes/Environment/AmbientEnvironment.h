#pragma once
#include "SeasonAtmosphereConstants.h"

namespace source_current_ambient {
struct CelestialClock {
    float monthDay{}, dayFraction{}, longitudeDegrees{};
};
// Accepted seasonal publication decoded once. No borrowed packet storage or
// camera bytes survive into face generation.
struct Environment {
    source_camera::CameraTransform camera;
    source_current_season_atmosphere::SeasonEnvironment sky;
    source_weather_fog::FogState selectedFog;
    source_camera::V3 selectedRadiance{};
    source_camera::V4 selectedDirection{};
    CelestialClock clock;
    uint32_t season{};
    bool skyEnabled{};
};
inline Environment decodeEnvironment(const source_current_season::Frame &publication) {
    Environment environment;
    environment.camera = source_camera::decodeCameraTransform(publication.main);
    environment.sky = source_current_season_atmosphere::decodeEnvironment(publication);
    const auto &light = source_current_season_atmosphere::selected(publication, 0x80);
    environment.selectedFog =
        source_current_season_atmosphere::packageFog(light, environment.sky.direct.fogBlend);
    environment.selectedRadiance = source_current_season_atmosphere::lightRgb(light);
    environment.selectedDirection = source_camera::get<source_camera::V4>(light.data.data());
    environment.clock = {source_camera::get<float>(publication.main.data() + 0x200),
                         source_camera::get<float>(publication.main.data() + 0x204),
                         source_camera::get<float>(publication.main.data() + 0x208)};
    environment.season = publication.season;
    environment.skyEnabled =
        (source_camera::get<uint32_t>(publication.main.data() + 0x3b0) & 0x80000000u) != 0;
    return environment;
}
} // namespace source_current_ambient
