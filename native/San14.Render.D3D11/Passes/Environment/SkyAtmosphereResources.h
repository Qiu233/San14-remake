#pragma once
#include "AmbientRenderer.h"
#include "SkyAtmosphereInputs.h"
#include <utility>

namespace San14::Render {
struct NativeSkyAtmospherePlaneEntry {
    std::uint32_t staticObjectKey{};
    AmbientNativeTextureBinding texture;
};

// The same retained Sky adopts actual immutable image owners already prepared
// for WORK. Only missing Plane children use the source-backed fallback media.
class NativeSkyAtmosphereResources {
public:
    static NativeSkyAtmosphereResources prepare(ID3D11Device* device,
        const std::filesystem::path& source, const std::vector<AmbientNativeTextureBinding>& existing) {
        auto inputs = Interop::readSkyAtmosphereInputs(source);
        auto fallback = ambientTextures(device, source, "textures.tsv", std::nullopt);
        std::array<NativeSkyAtmospherePlaneEntry, 4> plane;
        for (std::size_t index = 0; index < plane.size(); ++index) {
            const auto& declared = inputs.plane[index];
            auto find = [&](const auto& textures) -> const AmbientNativeTextureBinding* {
                for (const auto& texture : textures)
                    if (texture.identity == declared.image) return &texture;
                return nullptr;
            };
            const auto* texture = find(existing);
            if (!texture) texture = find(fallback);
            if (!texture || !texture->resource || !texture->view || !texture->sampler)
                throw std::runtime_error("Atmosphere Plane child has no prepared physical image");
            plane[index] = {declared.staticObjectKey, *texture};
        }
        return NativeSkyAtmosphereResources(std::move(inputs), std::move(plane));
    }

    const SkyAtmosphereSettings& settings() const { return inputs_.settings; }
    const std::vector<std::uint8_t>& transmittance() const { return inputs_.transmittance; }
    const std::vector<std::uint8_t>& ambient() const { return inputs_.ambient; }
    const NativeSkyAtmospherePlaneEntry& plane(std::size_t index) const { return plane_.at(index); }

private:
    NativeSkyAtmosphereResources(SkyAtmosphereInputs inputs,
        std::array<NativeSkyAtmospherePlaneEntry, 4> plane)
        : inputs_(std::move(inputs)), plane_(std::move(plane)) {}
    SkyAtmosphereInputs inputs_;
    std::array<NativeSkyAtmospherePlaneEntry, 4> plane_;
};
} // namespace San14::Render
