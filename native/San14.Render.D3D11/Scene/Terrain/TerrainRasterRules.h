#pragma once
#include <d3d11.h>
#include <cstdint>

namespace San14::Render {
inline D3D11_DEPTH_STENCIL_DESC geometryEntryDepth(bool reversedDepth) {
    D3D11_DEPTH_STENCIL_DESC state{};
    state.DepthEnable=TRUE;state.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ALL;
    state.DepthFunc=reversedDepth?D3D11_COMPARISON_GREATER_EQUAL:D3D11_COMPARISON_LESS_EQUAL;
    state.StencilReadMask=state.StencilWriteMask=255;
    state.FrontFace={D3D11_STENCIL_OP_KEEP,D3D11_STENCIL_OP_KEEP,D3D11_STENCIL_OP_KEEP,D3D11_COMPARISON_ALWAYS};
    state.BackFace=state.FrontFace;return state;
}
inline float geometryDepthClear(bool reversedDepth) {return reversedDepth?0.f:1.f;}
inline D3D11_RASTERIZER_DESC geometryEntryRaster() {
    D3D11_RASTERIZER_DESC state{};state.FillMode=D3D11_FILL_SOLID;
    state.CullMode=D3D11_CULL_BACK;state.DepthClipEnable=TRUE;return state;
}
inline D3D11_BLEND_DESC geometryEntryBlend() {
    D3D11_BLEND_DESC state{};state.IndependentBlendEnable=TRUE;
    for(auto& target:state.RenderTarget) {
        target.SrcBlend=target.SrcBlendAlpha=D3D11_BLEND_ONE;
        target.DestBlend=target.DestBlendAlpha=D3D11_BLEND_ZERO;
        target.BlendOp=target.BlendOpAlpha=D3D11_BLEND_OP_ADD;
        target.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
    }
    return state;
}
inline D3D11_RASTERIZER_DESC terrainDrawRaster(D3D11_RASTERIZER_DESC inherited) {
    inherited.CullMode=D3D11_CULL_BACK;return inherited;
}
inline D3D11_DEPTH_STENCIL_DESC terrainDrawDepth(D3D11_DEPTH_STENCIL_DESC inherited) {
    inherited.DepthEnable=TRUE;
    inherited.StencilEnable=TRUE;inherited.StencilReadMask=inherited.StencilWriteMask=255;
    inherited.FrontFace={D3D11_STENCIL_OP_KEEP,D3D11_STENCIL_OP_KEEP,D3D11_STENCIL_OP_REPLACE,D3D11_COMPARISON_ALWAYS};
    inherited.BackFace=inherited.FrontFace;return inherited;
}
inline D3D11_DEPTH_STENCIL_DESC terrainExitDepth(D3D11_DEPTH_STENCIL_DESC drawn) {
    drawn.StencilEnable=FALSE;return drawn;
}
inline bool terrainMaskNeedsSelectedLight(uint32_t geometryHostFlags,uint32_t effectFlags) {
    constexpr uint32_t geometryModeMask=3,ordinaryDeferredMode=1,auxiliaryMaskBypass=0xA00000;
    return (geometryHostFlags&geometryModeMask)==ordinaryDeferredMode&&(effectFlags&auxiliaryMaskBypass)==0;
}
inline uint32_t terrainColorWriteMasks(uint32_t geometryHostFlags,uint32_t effectFlags,
    uint32_t selectedLightMaskScalarBits,bool denormalsAreZero) {
    if(!terrainMaskNeedsSelectedLight(geometryHostFlags,effectFlags))return 0xFFFFF;
    const uint32_t magnitude=selectedLightMaskScalarBits&0x7FFFFFFF;
    const bool unordered=magnitude>0x7F800000;
    const bool zero=magnitude==0||(denormalsAreZero&&magnitude<0x00800000);
    const bool negative=(selectedLightMaskScalarBits&0x80000000)!=0;
    return !unordered&&(zero||negative)?0x0FFFF:0xFFFFF;
}
inline D3D11_BLEND_DESC terrainDrawBlend(D3D11_BLEND_DESC inherited,uint32_t colorMasks) {
    if(!inherited.IndependentBlendEnable)
        for(unsigned target=1;target<8;++target)inherited.RenderTarget[target]=inherited.RenderTarget[0];
    inherited.IndependentBlendEnable=TRUE;
    for(unsigned target=0;target<5;++target)
        inherited.RenderTarget[target].RenderTargetWriteMask=BYTE((colorMasks>>(target*4))&15);
    return inherited;
}
} // namespace San14::Render
