#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "ArmyCategoryInputs.h"
#include "Accessories/Category2Resources.h"

struct NativeArmyResourceBank {
    NativeArmyPopulation population;
    ArmyWorldTemplateLibrary library;
    std::vector<unsigned> capacities;
};

inline NativeArmyResourceBank prepareNativeArmyResourceBank(ID3D11Device* device,const std::filesystem::path& root,
        const NativeCategory2Resources& sharedCategory2){
    NativeArmyResourceBank result;auto& owner=result.population;auto& library=result.library;
    auto profiles=readNativeArmyResourceProfiles(root);
    owner.source.poses=std::move(profiles.poses);owner.source.defaultActions=std::move(profiles.defaults);
    owner.source.recordMasks=std::move(profiles.recordMasks);owner.source.capacities=std::move(profiles.recordCapacities);
    owner.source.commonExclude=profiles.commonExclude;library=std::move(profiles.library);
    result.capacities=std::move(profiles.capacities);
    for(const auto& categoryProfile:profiles.categories){
        const auto& declaration=categoryProfile.declaration;const auto category=declaration.category;
        NativeModelInputs models;std::vector<std::vector<char>> vertices;
        std::vector<std::array<char,80>> pixels;std::vector<std::array<uint32_t,3>> controls;
        if(declaration.borrowedGeometry){
            if(owner.source.defaultActions.fps!=sharedCategory2.fps||owner.source.defaultActions.rows[category].clipFileId!=sharedCategory2.defaultAction.clipFileId)
                throw std::runtime_error("Army and shared category2 animation declarations differ");
            models=sharedCategory2.models;pixels=sharedCategory2.pixels;
            for(const auto& draw:models.draws){
                D3D11_BUFFER_DESC desc{};draw.vsConstants->GetDesc(&desc);
                if(desc.ByteWidth!=6256)throw std::runtime_error("Shared category2 vertex interface differs");
            }
            // CPU seeds belong to the shared resource owner, including actual skin flags.
            vertices=sharedCategory2.vertices;controls=sharedCategory2.skinControls;
        }else{
            models=loadNativeModels(device,declaration.directory);
            NativePreparedInputFile names(declaration.directory/"models.txt");std::string relative;
            if(!names)throw std::runtime_error("Army category model declarations absent");
            while(names>>relative){
                auto vertex=load(declaration.directory/relative/"vs-b0.bin");auto pixel=load(declaration.directory/relative/"ps-b0.bin");
                auto skin=load(declaration.directory/relative/"skin-controls.bin");
                if((vertex.size()!=160&&vertex.size()!=6256)||pixel.size()!=80||skin.size()!=12)
                    throw std::runtime_error("Army category material interface unsupported");
                vertices.push_back(std::move(vertex));std::array<char,80> p{};std::memcpy(p.data(),pixel.data(),80);pixels.push_back(p);
                std::array<uint32_t,3> c{};std::memcpy(c.data(),skin.data(),12);controls.push_back(c);
            }
        }
        if(models.draws.size()!=categoryProfile.templateCount||vertices.size()!=models.draws.size()||
            pixels.size()!=models.draws.size()||controls.size()!=models.draws.size())throw std::runtime_error("Army category model bank is incomplete");
        if(category==0)owner.templates.blend=models.blend;
        for(auto& model:models.draws)owner.templates.draws.push_back(std::move(model));
        for(auto& vertex:vertices)owner.source.vertices.push_back(std::move(vertex));
        for(const auto& pixel:pixels)owner.source.pixels.push_back(pixel);
        for(const auto& skin:controls)owner.source.skinControls.push_back(skin);
    }
    owner.source.templateCount=unsigned(owner.templates.draws.size());
    owner.currentModels.blend=owner.templates.blend;
    return result;
}
