#pragma once
#include "../../Interop/CommandMapInput.h"
#include "TerritoryState.h"
#include "ZoneLayer.h"
#include <cmath>

namespace source_command_map {
using DisplayKind=San14CommandMapDisplayKind;
// One session owns revisions, the callback result array and the independent
// area-focus record. There are no borrowed managed callbacks or pointers.
struct Publication {
    std::vector<std::uint8_t> membership;
    std::vector<std::uint16_t> rawAreas;
    std::uint64_t revision=0,dataRevision=0,providerGeneration=0;
    bool present=false;
    DisplayKind displayKind=DisplayKind::None;
    std::uint32_t hoverHex=source_zone::cells,fpsMode=0;
    void accept(const San14CommandMapInputV1& input) {
        if(input.size!=sizeof(input)||input.version!=1||input.present>1)
            throw std::runtime_error("Command map V1 extension version/fields differ");
        acceptDecoded(San14CommandMapInputV2{sizeof(San14CommandMapInputV2),2,
            input.revision,input.dataRevision,input.providerGeneration,
            input.present?DisplayKind::AreaAdministration:DisplayKind::None,
            input.hoverHex,input.fpsMode,input.count,input.membership,input.rawAreas});
    }
    void accept(const San14CommandMapInputV2& input) {
        acceptDecoded(input);
    }
private:
    void acceptDecoded(const San14CommandMapInputV2& input) {
        bool nextPresent=input.displayKind!=DisplayKind::None;
        bool areaDisplay=input.displayKind==DisplayKind::AreaAdministration;
        bool campaignFocus=input.displayKind==DisplayKind::CampaignProvinceFocus||input.displayKind==DisplayKind::CampaignDistrictFocus||input.displayKind==DisplayKind::SummaryCityFocus||input.displayKind==DisplayKind::SummaryAreaFocus;
        bool diplomacy=input.displayKind==DisplayKind::DiplomacyForceFocus||input.displayKind==DisplayKind::DiplomacyFootholdFocus;
        bool membershipDisplay=areaDisplay||input.displayKind==DisplayKind::FixedCandidateGray||diplomacy;
        if(input.size!=sizeof(input)||input.version!=2||!input.revision||
            unsigned(input.displayKind)>unsigned(DisplayKind::DiplomacyFootholdFocus)||
            input.hoverHex>source_zone::cells||input.fpsMode>1||
            (input.displayKind==DisplayKind::FixedCandidateGray&&input.hoverHex!=source_zone::cells)||
            !input.providerGeneration||(input.providerGeneration%2!=0)!=nextPresent)
            throw std::runtime_error("Command map extension version/fields differ");
        if(input.revision<revision||input.providerGeneration<providerGeneration)
            throw std::runtime_error("Command map request revision regressed");
        if(input.count) {
            if(input.count!=source_zone::cells||!nextPresent||!input.dataRevision||
                (membershipDisplay?!input.membership:input.membership!=nullptr)||
                ((areaDisplay||campaignFocus||diplomacy)?!input.rawAreas:input.rawAreas!=nullptr))
                throw std::runtime_error("Command map display arrays are incomplete");
            for(unsigned i=0;membershipDisplay&&i<input.count;++i) {
                if(!diplomacy&&input.membership[i]>1)throw std::runtime_error("Command map predicate is unknown");
                if(diplomacy&&input.membership[i]>(input.displayKind==DisplayKind::DiplomacyForceFocus?60:61))
                    throw std::runtime_error("Diplomacy focus color/target is outside source range");
                if(input.displayKind==DisplayKind::DiplomacyForceFocus&&input.rawAreas[i]>255)
                    throw std::runtime_error("Diplomacy raw Hex14 is outside its source byte");
                if(input.displayKind==DisplayKind::DiplomacyFootholdFocus&&
                    ((input.rawAreas[i]&255)>61||(input.rawAreas[i]>>8)>61))
                    throw std::runtime_error("Diplomacy City/Gate links are outside source range");
            }
        } else if(input.membership||input.rawAreas)throw std::runtime_error("Command map array pointer/count disagree");
        if(nextPresent&&(!input.dataRevision||
            (input.dataRevision!=dataRevision||input.displayKind!=displayKind)&&!input.count))
            throw std::runtime_error("Command map display binding is absent");
        if(input.count) {
            std::vector<std::uint8_t> nextMembership;
            if(membershipDisplay)nextMembership.assign(input.membership,input.membership+input.count);
            std::vector<std::uint16_t> nextAreas;
            if(areaDisplay||campaignFocus||diplomacy)nextAreas.assign(input.rawAreas,input.rawAreas+input.count);
            membership.swap(nextMembership);rawAreas.swap(nextAreas);dataRevision=input.dataRevision;
        }
        revision=input.revision;present=nextPresent;displayKind=input.displayKind;
        hoverHex=input.hoverHex;fpsMode=input.fpsMode;
        providerGeneration=input.providerGeneration;
    }
};

struct Owner {
    std::uint64_t appliedRevision=0,dataRevision=0,providerGeneration=0;
    bool present=false;
    DisplayKind displayKind=DisplayKind::None;
    unsigned hex=source_zone::cells,area=0,mode=0,counter=0,fpsMode=0,forceColor=60;
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
        bool areaDisplay=input.displayKind==DisplayKind::AreaAdministration;
        bool campaignFocus=input.displayKind==DisplayKind::CampaignProvinceFocus||input.displayKind==DisplayKind::CampaignDistrictFocus||input.displayKind==DisplayKind::SummaryCityFocus||input.displayKind==DisplayKind::SummaryAreaFocus;
        bool forceFocus=input.displayKind==DisplayKind::DiplomacyForceFocus;
        bool footholdFocus=input.displayKind==DisplayKind::DiplomacyFootholdFocus;
        bool diplomacy=forceFocus||footholdFocus;
        bool focusDisplay=areaDisplay||campaignFocus||diplomacy;
        bool replace=input.present&&(input.dataRevision!=dataRevision||!present||input.displayKind!=displayKind);
        if(input.providerGeneration!=providerGeneration||input.displayKind!=displayKind) {
            //18B9D0 clears the OLD active pair, then changes selector/mode.
            unsigned old=basePalette+2*mode;
            color(profile,cpu,old,{},{ });color(profile,cpu,old+1,{},{ });
            if(input.providerGeneration-providerGeneration>1) {
                // A close/reopen may merge before rendering. Both real setter
                // transitions still clear their old pairs and alternate mode.
                color(profile,cpu,basePalette+2*(1-mode),{},{});
                color(profile,cpu,basePalette+2*(1-mode)+1,{},{});
            }
            hex=source_zone::cells;area=counter=0;forceColor=60;
            int selector=areaDisplay?4:input.displayKind==DisplayKind::CampaignProvinceFocus?3:
                input.displayKind==DisplayKind::CampaignDistrictFocus?2:
                input.displayKind==DisplayKind::SummaryCityFocus?5:input.displayKind==DisplayKind::SummaryAreaFocus?4:
                forceFocus?1:footholdFocus?6:-1;
            // Provider generations encode lifetime/presence, not the source
            // alternating byte. 18B9D0 toggles it on a selector transition.
            if(current.selector1!=selector)mode=1-mode;
            current.selector1=selector;
            current.areaFocus=0;current.areaFocusPalette=basePalette+2*mode;
            controls.width5Selector=current.selector1;
            source_zone::queueDirty(zones,1u<<5);
        }
        if(replace||input.present!=present) {
            current.membership=input.present&&!campaignFocus&&!diplomacy?&input.membership:nullptr;
            current.areaFocusSlots=focusDisplay?&input.rawAreas:nullptr;
            if(input.present&&!campaignFocus&&!diplomacy) {
                //18BD30 changes CELL palettes only, preserving line colors.
                if(areaDisplay) {
                    profile.baseCell[13]={0,0,0,0};profile.baseCell[14]={0,0,0,1.f/255.f};
                } else {
                    //7FB510: 18BD30(callback,80808080,0), true13/false14.
                    constexpr float gray=128.f/255.f;
                    profile.baseCell[13]={gray,gray,gray,gray};profile.baseCell[14]={0,0,0,0};
                }
                source_territory::writeVector(cpu.constants,208+13*16,profile.baseCell[13]);
                source_territory::writeVector(cpu.constants,208+14*16,profile.baseCell[14]);
            }
            source_zone::queueDirty(zones,1u<<2);
            if(focusDisplay)source_zone::queueDirty(zones,1u<<5);
        }
        if(focusDisplay&&input.hoverHex<source_zone::cells) {
            unsigned raw=footholdFocus?input.membership[input.hoverHex]:input.rawAreas[input.hoverHex];
            if(raw!=area) {
                hex=input.hoverHex;area=raw;
                // City17F300 changes only target/palette fields. Its pulse
                // counter survives a City-to-City target change.
                if(forceFocus) {
                    //17F590/17FAC0 clear the current pair and alternate it on
                    //Force change; Foothold17F480 preserves the pulse counter.
                    unsigned old=basePalette+2*mode;
                    color(profile,cpu,old,{},{});color(profile,cpu,old+1,{},{});
                    mode=1-mode;current.areaFocusPalette=basePalette+2*mode;
                    forceColor=input.membership[input.hoverHex];
                }
                if(input.displayKind!=DisplayKind::SummaryCityFocus&&!footholdFocus)counter=0;
                current.areaFocus=raw;
                source_zone::queueDirty(zones,1u<<5);
            }
        }
        present=input.present;displayKind=input.displayKind;
        dataRevision=input.dataRevision;appliedRevision=input.revision;
        providerGeneration=input.providerGeneration;
    }
    //18D870 runs only after this provider's dirty layer really cleared.
    //Counter advancement belongs to the accepted Map update, not capture or
    //elapsed time. The CRT cosine is not claimed bit-identical to the old CRT.
    template<class Profile>void updatePalette(Profile& profile,source_territory::Owner& cpu,
        const source_zone::Owner& zones) {
        if((displayKind!=DisplayKind::AreaAdministration&&displayKind!=DisplayKind::CampaignProvinceFocus&&
            displayKind!=DisplayKind::CampaignDistrictFocus&&displayKind!=DisplayKind::SummaryCityFocus&&
            displayKind!=DisplayKind::SummaryAreaFocus&&displayKind!=DisplayKind::DiplomacyForceFocus&&
            displayKind!=DisplayKind::DiplomacyFootholdFocus)||!present||(zones.dirty&(1u<<5)))return;
        constexpr unsigned fps[]{30,60};
        unsigned period=static_cast<unsigned>(float(fps[fpsMode])*(2000.f/1000.f));
        float t=float(counter<=period?counter:0)/float(period);
        bool forceFocus=displayKind==DisplayKind::DiplomacyForceFocus;
        unsigned sourceSlot=0;
        if(forceFocus&&forceColor!=60) {
            sourceSlot=25;
            while(sourceSlot<128&&profile.allocatedColor[sourceSlot]!=int(forceColor)&&profile.allocatedColor[sourceSlot]!=60)++sourceSlot;
            //187B30 is a lookup, not a color allocator.
            if(sourceSlot==128||profile.allocatedColor[sourceSlot]!=int(forceColor))return;
        }
        float phase=source_territory::product(source_territory::sum(t,forceFocus?0.f:.25f),6.2831854820251465f);
        float q=std::abs(std::cos(phase));
        float alpha=source_territory::readFloat(cpu.constants,forceFocus?208+sourceSlot*16+12:2256+19*16+12);
        float low=std::max(0.f,alpha-128.f/255.f);
        float value=std::max(.009999999776482582f,source_territory::sum(
            source_territory::product(1.f-q,low),source_territory::product(q,alpha)));
        auto byteAlpha=static_cast<std::uint8_t>(static_cast<int>(source_territory::product(value,255.f)));
        source_territory::Vector fill{};
        if(forceFocus) {
            for(unsigned component=0;component<3;++component) {
                float value=source_territory::readFloat(cpu.constants,208+sourceSlot*16+component*4);
                fill[component]=static_cast<std::uint8_t>(static_cast<int>(source_territory::product(value,255.f)))/255.f;
            }
            fill[3]=byteAlpha/255.f;
        }
        color(profile,cpu,basePalette+2*mode,fill,{1.f,244.f/255.f,26.f/255.f,byteAlpha/255.f});
        if(++counter>=period)counter=0;
    }
};
} // namespace source_command_map
