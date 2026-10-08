#pragma once
// CE C600/C080 -> the existing PPCE population, retained 31-template bank.
// CPU only. Fully settled BC6540 banks; not a BC8060 request-budget emulator.
#include "../ground-detail-source-review/ground_detail_source.hpp"
#include <span>
#include <filesystem>
#include <limits>

namespace ground_detail_season_source {
using ground_detail_source::Bank;
using ground_detail_source::Block;
struct Change {bool clearPreviousDirty{};std::vector<uint64_t> dirtyXY;};
inline Change applyResourceMap(std::span<uint8_t> retained,uint32_t actualSeason) {
    constexpr std::array<uint8_t,6> a{1,3,2,0,4,6},b{11,12,13,8,9,10};
    const auto& from=actualSeason<2?b:a;const auto& to=actualSeason<2?a:b;
    Change changed;changed.clearPreviousDirty=(actualSeason&~2u)==0;
    for(uint32_t x=0;x<1024;++x)for(uint32_t y=0;y<1024;++y){
        auto& value=retained[y*1024+x];
        for(size_t k=0;k<from.size();++k)if(value==from[k]){
            value=to[k];changed.dirtyXY.push_back(uint64_t(x)|(uint64_t(y)<<32));break;
        }
    }
    return changed;
}
struct Population {std::vector<Bank> banks;std::vector<Block> blocks;};
inline Population readBanks(const std::filesystem::path& path) {
    ground_detail_source::Reader r(path.wstring());Population p;
    for(uint32_t i=0,n=r.value<uint32_t>();i<n;++i){Bank b;
        b.x=r.value<uint32_t>();b.z=r.value<uint32_t>();b.element=r.value<uint32_t>();b.templateIndex=r.value<uint32_t>();
        b.instances.resize(r.value<uint32_t>());r.read(b.instances.data(),b.instances.size()*48);
        auto block=b.z*128+b.x;
        if(p.blocks.empty()||p.blocks.back().id!=block)p.blocks.push_back({block,{}});
        p.blocks.back().bankIndices.push_back(i);p.banks.push_back(std::move(b));
    }
    if(!r.complete())throw std::runtime_error("Seasonal procedural banks trailing bytes");return p;
}
struct BankPair {
    std::array<Population,2> group;
    std::array<uint32_t,31> streamCapacity{};
    uint32_t currentGroup=std::numeric_limits<uint32_t>::max();
};
inline BankPair loadOnce(const std::filesystem::path& root) {
    BankPair p;p.group[0]=readBanks(root/"spring-summer/banks.bin");p.group[1]=readBanks(root/"autumn-winter/banks.bin");
    std::ifstream input(root/"stream-capacities.txt");uint32_t index,count;
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
