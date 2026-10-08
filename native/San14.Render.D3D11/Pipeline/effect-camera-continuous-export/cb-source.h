#pragma once
// Selected ordinary Effect188 pass7/22/125 camera updates. CPU only.
// Caller owns complete source-initialized CB bytes and the accepted Main prefix.
#include <cstdint>
#include <cstddef>
#include <cstring>

namespace source_effect_camera {
inline size_t bufferSize(uint32_t pass, bool vertex, uint32_t slot) {
    if (pass != 7 && pass != 22 && pass != 125) return 0;
    if (!vertex && slot == 1) return 32;
    if (slot != 0) return 0;
    if (!vertex) return 1344;
    return pass == 7 ? 64 : pass == 22 ? 624 : 608;
}
template<class T> inline T read(const uint8_t* data, size_t offset) {
    T value;
    std::memcpy(&value, data + offset, sizeof value);
    return value;
}
// A4DE20 mode3: full140/144 and full158.bit1; caller view covers full target.
// Separate binary32 operations reproduce the original profile2 writer.
inline void depthCoefficients(const uint8_t* fullCamera3C0, float* out4) {
    const bool reverse = (read<uint32_t>(fullCamera3C0, 0x158) & 2) != 0;
    const float a = read<float>(fullCamera3C0, reverse ? 0x144 : 0x140);
    const float b = read<float>(fullCamera3C0, reverse ? 0x140 : 0x144);
    const float inverseB = 1.0f / b;
    const float inverseA = 1.0f / a;
    out4[0] = inverseB - inverseA;
    out4[1] = 0.0f;
    out4[2] = 0.0f;
    out4[3] = inverseA;
}
// Returns whether this selected buffer has camera fields. No allocation, hash,
// matrix reconstruction or D3D dependency. Requires the size from bufferSize().
inline bool updateCB(uint32_t pass, bool vertex, uint32_t slot,
                     const uint8_t* fullCamera3C0, uint8_t* cb) {
    if (slot != 0 || (pass != 7 && pass != 22 && pass != 125)) return false;
    if (vertex) {
        std::memcpy(cb, fullCamera3C0 + 0x100, 64); // mW2P, original memory order.
        if (pass != 7) std::memcpy(cb + 592, fullCamera3C0 + 0x10, 16); // vEye.
    } else {
        std::memcpy(cb + 608, fullCamera3C0 + 0x10, 16); // vEye.
        if (pass != 7) {
            float coefficients[4];
            depthCoefficients(fullCamera3C0, coefficients);
            std::memcpy(cb + 640, coefficients, sizeof coefficients); // vDDot.
        }
    }
    return true;
}
}
