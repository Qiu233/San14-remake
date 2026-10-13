#pragma once
#include "ArmyPose.h"
#include "ArmyDefaultActions.h"
#include "Accessories/AccessoryGlyphRenderer.h"
#include "ArmyRigidWorldConstants.h"
#include <map>

// Include after model-native.h/model-continuous-native.h. Army retains its
// real skin6256/rigid160 CBs, current root, palette and independent light records.
struct NativeArmyActor {
    uint32_t index{},category{},exclusion{},mode{};float slotScalar{},forceDistance{},distanceBias{};
    std::array<float,20> obb{};NativeAccessoryPoseRoot world{};
};
struct NativeArmyPrimitive {uint32_t actor{},draw{},sourceTemplate{},palette{},queue{},allowedRecords{};uint64_t tableOwner{};};
struct NativeArmySource {
    unsigned templateCount{},commonExclude{};
    std::vector<uint32_t> recordMasks,capacities;std::vector<NativeArmyActor> actors;std::vector<NativeArmyPrimitive> primitives;
    std::vector<NativeAccessoryPoseSource> poses;NativeArmyDefaultActions defaultActions;
    std::vector<std::vector<char>> vertices;
    std::vector<std::array<uint32_t,3>> skinControls;
    std::vector<std::array<char,80>> pixels;std::vector<std::string> identities;
};
struct NativeArmyDraw {
    unsigned primitive{},record{};uint64_t distanceKey{},tableOwner{};
    std::vector<char> vertex;std::array<char,80> pixel{};
};
struct NativeArmyPlan {std::vector<unsigned> visibleActors,ordinaryCounts;std::vector<NativeArmyDraw> draws;};
struct NativeArmyPopulation {
    NativeArmySource source;NativeModelInputs templates,currentModels;
    std::vector<NativeAccessoryPoseOwner> poses;
    std::vector<NativeArmyPoseCache> poseCaches;
};
// Source owner supplies current actor World/root on normal logical changes.
// Same model/category inventory reuses every imported/device source object.
inline void publishNativeArmyWorld(NativeArmyPopulation& owner,unsigned actor,const NativeAccessoryPoseRoot& world,const std::array<float,20>& actualBounds){
    owner.source.actors.at(actor).world=world;owner.source.actors.at(actor).obb=actualBounds;
}
template<class AcceptedCamera>
inline NativeArmyPlan selectNativeArmyPopulation(NativeArmyPopulation& owner,const AcceptedCamera& camera,const std::vector<float>& actualReturnedPhases,
    const std::vector<bool>* actualSamples=nullptr,const std::vector<const NativeAccessoryPoseSource*>* actualActions=nullptr,
    const std::vector<bool>* actualMainMembership=nullptr){
    auto& s=owner.source;if(actualReturnedPhases.size()!=s.actors.size())throw std::runtime_error("Army current phase per actor is absent");
    NativeArmyPlan r;r.ordinaryCounts.resize(s.recordMasks.size());std::vector<bool> visible(s.actors.size());
    std::vector<uint64_t> keys(s.actors.size());std::array<char,0x3C0> prefix{};std::memcpy(prefix.data(),camera.prefix.data(),prefix.size());
    std::array<std::array<float,4>,6> planes{};for(unsigned i=0;i<6;++i){const auto& p=camera.planes[i].equation;planes[i]={p.X,p.Y,p.Z,p.W};}
    float nearValue,farValue;std::memcpy(&nearValue,prefix.data()+0x140,4);std::memcpy(&farValue,prefix.data()+0x144,4);
    float span=modelContinuousAdd(modelContinuousSub(farValue,nearValue),1.f),scale=float(18446744073709551616.0/double(modelContinuousAdd(span,span)));
    const unsigned viewIndices[]{0,4,8,12,1,5,9,13,2,6,10,14};std::array<char,48> view{};
    for(unsigned i=0;i<12;++i)std::memcpy(view.data()+i*4,prefix.data()+0x80+viewIndices[i]*4,4);
    for(const auto& a:s.actors){if(actualMainMembership&&!(*actualMainMembership)[a.index])continue;
        auto& pose=owner.poses[a.index];const auto& source=actualActions?*(*actualActions)[a.index]:s.poses[a.category];
        prepareNativeArmyCurrentPose(source,pose,owner.poseCaches[a.index],
        actualReturnedPhases[a.index],a.world,!actualSamples||(*actualSamples)[a.index],a.category!=0&&a.category!=1);
        NativeModelContinuousInstance bounds;bounds.index=a.index;bounds.mode=a.mode;bounds.obb=a.obb;bounds.forceDistance=a.forceDistance;bounds.distanceBias=a.distanceBias;
        visible[a.index]=a.slotScalar>0&&modelContinuousVisible(bounds,planes);if(visible[a.index]){r.visibleActors.push_back(a.index);keys[a.index]=modelContinuousDistanceKey(bounds,planes[4].data(),span,scale);}}
    for(unsigned record=0;record<s.recordMasks.size();++record)for(unsigned i=0;i<s.primitives.size();++i){const auto& p=s.primitives[i];const auto& a=s.actors[p.actor];
        if(!visible[p.actor]||(s.recordMasks[record]&(a.exclusion|s.commonExclude))!=0)continue;
        ++r.ordinaryCounts[record];
        if((p.allowedRecords&(uint32_t(1)<<record))==0)continue;
        NativeArmyDraw d;d.primitive=i;d.record=record;d.distanceKey=keys[p.actor];d.tableOwner=p.tableOwner;d.pixel=s.pixels[p.sourceTemplate];
        auto& held=*owner.currentModels.draws[i].retainedVs;d.vertex=held;
        const auto& palette=owner.poses[p.actor].frame.palettes[p.palette];
        if(d.vertex.size()==160){
            const auto packed=packArmyRigid160(std::span<const char,160>(held.data(),160),
                std::span<const char,64>(prefix.data()+0x100,64),palette);
            std::copy(packed.begin(),packed.end(),d.vertex.begin());
        }else{
            std::memcpy(d.vertex.data(),prefix.data()+0x100,64);
            std::memcpy(d.vertex.data()+64,palette.rows.data(),palette.rows.size()*16);
            const auto& controls=s.skinControls[p.sourceTemplate];
            for(unsigned control=0;control<3;++control)
                std::memcpy(d.vertex.data()+6208+control*16,&controls[control],4);
        }
        held=d.vertex;
        std::memcpy(d.pixel.data()+16,view.data(),48);r.draws.push_back(std::move(d));}
    for(unsigned i=0;i<r.ordinaryCounts.size();++i)if(r.ordinaryCounts[i]&&r.ordinaryCounts[i]>=s.capacities[i])throw std::runtime_error("Army actual ordinary counter reaches source JGE capacity");
    // Each source-selected49/50/54/55 bank retains its independent record and
    // publication order. Current B90 lacks LightMap, so it enters54 after the
    // original49/50 gate skipped it; its independent count is unchanged.
    std::stable_sort(r.draws.begin(),r.draws.end(),[&](const auto& a,const auto& b){const auto& pa=s.primitives[a.primitive];const auto& pb=s.primitives[b.primitive];
        if(pa.queue!=pb.queue)return pa.queue<pb.queue;if(a.record!=b.record)return a.record<b.record;
        if(a.tableOwner!=b.tableOwner)return a.tableOwner<b.tableOwner;return a.distanceKey<b.distanceKey;});
    return r;
}
struct NativeArmyQueues {NativeModelInputs ordinary49,alpha50,ordinary54,alpha55;};
inline NativeArmyQueues updateSelectNativeArmyPopulation(ID3D11DeviceContext* context,const NativeArmyPopulation& owner,const NativeArmyPlan& plan){
    NativeArmyQueues r;r.ordinary49.blend=r.alpha50.blend=r.ordinary54.blend=r.alpha55.blend=owner.currentModels.blend;
    for(const auto& row:plan.draws){auto d=owner.currentModels.draws[row.primitive];context->UpdateSubresource(d.vsConstants.Get(),0,nullptr,row.vertex.data(),0,0);
        context->UpdateSubresource(d.psConstants.Get(),0,nullptr,row.pixel.data(),0,0);const auto& p=owner.source.primitives[row.primitive];
        auto* bank=p.queue==49?&r.ordinary49:p.queue==50?&r.alpha50:p.queue==54?&r.ordinary54:&r.alpha55;bank->draws.push_back(std::move(d));}return r;
}
