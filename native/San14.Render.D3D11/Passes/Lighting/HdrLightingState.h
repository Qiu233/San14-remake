#pragma once
#include "../Environment/WeatherFogFrame.h"
#include <span>
#include <optional>

namespace San14::Render {
using HdrVector = source_camera::V4;
struct HdrLightModifier {
    bool present{}, adjustsLighting{};
    std::array<float, 4> directionalFactors{};
    float ambientFactor{}, hemisphereFactor{};
    std::optional<std::array<float, 2>> occlusionRates;
};
struct HdrDirectionalLight {
    HdrVector direction{}, color{};
    float occurrence{};
};
struct HdrHemisphereLighting {
    HdrVector upperDiffuse{}, lowerDiffuse{}, upperSpecular{}, lowerSpecular{};
};
struct HdrLightState {
    std::array<HdrDirectionalLight, 4> directional;
    HdrVector ambient{};
    HdrLightModifier modifier;
    HdrVector diffuse{}, specular{};
    float diffuseIntensity{}, specularIntensity{};
    HdrHemisphereLighting hemisphere;
    source_weather_fog::FogState fog;
    bool preservesFreshHatching{}, preservesFreshShadowFade{}, causticsUpdateDisabled{};
};
struct HdrLightingSettings {
    float ambientMapsRate{};
    bool suppressAmbient{}, fogEnabled{};
};
enum class HdrLightingField : std::uint32_t {
    Direction, Color, Occurrence, Ambient, DirectionalCount, Fog, FogColor,
    Scattering, AmbientMapsRate, AmbientDiffuseScale, AmbientSpecularScale, OcclusionRates
};
inline constexpr std::array<std::uint32_t, 12> HdrLightingFieldBytes{
    64, 64, 16, 16, 4, 16, 16, 64, 4, 16, 16, 8};
struct HdrLightingValues {
    std::array<HdrVector, 4> directions{}, colors{};
    std::array<float, 4> occurrences{};
    HdrVector ambient{};
    std::uint32_t directionalCountMinusOne{};
    source_weather_fog::Shared fog;
    float ambientMapsRate{};
    HdrVector ambientDiffuseScale{}, ambientSpecularScale{};
    HdrHemisphereLighting hemisphere;
    std::optional<std::array<float, 2>> occlusionRates;
};
inline HdrVector scaleHdrRgb(HdrVector color, float factor) {
    if (!std::isfinite(factor)) throw std::runtime_error("Nonfinite HDR color multiplier");
    if (factor == 1) return color;
    if (!std::isfinite(color.X) || !std::isfinite(color.Y) || !std::isfinite(color.Z))
        throw std::runtime_error("Nonfinite HDR color to scale");
    return {source_camera::M(color.X, factor), source_camera::M(color.Y, factor),
        source_camera::M(color.Z, factor), color.W};
}
inline bool positiveHdrRgb(HdrVector color) { return color.X > 0 || color.Y > 0 || color.Z > 0; }
inline HdrLightingValues prepareHdrLighting(const HdrLightState& outer,
    const HdrLightState& inner, const HdrLightingSettings& settings) {
    const auto& selected = inner.modifier.present ? inner : outer;
    const auto& modifier = selected.modifier;
    const bool adjust = modifier.present && (settings.suppressAmbient || modifier.adjustsLighting);
    HdrLightingValues result;
    result.occlusionRates = modifier.occlusionRates;
    result.ambient = outer.ambient;
    result.hemisphere = outer.hemisphere;
    for (std::size_t i = 0; i < outer.directional.size(); ++i) {
        result.directions[i] = outer.directional[i].direction;
        result.colors[i] = adjust ? scaleHdrRgb(outer.directional[i].color, modifier.directionalFactors[i])
            : outer.directional[i].color;
        result.occurrences[i] = outer.directional[i].occurrence;
    }
    if (adjust) {
        if (!settings.suppressAmbient) result.ambient = scaleHdrRgb(result.ambient, modifier.ambientFactor);
        auto& hemi = result.hemisphere;
        hemi.upperDiffuse = scaleHdrRgb(hemi.upperDiffuse, modifier.hemisphereFactor);
        hemi.lowerDiffuse = scaleHdrRgb(hemi.lowerDiffuse, modifier.hemisphereFactor);
        hemi.upperSpecular = scaleHdrRgb(hemi.upperSpecular, modifier.hemisphereFactor);
        hemi.lowerSpecular = scaleHdrRgb(hemi.lowerSpecular, modifier.hemisphereFactor);
    }
    if (settings.suppressAmbient) result.ambient = {0, 0, 0, result.ambient.W};
    std::size_t count = result.colors.size();
    while (count > 1 && !positiveHdrRgb(result.colors[count - 1])) --count;
    result.directionalCountMinusOne = std::uint32_t(count - 1);
    for (std::size_t i = count; i < result.colors.size(); ++i) {
        result.directions[i] = {0, 1, 0, 0};
        result.colors[i] = {};
        result.occurrences[i] = 1;
    }
    const auto color = result.colors[0];
    if (!settings.fogEnabled || outer.fog.mode == source_weather_fog::FogMode::Inactive) {
        // Inactive fog retains its coefficients, without evaluating unused
        // scattering inputs. The shared active producer remains unchanged.
        const float pivot = outer.fog.distancePivot > source_weather_fog::MinimumFogDistance
            ? outer.fog.distancePivot : source_weather_fog::MinimumFogDistance;
        result.fog.fog = {pivot, source_camera::D(1, pivot), outer.fog.blend, outer.fog.planetRadius};
        result.fog.color = outer.fog.background;
        result.fog.scat = {HdrVector{0, 0, 0, outer.fog.startDistance},
            HdrVector{0, 0, 0, outer.fog.endDistance}, HdrVector{}, HdrVector{0, 0, 0, 1}};
    } else {
        result.fog = source_weather_fog::shared(outer.fog, {color.X, color.Y, color.Z});
    }
    result.ambientMapsRate = settings.ambientMapsRate;
    result.ambientDiffuseScale = {source_camera::M(selected.diffuseIntensity, selected.diffuse.X),
        source_camera::M(selected.diffuseIntensity, selected.diffuse.Y),
        source_camera::M(selected.diffuseIntensity, selected.diffuse.Z), selected.diffuse.W};
    result.ambientSpecularScale = {source_camera::M(selected.specularIntensity, selected.specular.X),
        source_camera::M(selected.specularIntensity, selected.specular.Y),
        source_camera::M(selected.specularIntensity, selected.specular.Z), selected.specular.W};
    return result;
}
inline std::span<const std::uint8_t> hdrLightingField(const HdrLightingValues& values, HdrLightingField field) {
    auto bytes = [](const auto& value) {
        return std::span<const std::uint8_t>(reinterpret_cast<const std::uint8_t*>(&value), sizeof(value));
    };
    switch (field) {
        case HdrLightingField::Direction: return bytes(values.directions);
        case HdrLightingField::Color: return bytes(values.colors);
        case HdrLightingField::Occurrence: return bytes(values.occurrences);
        case HdrLightingField::Ambient: return bytes(values.ambient);
        case HdrLightingField::DirectionalCount: return bytes(values.directionalCountMinusOne);
        case HdrLightingField::Fog: return bytes(values.fog.fog);
        case HdrLightingField::FogColor: return bytes(values.fog.color);
        case HdrLightingField::Scattering: return bytes(values.fog.scat);
        case HdrLightingField::AmbientMapsRate: return bytes(values.ambientMapsRate);
        case HdrLightingField::AmbientDiffuseScale: return bytes(values.ambientDiffuseScale);
        case HdrLightingField::AmbientSpecularScale: return bytes(values.ambientSpecularScale);
        case HdrLightingField::OcclusionRates:
            if (!values.occlusionRates) throw std::runtime_error("HDR current occlusion rates are unresolved");
            return bytes(*values.occlusionRates);
    }
    throw std::runtime_error("HDR lighting field is not supported");
}
struct HdrLightingWrite {
    HdrLightingField field{};
    std::uint32_t destination{}, source{}, bytes{};
};
struct HdrLightingLayout {
    std::uint32_t slot{}, constantBytes{};
    std::vector<HdrLightingWrite> writes;
};
inline void writeHdrLighting(const HdrLightingLayout& layout, const HdrLightingValues& values,
    std::span<char> constant) {
    if (constant.size() != layout.constantBytes) throw std::runtime_error("Current HDR constant extent changed");
    for (const auto& write : layout.writes) {
        const auto source = hdrLightingField(values, write.field);
        std::memcpy(constant.data() + write.destination, source.data() + write.source, write.bytes);
    }
}
} // namespace San14::Render
