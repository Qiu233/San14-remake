#pragma once
#include "OcclusionProgram.h"
#include "OcclusionTargets.h"
#include "OcclusionStock.h"
#include <array>
#include <map>

namespace San14::Render {
enum class OcclusionTextureInput : unsigned {
    Depth=128,Normals,Noise,Occlusion,Stencil
};
struct OcclusionTextureBinding {unsigned slot{};OcclusionTextureInput input{};};
struct OcclusionPassSource {
    unsigned vertex{},pixel{};
    std::vector<OcclusionTextureBinding> textures;
};
struct OcclusionVariantSource {
    std::array<unsigned,16> words{};
    std::vector<OcclusionPassSource> passes;
};
// The static bank retains only unique actual programs and finite resource
// recipes. Current camera, light admission and group fade are never persisted
// as a pass manifest or recovered from a cached frame.
struct OcclusionResourceBank {
    std::map<unsigned,OcclusionProgramSource> programs;
    std::vector<OcclusionVariantSource> variants;
    std::map<std::array<unsigned,16>,size_t> variantIndices;
    std::uint64_t blurCapabilities{};
    std::vector<D3D11_SAMPLER_DESC> samplers;
    std::array<std::vector<OcclusionFormatCandidate>,5> candidates;
    OcclusionNoiseSource noise;
    OcclusionStockSource stock;
    D3D11_RENDER_TARGET_BLEND_DESC compositionBlend{};

    const OcclusionProgramSource& program(unsigned id,unsigned stage) const {
        const auto found=programs.find(id);
        if(found==programs.end()||found->second.stage!=stage)
            throw std::runtime_error("Selected occlusion graphics stage has no actual program");
        return found->second;
    }

    const OcclusionVariantSource& match(const std::array<unsigned,16>& requested) const {
        const auto found=variantIndices.find(requested);
        if(found==variantIndices.end())throw std::runtime_error("Current occlusion variant has no actual resource match");
        return variants.at(found->second);
    }
};
} // namespace San14::Render
