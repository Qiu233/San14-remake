#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "../../Device/PrimaryForwardMetadata.h"
// CPU-only adapter after city-model-population-native.h. Import/static recipe
// stays owned; each request uses the real current catalog and ordinary plan.
// Shader sources: state0 2F9960EF/8F6C9FB9, matrix0, combo14 VS0/PS11.
#include "../Lighting/HdrLightingState.h"
#include "../Environment/SeasonFrame.h"
#include "../Shadows/ShadowReceiverMaterial.h"
#include "../Shadows/ShadowReceiverResources.h"
#include "../Shadows/PatternTransform.h"
#include "../Environment/SkyPatternPalette.h"
#include "../../Scene/View/MapViewSettings.h"
#include <array>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <string>
#include <vector>
struct NativePrimaryForwardTemplate {
    bool eligible=false;
    std::array<char,480> vertex{};
    std::array<char,1360> pixel{};
};
using NativePrimaryForwardLightingWrite=San14::Render::NativePrimaryForwardNamedWrite;
struct NativePrimaryForwardSource {
    std::vector<NativePrimaryForwardTemplate> templates;
    std::shared_ptr<const San14::Render::NativePrimaryForwardMetadata> metadata;
};
struct NativePrimaryForwardDraw {
    unsigned primitiveIndex{},templateIndex{};
    std::array<char,480> vertex{};
    std::array<char,1360> pixel{};
    // The long-lived Pattern resource owner outlives this per-frame draw list.
    ID3D11ShaderResourceView* litView{};
    ID3D11SamplerState* litSampler{};
};
struct NativePrimaryForwardFrame {std::vector<NativePrimaryForwardDraw> draws;};
inline NativePrimaryForwardSource prepareNativePrimaryForwardSource(const std::filesystem::path& directory) {
    NativePrimaryForwardSource result;
    result.metadata=std::make_shared<const San14::Render::NativePrimaryForwardMetadata>(
        San14::Render::NativePrimaryForwardMetadata::loadSourceLayout(directory));
    result.templates.resize(result.metadata->templates.size());
    for(std::size_t index=0;index<result.templates.size();++index) {
        const auto& source=result.metadata->templates[index];auto& current=result.templates[index];
        current.eligible=source.eligible;if(!current.eligible)continue;
        NativePreparedInputFile vertex(directory/source.directory/"vs-b0.bin",std::ios::binary),
            pixel(directory/source.directory/"ps-b0.bin",std::ios::binary);
        vertex.read(current.vertex.data(),current.vertex.size());pixel.read(current.pixel.data(),current.pixel.size());
        if(!vertex||!pixel)throw std::runtime_error("Original forward complete source CB template absent");
    }
    return result;
}

inline NativePrimaryForwardFrame prepareNativePrimaryForwardCurrent(const NativePrimaryForwardSource& source,
    const NativeCityPopulationSource& currentWorld, const NativeCityPopulationPlan& currentPlan,
    const San14::Render::MapViewDefinition& view, const source_current_season::Frame& current,
    const std::array<float,4>& eye, const San14::Render::ShadowReceiverShadowValues& shadow,
    const San14::Render::SkyPatternPalette& palette,
    const San14::Render::NativeShadowReceiverResources& patternResources,float commonClock) {
    if (!current.hdrLighting)
        throw std::runtime_error("Forward requires current lighting and its real shadow receiver");
    const bool special=palette.atmosphere&&current.shadowReceiver.patternCameraBranch!=5;
    auto group = source.metadata->lightGroup & 0x7fffffffu;
    const bool overridden = view.common.present && view.common.outerLightGroupOverride != 0;
    if (overridden) group = view.common.outerLightGroupOverride;
    else if (source.metadata->lightGroup & 0x80000000u)
        throw std::runtime_error("Forward negative outer-light group requires its unimplemented producer");
    const auto exclusion = ~(1u << ((group - 1) & 31));
    NativePrimaryForwardFrame frame;
    frame.draws.reserve(currentPlan.models.draws.size());
    // One request owns these associations. Lighting, Pattern and its sampled
    // resource depend on the current record/lights, not the primitive. Prepare
    // a record only when a real eligible primitive first admits it; unused
    // records keep the original no-query/no-error path. Nothing survives the
    // request or borrows a mutable light/season publication.
    struct Association {
        San14::Render::HdrLightingValues lighting;
        San14::Render::ShadowReceiverPatternState pattern;
        San14::Render::PatternSampling sampling;
    };
    struct RecordAssociations {bool prepared{};std::vector<Association> values;};
    std::vector<RecordAssociations> records;
    for (const auto& ordinary : currentPlan.models.draws) {
        const auto& primitive = currentWorld.catalog.primitives.at(ordinary.primitiveIndex);
        const auto& retained = source.templates.at(primitive.templateIndex);
        if (!retained.eligible) continue;
        const auto& binding = currentWorld.templates.at(primitive.templateIndex);
        const auto& world = currentWorld.worlds.at(primitive.instanceIndex);
        const auto masks = nativeCityModelCurrentMasks(world,currentWorld.caller);
        bool supplied = false;
        for (std::size_t record = 0; record < view.records.size(); ++record) {
            const auto mask = view.records[record].groupMask;
            if (!nativePrimaryRecordAdmitted(masks,binding.allowedRecords,unsigned(record))) continue;
            if(records.empty())records.resize(view.records.size());
            auto& associations=records[record];
            if(!associations.prepared) {
                for(const auto& outer:current.lights) {
                    if(outer.mask&exclusion)continue;
                    for(const auto& inner:current.lights) {
                        if(inner.mask!=mask)continue;
                        // Forward has its own current light association. It shares
                        // typed numeric preparation, not a selected HDR byte layout.
                        const auto lighting = San14::Render::prepareHdrLighting(outer.lighting, inner.lighting, *current.hdrLighting);
                        auto pattern=San14::Render::ShadowReceiverPatternState::construct();
                        if(special)San14::Render::installShadowReceiverPattern(pattern,current.shadowReceiver.patternScale,
                            palette.selected?&palette.worldToPattern:nullptr);
                        else San14::Render::installOrdinaryPattern(pattern,current.patternCamera,
                            inner.patternModifier.present?inner.patternModifier:outer.patternModifier,
                            patternResources.mergedPresent(),commonClock);
                        const auto sampling=special?San14::Render::PatternSampling{
                            palette.selected?palette.selected->view.Get():patternResources.fallback().view.Get(),
                            palette.selected?palette.selected->sampler.Get():patternResources.fallback().sampler.Get()}:
                            patternResources.ordinaryLit(outer.object,current.patternCamera.mergedOverrideEnabled);
                        associations.values.push_back({lighting,pattern,sampling});
                    }
                }
                associations.prepared=true;
            }
            for(const auto& association:associations.values) {
                const auto& lighting=association.lighting;
                const auto& pattern=association.pattern;
                const auto& sampling=association.sampling;
                // Construct the complete packet in its final slot. Moving
                // a local draw would copy both fixed-size CB arrays again.
                auto& draw=frame.draws.emplace_back();
                draw.litView=sampling.view;draw.litSampler=sampling.sampler;
                draw.primitiveIndex = ordinary.primitiveIndex; draw.templateIndex = primitive.templateIndex;
                draw.vertex = retained.vertex; draw.pixel = retained.pixel;
                std::memcpy(draw.vertex.data(), ordinary.vertex.data(), 160);
                std::memcpy(draw.pixel.data()+192, ordinary.pixel.data(), 16);
                std::memcpy(draw.pixel.data()+336, ordinary.pixel.data()+64, 4);
                for (const auto& write : source.metadata->lightingWrites) {
                    const auto bytes = write.field == 12
                        ? std::span<const std::uint8_t>(reinterpret_cast<const std::uint8_t*>(eye.data()), sizeof(eye))
                        : San14::Render::hdrLightingField(lighting, San14::Render::HdrLightingField(write.field));
                    std::memcpy(draw.pixel.data()+write.destination, bytes.data()+write.source, write.bytes);
                }
                // Forward's shadow module consumes the receiver ambient
                // alpha and shadow group even where HDR did not read them.
                std::memcpy(draw.pixel.data()+156, &lighting.ambient.W, 4);
                for(const auto& write:source.metadata->patternWrites) {
                    const void* value=write.field==0?static_cast<const void*>(pattern.worldToPattern.data()):
                        write.field==1?static_cast<const void*>(&pattern.scale):static_cast<const void*>(pattern.extra.data());
                    auto destination=write.field==0?draw.vertex.data():draw.pixel.data();
                    std::memcpy(destination+write.destination,static_cast<const char*>(value)+write.source,write.bytes);
                }
                std::memcpy(draw.vertex.data()+224, shadow.worldToShadow.data(), sizeof(shadow.worldToShadow));
                std::memcpy(draw.pixel.data()+352, shadow.atlasTransforms.data(), sizeof(shadow.atlasTransforms));
                std::memcpy(draw.pixel.data()+416, &shadow.depthPlane, sizeof(shadow.depthPlane));
                std::memcpy(draw.pixel.data()+432, &shadow.splitEnds, sizeof(shadow.splitEnds));
                std::memcpy(draw.pixel.data()+448, &shadow.atlasScale, sizeof(shadow.atlasScale));
                std::memcpy(draw.pixel.data()+464, &shadow.sampleLoopCount, sizeof(shadow.sampleLoopCount));
                std::memcpy(draw.pixel.data()+480, shadow.lightPlanes.data(), sizeof(shadow.lightPlanes));
                std::memcpy(draw.pixel.data()+672, &shadow.shadowDepthPlane, sizeof(shadow.shadowDepthPlane));
                supplied = true;
            }
        }
        if (!supplied) throw std::runtime_error("Forward primitive has no actual current light association");
    }
    return frame;
}
