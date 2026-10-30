#pragma once
#include "CurrentEffectRenderer.h"
#include "UiColorBlit.h"

namespace current_effect {
// Independent View1C10 offscreen command3/4. All immutable media comes from
// normal CER1 resources. This owner has no map, scene, shadow or HDR producer.
class UiRenderer {
    Renderer effects;
    UiColorBlit blit;
    UINT width,height;
    ComPtr<ID3D11Texture2D> color,registeredColor,depth,depthMirror,sceneCapture,output;
    ComPtr<ID3D11RenderTargetView> target,registeredTarget,captureTarget,outputTarget;
    ComPtr<ID3D11DepthStencilView> depthTarget;
    ComPtr<ID3D11ShaderResourceView> sampledDepth,capturedScene,colorView,registeredView;
    std::array<ComPtr<ID3D11DepthStencilState>,2> initialDepth;
    NativeSourceDepthState logical{};
    ComPtr<ID3D11RasterizerState> raster;
    ComPtr<ID3D11BlendState> initialBlend;
public:
    UiRenderer(ID3D11Device* device,UINT width,UINT height,UINT selector)
        :effects(DrawScope::uiOffscreen(selector)),blit(device),width(width),height(height) {
        if(!width||!height||width>D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION||height>D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION)
            throw std::invalid_argument("UI Effect output dimensions");
        D3D11_TEXTURE2D_DESC image{};image.Width=width;image.Height=height;image.ArraySize=1;image.MipLevels=1;
        image.SampleDesc.Count=1;image.Format=DXGI_FORMAT_R16G16B16A16_TYPELESS;
        image.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
        // E85F70 swaps the two native3 Forward intermediates when necessary.
        // Working and capture remain distinct RGBA16F resources. The actual
        // registered native0 Color is written only by the E88DA0 resolve.
        check(device->CreateTexture2D(&image,nullptr,&color),"UI Effect actual Forward working target");
        D3D11_RENDER_TARGET_VIEW_DESC rtv{};rtv.Format=DXGI_FORMAT_R16G16B16A16_FLOAT;rtv.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
        check(device->CreateRenderTargetView(color.Get(),&rtv,&target),"UI Effect actual Forward working attachment");
        D3D11_SHADER_RESOURCE_VIEW_DESC colorSrv{};colorSrv.Format=DXGI_FORMAT_R16G16B16A16_FLOAT;
        colorSrv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;colorSrv.Texture2D.MipLevels=1;
        check(device->CreateShaderResourceView(color.Get(),&colorSrv,&colorView),"UI Effect actual color view");
        // A4F280 consumes the other native3 image after the source swap.
        check(device->CreateTexture2D(&image,nullptr,&sceneCapture),"UI Effect actual RTT scratch");
        rtv.Format=DXGI_FORMAT_R16G16B16A16_FLOAT;
        check(device->CreateRenderTargetView(sceneCapture.Get(),&rtv,&captureTarget),"UI Effect actual RTT attachment");
        colorSrv.Format=DXGI_FORMAT_R16G16B16A16_FLOAT;
        check(device->CreateShaderResourceView(sceneCapture.Get(),&colorSrv,&capturedScene),"UI Effect actual RTT view");
        image.Format=DXGI_FORMAT_R8G8B8A8_TYPELESS;
        check(device->CreateTexture2D(&image,nullptr,&registeredColor),"UI Effect actual registered color target");
        rtv.Format=DXGI_FORMAT_R8G8B8A8_UNORM;colorSrv.Format=DXGI_FORMAT_R8G8B8A8_UNORM;
        check(device->CreateRenderTargetView(registeredColor.Get(),&rtv,&registeredTarget),"UI Effect actual registered color attachment");
        check(device->CreateShaderResourceView(registeredColor.Get(),&colorSrv,&registeredView),"UI Effect actual registered color view");
        // The Canvas bridge has BGRA bytes, while the normal UI color0 owner
        // is RGBA. A backend blit performs that actual view-format conversion.
        image.Format=DXGI_FORMAT_B8G8R8A8_UNORM;image.BindFlags=D3D11_BIND_RENDER_TARGET;
        check(device->CreateTexture2D(&image,nullptr,&output),"UI Effect Canvas output");
        check(device->CreateRenderTargetView(output.Get(),nullptr,&outputTarget),"UI Effect Canvas output view");
        image.Format=DXGI_FORMAT_R24G8_TYPELESS;image.BindFlags=D3D11_BIND_DEPTH_STENCIL;
        check(device->CreateTexture2D(&image,nullptr,&depth),"UI Effect actual depth target");
        D3D11_DEPTH_STENCIL_VIEW_DESC dsv{};dsv.Format=DXGI_FORMAT_D24_UNORM_S8_UINT;dsv.ViewDimension=D3D11_DSV_DIMENSION_TEXTURE2D;
        check(device->CreateDepthStencilView(depth.Get(),&dsv,&depthTarget),"UI Effect actual depth attachment");
        image.BindFlags=D3D11_BIND_SHADER_RESOURCE;
        check(device->CreateTexture2D(&image,nullptr,&depthMirror),"UI Effect actual sampled depth mirror");
        D3D11_SHADER_RESOURCE_VIEW_DESC srv{};srv.Format=DXGI_FORMAT_R24_UNORM_X8_TYPELESS;
        srv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;srv.Texture2D.MipLevels=1;
        check(device->CreateShaderResourceView(depthMirror.Get(),&srv,&sampledDepth),"UI Effect sampled depth view");
        // D53440 supplies its pre-FX geometry state to D497F0/D43190/D71500:
        // source compare6 under reverse depth, otherwise3. AA16A0 then disables
        // sprite depth writes. This is the actual scope, not constructor LESS.
        D3D11_DEPTH_STENCIL_DESC ds{};ds.DepthEnable=TRUE;ds.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;
        ds.DepthFunc=D3D11_COMPARISON_LESS_EQUAL;ds.StencilEnable=FALSE;ds.StencilReadMask=ds.StencilWriteMask=255;
        ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=ds.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;
        ds.FrontFace.StencilFunc=D3D11_COMPARISON_ALWAYS;ds.BackFace=ds.FrontFace;
        check(device->CreateDepthStencilState(&ds,&initialDepth[0]),"UI Effect source ordinary depth state");
        ds.DepthFunc=D3D11_COMPARISON_GREATER_EQUAL;
        check(device->CreateDepthStencilState(&ds,&initialDepth[1]),"UI Effect source reverse depth state");logical={ds,0};
        D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.CullMode=D3D11_CULL_NONE;rs.DepthClipEnable=TRUE;
        check(device->CreateRasterizerState(&rs,&raster),"UI Effect source ordinary raster state");
        D3D11_BLEND_DESC blend{};
        for(auto& rt:blend.RenderTarget) {rt.SrcBlend=rt.SrcBlendAlpha=D3D11_BLEND_ONE;rt.DestBlend=rt.DestBlendAlpha=D3D11_BLEND_ZERO;
            rt.BlendOp=rt.BlendOpAlpha=D3D11_BLEND_OP_ADD;rt.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;}
        check(device->CreateBlendState(&blend,&initialBlend),"UI Effect owned target component masks");
    }
    ID3D11Texture2D* texture() const {return output.Get();}
    ID3D11ShaderResourceView* captureCurrent(ID3D11DeviceContext* context) {
        DeviceScope state(context,logical);
        // A prior RTT row may still sample the capture destination. The
        // following row binds its complete selected resource set again.
        std::array<ID3D11ShaderResourceView*,128> empty{};
        context->VSSetShaderResources(0,UINT(empty.size()),empty.data());
        context->PSSetShaderResources(0,UINT(empty.size()),empty.data());
        context->OMSetRenderTargets(0,nullptr,nullptr);
        // 959FD0 blits the actual preceding FX rows into the other Forward
        // RGBA16F image. Its sampler is provided by the normal resource owner.
        blit.draw(context,colorView.Get(),captureTarget.Get(),width,height);
        auto rt=target.Get();context->OMSetRenderTargets(1,&rt,depthTarget.Get());
        return capturedScene.Get();
    }
    UINT drawCount() const {return effects.drawCount();}
    void clear(ID3D11DeviceContext* context,bool opaquePane=true) {
        // E85F70 uses the actual E11A20 packed-alphaFF predicate. Partial
        // panes start at A0; they retain the same primary inner FX recipes.
        const FLOAT initial[4]{0,0,0,opaquePane?1.f:0.f};context->ClearRenderTargetView(target.Get(),initial);
    }
    void present(ID3D11DeviceContext* context) {
        // This fresh8361F814 has a null ToneScale reference. The original
        // E88DA0 shader therefore applies only abs RGB gamma and passes T.
        // D7A3A0 uses gamma2.2: native0's target view is UNORM and the normal
        // Host virtual80 returns false. No Main exposure or LUT is borrowed.
        blit.resolve(context,colorView.Get(),registeredTarget.Get(),width,height,2.2f);
        blit.draw(context,registeredView.Get(),outputTarget.Get(),width,height);
    }
    void draw(ID3D11Device* device,ID3D11DeviceContext* context,const San14CurrentEffectInputV1& input,bool reverseDepth,UINT paneAlpha) {
        effects.accept(device,context,input);
        context->OMSetRenderTargets(0,nullptr,nullptr);
        clear(context,paneAlpha==255);
        context->ClearDepthStencilView(depthTarget.Get(),D3D11_CLEAR_DEPTH|D3D11_CLEAR_STENCIL,reverseDepth?0.f:1.f,0);
        // E85F70 starts this offscreen target before ordinary FX. The mirror
        // is the same attachment's actual cleared contents, never a dummy map.
        context->CopyResource(depthMirror.Get(),depth.Get());
        auto rt=target.Get();context->OMSetRenderTargets(1,&rt,depthTarget.Get());
        D3D11_VIEWPORT port{0,0,FLOAT(width),FLOAT(height),0,1};context->RSSetViewports(1,&port);
        D3D11_RECT rect{0,0,LONG(width),LONG(height)};context->RSSetScissorRects(1,&rect);
        context->RSSetState(raster.Get());context->OMSetBlendState(initialBlend.Get(),nullptr,0xFFFFFFFFu);
        auto sourceDepth=logical.description;
        sourceDepth.DepthFunc=reverseDepth?D3D11_COMPARISON_GREATER_EQUAL:D3D11_COMPARISON_LESS_EQUAL;
        logical.bind(context,initialDepth[reverseDepth?1:0].Get(),sourceDepth,0);
        GlobalViews globals;globals.depth=sampledDepth.Get();
        // D02010 Common308 is null for this fresh UI owner. D32BE0 therefore
        // never selects token41. The original PS explicitly handles null t25.
        globals.exposureAbsent=true;
        // The normal named lists start empty and receive ready FX only.
        // Portrait/title Canvas draws happen later. Thus the actual source
        // of pass29 is this working image, including all preceding FX rows.
        // Normal mode1778=1 resets capture validity at Effect2 begin. Only
        // the first pass29 callback captures; later rows reuse that image.
        bool captureValid=false;
        globals.captureScene=[this,context,&captureValid]{
            if(!captureValid){captureCurrent(context);captureValid=true;}
            return capturedScene.Get();
        };
        effects.drawUi(device,context,logical,globals);
        context->OMSetRenderTargets(0,nullptr,nullptr);
        present(context);context->ClearState();
    }
};
}
