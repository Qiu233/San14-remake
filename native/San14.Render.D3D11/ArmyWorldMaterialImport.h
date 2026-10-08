#pragma once
#include "ArmyWorldPopulation.h"

// Source capture made beside the imported model bank. This is not a resource
// completion receipt. Load once with the same native model/template import.
struct ArmyWorldMaterialImport {
    struct Row {
        uint32_t sourceTemplate{};ArmyWorldMaterialKey key{};
        uint32_t manager{},proxy{},texture{},sampler{},regularGroup{},targetGroup{},alternateGroup{},preserve{};
    };
    std::set<unsigned> materialTemplates;std::vector<Row> rows;
};
inline ArmyWorldMaterialImport loadArmyWorldMaterialImport(const std::filesystem::path& directory,unsigned templateCount){
    native_accessory_pose_detail::Reader in(directory/"army-world-materials.bin");std::array<char,8> magic{};in.read(magic.data(),8);
    if(magic!=std::array<char,8>{'A','R','M','Y','M','A','T','1'})throw std::runtime_error("Army world source material import schema");
    unsigned importedTemplates=in.get<uint32_t>(),count=in.get<uint32_t>();
    if(importedTemplates!=templateCount||count>templateCount||in.bytes.size()!=16+size_t(count)*48)
        throw std::runtime_error("Army world source material import size/template count");
    ArmyWorldMaterialImport r;r.rows.resize(count);
    for(auto& row:r.rows){row.sourceTemplate=in.get<uint32_t>();row.key.force=in.get<int32_t>();row.key.color49=in.get<int32_t>();row.key.glyph=in.get<uint32_t>();
        row.manager=in.get<uint32_t>();row.proxy=in.get<uint32_t>();row.texture=in.get<uint32_t>();row.sampler=in.get<uint32_t>();
        row.regularGroup=in.get<uint32_t>();row.targetGroup=in.get<uint32_t>();row.alternateGroup=in.get<uint32_t>();row.preserve=in.get<uint32_t>();
        if(row.sourceTemplate>=templateCount||row.key.force<0||row.key.force>=52||row.key.color49<0||row.key.color49>60||
            row.key.glyph>65535||row.manager>2||row.texture>=16||row.sampler>=16||!row.regularGroup||
            !r.materialTemplates.insert(row.sourceTemplate).second)
            throw std::runtime_error("Army world material import source row range/duplicate");
    }
    return r;
}

// Call after prepareNativeArmyPopulation has returned. Its normal import has
// completed stock glyph jobs, real regular groups and CPU20/30 branch checks.
// Take keys only from actual completed producer resources bound to that draw.
// No Force constant or declaration-only GPU-ready assumption is involved.
template<class Population>
inline std::vector<ArmyWorldTemplateMaterial> captureCompletedArmyWorldMaterials(const Population& owner,const ArmyWorldMaterialImport& imported){
    std::vector<ArmyWorldTemplateMaterial> r;
    for(const auto& row:imported.rows){const auto& draw=owner.templates.draws.at(row.sourceTemplate);
        auto found=std::find_if(owner.generatedPrimaries.begin(),owner.generatedPrimaries.end(),[&](const auto& p){return p.proxy==row.proxy;});
        if(found==owner.generatedPrimaries.end()||!found->producerCompleted)continue;
        if(found->manager!=row.manager||found->force!=unsigned(row.key.force)||found->code!=row.key.glyph||
            !found->srv.Get()||!found->sourceSampler.Get()||draw.textures[row.texture].Get()!=found->srv.Get()||
            draw.samplers[row.sampler].Get()!=found->sourceSampler.Get())
            throw std::runtime_error("Army actual completed producer key/binding differs from once-imported source material");
        r.push_back({row.sourceTemplate,row.key});
    }
    return r;
}
