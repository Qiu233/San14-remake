#pragma once
#include <cmath>
#include <cstdint>
namespace ground_detail_source {
// CE152B0 tail after17BD0, same currentApp288/currentECb0; no camera advance.
inline uint16_t normalAppTail(uint16_t flags0,int currentMode,float currentECb0) {
    flags0&=0xfffb;return uint16_t((std::floor(currentECb0)<5?4u:0u)|(flags0&0xfff7)|(currentMode<2?8u:0u));
}
// CE153EA0's actual conditional store to overlay0, beforeD6D070 publish.
inline uint32_t hostPlacementOverlay(uint32_t currentOverlay0,uint16_t currentAppFlags) {
    return (currentOverlay0&0xffbfffffu)|((currentAppFlags&0x18)?0x400000u:0u);
}
// CE F43360->D1E410->D12A80 currentFrame240; null keeps copied sourceFlags14.
inline uint32_t placementAux14(uint32_t copiedFlags14,bool selected,uint32_t overlay0) {
    return selected?(copiedFlags14&0xfffffffbu)|((overlay0>>20)&4u):copiedFlags14;
}
}
