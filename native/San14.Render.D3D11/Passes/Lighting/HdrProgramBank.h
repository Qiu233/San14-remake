#pragma once
#include "HdrLightingState.h"
#include <algorithm>
#include <limits>
#include <optional>

namespace San14::Render {
struct HdrFeatureKey {
    std::uint32_t flags{}, directionalLights{}, pointLights{}, spotLights{}, hemisphereLights{};
    std::uint32_t directionalMode{}, fogMode{}, fogQuality{}, shadowMode{}, shadowQuality{};
    std::uint32_t localShadowMode{}, materialMode{};
};
struct HdrVariantKey {
    std::array<std::uint32_t, 24> words{};
    bool operator==(const HdrVariantKey&) const = default;
};
enum class HdrVariantField : std::size_t {
    SpecularOcclusion = 1, SharedTargets, FirstLighting, SecondLighting, SharedAuxiliary,
    LightPair = 9, LightPairAuxiliary, CombinedLighting, LocalLightQueriesEmpty,
    VolumetricFog, RefractionResources, Reflection, RefractionSource, SharedDistance
};
enum class HdrColorMode : std::uint32_t { Default = 0, Ocean = 0x50, CameraSpecular = 0x60 };
struct HdrContextSelection {
    bool sharedTargets{}, lightPair{}, combinedLighting{}, firstLighting{}, secondLighting{};
    bool volumetricFog{}, extendedReflection{}, refractionSource{};
};
struct HdrSelectionSettings {
    HdrColorMode colorMode{};
    std::uint32_t cameraSpecularOcclusion{};
    HdrContextSelection context;
    bool reflectionAvailable{}, refractionResourcesAvailable{}, localLightQueriesEmpty{}, preRendererPresent{};
};
struct HdrCurrentSelectionSettings {
    std::uint32_t cameraSpecularOcclusion{}, cameraReflectionDeclaration{}, commonEffectMode{}, specularSourceIndex{};
};
struct HdrShaderPlugins { bool ocean{}, refraction{}, thinSpecularSource{}, volumetricFog{}, caustics{}; };
struct HdrProgramReference { std::uint32_t combination{}, vertex{}, pixel{}; };
struct HdrProgramSelection {
    std::size_t featureIndex{}, variantIndex{};
    HdrVariantKey variant;
    HdrProgramReference program;
};

// These are imported resource keys. Current feature production uses named
// values and never reads a cached selected program to recover its request.
class HdrProgramBank {
public:
    HdrProgramBank(std::uint32_t node, std::uint32_t viewSlot, std::uint32_t lightGroup,
        HdrShaderPlugins plugins, std::vector<HdrFeatureKey> features,
        std::vector<HdrVariantKey> variants, std::vector<HdrProgramReference> programs)
        : node_(node), viewSlot_(viewSlot), lightGroup_(lightGroup), plugins_(plugins),
          features_(std::move(features)), variants_(std::move(variants)), programs_(std::move(programs)) {
        if (features_.empty() || variants_.empty() || programs_.size() != features_.size() * variants_.size())
            throw std::runtime_error("HDR program bank dimensions");
        for (const auto& key : variants_)
            for (std::size_t field = 0; field < capabilities_.size(); ++field)
                capabilities_[field] |= 1u << (key.words[field] & 31);
    }
    std::uint32_t node() const { return node_; }
    std::uint32_t viewSlot() const { return viewSlot_; }
    std::uint32_t declaredLightGroup() const { return lightGroup_; }
    std::size_t variantCount() const { return variants_.size(); }
    bool ocean() const { return plugins_.ocean; }
    bool caustics() const { return plugins_.caustics; }
    const std::vector<HdrProgramReference>& programs() const { return programs_; }
    HdrProgramSelection select(const HdrFeatureKey& feature, const HdrSelectionSettings& settings) const {
        const auto variant = prepareVariant(settings);
        const auto found = std::find(variants_.begin(), variants_.end(), variant);
        if (found == variants_.end()) throw std::runtime_error("HDR current variant has no resource match");
        const auto row = selectFeature(feature);
        const auto column = std::size_t(std::distance(variants_.begin(), found));
        return {row, column, variant, programs_[row * variants_.size() + column]};
    }
private:
    HdrVariantKey prepareVariant(const HdrSelectionSettings& settings) const {
        auto key = variants_.front();
        auto set = [&](HdrVariantField field, std::uint32_t value) {
            const auto index = static_cast<std::size_t>(field);
            if (capabilities_[index] & (1u << (value & 31))) key.words[index] = value;
        };
        const auto& c = settings.context;
        if (!plugins_.ocean && plugins_.refraction && !plugins_.thinSpecularSource)
            set(HdrVariantField::RefractionSource, c.refractionSource);
        const bool specular = !plugins_.ocean && (settings.colorMode == HdrColorMode::CameraSpecular
            ? settings.cameraSpecularOcclusion == 1 : settings.cameraSpecularOcclusion != 0);
        set(HdrVariantField::SpecularOcclusion, specular);
        set(HdrVariantField::SharedTargets, plugins_.ocean ? !settings.preRendererPresent : c.sharedTargets);
        if (!plugins_.ocean) {
            set(HdrVariantField::SharedAuxiliary, c.sharedTargets);
            set(HdrVariantField::SharedDistance, c.sharedTargets);
        }
        set(HdrVariantField::LightPair, c.lightPair);
        set(HdrVariantField::LightPairAuxiliary, c.lightPair);
        set(HdrVariantField::CombinedLighting, c.combinedLighting ? 3u : 0u);
        set(HdrVariantField::FirstLighting, c.firstLighting);
        set(HdrVariantField::SecondLighting, c.secondLighting);
        set(HdrVariantField::LocalLightQueriesEmpty, settings.localLightQueriesEmpty);
        if (plugins_.volumetricFog && c.volumetricFog) set(HdrVariantField::VolumetricFog, 1);
        const bool extended = c.extendedReflection && settings.colorMode != HdrColorMode::CameraSpecular;
        set(HdrVariantField::Reflection, extended ? 2u : settings.reflectionAvailable ? 0u : 1u);
        if (!plugins_.ocean && plugins_.refraction)
            set(HdrVariantField::RefractionResources, !settings.refractionResourcesAvailable);
        return key;
    }
    static std::int32_t difference(std::uint32_t left, std::uint32_t right) {
        const auto value = left - right;
        return static_cast<std::int32_t>((value & 0x80000000u) ? 0u - value : value);
    }
    static std::pair<std::int32_t, std::int32_t> score(const HdrFeatureKey& c, const HdrFeatureKey& r) {
        const auto request = c.flags & 0xF77FFu;
        const auto missing = request - (request & r.flags);
        std::uint32_t penalty = ((missing & ~0x80u) << 16) + ((missing >> 7) & 1);
        std::int32_t excess = (r.flags & ~request) != 0 ? 1 : 0;
        const std::array counts{c.directionalLights, c.pointLights, c.spotLights, c.hemisphereLights};
        const std::array available{r.directionalLights, r.pointLights, r.spotLights, r.hemisphereLights};
        for (std::size_t i = 0; i < counts.size(); ++i) {
            if (available[i] < counts[i]) penalty += (counts[i] - available[i]) << 8;
            else if (r.flags & (1u << i)) excess += std::int32_t(available[i] - counts[i]);
            else penalty += (available[i] - counts[i]) << 4;
        }
        if (c.fogMode == 0) excess += difference(r.fogMode, 0);
        else {
            if (r.fogMode != 5 || c.fogMode == 4 || c.fogMode == 7) penalty += difference(r.fogMode, c.fogMode);
            penalty += difference(r.fogQuality, c.fogQuality);
        }
        penalty += std::uint32_t(difference(c.directionalMode, r.directionalMode)) << 12;
        if ((r.flags & 0x100) == 0) penalty += difference(c.shadowMode, r.shadowMode);
        else if (std::int32_t(r.shadowMode) >= 7 && std::int32_t(c.shadowMode) >= 7 && r.shadowMode != c.shadowMode)
            penalty += difference(c.shadowMode, r.shadowMode);
        else if (std::int32_t(r.shadowMode) < std::int32_t(c.shadowMode) || ((c.shadowMode & 4) && std::int32_t(r.shadowMode) > 7))
            penalty += c.shadowMode - r.shadowMode;
        else excess += difference(c.shadowMode, r.shadowMode);
        if (c.shadowMode != 0) {
            penalty += std::uint32_t(difference(c.shadowQuality, r.shadowQuality)) << 12;
            if (r.localShadowMode != 5 || c.localShadowMode == 2)
                penalty += std::uint32_t(difference(c.localShadowMode, r.localShadowMode)) << 8;
        }
        return {static_cast<std::int32_t>(penalty), excess};
    }
    std::size_t selectFeature(const HdrFeatureKey& current) const {
        std::size_t best = 0;
        auto penalty = std::numeric_limits<std::int32_t>::max();
        auto excess = std::numeric_limits<std::int32_t>::max();
        for (std::size_t i = 0; i < features_.size(); ++i) {
            const auto [p, e] = score(current, features_[i]);
            if (p == 0) {
                if (e < excess) { excess = e; best = i; }
                if (e == 0) break;
                penalty = 0;
            } else if (p < penalty) { penalty = p; best = i; }
        }
        return best;
    }
    std::uint32_t node_, viewSlot_, lightGroup_;
    HdrShaderPlugins plugins_;
    std::vector<HdrFeatureKey> features_;
    std::vector<HdrVariantKey> variants_;
    std::vector<HdrProgramReference> programs_;
    std::array<std::uint32_t, 24> capabilities_{};
};
} // namespace San14::Render
