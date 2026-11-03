#pragma once
#include "../../Interop/HexFocusInput.h"
#include "TerritoryState.h"
#include "ZoneLayer.h"
#include <cmath>

namespace source_hex_focus {
struct Publication {
    unsigned hex=source_zone::cells;
    void accept(const San14HexFocusInputV1& input) {
        if(input.size!=sizeof(input)||input.version!=1||input.reserved||input.hex>source_zone::cells)
            throw std::runtime_error("Hex focus scalar version/range differs");
        hex=input.hex;
    }
};
// Ordinary CHexFocusModeHex. The second focus has its own target/palette/counter.
struct Owner {
    unsigned counter=0;
    void accept(const Publication& input,source_zone::Owner& zones,source_zone::Current& current) {
        if(current.focusCell==input.hex)return;
        current.focusCell=input.hex;
        source_zone::queueDirty(zones,1u<<4);
        //17F620 changes the target without resetting the pulse counter.
    }
    template<class Profile>void updatePalette(Profile& profile,source_territory::Owner& cpu,
        const source_zone::Owner& zones,const source_zone::Current& current,unsigned fpsMode) {
        if(current.selector0!=0||(zones.dirty&(1u<<4)))return;
        constexpr unsigned fps[]{30,60};
        const unsigned period=static_cast<unsigned>(float(fps[fpsMode])*(2000.f/1000.f));
        //182DE0's ordinary Hex color/alpha, 180280 and the shared17F060 wave.
        constexpr float alpha=.125f;
        const float t=float(counter<=period?counter:0)/float(period);
        const float phase=source_territory::product(source_territory::sum(t,.25f),6.2831854820251465f);
        const float q=std::abs(std::cos(phase));
        const float low=std::max(0.f,alpha-128.f/255.f);
        const float value=std::max(.009999999776482582f,source_territory::sum(
            source_territory::product(1.f-q,low),source_territory::product(q,alpha)));
        const auto alphaByte=static_cast<std::uint8_t>(static_cast<int>(source_territory::product(value,255.f)));
        constexpr float rgb=127.f/255.f; //trunc(0.5f*255), not a fitted color.
        const source_territory::Vector fill{rgb,rgb,rgb,float(alphaByte)/255.f};
        const unsigned slot=current.focusPalette+2*current.focusMode;
        profile.baseCell[slot]=fill;profile.baseLine[slot]={};
        source_territory::writeVector(cpu.constants,208+slot*16,fill);
        source_territory::writeVector(cpu.constants,2256+slot*16,{});
        //180280 wraps on >=P; the City/Area endpoint is intentionally separate.
        if(++counter>=period)counter=0;
    }
};
} //namespace source_hex_focus
