#pragma once
#include "ArmyPose.h"
#include <map>
#include <tuple>
#include <limits>

// Synchronously copy the managed publication. No borrowed managed pointer,
// source files, frame number or whole-world serialization survives this call.
struct ArmyWorldActorKey {
    int32_t unit{},category{},descriptor{},instance{};
    auto tuple() const {return std::tie(unit,category,descriptor,instance);}
    bool operator<(const ArmyWorldActorKey& b) const {return tuple()<b.tuple();}
    bool operator==(const ArmyWorldActorKey& b) const {return tuple()==b.tuple();}
    std::string identity() const {return "remake-army/"+std::to_string(unit)+"/"+std::to_string(category)+"/"+
        std::to_string(descriptor)+"/"+std::to_string(instance);}
};
struct ArmyWorldMaterialKey {
    int32_t force{-1},color49{-1};uint32_t glyph{};
    auto tuple() const {return std::tie(force,color49,glyph);}
    bool operator<(const ArmyWorldMaterialKey& b) const {return tuple()<b.tuple();}
    bool operator==(const ArmyWorldMaterialKey& b) const {return tuple()==b.tuple();}
};
struct ArmyWorldActor {
    ArmyWorldActorKey key{};int32_t action{};float returnedPhase{};
    NativeAccessoryPoseRoot world{};std::array<float,20> bounds{};ArmyWorldMaterialKey material{};
};
struct ArmyWorldPublication {uint64_t revision{},contextGeneration{};std::map<int32_t,std::array<float,3>> unitCenters;std::map<int32_t,int32_t> graphicsActions;
    std::map<int32_t,float> unitGroundHeights;std::map<int32_t,uint32_t> unitMotionRevisions;
    std::map<int32_t,uint32_t> unitModelModes,unitSelectedFormations,unitNavalFormations;std::vector<ArmyWorldActor> actors;};
inline int armyWorldNavalCategory(uint32_t formation){
    switch(formation){case 11:case 20:return 8;case 12:case 17:return 7;case 13:case 18:return 9;default:return -1;}
}
inline ArmyWorldPublication copyArmyWorldPublication(const void* data,size_t byteCount){
    constexpr size_t header=32,actorBytes=164;
    if(!data||byteCount<header)throw std::runtime_error("Army actual World publication header is absent");
    const auto* bytes=static_cast<const char*>(data);size_t at=0;
    auto read=[&](void* destination,size_t count){if(count>byteCount-at)throw std::runtime_error("Army World publication ends inside actor");
        std::memcpy(destination,bytes+at,count);at+=count;};
    std::array<char,8> magic{};read(magic.data(),8);
    if(magic!=std::array<char,8>{'A','R','M','Y','W','L','D','7'})throw std::runtime_error("Army actual World publication schema");
    ArmyWorldPublication r;read(&r.revision,8);read(&r.contextGeneration,8);int32_t count,units;read(&count,4);read(&units,4);
    if(!r.revision||count<0||units<0||header+size_t(units)*40+size_t(count)*actorBytes!=byteCount)
        throw std::runtime_error("Army actual World publication length/revision");
    for(int i=0;i<units;++i){int32_t id;std::array<float,3> center;read(&id,4);read(center.data(),12);
        if(id<=0||!std::isfinite(center[0])||!std::isfinite(center[1])||!std::isfinite(center[2])||!r.unitCenters.emplace(id,center).second)
            throw std::runtime_error("Army actual Unit center range/duplicate");
        int32_t action;read(&action,4);if(action<0)throw std::runtime_error("Army actual graphics action is negative");
        r.graphicsActions.emplace(id,action);
        float ground;uint32_t motionRevision;read(&ground,4);read(&motionRevision,4);
        if(!std::isfinite(ground))throw std::runtime_error("Army current terrain query is nonfinite");
        r.unitGroundHeights.emplace(id,ground);r.unitMotionRevisions.emplace(id,motionRevision);
        uint32_t mode,formation,naval;read(&mode,4);read(&formation,4);read(&naval,4);
        if(mode>2||formation>255||naval>255||(mode==2&&(formation<8||formation>10)))
            throw std::runtime_error("Army current model mode needs its imported resource category");
        r.unitModelModes.emplace(id,mode);r.unitSelectedFormations.emplace(id,formation);r.unitNavalFormations.emplace(id,naval);}
    r.actors.resize(size_t(count));std::map<ArmyWorldActorKey,bool> unique;
    constexpr int32_t descriptors[]{0x778,0x750,0x7F0,0x7C8,0x7A0,0x7A0,0x7A0,0x818,0x818,0x818},capacities[]{15,4,1,3,3,3,3,1,1,1};
    for(auto& a:r.actors){
        read(&a.key.unit,4);read(&a.key.category,4);read(&a.key.descriptor,4);read(&a.key.instance,4);
        read(&a.action,4);read(&a.returnedPhase,4);read(&a.world,48);read(a.bounds.data(),80);
        read(&a.material.force,4);read(&a.material.color49,4);read(&a.material.glyph,4);
        if(!r.unitCenters.count(a.key.unit)||a.key.category<0||a.key.category>9||a.action<0||a.key.instance<0||
            a.key.descriptor!=descriptors[a.key.category]||a.key.instance>=capacities[a.key.category]||
            !std::isfinite(a.returnedPhase)||a.returnedPhase<0||!unique.emplace(a.key,true).second)
            throw std::runtime_error("Army actual actor key/action/phase range");
        const auto& w=a.world;
        for(float f:std::array<float,12>{w.scale.x,w.scale.y,w.scale.z,w.scale.w,w.rotation.x,w.rotation.y,w.rotation.z,w.rotation.w,
            w.translation.x,w.translation.y,w.translation.z,w.translation.w})
            if(!std::isfinite(f))throw std::runtime_error("Army actual S/Q/P is nonfinite");
        for(float f:a.bounds)if(!std::isfinite(f))throw std::runtime_error("Army actual ModelCPU bounds are nonfinite");
        if(a.key.category>=4&&a.key.category<=6&&(r.unitModelModes.at(a.key.unit)!=2||
            a.key.category!=int32_t(r.unitSelectedFormations.at(a.key.unit))-4))
            throw std::runtime_error("Army siege descriptor differs from current unit mode");
        if(a.key.category>=7&&(r.unitModelModes.at(a.key.unit)!=1||
            a.key.category!=armyWorldNavalCategory(r.unitNavalFormations.at(a.key.unit))))
            throw std::runtime_error("Army naval descriptor differs from current unit byte1C");
        if(a.key.category<2||a.key.category>=4){if(a.material.force!=-1||a.material.color49!=-1||a.material.glyph)
                throw std::runtime_error("Army body actor received a cloth material key");}
        else if(a.material.force<0||a.material.force>=52||a.material.color49< -1||a.material.color49>255||a.material.glyph>65535)
            throw std::runtime_error("Army actual flag Force/color49/single-unit glyph range");
    }
    return r;
}
