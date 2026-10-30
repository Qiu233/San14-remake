#pragma once
#include <array>
#include <atomic>
#include <cstdint>
#include <memory>
#include <optional>
#include <span>
#include <stdexcept>
#include <string>
#include <vector>

struct NativeModelDraw;
struct ID3D11ShaderResourceView;
namespace San14::Render {class PreparedMapView;}
namespace San14::Render::Reflection {
using Float3=std::array<float,3>;
using Float4=std::array<float,4>;
using Matrix=std::array<float,16>;
enum class ModelFamily : std::uint32_t {Static=0,Primary=1,Army=2};
struct FieldDefinition {
    std::uint32_t rootIndex{},objectId{};
    std::array<std::uint32_t,3> dimensions{};
    float cellSize{};
    std::uint32_t tileSize{},gBufSize{};
    float viewRange{};
    std::uint32_t groupMask{},maxPrimitive{},probeShadowMapSize{};
    Float3 origin{};
    Float4 quaternion{};
};
struct PrimitiveReference {
    ModelFamily family{};
    std::uint32_t modelIndex{},sourcePrimitiveIndex{},sourceDrawIndex{},templateIndex{};
    std::uint64_t tableOwner{};
    const NativeModelDraw* geometry{};
    // Actual retained current Main/palette inputs, never relabeled capture CBs.
    // The selected state12 exporter/renderer packs its real capture ABI.
    std::span<const char> currentVertex;
    std::optional<std::uint64_t> actualPriorTupleKey;
};
struct ModelReference {
    ModelFamily family{};
    std::string identity;
    std::array<float,20> bounds{};
    std::uint32_t spatialFlags{1};
    float opacity{};
    bool eligible{};
    bool captureAdmissionKnown{};
    bool incompleteGeometry{};
    std::uint32_t captureViewMask{};
    std::vector<std::uint32_t> primitives;
};
struct CaptureFace {
    std::uint32_t index{};
    bool visible{};
    float nearDistance{},farDistance{};
    Matrix worldToProjection{};
    // Indices into the SAME SceneFrame.primitives vector. Tuples are not
    // cloned for another face; their first-allocation key remains shared.
    std::vector<std::uint32_t> primitiveIndices;
};
struct CaptureShadowInput {
    std::uint32_t fieldObjectId{},sourceOrdinal{},width{},height{};
    ID3D11ShaderResourceView* actualDepth{};
    Matrix worldToShadow{};
};
struct CaptureWork {
    std::uint32_t fieldObjectId{},sourceOrdinal{};
    Float4 position{},topDownEye{};
    std::array<CaptureFace,6> faces;
    std::optional<CaptureShadowInput> shadow;
};
struct FieldState {
    FieldDefinition definition;
    Float4 managerPosition{};
    std::uint32_t requestFlags{},registeredViews{},capturePointerCapacity{};
    std::int32_t currentStart{-1},wrapperNext{};
};
struct FrameLifetime {std::atomic_bool current{true};};
struct SceneFrame {
    std::uint64_t generation{},resourceGeneration{};
    std::uint32_t commonFlags14{},viewSlot{};
    const PreparedMapView* preparedView{};
    std::shared_ptr<FrameLifetime> lifetime;
    std::optional<std::array<Float4,6>> actualClipPlanes;
    std::vector<std::uint32_t> unresolvedModelObjectIds;
    std::vector<ModelReference> models;
    std::vector<PrimitiveReference> primitives;
    std::vector<FieldState> fields;
    // Real root publication is independent of capture/work success.
    std::vector<std::uint32_t> rootFieldObjectIds;
    std::vector<CaptureWork> captureWork;
    void requireCurrent() const {
        if(!lifetime||!lifetime->current.load())throw std::runtime_error("Reflection Scene CPU frame was retired");
    }
    bool hasPublishedRoots() const {requireCurrent();return !rootFieldObjectIds.empty();}
};
} // namespace San14::Render::Reflection
