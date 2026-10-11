#pragma once
#include <algorithm>

namespace San14::Render {
namespace star_twinkle_math {
// Keep the source scalar operations separate so the phase is not fused.
__declspec(noinline) inline float add(float a,float b) {return a+b;}
__declspec(noinline) inline float subtract(float a,float b) {return a-b;}
__declspec(noinline) inline float multiply(float a,float b) {return a*b;}
__declspec(noinline) inline float divide(float a,float b) {return a/b;}
}

// Main draws and admitted WORK faces borrow this single session-owned phase.
// It advances at the actual draw call, rather than once per frame or update.
struct StarTwinklePhase {
    float value{};

    void advance(float eyeHeight,float controllerDelta) {
        using namespace star_twinkle_math;
        constexpr float ceiling=80000.f;
        const float height=std::max(0.f,subtract(ceiling,eyeHeight));
        const float step=multiply(divide(multiply(height,controllerDelta),ceiling),.5f);
        value=add(value,step);
        if(value>=1.f)value=add(value,-1.f);
    }
};
} // namespace San14::Render
