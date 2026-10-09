#pragma once
#include <array>
#include <d3d11.h>
#include <wrl/client.h>

// Attachments and rectangles only. Source Pop leaves all other pipeline state.
struct NativeMainTargetScope {
    std::array<Microsoft::WRL::ComPtr<ID3D11RenderTargetView>,8> colors;
    Microsoft::WRL::ComPtr<ID3D11DepthStencilView> depth;
    std::array<D3D11_VIEWPORT,D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE> viewports{};
    std::array<D3D11_RECT,D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE> scissors{};
    UINT colorCount{},viewportCount{},scissorCount{};

    static NativeMainTargetScope retain(ID3D11DeviceContext* context) {
        NativeMainTargetScope result;ID3D11RenderTargetView* raw[8]{};
        context->OMGetRenderTargets(8,raw,result.depth.GetAddressOf());
        for(UINT i=0;i<8;++i){result.colors[i].Attach(raw[i]);if(raw[i])result.colorCount=i+1;}
        result.viewportCount=UINT(result.viewports.size());context->RSGetViewports(&result.viewportCount,result.viewports.data());
        result.scissorCount=UINT(result.scissors.size());context->RSGetScissorRects(&result.scissorCount,result.scissors.data());
        return result;
    }
    static NativeMainTargetScope main73(ID3D11RenderTargetView* hdr,ID3D11DepthStencilView* d1,
        const D3D11_VIEWPORT& viewport,const D3D11_RECT& scissor) {
        NativeMainTargetScope result;result.colors[0]=hdr;result.depth=d1;result.colorCount=1;
        result.viewports[0]=viewport;result.scissors[0]=scissor;result.viewportCount=result.scissorCount=1;
        return result;
    }
    void restore(ID3D11DeviceContext* context) const {
        ID3D11RenderTargetView* raw[8]{};for(UINT i=0;i<colorCount;++i)raw[i]=colors[i].Get();
        context->OMSetRenderTargets(colorCount,raw,depth.Get());
        context->RSSetViewports(viewportCount,viewports.data());
        context->RSSetScissorRects(scissorCount,scissors.data());
    }
};
