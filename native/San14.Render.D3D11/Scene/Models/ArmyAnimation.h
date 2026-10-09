#pragma once
#include "ArmyDefaultActions.h"
#include <optional>

// Current raw ActionID0 adapter for CE1CEB90. It consumes actual
// normal callback/FPS inputs, never Common254/renderer elapsed. Group reset
// belongs to one reached CE1CA6E0 manager callback, not each actor or camera.
struct NativeArmyAnimationClock {
    int32_t selectedRawAction24{};
    float hFrame24{},hRate28{},returnedPhase{};
    bool hasSuccessfulSample{};
    int32_t poseRawAction{}; // The last successful player's retained local pose.
};
struct NativeArmyAnimationGroup {
    bool played3CF0{},wrapped3D0C{};
    float nextFrame3D28{},returnedPhase{}; // phase retains the sampled shared model
};
struct NativeArmyAnimationGroups {std::array<NativeArmyAnimationGroup,7> slots;};
struct NativeArmyAnimationCallback {
    bool reached{},eligible{}; // actual kind1/ready14/H/model presence
    int32_t requestedRawAction=-1; // negative reads actual admitted H default0
    float requestedFrame=-1;
    unsigned fpsMode{}; // actual9363B0 mode, selects once-imported30/60 table
};
struct NativeArmyAnimationStep {
    bool succeeded{},sampled{},cached{},wrapped{},needsLoopThunk{};
    float requestedSeconds{},returnedPhase{},nextFrame{};
};
inline NativeArmyAnimationClock createNativeArmyDefaultAnimationClock(const NativeArmyDefaultAction& source){
    return {-1,0,source.controllerRate,0}; // fresh node24=-1; H1C=0/24=0/28=60
}
inline void resetNativeArmyAnimationGroups(NativeArmyAnimationGroups& owner){
    for(auto& slot:owner.slots){if(!slot.played3CF0)slot.nextFrame3D28=0;
        slot.played3CF0=slot.wrapped3D0C=false;}
}
inline int nativeArmyDefaultAnimationGroupSlot(unsigned category){
    // CE1C5040(category, actual rawAction0), independent of declared ordinal.
    return category==0?4:category==1?0:-1;
}
inline int nativeArmyAnimationGroupSlot(unsigned category,unsigned rawAction){
    return category==0&&rawAction<3?int(rawAction)+4:category==1&&rawAction<4?int(rawAction):-1;
}
inline std::optional<float> finiteNativeArmyDefaultReturnedPhase(const NativeArmyDefaultAction& source,float requestedSeconds){
    if(requestedSeconds>=0&&requestedSeconds<source.clipDuration)return requestedSeconds;
    return std::nullopt; // prefix-only CPU helper; Main supplies the closed default-loop leaf
}
// actualPlayer(requestedSeconds, needsLoopThunk) returns nullopt on actual
// unsuccessful/unavailable response. Inside the admitted finite prefix it can
// call finiteNativeArmyDefaultReturnedPhase; normal Main supplies the closed phase at all times.
template<class ActualPlayer>
inline NativeArmyAnimationStep consumeNativeArmyAnimation(const NativeArmyDefaultAction& action,const std::array<uint32_t,2>& fps,unsigned category,
        NativeArmyAnimationClock& clock,NativeArmyAnimationGroups& groups,const NativeArmyAnimationCallback& current,
        ActualPlayer&& actualPlayer){
    using namespace native_accessory_pose_detail;
    NativeArmyAnimationStep result;result.returnedPhase=clock.returnedPhase;result.nextFrame=clock.hFrame24;
    if(!current.reached||!current.eligible)return result;
    int32_t raw=current.requestedRawAction<0?0:current.requestedRawAction;
    if(raw!=int32_t(action.rawActionId))throw std::runtime_error("Army clock action needs its actual separately imported source clip");
    float requestFrame=current.requestedFrame;
    if(raw!=clock.selectedRawAction24){requestFrame=0;clock.hFrame24=0;result.nextFrame=0;}
    clock.selectedRawAction24=raw;
    NativeArmyAnimationGroup* slot=nullptr;
    if(action.grouped){int index=nativeArmyAnimationGroupSlot(category,unsigned(raw));
        if(index<0)return result;slot=&groups.slots[size_t(index)];requestFrame=slot->nextFrame3D28;
        if(slot->played3CF0){result.succeeded=result.cached=true;result.wrapped=slot->wrapped3D0C;
            clock.returnedPhase=slot->returnedPhase; // adapter retained display phase; no source H frame write
            clock.hasSuccessfulSample=true;clock.poseRawAction=raw;
            result.returnedPhase=slot->returnedPhase;result.nextFrame=slot->nextFrame3D28;return result;}}
    if(requestFrame<0)requestFrame=clock.hFrame24;
    result.nextFrame=requestFrame; // The caller's shared descriptor frame ref,
                                  // including action-change writes on failure.
    result.requestedSeconds=divide(requestFrame,clock.hRate28);
    result.needsLoopThunk=result.requestedSeconds>=action.clipDuration;
    auto returned=actualPlayer(result.requestedSeconds,result.needsLoopThunk);if(!returned)return result;
    float rounded=std::floor(add(mul(*returned,clock.hRate28),.5f));
    result.wrapped=clock.hFrame24>rounded;
    result.nextFrame=result.wrapped?rounded:add(rounded,divide(clock.hRate28,float(fps[current.fpsMode])));
    result.succeeded=result.sampled=true;result.returnedPhase=*returned;
    clock.hFrame24=result.nextFrame;clock.returnedPhase=*returned;
    clock.hasSuccessfulSample=true;clock.poseRawAction=raw;
    if(slot){slot->played3CF0=true;slot->wrapped3D0C=result.wrapped;slot->nextFrame3D28=result.nextFrame;slot->returnedPhase=*returned;}
    return result;
}

// CE1CEB90 -> BCE360/BD2610 -> B3B6F0(hash0): the requested action may be
// absent from a retained model. Node24/frame writes precede the failed lookup;
// no sampler, pose or returned-phase write occurs in the actual WorldA8=0 scope.
inline NativeArmyAnimationStep consumeNativeArmyUnavailableAction(NativeArmyAnimationClock& clock,
        const NativeArmyAnimationCallback& current){
    NativeArmyAnimationStep result;result.returnedPhase=clock.returnedPhase;result.nextFrame=clock.hFrame24;
    if(!current.reached||!current.eligible)return result;
    float frame=current.requestedFrame;
    if(current.requestedRawAction!=clock.selectedRawAction24){frame=0;clock.hFrame24=0;}
    clock.selectedRawAction24=current.requestedRawAction;
    result.nextFrame=frame<0?clock.hFrame24:frame;
    return result;
}
template<class ActualPlayer>
inline NativeArmyAnimationStep consumeNativeArmyDefaultAnimation(const NativeArmyDefaultActions& source,unsigned category,
    NativeArmyAnimationClock& clock,NativeArmyAnimationGroups& groups,const NativeArmyAnimationCallback& current,ActualPlayer&& actualPlayer){
    return consumeNativeArmyAnimation(source.rows.at(category),source.fps,category,clock,groups,current,std::forward<ActualPlayer>(actualPlayer));
}
