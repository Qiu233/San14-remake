#pragma once
#include "HdrProgramBank.h"
#include "../../Scene/View/PreparedMapView.h"

namespace San14::Render {
struct HdrOrderedAssociation { std::size_t outer{}, inner{}; std::uint32_t innerGroup{}; };
inline std::vector<HdrOrderedAssociation> prepareHdrAssociations(const HdrProgramBank& bank,
    const MapViewCommonSettings& common, const PreparedMapView& view,
    const source_current_season::Frame& current, bool ocean) {
    if (bank.viewSlot() != view.slot()) throw std::runtime_error("HDR bank belongs to another prepared View");
    auto group = bank.declaredLightGroup();
    if (common.present && common.outerLightGroupOverride != 0) group = common.outerLightGroupOverride;
    const auto exclusion = ~(1u << ((group - 1) & 31));
    const auto enabled = ocean ? view.frame().oceanGroups : view.frame().activeGroups & 0x9fffffffu;
    std::vector<HdrOrderedAssociation> result;
    for (std::size_t outer = 0; outer < current.lights.size(); ++outer) {
        if (current.lights[outer].mask & exclusion) continue;
        for (std::uint32_t bit = 1; bit <= 8; bit <<= 1) {
            if ((enabled & bit) == 0) continue;
            for (std::size_t inner = 0; inner < current.lights.size(); ++inner)
                if (current.lights[inner].mask == bit) result.push_back({outer, inner, bit});
        }
    }
    return result;
}
inline std::optional<HdrColorMode> prepareHdrColorMode(std::uint32_t header,
    bool ocean, std::uint32_t context) {
    if ((header & 0x14) == 0x14) {
        if (header & 0x80) return (context & 0x08000000) ? std::optional(HdrColorMode(0x30)) : std::nullopt;
        if (header & 0x40) return (context & 0x10000000) ? std::optional(HdrColorMode(0x20)) : std::nullopt;
        return (context & 0x20000000) ? std::optional(HdrColorMode(0x10)) : std::nullopt;
    }
    if (header & 0x200) return (context & 0x04000000) ? std::optional(HdrColorMode(0x40)) : std::nullopt;
    return (header & 4) ? (ocean ? HdrColorMode::Ocean : HdrColorMode::CameraSpecular) : HdrColorMode::Default;
}
inline HdrSelectionSettings prepareHdrSelectionSettings(const HdrCurrentSelectionSettings& current,
    const PreparedMapView& view, const MapViewCommonSettings& common,
    std::uint32_t context, HdrColorMode color) {
    if (current.cameraReflectionDeclaration != 0)
        throw std::runtime_error("HDR nonzero Camera reflection provider is not implemented");
    const auto& inputs = view.hdrInputs();
    if (inputs.preRenderer != HdrPreRendererState::Absent)
        throw std::runtime_error("HDR nonempty or unresolved pre-renderer is not implemented");
    const auto& ambient = view.ambientSelection();
    if (!ambient) throw std::runtime_error("HDR ambient selection has not been published for this View");
    const bool hasRefractionResources = (common.featureFlags & 0x200) != 0 &&
        std::any_of(ambient->layers.begin(), ambient->layers.end(),
            [](auto layer) { return layer != HdrAmbientMapVariant::Unsupported; });
    return {color, current.cameraSpecularOcclusion,
        {(context & 0x4000) == 0, (context & 0x1800) == 0, (context & 0xc0000) != 0,
         (context & 0x8000) == 0, (context & 0x10000) == 0, (context & 0x20000) == 0,
         (context & 0x100000) != 0, (context & 0x40000000) == 0},
        false, hasRefractionResources, inputs.localLights.areEmpty(), false};
}
struct HdrFeatureScratch {
    source_weather_fog::FogState fog;
    std::uint32_t directions{}, hemispheres{};
    bool shadowProvider{};
};
struct HdrCurrentRoute {
    HdrOrderedAssociation association;
    HdrFeatureKey feature;
    HdrLightingValues lighting;
};
inline HdrCurrentRoute prepareHdrCurrentRoute(const source_current_season::Frame& current,
    const PreparedMapView& view, const MapViewCameraSettings& camera,
    HdrOrderedAssociation association, HdrColorMode color, std::optional<HdrFeatureScratch>& previous) {
    if (!current.hdrLighting) throw std::runtime_error("HDR current lighting publication is absent");
    auto outer = current.lights[association.outer].lighting;
    const auto& inner = current.lights[association.inner].lighting;
    if (!current.hdrSelection) throw std::runtime_error("HDR current Common mode is absent");
    const auto effectMode=current.hdrSelection->commonEffectMode;
    if (effectMode>=2&&effectMode<=5) {
        if (color!=HdrColorMode::CameraSpecular) outer.fog.mode=source_weather_fog::FogMode::Inactive;
        else if (outer.fog.mode==source_weather_fog::FogMode::Inactive)
            throw std::runtime_error("HDR Common mode requires its unimplemented shader fog query producer");
    }
    if (!outer.modifier.present && !inner.modifier.present)
        throw std::runtime_error("HDR null built-in modifier scratch producer is not implemented");
    auto lighting = prepareHdrLighting(outer, inner, *current.hdrLighting);
    const auto directions = lighting.directionalCountMinusOne + 1;
    const auto hemi = positiveHdrRgb(lighting.hemisphere.upperDiffuse) ||
        positiveHdrRgb(lighting.hemisphere.lowerDiffuse) ? 1u : 0u;
    const auto receive = current.lights[association.outer].shadowModes.receive;
    const bool activeShadow = camera.shadow.lightGroup > 0 && (view.frame().activeGroups & 0x40000000) != 0 && receive != 0;
    std::uint32_t flags = 0;
    if (activeShadow) {
        if (!view.hdrInputs().compactShadowProvider)
            throw std::runtime_error("HDR active shadow has no actual compact CPU provider");
        auto type = std::uint32_t(receive);
        if (type == 0x6c) { type = 8; flags |= 0x4000; }
        if ((type == 8 || type == 9) && camera.shadow.planeScale <= 0) type = 1;
        if (type <= 3) flags |= 0x100;
        flags |= 0x400;
    }
    const bool rebuild = !previous || previous->shadowProvider ||
        std::uint32_t(previous->fog.mode) != 0 || std::uint32_t(outer.fog.mode) != 0 || activeShadow ||
        previous->directions != directions || previous->hemispheres != hemi;
    if (!rebuild) {
        outer.fog = previous->fog;
        lighting = prepareHdrLighting(outer, inner, *current.hdrLighting);
    }
    previous = HdrFeatureScratch{outer.fog, directions, hemi,
        activeShadow && view.hdrInputs().compactShadowProvider != nullptr};
    const auto mode = std::uint32_t(outer.fog.mode);
    return {association, {flags, directions, 0, 0, hemi, directions ? 1u : 0u,
        mode, mode == 7 ? 2u : 1u, 0, 0, 0, 3}, std::move(lighting)};
}
} // namespace San14::Render
