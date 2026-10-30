#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <stdexcept>

namespace source_hdr_population {
enum class ViewSelector : uint32_t { Ordinary19AC=0,Ocean19B0=1 };
struct Gate {
    uint32_t node{},innerBit{};
    ViewSelector selector{};
    uint32_t outerMask{}; // Actual imported L-owner selection, not an active-population bit.
};
inline Gate load(const std::filesystem::path& directory) {
    NativePreparedInputFile in(directory/"population-gate.txt");Gate g;uint32_t selector;
    in>>g.node>>g.innerBit>>selector>>g.outerMask;
    if(!in||(g.node!=78&&g.node!=123&&g.node!=166)||
            (g.innerBit!=1&&g.innerBit!=2&&g.innerBit!=4&&g.innerBit!=8)||
            selector>1||((g.node==123)!=(selector==1)))
        throw std::runtime_error("Source HDR population gate import differs");
    g.selector=ViewSelector(selector);return g;
}
inline bool admitted(const Gate& gate,uint32_t actualView19AC,uint32_t actualView19B0) {
    const uint32_t enabled=gate.selector==ViewSelector::Ocean19B0
        ?actualView19B0:(actualView19AC&0x9fffffffu);
    return (enabled&gate.innerBit)!=0;
}
}
