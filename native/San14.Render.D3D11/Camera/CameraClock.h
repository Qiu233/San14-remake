#pragma once
// Source 93A330 -> 93A2F0 -> 934EE0; independent of paused Common254/258.
#include "CameraMotion.h"
#include <intrin.h>
#include <ctime>

struct NativeCameraClock {
    bool performanceCounter=false;
    uint64_t frequency=1000;
};
inline NativeCameraClock prepareNativeCameraClock() {
    NativeCameraClock clock;int cpu[4]{};__cpuid(cpu,0);
    if(cpu[0]>0){__cpuid(cpu,1);if(cpu[3]&0x10){LARGE_INTEGER frequency{};
        if(QueryPerformanceFrequency(&frequency)){clock.performanceCounter=true;clock.frequency=uint64_t(frequency.QuadPart);}}}
    return clock;
}
inline int32_t sampleNativeCameraClock(const NativeCameraClock& clock) {
    uint64_t counter;
    if(clock.performanceCounter){LARGE_INTEGER value{};QueryPerformanceCounter(&value);counter=uint64_t(value.QuadPart);}
    else counter=uint64_t(std::clock());
    return source_camera_motion::sourceClock(counter,clock.frequency);
}
