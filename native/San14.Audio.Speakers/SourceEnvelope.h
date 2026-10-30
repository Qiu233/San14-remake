#pragma once
#include <algorithm>
#include <cmath>
#include <cstdint>

// One authored static parameter-group envelope, in the middleware's 48000
// tick domain and 480-tick update quantum. No render frame or sample pitch.
namespace san14_audio {
struct SourceEnvelope {
    float start = 1.0f, target = 1.0f, value = 1.0f;
    uint32_t duration = 0, elapsed = 0, curve = 0;
    uint64_t remainder = 0;
    void begin(float from, float to, uint32_t ticks, uint32_t shape) {
        start = from; target = to; duration = ticks; elapsed = 0;
        curve = shape; remainder = 0; value = ticks == 0 ? target : start;
    }
    void advance_frame(uint32_t sample_rate) {
        if (duration == 0) return;
        remainder += 100;
        if (remainder < sample_rate) return;
        const uint64_t ticks = (remainder / sample_rate) * 480;
        remainder %= sample_rate;
        if (ticks >= duration - elapsed) {
            value = target; duration = elapsed = 0; return;
        }
        elapsed += static_cast<uint32_t>(ticks);
        float t = static_cast<float>(elapsed) / duration;
        switch (curve) {
            case 1: t = std::sin(t * 1.5707964f); break;
            case 2: t *= t; break;
            case 3: t = (std::sin((t - .5f) * 3.1415927f) + 1) * .5f; break;
            default: break;
        }
        value = (target - start) * t + start;
    }
};
}
