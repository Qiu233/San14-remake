#pragma once
// CE1196650 current WORK leaf. Only call for E40540 faces0/1/4/5.
// Actual prepared face camera + scene228 PlanetRadius; no owner/history/GPU.
// Finite SSE inverse-trig/tangent kernels reuse the proved Data arithmetic.
#include "AtmosphereCameraFrame.h"
#include "../../Camera/CameraTangent.h"

namespace source_work_cloud_clip {
using namespace source_camera;
inline float F(uint32_t word) {
    float value;
    std::memcpy(&value, &word, 4);
    return value;
}
inline double C(uint64_t word) {
    double value;
    std::memcpy(&value, &word, 8);
    return value;
}
__declspec(noinline) inline double DA(double a, double b) { return a + b; }
__declspec(noinline) inline double DS(double a, double b) { return a - b; }
__declspec(noinline) inline double DM(double a, double b) { return a * b; }
__declspec(noinline) inline double DD(double a, double b) { return a / b; }
inline float sqrtSource(float value) { return _mm_cvtss_f32(_mm_sqrt_ss(_mm_set_ss(value))); }
inline float rational(float square) {
    float n = S(F(0xbc5b3fe1), M(square, F(0x3b81ce6b)));
    n = S(M(n, square), F(0x3d678bdd));
    n = A(M(n, square), F(0x3e3c94dc));
    n = M(n, square);
    return D(n, S(F(0x3f8d6fa5), M(square, F(0x3f561f0d))));
}
inline float asinSource(float value) {
    uint32_t bits;
    std::memcpy(&bits, &value, 4);
    float a = F(bits & 0x7fffffff);
    bool negative = (bits >> 31) != 0;
    if (!std::isfinite(value) || a > 1)
        throw std::runtime_error("WORK source asin exceptional domain");
    if (a == 1)
        return negative ? F(0xbfc90fdb) : F(0x3fc90fdb);
    if (a < F(0x38800000))
        return value;
    bool large = a >= .5f;
    float square = large ? M(S(1, a), .5f) : M(a, a), root = large ? sqrtSource(square) : 0;
    float argument = large ? root : a, ratio = rational(square), result;
    if (large) {
        uint32_t word;
        std::memcpy(&word, &root, 4);
        float truncated = F(word & 0xffff0000);
        float correction = D(S(square, M(truncated, truncated)), A(truncated, root)),
              scaled = M(M(root, 2), ratio);
        float tail = S(scaled, S(F(0x33a22168), M(correction, 2)));
        tail = S(tail, S(F(0x3f490fda), M(truncated, 2)));
        result = S(F(0x3f490fda), tail);
    } else
        result = A(M(ratio, argument), argument);
    return negative ? flip(result) : result;
}
inline float acosSource(float value) {
    uint32_t bits;
    std::memcpy(&bits, &value, 4);
    float a = F(bits & 0x7fffffff);
    bool negative = (bits >> 31) != 0;
    if (!std::isfinite(value) || a > 1)
        throw std::runtime_error("WORK source acos exceptional domain");
    if (a == 1)
        return negative ? F(0x40490fdb) : 0;
    if (a < F(0x32800000))
        return F(0x3fc90fdb);
    bool large = a >= .5f;
    float square = large ? M(S(1, a), .5f) : M(a, a), root = large ? sqrtSource(square) : 0;
    float argument = large ? root : a, ratio = rational(square);
    double low = C(0x3c91a62633145c07), halfPi = C(0x3ff921fb54442d18);
    if (large) {
        if (negative)
            return float(DS(C(0x400921fb54442d18), DM(DA(DS(double(M(ratio, argument)), low), root), 2)));
        uint32_t word;
        std::memcpy(&word, &root, 4);
        float truncated = F(word & 0xffff0000);
        float correction = D(S(square, M(truncated, truncated)), A(truncated, root)),
              tail = A(M(correction, 2), M(M(argument, 2), ratio));
        return A(tail, M(truncated, 2));
    }
    return float(DS(halfPi, DS(double(value), DS(low, double(M(ratio, value))))));
}
inline float tanSource(float angle) {return source_camera::tangentSse(angle);}

inline V4 rotateSource(V4 v, V4 axis, float angle) {
    auto sc = sinCos(angle);
    float s = sc[0], c = sc[1];
    float k = M(A(A(M(axis.X, v.X), M(axis.Y, v.Y)), M(axis.Z, v.Z)), S(1, c));
    return {A(A(M(axis.X, k), M(v.X, c)), M(S(M(v.Z, axis.Y), M(axis.Z, v.Y)), s)),
            A(A(M(k, axis.Y), M(c, v.Y)), M(S(M(axis.Z, v.X), M(v.Z, axis.X)), s)),
            A(A(M(k, axis.Z), M(c, v.Z)), M(S(M(axis.X, v.Y), M(v.X, axis.Y)), s)), v.W};
}
// preparedFace carries the accepted eye, focus, projection and world-to-clip.
// The horizon clone changes only its up direction; it does not replace the face.
// Its original Up30 remains represented by current W2P. The local clone
// forces Up=(0,1,0,0); it never replaces the actual face W2P with that clone.
// D3D11's admitted native clip profile2 is explicit; source AB5D60 selects
// farZ=0 for reverse or1 for forward. Planet is scene228, not Rayleigh.w.
inline V4 prepareCurrentWorkCloudClip(float scenePlanetRadius, const CameraTransform &preparedFace,
                                      uint32_t nativeClipProfile = 2) {
    if (nativeClipProfile != 2 || !std::isfinite(scenePlanetRadius) || scenePlanetRadius <= 0)
        throw std::runtime_error("WORK clip needs actual positive scene planet and D3D source clip profile2");
    V4 eye = preparedFace.eye, target = preparedFace.target;
    const auto &projection = preparedFace.projection;
    const auto &currentW2P = preparedFace.worldToClip;
    float halfFov = M(preparedFace.optics.viewSpan, .5f);
    if (!(halfFov > 0) || halfFov >= F(0x3fc90fdb))
        throw std::runtime_error("WORK actual camera half FOV domain");
    auto cloneView =
        source_atmosphere_camera::adjustedView(lookTo(eye, target, {0, 1, 0, 0}), preparedFace.optics.roll);
    Matrix cloneW2P = compose(cloneView, projection);
    float dx = S(target.X, eye.X), dy = S(target.Y, eye.Y), dz = S(target.Z, eye.Z);
    float reciprocal = D(1, sqrtSource(A(A(M(dx, dx), M(dy, dy)), M(dz, dz))));
    dx = M(dx, reciprocal);
    dy = M(dy, reciprocal);
    dz = M(dz, reciprocal);
    V4 axis{S(M(dz, -1), M(dy, 0)), S(M(dx, 0), M(dz, 0)), S(M(dy, 0), M(dx, -1)), 0};
    reciprocal = D(1, sqrtSource(A(A(M(axis.Y, axis.Y), M(axis.X, axis.X)), M(axis.Z, axis.Z))));
    axis = {M(axis.X, reciprocal), M(axis.Y, reciprocal), M(axis.Z, reciprocal), 0};
    float altitude = eye.Y <= 0 ? 0 : eye.Y,
          angle = asinSource(D(scenePlanetRadius, A(altitude, scenePlanetRadius)));
    auto horizon = rotateSource({0, -1, 0, 0}, axis, angle);
    reciprocal =
        D(1, sqrtSource(A(A(M(horizon.X, horizon.X), M(horizon.Y, horizon.Y)), M(horizon.Z, horizon.Z))));
    float dot = A(A(M(M(horizon.X, reciprocal), dx), M(M(dy, horizon.Y), reciprocal)),
                  M(M(horizon.Z, reciprocal), dz));
    float clamped = dot < 1 ? (dot >= -1 ? dot : -1) : 1;
    float horizonY = D(tanSource(acosSource(clamped)), tanSource(halfFov));
    if (M(horizon.Y, reciprocal) < dy)
        horizonY = flip(horizonY);
    Matrix transform = compose(source_matrix::inverse(cloneW2P), currentW2P);
    float farZ = preparedFace.optics.reversedDepth ? 0.f : 1.f;
    auto point = [&](bool minus) {
        V4 out;
        float *component = &out.X;
        float w =
            minus ? A(A(S(M(transform[7], horizonY), M(transform[3], 1)), M(transform[11], farZ)),
                      transform[15])
                  : A(A(A(transform[3], M(transform[7], horizonY)), M(transform[11], farZ)), transform[15]);
        float inverse = D(1, w);
        for (unsigned c = 0; c < 3; ++c) {
            float v = minus ? S(M(transform[4 + c], horizonY), M(transform[c], 1))
                            : A(transform[c], M(transform[4 + c], horizonY));
            component[c] = M(A(A(v, M(transform[8 + c], farZ)), transform[12 + c]), inverse);
        }
        out.W = 1;
        return out;
    };
    V4 negative = point(true), positive = point(false);
    float lineX = S(positive.X, negative.X), lineY = S(positive.Y, negative.Y);
    reciprocal = D(1, sqrtSource(A(A(M(lineY, lineY), M(lineX, lineX)), 0)));
    V4 plane{flip(M(lineY, reciprocal)), M(lineX, reciprocal), 0, 0};
    plane.W = flip(A(A(M(negative.Y, plane.Y), M(plane.X, negative.X)), M(negative.Z, 0)));
    if (!std::isfinite(plane.X) || !std::isfinite(plane.Y) || !std::isfinite(plane.W))
        throw std::runtime_error("WORK actual clip projection/normalization is degenerate");
    return plane;
}
} // namespace source_work_cloud_clip
