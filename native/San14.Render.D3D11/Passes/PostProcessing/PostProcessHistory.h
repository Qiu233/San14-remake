#pragma once
#include <algorithm>
#include <cmath>
#include <cstdint>
#include <stdexcept>

namespace San14::Render {

struct PostHistorySeed {
    uint32_t warmupApplications{}, stagingReadSide{};
    bool cocWriteSide{}, apertureChanged{}, previousGatherApplied{};
    float previousApplyTime{};
    bool hasApplied{};
};
struct PostHistoryFrame {
    float applyTime{}, deltaTime{};
    uint32_t warmupRemaining{}, stagingReadSide{};
    bool cocWriteSide{}, initializeCoc{}, rebuildAperture{}, readScatterFeedback{};
    bool scatterPrepared{};
};

// One timeline per post processor. GPU resource aliases belong to its renderer;
// this owner tracks only temporal decisions and the accepted application clock.
class PostProcessHistory {
public:
    PostProcessHistory()=default;
    explicit PostProcessHistory(PostHistorySeed seed):state_(seed){}
    const PostHistorySeed& state() const {return state_;}
    bool applicationInProgress() const {return applicationInProgress_;}

    PostHistoryFrame prepare(float applyTime,float deltaTime,bool dofEnabled,
        bool resetHistory=false,bool resetView=false) {
        if(applicationInProgress_)throw std::runtime_error("Complete the current normal post application before preparing another");
        if(!std::isfinite(applyTime)||!std::isfinite(deltaTime))throw std::runtime_error("Post application time must be finite");
        PostHistoryFrame frame;
        frame.applyTime=applyTime;frame.deltaTime=std::max(0.f,deltaTime);
        frame.initializeCoc=!state_.hasApplied||resetHistory||resetView||(!state_.previousGatherApplied&&dofEnabled);
        frame.rebuildAperture=state_.apertureChanged;
        if(frame.initializeCoc)state_.warmupApplications=2;
        state_.cocWriteSide=!state_.cocWriteSide;
        state_.stagingReadSide=state_.stagingReadSide==0?1:0;
        frame.warmupRemaining=state_.warmupApplications;
        frame.cocWriteSide=state_.cocWriteSide;
        frame.stagingReadSide=state_.stagingReadSide;
        frame.readScatterFeedback=state_.warmupApplications==0;
        return frame;
    }
    // Commit only after this application's constants and pass list are prepared.
    // A preparation exception preserves the original partial-history semantics.
    void begin(const PostHistoryFrame& frame) {
        state_.previousApplyTime=frame.applyTime;applicationInProgress_=true;
    }
    // Records preparation, not GPU completion or a fabricated CPU counter.
    // The renderer has queued both GPU clear arguments, or selected the
    // source's known unconditional warmup/no-feedback clear path.
    void prepareScatter(PostHistoryFrame& frame) const {
        if(!applicationInProgress_||frame.scatterPrepared)throw std::runtime_error("Post scatter preparation requires the current application");
        frame.scatterPrepared=true;
    }
    // The caller completes only after draw/dispatch execution and GPU alias
    // rotation succeed. Failed execution leaves the current application open.
    void complete(const PostHistoryFrame& frame,bool gatherEnabled,bool returned=true,
        uint32_t callIndex=0,uint32_t callCount=1) {
        if(!applicationInProgress_||!frame.scatterPrepared)throw std::runtime_error("Post completion requires current scatter feedback and execution");
        if(state_.warmupApplications)--state_.warmupApplications;
        state_.apertureChanged=false;
        if(returned&&callIndex==callCount-1)state_.previousGatherApplied=gatherEnabled;
        state_.hasApplied=true;applicationInProgress_=false;
    }
private:
    PostHistorySeed state_;
    bool applicationInProgress_{};
};
} // namespace San14::Render
