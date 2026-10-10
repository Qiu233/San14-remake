#pragma once
#include "MapViewPopulation.h"
#include "../../Passes/Environment/SeasonFrame.h"
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

// One loaded View owns its working population and published frame. Loading a
// new View replaces this owner; population updates do not reset its lifecycle.
class PreparedMapView {
public:
    explicit PreparedMapView(ImportedMapView imported)
        :definition_(std::move(imported.definition)),camera_(imported.camera),
         initialReceiveShadowMode_(imported.initialReceiveShadowMode){}
    PreparedMapView(const PreparedMapView&)=delete;
    PreparedMapView& operator=(const PreparedMapView&)=delete;

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
    void publishPopulation(MapViewPopulation population,
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
        frame_=prepareMapViewFrame(definition_,camera_,population);
    }
    const MapViewDefinition& definition()const { return definition_; }
    const MapViewCameraSettings& cameraSettings()const { return camera_; }
    const MapViewFrame& frame()const { return frame_; }
private:
    const MapViewDefinition definition_;
    MapViewCameraSettings camera_;
    const std::uint32_t initialReceiveShadowMode_;
    MapViewFrame frame_;
    std::vector<unsigned> ordinaryCounts_;
    unsigned accessoryCount_{};
};
} // namespace San14::Render
