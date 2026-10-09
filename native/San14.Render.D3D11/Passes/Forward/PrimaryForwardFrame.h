#pragma once
// CPU-only adapter after city-model-population-native.h. Import/static recipe
// stays owned; each request uses the real current catalog and ordinary plan.
// Shader sources: state0 2F9960EF/8F6C9FB9, matrix0, combo14 VS0/PS11.
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
struct NativePrimaryForwardSharedWrite {unsigned destination{},source{},bytes{};};
struct NativePrimaryForwardSource {
    std::vector<NativePrimaryForwardTemplate> templates;
    std::vector<NativePrimaryForwardSharedWrite> hdrWrites;
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
    std::ifstream writes(directory/"hdr-to-forward.txt");NativePrimaryForwardSharedWrite write;
    while(writes>>write.destination>>write.source>>write.bytes)p.hdrWrites.push_back(write);
    if(!rows.eof()||p.hdrWrites.empty())throw std::runtime_error("Original forward source profile incomplete");return p;
}
inline NativePrimaryForwardFrame prepareNativePrimaryForwardCurrent(const NativePrimaryForwardSource& source,
    const NativeCityPopulationSource& currentWorld,const NativeCityPopulationPlan& currentPlan,
    const std::array<float,4>& eye,const char* currentHdrInner02,size_t hdrBytes,
    const char* currentShadowReceiver,size_t receiverBytes) {
    // Held-lens season/MainMode0/1 is this imported producer's source scope.
    // Its lens-dependent Cluster/Volume scalars are stable, not fingerprinted
    // on each request. A changed lens uses its actual numeric producer.
    if(hdrBytes!=736||receiverBytes!=768)throw std::runtime_error("Forward current source CB layout differs");
    NativePrimaryForwardFrame frame;frame.draws.reserve(currentPlan.models.draws.size());
    for(const auto& ordinary:currentPlan.models.draws){
        const auto& primitive=currentWorld.catalog.primitives.at(ordinary.primitiveIndex);
        const auto& retained=source.templates.at(primitive.templateIndex);if(!retained.eligible)continue;
        NativePrimaryForwardDraw draw;draw.primitiveIndex=ordinary.primitiveIndex;draw.templateIndex=primitive.templateIndex;
        draw.vertex=retained.vertex;draw.pixel=retained.pixel;
        // Source current primitive LOCAL/World + actual V418 mW2P. Plan is
        // already sorted with actual14->3 keys and owns current admission.
        std::memcpy(draw.vertex.data(),ordinary.vertex.data(),160);
        std::memcpy(draw.pixel.data()+192,ordinary.pixel.data(),16); // cSpec
        std::memcpy(draw.pixel.data()+336,ordinary.pixel.data()+64,4); // actual AlphaTest value
        for(const auto& w:source.hdrWrites)std::memcpy(draw.pixel.data()+w.destination,currentHdrInner02+w.source,w.bytes);
        std::memcpy(draw.pixel.data()+176,eye.data(),sizeof(eye));
        // HDR PS383 normalizes unread ambLit.W, but forward's actual Shadow
        // module uses it. The normal receiver has the same full E0 writer.
        std::memcpy(draw.pixel.data()+156,currentShadowReceiver+12,4);
        // Same normal ready Pattern factory/cache, not old FE8 world fit.
        std::memcpy(draw.vertex.data()+160,currentShadowReceiver+16,64); // mW2Pt
        std::memcpy(draw.pixel.data()+288,currentShadowReceiver+80,4); // fPtn
        std::memcpy(draw.vertex.data()+224,currentShadowReceiver+96,256); // current retained mW2S
        // Source PS5/768 positions coincide with the actual forward PS11
        // shadow named group. Compact count2 retains its inactive owner rows.
        for(const auto& field:std::array<std::array<unsigned,2>,7>{{{352,64},{416,16},{432,16},{448,16},{464,4},{480,192},{672,16}}})
            std::memcpy(draw.pixel.data()+field[0],currentShadowReceiver+field[0],field[1]);
        frame.draws.push_back(draw);
    }
    return frame;
}
// The caller supplies actual HDR inner02 and normal receiver bytes after their
// source CPU writers. Never pick an initial/pan/mode1 template by frame/camera.
// GPU owner retains the actual undefined t16 allocation and sparse b1/b2.
