#pragma once
#include <algorithm>
#include <cmath>
#include "AmbientBudget.h"

enum class AmbientGenerationStage { SkyFaces, PrepareMips, FilterFaces };

// This is runtime progress, not the original program's packed memory field.
// Progress is face-major during sky rendering and mip-major during filtering.
struct AmbientGenerationProgress {
    UINT generation=0,nextFace=0,selectedOutput=0;
    AmbientGenerationStage stage=AmbientGenerationStage::SkyFaces;
};

struct NativeAmbientWorkResult {
    UINT units=0,completedIndex=0;
    bool completed=false,blend=false;
    float blendFraction=0;
};

struct AmbientGenerationUpdate {
    NativeAmbientBudgetRequest budget;
    bool enabled=false;
    float interpolationSpan=1.f;
};

// The resource layer reports reached callback units, independently of its Draw
// counts. Each normal callback return commits that prefix immediately. Parent
// clocks commit separately after work and interpolation return normally.
class AmbientGeneration {
  public:
    explicit AmbientGeneration(UINT initialGeneration=0) {progress_.generation=initialGeneration;}
    UINT selectedOutput() const {return progress_.selectedOutput;}
    const AmbientGenerationProgress& progress() const {return progress_;}
    bool disabled() const {return disabled_;}
    bool needsInitialization() const {return !initialized_;}
    void recordInitialization() {initialized_=true;}

    AmbientGenerationUpdate prepare(float currentCommonClock,bool enabled,float interpolationSpan=1.f) {
        return {prepareNativeAmbientBudget(budget_,currentCommonClock),enabled,interpolationSpan};
    }

    // Face/filter callbacks receive the remaining budget and bound their own
    // loops to the stage endpoint. Mip preparation consumes one unit regardless
    // of its internal face count. A zero base return skips subsequent stages.
    template<class RenderFaces,class PrepareMips,class FilterFaces>
    NativeAmbientWorkResult execute(const AmbientGenerationUpdate& update,
        RenderFaces&& renderFaces,PrepareMips&& prepareMips,FilterFaces&& filterFaces) {
        NativeAmbientWorkResult result;disabled_=!update.enabled;
        if(disabled_)return result;
        UINT remaining=update.budget.units;
        bool baseStopped=false;
        if(progress_.stage==AmbientGenerationStage::SkyFaces&&remaining>0) {
            const UINT reached=renderFaces(progress_.nextFace,remaining);
            progress_.nextFace+=reached;remaining-=reached;result.units+=reached;
            baseStopped=reached==0;
            if(progress_.nextFace==ambient_schedule::cubeFaces)
                progress_.stage=AmbientGenerationStage::PrepareMips;
        }
        if(!baseStopped&&progress_.stage==AmbientGenerationStage::PrepareMips&&remaining>0) {
            prepareMips();--remaining;++result.units;
            progress_.stage=AmbientGenerationStage::FilterFaces;
        }
        if(!baseStopped&&progress_.stage==AmbientGenerationStage::FilterFaces&&remaining>0) {
            const UINT reached=filterFaces(progress_.nextFace,remaining);
            progress_.nextFace+=reached;result.units+=reached;
        }
        if(progress_.nextFace==ambient_schedule::filteredFaceEnd) {
            result.completed=true;result.completedIndex=progress_.generation;
            progress_.generation=(progress_.generation+1)%ambient_schedule::historySlots;
            progress_.nextFace=0;progress_.stage=AmbientGenerationStage::SkyFaces;elapsed_=0;
        }
        if(update.interpolationSpan<=0)
            progress_.selectedOutput=(progress_.generation+2)%ambient_schedule::historySlots;
        else {
            result.blendFraction=nativeAmbientBudgetDiv(elapsed_,update.interpolationSpan);
            if(result.blendFraction<=0)progress_.selectedOutput=(progress_.generation+1)%ambient_schedule::historySlots;
            else if(result.blendFraction>=1)progress_.selectedOutput=(progress_.generation+2)%ambient_schedule::historySlots;
            else {progress_.selectedOutput=ambient_schedule::interpolatedOutput;result.blend=true;}
        }
        return result;
    }

    void complete(const AmbientGenerationUpdate& update) {
        // Disabled and zero-work returns still observe the ordinary parent
        // clock, accumulating time without changing the disabled selection.
        elapsed_=nativeAmbientBudgetAdd(elapsed_,
            std::max(nativeAmbientBudgetSub(update.budget.clock,budget_.priorClock),0.f));
        completeNativeAmbientBudget(budget_,update.budget);
    }

  private:
    AmbientGenerationProgress progress_;
    NativeAmbientBudget budget_;
    float elapsed_=0;
    bool initialized_=false,disabled_=false;
};
