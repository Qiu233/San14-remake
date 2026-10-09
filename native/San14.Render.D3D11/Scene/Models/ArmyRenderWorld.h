#pragma once
#include "ArmyPopulation.h"
#include "ArmyAnimation.h"
#include "ArmyGraphicsActions.h"
#include "ArmyWorldMaterialImport.h"
#include "ArmyWorldMaterialBank.h"
#include "ArmyWorldModeImport.h"
#include "ArmyWorldModeBounds.h"
#include "SharedCategory2Accessories.h"
#include "AnimationCadence.h"
#include "../../Runtime/RenderSession.h"

namespace San14::Render {
// Retained actor presentation and the shared category-2 accessory pool belong
// to one render session. World publications and pass targets are borrowed only
// for the duration of an operation; the frame coordinator owns their cadence.
class ArmyRenderWorld {
public:
    ArmyRenderWorld(bool armies,bool accessories):sourceArmyVisuals(armies),sourceAccessoryVisuals(accessories){}
    ArmyRenderWorld(const ArmyRenderWorld&)=delete;
    ArmyRenderWorld& operator=(const ArmyRenderWorld&)=delete;

    void prepare(ID3D11Device* device,ID3D11DeviceContext* context,const PreparedResourceDirectories& resources,
        NativeCityAccessoryGpu& cityAccessories,NativeCityAccessoryGpu& areaAccessories) {
        if(sourceArmyVisuals
            ||sourceAccessoryVisuals
            ) {
            const auto armyDirectory=resources.directory(ResourceGroup::Armies);
            armyModels=prepareNativeArmyPopulation(device,context,armyDirectory,false);
            const auto materials=loadArmyWorldMaterialImport(armyDirectory,armyModels.source.templateCount);
            armyWorldTemplates.emplace(armyModels.source,materials.materialTemplates,
                captureCompletedArmyWorldMaterials(armyModels,materials));
            prepareArmyWorldMaterialBank(device,context,armyDirectory,armyModels,*armyWorldTemplates,materials);
            armyWorldTemplates->constructors=readArmyWorldConstructors(resources.directory(ResourceGroup::ArmyActions)/"constructor-worlds.bin");
            auto modelCapacities=readArmyWorldPoolCapacities(resources.directory(ResourceGroup::ArmyActions)/"pool-banks.bin");
            armyGraphicsActions=loadNativeArmyGraphicsActions(resources.directory(ResourceGroup::ArmyActions),armyModels.source);
            for(unsigned category=4;category<=9;++category){
                const auto directory=category<7?"army-mode-category"+std::to_string(category):
                    "army-naval-category-"+std::to_string(category);
                auto resource=importArmyWorldModeResources(device,resources.root()/directory,
                    armyModels.source,armyWorldTemplates->tableOwners);
                appendArmyWorldModeResources(std::move(resource),armyModels,*armyWorldTemplates,*armyGraphicsActions,modelCapacities);
            }
            armyModelPool.emplace(modelCapacities,
                armyWorldTemplates->constructors,armyModels.source.poses,armyModels.source.defaultActions);
            if(sourceAccessoryVisuals){
                cityAccessories.templates.blend=armyModels.templates.blend;
                for(const auto& primitive:armyWorldTemplates->primitives[2])
                    cityAccessories.templates.draws.push_back(armyModels.templates.draws.at(primitive.sourceTemplate));
                areaAccessories.templates=cityAccessories.templates;
                sharedCityAccessories=prepareNativeSharedAccessories(cityAccessories.population,false);
                sharedAreaAccessories=prepareNativeSharedAccessories(areaAccessories.population,true);
            }
            armyReturnedPhases.assign(armyModels.source.actors.size(),0.f);
        }
    }

    void publishArmy(const std::optional<ArmyWorldPublication>& world) {
        if(sourceArmyVisuals)armyModelPool->snapshot(armyModels,armyWorldState);
        if(sourceArmyVisuals){
            observeArmyUnitHeightRoster(armyUnitHeights,world->unitCenters);
            for(const auto& [id,current]:world->unitModelModes){
                const auto selection=std::pair{current,world->unitNavalFormations.at(id)};
                auto previous=armyUnitSelections.try_emplace(id,std::pair{0u,0u}).first;
                if(previous->second==selection)continue;
                // CE1C8D30 clears the old descriptor chains immediately,
                // before visibility/admission or the next model acquisition.
                if(current==1){
                    for(int descriptor:{0x750,0x778,0x7C8,0x840})
                        armyModelPool->releaseDescriptor(GraphicsModelFamily::Army,id,descriptor);
                    if(armyWorldNavalCategory(selection.second)<0)
                        armyModelPool->releaseDescriptor(GraphicsModelFamily::Army,id,0x818);
                    armyModelPool->releaseDescriptor(GraphicsModelFamily::Army,id,0x7A0);
                }else if(current==2)for(int descriptor:{0x750,0x778,0x7C8,0x818,0x840})
                    armyModelPool->releaseDescriptor(GraphicsModelFamily::Army,id,descriptor);
                else for(int descriptor:{0x818,0x7A0,0x840})
                    armyModelPool->releaseDescriptor(GraphicsModelFamily::Army,id,descriptor);
                previous->second=selection;armyUnitHeights[id].dirty30=true;
            }
            for(auto it=armyUnitSelections.begin();it!=armyUnitSelections.end();)
                if(!world->unitCenters.contains(it->first))it=armyUnitSelections.erase(it);else ++it;
        }
    }

    void observeCityAccessories(const source_accessory_population::Profile& prior,const source_accessory_population::Profile& current) {
        observeNativeSharedAccessorySource(sharedCityAccessories,prior,current);
    }

    void observeAreaAccessories(const source_accessory_population::Profile& prior,const source_accessory_population::Profile& current) {
        observeNativeSharedAccessorySource(sharedAreaAccessories,prior,current);
    }

    void synchronize(ID3D11Device* device,const ArmyWorldPublication& candidates) {

        if(!sourceArmyVisuals)return;
        auto& pool=*armyModelPool;
        pool.releaseAbsent(candidates.unitCenters);
        const bool bindingsChanged=armyBindingRevision!=pool.bindingRevision;
        if(armyCandidateRevision==candidates.revision&&!bindingsChanged&&
            armyWorldState.appliedMaterialRevision==armyWorldTemplates->materialRevision)return;
        ArmyWorldPublication active=candidates;active.revision=++armyRenderRevision;active.actors.clear();
        for(const auto& candidate:candidates.actors){
            auto key=candidate.key;
            if(key.descriptor==0x7A0||key.descriptor==0x818){auto member=pool.member(GraphicsModelFamily::Army,key.unit,key.descriptor,key.instance);
                if(!member)continue;key=*member;}
            auto* slot=pool.find(key);if(!slot)continue;
            auto current=candidate;current.key=key;current.action=armyWorldTemplates->defaultActionOrdinals[key.category];
            current.world=slot->presented.world;current.bounds=slot->presented.bounds;
            active.actors.push_back(current);
        }
        // A logical key can bind another physical slot after leaving view.
        // Draw objects belong to that slot, rather than the previous key.
        if(bindingsChanged)armyWorldState.primitiveIndices.clear();
        publishArmyWorldPopulation(armyModels,armyWorldState,*armyWorldTemplates,active,
            [&](const ArmyWorldActorKey& key,unsigned primitive,unsigned index,const std::string& identity){
                auto& model=resolveNativePhysicalModel(device,*pool.find(key),primitive,armyModels.templates.draws.at(index),
                    armyModels.source.vertices.at(index));
                // Material changes preserve the physical draw and its private CBs.
                const auto& material=armyModels.templates.draws.at(index);
                model.textures=material.textures;model.samplers=material.samplers;
                auto draw=model;draw.identity=identity;return draw;
            });
        armyReturnedPhases.resize(armyModels.poses.size());armySuccessfulSamples.assign(armyModels.poses.size(),false);
        armyActivePoses.resize(armyModels.poses.size());armyMainMembership.assign(armyModels.poses.size(),false);
        for(const auto& [key,index]:armyWorldState.actorIndices){
            auto& slot=*pool.find(key);auto& actor=armyModels.source.actors[index];
            actor.world=slot.presented.world;actor.obb=slot.presented.bounds;
            armyModels.poses[index]=slot.pose;armyModels.poseCaches[index]=slot.cache;
            const auto& clock=slot.clock;int raw=clock.poseRawAction;
            const auto& action=armyGraphicsActions->rows.at({unsigned(key.category),unsigned(raw)});
            armyActivePoses[index]=action.pose?&*action.pose:&armyModels.source.poses[key.category];
            armyReturnedPhases[index]=clock.returnedPhase;armySuccessfulSamples[index]=clock.hasSuccessfulSample;
            // Ordinary TGK21 v28 publishes750/778/7C8. Banner7F0 requires
            // the independent retiring branch, which is not this population.
            armyMainMembership[index]=key.category!=2;
        }
        armyCandidateRevision=candidates.revision;armyBindingRevision=pool.bindingRevision;

        }

    void selectCamera(ID3D11DeviceContext* context,const source_camera::Frame& staticCamera,
        NativeCityAccessoryGpu& cityAccessories,NativeCityAccessoryGpu& areaAccessories,
        const source_view::Profile& viewProfile,std::vector<unsigned>& ordinaryPopulationCounts,unsigned& accessoryOrdinaryCount) {
        if(sourceAccessoryVisuals) {
            accessoryOrdinaryCount=selectNativeSharedAccessories(context,*armyModelPool,
                sharedAreaAccessories,areaAccessories,staticCamera);
            accessoryOrdinaryCount+=selectNativeSharedAccessories(context,*armyModelPool,
                sharedCityAccessories,cityAccessories,staticCamera);
            for(unsigned i=0;i<viewProfile.records.size();++i)
                if(viewProfile.records[i].mask==1)ordinaryPopulationCounts[i]+=accessoryOrdinaryCount;
        }
        if(sourceArmyVisuals) {
            armyPlan=selectNativeArmyPopulation(armyModels,staticCamera,armyReturnedPhases,
                &armySuccessfulSamples,&armyActivePoses,&armyMainMembership
                );
            for(unsigned i=0;i<viewProfile.records.size();++i)
                for(unsigned j=0;j<armyModels.source.recordMasks.size();++j)
                    if(viewProfile.records[i].mask==armyModels.source.recordMasks[j])ordinaryPopulationCounts[i]+=armyPlan.ordinaryCounts[j];
        }
    }

    void requireAnimationTiming(const source_animation_cadence::Profile& animationFps)const {
    if((sourceArmyVisuals||sourceAccessoryVisuals)&&armyModels.source.defaultActions.fps!=animationFps.fps)
        throw std::runtime_error("Army and App imported FPS tables differ");
    }

    void updateAnimation(ID3D11Device* device,ID3D11DeviceContext* context,
        const std::optional<ArmyWorldPublication>& world,const RenderInput& input,
        const source_camera::Frame& eventCamera,const source_camera::CurrentRenderMode& mode,
        const source_animation_cadence::Profile& animationFps,unsigned fpsMode,
        NativeCityAccessoryGpu& cityAccessories,NativeCityAccessoryGpu& areaAccessories,AnimationState& state) {
        resetNativeArmyAnimationGroups(armyAnimationGroups); // one manager, including an empty Army
        if(sourceArmyVisuals) {
            std::map<int32_t,std::vector<const ArmyWorldActor*>> candidates;
            for(const auto& actor:world->actors)candidates[actor.key.unit].push_back(&actor);
            std::array<std::vector<NativeAccessoryPoseJoint>,7> sampledGroupLocals;
            auto materialReady=[&](const ArmyWorldActor& actor){
                for(const auto& primitive:armyWorldTemplates->primitives[unsigned(actor.key.category)])
                    if(primitive.clothMaterial&&!armyWorldTemplates->materials.count({primitive.sourceTemplate,actor.material}))return false;
                return true;
            };
            for(const auto& [id,center]:world->unitCenters) {
                const unsigned modelMode=world->unitModelModes.at(id);
                const bool siege=modelMode==2,naval=modelMode==1;
                auto& height=armyUnitHeights[id];height.observeMotion(world->unitMotionRevisions.at(id));
                auto currentCenter=center;currentCenter[1]=height.y14;
                auto gate=std::floor(mode.zoomLevel)<4?source_accessory_population::unitAdmission(currentCenter,
                    reinterpret_cast<const char*>(eventCamera.prefix.data()),reinterpret_cast<const char*>(eventCamera.prefix.data()),
                    mode.sceneMode,mode.zoomLevel,input.controller.distance):source_accessory_population::UnitAdmission{};
                const bool reached=gate.visible||armyPriorVisible[id];armyPriorVisible[id]=gate.visible;
                unsigned resolved=0;
                if(reached){
                    if(!gate.visible)armyModelPool->releaseUnit(id);
                    else for(int descriptor:{0x750,0x778,0x7A0,0x7C8,0x7F0,0x818,0x840})
                    for(const auto* actor:candidates[id])if(actor->key.descriptor==descriptor&&materialReady(*actor)){
                        const bool existing=(actor->key.descriptor==0x7A0||actor->key.descriptor==0x818)&&armyModelPool->member(GraphicsModelFamily::Army,
                            id,actor->key.descriptor,actor->key.instance).has_value();
                        if(!existing&&armyModelPool->acquire(actor->key))height.dirty30=true;
                        if((naval?actor->key.descriptor==0x818:siege?actor->key.descriptor==0x7A0:actor->key.category<2)&&
                            (existing||armyModelPool->find(actor->key)))++resolved;
                    }
                }
                const bool dirtyBeforeV30=height.dirty30;
                // Imported siege nodes are all ready; absent list nodes do not
                // mark remainingDirty in this source branch.
                const bool setter=height.updateGround(reached,true,world->unitGroundHeights.at(id),
                    naval?resolved==1:siege||resolved==19,modelMode);
                synchronize(device,*world);
                // Complete this unit before the next caller can take any slots.
                float siegeChainFrame=-1,navalChainFrame=-1;
                for(const auto* candidate:candidates[id]) {
                auto actor=*candidate;
                if(actor.key.descriptor==0x7A0||actor.key.descriptor==0x818){
                    auto member=armyModelPool->member(GraphicsModelFamily::Army,id,actor.key.descriptor,actor.key.instance);
                    if(!member)continue;actor.key=*member;
                }
                auto index=armyWorldState.actorIndices.find(actor.key);if(index==armyWorldState.actorIndices.end())continue;
                auto& presented=armyModels.source.actors[index->second];
                if(setter){
                    if(actor.key.descriptor==0x7A0||actor.key.descriptor==0x818){
                        actor.world.scale=presented.world.scale; // v30 does not write scale.
                        actor.world.translation.y=height.y14;
                        actor.bounds=buildArmyWorldModeBounds(armyWorldTemplates->localBounds.at(unsigned(actor.key.category)),actor.world);
                    }
                    publishArmyWorldPresentation(presented,actor);
                }
                const unsigned category=unsigned(actor.key.category);
                const bool siegeBody=category>=4&&category<=6,navalBody=category>=7&&category<=9;
                const unsigned graphics=unsigned(world->graphicsActions.at(id));
                const bool attacking=graphics==2&&world->unitSelectedFormations.at(id)!=8;
                const unsigned raw=category<2?graphics:siegeBody&&attacking?2u:0u;
                auto actionRow=armyGraphicsActions->rows.find({category,raw});
                const auto* action=actionRow==armyGraphicsActions->rows.end()?nullptr:&actionRow->second;
                auto& physical=*armyModelPool->find(actor.key);auto& player=physical.clock;
                bool eligible=reached;
                if(category==2||category==3)eligible=eligible&&gate.animateNear;
                if(category==3)eligible=eligible&&std::floor(mode.zoomLevel)<1;
                if(navalBody)eligible=eligible&&gate.animateNear;
                if(siegeBody)eligible=eligible&&(graphics==2?
                    world->unitSelectedFormations.at(id)!=8:dirtyBeforeV30);
                const NativeArmyAnimationCallback callback{true,eligible,int(raw),
                    siegeBody?siegeChainFrame:navalBody?navalChainFrame:-1,fpsMode};
                const auto step=action?consumeNativeArmyAnimation(action->player,armyModels.source.defaultActions.fps,category,
                    player,armyAnimationGroups,callback,
                    [&](float requested,bool){return std::optional<float>(source_default_player_phase::defaultReturnedPhase(
                        {action->player.clipDuration,action->player.loopDeclaration},requested));}):
                    consumeNativeArmyUnavailableAction(player,callback);
                if(siegeBody&&eligible)siegeChainFrame=step.nextFrame;
                if(navalBody&&eligible)navalChainFrame=step.nextFrame;
                armyReturnedPhases[index->second]=player.returnedPhase;
                armySuccessfulSamples[index->second]=player.hasSuccessfulSample;
                if(step.succeeded){
                    const auto* pose=action->pose?&*action->pose:&armyModels.source.poses[category];
                    armyActivePoses[index->second]=pose;
                    // Consume every accepted action/phase in event order.
                    // Sparse local writes must survive A→B→A before one draw.
                    auto& owner=armyModels.poses[index->second];auto& cache=armyModels.poseCaches[index->second];
                    const int slot=action->player.grouped?nativeArmyAnimationGroupSlot(category,raw):-1;
                    if(step.cached)
                        publishNativeArmySharedLocal(*pose,owner,cache,sampledGroupLocals[size_t(slot)],
                            player.returnedPhase,presented.world);
                    else{
                        prepareNativeArmyCurrentPose(*pose,owner,cache,player.returnedPhase,presented.world,true,category>=2);
                        if(slot>=0)sampledGroupLocals[size_t(slot)]=owner.frame.localPose;
                    }
                }
                physical.presented={presented.world,presented.obb};
                physical.pose=armyModels.poses[index->second];physical.cache=armyModels.poseCaches[index->second];
                }
            }
            for(auto it=armyPriorVisible.begin();it!=armyPriorVisible.end();)
                if(!world->unitCenters.count(it->first))it=armyPriorVisible.erase(it);else ++it;
        }
        if(sourceAccessoryVisuals) {
            const auto& action=armyGraphicsActions->rows.at({2,0});
            updateNativeSharedAccessoryFamily(device,*armyModelPool,sharedAreaAccessories,areaAccessories,
                GraphicsModelFamily::AreaCapital,eventCamera,mode.sceneMode,mode.zoomLevel,
                input.controller.distance,action,armyModels.source.poses[2],animationFps.fps,fpsMode,armyAnimationGroups);
            updateNativeSharedAccessoryFamily(device,*armyModelPool,sharedCityAccessories,cityAccessories,
                GraphicsModelFamily::City,eventCamera,mode.sceneMode,mode.zoomLevel,
                input.controller.distance,action,armyModels.source.poses[2],animationFps.fps,fpsMode,armyAnimationGroups);
            updateNativeSharedAccessoryFamily(device,*armyModelPool,sharedCityAccessories,cityAccessories,
                GraphicsModelFamily::Gate,eventCamera,mode.sceneMode,mode.zoomLevel,
                input.controller.distance,action,armyModels.source.poses[2],animationFps.fps,fpsMode,armyAnimationGroups);
        }
        ++state.updates;state.fpsMode=fpsMode;
        state.armyPlayers=sourceArmyVisuals?unsigned(armyWorldState.actorIndices.size()):0;
        state.cityGroups=sourceAccessoryVisuals?unsigned(armyModelPool->boundCount(GraphicsModelFamily::City)+
            armyModelPool->boundCount(GraphicsModelFamily::Gate)):0;
        state.areaGroups=sourceAccessoryVisuals?unsigned(armyModelPool->boundCount(GraphicsModelFamily::AreaCapital)):0;
        state.firstBodyPhase=state.firstFlagPhase=0;
        bool body=false,flag=false;
        if(sourceArmyVisuals)for(const auto& actor:world->actors){
            auto key=actor.key;
            if(key.descriptor==0x7A0||key.descriptor==0x818){auto member=armyModelPool->member(GraphicsModelFamily::Army,key.unit,key.descriptor,key.instance);
                if(!member)continue;key=*member;}
            if(auto* slot=armyModelPool->find(key)) {
            if((key.category<2||key.category>=4)&&!body){state.firstBodyPhase=slot->clock.returnedPhase;body=true;}
            if(actor.key.category==2&&!flag){state.firstFlagPhase=slot->clock.returnedPhase;flag=true;}
            }
        }
    }

    std::vector<ArmyLabelUnitPosition> labelPositions(const ArmyWorldPublication& world)const {
        return readArmyLabelUnitPositions(world.unitCenters,armyUnitHeights);
    }

    void drawOpaque(ID3D11DeviceContext* context,ID3D11RenderTargetView* const* colors,
        ID3D11DepthStencilView* depth,const D3D11_VIEWPORT& viewport,
        NativeCityAccessoryGpu& cityAccessories,NativeCityAccessoryGpu& areaAccessories) {
            if(sourceArmyVisuals) {
                armyQueues=updateSelectNativeArmyPopulation(context,armyModels,armyPlan);
                drawNativeModels(context,armyQueues.ordinary49,colors,depth,viewport);
                drawNativeModels(context,armyQueues.alpha50,colors,depth,viewport);
                drawNativeModels(context,armyQueues.ordinary54,colors,depth,viewport);
                drawNativeModels(context,armyQueues.alpha55,colors,depth,viewport);

            }
            if(sourceAccessoryVisuals) {
                drawNativeSharedAccessories(context,*armyModelPool,sharedAreaAccessories,areaAccessories,colors,depth,viewport);
                drawNativeSharedAccessories(context,*armyModelPool,sharedCityAccessories,cityAccessories,colors,depth,viewport);
            }
    }

private:
    const bool sourceArmyVisuals,sourceAccessoryVisuals;
    NativeArmyPopulation armyModels;NativeArmyPlan armyPlan;NativeArmyQueues armyQueues;
    std::vector<float> armyReturnedPhases;
    std::optional<ArmyWorldTemplateLibrary> armyWorldTemplates;
    ArmyWorldPopulationState armyWorldState;
    NativeArmyAnimationGroups armyAnimationGroups;
    std::optional<ArmyWorldModelPool<NativeArmyPopulation>> armyModelPool;
    NativeSharedAccessories sharedCityAccessories,sharedAreaAccessories;
    uint64_t armyCandidateRevision{},armyBindingRevision{},armyRenderRevision{};
    std::map<int32_t,bool> armyPriorVisible;
    std::map<int32_t,std::pair<uint32_t,uint32_t>> armyUnitSelections;
    std::map<int32_t,ArmyUnitHeight> armyUnitHeights;
    std::vector<bool> armySuccessfulSamples;
    std::optional<NativeArmyGraphicsActions> armyGraphicsActions;
    std::vector<const NativeAccessoryPoseSource*> armyActivePoses;
    std::vector<bool> armyMainMembership;
};
} // namespace San14::Render
