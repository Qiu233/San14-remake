#pragma once
#include "../Camera/CameraClock.h"
#include <cstdint>
#include <stdexcept>

// Use the same normal engine clock mode as camera timing. RNG seeding consumes
// raw ticks, before camera timing converts that sample to milliseconds.
inline std::uint32_t sampleRenderRandomSeed(const NativeCameraClock& clock) {
    if(clock.performanceCounter) {
        LARGE_INTEGER sample{};
        if(!QueryPerformanceCounter(&sample))throw std::runtime_error("Render startup performance counter failed");
        return std::uint32_t(std::uint64_t(sample.QuadPart));
    }
    return std::uint32_t(std::clock());
}
