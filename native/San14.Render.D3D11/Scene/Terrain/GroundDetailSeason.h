#pragma once
#include "../../Runtime/PreparedFileInput.h"
// Settled seasonal banks from the same scene resource owner. Switching groups
// replaces population only; template media and wind resources remain owned.
#include "GroundDetailPacketReader.hpp"
#include <filesystem>
#include <limits>

namespace ground_detail_season_source {
using ground_detail_source::Bank;
using ground_detail_source::Block;
struct Population {std::vector<Bank> banks;std::vector<Block> blocks;};
inline Population readBanks(const std::filesystem::path& path,const ground_detail_source::Packet& packet) {
    ground_detail_source::Reader r(path.wstring());Population p;
    for(uint32_t i=0,n=r.value<uint32_t>();i<n;++i){Bank b;
        b.x=r.value<uint32_t>();b.z=r.value<uint32_t>();b.element=r.value<uint32_t>();b.templateIndex=r.value<uint32_t>();
        b.instances.resize(r.value<uint32_t>());r.read(b.instances.data(),b.instances.size()*48);
        if(b.x>=packet.declaration.blocksX||b.z>=packet.declaration.blocksZ||b.element>=packet.elements.size()||b.templateIndex>=packet.templates.size())
            throw std::runtime_error("Seasonal population bank index exceeds directory");
        auto block=b.z*packet.declaration.blocksX+b.x;
        if(p.blocks.empty()||p.blocks.back().id!=block)p.blocks.push_back({block,{}});
        p.blocks.back().bankIndices.push_back(i);p.banks.push_back(std::move(b));
    }
    if(!r.complete())throw std::runtime_error("Seasonal procedural banks trailing bytes");return p;
}
struct BankPair {
    std::array<Population,2> group;
    std::vector<uint32_t> streamCapacity;
    uint32_t currentGroup=std::numeric_limits<uint32_t>::max();
};
inline BankPair loadOnce(const std::filesystem::path& root,const ground_detail_source::Packet& packet) {
    BankPair p;p.streamCapacity.resize(packet.templates.size());
    p.group[0]=readBanks(root/"spring-summer/banks.bin",packet);p.group[1]=readBanks(root/"autumn-winter/banks.bin",packet);
    NativePreparedInputFile input(root/"stream-capacities.txt");uint32_t index,count;
    while(input>>index>>count){if(index>=p.streamCapacity.size())throw std::runtime_error("Seasonal stream template out of range");p.streamCapacity[index]=count;}
    if(!input.eof())throw std::runtime_error("Seasonal stream capacity file");return p;
}
// Call at accepted season-group changes, before select()/aggregate(). Existing
// matrices/visibility/stream upload remain the normal current-camera producer.
// Copy only the affine bank at this boundary; model resources/wind stay owned.
inline bool installSettled(BankPair& pair,ground_detail_source::Packet& packet,uint32_t actualSeason) {
    uint32_t selected=actualSeason<2?0u:1u;
    if(selected==pair.currentGroup)return false;
    packet.banks=pair.group[selected].banks;packet.blocks=pair.group[selected].blocks;
    pair.currentGroup=selected;return true;
}
} // namespace ground_detail_season_source
