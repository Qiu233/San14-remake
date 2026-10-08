#pragma once
#include <algorithm>
#include <array>
#include <cstdint>
#include <map>
#include <vector>

// Ordinary1C2900/1CA710/1CCA80: Unit14 is independent of follower ground Y.
// Motion writes X/Z and dirties30; the later reached v30 writes terrain Y.
struct ArmyUnitHeight {
    float y14{};
    bool dirty30=true;
    uint32_t motionRevision{};
    void observeMotion(uint32_t revision){
        if(revision!=motionRevision){motionRevision=revision;dirty30=true;}
    }
    bool updateGround(bool reached,bool hasTerrain,float currentGround,bool modelsResolved,unsigned mode=0){
        if(!reached||!dirty30||!hasTerrain)return false;
        y14=std::max(currentGround,mode==1?445.f:420.f);
        dirty30=!modelsResolved;
        return true;
    }
};

struct ArmyLabelUnitPosition {
    int32_t unit{};
    std::array<float,4> position{};
};

// Actual CUnit birth/retirement, independent of model admission and readiness.
inline void observeArmyUnitHeightRoster(std::map<int32_t,ArmyUnitHeight>& heights,
        const std::map<int32_t,std::array<float,3>>& centers){
    for(const auto& [id,center]:centers)heights.try_emplace(id);
    for(auto it=heights.begin();it!=heights.end();)
        if(!centers.contains(it->first))it=heights.erase(it);else ++it;
}

// CE19EED0 reads Unit10/14/18/1C. This CPU query never reaches v30.
inline std::vector<ArmyLabelUnitPosition> readArmyLabelUnitPositions(
        const std::map<int32_t,std::array<float,3>>& centers,
        const std::map<int32_t,ArmyUnitHeight>& heights){
    std::vector<ArmyLabelUnitPosition> result;result.reserve(centers.size());
    for(const auto& [id,center]:centers)
        result.push_back({id,{center[0],heights.at(id).y14,center[2],1.f}});
    return result;
}
