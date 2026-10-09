#pragma once
#include <array>

namespace source_camera {
struct V3 {float X,Y,Z;};
struct V4 {float X,Y,Z,W;};
using Matrix=std::array<float,16>;

enum class ProjectionKind {Perspective,Orthographic};
enum class ClipDepthRange {MinusOneToOne,ZeroToOne};
struct CameraOptics {
    float nearPlane{},farPlane{},aspectRatio{},roll{};
    float viewSpan{}; // Published vertical view span used by screen-space effects.
    ProjectionKind projectionKind=ProjectionKind::Perspective;
    bool reversedDepth{};
};

// Camera mathematics owns these values. Interchange packets and GPU constant
// buffers are projections of this state, rather than a backing byte arena.
struct CameraTransform {
    V4 eye{},target{},up{};
    Matrix unrolledView{},view{},projection{},worldToClip{};
    CameraOptics optics{};
};
} // namespace source_camera
