// Original shared camera writer group for selected HDR78/123/166 layouts.
// Preserves complete caller templates. Other source groups are independent.
#pragma once
#include "camera-matrix-native.h"
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
inline NativeDeferredCameraFields prepareNativeDeferredCameraFields(const std::array<std::uint8_t,0x3C0>& prefix) {
    NativeDeferredCameraFields result;
    source_matrix::Matrix view{},projection{};
    std::memcpy(result.eye.data(),prefix.data()+0x10,16);
    std::memcpy(view.data(),prefix.data()+0x80,64);
    std::memcpy(projection.data(),prefix.data()+0xC0,64);
    std::memcpy(result.worldToProjection.data(),prefix.data()+0x100,64);
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
    std::vector<char> templateBytes;
    std::vector<NativeDeferredCameraWrite> writes;
};
struct NativeDeferredCameraProfile {std::map<std::string,NativeDeferredCameraConstant> constants;};
inline NativeDeferredCameraProfile prepareNativeDeferredCameraProfile(const std::filesystem::path& source) {
    NativeDeferredCameraProfile result;
    std::ifstream names(source/"passes.txt");std::string name;
    if(!names)throw std::runtime_error("Original deferred camera layout catalogue absent");
    constexpr unsigned sourceWidths[]{16,64,48,64,64,64};
    while(names>>name) {
        auto& constant=result.constants[name];
        std::ifstream bytes(source/name/"source-ps-b0.bin",std::ios::binary|std::ios::ate);
        if(!bytes)throw std::runtime_error("Original complete deferred camera template absent");
        constant.templateBytes.resize(static_cast<std::size_t>(bytes.tellg()));bytes.seekg(0);
        bytes.read(constant.templateBytes.data(),constant.templateBytes.size());
        std::ifstream layout(source/name/"camera-writes.txt");NativeDeferredCameraWrite write;
        if(!layout||!bytes)throw std::runtime_error("Original deferred camera static input incomplete");
        while(layout>>write.kind>>write.offset>>write.sourceOffset>>write.bytes) {
            if(write.kind>=std::size(sourceWidths)||write.sourceOffset+write.bytes>sourceWidths[write.kind]||
                write.offset+write.bytes>constant.templateBytes.size())
                throw std::runtime_error("Original deferred camera write range differs from its RDEF");
            constant.writes.push_back(write);
        }
    }
    return result;
}
inline void applyNativeDeferredCameraConstant(const NativeDeferredCameraConstant& source,
    const NativeDeferredCameraFields& current,std::vector<char>& bytes) {
    const void* values[]{current.eye.data(),current.worldToProjection.data(),current.worldToViewRows.data(),
        current.viewToWorld.data(),current.projectionToWorld.data(),current.projectionToView.data()};
    for(const auto& write:source.writes)std::memcpy(bytes.data()+write.offset,
        static_cast<const char*>(values[write.kind])+write.sourceOffset,write.bytes);
}
inline std::vector<char> prepareNativeDeferredCameraConstant(const NativeDeferredCameraConstant& source,
    const NativeDeferredCameraFields& current) {
    auto bytes=source.templateBytes;
    applyNativeDeferredCameraConstant(source,current,bytes);return bytes;
}
