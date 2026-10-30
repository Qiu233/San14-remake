#pragma once
#include "FieldDisplayFrame.h"
#include "../../Passes/Shadows/ModelShadowAdmission.h"
#include <algorithm>

namespace San14::Render {
// The complete static Root bank and the FieldScene reset directory have
// different memberships. Source indices and Model ordinals are not positions
// in that directory; Root-only models keep their own declared masks/type.
class FieldDisplay {
    std::vector<source_far_main::RegistryEntry> entries;
    std::vector<source_far_main::CurrentModelMasks> owned,current;
    std::vector<uint32_t> rootTypes,eligibility;
    std::vector<int> sourceIndexToOrdinal;
public:
    template<class Catalog,class Profiles,class Root>
    void bind(const source_far_main::Profile& source,const Catalog& field,const Profiles& shadows,const Root& root) {
        entries.clear();owned.resize(field.instances.size());current.resize(owned.size());
        rootTypes.resize(owned.size());eligibility.resize(owned.size());
        unsigned maximum=0;
        for(const auto& instance:field.instances)maximum=std::max(maximum,instance.index);
        sourceIndexToOrdinal.assign(maximum+1,-1);
        for(size_t i=0;i<field.instances.size();++i){
            auto& ordinal=sourceIndexToOrdinal[field.instances[i].index];
            if(ordinal!=-1)throw std::runtime_error("Complete static Model bank repeats a source identity");
            ordinal=int(i);
        }
        std::vector<bool> declared(owned.size()),registered(owned.size());
        std::vector<uint32_t> objectIds(owned.size());
        for(const auto& child:root.children)if(child.classId==0xC4B9B28D&&child.staticInstanceIndex!=0xFFFFFFFFu){
            const auto ordinal=child.staticInstanceIndex;
            if(ordinal>=owned.size()||declared[ordinal])
                throw std::runtime_error("Scene Root and complete Model bank identities differ");
            declared[ordinal]=true;objectIds[ordinal]=child.objectId;
            rootTypes[ordinal]=uint32_t(child.flags);eligibility[ordinal]=child.eligibility;
            owned[ordinal]={child.world328,child.world32C,child.world330};
        }
        if(std::find(declared.begin(),declared.end(),false)!=declared.end())
            throw std::runtime_error("Scene Root omits a loaded static Model identity");
        for(const auto& entry:source.registry)if(entry.type==1){
            if(entry.modelOrdinal>=owned.size()||registered[entry.modelOrdinal]||
               entry.sourceIndex>=source.registry.size()||
               field.instances[entry.modelOrdinal].index!=entry.sourceIndex||
               field.instances[entry.modelOrdinal].primitiveCount!=entry.primitiveCount||
               objectIds[entry.modelOrdinal]!=entry.objectId)
                throw std::runtime_error("Field registry and loaded Model identities differ");
            entries.push_back(entry);registered[entry.modelOrdinal]=true;
        }
        for(size_t i=0;i<field.instances.size();++i)
            if((field.instances[i].index<source.registry.size())!=registered[i])
                throw std::runtime_error("Field registry omits or misclassifies a loaded Field Model identity");
        for(size_t family=0;family<shadows.size();++family) {
            const auto& worlds=shadows[family].worlds;
            if(worlds.size()<owned.size())throw std::runtime_error("Complete static shadow registry is incomplete");
            for(size_t i=0;i<owned.size();++i)
                if(worlds[i].index!=field.instances[i].index||worlds[i].primitiveCount!=field.instances[i].primitiveCount)
                    throw std::runtime_error("Static shadow and Model registry identities differ");
        }
    }
    void capture(const source_far_main::Current& frame) {
        for(const auto& entry:entries){
            auto& masks=owned[entry.modelOrdinal];
            masks=source_far_main::captureCurrentModelMasks(entry,frame,masks);
        }
        for(size_t i=0;i<owned.size();++i)
            current[i]=eligibility[i]&&source_mode2_octree::admitsEntry(rootTypes[i],frame.sceneTypeMask)
                ?owned[i]:source_far_main::CurrentModelMasks{};
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
    void publishShadow(Profile& profile,const NativeModelCollectionCaller& caller,
        const NativeCityGateShadowAdmission& admission) const {
        for(size_t i=0;i<current.size();++i) {
            auto& row=profile.worlds[i];const auto& masks=current[i];
            NativeModelWorldAdmission world{masks.first,masks.second,masks.shadow,eligibility[i],
                row.exclusion,row.flags,row.opacity};
            const auto admitted=nativeCityModelCurrentMasks(world,caller);
            // Cold profile allowed bits are not a current View admission.
            row.allowedMask=nativeCityGateFirstCasterMask(admission,world,caller);
            row.ordinaryFirstMask=admitted.firstSlot?admitted.first&caller.successfulViews:0;
        }
    }
    void finishPreparation(){for(const auto& entry:entries)source_far_main::restoreNormalWorldMasks(owned[entry.modelOrdinal]);}
    unsigned admittedModels()const {
        return unsigned(std::count_if(current.begin(),current.end(),[](const auto& mask){return mask.first!=0;}));
    }
};
} // namespace San14::Render
