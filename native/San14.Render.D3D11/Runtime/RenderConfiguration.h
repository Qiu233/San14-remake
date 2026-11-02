#pragma once
#include <cstdint>
#include <filesystem>
#include <stdexcept>
#include <string>
#include <utility>
#include "PreparedFileInput.h"

namespace San14::Render {
// Subdirectories are a prepared-resource format contract. Keep that disk
// format at the import boundary; passes address resources by purpose.
enum class ResourceGroup {
    NamedRenderer,
    Models,
    ShadowCasters,
    AmbientClouds,
    Ambient,
    SkyPattern,
    SkyAtmosphere,
    AnimationTiming,
    ArmyActions,
    MaterialResourceContext,
    Armies,
    AtmosphereCamera,
    BaseVisuals,
    Camera,
    CameraMotion,
    BaseAccessories,
    BaseShadows,
    AmbientFrame,
    CloudWeather,
    SeasonalResources,
    AnimationDefaults,
    HdrProgramBank,
    DistantMap,
    FogAcceleration,
    FrameClock,
    GroundDetails,
    SeasonalGroundDetails,
    Lighting,
    CameraInitialState,
    CloudAnimation,
    Clouds,
    Sky,
    MapExposure,
    ShadowReceivers,
    ShadowReceiverBank,
    SdfConstruction,
    PostCamera,
    PostAnimation,
    FinalOutput,
    PostEffects,
    OceanTextures,
    ForwardLighting,
    Refraction,
    ShadowRaster,
    ContactShadows,
    Territory,
    TerritoryZones,
    View,
    WaterCamera,
    Water,
    Weather,
    Atmosphere,
};

class PreparedResourceDirectories {
public:
    explicit PreparedResourceDirectories(std::filesystem::path terrain)
        : terrainDirectory(std::move(terrain)), packageRoot(terrainDirectory.parent_path()),
          environmentRoot(packageRoot) {
        const auto reference=packageRoot/"environment-static-root.txt";
        if(std::filesystem::exists(reference)) {
            NativePreparedInputFile input(reference);
            std::string directory;
            if(!std::getline(input,directory)||directory.empty())
                throw std::runtime_error("Static environment resource reference is absent or malformed");
            if(directory.back()=='\r')directory.pop_back();
            environmentRoot=std::filesystem::u8path(directory);
            if(!environmentRoot.is_absolute())
                throw std::runtime_error("Static environment resource reference requires an absolute generation directory");
            environmentRoot=nativePreparedFilePath(std::move(environmentRoot));
        }
    }
    const std::filesystem::path& terrain() const noexcept { return terrainDirectory; }
    const std::filesystem::path& root() const noexcept { return packageRoot; }
    std::filesystem::path directory(ResourceGroup group) const {
        switch(group) {
        case ResourceGroup::NamedRenderer: return packageRoot/"named-renderer-input";
        case ResourceGroup::Models: return packageRoot/"model-continuous-input";
        case ResourceGroup::ShadowCasters: return packageRoot/"shadow-caster-input";
        case ResourceGroup::AmbientClouds: return packageRoot/"ambient-cloud-continuous-input";
        case ResourceGroup::Ambient: return packageRoot/"ambient-input";
        case ResourceGroup::SkyPattern: return environmentRoot/"pattern-input";
        case ResourceGroup::SkyAtmosphere: return packageRoot/"atmosphere-input";
        case ResourceGroup::AnimationTiming: return packageRoot/"animation-cadence-input";
        case ResourceGroup::ArmyActions: return packageRoot/"army-graphics-action-input";
        case ResourceGroup::MaterialResourceContext: return packageRoot/"resource-context-input";
        case ResourceGroup::Armies: return packageRoot/"army-visual-input";
        case ResourceGroup::AtmosphereCamera: return packageRoot/"atmosphere-camera-continuous-input";
        case ResourceGroup::BaseVisuals: return packageRoot/"base-visual-world-input";
        case ResourceGroup::Camera: return packageRoot/"camera-continuous-input";
        case ResourceGroup::CameraMotion: return packageRoot/"camera-motion-input";
        case ResourceGroup::BaseAccessories: return packageRoot/"city-accessory-input";
        case ResourceGroup::BaseShadows: return packageRoot/"city-gate-shadow-input";
        case ResourceGroup::AmbientFrame: return packageRoot/"current-ambient-frame-input";
        case ResourceGroup::CloudWeather: return packageRoot/"current-cloud-weather-input";
        case ResourceGroup::SeasonalResources: return packageRoot/"current-season-resource-input";
        case ResourceGroup::AnimationDefaults: return packageRoot/"default-player-phase-input";
        case ResourceGroup::HdrProgramBank: return packageRoot/"hdr-bank";
        case ResourceGroup::DistantMap: return packageRoot/"far-main-source-input";
        case ResourceGroup::FogAcceleration: return packageRoot/"fog-accel-input";
        case ResourceGroup::FrameClock: return packageRoot/"frame-clock-input";
        case ResourceGroup::GroundDetails: return packageRoot/"ground-detail-input";
        case ResourceGroup::SeasonalGroundDetails: return packageRoot/"ground-detail-season-source-input";
        case ResourceGroup::Lighting: return packageRoot/"lighting-input";
        case ResourceGroup::CameraInitialState: return packageRoot/"main-camera-initial-input";
        case ResourceGroup::CloudAnimation: return packageRoot/"main-cloud-continuous-input";
        case ResourceGroup::Clouds: return packageRoot/"main-cloud-input";
        case ResourceGroup::Sky: return packageRoot/"main-sky-input";
        case ResourceGroup::MapExposure: return packageRoot/"map-exposure-input";
        case ResourceGroup::ShadowReceivers: return packageRoot/"normal-pattern-receiver-input";
        case ResourceGroup::ShadowReceiverBank: return environmentRoot/"normal-pattern-receiver-input";
        case ResourceGroup::SdfConstruction: return environmentRoot/"sdf-source-input";
        case ResourceGroup::PostCamera: return packageRoot/"normal-post-camera-input";
        case ResourceGroup::PostAnimation: return packageRoot/"normal-post-continuous-input";
        case ResourceGroup::FinalOutput: return packageRoot/"normal-post-input";
        case ResourceGroup::PostEffects: return packageRoot/"normal-post-next-input";
        case ResourceGroup::OceanTextures: return packageRoot/"ocean-texture-input";
        case ResourceGroup::ForwardLighting: return packageRoot/"primary-forward-current-bank-input";
        case ResourceGroup::Refraction: return packageRoot/"refraction-source-input";
        case ResourceGroup::ShadowRaster: return packageRoot/"shadow-raster-source-input";
        case ResourceGroup::ContactShadows: return environmentRoot/"sub-shadow-input";
        case ResourceGroup::Territory: return packageRoot/"territory-visual-input";
        case ResourceGroup::TerritoryZones: return packageRoot/"territory-zone-source-input";
        case ResourceGroup::View: return packageRoot/"view-continuous-input";
        case ResourceGroup::WaterCamera: return packageRoot/"water-camera-continuous-input";
        case ResourceGroup::Water: return packageRoot/"water-input";
        case ResourceGroup::Weather: return packageRoot/"weather-fog-camera-input";
        case ResourceGroup::Atmosphere: return packageRoot/"ssfog-population-input";
        }
        throw std::invalid_argument("Unknown prepared resource group");
    }
private:
    std::filesystem::path terrainDirectory;
    std::filesystem::path packageRoot;
    std::filesystem::path environmentRoot;
};

struct WorldVisuals {
    bool accessories{};
    bool armies{};
    static WorldVisuals fromInteropFlags(std::uint32_t flags) noexcept {
        const bool armies=(flags & 2u)!=0;
        return {armies || (flags & 1u)!=0, armies};
    }
};

struct RenderConfiguration {
    PreparedResourceDirectories resources;
    unsigned width;
    unsigned height;
    WorldVisuals world;
};
}
