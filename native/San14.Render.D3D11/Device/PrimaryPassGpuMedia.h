#pragma once
#include "../Runtime/PreparedFileInput.h"
#include "PrimaryShadowGpuMedia.h"
#include "PrimaryForwardMetadata.h"
#include <d3d11.h>
#include <wrl/client.h>
#include <array>
#include <cstring>
#include <iterator>
#include <memory>
#include <string>
#include <vector>

namespace San14::Render {
struct NativePrimaryForwardMedia final {
    template<class T>using ComPtr=Microsoft::WRL::ComPtr<T>;
    NativePrimaryForwardMetadata metadata;
    ComPtr<ID3D11VertexShader> vertex;
    ComPtr<ID3D11PixelShader> pixel;
    std::vector<ComPtr<ID3D11InputLayout>> layouts;
    std::array<ComPtr<ID3D11ShaderResourceView>,29> resources;
    std::array<ComPtr<ID3D11SamplerState>,16> samplers;
    ComPtr<ID3D11BlendState> blend;
private:
    static void require(HRESULT result,const char* operation) {
        if(FAILED(result))throw std::runtime_error(std::string(operation)+" HRESULT="+std::to_string(std::uint32_t(result)));
    }
    static std::vector<char> read(const std::filesystem::path& path) {
        NativePreparedInputFile input(path,std::ios::binary);
        if(!input)throw std::runtime_error("Primary Forward static media is absent: "+path.string());
        return {std::istreambuf_iterator<char>(input),std::istreambuf_iterator<char>()};
    }
static ComPtr<ID3D11ShaderResourceView> whiteArray(ID3D11Device* device,
        const std::filesystem::path& pixelPath,bool cube) {
    auto pixels=read(pixelPath);
    if(pixels.size()!=4)throw std::runtime_error("Forward static fallback pixel is incomplete");
    D3D11_TEXTURE2D_DESC texture{};
    texture.Width=texture.Height=texture.MipLevels=1;texture.ArraySize=cube?6:1;
    texture.Format=DXGI_FORMAT_B8G8R8A8_TYPELESS;texture.SampleDesc.Count=1;
    texture.BindFlags=D3D11_BIND_SHADER_RESOURCE;texture.MiscFlags=cube?D3D11_RESOURCE_MISC_TEXTURECUBE:0;
    std::array<D3D11_SUBRESOURCE_DATA,6> initial;
    for(auto& side:initial)side={pixels.data(),4,4};
    ComPtr<ID3D11Texture2D> owner;require(device->CreateTexture2D(&texture,initial.data(),&owner),"original Common330/340 white array");
    D3D11_SHADER_RESOURCE_VIEW_DESC view{};view.Format=DXGI_FORMAT_B8G8R8A8_UNORM;
    if(cube){view.ViewDimension=D3D11_SRV_DIMENSION_TEXTURECUBEARRAY;view.TextureCubeArray.MipLevels=1;view.TextureCubeArray.NumCubes=1;}
    else{view.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2DARRAY;view.Texture2DArray.MipLevels=1;view.Texture2DArray.ArraySize=1;}
    ComPtr<ID3D11ShaderResourceView> result;require(device->CreateShaderResourceView(owner.Get(),&view,&result),"original Point/Spot array view");return result;
}
public:
    static std::shared_ptr<const NativePrimaryForwardMedia> load(ID3D11Device* device,const std::filesystem::path& input,
            bool includeSourceMetadata=false) {
    NativePrimaryForwardMedia out;out.metadata=includeSourceMetadata?NativePrimaryForwardMetadata::load(input):
        NativePrimaryForwardMetadata::loadGpuLayout(input);
    auto vertex=read(input/"vs.dxbc"),pixel=read(input/"ps.dxbc");
    require(device->CreateVertexShader(vertex.data(),vertex.size(),nullptr,&out.vertex),"original forward VS0");
    require(device->CreatePixelShader(pixel.data(),pixel.size(),nullptr,&out.pixel),"original forward PS11");
    const auto templateCount=out.metadata.templates.size();
    if(!templateCount)throw std::runtime_error("Forward actual template catalog absent");out.layouts.resize(templateCount);
    for(std::size_t templateIndex=0;templateIndex<templateCount;++templateIndex) {
        const auto& source=out.metadata.templates[templateIndex];if(!source.eligible)continue;
        struct Element{std::string name;UINT index,format,slot,offset;};Element row;std::vector<Element> elements;
        NativePreparedInputFile rows(input/source.directory/"layout.txt");
        while(rows>>row.name>>row.index>>row.format>>row.slot>>row.offset)elements.push_back(row);
        std::vector<D3D11_INPUT_ELEMENT_DESC> layout;
        for(auto& e:elements)layout.push_back({e.name.c_str(),e.index,DXGI_FORMAT(e.format),e.slot,e.offset,D3D11_INPUT_PER_VERTEX_DATA,0});
        require(device->CreateInputLayout(layout.data(),UINT(layout.size()),vertex.data(),vertex.size(),&out.layouts.at(templateIndex)),"original forward selected layout");
    }
    NativePreparedInputFile samplerRows(input/"samplers.txt");unsigned slot{};std::string file;
    while(samplerRows>>slot>>file) {
        // Environment sampling belongs to the selected image wrapper, even
        // when an older input directory still lists a fixed sampler here.
        if(slot==11)continue;
        auto source=read(input/file);
        if(source.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Forward static sampler descriptor is incomplete");
        D3D11_SAMPLER_DESC desc{};std::memcpy(&desc,source.data(),sizeof(desc));
        require(device->CreateSamplerState(&desc,&out.samplers.at(slot)),"original forward source sampler");
    }
    out.resources[8]=whiteArray(device,input/"fallback8-pixel4.bin",true);
    out.resources[9]=whiteArray(device,input/"fallback9-pixel4.bin",false);
    auto white=read(input/"volume-white-pixel4.bin");
    if(white.size()!=4)throw std::runtime_error("Forward static volume pixel is incomplete");
    D3D11_TEXTURE3D_DESC volume{};
    volume.Width=volume.Height=volume.Depth=volume.MipLevels=1;volume.Format=DXGI_FORMAT_B8G8R8A8_TYPELESS;volume.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    D3D11_SUBRESOURCE_DATA volumeData{white.data(),4,4};ComPtr<ID3D11Texture3D> volumeOwner;
    require(device->CreateTexture3D(&volume,&volumeData,&volumeOwner),"original Common348 white volume");
    D3D11_SHADER_RESOURCE_VIEW_DESC volumeView{};volumeView.Format=DXGI_FORMAT_B8G8R8A8_UNORM;
    volumeView.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE3D;volumeView.Texture3D.MipLevels=1;
    require(device->CreateShaderResourceView(volumeOwner.Get(),&volumeView,&out.resources[28]),"original white volume view");
    D3D11_BLEND_DESC blend{};blend.RenderTarget[0].RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
    require(device->CreateBlendState(&blend,&out.blend),"original forward no blend / full mask");
        return std::make_shared<const NativePrimaryForwardMedia>(std::move(out));
    }
};
struct NativePrimaryPassGpuMedia final {
    std::shared_ptr<const NativePrimaryForwardMedia> forward;
    std::shared_ptr<const NativePrimaryShadowGpuMedia> shadow;
    static std::shared_ptr<const NativePrimaryPassGpuMedia> load(ID3D11Device* device,
        const std::filesystem::path& forward,const std::filesystem::path& shadow,std::size_t templateCount) {
        auto result=std::make_shared<NativePrimaryPassGpuMedia>();
        result->forward=NativePrimaryForwardMedia::load(device,forward,true);
        result->shadow=NativePrimaryShadowGpuMedia::load(device,shadow,templateCount);
        return result;
    }
};
}
