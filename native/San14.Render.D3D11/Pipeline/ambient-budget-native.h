#pragma once
#include <immintrin.h>
// Original node5 trigger0: retained fractional unit14 and prior Common clock1C.
// This numerical owner does not confer WORK initialization or GPU readiness.
struct NativeAmbientBudget {float fraction14=0,priorClock1C=0;};
struct NativeAmbientBudgetRequest {UINT units=0;float fraction14=0,interval=0,clock=0;};
__declspec(noinline) static float nativeAmbientBudgetAdd(float a,float b){return a+b;}
__declspec(noinline) static float nativeAmbientBudgetSub(float a,float b){return a-b;}
__declspec(noinline) static float nativeAmbientBudgetDiv(float a,float b){return a/b;}
static NativeAmbientBudgetRequest prepareNativeAmbientBudget(NativeAmbientBudget& owner,float currentCommonClock) {
    NativeAmbientBudgetRequest request;request.clock=currentCommonClock;request.interval=nativeAmbientBudgetDiv(1.f,43.f);
    float units=nativeAmbientBudgetDiv(nativeAmbientBudgetSub(currentCommonClock,owner.priorClock1C),request.interval);
    float whole=std::floor(units);float fraction=nativeAmbientBudgetAdd(nativeAmbientBudgetSub(units,whole),owner.fraction14);
    UINT wholeUnits=UINT(_mm_cvttss_si64(_mm_set_ss(whole)));
    if(fraction>=1.f){++wholeUnits;fraction=nativeAmbientBudgetSub(fraction,std::floor(fraction));}
    owner.fraction14=fraction;request.fraction14=fraction;request.units=std::min(wholeUnits,43u);return request;
}
static void completeNativeAmbientBudget(NativeAmbientBudget& owner,const NativeAmbientBudgetRequest& request) {
    // EA3 commits prior1C only after E468 returns, even if WORK did not run.
    owner.priorClock1C=request.clock;
}
struct NativeAmbientWorkPlan {
    UINT generation=0,firstBaseFace=0,baseFaces=0,firstFilter=0,filterFaces=0,units=0;
    UINT exitState168=0,selectedIndex=0,completedIndex=0;
    bool copy=false,completed=false,blend=false;
    float elapsed=0,blendFraction=0;
};
static NativeAmbientWorkPlan planNativeAmbientWork(UINT state168,float elapsed,float interpolationSpan,UINT requestedUnits) {
    NativeAmbientWorkPlan plan;plan.generation=state168&3u;plan.firstBaseFace=(state168>>2)&63u;UINT phase=(state168>>13)&3u;
    UINT flat=plan.firstBaseFace,remaining=requestedUnits;
    plan.baseFaces=flat<6?std::min(remaining,6u-flat):0;flat+=plan.baseFaces;remaining-=plan.baseFaces;plan.units=plan.baseFaces;
    plan.copy=flat==6&&phase==0&&remaining>0;if(plan.copy){--remaining;++plan.units;++phase;}
    plan.firstFilter=flat;plan.filterFaces=phase==1?std::min(remaining,42u-flat):0;flat+=plan.filterFaces;plan.units+=plan.filterFaces;
    UINT generation=plan.generation;plan.elapsed=elapsed;
    if(flat==42){plan.completed=true;plan.completedIndex=generation;generation=(generation+1)%3;flat=phase=0;plan.elapsed=0;}
    UINT local=(state168&~0x40000000u)|0x80000000u;
    if(requestedUnits!=0)local=(local&0xFFFF9F00u)|generation|(flat<<2)|(phase<<13);
    if(interpolationSpan<=0){plan.selectedIndex=(generation+2)%3;}
    else {
        plan.blendFraction=nativeAmbientBudgetDiv(plan.elapsed,interpolationSpan);
        if(plan.blendFraction<=0)plan.selectedIndex=(generation+1)%3;
        else if(plan.blendFraction>=1)plan.selectedIndex=(generation+2)%3;
        else {plan.selectedIndex=3;plan.blend=true;}
    }
    plan.exitState168=(local&0xFFFFFCFFu)|(plan.selectedIndex<<8);return plan;
}
