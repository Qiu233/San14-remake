#pragma once
#include "MainStarDrawState.h"
#include "SkyDrawState.h"

struct NativeMainSkyBlendCache {
    ComPtr<ID3D11BlendState> state,entry;
    San14::Render::SkyAlphaBlendMode alphaMode{};
    UINT targetCount{};
    bool suppressed{},star{};
};
static void bindNativeMainSkyBlend(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeMainSkyBlendCache& cache,bool suppressed,bool star,
    San14::Render::SkyAlphaBlendMode alphaMode,UINT targetCount) {
    ComPtr<ID3D11BlendState> entry;float factor[4];UINT sampleMask;
    context->OMGetBlendState(&entry,factor,&sampleMask);
    if(!cache.state||(entry.Get()!=cache.entry.Get()&&entry.Get()!=cache.state.Get())||
        cache.suppressed!=suppressed||cache.star!=star||cache.alphaMode!=alphaMode||cache.targetCount!=targetCount) {
        D3D11_BLEND_DESC description{};
        for(auto& target:description.RenderTarget) {
            target.SrcBlend=target.SrcBlendAlpha=D3D11_BLEND_ONE;
            target.DestBlend=target.DestBlendAlpha=D3D11_BLEND_ZERO;
            target.BlendOp=target.BlendOpAlpha=D3D11_BLEND_OP_ADD;
            target.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
        }
        if(entry)entry->GetDesc(&description);
        description=suppressed?reachedMainSkyBlend(description):
            reachedActiveMainSkyBlend(description,alphaMode,star,targetCount);
        ComPtr<ID3D11BlendState> candidate;
        check(device->CreateBlendState(&description,&candidate),"Main Sky/Star reached blend");
        cache.state=std::move(candidate);cache.entry=entry;
        cache.suppressed=suppressed;cache.star=star;cache.alphaMode=alphaMode;cache.targetCount=targetCount;
    }
    context->OMSetBlendState(cache.state.Get(),factor,sampleMask);
}
