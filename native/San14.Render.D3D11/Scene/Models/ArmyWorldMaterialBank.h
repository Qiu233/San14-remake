#pragma once
#include "ArmyWorldMaterialImport.h"

// Additional fresh source keys share the imported geometry and regular material.
// Each primary is produced once at load, before Main consumes the registry.
inline unsigned prepareArmyWorldMaterialBank(ID3D11Device* device,ID3D11DeviceContext* context,
        const std::filesystem::path& directory,NativeArmyPopulation& owner,
        ArmyWorldTemplateLibrary& library,const ArmyWorldMaterialImport& imported){
    const auto input=directory/"army-material-bank";
    std::ifstream records(input/"materials.txt");std::string schema;unsigned count;
    if(!(records>>schema>>count)||schema!="ARMYBNK1")
        throw std::runtime_error("Army source material bank import absent or unsupported");
    accessory_glyph::StockProducer producer(device,input);
    std::map<unsigned,accessory_glyph::Atlas> atlases;
    std::set<std::pair<unsigned,ArmyWorldMaterialKey>> keys;
    std::set<unsigned> regularGroups;std::vector<std::array<unsigned,2>> alternateGroups;
    unsigned added=0;
    for(unsigned index=0;index<count;++index){
        ArmyWorldMaterialImport::Row row;std::string relative;
        if(!(records>>row.sourceTemplate>>row.key.force>>row.key.color49>>row.key.glyph>>
                row.manager>>row.proxy>>row.texture>>row.sampler>>row.regularGroup>>
                row.targetGroup>>row.alternateGroup>>row.preserve>>relative))
            throw std::runtime_error("Army source material bank row truncated");
        auto base=std::find_if(imported.rows.begin(),imported.rows.end(),[&](const auto& source){
            return source.sourceTemplate==row.sourceTemplate;
        });
        if(base==imported.rows.end()||row.manager!=base->manager||row.texture!=base->texture||
                row.sampler!=base->sampler||row.preserve!=base->preserve||row.key.force<0||
                row.key.force>=52||row.key.color49<0||row.key.color49>60||row.key.glyph>65535||
                !keys.emplace(row.sourceTemplate,row.key).second)
            throw std::runtime_error("Army material bank source template/key differs");
        regularGroups.insert(row.regularGroup);alternateGroups.push_back({row.targetGroup,row.alternateGroup});
        if(library.materials.count({row.sourceTemplate,row.key}))continue;
        const auto job=accessory_glyph::read(input/relative/"glyph-job.bin");
        if(job.size()<48)throw std::runtime_error("Army material bank source glyph job truncated");
        std::array<unsigned,4> header;std::memcpy(header.data(),job.data(),16);
        if(header!=std::array<unsigned,4>{row.manager,unsigned(row.key.force),row.proxy,row.key.glyph})
            throw std::runtime_error("Army material bank source key differs from glyph job");
        accessory_glyph::Atlas* atlas=nullptr;
        if(row.key.glyph){
            auto found=atlases.find(row.key.glyph);
            if(found==atlases.end())found=atlases.emplace(row.key.glyph,
                accessory_glyph::Atlas::create(device,input,row.key.glyph)).first;
            atlas=&found->second;
        }
        auto primary=producer.execute(device,context,input,relative,atlas);
        auto model=owner.templates.draws.at(row.sourceTemplate);
        model.textures.at(row.texture)=primary.srv;model.samplers.at(row.sampler)=primary.sourceSampler;
        const unsigned completed=unsigned(owner.templates.draws.size());
        owner.templates.draws.push_back(std::move(model));
        owner.source.pixels.push_back(owner.source.pixels.at(row.sourceTemplate));
        owner.source.vertices.push_back(owner.source.vertices.at(row.sourceTemplate));
        owner.source.skinControls.push_back(owner.source.skinControls.at(row.sourceTemplate));
        owner.generatedPrimaries.push_back(std::move(primary));
        library.registerCompleted(row.sourceTemplate,row.key,completed);++added;
    }
    for(const auto& groups:alternateGroups)if(regularGroups.count(groups[0])||regularGroups.count(groups[1]))
        throw std::runtime_error("Army material bank reached a different registered CPU20/30 branch");
    std::cout<<"source ArmyMaterialBank keys="<<keys.size()<<" added="<<added<<'\n';
    return added;
}
