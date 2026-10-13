#pragma once
#include "ArmyWorldPresentation.h"
#include <optional>
#include <set>
#include <type_traits>

// Static category visibility, primitives and completed materials are imported
// independently. Only current World publications create actor instances.
struct ArmyWorldTemplateLibrary {
    struct CategoryVisibility {uint32_t exclusion{},mode{};float scalar{},forceDistance{},distanceBias{};};
    struct Primitive {uint32_t draw{},sourceTemplate{},palette{},queue{},allowedRecords{};uint64_t tableOwner{};
        bool materialDependent{};int materialManager=-1;};
    std::vector<std::optional<CategoryVisibility>> visibility;
    std::vector<std::vector<Primitive>> primitives;
    std::map<std::pair<unsigned,ArmyWorldMaterialKey>,unsigned> materials;
    std::vector<uint32_t> defaultActionOrdinals;
    std::vector<ArmyWorldPresentation> constructors;
    std::map<std::pair<std::string,uint32_t>,uint64_t> tableOwners;
    struct TemplateRoute {unsigned queue{},allowedRecords{};uint64_t tableOwner{};};
    std::vector<TemplateRoute> templateRoutes;
    std::map<unsigned,std::array<float,6>> localBounds;
    uint64_t materialRevision{1};

    ArmyWorldTemplateLibrary() = default;
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
        const auto& sourceActor=*library.visibility.at(category);const auto& sourcePrimitives=library.primitives.at(category);
        std::vector<unsigned> selected;bool missing=false;
        for(const auto& p:sourcePrimitives){
            unsigned index=p.sourceTemplate;
            if(p.materialDependent){auto found=library.materials.find(std::make_pair(index,current.material));
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
            const auto& route=library.templateRoutes.at(index);
            primitives.push_back(Primitive{actor,p.draw,index,p.palette,route.queue,route.allowedRecords,route.tableOwner});
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
