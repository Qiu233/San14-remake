#pragma once
#include "ArmyWorldPresentation.h"
#include <optional>
#include <set>
#include <type_traits>

// Include after army-population-native.h. NativeArmySource's existing static
// culler/record masks/skin or rigid/material templates are retained. The imported
// experiment's actors, positions and identities are not a World population.
struct ArmyWorldTemplateMaterial {
    unsigned sourceTemplate{};ArmyWorldMaterialKey key{};
};
struct ArmyWorldTemplateLibrary {
    struct Actor {uint32_t exclusion{},mode{};float scalar{},forceDistance{},distanceBias{};};
    struct Primitive {uint32_t draw{},sourceTemplate{},palette{},queue{},allowedRecords{};uint64_t tableOwner{};bool clothMaterial{};};
    std::vector<std::optional<Actor>> actors;
    std::vector<std::vector<Primitive>> primitives;
    std::map<std::pair<unsigned,ArmyWorldMaterialKey>,unsigned> materials;
    std::vector<uint32_t> defaultActionOrdinals;
    std::vector<ArmyWorldPresentation> constructors;
    std::map<uint32_t,uint64_t> tableOwners;
    std::map<unsigned,std::array<float,6>> localBounds;
    uint64_t materialRevision{1};

    // Material rows describe completed source groups, not requested glyph jobs.
    // Every material-dependent base template needs its actual completed key.
    template<class Source>
    ArmyWorldTemplateLibrary(const Source& source,const std::set<unsigned>& materialTemplates,
            const std::vector<ArmyWorldTemplateMaterial>& completed){
        std::map<unsigned,ArmyWorldMaterialKey> baseMaterial;
        const unsigned categories=unsigned(source.poses.size());
        actors.resize(categories);primitives.resize(categories);constructors.resize(categories);defaultActionOrdinals.resize(categories);
        for(unsigned category=0;category<categories;++category)defaultActionOrdinals[category]=source.defaultActions.rows[category].ordinal;
        for(unsigned index:materialTemplates)if(index>=source.templateCount)throw std::runtime_error("Army source material template range");
        for(const auto& row:completed){if(!materialTemplates.count(row.sourceTemplate)||
                !baseMaterial.emplace(row.sourceTemplate,row.key).second)
                throw std::runtime_error("Army completed source template material duplicate/range");}
        for(const auto& a:source.actors){
            if(a.category>=categories||(a.mode!=0&&a.mode!=3))throw std::runtime_error("Army once-imported category/culler mode");
            Actor next{a.exclusion,a.mode,a.slotScalar,a.forceDistance,a.distanceBias};
            auto& prior=actors[a.category];if(prior){
                if(prior->exclusion!=next.exclusion||prior->mode!=next.mode||prior->scalar!=next.scalar||
                    prior->forceDistance!=next.forceDistance||prior->distanceBias!=next.distanceBias)
                    throw std::runtime_error("Army same-category static actor source differs");
            }else prior=next;
        }
        for(const auto& p:source.primitives){
            unsigned category=source.actors.at(p.actor).category;auto& rows=primitives[category];
            Primitive next{p.draw,p.sourceTemplate,p.palette,p.queue,p.allowedRecords,p.tableOwner,materialTemplates.count(p.sourceTemplate)!=0};
            auto found=std::find_if(rows.begin(),rows.end(),[&](const auto& r){return r.draw==p.draw;});
            if(found==rows.end())rows.push_back(next);
            else if(found->sourceTemplate!=next.sourceTemplate||found->palette!=next.palette||found->queue!=next.queue||
                found->allowedRecords!=next.allowedRecords||found->tableOwner!=next.tableOwner)
                throw std::runtime_error("Army same-category ordinary draw source differs");
        }
        for(unsigned category=0;category<categories;++category){
            if(!actors[category]||primitives[category].empty())throw std::runtime_error("Army required once-imported source category absent");
            std::sort(primitives[category].begin(),primitives[category].end(),[](const auto& a,const auto& b){return a.draw<b.draw;});
        }
        for(const auto& row:completed)materials.emplace(std::make_pair(row.sourceTemplate,row.key),row.sourceTemplate);
    }
    // Extend only after the real source material producer publishes a completed
    // template. Caller keeps template/pixel arrays and palette layout coherent.
    void registerCompleted(unsigned baseTemplate,const ArmyWorldMaterialKey& actualKey,unsigned completedTemplate){
        auto key=std::make_pair(baseTemplate,actualKey);auto prior=materials.find(key);
        if(prior!=materials.end()&&prior->second==completedTemplate)return;
        materials[key]=completedTemplate;++materialRevision;
    }
};
struct ArmyWorldPopulationState {
    uint64_t appliedRevision{};
    uint64_t appliedMaterialRevision{};
    std::map<ArmyWorldActorKey,unsigned> actorIndices;
    std::map<std::tuple<ArmyWorldActorKey,unsigned,unsigned>,unsigned> primitiveIndices;
    std::vector<float> returnedPhases;
    std::vector<ArmyWorldActorKey> materialUnavailable;
};

// createCurrentModel(actorKey,drawIndex,templateIndex,identity) resolves the
// retained physical model; its first use copies an imported draw and makes
// only its private source-sized VS/PS buffers. It is called for newly admitted actors
// or a newly completed material; movement reuses all retained device objects.
template<class Population,class CreateCurrentModel>
inline bool publishArmyWorldPopulation(Population& owner,ArmyWorldPopulationState& state,
        const ArmyWorldTemplateLibrary& library,const ArmyWorldPublication& publication,CreateCurrentModel createCurrentModel){
    if(publication.revision==state.appliedRevision&&library.materialRevision==state.appliedMaterialRevision)return false;
    using Actor=typename decltype(owner.source.actors)::value_type;
    using Primitive=typename decltype(owner.source.primitives)::value_type;
    using Model=typename decltype(owner.currentModels.draws)::value_type;
    std::vector<Actor> actors;std::vector<Primitive> primitives;std::vector<std::string> identities;
    std::vector<NativeAccessoryPoseOwner> poses;std::vector<NativeArmyPoseCache> poseCaches;std::vector<Model> models;ArmyWorldPopulationState next;
    next.appliedRevision=publication.revision;next.appliedMaterialRevision=library.materialRevision;
    for(const auto& current:publication.actors){
        unsigned category=unsigned(current.key.category);const auto& sourcePose=owner.source.poses.at(category);
        const auto& sourceActor=*library.actors.at(category);const auto& sourcePrimitives=library.primitives.at(category);
        std::vector<unsigned> selected;bool missing=false;
        for(const auto& p:sourcePrimitives){
            unsigned index=p.sourceTemplate;
            if(p.clothMaterial){auto found=library.materials.find(std::make_pair(index,current.material));
                if(found==library.materials.end()){missing=true;break;}index=found->second;}
            selected.push_back(index);
        }
        if(missing){next.materialUnavailable.push_back(current.key);continue;}
        unsigned actor=unsigned(actors.size());auto identity=current.key.identity();
        auto old=state.actorIndices.find(current.key);
        const auto initial=old==state.actorIndices.end()?library.constructors[category]:
            ArmyWorldPresentation{owner.source.actors.at(old->second).world,owner.source.actors.at(old->second).obb};
        actors.push_back(Actor{actor,category,sourceActor.exclusion,sourceActor.mode,sourceActor.scalar,
            sourceActor.forceDistance,sourceActor.distanceBias,initial.bounds,initial.world});identities.push_back(identity);
        poses.push_back(old==state.actorIndices.end()?createNativeAccessoryPoseOwner(sourcePose):owner.poses.at(old->second));
        poseCaches.push_back(old==state.actorIndices.end()?NativeArmyPoseCache{}:owner.poseCaches.at(old->second));
        next.actorIndices.emplace(current.key,actor);next.returnedPhases.push_back(current.returnedPhase);
        for(size_t draw=0;draw<sourcePrimitives.size();++draw){const auto& p=sourcePrimitives[draw];unsigned index=selected[draw];
            auto key=std::make_tuple(current.key,p.draw,index);auto prior=state.primitiveIndices.find(key);
            next.primitiveIndices.emplace(key,unsigned(primitives.size()));
            primitives.push_back(Primitive{actor,p.draw,index,p.palette,p.queue,p.allowedRecords,p.tableOwner});
            models.push_back(prior==state.primitiveIndices.end()?createCurrentModel(current.key,p.draw,index,identity+"/draw"+std::to_string(p.draw)):
                owner.currentModels.draws.at(prior->second));
        }
    }
    // Also handles the initial empty World and actual withdrawal. The physical
    // pool retains private buffers after a descriptor leaves this live list.
    owner.source.actors=std::move(actors);owner.source.primitives=std::move(primitives);owner.source.identities=std::move(identities);
    owner.poses=std::move(poses);owner.poseCaches=std::move(poseCaches);owner.currentModels.draws=std::move(models);owner.currentModels.blend=owner.templates.blend;
    state=std::move(next);return true;
}
