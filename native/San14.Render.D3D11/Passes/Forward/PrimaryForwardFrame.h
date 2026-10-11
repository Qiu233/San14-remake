#pragma once
// CPU-only adapter after city-model-population-native.h. Import/static recipe
// stays owned; each request uses the real current catalog and ordinary plan.
// Shader sources: state0 2F9960EF/8F6C9FB9, matrix0, combo14 VS0/PS11.
#include "../Lighting/HdrLightingState.h"
#include "../Environment/SeasonFrame.h"
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
struct NativePrimaryForwardLightingWrite {unsigned field{}, destination{}, source{}, bytes{};};
struct NativePrimaryForwardSource {
    std::vector<NativePrimaryForwardTemplate> templates;
    std::vector<NativePrimaryForwardLightingWrite> lightingWrites;
    unsigned lightGroup{};
};
struct NativePrimaryForwardDraw {
    unsigned primitiveIndex{},templateIndex{};
    std::array<char,480> vertex{};
    std::array<char,1360> pixel{};
};
struct NativePrimaryForwardFrame {std::vector<NativePrimaryForwardDraw> draws;};
inline NativePrimaryForwardSource prepareNativePrimaryForwardSource(const std::filesystem::path& directory) {
    NativePrimaryForwardSource p;unsigned n;std::ifstream count(directory/"template-count.txt");count>>n;
    if(!count)throw std::runtime_error("Forward state0 template count absent");p.templates.resize(n);
    std::ifstream rows(directory/"templates.txt");unsigned index,eligible;std::string name;
    while(rows>>index>>eligible>>name){auto& t=p.templates.at(index);t.eligible=eligible!=0;if(!t.eligible)continue;
        std::ifstream v(directory/name/"vs-b0.bin",std::ios::binary),b(directory/name/"ps-b0.bin",std::ios::binary);
        v.read(t.vertex.data(),t.vertex.size());b.read(t.pixel.data(),t.pixel.size());
        if(!v||!b)throw std::runtime_error("Original forward complete source CB template absent");}
    std::ifstream writes(directory/"forward-lighting-writes.txt");
    NativePrimaryForwardLightingWrite write;
    while(writes>>write.field>>write.destination>>write.source>>write.bytes) {
        const auto width = write.field < San14::Render::HdrLightingFieldBytes.size()
            ? San14::Render::HdrLightingFieldBytes[write.field] : write.field == 12 ? 16u : 0u;
        if (width == 0 || write.bytes == 0 || std::uint64_t(write.source) + write.bytes > width ||
            std::uint64_t(write.destination) + write.bytes > 1360)
            throw std::runtime_error("Forward named lighting layout range");
        p.lightingWrites.push_back(write);
    }
    std::ifstream group(directory/"forward-light-group.txt"); group>>p.lightGroup;
    if(!rows.eof()||!writes.eof()||!group||p.lightingWrites.empty())
        throw std::runtime_error("Forward lighting source is incomplete");
    return p;
}

inline NativePrimaryForwardFrame prepareNativePrimaryForwardCurrent(const NativePrimaryForwardSource& source,
    const NativeCityPopulationSource& currentWorld, const NativeCityPopulationPlan& currentPlan,
    const San14::Render::MapViewDefinition& view, const source_current_season::Frame& current,
    const std::array<float,4>& eye, const char* currentShadowReceiver, size_t receiverBytes) {
    if (receiverBytes != 768 || !current.hdrLighting)
        throw std::runtime_error("Forward requires current lighting and its real shadow receiver");
    auto group = source.lightGroup & 0x7fffffffu;
    const bool overridden = view.common.present && view.common.outerLightGroupOverride != 0;
    if (overridden) group = view.common.outerLightGroupOverride;
    else if (source.lightGroup & 0x80000000u)
        throw std::runtime_error("Forward negative outer-light group requires its unimplemented producer");
    const auto exclusion = ~(1u << ((group - 1) & 31));
    NativePrimaryForwardFrame frame;
    frame.draws.reserve(currentPlan.models.draws.size());
    for (const auto& ordinary : currentPlan.models.draws) {
        const auto& primitive = currentWorld.catalog.primitives.at(ordinary.primitiveIndex);
        const auto& retained = source.templates.at(primitive.templateIndex);
        if (!retained.eligible) continue;
        const auto& binding = currentWorld.templates.at(primitive.templateIndex);
        const auto& world = currentWorld.worlds.at(primitive.instanceIndex);
        const auto masks = nativeCityModelCurrentMasks(world);
        bool supplied = false;
        for (std::size_t record = 0; record < view.records.size(); ++record) {
            const auto mask = view.records[record].groupMask;
            if ((binding.allowedRecords & (1u << record)) == 0 ||
                (masks.first & world.successful3340 & mask) == 0) continue;
            for (const auto& outer : current.lights) {
                if (outer.mask & exclusion) continue;
                for (const auto& inner : current.lights) {
                    if (inner.mask != mask) continue;
                    // Forward has its own current light association. It shares
                    // typed numeric preparation, not a selected HDR byte layout.
                    const auto lighting = San14::Render::prepareHdrLighting(outer.lighting, inner.lighting, *current.hdrLighting);
                    NativePrimaryForwardDraw draw;
                    draw.primitiveIndex = ordinary.primitiveIndex; draw.templateIndex = primitive.templateIndex;
                    draw.vertex = retained.vertex; draw.pixel = retained.pixel;
                    std::memcpy(draw.vertex.data(), ordinary.vertex.data(), 160);
                    std::memcpy(draw.pixel.data()+192, ordinary.pixel.data(), 16);
                    std::memcpy(draw.pixel.data()+336, ordinary.pixel.data()+64, 4);
                    for (const auto& write : source.lightingWrites) {
                        const auto bytes = write.field == 12
                            ? std::span<const std::uint8_t>(reinterpret_cast<const std::uint8_t*>(eye.data()), sizeof(eye))
                            : San14::Render::hdrLightingField(lighting, San14::Render::HdrLightingField(write.field));
                        std::memcpy(draw.pixel.data()+write.destination, bytes.data()+write.source, write.bytes);
                    }
                    // Forward's shadow module consumes the receiver ambient
                    // alpha and shadow group even where HDR did not read them.
                    std::memcpy(draw.pixel.data()+156, currentShadowReceiver+12, 4);
                    std::memcpy(draw.vertex.data()+160, currentShadowReceiver+16, 64);
                    std::memcpy(draw.pixel.data()+288, currentShadowReceiver+80, 4);
                    std::memcpy(draw.vertex.data()+224, currentShadowReceiver+96, 256);
                    for (const auto& field : std::array<std::array<unsigned,2>,7>{{{352,64},{416,16},{432,16},{448,16},{464,4},{480,192},{672,16}}})
                        std::memcpy(draw.pixel.data()+field[0], currentShadowReceiver+field[0], field[1]);
                    frame.draws.push_back(std::move(draw)); supplied = true;
                }
            }
        }
        if (!supplied) throw std::runtime_error("Forward primitive has no actual current light association");
    }
    return frame;
}
