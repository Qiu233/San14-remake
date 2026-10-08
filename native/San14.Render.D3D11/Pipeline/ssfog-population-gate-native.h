#pragma once
// D3C310 normal ctx1: local bits are admitted by View19AC, independently of
// the per-draw stencil descriptor. Load once with the actual source packet.
#include <filesystem>
#include <fstream>
#include <stdexcept>
namespace source_ssfog_population {
struct Gate {unsigned graphNode{},recordMask78{},background{};};
inline Gate load(const std::filesystem::path& directory) {
 std::ifstream s(directory/"population-gate.txt");Gate g;
 if(!(s>>g.graphNode>>g.recordMask78>>g.background)||g.graphNode!=125||g.background>1||
  (g.background?g.recordMask78!=0:(g.recordMask78!=1&&g.recordMask78!=2&&g.recordMask78!=4&&g.recordMask78!=8)))
  throw std::runtime_error("SSFog actual source record population metadata absent or invalid");
 return g;
}
inline bool admitted(const Gate& gate,unsigned currentView19AC) {
 return gate.background!=0||(currentView19AC&gate.recordMask78&0xfu)!=0;
}
} // namespace source_ssfog_population
