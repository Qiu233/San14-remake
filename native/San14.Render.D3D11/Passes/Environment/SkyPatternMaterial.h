#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "../../Camera/SkyPatternCamera.h"
#include "CloudVolumeMaterial.h"
#include <algorithm>
#include <cstring>
#include <cmath>
#include <filesystem>
#include <fstream>
#include <span>
#include <stdexcept>
#include <vector>

namespace San14::Render {
struct SkyPatternMaterial {
    source_sky_pattern::VertexMaterial vertex;
    source_camera::V4 eye{};
    CloudVolumeMaterial volume;
    source_camera::V4 raySamples{}, earthRadius{};
};
inline SkyPatternMaterial prepareSkyPatternMaterial(const source_sky_pattern::Cameras& cameras,
    source_camera::V4 clipPlane, const CloudVolumeMaterial& volume, float planetRadius) {
    using source_camera::A;
    const auto bottom = A(volume.layerGeometry[0], planetRadius);
    return {source_sky_pattern::prepareVertex(cameras, clipPlane), cameras.temporary.eye, volume,
        {volume.raySampleSlopeAndMaximum[0], volume.raySampleSlopeAndMaximum[1], 0,
            cameras.temporary.optics.viewSpan},
        {planetRadius, bottom, A(bottom, volume.layerGeometry[1]), A(volume.cirrusAltitude, planetRadius)}};
}

struct SkyPatternFieldView { const void* data; std::size_t bytes; };
inline SkyPatternFieldView skyPatternField(const SkyPatternMaterial& material, std::uint32_t id) {
#define PATTERN_FIELD(number, member) case number: return {&material.member, sizeof(material.member)}
    switch (id) {
        PATTERN_FIELD(0, vertex.clipPlane); PATTERN_FIELD(1, vertex.projectionToWorld);
        PATTERN_FIELD(2, vertex.directions); PATTERN_FIELD(3, vertex.projectionFarNear);
        PATTERN_FIELD(4, eye); PATTERN_FIELD(5, volume.baseOffset); PATTERN_FIELD(6, volume.baseScale);
        PATTERN_FIELD(7, volume.detailOffset); PATTERN_FIELD(8, volume.detailScale);
        PATTERN_FIELD(9, volume.distortOffset); PATTERN_FIELD(10, volume.distortScale);
        PATTERN_FIELD(11, raySamples); PATTERN_FIELD(12, earthRadius);
        PATTERN_FIELD(13, volume.layerGeometry); PATTERN_FIELD(14, volume.densityParameters);
        PATTERN_FIELD(15, volume.distortionScale); PATTERN_FIELD(16, volume.erosionScale);
        PATTERN_FIELD(17, volume.cirrusOffset); PATTERN_FIELD(18, volume.cirrusScale);
        PATTERN_FIELD(19, volume.cirrusDistortOffset); PATTERN_FIELD(20, volume.cirrusDistortScale);
        PATTERN_FIELD(21, volume.cirrusParameters); PATTERN_FIELD(22, volume.cirrusDistortionScale);
        default: throw std::runtime_error("Unknown Pattern material semantic");
    }
#undef PATTERN_FIELD
}
inline constexpr std::array<std::uint32_t, 23> SkyPatternFieldBytes{
    16, 64, 64, 8, 16, 12, 12, 12, 12, 8, 8, 16, 16, 16, 16, 16, 8,
    8, 8, 8, 8, 16, 8};
struct SkyPatternMaterialWrite { std::uint32_t field{}, offset{}, bytes{}; };
struct SkyPatternStageLayout {
    std::uint32_t slot{}, bytes{};
    std::vector<SkyPatternMaterialWrite> writes;
    void pack(const SkyPatternMaterial& material, std::span<char> output) const {
        if (output.size() != bytes) throw std::runtime_error("Pattern material stage extent");
        for (const auto& write : writes) {
            const auto* source=static_cast<const char*>(skyPatternField(material, write.field).data);
            // The actual used RDEF fields share Data's finite material domain.
            // Unused semantic values do not add a draw-admission condition.
            for(std::uint32_t offset=0;offset<write.bytes;offset+=4) {
                float value;std::memcpy(&value,source+offset,4);
                if(!std::isfinite(value))throw std::runtime_error("Pattern used material field is nonfinite");
            }
            std::memcpy(output.data() + write.offset, source, write.bytes);
        }
    }
};
struct SkyPatternTextureRoute { std::uint32_t role{}, slot{}, dimension{}; };
struct SkyPatternSamplerRoute { std::uint32_t slot{}, textureSlot{}; };
struct SkyPatternMaterialLayout {
    SkyPatternStageLayout vertex, pixel;
    std::vector<SkyPatternTextureRoute> textures;
    std::vector<SkyPatternSamplerRoute> samplers;
    bool usesIntegration{};
};
namespace Interop {
inline SkyPatternMaterialLayout readSkyPatternMaterialLayout(const std::filesystem::path& path) {
    NativePreparedInputFile input(path, std::ios::binary);
    auto word = [&]() {
        std::uint32_t value{}; input.read(reinterpret_cast<char*>(&value), 4);
        if (!input) throw std::runtime_error("Incomplete Pattern PML1 layout");
        return value;
    };
    if (word() != 0x314C4D50u) throw std::runtime_error("Pattern PML1 layout version");
    SkyPatternMaterialLayout result;
    result.vertex.slot = word(); result.vertex.bytes = word(); const auto vertexCount = word();
    result.pixel.slot = word(); result.pixel.bytes = word(); const auto pixelCount = word();
    const auto textureCount = word(), samplerCount = word(), integration = word();
    if (textureCount > 128 || samplerCount > 16 || integration > 1)
        throw std::runtime_error("Pattern material binding counts");
    result.usesIntegration = integration != 0;
    auto stage = [&](SkyPatternStageLayout& layout, std::uint32_t count, bool vertex) {
        if (layout.slot >= 14 || !layout.bytes || layout.bytes > 65536 || layout.bytes % 16 || count > 4096)
            throw std::runtime_error("Pattern constant stage format");
        for (std::uint32_t index = 0; index < count; ++index) {
            SkyPatternMaterialWrite write{word(), word(), word()};
            if (write.field >= SkyPatternFieldBytes.size() || (vertex ? write.field >= 4 : write.field < 4) ||
                !write.bytes || write.bytes > SkyPatternFieldBytes[write.field] ||
                (write.offset | write.bytes) % 4 || std::uint64_t(write.offset) + write.bytes > layout.bytes)
                throw std::runtime_error("Pattern material semantic range");
            layout.writes.push_back(write);
        }
    };
    stage(result.vertex, vertexCount, true); stage(result.pixel, pixelCount, false);
    for (std::uint32_t index = 0; index < textureCount; ++index) {
        SkyPatternTextureRoute route{word(), word(), word()};
        if (route.role >= 4 || route.slot >= 128 || route.dimension != (route.role < 2 ? 8u : 4u))
            throw std::runtime_error("Pattern material texture role");
        result.textures.push_back(route);
    }
    for (std::uint32_t index = 0; index < samplerCount; ++index) {
        SkyPatternSamplerRoute route{word(), word()};
        if (route.slot >= 16 || std::none_of(result.textures.begin(), result.textures.end(),
            [&](const auto& texture) { return texture.slot == route.textureSlot; }))
            throw std::runtime_error("Pattern material sampler association");
        result.samplers.push_back(route);
    }
    if (input.peek() != std::char_traits<char>::eof()) throw std::runtime_error("Unexpected Pattern layout tail");
    return result;
}
} // namespace Interop
} // namespace San14::Render
