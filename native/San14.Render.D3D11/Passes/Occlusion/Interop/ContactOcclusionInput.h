#pragma once
#include "../ContactOcclusionCurrent.h"
#include <cstdint>
#include <cstring>
#include <span>
#include <stdexcept>
#include <vector>

namespace San14::Render {
struct ContactOcclusionInput {
    contact_occlusion::Settings settings;
    std::vector<contact_occlusion::LightRecord> lights;
};

namespace Interop {
// OCC1 is project-owned interchange. Neither original Main offsets nor light
// memory layouts enter the current occlusion algorithm.
inline ContactOcclusionInput decodeContactOcclusionInput(std::span<const std::uint8_t> bytes) {
    constexpr std::size_t headerBytes=84,lightBytes=52;
    if(bytes.size()<headerBytes)throw std::runtime_error("Current contact-occlusion input is truncated");
    std::size_t cursor=0;
    auto word=[&]() {std::uint32_t value;std::memcpy(&value,bytes.data()+cursor,4);cursor+=4;return value;};
    auto scalar=[&]() {float value;std::memcpy(&value,bytes.data()+cursor,4);cursor+=4;return value;};
    if(word()!=0x3143434Fu)throw std::runtime_error("Current contact-occlusion OCC1 signature");
    const auto count=word();
    if(count!=(bytes.size()-headerBytes)/lightBytes||
        (bytes.size()-headerBytes)%lightBytes)
        throw std::runtime_error("Current contact-occlusion ordered light extent");
    ContactOcclusionInput result;
    auto& settings=result.settings;
    settings.radius=scalar();settings.endRadius=scalar();
    settings.rangeStart=scalar();settings.rangeEnd=scalar();
    settings.maxRadiusRate=scalar();settings.contrast=scalar();
    settings.maxOcclusion=scalar();settings.angleBiasDegrees=scalar();
    settings.quality=word();settings.resolution=word();settings.useNormal=word();settings.useViewDepth=word();
    settings.rayCount=word();settings.stepCount=word();settings.blurSize=word();settings.blurSharpness=scalar();
    settings.blurLinearFilter=word();settings.blurAtHalfSize=word();settings.suppressHalo=word();
    result.lights.resize(count);
    for(auto& light:result.lights) {
        light.groupMask=word();
        auto& modifier=light.modifier;
        modifier.present=word()!=0;modifier.suppressCopy=word()!=0;
        modifier.fade=scalar();modifier.radius=scalar();modifier.endRadius=scalar();
        modifier.rangeStart=scalar();modifier.rangeEnd=scalar();modifier.maxRadiusRate=scalar();
        modifier.contrast=scalar();modifier.maxOcclusion=scalar();modifier.angleBias=scalar();modifier.stepCount=scalar();
    }
    return result;
}
} // namespace Interop
} // namespace San14::Render
