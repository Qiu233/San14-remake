#pragma once
#include <cstdint>
#include <vector>

namespace San14::Render {
struct MapViewRecord {
    std::uint32_t groupMask{}, primitiveCapacity{};
    bool usesOrdinaryCounter{};
};
struct MapViewCommonSettings {
    bool present{};
    std::uint32_t featureFlags{}, excludedCategories{}, outerLightGroupOverride{};
};
struct MapViewShadowSettings {
    int lightGroup{};
    float planeScale{};
    std::uint32_t cascadeMode{};
};
struct MapViewCameraSettings {
    MapViewShadowSettings shadow;
    std::uint32_t oceanGroup{}, rainGroup{};
};
enum class ModelGroupSign { Nonnegative, Negative };
struct ModelStencilSettings {
    ModelGroupSign selectedSign{ModelGroupSign::Nonnegative};
    std::uint32_t selectorGroup{};
    bool referencePrefix{}, secondaryReferencePrefix{}, enabled{};
};
// Imported once; camera acceptance cannot mutate model/terrain declarations.
struct MapViewDefinition {
    std::vector<MapViewRecord> records;
    MapViewCommonSettings common;
    std::uint32_t terrainExcludedGroups{};
    ModelStencilSettings modelStencil;
};
struct ImportedMapView {
    MapViewDefinition definition;
    MapViewCameraSettings camera;
    std::uint32_t initialReceiveShadowMode{};
};
} // namespace San14::Render
