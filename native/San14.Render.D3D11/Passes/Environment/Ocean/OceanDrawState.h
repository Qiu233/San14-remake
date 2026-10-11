#pragma once
#include "../../../Device/DepthState.h"
#include "../../../Scene/CommonRenderPolicy.h"
#include <cstdint>
#include <utility>

inline bool oceanDrawEnabled(std::uint32_t group,float refractionTransparency,
    const San14::Render::CommonRenderPolicy& policy) {
    return group!=0 && refractionTransparency>0 && (!policy.present || !policy.suppressOceanDrawing);
}

// Only nonzero admitted groups reach the stencil writer. Group zero skips the
// entire draw; its complemented exclusion is not a request to draw with 0xFF.
inline UINT oceanStencilReference(std::uint32_t group) {
    const std::uint32_t exclusion = group == 0 ? 0u : ~(1u << ((group - 1u) & 31u));
    return ~exclusion | 0x50u;
}

// Ocean owns these writes. Comparison, color masks and unrelated pipeline
// settings remain associated with the actual execution entry.
inline D3D11_DEPTH_STENCIL_DESC oceanDrawDepth(const D3D11_DEPTH_STENCIL_DESC& entry) {
    auto result = entry;
    result.DepthEnable = TRUE;
    result.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ALL;
    result.StencilEnable = TRUE;
    result.StencilReadMask = result.StencilWriteMask = 255;
    result.FrontFace.StencilFunc = D3D11_COMPARISON_ALWAYS;
    result.FrontFace.StencilFailOp = result.FrontFace.StencilDepthFailOp = D3D11_STENCIL_OP_KEEP;
    result.FrontFace.StencilPassOp = D3D11_STENCIL_OP_REPLACE;
    result.BackFace = result.FrontFace;
    return result;
}
inline D3D11_DEPTH_STENCIL_DESC oceanExitDepth(const D3D11_DEPTH_STENCIL_DESC& entry) {
    auto result = oceanDrawDepth(entry);
    result.DepthWriteMask = entry.DepthWriteMask;
    result.StencilEnable = FALSE;
    return result;
}
inline D3D11_RASTERIZER_DESC oceanDrawRaster(const D3D11_RASTERIZER_DESC& entry) {
    auto result = entry;
    result.CullMode = D3D11_CULL_BACK;
    return result;
}
inline D3D11_BLEND_DESC oceanDrawBlend(const D3D11_BLEND_DESC& entry) {
    auto result = entry;
    result.RenderTarget[0].BlendEnable = FALSE;
    return result;
}

#ifndef SOURCE_OCEAN_DRAW_STATE_CPU_ONLY
// Device states follow actual entry objects. Re-entering the reached Cull Back
// state reuses this owner's output rather than allocating another copy.
struct NativeOceanDrawState {
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11RasterizerState> rasterEntry, raster;
    ComPtr<ID3D11BlendState> blendEntry, blend;
    ComPtr<ID3D11DepthStencilState> drawDepth, exitDepth;
    D3D11_DEPTH_STENCIL_DESC drawDescription{}, exitDescription{};
    bool rasterReady = false, blendReady = false, depthReady = false;

    void prepare(ID3D11RasterizerState* currentRaster, ID3D11BlendState* currentBlend,
        const D3D11_DEPTH_STENCIL_DESC& entry) {
        if (!rasterReady || (rasterEntry.Get() != currentRaster && raster.Get() != currentRaster)) {
            D3D11_RASTERIZER_DESC description{};
            description.FillMode = D3D11_FILL_SOLID;
            description.CullMode = D3D11_CULL_BACK;
            description.DepthClipEnable = TRUE;
            if (currentRaster) currentRaster->GetDesc(&description);
            const auto wanted = oceanDrawRaster(description);
            ComPtr<ID3D11RasterizerState> next;
            check(device->CreateRasterizerState(&wanted, &next), "Ocean inherited raster state");
            raster = std::move(next);
            rasterEntry = currentRaster;
            rasterReady = true;
        }
        if (!blendReady || (blendEntry.Get() != currentBlend && blend.Get() != currentBlend)) {
            D3D11_BLEND_DESC description{};
            for (auto& target : description.RenderTarget) {
                target.SrcBlend = target.SrcBlendAlpha = D3D11_BLEND_ONE;
                target.DestBlend = target.DestBlendAlpha = D3D11_BLEND_ZERO;
                target.BlendOp = target.BlendOpAlpha = D3D11_BLEND_OP_ADD;
                target.RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
            }
            if (currentBlend) currentBlend->GetDesc(&description);
            const auto wanted = oceanDrawBlend(description);
            ComPtr<ID3D11BlendState> next;
            check(device->CreateBlendState(&wanted, &next), "Ocean inherited six-target blend state");
            blend = std::move(next);
            blendEntry = currentBlend;
            blendReady = true;
        }
        const auto wantedDraw = oceanDrawDepth(entry);
        const auto wantedExit = oceanExitDepth(entry);
        if (!depthReady || !sameDepthStencilDescription(drawDescription, wantedDraw) ||
            !sameDepthStencilDescription(exitDescription, wantedExit)) {
            ComPtr<ID3D11DepthStencilState> nextDraw, nextExit;
            check(device->CreateDepthStencilState(&wantedDraw, &nextDraw), "Ocean inherited draw depth state");
            check(device->CreateDepthStencilState(&wantedExit, &nextExit), "Ocean exit depth state");
            drawDepth = std::move(nextDraw);
            exitDepth = std::move(nextExit);
            drawDescription = wantedDraw;
            exitDescription = wantedExit;
            depthReady = true;
        }
    }
};

class NativeOceanDrawScope {
  public:
    NativeOceanDrawScope(NativeOceanDrawState& state, ID3D11DeviceContext* context,
        NativeSourceDepthState& logical, std::uint32_t group)
        : state_(state), context_(context), logical_(logical), reference_(oceanStencilReference(group)) {
        ComPtr<ID3D11RasterizerState> raster;
        context_->RSGetState(&raster);
        context_->OMGetBlendState(&blend_, factor_, &sampleMask_);
        state_.prepare(raster.Get(), blend_.Get(), logical_.description);
        context_->RSSetState(state_.raster.Get());
        context_->OMSetBlendState(state_.blend.Get(), factor_, sampleMask_);
        logical_.bind(context_, state_.drawDepth.Get(), state_.drawDescription, reference_);
    }
    NativeOceanDrawScope(const NativeOceanDrawScope&) = delete;
    NativeOceanDrawScope& operator=(const NativeOceanDrawScope&) = delete;
    ~NativeOceanDrawScope() {
        // Cull Back and depth test remain reached. Only the temporary write,
        // stencil enable and blend override leave this draw's ownership.
        logical_.bind(context_, state_.exitDepth.Get(), state_.exitDescription, reference_);
        context_->OMSetBlendState(blend_.Get(), factor_, sampleMask_);
    }

  private:
    NativeOceanDrawState& state_;
    ID3D11DeviceContext* context_;
    NativeSourceDepthState& logical_;
    ComPtr<ID3D11BlendState> blend_;
    FLOAT factor_[4]{};
    UINT sampleMask_{}, reference_{};
};
#endif
