#pragma once
#include "ArmyWorldPublication.h"

struct ArmyWorldPresentation {
    NativeAccessoryPoseRoot world{};
    std::array<float,20> bounds{};
};

inline std::vector<ArmyWorldPresentation> readArmyWorldConstructors(const std::filesystem::path& path){
    native_accessory_pose_detail::Reader in(path);std::array<char,8> magic{};in.read(magic.data(),8);
    if(magic!=std::array<char,8>{'A','R','M','Y','C','S','T','1'})
        throw std::runtime_error("Army constructor source schema");
    const unsigned count=in.get<uint32_t>();std::vector<ArmyWorldPresentation> rows(count);
    for(unsigned category=0;category<count;++category){
        if(in.get<uint32_t>()!=category)throw std::runtime_error("Army constructor source category order");
        in.read(&rows[category].world,48);in.read(rows[category].bounds.data(),80);
    }
    if(in.at!=in.bytes.size())throw std::runtime_error("Army constructor source trailing bytes");
    return rows;
}

inline std::vector<unsigned> readArmyWorldPoolCapacities(const std::filesystem::path& path){
    native_accessory_pose_detail::Reader in(path);std::array<char,8> magic{};in.read(magic.data(),8);
    if(magic!=std::array<char,8>{'A','R','M','Y','P','O','L','1'})
        throw std::runtime_error("Army pool source schema");
    const unsigned count=in.get<uint32_t>();std::vector<unsigned> capacities(count);
    for(unsigned category=0;category<count;++category){
        if(in.get<uint32_t>()!=category)throw std::runtime_error("Army pool category order");
        capacities[category]=in.get<uint32_t>();
        if(in.get<uint32_t>()!=1||in.get<uint32_t>()!=0)throw std::runtime_error("Army pool requires the source loaded model bank");
    }
    if(in.at!=in.bytes.size())throw std::runtime_error("Army pool source trailing bytes");
    return capacities;
}

// Candidates are current formation records. Only a reached source v30 setter
// publishes them to the model, as one root/OBB pair.
template<class Actor>
inline void publishArmyWorldPresentation(Actor& presented,const ArmyWorldActor& candidate){
    presented.world=candidate.world;presented.obb=candidate.bounds;
}
