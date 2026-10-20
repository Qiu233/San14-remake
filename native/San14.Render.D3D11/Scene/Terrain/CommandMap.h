#pragma once
#include "../../Interop/CommandMapInput.h"
#include "TerritoryState.h"
#include "ZoneLayer.h"
#include <cmath>

namespace source_command_map {
// One session owns revisions, the callback result array and the independent
// area-focus record. There are no borrowed managed callbacks or pointers.
struct Publication {
    std::vector<std::uint8_t> membership;
    std::vector<std::uint16_t> rawAreas;
    std::uint64_t revision=0,dataRevision=0,providerGeneration=0;
    bool present=false;
    std::uint32_t hoverHex=source_zone::cells,fpsMode=0;
    void accept(const San14CommandMapInputV1& input) {
        if(input.size!=sizeof(input)||input.version!=1||!input.revision||
            input.present>1||input.hoverHex>source_zone::cells||input.fpsMode>1||
            !input.providerGeneration||(input.providerGeneration%2!=0)!=(input.present!=0))
            throw std::runtime_error("Command map extension version/fields differ");
        if(input.revision<revision||input.providerGeneration<providerGeneration)
            throw std::runtime_error("Command map request revision regressed");
        if(input.count) {
            if(input.count!=source_zone::cells||!input.membership||!input.rawAreas||!input.present||!input.dataRevision)
                throw std::runtime_error("Command map display arrays are incomplete");
            for(unsigned i=0;i<input.count;++i)if(input.membership[i]>1)
                throw std::runtime_error("Command map predicate is unknown");
        } else if(input.membership||input.rawAreas)throw std::runtime_error("Command map array pointer/count disagree");
        if(input.present&&(!input.dataRevision||input.dataRevision!=dataRevision&&!input.count))
            throw std::runtime_error("Command map display binding is absent");
        if(input.count) {
            std::vector<std::uint8_t> nextMembership(input.membership,input.membership+input.count);
            std::vector<std::uint16_t> nextAreas(input.rawAreas,input.rawAreas+input.count);
            membership.swap(nextMembership);rawAreas.swap(nextAreas);dataRevision=input.dataRevision;
        }
        revision=input.revision;present=input.present!=0;hoverHex=input.hoverHex;fpsMode=input.fpsMode;
        providerGeneration=input.providerGeneration;
    }
};

struct Owner {
    std::uint64_t appliedRevision=0,dataRevision=0,providerGeneration=0;
    bool present=false;
    unsigned hex=source_zone::cells,area=0,mode=0,counter=0,fpsMode=0;
    static constexpr unsigned basePalette=6;

    template<class Profile>static void color(Profile& profile,source_territory::Owner& cpu,
        unsigned slot,source_territory::Vector cell,source_territory::Vector line) {
        profile.baseCell[slot]=cell;profile.baseLine[slot]=line;
        source_territory::writeVector(cpu.constants,208+slot*16,cell);
        source_territory::writeVector(cpu.constants,2256+slot*16,line);
    }
    template<class Profile>void accept(const Publication& input,Profile& profile,
        source_territory::Owner& cpu,source_zone::Owner& zones,source_zone::Current& current,
        source_territory::Controls& controls) {
        if(!input.revision)return;
        if(input.revision<appliedRevision)throw std::runtime_error("Command map applied revision regressed");
        fpsMode=input.fpsMode;
        if(input.revision==appliedRevision)return;
        bool replace=input.present&&(input.dataRevision!=dataRevision||!present);
        if(input.providerGeneration!=providerGeneration) {
            //18B9D0 clears the OLD active pair, then changes selector/mode.
            unsigned old=basePalette+2*mode;
            color(profile,cpu,old,{},{ });color(profile,cpu,old+1,{},{ });
            if(input.providerGeneration-providerGeneration>1) {
                // A close/reopen may merge before rendering. Both real setter
                // transitions still clear their old pairs and alternate mode.
                color(profile,cpu,basePalette+2*(1-mode),{},{});
                color(profile,cpu,basePalette+2*(1-mode)+1,{},{});
            }
            hex=source_zone::cells;area=counter=0;mode=unsigned(input.providerGeneration%2);
            current.selector1=input.present?4:-1;
            current.areaFocus=0;current.areaFocusPalette=basePalette+2*mode;
            controls.width5Selector=current.selector1;
            source_zone::queueDirty(zones,1u<<5);
        }
        if(replace||input.present!=present) {
            current.membership=input.present?&input.membership:nullptr;
            current.areaFocusSlots=input.present?&input.rawAreas:nullptr;
            if(input.present) {
                //18BD30 changes CELL palettes only, preserving line colors.
                profile.baseCell[13]={0,0,0,0};profile.baseCell[14]={0,0,0,1.f/255.f};
                source_territory::writeVector(cpu.constants,208+13*16,profile.baseCell[13]);
                source_territory::writeVector(cpu.constants,208+14*16,profile.baseCell[14]);
            }
            source_zone::queueDirty(zones,1u<<2);
        }
        if(input.present&&input.hoverHex<source_zone::cells) {
            unsigned raw=input.rawAreas[input.hoverHex];
            if(raw!=area) {
                hex=input.hoverHex;area=raw;counter=0;current.areaFocus=raw;
                source_zone::queueDirty(zones,1u<<5);
            }
        }
        present=input.present;dataRevision=input.dataRevision;appliedRevision=input.revision;
        providerGeneration=input.providerGeneration;
    }
    //18D870 runs only after this provider's dirty layer really cleared.
    //Counter advancement belongs to the accepted Map update, not capture or
    //elapsed time. The CRT cosine is not claimed bit-identical to the old CRT.
    template<class Profile>void updatePalette(Profile& profile,source_territory::Owner& cpu,
        const source_zone::Owner& zones) {
        if(!present||(zones.dirty&(1u<<5)))return;
        constexpr unsigned fps[]{30,60};
        unsigned period=static_cast<unsigned>(float(fps[fpsMode])*(2000.f/1000.f));
        float t=float(counter<=period?counter:0)/float(period);
        float phase=source_territory::product(source_territory::sum(t,.25f),6.2831854820251465f);
        float q=std::abs(std::cos(phase));
        float alpha=source_territory::readFloat(cpu.constants,2256+19*16+12);
        float low=std::max(0.f,alpha-128.f/255.f);
        float value=std::max(.009999999776482582f,source_territory::sum(
            source_territory::product(1.f-q,low),source_territory::product(q,alpha)));
        auto byteAlpha=static_cast<std::uint8_t>(static_cast<int>(source_territory::product(value,255.f)));
        color(profile,cpu,basePalette+2*mode,{},{1.f,244.f/255.f,26.f/255.f,byteAlpha/255.f});
        if(++counter>=period)counter=0;
    }
};
} // namespace source_command_map
