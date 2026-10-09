#pragma once
#include "../../Camera/CameraState.h"
namespace source_weather_fog {
using source_camera::V3;using source_camera::V4;
struct Power {
    std::array<double,257> reciprocal{},logarithms{};
    std::array<double,64> exponentials{};std::array<double,11> constants{};
};
// Conditions and coefficients are decoded once from the resource profile.
// Resource ordinal numbers never participate in weather calculations.
struct WeatherConditions {float cloudCover{},humidity{},altitude{};};
struct WeatherResponse {
    float cloudScale{},cloudHumidityStart{},cloudHumidityEnd{};
    float strength{},heightMultiplier{},opacityBase{};
    float scatteringHumidityStart{},scatteringHumidityEnd{};
    float rayleighDryScale{},rayleighWetScale{},mieDryScale{},mieWetScale{};
    float atmosphereHeight{},saturationStart{},saturationEnd{};
};
struct FogSettings {float startDistance{},anisotropy{},distancePivot{},blendWeight{},blendTarget{};};
struct ScatteringCoefficients {V3 mie{},rayleigh{},mieExtinction{},rayleighExtinction{};};
struct AmbientSettings {float planetRadius{},atmosphereTop{},sunAngle{},moonAngle{};V3 sunRgb{},moonRgb{};};
struct Profile {
    WeatherConditions conditions;
    WeatherResponse response;
    FogSettings fog;
    ScatteringCoefficients scattering;
    AmbientSettings lighting;
    Power power;
    std::vector<uint16_t> ambient;
};
enum class FogMode:uint32_t {Inactive=0,Scattering=7};
struct FogState {
    V4 background{};
    FogMode mode=FogMode::Inactive;
    float startDistance{},endDistance{},planetRadius{};
    V3 rayleigh{};float density{};
    V3 mie{};float anisotropy{};
    float distancePivot{},blend{};
};
struct Frame {
    FogState fog;
    float saturation=0,effectiveHeight=0,heightCoordinate=0,currentAltitude=0;
    V4 sunAmbient{},moonAmbient{};
};
struct Shared {V4 fog{},color{};std::array<V4,4> scat{};};
} // namespace source_weather_fog
