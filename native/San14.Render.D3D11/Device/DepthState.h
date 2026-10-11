#pragma once
#include <d3d11.h>
// Source CPU state survives disabled hardware fields. D3D11 GetDesc may
// canonicalize ignored depth/stencil fields, so it is not the source state.
struct NativeSourceDepthState {
    D3D11_DEPTH_STENCIL_DESC description;
    UINT reference;
    void bind(ID3D11DeviceContext* context,ID3D11DepthStencilState* hardware,
        const D3D11_DEPTH_STENCIL_DESC& requested,UINT requestedReference) {
        description=requested;reference=requestedReference;
        context->OMSetDepthStencilState(hardware,requestedReference);
    }
};

inline bool sameStencilFace(const D3D11_DEPTH_STENCILOP_DESC& a,const D3D11_DEPTH_STENCILOP_DESC& b) {
    return a.StencilFailOp==b.StencilFailOp&&a.StencilDepthFailOp==b.StencilDepthFailOp&&a.StencilPassOp==b.StencilPassOp&&a.StencilFunc==b.StencilFunc;
}
inline bool sameDepthStencilDescription(const D3D11_DEPTH_STENCIL_DESC& a,const D3D11_DEPTH_STENCIL_DESC& b) {
    return a.DepthEnable==b.DepthEnable&&a.DepthWriteMask==b.DepthWriteMask&&a.DepthFunc==b.DepthFunc&&a.StencilEnable==b.StencilEnable&&
        a.StencilReadMask==b.StencilReadMask&&a.StencilWriteMask==b.StencilWriteMask&&
        sameStencilFace(a.FrontFace,b.FrontFace)&&sameStencilFace(a.BackFace,b.BackFace);
}
