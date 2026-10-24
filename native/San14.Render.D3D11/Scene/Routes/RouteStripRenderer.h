// Original route XYZUV draw consumer. Include after ModelRenderer.h so its
// source96A3E0 input-layout matcher is shared rather than copied.
#pragma once
#include <span>
#include <limits>
#include <cstdint>

struct NativeRouteStripVertex { float x,y,z,u,v; };
static_assert(sizeof(NativeRouteStripVertex)==20);

struct NativeRouteStripRange {
    UINT first{},count{};
    D3D11_PRIMITIVE_TOPOLOGY topology{};
    ComPtr<ID3D11ShaderResourceView> stage0;
};

// One immutable publication owns its uploaded geometry and exact selected
// stage0 images. Camera frames reuse this object without rebuilding vertices.
struct NativeRouteStripGeometry {
    ComPtr<ID3D11Buffer> vertices;
    std::vector<NativeRouteStripRange> ranges;

    static NativeRouteStripGeometry prepare(ID3D11Device* device,
            std::span<const NativeRouteStripVertex> input,
            std::span<const NativeRouteStripRange> ranges) {
        if(input.size()>std::numeric_limits<UINT>::max()/sizeof(NativeRouteStripVertex))
            throw std::runtime_error("Route vertex buffer exceeds native allocation");
        NativeRouteStripGeometry out;
        out.ranges.reserve(ranges.size());
        for(const auto& range:ranges) {
            if(!range.count)continue;
            if(range.first>input.size()||range.count>input.size()-range.first)
                throw std::runtime_error("Route draw span exceeds supplied vertices");
            if(range.topology!=D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP&&
                range.topology!=D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST)
                throw std::runtime_error("Unsupported source route primitive");
            if(!range.stage0)
                throw std::runtime_error("Route selected stage0 image is absent");
            out.ranges.push_back(range);
        }
        if(!out.ranges.empty()) {
            D3D11_BUFFER_DESC desc{};
            desc.ByteWidth=UINT(input.size_bytes());desc.Usage=D3D11_USAGE_IMMUTABLE;
            desc.BindFlags=D3D11_BIND_VERTEX_BUFFER;
            D3D11_SUBRESOURCE_DATA data{};data.pSysMem=input.data();
            check(device->CreateBuffer(&desc,&data,&out.vertices),"Original route vertex publication");
        }
        return out;
    }
};
