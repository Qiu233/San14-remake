// Original shared camera writer group for selected HDR78/123/166 layouts.
// Preserves complete caller templates. Other source groups are independent.
#pragma once
#include "../../Camera/CameraMatrix.h"
#include "../../Camera/CameraTransform.h"
#include <array>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <map>
#include <stdexcept>
#include <string>
#include <vector>

struct NativeDeferredCameraFields {
    std::array<float,4> eye{};
    source_matrix::Matrix worldToProjection{},viewToWorld{},projectionToWorld{},projectionToView{};
    std::array<float,12> worldToViewRows{};
};
inline NativeDeferredCameraFields prepareNativeDeferredCameraFields(const source_camera::CameraTransform& camera) {
    NativeDeferredCameraFields result;
    const auto& view=camera.view;const auto& projection=camera.projection;
    result.eye={camera.eye.X,camera.eye.Y,camera.eye.Z,camera.eye.W};
    result.worldToProjection=camera.worldToClip;
    // mW2P is the original matrix. mWV is only the transposed affine 3 rows.
    for(unsigned row=0;row<3;++row)for(unsigned column=0;column<4;++column)
        result.worldToViewRows[row*4+column]=view[column*4+row];
    result.viewToWorld=source_matrix::inverse(view);
    result.projectionToWorld=source_matrix::inverse(result.worldToProjection);
    result.projectionToView=source_matrix::inverse(projection);
    return result;
}
struct NativeDeferredCameraWrite {unsigned kind{},offset{},sourceOffset{},bytes{};};
struct NativeDeferredCameraConstant {
    std::vector<NativeDeferredCameraWrite> writes;
};
inline void applyNativeDeferredCameraConstant(const NativeDeferredCameraConstant& source,
    const NativeDeferredCameraFields& current,std::vector<char>& bytes) {
    const void* values[]{current.eye.data(),current.worldToProjection.data(),current.worldToViewRows.data(),
        current.viewToWorld.data(),current.projectionToWorld.data(),current.projectionToView.data()};
    for(const auto& write:source.writes)std::memcpy(bytes.data()+write.offset,
        static_cast<const char*>(values[write.kind])+write.sourceOffset,write.bytes);
}
