#pragma once
#include <cstdint>

namespace San14::Render {

// Retained traversal state. Main entry resets its queues; successful outer
// completion persists on the Host across accepted camera requests.
class MainRenderPhaseState {
public:
    explicit MainRenderPhaseState(std::uint64_t initialHostCompletion=0)
        :hostCompletion_(initialHostCompletion){}
    static constexpr std::uint32_t FreshHostFlags = 0x20000001u;

    void beginMain() { deferredQueue_=0; }
    void publishThinSurface(bool rainPresent) { if(rainPresent)deferredQueue_|=ThinSurface; }
    // Node71 publishes the ordinary combined-light path after preparing its
    // actual cluster producer. Queue allocation alone does not set this state.
    void publishClusteredLights() { deferredQueue_|=ClusteredLights; }
    void publishRainWetness(bool present) { if(present)deferredQueue_|=0x800000u; }
    void publishSpecialTail() { deferredQueue_|=SpecializedGeometry; }
    void completeOuterApply() { hostCompletion_|=3; deferredQueue_|=OuterApplyComplete; }

    // Encoded words are borrowed by existing raster and cloud consumers.
    std::uint32_t deferredQueueFlags()const { return deferredQueue_; }
    std::uint64_t hostCompletionFlags()const { return hostCompletion_; }
private:
    static constexpr std::uint32_t ClusteredLights=0xC0000;
    static constexpr std::uint32_t ThinSurface=0x400;
    static constexpr std::uint32_t SpecializedGeometry=0x40;
    static constexpr std::uint32_t OuterApplyComplete=0x20;
    std::uint32_t deferredQueue_{};
    std::uint64_t hostCompletion_;
};
} // namespace San14::Render
