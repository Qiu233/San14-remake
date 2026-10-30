#pragma once
#include "SpecialEffectInputs.h"
#include <functional>

// D33's admission and retained frontend state are independent of tail count.
struct NativeSpecialEffects {
    UINT hostFlags{},alphaTest{};
};
static NativeSpecialEffects createNativeSpecialEffects(UINT actualHostFlags) {
    NativeSpecialEffects out{};out.hostFlags=actualHostFlags;return out;
}
static void applyNativeSpecialEffects(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeSourceDepthState& logical,NativeSpecialEffects& source,San14::Render::MainRenderPhaseState& phase,
    const SpecialEffectInputs& inputs,bool bankZeroRendererPresent,UINT node,const std::function<void()>& draw) {
    if(node!=141&&node!=147)throw std::runtime_error("Special Effect source node");
    phase.publishSpecialTail(); // Before Common, light and renderer tests.
    if(!inputs.admitted()||!bankZeroRendererPresent)return;
    const auto entry=logical;const UINT savedAlpha=source.alphaTest;
    source.alphaTest=1;auto enter=entry.description;enter.DepthEnable=TRUE;
    // Source native comparison codes are zero-based, unlike D3D11.
    enter.DepthFunc=(source.hostFlags&0x20000000u)?D3D11_COMPARISON_GREATER_EQUAL:D3D11_COMPARISON_LESS_EQUAL;
    enter.StencilEnable=TRUE;enter.StencilReadMask=0xFF;enter.StencilWriteMask=0xFF;
    D3D11_DEPTH_STENCILOP_DESC operations{D3D11_STENCIL_OP_KEEP,D3D11_STENCIL_OP_KEEP,
        D3D11_STENCIL_OP_REPLACE,D3D11_COMPARISON_ALWAYS};
    enter.FrontFace=enter.BackFace=operations;
    const UINT reference=*inputs.selectedLightTag|(node==147?0x60u:0u);
    ComPtr<ID3D11DepthStencilState> state;check(device->CreateDepthStencilState(&enter,&state),"D33 current entry state");
    logical.bind(context,state.Get(),enter,reference);
    // The row consumer has its own AA0AA0 device scope and restores entry
    // depth-write, culling and RT0 blend before the D33 frontend exit below.
    draw();
    auto exit=enter;exit.FrontFace.StencilFunc=entry.description.FrontFace.StencilFunc;
    exit.BackFace.StencilFunc=entry.description.BackFace.StencilFunc;
    exit.StencilReadMask=entry.description.StencilReadMask;exit.StencilWriteMask=entry.description.StencilWriteMask;
    exit.StencilEnable=FALSE;source.alphaTest=savedAlpha;
    exit.DepthEnable=entry.description.DepthEnable;exit.DepthFunc=entry.description.DepthFunc;
    ComPtr<ID3D11DepthStencilState> restored;check(device->CreateDepthStencilState(&exit,&restored),"D33 current source exit");
    logical.bind(context,restored.Get(),exit,entry.reference);
}
