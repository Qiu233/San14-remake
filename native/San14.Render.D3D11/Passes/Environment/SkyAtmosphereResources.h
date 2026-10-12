#pragma once
#include "AmbientRenderer.h"
#include "SkyAtmosphereInputs.h"
#include <optional>
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
        const std::filesystem::path& source, const std::vector<AmbientNativeTextureBinding>& existing,
        std::uint32_t selectedObjectKey) {
        auto inputs = Interop::readSkyAtmosphereInputs(source);
        if(inputs.settings.objectKey!=selectedObjectKey)
            throw std::runtime_error("Atmosphere SAT1 owner differs from its actual SAS1 selection");
        auto fallback = ambientTextures(device, source, "textures.tsv", std::nullopt);
        std::vector<NativeSkyAtmospherePlaneEntry> plane(inputs.plane.size());
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
    std::size_t planeCount() const { return plane_.size(); }
    const NativeSkyAtmospherePlaneEntry& plane(std::size_t index) const { return plane_.at(index); }

private:
    NativeSkyAtmosphereResources(SkyAtmosphereInputs inputs,
        std::vector<NativeSkyAtmospherePlaneEntry> plane)
        : inputs_(std::move(inputs)), plane_(std::move(plane)) {}
    SkyAtmosphereInputs inputs_;
    std::vector<NativeSkyAtmospherePlaneEntry> plane_;
};

// Construction completes the actual declaration import. The rendering child
// selects the primary or its declared fallback; only two zero keys leave it null.
class NativeSkyAtmosphereOwner {
public:
    static NativeSkyAtmosphereOwner prepare(ID3D11Device* device,const std::filesystem::path& source,
        const std::vector<AmbientNativeTextureBinding>& existing) {
        const auto selection=Interop::readSkyAtmosphereSelection(source);
        std::optional<NativeSkyAtmosphereResources> resource;
        const auto selectedKey=selection.staticObjectKey?selection.staticObjectKey:selection.fallbackPlaneObjectKey;
        if(selectedKey)
            resource.emplace(NativeSkyAtmosphereResources::prepare(device,source,existing,selectedKey));
        return NativeSkyAtmosphereOwner(selection,std::move(resource));
    }
    const NativeSkyAtmosphereResources* atmosphere() const {return selection_.staticObjectKey?renderingAtmosphere():nullptr;}
    const NativeSkyAtmosphereResources* renderingAtmosphere() const {return resource_?&*resource_:nullptr;}
    const SkyAtmosphereSelection& selection() const {return selection_;}
private:
    NativeSkyAtmosphereOwner(SkyAtmosphereSelection selection,std::optional<NativeSkyAtmosphereResources> resource)
        :selection_(selection),resource_(std::move(resource)) {}
    SkyAtmosphereSelection selection_;
    std::optional<NativeSkyAtmosphereResources> resource_;
};
} // namespace San14::Render
