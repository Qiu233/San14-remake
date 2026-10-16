#pragma once
#include "CameraTransform.h"
#include <cmath>
#include <cstdint>
#include <cstring>
#include <stdexcept>

namespace source_camera {
namespace tangent_detail {
inline float F(uint32_t word) {float value; std::memcpy(&value,&word,4); return value;}
inline double C(uint64_t word) {double value; std::memcpy(&value,&word,8); return value;}
__declspec(noinline) inline double DA(double a,double b) {return a+b;}
__declspec(noinline) inline double DS(double a,double b) {return a-b;}
__declspec(noinline) inline double DM(double a,double b) {return a*b;}
__declspec(noinline) inline double DD(double a,double b) {return a/b;}
inline float flip(float value) {uint32_t word; std::memcpy(&word,&value,4); word^=0x80000000u; return F(word);}
inline float evaluate(float angle) {
    if (!std::isfinite(angle) || std::abs(angle) > 4)
        throw std::runtime_error("Camera tangent finite reduction domain");
    uint32_t bits;
    std::memcpy(&bits, &angle, 4);
    float a = F(bits & 0x7fffffff);
    if (a == 0 || (bits & 0x7fffffff) < 0x32000000)
        return angle;
    double x = a, result;
    int count = 0;
    if (x < C(0x3f20000000000000))
        result = DA(DM(DM(DM(x, x), x), C(0x3fd5555555555555)), x);
    else {
        if (x > C(0x3fe921fb54442d18)) {
            count = int(DA(DM(x, C(0x3fe45f306dc9c883)), .5));
            x = DS(DS(x, DM(count, C(0x3ff921fb54400000))), DM(count, C(0x3dd0b4611a626331)));
        }
        double square = DM(x, x), numerator = DA(DM(C(0xbf919dba6efd6aad), square), C(0x3fd8a8b0da56cb17));
        double denominator = DA(DM(DA(DM(C(0x3f92e29003c692d9), square), C(0xbfe07266d7b3511b)), square),
                                C(0x3ff27e84a3e73a2e));
        result = DA(x, DM(DM(square, x), DD(numerator, denominator)));
        if (count % 2 != 0)
            result = DD(-1, result);
    }
    float value = float(result);
    return (bits >> 31) != 0 ? flip(value) : value;
}
} // namespace tangent_detail
// Shared finite SSE tangent kernel, used by camera and screen-space effects.
inline float tangentSse(float radians) {return tangent_detail::evaluate(radians);}
} // namespace source_camera
