#pragma once
#include "AmbientEnvironment.h"

namespace source_current_ambient {
using namespace source_camera;
using source_atmosphere_camera::F;
struct FaceGeometry {
    V4 eye{}, ordinaryEye{};
    float focusDistance{}, heightOffset{};
    Matrix projection{};
};
inline FaceGeometry prepareFaceGeometry(const CameraTransform &ordinary, float altitude) {
    FaceGeometry geometry;
    const float dx = S(ordinary.target.X, ordinary.eye.X), dy = S(ordinary.target.Y, ordinary.eye.Y),
                dz = S(ordinary.target.Z, ordinary.eye.Z);
    geometry.focusDistance = std::sqrt(A(A(M(dy, dy), M(dx, dx)), M(dz, dz)));
    geometry.heightOffset = M(altitude, 100000);
    geometry.ordinaryEye = ordinary.eye;
    geometry.eye = ordinary.eye;
    geometry.eye.Y = A(geometry.eye.Y, geometry.heightOffset);
    const float nearPlane = ordinary.optics.nearPlane, farPlane = ordinary.optics.farPlane;
    const bool reversed = ordinary.optics.reversedDepth;
    float depthScale =
        D(reversed ? nearPlane : farPlane, reversed ? S(farPlane, nearPlane) : S(nearPlane, farPlane));
    geometry.projection = {
        -1, 0, 0, 0, 0, 1, 0, 0, 0, 0, depthScale, -1, 0, 0, M(depthScale, reversed ? farPlane : nearPlane),
        0};
    return geometry;
}
inline CameraTransform prepareFaceCamera(const CameraTransform &ordinary, const FaceGeometry &geometry,
                                         unsigned face) {
    static constexpr std::array<V4, 6> directions{
        {{1, 0, 0, 1}, {-1, 0, 0, 1}, {0, 1, 0, 1}, {0, -1, 0, 1}, {0, 0, 1, 1}, {0, 0, -1, 1}}};
    static constexpr std::array<V4, 6> ups{
        {{0, 1, 0, 0}, {0, 1, 0, 0}, {0, 0, -1, 0}, {0, 0, 1, 0}, {0, 1, 0, 0}, {0, 1, 0, 0}}};
    const auto axis = directions[face];
    const auto eye = geometry.eye;
    CameraTransform camera = ordinary;
    camera.eye = eye;
    // Keep the vertical addition order: focus is based on the ordinary eye,
    // whereas the view originates at the independently raised eye.
    camera.target = {A(M(axis.X, geometry.focusDistance), eye.X),
                     A(A(M(axis.Y, geometry.focusDistance), geometry.ordinaryEye.Y), geometry.heightOffset),
                     A(M(axis.Z, geometry.focusDistance), eye.Z), 1};
    camera.up = ups[face];
    camera.view = lookTo(eye, camera.target, camera.up);
    camera.unrolledView = camera.view;
    camera.projection = geometry.projection;
    camera.worldToClip = compose(camera.view, camera.projection);
    camera.optics.viewSpan = F(0x3fc90fdb);
    camera.optics.aspectRatio = -1;
    camera.optics.roll = 0;
    // Every cube face has a perspective projection, irrespective of source lens kind.
    camera.optics.projectionKind = ProjectionKind::Perspective;
    return camera;
}
} // namespace source_current_ambient
