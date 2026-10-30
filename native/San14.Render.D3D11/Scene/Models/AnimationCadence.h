#pragma once
#include "../../Runtime/PreparedFileInput.h"
// CE FPSManager + actual FrameSync wait/dispatch predicates. No scheduler,
// elapsed accumulator, rendering request count or actor clock is introduced.
#include <array>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <stdexcept>
namespace source_animation_cadence {
struct Profile {std::array<uint32_t,2> fps;};
struct FpsManager {uint32_t mode8=0;}; // CE935110
inline Profile readProfile(const std::filesystem::path& path) {
 Profile p;NativePreparedInputFile in(path,std::ios::binary);in.read(reinterpret_cast<char*>(p.fps.data()),8);
 if(!in||p.fps[0]==0||p.fps[1]==0)throw std::runtime_error("Source animation FPS table");return p;
}
inline void applyLoadedOption31(FpsManager& owner,uint32_t selectedSettings84) {
 if(selectedSettings84<2)owner.mode8=selectedSettings84; // CE937740
}
inline uint32_t effectiveFps(const Profile& profile,const FpsManager& owner) {return profile.fps.at(owner.mode8);}
__declspec(noinline) inline float divide(float a,float b) {return a/b;}
inline float currentAppFixedDelta(const Profile& profile,const FpsManager& owner) {
 return divide(1.f,float(effectiveFps(profile,owner))); // CE153F7A..86
}
inline bool frameSyncShouldWait(uint64_t priorAcceptedCounter,uint64_t currentCounter,
 uint64_t counterFrequency,uint32_t currentFrameCap) {
 auto cap=currentFrameCap?currentFrameCap:1u; // actual F402D0 fallback
 return priorAcceptedCounter<currentCounter && currentCounter-priorAcceptedCounter<counterFrequency/cap;
}
inline bool stateCallbackAdmitted(int stateVirtual30Result,uint32_t terminate68) {
 return stateVirtual30Result==0&&terminate68==0; // CE573970
}
inline bool appStateDispatchAdmitted(uint8_t actualPendingMode10) {
 return actualPendingMode10==0; // CE149E70 sets App208=0 only in case0
}
inline bool normalManagerCallbackReached(bool actualStateCallback,bool reachedGameNormalTail,bool map98Present) {
 return actualStateCallback&&reachedGameNormalTail&&map98Present; // CE44CBB4..44CBF9
}
} // namespace source_animation_cadence
