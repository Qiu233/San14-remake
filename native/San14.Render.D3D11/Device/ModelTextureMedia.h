#pragma once
#include "../Runtime/PreparedFileInput.h"
#include <d3d11.h>
#include <wrl/client.h>
#include <cstdint>
#include <stdexcept>
#include <string>
#include <vector>

namespace San14::Render {
// One encoded-mip importer for ordinary models and the application primary bank.
// Resource/view formats and all encoded texels are kept exactly as prepared.
inline Microsoft::WRL::ComPtr<ID3D11ShaderResourceView> loadNativeModelTexture(
        ID3D11Device* device,const std::filesystem::path& path,DXGI_FORMAT resourceFormat,DXGI_FORMAT viewFormat) {
    NativePreparedInputFile input(path,std::ios::binary);
    if(!input)throw std::runtime_error(path.string());
    auto word=[&]() {
        std::uint32_t value{};input.read(reinterpret_cast<char*>(&value),sizeof(value));
        if(!input)throw std::runtime_error("Model source texture header is incomplete");
        return value;
    };
    auto require=[](HRESULT result,const char* what) {
        if(FAILED(result))throw std::runtime_error(std::string(what)+" HRESULT="+std::to_string(std::uint32_t(result)));
    };
    D3D11_TEXTURE2D_DESC description{};
    description.Width=word();description.Height=word();description.ArraySize=word();description.MipLevels=word();
    auto encodedFormat=DXGI_FORMAT(word());
    if(encodedFormat!=viewFormat||description.ArraySize!=1)
        throw std::runtime_error("Model source texture view mismatch");
    description.Format=resourceFormat;description.SampleDesc.Count=1;
    description.Usage=D3D11_USAGE_IMMUTABLE;description.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    std::vector<std::vector<char>> storage(description.MipLevels);
    std::vector<D3D11_SUBRESOURCE_DATA> initial(description.MipLevels);
    for(UINT mip=0;mip<description.MipLevels;++mip) {
        initial[mip].SysMemPitch=word();initial[mip].SysMemSlicePitch=word();storage[mip].resize(word());
        input.read(storage[mip].data(),storage[mip].size());
        if(!input)throw std::runtime_error("Model source texture mip is incomplete");
        initial[mip].pSysMem=storage[mip].data();
    }
    Microsoft::WRL::ComPtr<ID3D11Texture2D> texture;
    require(device->CreateTexture2D(&description,initial.data(),&texture),"Model original texture");
    D3D11_SHADER_RESOURCE_VIEW_DESC viewDescription{};
    viewDescription.Format=viewFormat;viewDescription.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;
    viewDescription.Texture2D.MipLevels=description.MipLevels;
    Microsoft::WRL::ComPtr<ID3D11ShaderResourceView> view;
    require(device->CreateShaderResourceView(texture.Get(),&viewDescription,&view),"Model source texture SRV");
    return view;
}
} // namespace San14::Render
