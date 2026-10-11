#pragma once
#include <array>

namespace San14::Render {
// Current PB2 material values. Albedo alpha is retained in transport although
// the selected shader uses roughness in both color vectors' fourth component.
struct OceanOpticsSettings {
    float refractionIndex{},refractionTransparency{};
    std::array<float,4> albedo{};
    std::array<float,3> reflectance{};
    float roughness{},diffuseTransparency{};
};
inline OceanOpticsSettings acceptOceanOpticsSettings(OceanOpticsSettings settings) {
    // The actual scalar setter uses an ordered comparison, preserving NaN.
    if(settings.refractionIndex<=1e-6f)settings.refractionIndex=1e-6f;
    return settings;
}
inline std::array<float,4> oceanRefractionParameters(const OceanOpticsSettings& settings) {
    float difference=settings.refractionIndex-1.0f;
    if(difference<0.0f)difference=-difference;
    // Signed-zero transparency intentionally produces the corresponding IEEE
    // infinity. These callbacks impose no positivity/finite-value clamp.
    return {1.0f/settings.refractionIndex,difference,
        settings.refractionTransparency*8.0f,-1.0f/settings.refractionTransparency};
}
} // namespace San14::Render
