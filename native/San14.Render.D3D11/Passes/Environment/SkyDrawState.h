#pragma once
#include <d3d11.h>

// Main Sky's setters produce a reached state, not a temporary scope. Cloud
// inherits this state and restores only its own temporary per-pass changes.
inline D3D11_DEPTH_STENCIL_DESC reachedMainSkyDepth(D3D11_DEPTH_STENCIL_DESC entry) {
    entry.DepthEnable=FALSE;
    entry.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;
    entry.StencilEnable=TRUE;
    entry.StencilReadMask=255;
    entry.FrontFace.StencilFunc=D3D11_COMPARISON_EQUAL;
    entry.FrontFace.StencilFailOp=entry.FrontFace.StencilDepthFailOp=entry.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;
    entry.BackFace=entry.FrontFace;
    return entry;
}
inline D3D11_RASTERIZER_DESC reachedMainSkyRaster(D3D11_RASTERIZER_DESC entry) {
    entry.CullMode=D3D11_CULL_NONE;
    return entry;
}
inline D3D11_BLEND_DESC reachedMainSkyBlend(D3D11_BLEND_DESC entry) {
    entry.RenderTarget[0].BlendEnable=FALSE;
    entry.RenderTarget[0].RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
    return entry;
}
