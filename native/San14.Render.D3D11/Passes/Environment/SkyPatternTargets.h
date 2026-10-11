#pragma once
#include "SkyPatternTargetInputs.h"
#include <cstring>
#include <optional>
#include <utility>
#include <d3d11.h>
#include <wrl/client.h>

namespace San14::Render {
struct NativeSkyPatternTarget {
    SkyPatternTargetDescription description;
    Microsoft::WRL::ComPtr<ID3D11Texture2D> texture;
    Microsoft::WRL::ComPtr<ID3D11ShaderResourceView> view;
    Microsoft::WRL::ComPtr<ID3D11RenderTargetView> surface;
    Microsoft::WRL::ComPtr<ID3D11SamplerState> sampler;
};

// The same long-lived Sky owns these declaration-backed wrappers. Import does
// not allocate targets. A selected operation can prepare one wrapper lazily;
// successful preparation alone makes no claim about its rendered contents.
class NativeSkyPatternTargets {
  public:
    NativeSkyPatternTargets(const NativeSkyPatternTargets&)=delete;
    NativeSkyPatternTargets& operator=(const NativeSkyPatternTargets&)=delete;
    NativeSkyPatternTargets(NativeSkyPatternTargets&&)=default;
    NativeSkyPatternTargets& operator=(NativeSkyPatternTargets&&)=default;

    explicit NativeSkyPatternTargets(SkyPatternTargetInputs inputs):inputs_(std::move(inputs)) {}

    const SkyPatternTargetDescription& description(std::size_t index) const {
        return inputs_.descriptions.at(index);
    }

    const NativeSkyPatternTarget* prepared(std::size_t index) const {
        const auto& target=targets_.at(index);
        return target?&*target:nullptr;
    }

    const NativeSkyPatternTarget& prepare(ID3D11Device* device,std::size_t index) {
        auto& installed=targets_.at(index);
        if(installed)return *installed;

        NativeSkyPatternTarget candidate;
        candidate.description=inputs_.descriptions.at(index);
        const auto& declared=candidate.description;
        D3D11_TEXTURE2D_DESC texture{};
        texture.Width=declared.width;texture.Height=declared.height;
        texture.MipLevels=declared.levels;texture.ArraySize=1;
        texture.Format=DXGI_FORMAT(declared.resourceFormat);
        texture.SampleDesc.Count=declared.samples;
        texture.Usage=D3D11_USAGE_DEFAULT;
        texture.BindFlags=D3D11_BIND_SHADER_RESOURCE|D3D11_BIND_RENDER_TARGET;
        check(device->CreateTexture2D(&texture,nullptr,&candidate.texture),"Pattern declared target resource");

        D3D11_SHADER_RESOURCE_VIEW_DESC view{};
        view.Format=DXGI_FORMAT(declared.shaderFormat);
        view.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;
        view.Texture2D.MipLevels=declared.levels;
        check(device->CreateShaderResourceView(candidate.texture.Get(),&view,&candidate.view),"Pattern declared sampled view");

        D3D11_RENDER_TARGET_VIEW_DESC surface{};
        surface.Format=DXGI_FORMAT(declared.targetFormat);
        surface.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
        surface.Texture2D.MipSlice=0;
        check(device->CreateRenderTargetView(candidate.texture.Get(),&surface,&candidate.surface),"Pattern default target surface");

        D3D11_SAMPLER_DESC sampler{};
        static_assert(sizeof(sampler)==52);
        std::memcpy(&sampler,inputs_.samplers[index].data(),sizeof(sampler));
        check(device->CreateSamplerState(&sampler,&candidate.sampler),"Pattern target output sampler");
        installed=std::move(candidate);
        return *installed;
    }

  private:
    SkyPatternTargetInputs inputs_;
    std::array<std::optional<NativeSkyPatternTarget>,4> targets_;
};
} // namespace San14::Render
