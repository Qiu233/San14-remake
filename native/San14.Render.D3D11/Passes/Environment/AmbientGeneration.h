#pragma once
#include "AmbientBudget.h"

enum class AmbientGenerationStage { SkyFaces, PrepareMips, FilterFaces };

// This is runtime progress, not the original program's packed memory field.
// Progress is face-major during sky rendering and mip-major during filtering.
struct AmbientGenerationProgress {
    UINT generation=0,nextFace=0,selectedOutput=0;
    AmbientGenerationStage stage=AmbientGenerationStage::SkyFaces;
};

struct NativeAmbientWorkPlan {
    UINT generation=0,firstBaseFace=0,baseFaces=0,firstFilter=0,filterFaces=0,units=0;
    UINT completedIndex=0;
    bool copy=false,completed=false,blend=false;
    float elapsed=0,blendFraction=0;
    AmbientGenerationProgress next;
};

static NativeAmbientWorkPlan planNativeAmbientWork(const AmbientGenerationProgress& current,
    float elapsed,float interpolationSpan,UINT requestedUnits) {
    NativeAmbientWorkPlan plan;plan.generation=current.generation;
    plan.firstBaseFace=current.nextFace;plan.next=current;plan.elapsed=elapsed;
    auto& next=plan.next;UINT remaining=requestedUnits;
    if(next.stage==AmbientGenerationStage::SkyFaces) {
        plan.baseFaces=std::min(remaining,ambient_schedule::cubeFaces-next.nextFace);
        next.nextFace+=plan.baseFaces;remaining-=plan.baseFaces;plan.units=plan.baseFaces;
        if(next.nextFace==ambient_schedule::cubeFaces)next.stage=AmbientGenerationStage::PrepareMips;
    }
    if(next.stage==AmbientGenerationStage::PrepareMips&&remaining>0) {
        plan.copy=true;--remaining;++plan.units;next.stage=AmbientGenerationStage::FilterFaces;
    }
    plan.firstFilter=next.nextFace;
    if(next.stage==AmbientGenerationStage::FilterFaces) {
        plan.filterFaces=std::min(remaining,ambient_schedule::filteredFaceEnd-next.nextFace);
        next.nextFace+=plan.filterFaces;plan.units+=plan.filterFaces;
    }
    if(next.nextFace==ambient_schedule::filteredFaceEnd) {
        plan.completed=true;plan.completedIndex=next.generation;
        next.generation=(next.generation+1)%ambient_schedule::historySlots;
        next.nextFace=0;next.stage=AmbientGenerationStage::SkyFaces;plan.elapsed=0;
    }
    if(interpolationSpan<=0)next.selectedOutput=(next.generation+2)%ambient_schedule::historySlots;
    else {
        plan.blendFraction=nativeAmbientBudgetDiv(plan.elapsed,interpolationSpan);
        if(plan.blendFraction<=0)next.selectedOutput=(next.generation+1)%ambient_schedule::historySlots;
        else if(plan.blendFraction>=1)next.selectedOutput=(next.generation+2)%ambient_schedule::historySlots;
        else {next.selectedOutput=ambient_schedule::interpolatedOutput;plan.blend=true;}
    }
    return plan;
}

struct AmbientGenerationUpdate {
    NativeAmbientBudgetRequest budget;
    NativeAmbientWorkPlan work;
    bool enabled=false;
};

// The resource owner supplies GPU work. This owner alone retains its schedule,
// selected output, interpolation time and clock budget. A plan does not publish
// progress: complete follows successful work and interpolation submission.
class AmbientGeneration {
  public:
    UINT selectedOutput() const {return progress_.selectedOutput;}
    const AmbientGenerationProgress& progress() const {return progress_;}
    bool needsInitialization() const {return !initialized_;}
    void recordInitialization() {initialized_=true;}

    AmbientGenerationUpdate prepare(float currentCommonClock,bool enabled,float interpolationSpan=1.f) {
        AmbientGenerationUpdate update;update.enabled=enabled;
        update.budget=prepareNativeAmbientBudget(budget_,currentCommonClock);
        if(enabled)update.work=planNativeAmbientWork(progress_,elapsed_,interpolationSpan,update.budget.units);
        return update;
    }

    void complete(const AmbientGenerationUpdate& update) {
        if(update.enabled){progress_=update.work.next;elapsed_=update.work.elapsed;}
        // Budget and interpolation advance at the same successful return, so
        // they share the previous accepted clock instead of retaining mirrors.
        elapsed_=nativeAmbientBudgetAdd(elapsed_,
            std::max(nativeAmbientBudgetSub(update.budget.clock,budget_.priorClock),0.f));
        completeNativeAmbientBudget(budget_,update.budget);
    }

  private:
    AmbientGenerationProgress progress_;
    NativeAmbientBudget budget_;
    float elapsed_=0;
    bool initialized_=false;
};
