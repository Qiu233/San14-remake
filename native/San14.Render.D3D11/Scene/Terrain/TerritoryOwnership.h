#pragma once
#include <array>
#include <cstdint>
#include <algorithm>
#include <cmath>
#include <vector>
#include <stdexcept>
#include "ZoneLayer.h"

namespace San14::Render {
struct TerritoryOwnershipPublication {
    std::array<std::uint8_t,source_zone::cells> colors{};
    std::uint64_t revision{};
    std::array<std::array<float,4>,60> mapColors{};
    bool mapColorsPresent{};
    void acceptMapColors(const float* source,unsigned count){
        if(!source){if(count)throw std::runtime_error("Ownership palette pointer/count disagree");return;}
        if(count!=60)throw std::runtime_error("Ownership requires all60 source MapColor records");
        for(unsigned i=0;i<count*4;++i)if(!std::isfinite(source[i]))throw std::runtime_error("Ownership source MapColor contains nonfinite data");
        for(unsigned color=0;color<count;++color)std::copy(source+color*4,source+color*4+4,mapColors[color].begin());mapColorsPresent=true;
    }
    // NativeApi calls this synchronously while the managed publication is pinned.
    // The coroutine keeps no borrowed managed pointer.
    void accept(const std::uint8_t* current,unsigned count,std::uint64_t currentRevision){
        if(!current){if(count)throw std::runtime_error("Ownership pointer/count disagree");return;}
        if(count!=colors.size()||currentRevision==0)throw std::runtime_error("Ownership publication size/revision");
        if(currentRevision==revision)return;
        for(unsigned id=0;id<count;++id)if(current[id]>60)throw std::runtime_error("Ownership unresolved source color");
        std::copy(current,current+count,colors.begin());revision=currentRevision;
    }
};
struct TerritoryOwnershipPalette {
    std::array<int,61> sourceColorSlot{};
    template<class TerritoryProfile>explicit TerritoryOwnershipPalette(const TerritoryProfile& profile){
        sourceColorSlot.fill(-1);sourceColorSlot[60]=0;
        // The actual style writer stops at the first known unallocated60.
        // Regular raw color0 is valid and has no relation to reserved slot0.
        for(unsigned slot=25;slot<128;++slot){int color=profile.allocatedColor[slot];if(color==60)break;
            if(color<0||color>59||sourceColorSlot[color]!=-1)throw std::runtime_error("Ownership imported color allocator range/alias");
            sourceColorSlot[color]=int(slot);}
    }
};
// One complete layer0/palette transaction, preserving prior allocated slots.
// New real colors use CE187B30/18D930's first free regular
// slot, with the complete once-loaded MapColor result; no color+25 relation.
template<class TerritoryProfile>inline bool publishCurrentTerritoryOwnership(const TerritoryOwnershipPublication& publication,
    TerritoryOwnershipPalette& palette,TerritoryProfile& territoryProfile,source_zone::Profile& profile,
    source_zone::Owner& owner,const source_zone::Current& current,std::uint64_t& appliedRevision){
    if(!publication.revision||publication.revision==appliedRevision)return false;
    std::vector<std::uint8_t> layer(source_zone::cells);
    auto allocation=territoryProfile.allocatedColor;
    auto cells=territoryProfile.baseCell,lines=territoryProfile.baseLine;
    auto slots=palette.sourceColorSlot;
    for(unsigned id=0;id<source_zone::cells;++id){unsigned color=publication.colors[id];
        if(color==60||std::uint16_t(profile.terrain2A[id])==0){layer[id]=0;continue;}
        int slot=slots[color];
        if(slot<0){
            unsigned selected=25;
            // Preserve the original ordered match/free test even when filling
            // a prior hole exposes an occupied slot behind that free gate.
            while(selected<128&&allocation[selected]!=int(color)&&allocation[selected]!=60)++selected;
            if(selected==128)throw std::runtime_error("Ownership source regular color allocator exhausted");
            slot=int(selected);slots[color]=slot;
            if(allocation[slot]==60){
                if(!publication.mapColorsPresent)throw std::runtime_error("Current ownership new color requires actual60 source MapColor records");
                allocation[slot]=int(color);
                cells[slot]=publication.mapColors[color];lines[slot]=publication.mapColors[color];
            }
        }
        layer[id]=std::uint8_t(slot);
    }
    territoryProfile.allocatedColor=allocation;territoryProfile.baseCell=cells;territoryProfile.baseLine=lines;
    palette.sourceColorSlot=slots;
    profile.ownership.swap(layer);source_zone::queueDirty(owner,1);
    //0 is the lowest dirty layer;18D930 updates exactly that layer now.
    source_zone::updateOne(profile,owner,current);appliedRevision=publication.revision;return true;
}
} // namespace San14::Render
