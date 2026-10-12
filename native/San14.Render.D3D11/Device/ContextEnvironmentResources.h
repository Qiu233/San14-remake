#pragma once
#include <array>
#include <cstdint>
#include <optional>
#include <stdexcept>
#include <d3d11.h>
#include <wrl/client.h>

namespace San14::Render {
struct EnvironmentImage {
    ID3D11ShaderResourceView* view;
    ID3D11SamplerState* sampler;
};

struct ContextEnvironmentDescription {
    D3D11_TEXTURE2D_DESC texture{};
    D3D11_SHADER_RESOURCE_VIEW_DESC view{};
    D3D11_SAMPLER_DESC sampler{};
    std::array<std::uint32_t,6> pixels{};
};
inline ContextEnvironmentDescription contextEnvironmentDescription() {
    ContextEnvironmentDescription result;
    auto& texture=result.texture;
    texture.Width=texture.Height=texture.MipLevels=1;texture.ArraySize=6;
    texture.Format=DXGI_FORMAT_B8G8R8A8_TYPELESS;texture.SampleDesc.Count=1;
    texture.Usage=D3D11_USAGE_IMMUTABLE;texture.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    texture.MiscFlags=D3D11_RESOURCE_MISC_TEXTURECUBE;
    result.view.Format=DXGI_FORMAT_B8G8R8A8_UNORM;
    result.view.ViewDimension=D3D11_SRV_DIMENSION_TEXTURECUBE;result.view.TextureCube.MipLevels=1;
    auto& sampler=result.sampler;
    sampler.Filter=D3D11_FILTER_MIN_MAG_LINEAR_MIP_POINT;
    sampler.AddressU=sampler.AddressV=sampler.AddressW=D3D11_TEXTURE_ADDRESS_CLAMP;
    sampler.MaxAnisotropy=1;sampler.ComparisonFunc=D3D11_COMPARISON_LESS_EQUAL;
    return result;
}

// A fresh normal execution context owns the backend default independently of
// scene Common settings and Sky generation. Custom context replacement and
// missing execution Common owners require their own lifecycle.
class ContextEnvironmentResources {
public:
    explicit ContextEnvironmentResources(ID3D11Device* device) {
        const auto description=contextEnvironmentDescription();
        std::array<D3D11_SUBRESOURCE_DATA,6> initial{};
        for(std::size_t face=0;face<initial.size();++face)
            initial[face]={&description.pixels[face],sizeof(std::uint32_t),sizeof(std::uint32_t)};
        auto require=[](HRESULT result,const char* message) {
            if(FAILED(result))throw std::runtime_error(message);
        };
        require(device->CreateTexture2D(&description.texture,initial.data(),&texture_),"Context environment cube allocation failed");
        require(device->CreateShaderResourceView(texture_.Get(),&description.view,&view_),"Context environment cube view failed");
        require(device->CreateSamplerState(&description.sampler,&sampler_),"Context environment cube sampler failed");
    }
    ContextEnvironmentResources(const ContextEnvironmentResources&)=delete;
    ContextEnvironmentResources& operator=(const ContextEnvironmentResources&)=delete;

    // The normal Main View has an existing Sky CPU even when its generated
    // image query is disabled. Only that query's actual absent wrapper selects
    // this context image; a selected resource preparation failure must throw.
    EnvironmentImage select(const std::optional<EnvironmentImage>& sky) const {
        return sky?*sky:EnvironmentImage{view_.Get(),sampler_.Get()};
    }
private:
    Microsoft::WRL::ComPtr<ID3D11Texture2D> texture_;
    Microsoft::WRL::ComPtr<ID3D11ShaderResourceView> view_;
    Microsoft::WRL::ComPtr<ID3D11SamplerState> sampler_;
};
} // namespace San14::Render
