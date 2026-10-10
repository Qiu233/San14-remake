#pragma once
#include "TerrainRasterRules.h"
#include "../../Device/DepthState.h"
#include <cstring>

namespace San14::Render {
// Retained state objects are derived from the actual predecessor bindings, not
// from media scalar files or a fabricated last-model submission receipt.
class TerrainRasterState {
public:
    explicit TerrainRasterState(ID3D11Device* device):device_(device){}
    void begin(ID3D11DeviceContext* context,NativeSourceDepthState& logical,
        UINT reference,uint32_t colorMasks) {
        context->RSGetState(&savedRaster_);
        auto raster=geometryEntryRaster();if(savedRaster_)savedRaster_->GetDesc(&raster);
        auto requestedRaster=terrainDrawRaster(raster);
        if(!raster_||std::memcmp(&rasterDescription_,&requestedRaster,sizeof(requestedRaster))!=0) {
            check(device_->CreateRasterizerState(&requestedRaster,&raster_),"Terrain current inherited raster");
            rasterDescription_=requestedRaster;
        }
        ComPtr<ID3D11DepthStencilState> predecessor;UINT previousReference;
        context->OMGetDepthStencilState(&predecessor,&previousReference);
        auto depth=geometryEntryDepth(false);depth.DepthFunc=D3D11_COMPARISON_LESS;
        if(predecessor)predecessor->GetDesc(&depth);
        auto requestedDepth=terrainDrawDepth(depth);
        if(!depth_||std::memcmp(&depthDescription_,&requestedDepth,sizeof(requestedDepth))!=0) {
            check(device_->CreateDepthStencilState(&requestedDepth,&depth_),"Terrain current inherited depth");
            const auto exit=terrainExitDepth(requestedDepth);
            check(device_->CreateDepthStencilState(&exit,&exitDepth_),"Terrain stencil exit");
            depthDescription_=requestedDepth;
        }
        ComPtr<ID3D11BlendState> predecessorBlend;FLOAT factors[4];UINT sampleMask;
        context->OMGetBlendState(&predecessorBlend,factors,&sampleMask);
        auto blend=geometryEntryBlend();blend.IndependentBlendEnable=FALSE;
        if(predecessorBlend)predecessorBlend->GetDesc(&blend);
        auto requestedBlend=terrainDrawBlend(blend,colorMasks);
        if(!blend_||std::memcmp(&blendDescription_,&requestedBlend,sizeof(requestedBlend))!=0) {
            check(device_->CreateBlendState(&requestedBlend,&blend_),"Terrain current five-target masks");
            blendDescription_=requestedBlend;
        }
        context->RSSetState(raster_.Get());logical.bind(context,depth_.Get(),requestedDepth,reference);
        context->OMSetBlendState(blend_.Get(),factors,sampleMask);
    }
    void finish(ID3D11DeviceContext* context,NativeSourceDepthState& logical) {
        const auto exit=terrainExitDepth(depthDescription_);
        logical.bind(context,exitDepth_.Get(),exit,logical.reference);
        context->RSSetState(savedRaster_.Get());savedRaster_.Reset();
    }
private:
    ComPtr<ID3D11Device> device_;
    ComPtr<ID3D11RasterizerState> raster_,savedRaster_;
    ComPtr<ID3D11DepthStencilState> depth_,exitDepth_;
    ComPtr<ID3D11BlendState> blend_;
    D3D11_RASTERIZER_DESC rasterDescription_{};
    D3D11_DEPTH_STENCIL_DESC depthDescription_{};
    D3D11_BLEND_DESC blendDescription_{};
};
} // namespace San14::Render
