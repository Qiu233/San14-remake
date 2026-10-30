// Original normal FrameSync scalar producer. CPU only, no D3D dependency.
#pragma once
#include "PreparedFileInput.h"
#include <cmath>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <xmmintrin.h>

struct NativeFrameClock {
    std::uint32_t settingsFlags10{},pauseMask{};
    float current3348{},initialApply1E4{},wrapSeconds{},scalar3354{};
};
struct NativeFrameClockPublication {
    float previous334C{},current3348{},effective3350{},scalar3354{};
    float common254{},common258{};
};

inline NativeFrameClock prepareNativeFrameClock(const std::filesystem::path& source) {
    NativeFrameClock owner;
    NativePreparedInputFile input(source/"frame-clock-profile.txt");
    input>>std::hex>>owner.settingsFlags10>>std::dec>>owner.current3348>>owner.initialApply1E4
        >>owner.wrapSeconds>>owner.scalar3354>>owner.pauseMask;
    if(!input||!std::isfinite(owner.current3348)||!std::isfinite(owner.initialApply1E4)
        ||owner.wrapSeconds!=3600.f||owner.scalar3354!=1.f||owner.pauseMask!=8u)
        throw std::runtime_error("Original FrameSync profile is absent or outside its source scalar layout");
    return owner;
}

// Explicit scalar SSE operation matches the original ADDSS even when a caller
// compiles its surrounding arithmetic under another floating-point mode.
inline float nativeFrameClockAdd(float left,float right) {
    return _mm_cvtss_f32(_mm_add_ss(_mm_set_ss(left),_mm_set_ss(right)));
}

// Call once before consumers of an accepted Main. Only this owner advances
// Common; caller supplies its actual selected renderer delta. To pause, update
// this same owner's settingsFlags10 bit3 (mask0x8) before the same function.
inline NativeFrameClockPublication advanceNativeFrameClock(NativeFrameClock& owner,float actualCallerDelta) {
    if(!std::isfinite(owner.current3348)||!std::isfinite(actualCallerDelta))
        throw std::runtime_error("Original FrameSync caller scalars must be finite");
    const float old=owner.current3348;
    const bool paused=(owner.settingsFlags10&owner.pauseMask)!=0;
    const float effective=paused?0.f:actualCallerDelta;
    float current=paused?old:nativeFrameClockAdd(old,actualCallerDelta);
    while(current>owner.wrapSeconds) {
        const float next=nativeFrameClockAdd(current,-owner.wrapSeconds);
        if(!std::isfinite(next)||next==current)
            throw std::runtime_error("Original FrameSync addition left its finite progress domain");
        current=next;
    }
    owner.current3348=current;
    return {old,current,effective,owner.scalar3354,current,effective};
}
