#pragma once
#include "MapViewSettings.h"
#include <array>
#include <stdexcept>

namespace San14::Render {
struct MapViewPopulation {
    std::vector<unsigned> ordinaryCounts;
    std::array<unsigned,4> shadowCasterCounts{};
    unsigned subShadowPrimitiveCount{}, selectedReceiveShadowMode{};
    bool terrainVisible{}, compactShadowAvailable{}, directionalDiffuseAvailable{};
};
struct MapViewFrame {
    std::vector<unsigned> ordinaryCounts;
    unsigned activeGroups{}, oceanGroups{}, rainGroups{}, subShadowPrimitiveCount{};
    bool shadowEnabled{}, compactShadowAvailable{}, subShadowUnavailable{};
    bool geometryReceiverEligible{}, waterReceiverEligible{};
};
inline unsigned mapViewWeatherGroup(unsigned group) {
    return group>=1&&group<=7 ? 1u<<(group-1) : 0;
}
inline MapViewFrame prepareMapViewFrame(const MapViewDefinition& definition,
    const MapViewCameraSettings& camera,const MapViewPopulation& population) {
    if(population.ordinaryCounts.size()!=definition.records.size())
        throw std::runtime_error("Current View needs ordered complete owned counters");
    MapViewFrame frame;
    frame.ordinaryCounts=population.ordinaryCounts;
    frame.subShadowPrimitiveCount=population.subShadowPrimitiveCount;
    for(std::size_t i=0;i<definition.records.size();++i) {
        const auto& record=definition.records[i];
        const auto count=population.ordinaryCounts[i];
        if(count&&count>=record.primitiveCapacity)
            throw std::runtime_error("Native first-counter append capacity");
        if(count)frame.activeGroups|=record.groupMask;
        if(population.terrainVisible&&(definition.terrainExcludedGroups&record.groupMask)==0)
            frame.activeGroups|=record.groupMask;
        frame.oceanGroups|=mapViewWeatherGroup(camera.oceanGroup)&record.groupMask;
        frame.rainGroups|=mapViewWeatherGroup(camera.rainGroup)&record.groupMask;
    }
    frame.activeGroups|=frame.oceanGroups|frame.rainGroups;
    bool hasShadowCasters=population.subShadowPrimitiveCount!=0;
    for(unsigned count:population.shadowCasterCounts)hasShadowCasters|=count!=0;
    constexpr unsigned shadowCasterGroup=0x40000000u;
    if(hasShadowCasters)frame.activeGroups|=shadowCasterGroup;
    frame.shadowEnabled=camera.shadow.lightGroup>0&&(frame.activeGroups&shadowCasterGroup)&&population.selectedReceiveShadowMode!=0;
    frame.compactShadowAvailable=frame.shadowEnabled&&population.compactShadowAvailable;
    frame.subShadowUnavailable=!population.directionalDiffuseAvailable||
        (population.subShadowPrimitiveCount&15)==0||!frame.compactShadowAvailable;
    frame.geometryReceiverEligible=(frame.activeGroups&0x9FFFFFFFu)!=0;
    frame.waterReceiverEligible=frame.geometryReceiverEligible&&frame.oceanGroups!=0;
    return frame;
}
} // namespace San14::Render
