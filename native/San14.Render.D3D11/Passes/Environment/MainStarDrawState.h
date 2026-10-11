#pragma once
#include "../../Scene/CommonRenderPolicy.h"
#include <d3d11.h>

// The active Main Star branch sets both Star draws and the intervening Sky
// blend. Only the actually bound color slots receive its source setters.
inline D3D11_BLEND_DESC reachedActiveMainSkyBlend(D3D11_BLEND_DESC entry,
    San14::Render::SkyAlphaBlendMode alphaMode,bool star,UINT boundTargetCount) {
    const auto priorFirst=entry.RenderTarget[0];
    auto& first=entry.RenderTarget[0];
    first.BlendEnable=TRUE;
    first.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
    first.SrcBlend=D3D11_BLEND_ONE;
    first.DestBlend=star?D3D11_BLEND_ONE:D3D11_BLEND_SRC_ALPHA;
    first.BlendOp=D3D11_BLEND_OP_ADD;
    first.SrcBlendAlpha=D3D11_BLEND_ONE;
    first.DestBlendAlpha=alphaMode==San14::Render::SkyAlphaBlendMode::Replace?
        D3D11_BLEND_ZERO:D3D11_BLEND_ONE;
    first.BlendOpAlpha=alphaMode==San14::Render::SkyAlphaBlendMode::Maximum?
        D3D11_BLEND_OP_MAX:D3D11_BLEND_OP_ADD;
    const bool factorsChanged=priorFirst.SrcBlend!=first.SrcBlend||priorFirst.DestBlend!=first.DestBlend||
        priorFirst.BlendOp!=first.BlendOp||priorFirst.SrcBlendAlpha!=first.SrcBlendAlpha||
        priorFirst.DestBlendAlpha!=first.DestBlendAlpha||priorFirst.BlendOpAlpha!=first.BlendOpAlpha;
    for(UINT slot=1;slot<boundTargetCount;++slot) {
        auto& target=entry.RenderTarget[slot];
        target.BlendEnable=FALSE;target.RenderTargetWriteMask=0;
        // The source broadcasts new factors only when slot zero changes. If
        // it already matches, heterogeneous retained factors stay untouched.
        if(factorsChanged) {
            target.SrcBlend=first.SrcBlend;target.DestBlend=first.DestBlend;target.BlendOp=first.BlendOp;
            target.SrcBlendAlpha=first.SrcBlendAlpha;target.DestBlendAlpha=first.DestBlendAlpha;
            target.BlendOpAlpha=first.BlendOpAlpha;
        }
    }
    // Independent blend is required to express the distinct slot setters on
    // D3D11; otherwise hardware repeats slot zero across every attachment.
    if(boundTargetCount>1)entry.IndependentBlendEnable=TRUE;
    return entry;
}
