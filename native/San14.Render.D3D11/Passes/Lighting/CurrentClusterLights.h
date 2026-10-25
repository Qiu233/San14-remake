#pragma once
#include <d3d11.h>
#include <array>
#include <cstdint>
#include <optional>

// Borrowed from the successful current node71 owner. An allocation alone
// cannot publish this packet; the owner retains the six typed SRVs.
struct NativeSourceClusterLights {
    std::array<ID3D11ShaderResourceView*,6> resources{};
    std::uint32_t pointCount{},spotCount{};
    bool computeSucceeded{};
    std::array<float,4> depthParameters{};
    std::array<std::uint32_t,2> patchCounts{};
    std::optional<float> scaling;
    std::optional<std::array<float,2>> fadeDistance;
};
