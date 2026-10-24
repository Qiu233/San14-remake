#pragma once
// CE189AD0 region colours and CE18D930 first-dirty-layer publication.
// Source input is imported once; no frame number, shader replacement or grid
// line construction. All seven buffers remain actual PB2SM R8_UINT layers.
#include <algorithm>
#include <array>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <vector>

namespace source_zone {
constexpr unsigned cells=48400,layers=7,areaCount=501;
struct Profile {
    std::vector<std::uint8_t> ownership,surface;
    std::vector<std::int16_t> terrain2A;
    std::vector<std::uint16_t> area;
    std::array<std::uint8_t,areaCount> valid;
    std::array<std::uint32_t,areaCount> regionColors{};
    Profile():ownership(cells),surface(cells),terrain2A(cells),area(cells){}
};
inline int neighbour(int cell,int direction) {
    int row=cell/220,col=cell%220,odd=col&1;
    switch(direction) {
    case 1:return col<=0?cells:odd<1?cell-1:row>0?cell-221:cells;
    case 2:return row>0?cell-220:cells;
    case 3:return col>218?cells:odd>0?(row<1?cells:cell-219):cell+1;
    case 4:return col<=0?cells:odd<1&&row<219?cell+219:cell-1;
    case 5:return row<219?cell+220:cells;
    case 6:return col>218?cells:odd<1&&row<219?cell+221:cell+1;
    default:return cell;
    }
}
inline void buildRegionColors(Profile& profile) {
    // Map constructor and the source cache-reset writer fill all 501 entries
    // with -1; 189AD0 only overwrites areas admitted by the real validity owner.
    profile.regionColors.fill(UINT32_MAX);
    std::array<std::array<unsigned,8>,areaCount> adjacency{};
    for(unsigned cell=0;cell<cells-1;cell++) {
        unsigned area=profile.area[cell];if(area<1||area>500)continue;
        for(unsigned direction=1;direction<7;direction++) {
            unsigned other=neighbour(cell,direction);if(other==cells)continue;
            unsigned next=profile.area[other];if(next<1||next>500||next==area)continue;
            for(auto& slot:adjacency[area]){if(slot==next)break;if(slot==0){slot=next;break;}}
        }
    }
    std::array<std::array<bool,areaCount>,6> groups{};
    for(unsigned area=1;area<areaCount;area++)if(profile.valid[area])
        for(unsigned color=0;color<6;color++) {
            bool conflict=false;for(unsigned other:adjacency[area])if(groups[color][other]){conflict=true;break;}
            if(!conflict){groups[color][area]=true;profile.regionColors[area]=color;break;}
        }
}
inline Profile load(const std::filesystem::path& directory) {
    Profile p;std::ifstream f(directory/"profile.bin",std::ios::binary);std::uint32_t magic=0;
    auto read=[&](void* data,size_t bytes){f.read(static_cast<char*>(data),bytes);if(!f)throw std::runtime_error("Source seven-layer input absent/incomplete");};
    // ZSV2 contains static map inputs only. Current ownership is published by
    // the live World owner before the first overlay draw.
    read(&magic,4);if(magic!=0x3256535A)throw std::runtime_error("Source zone profile version differs");
    read(p.terrain2A.data(),cells*2);read(p.surface.data(),cells);read(p.area.data(),cells*2);read(p.valid.data(),areaCount);
    if(f.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Source zone profile has trailing data");
    buildRegionColors(p);return p;
}
struct Current {
    // NULL means the source2F8F0 membership owner is absent; a real owner
    // supplies one predicate result per cell (false14 / true13).
    const std::vector<std::uint8_t>* membership=nullptr;
    std::array<std::vector<std::uint32_t>,2> referenceLists;
    std::uint32_t focusCell=cells,focusPalette=1;
    std::int8_t focusMode=0;
    std::int32_t selector0=0,selector1=-1;
    const std::vector<std::uint16_t>* areaFocusSlots=nullptr;
    std::uint32_t areaFocus=0,areaFocusPalette=6;
};
// CE18C7D0. referencesReady is the source pool-record length predicate, not
// merely a non-null descriptor/handle. Optional owners are gated by enabled.
constexpr std::uint32_t displayMask(bool enabled,int style,bool membershipPresent,bool referencesReady) {
    if(!enabled)return 0;
    std::uint32_t mask=0;
    switch(style){case 0:mask=0x5E1F;break;case 1:mask=0x604;break;
        case 3:mask=0x602;break;case 4:mask=0x4019;break;case 5:mask=2;break;}
    if(membershipPresent)mask|=0x20;
    if(referencesReady)mask|=0x80;
    return mask;
}
struct Owner {
    std::vector<std::uint8_t> zones=std::vector<std::uint8_t>(cells*layers);
    unsigned dirty=0x7F;
};
inline void queueDirty(Owner& owner,unsigned mask){owner.dirty|=mask&0x7F;}
inline int updateOne(const Profile& p,Owner& owner,const Current& current) {
    if(!owner.dirty)return -1;
    unsigned layer=0;while(!(owner.dirty&(1u<<layer)))++layer;
    auto* output=owner.zones.data()+layer*cells;
    switch(layer) {
    case 0:std::copy(p.ownership.begin(),p.ownership.end(),output);break;
    case 1:for(unsigned cell=0;cell<cells;cell++)output[cell]=p.terrain2A[cell]!=0?0:p.surface[cell]==23?12:11;break;
    case 2:
        if(!current.membership)std::fill(output,output+cells,0);
        else {if(current.membership->size()!=cells)throw std::runtime_error("Actual source membership results incomplete");
            for(unsigned cell=0;cell<cells;cell++)output[cell]=(*current.membership)[cell]?13:14;}
        break;
    case 3:{
        auto references=current.referenceLists;for(auto& list:references)std::sort(list.begin(),list.end());
        std::fill(output,output+cells,0);
        for(unsigned n=0;n<2;n++)for(unsigned cell:references[n])if(cell<cells)output[cell]=std::uint8_t(16+n);
        break;}
    case 4:
        if(current.selector0!=0)throw std::runtime_error("Different actual layer4 focus provider required");
        std::fill(output,output+cells,0);
        if(current.focusCell<cells)output[current.focusCell]=std::uint8_t(current.focusPalette+2*current.focusMode);
        break;
    case 5:
        if(current.selector1==-1)std::fill(output,output+cells,0);
        else if(current.selector1==2||current.selector1==3||current.selector1==4) {
            if(!current.areaFocusSlots||current.areaFocusSlots->size()!=cells)
                throw std::runtime_error("Actual area-focus raw Hex12 inputs required");
            for(unsigned cell=0;cell<cells;++cell)output[cell]=current.areaFocus!=0&&
                (*current.areaFocusSlots)[cell]==current.areaFocus?std::uint8_t(current.areaFocusPalette):0;
        } else throw std::runtime_error("Actual layer5 provider required");
        break;
    case 6:for(unsigned cell=0;cell<cells;cell++)output[cell]=p.area[cell]>=1&&p.area[cell]<=500?std::uint8_t(p.regionColors[p.area[cell]]+19):18;break;
    }
    owner.dirty&=~(1u<<layer);return int(layer);
}
inline unsigned updateUntilSettled(const Profile& profile,Owner& owner,const Current& current) {
    unsigned count=0;while(updateOne(profile,owner,current)>=0)++count;return count;
}
} // namespace source_zone
