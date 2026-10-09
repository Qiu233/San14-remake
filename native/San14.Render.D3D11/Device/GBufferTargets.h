#pragma once
// Session-owned G-buffer attachments and immutable entry states. A frame may
// borrow views and descriptions; only the target owner allocates or clears them.
#include "RenderTargetScope.h"

namespace San14::Render {
struct GBufferConfiguration {
    UINT width, height;
    std::array<DXGI_FORMAT, 6> resourceFormats;
    std::array<DXGI_FORMAT, 5> formats;
    DXGI_FORMAT depthFormat;
    std::array<std::string, 6> targetNames;
    std::array<UINT, 6> surfaces;
    UINT cull, front, depthCompare, colorMasks, stencilEnable, stencilRef;
    float depthClear;
};

class GBufferTargets {
  public:
    GBufferTargets(ID3D11Device *device, ID3D11DeviceContext *context,
                   const GBufferConfiguration &configuration)
        : depthClear_(configuration.depthClear) {
        D3D11_TEXTURE2D_DESC td{};
        td.Width = configuration.width;
        td.Height = configuration.height;
        td.ArraySize = td.MipLevels = 1;
        td.SampleDesc.Count = 1;
        td.Usage = D3D11_USAGE_DEFAULT;
        td.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
        float clear[4]{};
        // This fixed scene's RS object 521C7DED has exactly five layers in source.
        // Node53 writes its surfaces0/2, not two unrelated textures or a five-MRT SRV.
        // Source85C125A6 (RR) is a distinct three-layer array; its surface0 is
        // the fourth MRT. The normal map is source7E94C315 (the second MRT).
        for (int i = 0; i < 5; ++i) {
            td.Format = configuration.resourceFormats[i];
            td.ArraySize = i == 2 ? 5 : i == 3 ? 3 : 1;
            if (i == 4 && !configuration.targetNames[4].empty() &&
                configuration.targetNames[4] == configuration.targetNames[2])
                textures_[i] = textures_[2];
            else
                check(device->CreateTexture2D(&td, nullptr, &textures_[i]), "G-buffer target");
            D3D11_RENDER_TARGET_VIEW_DESC view{};
            view.Format = configuration.formats[i];
            if (i == 2 || i == 3 || i == 4) {
                view.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
                view.Texture2DArray.FirstArraySlice = configuration.surfaces[i];
                view.Texture2DArray.ArraySize = 1;
            } else
                view.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2D;
            check(device->CreateRenderTargetView(textures_[i].Get(), &view, &views_[i]), "G-buffer RTV");
            colors_[i] = views_[i].Get();
            context->ClearRenderTargetView(colors_[i], clear);
        }
        td.ArraySize = 1;
        td.Format = configuration.resourceFormats[5];
        td.BindFlags = D3D11_BIND_DEPTH_STENCIL | D3D11_BIND_SHADER_RESOURCE;
        check(device->CreateTexture2D(&td, nullptr, &depth_), "source depth target");
        D3D11_DEPTH_STENCIL_VIEW_DESC depthView{};
        depthView.Format = configuration.depthFormat;
        depthView.ViewDimension = D3D11_DSV_DIMENSION_TEXTURE2D;
        check(device->CreateDepthStencilView(depth_.Get(), &depthView, &depthView_), "depth DSV");
        context->ClearDepthStencilView(depthView_.Get(), D3D11_CLEAR_DEPTH | D3D11_CLEAR_STENCIL,
                                       configuration.depthClear, 0);
        depthDescription_.DepthEnable = TRUE;
        depthDescription_.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ALL;
        depthDescription_.DepthFunc = D3D11_COMPARISON_FUNC(configuration.depthCompare);
        depthDescription_.StencilEnable = configuration.stencilEnable;
        depthDescription_.StencilReadMask = depthDescription_.StencilWriteMask = 255;
        depthDescription_.FrontFace.StencilFunc = depthDescription_.BackFace.StencilFunc =
            D3D11_COMPARISON_ALWAYS;
        depthDescription_.FrontFace.StencilPassOp = depthDescription_.BackFace.StencilPassOp =
            D3D11_STENCIL_OP_REPLACE;
        depthDescription_.FrontFace.StencilFailOp = depthDescription_.FrontFace.StencilDepthFailOp =
            depthDescription_.BackFace.StencilFailOp = depthDescription_.BackFace.StencilDepthFailOp =
                D3D11_STENCIL_OP_KEEP;
        check(device->CreateDepthStencilState(&depthDescription_, &depthState_), "depth/stencil state");
        context->OMSetDepthStencilState(depthState_.Get(), configuration.stencilRef);
        rasterDescription_.FillMode = D3D11_FILL_SOLID;
        rasterDescription_.CullMode = D3D11_CULL_MODE(configuration.cull);
        rasterDescription_.FrontCounterClockwise = configuration.front;
        rasterDescription_.DepthClipEnable = TRUE;
        check(device->CreateRasterizerState(&rasterDescription_, &raster_), "rasterizer");
        context->RSSetState(raster_.Get());
        blendDescription_.IndependentBlendEnable = TRUE;
        for (int i = 0; i < 5; ++i)
            blendDescription_.RenderTarget[i].RenderTargetWriteMask =
                uint8_t((configuration.colorMasks >> (i * 4)) & 15);
        check(device->CreateBlendState(&blendDescription_, &blendState_), "color masks");
        context->OMSetBlendState(blendState_.Get(), nullptr, ~0u);
        viewport_ = {0, 0, float(configuration.width), float(configuration.height), 0, 1};
        scissor_ = {0, 0, LONG(configuration.width), LONG(configuration.height)};
    }
    GBufferTargets(const GBufferTargets &) = delete;
    GBufferTargets &operator=(const GBufferTargets &) = delete;

    // This extra RR1 view is created after the caller's final-output surface,
    // preserving session preparation order. It shares the RR texture owner.
    void prepareExpandedTargets(ID3D11Device *device, DXGI_FORMAT rrFormat) {
        {
            expandedTargets_.colorCount = 6;
            expandedTargets_.depth = depthView_;
            for (UINT i = 0; i < 5; ++i)
                expandedTargets_.colors[i] = views_[i];
            D3D11_RENDER_TARGET_VIEW_DESC rr1{};
            rr1.Format = rrFormat;
            rr1.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
            rr1.Texture2DArray.FirstArraySlice = 1;
            rr1.Texture2DArray.ArraySize = 1;
            check(device->CreateRenderTargetView(textures_[3].Get(), &rr1, &expandedTargets_.colors[5]),
                  "143/156 original RR1 surface view");
            expandedTargets_.viewportCount = expandedTargets_.scissorCount = 1;
            expandedTargets_.viewports[0] = viewport_;
            expandedTargets_.scissors[0] = scissor_;
        }
    }

    // Node33 starts each geometry request. Color was cleared once on allocation;
    // repeated requests clear only the retained depth/stencil attachment.
    void beginGeometry(ID3D11DeviceContext *context) {
        std::array<ID3D11ShaderResourceView *, 32> empty{};
        context->VSSetShaderResources(0, 32, empty.data());
        context->PSSetShaderResources(0, 32, empty.data());
        context->CSSetShaderResources(0, 32, empty.data());
        std::array<ID3D11UnorderedAccessView *, 8> noUav{};
        context->CSSetUnorderedAccessViews(0, 8, noUav.data(), nullptr);
        context->OMSetRenderTargets(0, nullptr, nullptr);
        // Actual node33: depth/stencil only. Fresh color initialization stays
        // at allocation above; no invented per-request G-buffer color clear.
        context->ClearDepthStencilView(depthView_.Get(), D3D11_CLEAR_DEPTH | D3D11_CLEAR_STENCIL, depthClear_,
                                       0);
    }

    const std::array<ComPtr<ID3D11Texture2D>, 5> &textures() const { return textures_; }
    const std::array<ID3D11RenderTargetView *, 5> &colors() const { return colors_; }
    ID3D11Texture2D *depthTexture() const { return depth_.Get(); }
    ID3D11DepthStencilView *depthView() const { return depthView_.Get(); }
    ID3D11DepthStencilState *depthState() const { return depthState_.Get(); }
    ID3D11RasterizerState *rasterState() const { return raster_.Get(); }
    ID3D11BlendState *blendState() const { return blendState_.Get(); }
    const D3D11_DEPTH_STENCIL_DESC &depthDescription() const { return depthDescription_; }
    const D3D11_RASTERIZER_DESC &rasterDescription() const { return rasterDescription_; }
    const D3D11_BLEND_DESC &blendDescription() const { return blendDescription_; }
    const D3D11_VIEWPORT &viewport() const { return viewport_; }
    const D3D11_RECT &scissor() const { return scissor_; }
    const NativeMainTargetScope &sixTargetScope() const { return expandedTargets_; }

  private:
    std::array<ComPtr<ID3D11Texture2D>, 5> textures_;
    std::array<ComPtr<ID3D11RenderTargetView>, 5> views_;
    std::array<ID3D11RenderTargetView *, 5> colors_{};
    ComPtr<ID3D11Texture2D> depth_;
    ComPtr<ID3D11DepthStencilView> depthView_;
    D3D11_DEPTH_STENCIL_DESC depthDescription_{};
    ComPtr<ID3D11DepthStencilState> depthState_;
    D3D11_RASTERIZER_DESC rasterDescription_{};
    ComPtr<ID3D11RasterizerState> raster_;
    D3D11_BLEND_DESC blendDescription_{};
    ComPtr<ID3D11BlendState> blendState_;
    D3D11_VIEWPORT viewport_{};
    D3D11_RECT scissor_{};
    float depthClear_;
    NativeMainTargetScope expandedTargets_;
};
} // namespace San14::Render
