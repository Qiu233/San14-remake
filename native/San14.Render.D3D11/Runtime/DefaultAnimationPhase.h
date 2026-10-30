#pragma once
#include "PreparedFileInput.h"
// Actual CE D953F0 default selection and D95330 float32 returned phase.
// The update callback/controller owns cadence; this leaf has no time advance.
#include <array>
#include <bit>
#include <cmath>
#include <cstdint>
#include <fstream>
#include <filesystem>
#include <stdexcept>
namespace source_default_player_phase {
struct Player {float duration8{},defaultLoop28{};};
struct Profile {std::array<Player,4> categories;};
inline Profile readProfile(const std::filesystem::path& path) {
 NativePreparedInputFile in(path,std::ios::binary);auto read=[&]<class T>(){T value;in.read(reinterpret_cast<char*>(&value),sizeof(value));if(!in)throw std::runtime_error("Default player source truncated");return value;};
 if(read.template operator()<uint32_t>()!=4)throw std::runtime_error("Default player source category count");Profile p;
 for(unsigned i=0;i<4;++i){auto category=read.template operator()<uint32_t>();if(category!=i)throw std::runtime_error("Default player source category");
  auto player=Player{read.template operator()<float>(),read.template operator()<float>()};
  if(!std::isfinite(player.duration8)||player.duration8<=0||!std::isfinite(player.defaultLoop28))throw std::runtime_error("Default player source fields");p.categories[category]=player;}
 return p;
}
__declspec(noinline) inline float add(float a,float b){return a+b;}
__declspec(noinline) inline float sub(float a,float b){return a-b;}
__declspec(noinline) inline float mul(float a,float b){return a*b;}
__declspec(noinline) inline float div(float a,float b){return a/b;}
inline float returnedPhase(Player player,float requestedSeconds,float loopOverride,uint32_t& loopCount) {
 // D953F0: only a strictly negative override selects player28.
 float selected=loopOverride<0?player.defaultLoop28:loopOverride;
 // D95330 MAXSS selects +zero also when selected is -zero.
 float start=selected>0?selected:0;
 if(requestedSeconds<player.duration8)return requestedSeconds;
 if(start>=player.duration8)return sub(player.duration8,std::bit_cast<float>(uint32_t(0x37480000)));
 float delta=sub(requestedSeconds,start);delta=delta>0?delta:0;float period=sub(player.duration8,start);
 float quotient=std::floor(div(delta,period));int64_t converted=static_cast<int64_t>(quotient);
 loopCount=static_cast<uint32_t>(converted); // source CVTTSS2SI64 then low32
 float remainder=sub(delta,mul(float(uint64_t(loopCount)),period));remainder=remainder>0?remainder:0;
 return add(remainder,start);
}
inline float defaultReturnedPhase(Player player,float requestedSeconds) {
 uint32_t loopCount=0;return returnedPhase(player,requestedSeconds,-1.f,loopCount);
}
} // namespace source_default_player_phase
