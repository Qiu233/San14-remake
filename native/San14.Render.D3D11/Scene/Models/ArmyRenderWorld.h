#pragma once
#include "ArmyPopulation.h"
#include "../View/MapViewSettings.h"
#include "ArmyAnimation.h"
#include "ArmyTargetReaction.h"
#include "ArmyGraphicsActions.h"
#include "ArmyWorldMaterialImport.h"
#include "ArmyWorldMaterialBank.h"
#include "ArmyMaterialResourceContext.h"
#include "ArmyCategoryResources.h"
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
        if(sourceArmyVisuals) {
            const auto armyDirectory=resources.directory(ResourceGroup::Armies);
            accessoryResources=cityAccessories.resources?cityAccessories.resources:
                prepareNativeCategory2Resources(device,resources.directory(ResourceGroup::BaseAccessories));
            auto bank=prepareNativeArmyResourceBank(device,armyDirectory,*accessoryResources);
            armyModels=std::move(bank.population);armyWorldTemplates.emplace(std::move(bank.library));
            auto modelCapacities=std::move(bank.capacities);
            armyGraphicsActions=loadNativeArmyGraphicsActions(resources.directory(ResourceGroup::ArmyActions),
                armyModels.source.poses,armyModels.source.defaultActions.rows);
            armyResourceSnapshots.emplace(resources.directory(ResourceGroup::MaterialResourceContext));
            armyResourceContext.emplace();
            const auto materialInitialization=readNativeArmyManagerInitialization(armyDirectory/"material-source");
            const auto initialSnapshot=armyResourceSnapshots->read(materialInitialization.generation);
            armyMaterials.emplace(device,armyDirectory/"material-source",armyModels,*armyWorldTemplates,*armyResourceContext,
                materialInitialization,initialSnapshot);
            armyModelPool.emplace(modelCapacities,
                armyWorldTemplates->constructors,armyModels.source.poses,armyModels.source.defaultActions);
            armyReturnedPhases.assign(armyModels.source.actors.size(),0.f);
        }
        else if(sourceAccessoryVisuals) {
            const auto& category=*cityAccessories.resources;
            armyModelPool.emplace(2,category.capacity,category.constructor,category.pose,category.defaultAction);
            armyGraphicsActions.emplace();NativeArmyGraphicsAction action;action.player=category.defaultAction;
            armyGraphicsActions->rows.emplace(std::pair{2u,0u},std::move(action));
        }
        if(sourceAccessoryVisuals) {
            sharedCityAccessories=prepareNativeSharedAccessories(cityAccessories.population,false);
            sharedAreaAccessories=prepareNativeSharedAccessories(areaAccessories.population,true);
            accessoryResources=cityAccessories.resources;
        }
    }

    void publishArmy(ID3D11Device* device,ID3D11DeviceContext* context,const std::optional<ArmyWorldPublication>& world) {
        if(!sourceArmyVisuals)return;
        if(!world) {
            if(armyOwnerLive)throw std::runtime_error("Live Army population lost its publication owner");
            return;
        }
        if(sourceArmyVisuals)armyModelPool->snapshot(armyModels,armyWorldState);
        if(sourceArmyVisuals){
            if(!armyResourceContext->isCurrentGeneration(world->contextGeneration))
                armyResourceContext->selectSnapshot(armyResourceSnapshots->read(world->contextGeneration));
            armyMaterials->prepareFreshWorldMaterials(device,context,armyModels,*armyWorldTemplates,*world);
            publishViewOwners(*world);
            const auto& views=armyManagedWorld;
            observeArmyUnitHeightRoster(armyUnitHeights,views.unitCenters);
            for(const auto& [id,current]:views.unitModelModes){
                const auto selection=std::pair{current,views.unitNavalFormations.at(id)};
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
                if(!views.unitCenters.contains(it->first))it=armyUnitSelections.erase(it);else ++it;
        }
        armyOwnerLive=true;
    }

    bool hasViewRetirementAdmission()const noexcept {return armyRetirementAdmissionPending;}

    void admitViewRetirements(const RenderInput& input,const source_camera::Prefix& camera,
        const source_camera::CurrentRenderMode& mode){
        if(!armyRetirementAdmissionPending)return;
        std::vector<int32_t> immediate;
        for(int32_t key:armyViewOrder){
            auto& view=armyViews.at(key);if(view.retirement!=ArmyViewLifetime::AwaitVisibility)continue;
            auto position=view.center;
            if(auto height=armyUnitHeights.find(key);height!=armyUnitHeights.end())position[1]=height->second.y14;
            const auto visible=source_accessory_population::unitAdmission(position,
                reinterpret_cast<const char*>(camera.data()),reinterpret_cast<const char*>(camera.data()),
                mode.sceneMode,mode.zoomLevel,input.controller.distance).visible;
            if(!visible){immediate.push_back(key);continue;}
            view.retirement=ArmyViewLifetime::Retiring;
            auto& reaction=armyReactions[key];reaction.pending=std::max(reaction.pending,3u);
        }
        for(int32_t key:immediate)releaseViewOwner(key);
        armyRetirementAdmissionPending=false;
        if(!immediate.empty())rebuildManagedWorld();
    }

    void observeCityAccessories(const source_accessory_population::Profile& prior,const source_accessory_population::Profile& current) {
        observeNativeSharedAccessorySource(sharedCityAccessories,prior,current);
    }

    void observeAreaAccessories(const source_accessory_population::Profile& prior,const source_accessory_population::Profile& current) {
        observeNativeSharedAccessorySource(sharedAreaAccessories,prior,current);
    }

    void synchronize(ID3D11Device* device,const ArmyWorldPublication&) {
        const auto& candidates=armyManagedWorld;

        if(!hasActiveArmyOwner())return;
        auto& pool=*armyModelPool;
        pool.releaseAbsent(candidates.unitCenters);
        for(auto it=armyReactions.begin();it!=armyReactions.end();)
            if(!candidates.unitCenters.contains(it->first))it=armyReactions.erase(it);else ++it;
        const bool bindingsChanged=armyBindingRevision!=pool.bindingRevision;
        if(armyCandidateRevision==candidates.revision&&armyAppliedReactionRevision==armyReactionRevision&&!bindingsChanged&&
            armyWorldState.appliedMaterialRevision==armyWorldTemplates->materialRevision){acceptTargetReactions(candidates);return;}
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
        for(const auto& [id,reaction]:armyReactions)for(unsigned i=0;i<reaction.count();++i){
            if(reaction.slots[i].phase!=1)continue;
            ArmyWorldActorKey key{id,int32_t(reaction.category()),reaction.descriptor(),int32_t(i)};
            auto* slot=pool.find(key);if(!slot)continue;
            ArmyWorldActor current;current.key=key;current.action=armyGraphicsActions->rows.at({reaction.category(),3u}).player.ordinal;
            current.world=slot->presented.world;current.bounds=slot->presented.bounds;active.actors.push_back(current);
        }
        // A logical key can bind another physical slot after leaving view.
        // Draw objects belong to that slot, rather than the previous key.
        if(bindingsChanged)armyWorldState.primitiveIndices.clear();
        publishArmyWorldPopulation(armyModels,armyWorldState,*armyWorldTemplates,active,
            [&](const ArmyWorldActorKey& key,unsigned primitive,unsigned index,const std::string& identity){
                auto& model=resolveNativePhysicalModel(device,*pool.find(key),primitive,armyModels.templates.draws.at(index),
                    armyModels.source.vertices.at(index));
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
            //1C6FD0 admits the actual7F0 member only for a real retiring
            // manager view. Active ordinary banner membership stays excluded.
            armyMainMembership[index]=key.category!=2||
                (key.descriptor==0x7F0&&armyViews.at(key.unit).retirement!=ArmyViewLifetime::Attached);
        }
        armyCandidateRevision=candidates.revision;armyBindingRevision=pool.bindingRevision;armyAppliedReactionRevision=armyReactionRevision;
        acceptTargetReactions(candidates);

        }

    void selectCamera(ID3D11DeviceContext* context,const source_camera::Frame& staticCamera,
        NativeCityAccessoryGpu& cityAccessories,NativeCityAccessoryGpu& areaAccessories,
        const MapViewDefinition& viewDefinition,std::vector<unsigned>& ordinaryPopulationCounts,unsigned& accessoryOrdinaryCount) {
        if(sourceAccessoryVisuals) {
            accessoryOrdinaryCount=selectNativeSharedAccessories(context,*armyModelPool,
                sharedAreaAccessories,areaAccessories,staticCamera);
            accessoryOrdinaryCount+=selectNativeSharedAccessories(context,*armyModelPool,
                sharedCityAccessories,cityAccessories,staticCamera);
            for(unsigned i=0;i<viewDefinition.records.size();++i)
                if(viewDefinition.records[i].groupMask==1)ordinaryPopulationCounts[i]+=accessoryOrdinaryCount;
        }
        if(hasActiveArmyOwner()) {
            armyPlan=selectNativeArmyPopulation(armyModels,staticCamera,armyReturnedPhases,
                &armySuccessfulSamples,&armyActivePoses,&armyMainMembership
                );
            for(unsigned i=0;i<viewDefinition.records.size();++i)
                for(unsigned j=0;j<armyModels.source.recordMasks.size();++j)
                    if(viewDefinition.records[i].groupMask==armyModels.source.recordMasks[j])ordinaryPopulationCounts[i]+=armyPlan.ordinaryCounts[j];
        }
    }

    void requireAnimationTiming(const source_animation_cadence::Profile& animationFps)const {
    if((sourceArmyVisuals&&armyModels.source.defaultActions.fps!=animationFps.fps)||
        (sourceAccessoryVisuals&&accessoryResources->fps!=animationFps.fps))
        throw std::runtime_error("Army and App imported FPS tables differ");
    }

    void updateAnimation(ID3D11Device* device,ID3D11DeviceContext* context,
        const std::optional<ArmyWorldPublication>&,const RenderInput& input,
        const source_camera::Prefix& eventCameraPrefix,const source_camera::CurrentRenderMode& mode,
        const source_animation_cadence::Profile& animationFps,unsigned fpsMode,
        NativeCityAccessoryGpu& cityAccessories,NativeCityAccessoryGpu& areaAccessories,AnimationState& state) {
        admitViewRetirements(input,eventCameraPrefix,mode);
        const auto* world=&armyManagedWorld;
        resetNativeArmyAnimationGroups(armyAnimationGroups); // one manager, including an empty Army
        if(hasActiveArmyOwner()) {
            std::array<std::vector<NativeAccessoryPoseJoint>,7> sampledGroupLocals;
            auto materialReady=[&](const ArmyWorldActor& actor){
                for(const auto& primitive:armyWorldTemplates->primitives[unsigned(actor.key.category)])
                    if(primitive.materialDependent&&!armyWorldTemplates->materials.count({primitive.sourceTemplate,actor.material}))return false;
                return true;
            };
            for(size_t viewIndex=0;viewIndex<armyViewOrder.size();) {
                const int32_t id=armyViewOrder[viewIndex];
                auto& viewOwner=armyViews.at(id);
                std::vector<const ArmyWorldActor*> candidates;candidates.reserve(viewOwner.actors.size());
                for(const auto& actor:viewOwner.actors)candidates.push_back(&actor);
                const auto& center=world->unitCenters.at(id);
                const unsigned modelMode=world->unitModelModes.at(id);
                const bool siege=modelMode==2,naval=modelMode==1;
                auto& height=armyUnitHeights[id];height.observeMotion(world->unitMotionRevisions.at(id));
                auto currentCenter=center;currentCenter[1]=height.y14;
                auto gate=std::floor(mode.zoomLevel)<4?source_accessory_population::unitAdmission(currentCenter,
                    reinterpret_cast<const char*>(eventCameraPrefix.data()),reinterpret_cast<const char*>(eventCameraPrefix.data()),
                    mode.sceneMode,mode.zoomLevel,input.controller.distance):source_accessory_population::UnitAdmission{};
                if(viewOwner.retirement!=ArmyViewLifetime::Attached){
                    //1C3EA0 uses the source visibility predicate, independently
                    // of LOD detail or gameplay map-selection visibility.
                    gate=source_accessory_population::unitAdmission(currentCenter,
                        reinterpret_cast<const char*>(eventCameraPrefix.data()),reinterpret_cast<const char*>(eventCameraPrefix.data()),
                        mode.sceneMode,mode.zoomLevel,input.controller.distance);
                }
                const bool reached=gate.visible||armyPriorVisible[id];armyPriorVisible[id]=gate.visible;
                unsigned resolved=0;
                if(reached){
                    if(!gate.visible)armyModelPool->releaseUnit(id);
                    else for(int descriptor:{0x750,0x778,0x7A0,0x7C8,0x7F0,0x818,0x840})
                    for(const auto* actor:candidates)if(actor->key.descriptor==descriptor&&materialReady(*actor)){
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
                // Source v30 completes this unit's root/OBB writes before1CA530,
                // then its ordinary players run, before the next unit can borrow.
                if(setter)for(const auto* candidate:candidates){
                    auto actor=*candidate;
                    if(actor.key.descriptor==0x7A0||actor.key.descriptor==0x818){
                        auto member=armyModelPool->member(GraphicsModelFamily::Army,id,actor.key.descriptor,actor.key.instance);
                        if(!member)continue;actor.key=*member;
                    }
                    const auto index=armyWorldState.actorIndices.find(actor.key);if(index==armyWorldState.actorIndices.end())continue;
                    auto& presented=armyModels.source.actors[index->second];
                    if(actor.key.descriptor==0x7A0||actor.key.descriptor==0x818){
                        actor.world.scale=presented.world.scale; // v30 does not write scale.
                        actor.world.translation.y=height.y14;
                        actor.bounds=buildArmyWorldModeBounds(armyWorldTemplates->localBounds.at(unsigned(actor.key.category)),actor.world);
                    }
                    publishArmyWorldPresentation(presented,actor);
                    armyModelPool->find(actor.key)->presented={presented.world,presented.obb};
                }
                auto actualUnitPosition=center;actualUnitPosition[1]=height.y14;
                updateTargetReaction(device,*world,id,gate.visible,actualUnitPosition,fpsMode,sampledGroupLocals);
                // Complete this unit before the next caller can take any slots.
                float siegeChainFrame=-1,navalChainFrame=-1;
                for(const auto* candidate:candidates) {
                auto actor=*candidate;
                if(actor.key.descriptor==0x7A0||actor.key.descriptor==0x818){
                    auto member=armyModelPool->member(GraphicsModelFamily::Army,id,actor.key.descriptor,actor.key.instance);
                    if(!member)continue;actor.key=*member;
                }
                auto index=armyWorldState.actorIndices.find(actor.key);if(index==armyWorldState.actorIndices.end())continue;
                auto& presented=armyModels.source.actors[index->second];
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
                auto& retainedCache=armyModels.poseCaches[index->second];
                if(retainedCache.pendingPoolSnapshot){
                    physical.pose=armyModels.poses[index->second];
                    retainedCache.pendingPoolSnapshot=false;
                }
                physical.presented={presented.world,presented.obb};physical.cache=retainedCache;
                }
                if(viewOwner.retirement==ArmyViewLifetime::Retiring){
                    const auto reaction=armyReactions.find(id);
                    const bool empty=reaction==armyReactions.end()||(!reaction->second.active&&!reaction->second.pending);
                    if((modelMode==0&&empty)||((modelMode==1||modelMode==2)&&gate.visible)){
                        //1CBBB0 returns this exact finished view before the next
                        // manager entry can borrow from the shared model banks.
                        releaseViewOwner(id);rebuildManagedWorld();synchronize(device,armyManagedWorld);
                        continue;
                    }
                }
                ++viewIndex;
            }
            for(auto it=armyPriorVisible.begin();it!=armyPriorVisible.end();)
                if(!world->unitCenters.count(it->first))it=armyPriorVisible.erase(it);else ++it;
        }
        if(sourceAccessoryVisuals) {
            const auto& action=armyGraphicsActions->rows.at({2,0});
            updateNativeSharedAccessoryFamily(device,*armyModelPool,sharedAreaAccessories,areaAccessories,
                GraphicsModelFamily::AreaCapital,eventCameraPrefix,mode.sceneMode,mode.zoomLevel,
                input.controller.distance,action,accessoryResources->pose,animationFps.fps,fpsMode,armyAnimationGroups);
            updateNativeSharedAccessoryFamily(device,*armyModelPool,sharedCityAccessories,cityAccessories,
                GraphicsModelFamily::City,eventCameraPrefix,mode.sceneMode,mode.zoomLevel,
                input.controller.distance,action,accessoryResources->pose,animationFps.fps,fpsMode,armyAnimationGroups);
            updateNativeSharedAccessoryFamily(device,*armyModelPool,sharedCityAccessories,cityAccessories,
                GraphicsModelFamily::Gate,eventCameraPrefix,mode.sceneMode,mode.zoomLevel,
                input.controller.distance,action,accessoryResources->pose,animationFps.fps,fpsMode,armyAnimationGroups);
        }
        ++state.updates;state.fpsMode=fpsMode;
        state.armyPlayers=hasActiveArmyOwner()?unsigned(armyWorldState.actorIndices.size()):0;
        state.cityGroups=sourceAccessoryVisuals?unsigned(armyModelPool->boundCount(GraphicsModelFamily::City)+
            armyModelPool->boundCount(GraphicsModelFamily::Gate)):0;
        state.areaGroups=sourceAccessoryVisuals?unsigned(armyModelPool->boundCount(GraphicsModelFamily::AreaCapital)):0;
        state.firstBodyPhase=state.firstFlagPhase=0;
        bool body=false,flag=false;
        if(hasActiveArmyOwner())for(const auto& actor:world->actors){
            auto key=actor.key;
            if(key.descriptor==0x7A0||key.descriptor==0x818){auto member=armyModelPool->member(GraphicsModelFamily::Army,key.unit,key.descriptor,key.instance);
                if(!member)continue;key=*member;}
            if(auto* slot=armyModelPool->find(key)) {
            if((key.category<2||key.category>=4)&&!body){state.firstBodyPhase=slot->clock.returnedPhase;body=true;}
            if(actor.key.category==2&&!flag){state.firstFlagPhase=slot->clock.returnedPhase;flag=true;}
            }
        }
    }

    std::vector<ArmyTargetReactionReceipt> targetReactionReceipts()const {return armyReactionReceipts;}
    std::vector<ArmyViewRetirementReceipt> viewRetirementReceipts()const {return armyRetirementReceipts;}

    std::vector<ArmyLabelUnitPosition> labelPositions(const ArmyWorldPublication& world)const {
        return readArmyLabelUnitPositions(world.unitCenters,armyUnitHeights);
    }

    void drawOpaque(ID3D11DeviceContext* context,ID3D11RenderTargetView* const* colors,
        ID3D11DepthStencilView* depth,const D3D11_VIEWPORT& viewport,
        NativeCityAccessoryGpu& cityAccessories,NativeCityAccessoryGpu& areaAccessories) {
            if(hasActiveArmyOwner()) {
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

    bool hasActiveArmyOwner() const noexcept {return sourceArmyVisuals&&armyOwnerLive;}

    const NativeArmyPopulation* completeReflectionPopulation() const {return hasActiveArmyOwner()?&armyModels:nullptr;}
    const NativeArmyPlan* currentReflectionPlan() const {return hasActiveArmyOwner()?&armyPlan:nullptr;}
    const ArmyWorldPopulationState* currentReflectionRoster() const {return hasActiveArmyOwner()?&armyWorldState:nullptr;}

private:
    enum class ArmyViewLifetime {Attached,AwaitVisibility,Retiring};
    struct ArmyViewOwner {
        int32_t logicalArmy{};
        // The manager lifetime is distinct from the logical Army display
        // pointer. Private keys are never logical Army IDs or save state.
        ArmyViewLifetime retirement=ArmyViewLifetime::Attached;
        std::array<float,3> center{};
        float ground{},heading{};NativeAccessoryPoseVector rotation{};
        int32_t graphics{};uint32_t motion{},mode{},land{},naval{};
        std::vector<ArmyWorldActor> actors;
    };

    template<class Map>
    void detachViewEntry(Map& entries,int32_t from,int32_t to){
        auto node=entries.extract(from);if(node.empty())return;
        node.key()=to;entries.insert(std::move(node));
    }

    void releaseViewOwner(int32_t key){
        armyModelPool->releaseUnit(key);
        armyReactions.erase(key);armyUnitHeights.erase(key);armyUnitSelections.erase(key);armyPriorVisible.erase(key);
        armyViews.erase(key);std::erase(armyViewOrder,key);
    }

    void rebuildManagedWorld(){
        ArmyWorldPublication next;next.revision=++armyViewRevision;next.contextGeneration=armyViewContext;
        next.targetReactions=armyManagedWorld.targetReactions;
        for(int32_t key:armyViewOrder){
            const auto& view=armyViews.at(key);
            next.unitCenters.emplace(key,view.center);next.graphicsActions.emplace(key,view.graphics);
            next.unitGroundHeights.emplace(key,view.ground);next.unitMotionRevisions.emplace(key,view.motion);
            next.unitModelModes.emplace(key,view.mode);next.unitSelectedFormations.emplace(key,view.land);
            next.unitNavalFormations.emplace(key,view.naval);next.unitHeadings.emplace(key,view.heading);next.unitRotations.emplace(key,view.rotation);
            next.actors.insert(next.actors.end(),view.actors.begin(),view.actors.end());
        }
        armyManagedWorld=std::move(next);
    }

    bool acceptViewRetirements(const ArmyWorldPublication& publication){
        bool unchanged=publication.retirements.size()==armyRetirementReceipts.size();
        if(unchanged)for(size_t i=0;i<publication.retirements.size();++i){
            const auto& request=publication.retirements[i];const auto& prior=armyRetirementReceipts[i];
            if(request.requestId!=prior.requestId||request.army!=prior.army||prior.status==2){unchanged=false;break;}
        }
        if(unchanged)return false;
        std::map<uint64_t,ArmyViewRetirementReceipt> prior;
        for(const auto& receipt:armyRetirementReceipts)prior.emplace(receipt.requestId,receipt);
        std::vector<ArmyViewRetirementReceipt> receipts;bool changed=false;
        for(const auto& request:publication.retirements){
            auto known=prior.find(request.requestId);
            if(known!=prior.end()){
                if(known->second.army!=request.army)throw std::runtime_error("Army view retirement identity was reused");
                if(known->second.status!=2){receipts.push_back(known->second);continue;}
            }else if(request.requestId<=armyLastRetirementRequest)throw std::runtime_error("Army view retirement identity regressed");
            armyLastRetirementRequest=std::max(armyLastRetirementRequest,request.requestId);
            ArmyViewRetirementReceipt receipt{request.requestId,request.army,3};
            auto current=armyViews.find(request.army);
            if(current!=armyViews.end()){
                if(!(request.flags&1u))releaseViewOwner(request.army);
                else{
                    if(armyNextDetachedView==std::numeric_limits<int32_t>::min())throw std::runtime_error("Army manager view identity exhausted");
                    const int32_t key=--armyNextDetachedView;
                    armyModelPool->detachUnit(request.army,key);
                    detachViewEntry(armyReactions,request.army,key);detachViewEntry(armyUnitHeights,request.army,key);
                    detachViewEntry(armyUnitSelections,request.army,key);detachViewEntry(armyPriorVisible,request.army,key);
                    auto node=armyViews.extract(current);node.key()=key;auto& view=node.mapped();view.retirement=ArmyViewLifetime::AwaitVisibility;armyRetirementAdmissionPending=true;
                    if(auto height=armyUnitHeights.find(key);height!=armyUnitHeights.end())view.center[1]=height->second.y14;
                    for(auto& actor:view.actors)actor.key.unit=key;
                    armyViews.insert(std::move(node));
                    *std::find(armyViewOrder.begin(),armyViewOrder.end(),request.army)=key;
                }
                receipt.status=1;changed=true;
            }else{
                // A binding without its manager view is an unavailable owner,
                // not permission to invent a known-null display pointer.
                auto binding=armyModelPool->bindings.lower_bound({GraphicsModelFamily::Army,{request.army,0,0,0}});
                if(binding!=armyModelPool->bindings.end()&&binding->first.family==GraphicsModelFamily::Army&&binding->first.actor.unit==request.army)receipt.status=2;
            }
            receipts.push_back(receipt);
        }
        armyRetirementReceipts=std::move(receipts);return changed;
    }

    void publishViewOwners(const ArmyWorldPublication& publication){
        // Retire against the already-established old view before admitting any
        // replacement receiver in this exact post-reset source publication.
        bool changed=acceptViewRetirements(publication);
        if(armyManagedWorld.targetReactions!=publication.targetReactions)armyManagedWorld.targetReactions=publication.targetReactions;
        if(publication.revision!=armySourceRevision||publication.contextGeneration!=armyViewContext){
            for(const auto& [id,center]:publication.unitCenters){
                auto [entry,created]=armyViews.try_emplace(id);
                if(created)armyViewOrder.push_back(id);
                auto& view=entry->second;view.logicalArmy=id;view.center=center;
                view.ground=publication.unitGroundHeights.at(id);view.heading=publication.unitHeadings.at(id);view.rotation=publication.unitRotations.at(id);
                view.graphics=publication.graphicsActions.at(id);view.motion=publication.unitMotionRevisions.at(id);view.mode=publication.unitModelModes.at(id);
                view.land=publication.unitSelectedFormations.at(id);view.naval=publication.unitNavalFormations.at(id);view.actors.clear();
            }
            for(const auto& actor:publication.actors)armyViews.at(actor.key.unit).actors.push_back(actor);
            if(!publication.explicitViewRetirement){
                // Legacy9 has no semantic-retirement channel. Preserve its
                // original immediate roster lifetime for legacy callers only.
                std::vector<int32_t> absent;
                for(const auto& [key,view]:armyViews)if(key>0&&!publication.unitCenters.contains(key))absent.push_back(key);
                for(int32_t key:absent)releaseViewOwner(key);
            }
            armySourceRevision=publication.revision;armyViewContext=publication.contextGeneration;changed=true;
        }
        if(changed)rebuildManagedWorld();
    }

    std::map<int32_t,ArmyViewOwner> armyViews;
    // A detached view keeps its existing manager position; a replacement is a
    // new manager entry. World roster removal never changes update order.
    std::vector<int32_t> armyViewOrder;
    ArmyWorldPublication armyManagedWorld;
    std::vector<ArmyViewRetirementReceipt> armyRetirementReceipts;
    uint64_t armySourceRevision{},armyViewContext{},armyViewRevision{},armyLastRetirementRequest{};
    int32_t armyNextDetachedView{};
    bool armyRetirementAdmissionPending{};

    void acceptTargetReactions(const ArmyWorldPublication& world) {
        // Camera-only and repeated animation publications do not allocate or
        // queue accepted requests again. Missing owners can become present later.
        bool unchanged=world.targetReactions.size()==armyReactionReceipts.size();
        if(unchanged)for(size_t i=0;i<world.targetReactions.size();++i){
            const auto& request=world.targetReactions[i];const auto& receipt=armyReactionReceipts[i];
            if((receipt.status!=1&&receipt.status!=3)||request.requestId!=receipt.requestId||request.army!=receipt.army||request.variant!=receipt.variant){unchanged=false;break;}
        }
        if(unchanged)return;
        std::map<uint64_t,ArmyTargetReactionReceipt> prior;
        for(const auto& receipt:armyReactionReceipts)prior.emplace(receipt.requestId,receipt);
        std::vector<ArmyTargetReactionReceipt> next;next.reserve(world.targetReactions.size());
        for(const auto& request:world.targetReactions){
            auto known=prior.find(request.requestId);
            if(known!=prior.end()){
                if(known->second.army!=request.army||known->second.variant!=request.variant)
                    throw std::runtime_error("Army reaction request ID was reused for another source request");
                if(known->second.status==1||known->second.status==3){next.push_back(known->second);continue;}
            }else if(request.requestId<=armyLastReactionRequest)
                throw std::runtime_error("Army reaction request identity regressed in this render session");
            armyLastReactionRequest=std::max(armyLastReactionRequest,request.requestId);
            ArmyTargetReactionReceipt receipt{request.requestId,request.army,request.variant,2};
            if(world.unitCenters.contains(request.army)){
                auto& reaction=armyReactions[request.army];
                reaction.pending=std::max(reaction.pending,unsigned(request.variant));receipt.status=1;
            }else if(request.sourceFlags&1u){
                // The exact source absence still needs an absent attached
                // display pointer. Detached manager views have private keys;
                // they may keep retiring after Army148 has actually cleared.
                const auto actor=armyWorldState.actorIndices.lower_bound({request.army,0,0,0});
                const auto binding=armyModelPool->bindings.lower_bound({GraphicsModelFamily::Army,{request.army,0,0,0}});
                const bool currentActor=actor!=armyWorldState.actorIndices.end()&&actor->first.unit==request.army;
                const bool boundOwner=binding!=armyModelPool->bindings.end()&&binding->first.family==GraphicsModelFamily::Army&&
                    binding->first.actor.unit==request.army;
                if(!currentActor&&!boundOwner&&!armyReactions.contains(request.army))receipt.status=3;
            }
            next.push_back(receipt);
        }
        armyReactionReceipts=std::move(next);
    }

    void updateTargetReaction(ID3D11Device* device,const ArmyWorldPublication& world,int32_t id,bool admitted,
        const std::array<float,3>& actualUnitPosition,unsigned fpsMode,
        std::array<std::vector<NativeAccessoryPoseJoint>,7>& sampledGroupLocals) {
        using namespace native_accessory_pose_detail;
        auto foundReaction=armyReactions.find(id);if(foundReaction==armyReactions.end())return;
        auto& reaction=foundReaction->second;auto& pool=*armyModelPool;
        // Keep the exact per-unit v30 -> reaction -> ordinary-player boundary.
        // Missing models remain Phase1 and retry at this unit's next admission.
        if(!reaction.playing()){
            pool.releaseDescriptor(GraphicsModelFamily::Army,id,0x160);
            pool.releaseDescriptor(GraphicsModelFamily::Army,id,0x188);
            if(reaction.active){reaction.active=0;reaction.slots={};++armyReactionRevision;}
            if(reaction.pending){reaction.begin(actualUnitPosition,world.unitHeadings.at(id),world.unitRotations.at(id));++armyReactionRevision;}
        }
        //1CBA20's false visibility clears the real model lists; it does not
        // skip controller state cleanup or pending maximum consumption.
        if(!admitted){
            pool.releaseDescriptor(GraphicsModelFamily::Army,id,0x160);
            pool.releaseDescriptor(GraphicsModelFamily::Army,id,0x188);
        }else for(unsigned i=0;i<reaction.count();++i){
            if(reaction.slots[i].phase!=1)continue;
            const ArmyWorldActorKey key{id,int32_t(reaction.category()),reaction.descriptor(),int32_t(i)};
            pool.acquire(key);
        }
        synchronize(device,world);
        bool retired=false;
        for(unsigned i=0;i<reaction.count();++i){
            auto& slot=reaction.slots[i];if(slot.phase!=1)continue;
            ArmyWorldActorKey key{id,int32_t(reaction.category()),reaction.descriptor(),int32_t(i)};
            auto* physical=pool.find(key);if(!physical)continue;
            const auto found=armyWorldState.actorIndices.find(key);if(found==armyWorldState.actorIndices.end())continue;
            const auto index=found->second;
            const auto& action=armyGraphicsActions->rows.at({reaction.category(),3u});
            auto sourcePlayer=action.player;
            // Descriptor160 writes instance group1; descriptor188 writes0.
            sourcePlayer.grouped=reaction.active==1;
            const auto step=consumeNativeArmyAnimation(sourcePlayer,armyModels.source.defaultActions.fps,reaction.category(),
                physical->clock,armyAnimationGroups,{true,true,3,-1,fpsMode},
                [&](float requested,bool){return std::optional<float>(source_default_player_phase::defaultReturnedPhase(
                    {sourcePlayer.clipDuration,sourcePlayer.loopDeclaration},requested));});
            if(!step.succeeded)continue;
            const auto q=slot.rotation;const auto forward=rotate(q,{0,0,1,0});
            const float t=divide(step.nextFrame,reaction.limit()),distance=mul(t,400.f);
            auto root=physical->presented.world;root.rotation=q;
            root.translation={sub(slot.initial[0],mul(distance,forward.x)),
                add(add(mul(distance,forward.y),slot.initial[1]),mul(sub(.5f,std::abs(sub(t,.5f))),100.f)),
                sub(slot.initial[2],mul(distance,forward.z)),1};
            auto& presented=armyModels.source.actors[index];presented.world=root;
            presented.obb=buildArmyWorldModeBounds(armyWorldTemplates->localBounds.at(reaction.category()),root);
            const auto* pose=action.pose?&*action.pose:&armyModels.source.poses[reaction.category()];
            auto& owner=armyModels.poses[index];auto& cache=armyModels.poseCaches[index];
            const int group=sourcePlayer.grouped?nativeArmyAnimationGroupSlot(reaction.category(),3):-1;
            if(step.cached)publishNativeArmySharedLocal(*pose,owner,cache,sampledGroupLocals[size_t(group)],physical->clock.returnedPhase,root);
            else{
                prepareNativeArmyCurrentPose(*pose,owner,cache,physical->clock.returnedPhase,root,true,false);
                if(group>=0)sampledGroupLocals[size_t(group)]=owner.frame.localPose;
            }
            armyActivePoses[index]=pose;armyReturnedPhases[index]=physical->clock.returnedPhase;
            armySuccessfulSamples[index]=physical->clock.hasSuccessfulSample;
            physical->presented={presented.world,presented.obb};
            if(cache.pendingPoolSnapshot){physical->pose=owner;cache.pendingPoolSnapshot=false;}
            physical->cache=cache;
            if(step.nextFrame>=reaction.limit()){slot.phase=2;retired=true;}
        }
        if(retired){++armyReactionRevision;synchronize(device,world);}
    }

    std::map<int32_t,ArmyTargetReaction> armyReactions;
    std::vector<ArmyTargetReactionReceipt> armyReactionReceipts;
    uint64_t armyLastReactionRequest{},armyReactionRevision{},armyAppliedReactionRevision{};
    const bool sourceArmyVisuals,sourceAccessoryVisuals;
    bool armyOwnerLive{};
    NativeArmyPopulation armyModels;NativeArmyPlan armyPlan;NativeArmyQueues armyQueues;
    std::vector<float> armyReturnedPhases;
    std::optional<ArmyWorldTemplateLibrary> armyWorldTemplates;
    std::optional<NativeRenderResourceSnapshots> armyResourceSnapshots;
    std::optional<NativeArmyMaterialResourceContext> armyResourceContext;
    std::optional<NativeArmyMaterials> armyMaterials;
    ArmyWorldPopulationState armyWorldState;
    NativeArmyAnimationGroups armyAnimationGroups;
    std::shared_ptr<const NativeCategory2Resources> accessoryResources;
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
