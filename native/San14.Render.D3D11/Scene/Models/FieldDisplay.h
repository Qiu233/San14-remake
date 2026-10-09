#pragma once
#include "FieldDisplayFrame.h"
#include <algorithm>

namespace San14::Render {
// Actual unchanged FieldScene registry membership. Dynamic City/Army owners
// are not members merely because they share a model template or ordinal.
class FieldDisplay {
    std::vector<source_far_main::RegistryEntry> entries;
    std::vector<source_far_main::CurrentModelMasks> owned,current;
    std::vector<int> sourceIndexToOrdinal;
    std::array<std::vector<uint32_t>,2> sourceShadowMasks;
public:
    template<class Catalog,class Profiles>
    void bind(const source_far_main::Profile& source,const Catalog& field,const Profiles& shadows) {
        entries.resize(field.instances.size());owned.resize(entries.size());current.resize(entries.size());
        std::vector<bool> found(entries.size());sourceIndexToOrdinal.assign(source.registry.size(),-1);
        for(const auto& entry:source.registry)if(entry.type==1){
            if(entry.modelOrdinal>=entries.size()||found[entry.modelOrdinal]||
               entry.sourceIndex>=sourceIndexToOrdinal.size()||
               field.instances[entry.modelOrdinal].index!=entry.sourceIndex||
               field.instances[entry.modelOrdinal].primitiveCount!=entry.primitiveCount)
                throw std::runtime_error("Field registry and loaded Model identities differ");
            entries[entry.modelOrdinal]=entry;found[entry.modelOrdinal]=true;
            sourceIndexToOrdinal[entry.sourceIndex]=int(entry.modelOrdinal);
        }
        if(std::find(found.begin(),found.end(),false)!=found.end())
            throw std::runtime_error("Field registry omits a loaded Model identity");
        for(auto& masks:owned)source_far_main::restoreNormalWorldMasks(masks);
        for(size_t family=0;family<sourceShadowMasks.size();++family){
            const auto& worlds=shadows[family].worlds;auto& masks=sourceShadowMasks[family];
            if(worlds.size()<entries.size())throw std::runtime_error("Field shadow registry is incomplete");
            masks.resize(entries.size());
            for(size_t i=0;i<entries.size();++i){
                if(worlds[i].index!=entries[i].sourceIndex||worlds[i].primitiveCount!=entries[i].primitiveCount)
                    throw std::runtime_error("Field shadow and Model registry identities differ");
                masks[i]=worlds[i].allowedMask;
            }
        }
    }
    void capture(const source_far_main::Current& frame) {
        const bool admitted=source_mode2_octree::admitsEntry(1,frame.sceneTypeMask);
        for(size_t i=0;i<entries.size();++i){
            owned[i]=source_far_main::captureCurrentModelMasks(entries[i],frame,owned[i]);
            current[i]=admitted?owned[i]:source_far_main::CurrentModelMasks{};
        }
    }
    template<class Plan,class Catalog>
    void selectOrdinary(Plan& plan,const Catalog& field)const {
        auto hidden=[&](unsigned index){return current[sourceIndexToOrdinal[index]].first==0;};
        auto remove=[&](auto& ids){ids.erase(std::remove_if(ids.begin(),ids.end(),hidden),ids.end());};
        remove(plan.visibleInstances);remove(plan.visibleUnsupportedInstances);
        plan.draws.erase(std::remove_if(plan.draws.begin(),plan.draws.end(),[&](const auto& draw){
            return hidden(field.primitives[draw.primitiveIndex].instanceIndex);
        }),plan.draws.end());
    }
    template<class Profile>
    void publishShadow(Profile& profile,unsigned family)const {
        for(size_t i=0;i<entries.size();++i){
            profile.worlds[i].allowedMask=sourceShadowMasks[family][i]&current[i].shadow;
            profile.worlds[i].ordinaryFirstMask=current[i].first;
        }
    }
    void finishPreparation(){for(auto& masks:owned)source_far_main::restoreNormalWorldMasks(masks);}
    unsigned admittedModels()const {
        return unsigned(std::count_if(current.begin(),current.end(),[](const auto& mask){return mask.first!=0;}));
    }
};
} // namespace San14::Render
