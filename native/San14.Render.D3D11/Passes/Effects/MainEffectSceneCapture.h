#pragma once
#include "CurrentEffectRenderer.h"
#include "../../Device/RenderTargetScope.h"

namespace current_effect {
// The normal Main HDR surface has a full retained rectangle. Capture belongs
// to one Effect2 begin, while this owned image survives until resize/retirement.
class MainSceneCapture {
    ComPtr<ID3D11Texture2D> image;
    ComPtr<ID3D11ShaderResourceView> view;
    UINT width{},height{};

    void capture(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical) {
        const auto targets=NativeMainTargetScope::retain(context);
        if(!targets.colors[0]||targets.viewportCount!=1)
            throw std::runtime_error("Main Effect capture requires its actual full RT0 surface");
        ComPtr<ID3D11Resource> resource;targets.colors[0]->GetResource(&resource);
        ComPtr<ID3D11Texture2D> source;check(resource.As(&source),"Main Effect capture RT0 texture");
        D3D11_TEXTURE2D_DESC desc{};source->GetDesc(&desc);
        D3D11_RENDER_TARGET_VIEW_DESC attachment{};targets.colors[0]->GetDesc(&attachment);
        const auto& port=targets.viewports[0];
        if(desc.Format!=DXGI_FORMAT_R11G11B10_FLOAT||attachment.Format!=DXGI_FORMAT_R11G11B10_FLOAT||
            attachment.ViewDimension!=D3D11_RTV_DIMENSION_TEXTURE2D||attachment.Texture2D.MipSlice!=0||
            desc.MipLevels!=1||desc.ArraySize!=1||desc.SampleDesc.Count!=1||desc.SampleDesc.Quality!=0||
            !desc.Width||!desc.Height||port.TopLeftX!=0||port.TopLeftY!=0||
            port.Width!=FLOAT(desc.Width)||port.Height!=FLOAT(desc.Height))
            throw std::runtime_error("Main Effect capture requires the current single-sample full HDR retained rectangle");
        if(!image||width!=desc.Width||height!=desc.Height) {
            D3D11_TEXTURE2D_DESC scratch{};scratch.Width=desc.Width;scratch.Height=desc.Height;
            scratch.MipLevels=1;scratch.ArraySize=1;scratch.Format=DXGI_FORMAT_R11G11B10_FLOAT;
            scratch.SampleDesc.Count=1;scratch.Usage=D3D11_USAGE_DEFAULT;
            scratch.BindFlags=D3D11_BIND_SHADER_RESOURCE|D3D11_BIND_RENDER_TARGET;
            ComPtr<ID3D11Texture2D> nextImage;ComPtr<ID3D11ShaderResourceView> nextView;
            check(device->CreateTexture2D(&scratch,nullptr,&nextImage),"Main Effect actual HDR scene scratch");
            check(device->CreateShaderResourceView(nextImage.Get(),nullptr,&nextView),"Main Effect actual HDR scene view");
            image=std::move(nextImage);view=std::move(nextView);width=desc.Width;height=desc.Height;
        }
        DeviceScope state(context,logical);
        struct RestoreTargets {ID3D11DeviceContext* context;const NativeMainTargetScope& targets;
            ~RestoreTargets(){targets.restore(context);}} restore{context,targets};
        std::array<ID3D11ShaderResourceView*,128> empty{};
        context->VSSetShaderResources(0,UINT(empty.size()),empty.data());
        context->PSSetShaderResources(0,UINT(empty.size()),empty.data());
        context->OMSetRenderTargets(0,nullptr,nullptr);
        // The actual same-format/same-sample full-rectangle fast branch.
        const D3D11_BOX sourceBox{0,0,0,width,height,1};
        context->CopySubresourceRegion(image.Get(),0,0,0,0,source.Get(),0,&sourceBox);
    }
public:
    GlobalViews begin(ID3D11Device* device,ID3D11DeviceContext* context,
        NativeSourceDepthState& logical,GlobalViews globals) {
        globals.scene=nullptr;
        globals.captureScene=[this,device,context,&logical,valid=false]() mutable {
            if(!valid){capture(device,context,logical);valid=true;}
            return view.Get();
        };
        return globals;
    }
};
}
