#pragma once
#include <immintrin.h>
// The normal environment update spends at most one generation per clock span.
// Its fixed schedule includes six sky faces, one mip preparation and thirty-six
// filtered faces. Resource allocation and completed pixels are separate.
namespace ambient_schedule {
inline constexpr UINT cubeFaces=6;
inline constexpr UINT historySlots=3;
inline constexpr UINT interpolatedOutput=historySlots;
inline constexpr UINT filteredFaceEnd=42;
inline constexpr UINT workUnits=43;
}
struct NativeAmbientBudget {float fractionalUnits=0,priorClock=0;};
struct NativeAmbientBudgetRequest {UINT units=0;float clock=0;};
__declspec(noinline) static float nativeAmbientBudgetAdd(float a,float b){return a+b;}
__declspec(noinline) static float nativeAmbientBudgetSub(float a,float b){return a-b;}
__declspec(noinline) static float nativeAmbientBudgetDiv(float a,float b){return a/b;}
static NativeAmbientBudgetRequest prepareNativeAmbientBudget(NativeAmbientBudget& owner,float currentCommonClock) {
    NativeAmbientBudgetRequest request;request.clock=currentCommonClock;
    const float interval=nativeAmbientBudgetDiv(1.f,float(ambient_schedule::workUnits));
    float units=nativeAmbientBudgetDiv(nativeAmbientBudgetSub(currentCommonClock,owner.priorClock),interval);
    float whole=std::floor(units);float fraction=nativeAmbientBudgetAdd(nativeAmbientBudgetSub(units,whole),owner.fractionalUnits);
    UINT wholeUnits=UINT(_mm_cvttss_si64(_mm_set_ss(whole)));
    if(fraction>=1.f){++wholeUnits;fraction=nativeAmbientBudgetSub(fraction,std::floor(fraction));}
    owner.fractionalUnits=fraction;request.units=std::min(wholeUnits,ambient_schedule::workUnits);return request;
}
static void completeNativeAmbientBudget(NativeAmbientBudget& owner,const NativeAmbientBudgetRequest& request) {
    // Commit the caller clock only after the update returns, including disabled calls.
    owner.priorClock=request.clock;
}
