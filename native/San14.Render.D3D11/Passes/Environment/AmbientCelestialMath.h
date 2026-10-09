#pragma once
#include "AmbientEnvironment.h"

namespace source_current_ambient {
using namespace source_camera;
using source_atmosphere_camera::F;
inline Matrix leftY(Matrix m, float angle) {
    auto t = sinCos(angle);
    Matrix r = m;
    for (int col = 0; col < 3; ++col)
        r[col] = S(M(m[col], t[1]), M(m[8 + col], t[0]));
    r[8] = A(M(m[0], t[0]), M(m[8], t[1]));
    r[9] = A(M(m[9], t[1]), M(m[1], t[0]));
    r[10] = A(M(m[10], t[1]), M(m[2], t[0]));
    return r;
}
inline Matrix rightY(Matrix m, float angle) {
    auto t = sinCos(angle);
    for (int row = 0; row < 4; ++row) {
        int at = row * 4;
        float x = m[at], z = m[at + 2];
        m[at] = A(M(z, t[0]), M(x, t[1]));
        m[at + 2] = S(M(z, t[1]), M(x, t[0]));
    }
    return m;
}
inline Matrix combine3x3(const Matrix &a, const Matrix &b) {
    Matrix out{};
    for (int row = 0; row < 3; ++row)
        for (int col = 0; col < 3; ++col)
            out[row * 4 + col] =
                A(A(M(b[row * 4 + 1], a[4 + col]), M(b[row * 4], a[col])), M(b[row * 4 + 2], a[8 + col]));
    out[15] = 1;
    return out;
}
inline Matrix celestialToWorld(const CelestialClock &clock, const Matrix &skyTransform) {
    float season = M(S(clock.monthDay, F(0x406ccccd)), F(0x3f060a92)), dt = clock.dayFraction;
    dt = dt > 0 ? dt : 0;
    float longitude = M(clock.longitudeDegrees, F(0x3c8efa35)),
          hour = S(A(M(S(A(dt, dt), 1), F(0x40490fdb)), season), longitude);
    auto q = sinCos(F(0x3fc90fdb));
    Matrix celestial{1, 0, 0, 0, 0, q[1], q[0], 0, 0, flip(q[0]), q[1], 0, 0, 0, 0, 1};
    celestial = rightY(leftY(celestial, F(0x40490fdb)), hour);
    return source_matrix::inverse(combine3x3(celestial, skyTransform));
}
inline float rational(float square) {
    float n = S(F(0xbc5b3fe1), M(square, F(0x3b81ce6b)));
    n = S(M(n, square), F(0x3d678bdd));
    n = A(M(n, square), F(0x3e3c94dc));
    n = M(n, square);
    return D(n, S(F(0x3f8d6fa5), M(square, F(0x3f561f0d))));
}
// Precision-preserving finite-domain inverse cosine and sine/cosine kernels.
// Polynomial coefficients and reduction order are retained; these are not libm approximations.
inline float acosSource(float value) {
    uint32_t b;
    std::memcpy(&b, &value, 4);
    float a = F(b & 0x7fffffff);
    bool negative = (b >> 31) != 0;
    if (a == 1)
        return negative ? F(0x40490fdb) : 0;
    if (a < F(0x32800000))
        return F(0x3fc90fdb);
    bool large = a >= .5f;
    float square = large ? M(S(1, a), .5f) : M(a, a), root = large ? std::sqrt(square) : 0,
          arg = large ? root : a, ratio = rational(square);
    uint64_t lowBits = 0x3c91a62633145c07;
    double low;
    std::memcpy(&low, &lowBits, 8);
    const double halfPi = 1.5707963267948966;
    if (large) {
        if (negative)
            return float(source_weather_fog::DS(
                3.1415926535897931,
                source_weather_fog::DM(
                    source_weather_fog::DA(source_weather_fog::DS(double(M(ratio, arg)), low), root), 2)));
        uint32_t bits;
        std::memcpy(&bits, &root, 4);
        float trunc = F(bits & 0xffff0000), correction = D(S(square, M(trunc, trunc)), A(trunc, root));
        return A(A(M(correction, 2), M(M(arg, 2), ratio)), M(trunc, 2));
    }
    return float(source_weather_fog::DS(
        halfPi, source_weather_fog::DS(double(value), source_weather_fog::DS(low, double(M(ratio, value))))));
}
inline std::array<float, 2> moonSinCos(float angle) {
    float q = M(angle, F(0x3e22f983));
    q = angle >= 0 ? A(q, .5f) : S(q, .5f);
    int reductions = int(q);
    float a = S(angle, M(float(reductions), F(0x40c90fdb))), sign = 1;
    if (a > F(0x3fc90fdb)) {
        a = S(F(0x40490fdb), a);
        sign = -1;
    } else if (a < F(0xbfc90fdb)) {
        a = S(F(0xc0490fdb), a);
        sign = -1;
    }
    float z = M(a, a), sp = S(F(0x3638b88e), M(z, F(0x32cd365b))), term = M(z, F(0x348bdd11));
    sp = S(M(sp, z), F(0x39500bf1));
    sp = A(M(sp, z), F(0x3c088886));
    sp = S(M(sp, z), F(0x3e2aaaab));
    sp = A(M(sp, z), 1);
    float cp = S(F(0x37cfb4c2), term);
    cp = S(M(cp, z), F(0x3ab609aa));
    cp = A(M(cp, z), F(0x3d2aaaa3));
    cp = S(M(cp, z), .5f);
    cp = A(M(cp, z), 1);
    return {M(sp, a), M(cp, sign)};
}
inline Matrix moonFace(V4 d) {
    float ax = S(M(d.Z, -1), M(d.Y, 0)), ay = S(M(d.X, 0), M(d.Z, 0)), az = S(M(d.Y, 0), M(d.X, -1)),
          dot = A(S(M(d.X, 0), M(d.Y, 1)), M(d.Z, 0));
    float length = std::sqrt(A(A(M(ax, ax), M(ay, ay)), M(az, az)));
    if (length != 0) {
        ax = D(ax, length);
        ay = D(ay, length);
        az = D(az, length);
    } else
        ax = ay = az = 0;
    auto sc = moonSinCos(flip(acosSource(dot)));
    float s = sc[0], c = sc[1], t = S(1, c), dx = A(M(M(t, ax), ax), c), dy = A(M(M(t, ay), ay), c),
          dz = A(M(M(t, az), az), c), yz = M(M(ay, t), az), zx = M(M(az, t), ax), xy = M(M(ax, t), ay),
          sx = M(s, ax), sy = M(s, ay), sz = M(s, az);
    return {dx,        A(sz, xy), S(zx, sy), 0, S(xy, sz), dy, A(sx, yz), 0,
            A(sy, zx), S(yz, sx), dz,        0, 0,         0,  0,         1};
}
} // namespace source_current_ambient
