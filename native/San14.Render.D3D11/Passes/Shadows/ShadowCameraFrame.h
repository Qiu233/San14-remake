// Cascaded shadow camera geometry and retained cascade history.
// The camera consumes a published transform; SHC1 decoding stays at import.
// Caster admission uses the complete model catalog independently of main visibility.
#pragma once
#include "../../Camera/CameraMatrix.h"
#include "../../Camera/CameraState.h"
#include <bit>
namespace source_shadow_camera {
using source_camera::A;
using source_camera::D;
using source_camera::flip;
using source_camera::M;
using source_camera::Matrix;
using source_camera::S;
using source_camera::V4;
struct World {
    unsigned index{}, allowedMask{}, exclusion{};
    int flags{};
    float opacity{};
    unsigned primitiveCount{};
    unsigned ordinaryFirstMask = 0xFFFFFFFFu;
};
struct Record {
    unsigned mask{}, index{};
};
struct Profile {
    source_camera::ClipDepthRange clipDepthRange = source_camera::ClipDepthRange::MinusOneToOne;
    float tangent{}, ownFar{}, farOverride{};
    int count{};
    std::array<float, 4> ends{};
    V4 direction{};
    unsigned forceFit{}, width{}, height{}, noHalfTexel{}, mask22C{};
    std::vector<Record> records;
    std::vector<World> worlds;
};
struct Volume {
    std::array<V4, 8> vertices{};
    std::array<V4, 6> planes{};
};
struct Pass {
    V4 minimum{}, maximum{}, casterMinimum{}, casterMaximum{};
    Matrix lightView{}, basis{}, centered{}, shadow{};
    unsigned clipPresent{}, casterDirty{}, shadowWritten{};
    float depthSpan{};
    V4 depth0{}, depth1{};
    unsigned shadowAvailable{}, viewAvailable{}, basisAvailable{};
};
struct Atlas {
    int x{}, y{}, width{}, height{}, unitWidth{}, unitHeight{};
    V4 scaleOffset{};
};
struct Admission {
    unsigned index{}, mask{};
    std::array<float, 4> distances{};
    std::array<unsigned, 4> distanceWritten{};
    std::uint64_t firstAcceptedDistanceKey{};
};
struct Frame {
    source_camera::CameraTransform camera{};
    Matrix workVolumeTransform{};
    unsigned effectiveMask{}, touchedMask{};
    std::array<Pass, 4> passes{};
    V4 depthPlane{}, relativeEnds{};
    std::array<Atlas, 4> atlas{};
    std::vector<Admission> admissions;
    std::array<unsigned, 4> instanceCounts{}, primitiveCounts{};
    std::vector<unsigned> ordinaryRecordCounts;
    float compactFar{};
    int passCount = 4;
};
// One actual selected compact CPU owns these destinations. The constructor
// does not initialize shadow/view matrices; the first real volume/final writer establishes
// availability. Reset clears bounds/flags, not these historical matrices.
struct Owner {
    std::array<Pass, 4> retained{};
    std::array<Atlas, 4> atlas{};
};
inline void restoreOwned(Frame &f, const Owner &owner) {
    f.passes = owner.retained;
    f.atlas = owner.atlas;
    for (auto &pass : f.passes) {
        pass.lightView = pass.centered;
        pass.shadowWritten = 0;
    }
}
inline void retainOwned(Owner &owner, const Frame &f) {
    owner.retained = f.passes;
    owner.atlas = f.atlas;
}
inline Profile loadProfile(const std::filesystem::path &path) {
    std::ifstream s(path, std::ios::binary);
    using source_camera::read;
    if (!s || read<unsigned>(s) != 0x31434853u)
        throw std::runtime_error("Shadow source SHC1 profile absent");
    Profile p;
    p.clipDepthRange = (read<unsigned>(s) & 2) ? source_camera::ClipDepthRange::ZeroToOne
                                               : source_camera::ClipDepthRange::MinusOneToOne;
    p.tangent = read<float>(s);
    p.ownFar = read<float>(s);
    p.farOverride = read<float>(s);
    p.count = read<int>(s);
    p.ends = read<std::array<float, 4>>(s);
    p.direction = read<V4>(s);
    p.forceFit = read<unsigned>(s);
    p.width = read<unsigned>(s);
    p.height = read<unsigned>(s);
    p.noHalfTexel = read<unsigned>(s);
    p.mask22C = read<unsigned>(s);
    // SHC1 retains the declaration's legacy capacity word. Current collection
    // storage uses the actual record and catalog vectors, not this metadata.
    (void)read<unsigned>(s);
    int worlds = read<int>(s), records = read<int>(s);
    if (p.count < 1 || p.count > 4 || p.forceFit != 0)
        throw std::runtime_error("Unsupported compact source profile");
    p.records.resize(records);
    for (auto &r : p.records) {
        r.mask = read<unsigned>(s);
        r.index = read<unsigned>(s);
    }
    p.worlds.resize(worlds);
    for (auto &w : p.worlds) {
        w.index = read<unsigned>(s);
        w.allowedMask = read<unsigned>(s);
        w.exclusion = read<unsigned>(s);
        w.flags = read<int>(s);
        w.opacity = read<float>(s);
        w.primitiveCount = read<unsigned>(s);
    }
    return p;
}
inline V4 add(V4 a, V4 b) {
    return {A(a.X, b.X), A(a.Y, b.Y), A(a.Z, b.Z), A(a.W, b.W)};
}
inline V4 sub(V4 a, V4 b) {
    return {S(a.X, b.X), S(a.Y, b.Y), S(a.Z, b.Z), S(a.W, b.W)};
}
inline V4 scale(V4 a, float s) {
    return {M(a.X, s), M(a.Y, s), M(a.Z, s), M(a.W, s)};
}
inline V4 neg(V4 a) {
    return {flip(a.X), flip(a.Y), flip(a.Z), flip(a.W)};
}
inline float min(float a, float b) {
    return a < b ? a : b;
}
inline float max(float a, float b) {
    return a > b ? a : b;
}
inline V4 normalize(V4 a) {
    float inverse = D(1, std::sqrt(A(A(M(a.Y, a.Y), M(a.X, a.X)), M(a.Z, a.Z))));
    return {M(a.X, inverse), M(a.Y, inverse), M(a.Z, inverse), 0};
}
inline float dotPlane(V4 p, V4 q) {
    return A(A(A(M(p.Y, q.Y), M(p.X, q.X)), M(p.Z, q.Z)), M(p.W, q.W));
}
inline V4 plane(V4 n, V4 p) {
    return {n.X, n.Y, n.Z, flip(A(A(M(n.Y, p.Y), M(n.X, p.X)), M(n.Z, p.Z)))};
}
inline V4 row(const Matrix &m, int i) {
    return {m[i * 4], m[i * 4 + 1], m[i * 4 + 2], m[i * 4 + 3]};
}
inline Matrix rigidInverse(const Matrix &m) {
    float x = A(A(M(m[13], m[1]), M(m[12], m[0])), M(m[14], m[2])),
          y = A(A(M(m[13], m[5]), M(m[12], m[4])), M(m[14], m[6])),
          z = A(A(M(m[13], m[9]), M(m[12], m[8])), M(m[14], m[10]));
    return {m[0], m[4], m[8], 0, m[1], m[5], m[9], 0, m[2], m[6], m[10], 0, flip(x), flip(y), flip(z), 1};
}
inline Matrix lightView(V4 eye, V4 direction, V4 up) {
    float rx = S(M(up.Y, direction.Z), M(up.Z, direction.Y)),
          ry = S(M(direction.X, up.Z), M(up.X, direction.Z)),
          rz = S(M(up.X, direction.Y), M(direction.X, up.Y));
    float f = D(1, std::sqrt(A(A(M(ry, ry), M(rx, rx)), M(rz, rz))));
    rx = M(rx, f);
    ry = M(ry, f);
    rz = M(rz, f);
    float ux = S(M(direction.Y, rz), M(direction.Z, ry)), uy = S(M(direction.Z, rx), M(direction.X, rz)),
          uz = S(M(direction.X, ry), M(direction.Y, rx));
    f = D(1, std::sqrt(A(A(M(uy, uy), M(ux, ux)), M(uz, uz))));
    ux = M(ux, f);
    uy = M(uy, f);
    uz = M(uz, f);
    float tx = flip(A(A(M(ry, eye.Y), M(rx, eye.X)), M(rz, eye.Z))),
          ty = flip(A(A(M(uy, eye.Y), M(ux, eye.X)), M(uz, eye.Z))),
          tz = flip(A(A(M(direction.X, eye.X), M(direction.Y, eye.Y)), M(eye.Z, direction.Z)));
    return {rx, ux, direction.X, 0, ry, uy, direction.Y, 0, rz, uz, direction.Z, 0, tx, ty, tz, 1};
}
// The shadow camera retains an already published view unless the caller has
// explicitly requested a view rebuild. Its projection always uses the shadow
// far plane and ordinary depth, independently of the main camera's depth mode.
inline source_camera::CameraTransform
ownCamera(const Profile &profile, const source_camera::CameraTransform &published, bool rebuildView = false) {
    auto camera = published;
    camera.optics.farPlane = profile.ownFar;
    camera.optics.reversedDepth = false;
    if (camera.optics.projectionKind != source_camera::ProjectionKind::Perspective)
        throw std::runtime_error("Shadow camera requires perspective");
    if (rebuildView) {
        camera.unrolledView = source_camera::lookTo(camera.eye, camera.target, camera.up);
        camera.view = camera.unrolledView;
        if (camera.optics.roll != 0) {
            const auto rotation = source_camera::sinCos(flip(camera.optics.roll));
            for (int row = 0; row < 4; ++row) {
                const float x = camera.unrolledView[row * 4], y = camera.unrolledView[row * 4 + 1];
                camera.view[row * 4] = S(M(x, rotation[1]), M(y, rotation[0]));
                camera.view[row * 4 + 1] = A(M(x, rotation[0]), M(y, rotation[1]));
            }
        }
    }
    const float nearPlane = camera.optics.nearPlane, farPlane = camera.optics.farPlane;
    const float range = S(nearPlane, farPlane), yScale = D(1, M(profile.tangent, 1));
    float depthScale, depthTranslation;
    if (profile.clipDepthRange == source_camera::ClipDepthRange::ZeroToOne) {
        depthScale = D(farPlane, range);
        depthTranslation = M(depthScale, nearPlane);
    } else {
        const float inverse = D(1, range);
        depthScale = M(A(nearPlane, farPlane), inverse);
        depthTranslation = M(M(A(nearPlane, nearPlane), farPlane), inverse);
    }
    camera.projection = {D(yScale, camera.optics.aspectRatio),
                         0,
                         0,
                         0,
                         0,
                         yScale,
                         0,
                         0,
                         0,
                         0,
                         depthScale,
                         -1,
                         0,
                         0,
                         depthTranslation,
                         0};
    camera.worldToClip = source_camera::compose(camera.view, camera.projection);
    // Shadow frustum points are positions even if the incoming vectors' W differ.
    camera.eye.W = camera.target.W = 1.f;
    return camera;
}
inline Matrix workMatrix(const Profile &p, const source_camera::CameraTransform &camera) {
    float sourceNear = camera.optics.nearPlane, sourceFar = camera.optics.farPlane,
          aspect = camera.optics.aspectRatio;
    float hh = M(p.tangent, sourceFar), hw = M(aspect, hh);
    V4 lo{S(flip(hw), 1), S(flip(hh), 1), flip(sourceFar), 1}, hi{A(hw, 1), A(hh, 1), flip(sourceNear), 1};
    Matrix inverse = rigidInverse(camera.view);
    float hx = M(S(hi.X, lo.X), .5f), hy = M(S(hi.Y, lo.Y), .5f), hz = M(S(hi.Z, lo.Z), .5f),
          cx = A(hx, lo.X), cy = A(hy, lo.Y), cz = A(hz, lo.Z);
    float tx = A(A(A(M(cy, inverse[4]), M(cx, inverse[0])), M(cz, inverse[8])), inverse[12]),
          ty = A(A(A(M(cy, inverse[5]), M(cx, inverse[1])), M(cz, inverse[9])), inverse[13]),
          tz = A(A(A(M(cy, inverse[6]), M(cx, inverse[2])), M(cz, inverse[10])), inverse[14]);
    return {M(hx, inverse[0]),
            M(hx, inverse[1]),
            M(hx, inverse[2]),
            M(hx, inverse[3]),
            M(hy, inverse[4]),
            M(hy, inverse[5]),
            M(hy, inverse[6]),
            M(hy, inverse[7]),
            M(hz, inverse[8]),
            M(hz, inverse[9]),
            M(hz, inverse[10]),
            M(hz, inverse[11]),
            tx,
            ty,
            tz,
            1};
}
inline Volume workBox(Matrix m) {
    Volume b;
    V4 a = row(m, 0), v = row(m, 1), c = row(m, 2), center = row(m, 3);
    b.vertices[0] = {S(S(S(center.X, a.X), v.X), c.X), S(S(S(center.Y, a.Y), v.Y), c.Y),
                     S(S(S(center.Z, a.Z), v.Z), c.Z), center.W};
    auto da = add(a, a), dv = add(v, v), dc = add(c, c);
    auto &x = b.vertices;
    x[1] = add(x[0], dc);
    x[2] = add(x[0], dv);
    x[3] = add(x[2], dc);
    x[4] = add(x[0], da);
    x[5] = add(x[4], dc);
    x[6] = add(x[4], dv);
    x[7] = add(x[6], dc);
    b.planes = {plane(normalize(a), x[0]), plane(normalize(scale(a, -1)), x[7]),
                plane(normalize(v), x[0]), plane(normalize(scale(v, -1)), x[7]),
                plane(normalize(c), x[0]), plane(normalize(scale(c, -1)), x[7])};
    return b;
}
inline Volume frustum(Matrix basis, float sourceNear, float sourceFar, float tangentX, float tangentY) {
    Volume b;
    V4 right = row(basis, 0), up = row(basis, 1), forward = row(basis, 2), eye = row(basis, 3);
    for (int side = 0; side < 2; side++) {
        float distance = side == 0 ? sourceNear : sourceFar;
        V4 center{A(M(distance, forward.X), eye.X), A(M(distance, forward.Y), eye.Y),
                  A(M(distance, forward.Z), eye.Z), eye.W};
        auto h = scale(right, M(distance, tangentX)), w = scale(up, M(distance, tangentY)),
             below = sub(center, w), above = add(w, center);
        b.vertices[side] = sub(below, h);
        b.vertices[side + 2] = sub(above, h);
        b.vertices[side + 4] = add(h, below);
        b.vertices[side + 6] = add(h, above);
        for (int i = side; i < 8; i += 2)
            b.vertices[i].W = eye.W;
    }
    b.planes = {plane(normalize(add(scale(forward, tangentX), right)), eye),
                plane(normalize(add(neg(right), scale(forward, tangentX))), eye),
                plane(normalize(add(scale(forward, tangentY), up)), eye),
                plane(normalize(add(neg(up), scale(forward, tangentY))), eye),
                plane(forward, b.vertices[0]),
                plane(neg(forward), b.vertices[7])};
    return b;
}
inline std::vector<V4> intersect(const Volume &box, const Volume &fr) {
    float fd[8][6], bd[8][6];
    unsigned fm[8]{}, bm[8]{};
    std::vector<V4> out;
    out.reserve(48);
    int fc = 0, bc = 0;
    for (int v = 0; v < 8; v++) {
        for (int p = 0; p < 6; p++) {
            float first = dotPlane(fr.vertices[v], box.planes[p]),
                  second = dotPlane(box.vertices[v], fr.planes[p]);
            fd[v][p] = first;
            bd[v][p] = second;
            if (first >= 0)
                fm[v] |= 1u << p;
            if (second >= 0)
                bm[v] |= 1u << p;
        }
        if (fm[v] == 63) {
            out.push_back(fr.vertices[v]);
            fc++;
        }
        if (bm[v] == 63) {
            out.push_back(box.vertices[v]);
            bc++;
        }
    }
    if (fc == 8 || bc == 8)
        return out;
    auto append = [&](const Volume &v, const Volume &p, float d[8][6], unsigned masks[8], int first,
                      int second, int pl) {
        unsigned flag = 1u << pl;
        if ((masks[first] & flag) == (masks[second] & flag))
            return false;
        float t = D(d[first][pl], S(d[first][pl], d[second][pl]));
        if (t <= 0 || t > 1)
            return false;
        V4 point = add(scale(v.vertices[first], S(1, t)), scale(v.vertices[second], t));
        for (int other = 0; other < 6; other++)
            if (other != pl && dotPlane(point, p.planes[other]) < 0)
                return false;
        out.push_back(point);
        return out.size() == 48;
    };
    constexpr int edges[12][2]{{0, 1}, {2, 3}, {0, 2}, {1, 3}, {4, 5}, {6, 7},
                               {4, 6}, {5, 7}, {0, 4}, {1, 5}, {2, 6}, {3, 7}};
    for (auto &e : edges)
        for (int p = 0; p < 6; p++) {
            if (append(fr, box, fd, fm, e[0], e[1], p))
                return out;
            if (append(box, fr, bd, bm, e[0], e[1], p))
                return out;
        }
    return out;
}
inline constexpr float BoundsSentinel = std::bit_cast<float>(0x7722B3DBu);
inline constexpr float ParallelLightLower = std::bit_cast<float>(0x3F7FB1E0u);
inline constexpr float ParallelLightUpper = std::bit_cast<float>(0x3F802710u);
inline constexpr float ProjectionTolerance = std::bit_cast<float>(0x37480000u);
inline constexpr float MinimumDepthSpan = std::bit_cast<float>(0x3C23D70Au);
inline std::array<V4, 2> bounds(const std::vector<V4> &points, const Matrix &m) {
    float limit = BoundsSentinel;
    V4 lo{limit, limit, limit, 1}, hi{flip(limit), flip(limit), flip(limit), 1};
    for (auto p : points) {
        float x = A(A(A(M(p.Y, m[4]), M(p.X, m[0])), M(p.Z, m[8])), m[12]),
              y = A(A(A(M(p.X, m[1]), M(p.Y, m[5])), M(p.Z, m[9])), m[13]),
              z = A(A(A(M(p.X, m[2]), M(p.Y, m[6])), M(p.Z, m[10])), m[14]);
        lo = {min(lo.X, x), min(lo.Y, y), min(lo.Z, z), 1};
        hi = {max(hi.X, x), max(hi.Y, y), max(hi.Z, z), 1};
    }
    hi = {max(hi.X, lo.X), max(hi.Y, lo.Y), max(hi.Z, lo.Z), 1};
    return {lo, hi};
}
inline void compactResetAndVolume(const Profile &p, Frame &f) {
    V4 eye = f.camera.eye, target = f.camera.target, up = f.camera.up, forward = normalize(sub(target, eye));
    forward.W = S(target.W, eye.W);
    Matrix view = lightView(eye, forward, up),
           basis{view[0],   view[4],   view[8],   0,         view[1], view[5], view[9], 0,
                 forward.X, forward.Y, forward.Z, forward.W, eye.X,   eye.Y,   eye.Z,   eye.W};
    float yDot = A(A(M(p.direction.X, 0), p.direction.Y), M(p.direction.Z, 0)), ay = std::fabs(yDot);
    float low = ParallelLightLower, high = ParallelLightUpper;
    V4 lightUp = ay >= low && ay <= high ? V4{0, 0, 1, 0} : V4{0, 1, 0, 0};
    Matrix light = lightView(eye, p.direction, lightUp), axis = rigidInverse(light);
    auto box = workBox(f.workVolumeTransform);
    float sourceNear = f.camera.optics.nearPlane, aspect = f.camera.optics.aspectRatio,
          tanX = M(p.tangent, aspect);
    float limit = BoundsSentinel;
    for (int i = 0; i < p.count; i++) {
        auto &pass = f.passes[i];
        pass.minimum = {limit, limit, limit, 1};
        pass.maximum = {flip(limit), flip(limit), flip(limit), 1};
        pass.clipPresent = pass.casterDirty = 0;
        pass.casterMinimum = {limit, limit, limit, 1};
        pass.casterMaximum = {flip(limit), flip(limit), flip(limit), 1};
        float sourceFar = p.ends[i];
        if (sourceFar > sourceNear) {
            auto points = intersect(box, frustum(basis, sourceNear, sourceFar, tanX, p.tangent));
            auto b = bounds(points, light);
            pass.minimum = b[0];
            pass.maximum = b[1];
            pass.lightView = light;
            pass.basis = axis;
            pass.viewAvailable = pass.basisAvailable = 1;
            pass.clipPresent = !points.empty();
            if (pass.clipPresent)
                f.effectiveMask |= 1u << i;
        }
        sourceNear = sourceFar;
    }
}
inline V4 support(const std::array<float, 20> &obb, V4 direction, bool culling) {
    V4 point{obb[0], obb[1], obb[2], 0};
    for (int axis = 0; axis < 3; axis++) {
        const float *b = obb.data() + 4 + axis * 4;
        float nx = culling ? flip(direction.X) : direction.X, ny = culling ? flip(direction.Y) : direction.Y,
              nz = culling ? flip(direction.Z) : direction.Z;
        float dot = A(M(b[1], ny), M(b[0], nx));
        dot = A(dot, M(b[2], nz));
        float extent = M(obb[16 + axis], dot <= 0 ? -1.f : 1.f);
        float *q = &point.X;
        for (int k = 0; k < 3; k++) {
            float product = M(extent, b[k]);
            q[k] = culling ? A(product, q[k]) : A(q[k], product);
        }
    }
    return point;
}
inline Matrix worldBox(const std::array<float, 20> &obb) {
    V4 lx = support(obb, {-1, 0, 0, 0}, false), ly = support(obb, {0, -1, 0, 0}, false),
       lz = support(obb, {0, 0, -1, 0}, false), hx = support(obb, {1, 0, 0, 0}, false),
       hy = support(obb, {0, 1, 0, 0}, false), hz = support(obb, {0, 0, 1, 0}, false);
    float x = M(S(hx.X, lx.X), .5f), y = M(S(hy.Y, ly.Y), .5f), z = M(S(hz.Z, lz.Z), .5f);
    return {x, 0, 0, 0, 0, y, 0, 0, 0, 0, z, 0, A(x, lx.X), A(y, ly.Y), A(z, lz.Z), 1};
}
inline float radius(const Matrix &box, V4 axis) {
    float r1 = A(A(M(axis.Y, box[5]), M(axis.X, box[4])), M(axis.Z, box[6])),
          r0 = A(A(M(box[0], axis.X), M(box[1], axis.Y)), M(box[2], axis.Z)),
          r2 = A(A(M(axis.Y, box[9]), M(axis.X, box[8])), M(axis.Z, box[10]));
    return A(A(std::fabs(r1), std::fabs(r0)), std::fabs(r2));
}
inline unsigned testAndAccumulate(Frame &f, const Matrix &box, Admission &admission) {
    unsigned accepted = 0;
    for (int i = 0; i < f.passCount; i++) {
        unsigned bit = 1u << i;
        if (!(f.effectiveMask & bit))
            continue;
        auto &p = f.passes[i];
        auto &m = p.lightView;
        float x = A(A(A(M(box[13], m[4]), M(box[12], m[0])), M(box[14], m[8])), m[12]),
              y = A(A(A(M(box[12], m[1]), M(box[13], m[5])), M(box[14], m[9])), m[13]),
              z = A(A(A(M(box[12], m[2]), M(box[13], m[6])), M(box[14], m[10])), m[14]);
        float ex = radius(box, row(p.basis, 0)), ey = radius(box, row(p.basis, 1)),
              ez = radius(box, row(p.basis, 2));
        V4 lo{S(x, ex), S(y, ey), S(z, ez), 1}, hi{A(x, ex), A(y, ey), A(z, ez), 1};
        if (!(lo.Z < p.maximum.Z) || !(min(hi.X, p.maximum.X) > max(lo.X, p.minimum.X)) ||
            !(min(hi.Y, p.maximum.Y) > max(lo.Y, p.minimum.Y)))
            continue;
        accepted |= bit;
        p.casterMinimum = {min(p.casterMinimum.X, lo.X), min(p.casterMinimum.Y, lo.Y),
                           min(p.casterMinimum.Z, lo.Z), min(p.casterMinimum.W, lo.W)};
        p.casterMaximum = {max(p.casterMaximum.X, hi.X), max(p.casterMaximum.Y, hi.Y),
                           max(p.casterMaximum.Z, hi.Z), max(p.casterMaximum.W, hi.W)};
        p.casterDirty = 1;
        admission.distances[i] = lo.Z;
        admission.distanceWritten[i] = 1;
    }
    f.touchedMask |= accepted;
    return accepted;
}
inline std::uint64_t firstDistanceKey(float distance) {
    float value = M(A(distance, 100), 100);
    constexpr float two63 = 9223372036854775808.f;
    std::uint64_t add = 0;
    if (!(value < two63)) {
        value = S(value, two63);
        if (!(value >= two63))
            add = 0x8000000000000000ULL;
    }
    return std::uint64_t(_mm_cvttss_si64(_mm_set_ss(value))) + add;
}
inline Matrix translate(Matrix m, V4 t) {
    for (int i = 0; i < 4; i++) {
        m[i * 4] = A(M(m[i * 4 + 3], t.X), m[i * 4]);
        m[i * 4 + 1] = A(M(m[i * 4 + 3], t.Y), m[i * 4 + 1]);
        m[i * 4 + 2] = A(M(m[i * 4 + 3], t.Z), m[i * 4 + 2]);
    }
    return m;
}
inline Matrix textureMatrix(Matrix m) {
    for (int i = 0; i < 4; i++) {
        m[i * 4] = M(m[i * 4], .5f);
        m[i * 4 + 1] = M(m[i * 4 + 1], -.5f);
    }
    return translate(m, {.5f, .5f, 0, 0});
}
inline void finalPasses(Frame &f, const Profile &p) {
    for (int i = 0; i < p.count; i++) {
        auto &pass = f.passes[i];
        auto view = pass.lightView;
        if (!pass.viewAvailable)
            throw std::runtime_error("Retained cascade view has no preceding volume or final writer");
        if (!pass.casterDirty && !pass.clipPresent) {
            pass.centered = view;
            pass.shadow = f.camera.view;
            float sourceFar = f.camera.optics.farPlane;
            pass.shadow[15] = A(sourceFar, sourceFar);
            pass.shadowWritten = pass.shadowAvailable = 1;
            continue;
        }
        V4 lo = pass.minimum, hi = pass.maximum;
        if (pass.casterDirty) {
            lo = {max(lo.X, pass.casterMinimum.X), max(lo.Y, pass.casterMinimum.Y), pass.casterMinimum.Z,
                  lo.W};
            hi = {max(min(hi.X, pass.casterMaximum.X), lo.X), max(min(hi.Y, pass.casterMaximum.Y), lo.Y),
                  max(min(hi.Z, pass.casterMaximum.Z), lo.Z), hi.W};
        }
        float width = S(hi.X, lo.X), height = S(hi.Y, lo.Y), depth = S(hi.Z, lo.Z),
              tx = flip(A(M(hi.X, .5f), M(lo.X, .5f))), ty = flip(A(M(lo.Y, .5f), M(hi.Y, .5f)));
        pass.centered = translate(view, {tx, ty, 0, 0});
        float halfWidth = M(width, .5f), halfHeight = M(height, .5f), sourceFar = A(lo.Z, depth);
        if (!(halfWidth > 0 && halfHeight > 0 && sourceFar > lo.Z))
            continue;
        float sourceNear = lo.Z, span = S(sourceFar, sourceNear);
        float low = -ProjectionTolerance, high = ProjectionTolerance;
        if ((halfWidth >= low && halfWidth <= high) || (halfHeight >= low && halfHeight <= high))
            throw std::runtime_error("Degenerate shadow projection has no preceding retained projection");
        if (span >= low && span <= high) {
            span = MinimumDepthSpan;
        }
        Matrix projection{
            D(-1, halfWidth),          0, 0, 0, 0, D(1, halfHeight), 0, 0, 0, 0, D(1, span), 0, 0, 0,
            flip(D(sourceNear, span)), 1};
        pass.shadow = source_camera::compose(pass.centered, projection);
        pass.shadowWritten = pass.shadowAvailable = 1;
        pass.depthSpan = S(sourceFar, lo.Z);
        pass.depth0 = {0, 0, S(lo.Z, sourceFar), flip(lo.Z)};
        pass.depth1 = {0, 0, 0, 1};
    }
    if (!f.passes[0].shadowAvailable)
        throw std::runtime_error("The first cascade shadow matrix requires a preceding final writer");
    auto inverse = source_matrix::inverse(textureMatrix(f.passes[0].shadow));
    V4 eye = f.camera.eye, target = f.camera.target, up = f.camera.up,
       direction = normalize(sub(target, eye));
    auto view = lightView(eye, direction, up);
    V4 depth{view[2], view[6], view[10], S(view[14], p.ends[0])};
    f.depthPlane = {dotPlane(depth, row(inverse, 0)), dotPlane(depth, row(inverse, 1)),
                    dotPlane(depth, row(inverse, 2)), dotPlane(depth, row(inverse, 3))};
    f.relativeEnds = {S(p.ends[1], p.ends[0]), S(p.ends[2], p.ends[0]), S(p.ends[3], p.ends[0]), 0};
}
inline void atlas(Frame &f, const Profile &p) {
    int active = 0;
    for (int i = 0; i < p.count; i++)
        if (f.touchedMask & (1u << i))
            active++;
    int columns = 1, rows = 1;
    if (active == 2 || active == 3) {
        if (p.width > p.height)
            columns = active;
        else
            rows = active;
    } else if (active == 4) {
        unsigned vertical = std::min(p.height / 4, p.width), horizontal = std::min(p.width / 4, p.height),
                 square = std::min(p.width / 2, p.height / 2);
        if (horizontal < vertical && vertical > square)
            rows = 4;
        else if (horizontal > vertical && horizontal > square)
            columns = 4;
        else
            columns = rows = 2;
    }
    int pitchW = int(p.width) / columns, pitchH = int(p.height) / rows, selected = 0;
    for (int i = 0; i < p.count; i++) {
        auto &r = f.atlas[i];
        if (f.touchedMask & (1u << i)) {
            r.x = 1 + (selected % columns) * pitchW;
            r.y = 1 + (selected / columns) * pitchH;
            r.width = pitchW - 2;
            r.height = pitchH - 2;
            selected++;
        } else {
            r.x = r.y = 0;
            r.width = r.height = 1;
        }
        r.unitWidth = r.width + 2;
        r.unitHeight = r.height + 2;
        float halfX = p.noHalfTexel ? 0 : D(.5f, float(p.width)),
              halfY = p.noHalfTexel ? 0 : D(.5f, float(p.height));
        r.scaleOffset = {D(float(r.unitWidth), float(p.width)), D(float(r.unitHeight), float(p.height)),
                         A(D(float(r.x - 1), float(p.width)), halfX),
                         A(D(float(r.y - 1), float(p.height)), halfY)};
    }
}
template <class ModelCatalog>
inline Frame prepareCurrent(const Profile &p, const source_camera::Frame &camera, const ModelCatalog &models,
                            Owner &owner) {
    if (models.instances.size() != p.worlds.size())
        throw std::runtime_error("Shadow candidates require same complete imported catalog");
    Frame f;
    f.passCount = p.count;
    restoreOwned(f, owner);
    f.camera = ownCamera(p, camera, camera.viewNeedsRebuild);
    f.compactFar = p.farOverride > 0 ? p.farOverride : camera.optics.farPlane;
    f.workVolumeTransform = workMatrix(p, f.camera);
    compactResetAndVolume(p, f);
    f.ordinaryRecordCounts.resize(p.records.size());
    std::array<float, 4> distances{};
    std::array<unsigned, 4> written{};
    for (size_t n = 0; n < p.worlds.size(); n++) {
        auto &w = p.worlds[n];
        auto &model = models.instances[n];
        if (w.index != model.index || w.primitiveCount != model.primitiveCount)
            throw std::runtime_error("Shadow/source catalog row identity differs");
        Admission admission;
        admission.index = w.index;
        admission.distances = distances;
        admission.distanceWritten = written;
        if (w.allowedMask)
            admission.mask = testAndAccumulate(f, worldBox(model.obb), admission) & w.allowedMask;
        distances = admission.distances;
        written = admission.distanceWritten;
        for (int i = 0; i < 4; i++)
            if (admission.mask & (1u << i)) {
                admission.firstAcceptedDistanceKey = firstDistanceKey(admission.distances[i]);
                break;
            }
        for (int i = 0; i < 4; i++)
            if (admission.mask & (1u << i)) {
                f.instanceCounts[i]++;
                f.primitiveCounts[i] += w.primitiveCount;
            }
        f.admissions.push_back(admission);
        bool visible = model.mode == 3;
        if (model.mode == 0) {
            visible = true;
            for (int pi : {0, 1, 4, 2, 3, 5}) {
                auto q = camera.planes[pi].equation;
                V4 point = support(model.obb, q, true);
                if (!(A(A(A(M(point.Y, q.Y), M(point.X, q.X)), M(point.Z, q.Z)), q.W) <= 0)) {
                    visible = false;
                    break;
                }
            }
        }
        if (visible && w.opacity > 0 && w.ordinaryFirstMask)
            for (size_t record = 0; record < p.records.size(); record++)
                if ((p.records[record].mask & w.exclusion) == 0)
                    f.ordinaryRecordCounts[record] += w.primitiveCount;
    }
    finalPasses(f, p);
    atlas(f, p);
    retainOwned(owner, f);
    return f;
}
template <class ModelCatalog>
inline Frame prepareCurrent(const Profile &p, const source_camera::Frame &camera,
                            const ModelCatalog &models) {
    Owner sourceFresh;
    return prepareCurrent(p, camera, models, sourceFresh);
}
} // namespace source_shadow_camera
