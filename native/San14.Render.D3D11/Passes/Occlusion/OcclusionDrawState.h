#pragma once
#include <vector>

namespace San14::Render {
// Only actual inherited fields participate in state reuse. No hardware GetDesc
// is used to reconstruct ignored source depth/stencil values.
class OcclusionDrawState {
    struct Depth {
        D3D11_DEPTH_STENCIL_DESC description{};
        ComPtr<ID3D11DepthStencilState> state;
    };
public:
    void prepare(ID3D11Device* device,const D3D11_RASTERIZER_DESC& entryRaster,
        const D3D11_BLEND_DESC& entryBlend,const D3D11_RENDER_TARGET_BLEND_DESC& composition) {
        auto raster=entryRaster;raster.CullMode=D3D11_CULL_NONE;
        check(device->CreateRasterizerState(&raster,&raster_),"Occlusion Cull writer");
        auto scratch=entryBlend;scratch.RenderTarget[0].BlendEnable=FALSE;
        scratch.RenderTarget[0].RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
        check(device->CreateBlendState(&scratch,&scratchBlend_),"Occlusion scratch blend writer");
        auto blend=entryBlend;blend.RenderTarget[0]=composition;
        check(device->CreateBlendState(&blend,&compositionBlend_),"Occlusion alpha composition blend");
        blend.RenderTarget[0].RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
        check(device->CreateBlendState(&blend,&exitBlend_),"Occlusion exit RGBA writer");
    }
    void bindEntry(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical) {
        auto description=logical.description;description.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;
        bindDepth(device,context,logical,description,logical.reference);
        context->RSSetState(raster_.Get());context->OMSetBlendState(scratchBlend_.Get(),nullptr,~0u);
        // Alpha testing is disabled in the source frontend here. There is no
        // D3D11 alpha-test state to bind, and this entry does not select a new
        // shader. The project stock owner carries its own disabled-test CB.
    }
    void bindScratch(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical) {
        auto description=logical.description;disableDepth(description);
        description.StencilEnable=TRUE;description.StencilReadMask=255;
        description.FrontFace.StencilFunc=D3D11_COMPARISON_NOT_EQUAL;
        keepStencil(description);bindDepth(device,context,logical,description,0);
        context->RSSetState(raster_.Get());context->OMSetBlendState(scratchBlend_.Get(),nullptr,~0u);
    }
    void bindComposition(ID3D11Device* device,ID3D11DeviceContext* context,
        NativeSourceDepthState& logical,unsigned reference,unsigned readMask) {
        auto description=logical.description;disableDepth(description);
        description.StencilEnable=TRUE;description.StencilReadMask=BYTE(readMask);
        description.FrontFace.StencilFunc=D3D11_COMPARISON_EQUAL;
        keepStencil(description);bindDepth(device,context,logical,description,reference);
        context->RSSetState(raster_.Get());context->OMSetBlendState(compositionBlend_.Get(),nullptr,~0u);
    }
    void exit(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,BOOL entryDepthTest) {
        auto description=logical.description;description.DepthEnable=entryDepthTest;
        bindDepth(device,context,logical,description,logical.reference);
        context->OMSetBlendState(exitBlend_.Get(),nullptr,~0u);
    }
private:
    static void disableDepth(D3D11_DEPTH_STENCIL_DESC& description) {
        description.DepthEnable=FALSE;description.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;
    }
    static void keepStencil(D3D11_DEPTH_STENCIL_DESC& description) {
        description.FrontFace.StencilFailOp=description.FrontFace.StencilDepthFailOp=
            description.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;
        description.BackFace=description.FrontFace;
    }
    void bindDepth(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
        const D3D11_DEPTH_STENCIL_DESC& description,unsigned reference) {
        for(const auto& depth:depths_)if(sameDepthStencilDescription(depth.description,description)) {
            logical.bind(context,depth.state.Get(),description,reference);return;
        }
        Depth depth;depth.description=description;
        check(device->CreateDepthStencilState(&description,&depth.state),"Occlusion inherited depth/stencil state");
        depths_.push_back(std::move(depth));logical.bind(context,depths_.back().state.Get(),description,reference);
    }
    ComPtr<ID3D11RasterizerState> raster_;
    ComPtr<ID3D11BlendState> scratchBlend_,compositionBlend_,exitBlend_;
    std::vector<Depth> depths_;
};
} // namespace San14::Render
