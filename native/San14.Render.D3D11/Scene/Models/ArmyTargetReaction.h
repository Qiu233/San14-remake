#pragma once
#include "ArmyWorldPublication.h"
#include <array>

// A model-view owner keeps the pending maximum independently of camera draws.
// Model clocks, pool instances and GPU objects remain in ArmyWorldModelPool.
struct ArmyTargetReactionSlot {
    unsigned phase{}; // 0 unused, 1 playing (including a missing model), 2 completed
    std::array<float,3> initial{};
    float heading{};NativeAccessoryPoseVector rotation{};
};
struct ArmyTargetReaction {
    unsigned pending{},active{};
    std::array<ArmyTargetReactionSlot,5> slots;
    bool playing()const {
        for(const auto& slot:slots)if(slot.phase==1)return true;
        return false;
    }
    unsigned category()const {return active==1?1:0;}
    int descriptor()const {return active==1?0x160:0x188;}
    unsigned count()const {return active==1?5:active==2?1:active==3?2:0;}
    float limit()const {return active==1?71.f:85.f;}
    void begin(const std::array<float,3>& position,float heading,NativeAccessoryPoseVector rotation) {
        using namespace native_accessory_pose_detail;
        active=pending;pending=0;
        const auto sc=sinCos(heading);
        constexpr std::array<std::array<float,2>,5> offsets{{{5,-2},{2.5f,-1},{0,-2},{-2.5f,-1},{-5,-2}}};
        for(unsigned index=0;index<slots.size();++index){
            auto& slot=slots[index];slot={};if(index>=count())continue;
            slot.phase=1;slot.heading=heading;slot.rotation=rotation;
            const auto local=active==1?offsets[index]:active==3?std::array<float,2>{index==0?2.5f:-2.5f,0}:std::array<float,2>{0,0};
            slot.initial={add(add(mul(mul(local[1],90.f),sc[0]),mul(mul(local[0],90.f),sc[1])),position[0]),position[1],
                add(sub(mul(mul(local[1],90.f),sc[1]),mul(mul(local[0],90.f),sc[0])),position[2])};
        }
    }
};
