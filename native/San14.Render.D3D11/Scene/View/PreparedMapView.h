#pragma once
#include "MapViewPopulation.h"
#include "HdrViewInputs.h"
#include "PreparedViewBaseLight.h"
#include "../../Passes/Environment/SeasonFrame.h"
#include "../NamedMapRenderer.h"
#include <optional>
#include <utility>

namespace San14::Render {
struct MapViewShadowSelection {
    const source_current_season::Frame& world;
    int lightGroup;
    float planeScale;
    std::uint32_t lightMask;
    std::optional<source_current_season_shadow::CompactMode> compactMode;
};

// Working counters and applied camera settings survive individual preparation
// attempts. Successful preparation returns a new frame for a View generation.
class MapViewPreparation {
public:
    explicit MapViewPreparation(ImportedMapView imported)
        :definition_(std::move(imported.definition)),camera_(imported.camera),
         initialReceiveShadowMode_(imported.initialReceiveShadowMode){}
    MapViewPreparation(const MapViewPreparation&)=delete;
    MapViewPreparation& operator=(const MapViewPreparation&)=delete;

    void beginPopulation(const std::vector<unsigned>& shadowCounts) { ordinaryCounts_=shadowCounts; }
    template<class Append> void withArmyPopulation(Append&& append) {
        append(definition_,ordinaryCounts_,accessoryCount_);
    }
    void requirePrimaryRecordCount(std::size_t count)const {
        if(ordinaryCounts_.size()!=count)throw std::runtime_error("Field/City ordinary record identities differ");
    }
    template<class Count> void addGroundDetails(bool populationChanged,Count&& count) {
        if(populationChanged)
            for(std::size_t record=0;record<definition_.records.size();++record)
                ordinaryCounts_[record]+=count(definition_.records[record].groupMask);
    }
    MapViewFrame preparePopulation(MapViewPopulation population,
        const std::optional<MapViewShadowSelection>& shadow=std::nullopt) {
        population.ordinaryCounts=ordinaryCounts_;
        population.selectedReceiveShadowMode=initialReceiveShadowMode_;
        if(shadow) {
            camera_.shadow.lightGroup=shadow->lightGroup;
            camera_.shadow.planeScale=shadow->planeScale;
            if(!shadow->compactMode)
                throw std::runtime_error("Selected seasonal shadow requires a missing imported setting");
            camera_.shadow.cascadeMode=static_cast<std::uint32_t>(*shadow->compactMode);
            population.selectedReceiveShadowMode=0;
            if(camera_.shadow.lightGroup>0) {
                // ShadowPass admitted this same current ordered World selection.
                const auto& selected=*source_current_season::firstMatching(shadow->world,shadow->lightMask);
                population.selectedReceiveShadowMode=selected.shadowModes.receive;
            }
        }
        // A failed preparation leaves the preceding publication intact. Working
        // counts and the already applied camera prefix retain their writes.
        return prepareMapViewFrame(definition_,camera_,population);
    }
    const MapViewDefinition& definition()const { return definition_; }
    const MapViewCameraSettings& cameraSettings()const { return camera_; }
    const PreparedViewLocalLightQueries& localLightQueries() const { return localLightQueries_; }
    HdrPreRendererState preRenderer() const { return preRenderer_; }
private:
    const MapViewDefinition definition_;
    MapViewCameraSettings camera_;
    const std::uint32_t initialReceiveShadowMode_;
    std::vector<unsigned> ordinaryCounts_;
    unsigned accessoryCount_{};
    // The normal constructor owns fresh empty group pools. Generations capture
    // these retained states and do not reset them during population preparation.
    PreparedViewLocalLightQueries localLightQueries_{LocalLightQuery::absent(), LocalLightQuery::absent()};
    HdrPreRendererState preRenderer_{HdrPreRendererState::Absent};
};
enum class HdrAmbientMapVariant : int { Unsupported = -1, First, Second, Third };
struct HdrAmbientSelection {
    std::array<HdrAmbientMapVariant, 3> layers;
};

// This is one successful normal View preparation, separate from the retained
// working domain. Base-light association is captured from the same accepted
// World independently of the later HDR ambient classification publication.
class PreparedMapView {
public:
    PreparedMapView(std::uint32_t slot, MapViewFrame frame,
        std::array<std::uint32_t, 3> heightFogModes, PreparedHdrViewInputs hdr,
        PreparedViewBaseLight baseLight)
        : slot_(slot), frame_(std::move(frame)), heightFogModes_(heightFogModes),
          hdr_(std::move(hdr)), baseLight_(std::move(baseLight)) {}
    PreparedMapView(const PreparedMapView&) = delete;
    PreparedMapView& operator=(const PreparedMapView&) = delete;

    const MapViewFrame& frame() const { return frame_; }
    std::uint32_t slot() const { return slot_; }
    const PreparedHdrViewInputs& hdrInputs() const { return hdr_; }
    const PreparedViewBaseLight& baseLight() const { return baseLight_; }
    const std::optional<HdrAmbientSelection>& ambientSelection() const { return ambient_; }

    bool publishAmbientSelection(NamedMapRenderer* renderer) {
        if (ambient_) return false;
        if (!renderer || renderer->viewSlot() != slot_) return false;
        renderer->prepareAmbientDimensions();
        HdrAmbientSelection selection;
        for (std::size_t i = 0; i < heightFogModes_.size(); ++i) {
            const auto mode = heightFogModes_[i] & 0x7fffffffu;
            selection.layers[i] = mode >= 1 && mode <= 3
                ? static_cast<HdrAmbientMapVariant>(mode - 1)
                : HdrAmbientMapVariant::Unsupported;
        }
        ambient_ = selection;
        return true;
    }

private:
    const std::uint32_t slot_;
    const MapViewFrame frame_;
    const std::array<std::uint32_t, 3> heightFogModes_;
    const PreparedHdrViewInputs hdr_;
    const PreparedViewBaseLight baseLight_;
    std::optional<HdrAmbientSelection> ambient_;
};
} // namespace San14::Render
