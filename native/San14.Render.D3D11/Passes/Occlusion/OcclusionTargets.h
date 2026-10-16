#pragma once
#include <array>
#include <vector>
#include <stdexcept>

namespace San14::Render {
// Ordered candidates are imported format recipes. Device support decides the
// allocation; a previous device's selected format is never a runtime input.
struct OcclusionFormatCandidate {
    unsigned resource{},sampled{},target{};
};
struct OcclusionSurface {
    ComPtr<ID3D11Texture2D> texture;
    ComPtr<ID3D11ShaderResourceView> view;
    ComPtr<ID3D11RenderTargetView> target;
    unsigned width{},height{};
};
struct OcclusionNoiseSource {
    unsigned resource{},sampled{},width{},height{},rowPitch{};
    std::vector<char> bytes;
};
struct OcclusionNoise {
    ComPtr<ID3D11Texture2D> texture;
    ComPtr<ID3D11ShaderResourceView> view;
};
inline OcclusionNoise createOcclusionNoise(ID3D11Device* device,const OcclusionNoiseSource& source) {
    if(source.width!=4||source.height!=4||source.rowPitch!=16||source.bytes.size()!=64)
        throw std::runtime_error("Occlusion procedural noise layout");
    OcclusionNoise result;D3D11_TEXTURE2D_DESC td{};
    td.Width=source.width;td.Height=source.height;td.MipLevels=td.ArraySize=td.SampleDesc.Count=1;
    td.Format=DXGI_FORMAT(source.resource);td.BindFlags=D3D11_BIND_SHADER_RESOURCE;td.Usage=D3D11_USAGE_IMMUTABLE;
    D3D11_SUBRESOURCE_DATA data{};data.pSysMem=source.bytes.data();data.SysMemPitch=source.rowPitch;
    check(device->CreateTexture2D(&td,&data,&result.texture),"Occlusion procedural noise");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT(source.sampled);
    sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
    check(device->CreateShaderResourceView(result.texture.Get(),&sd,&result.view),"Occlusion procedural noise SRV");
    return result;
}
inline OcclusionSurface createOcclusionSurface(ID3D11Device* device,unsigned width,unsigned height,
    const std::vector<OcclusionFormatCandidate>& candidates) {
    if(!width||!height)throw std::runtime_error("Occlusion scratch dimensions are empty");
    for(const auto& candidate:candidates) {
        if(!candidate.resource||!candidate.sampled||!candidate.target)continue;
        UINT support=0,quality=0;
        if(FAILED(device->CheckFormatSupport(DXGI_FORMAT(candidate.sampled),&support))||
            !(support&D3D11_FORMAT_SUPPORT_TEXTURE2D)||!(support&D3D11_FORMAT_SUPPORT_SHADER_SAMPLE))continue;
        if(FAILED(device->CheckFormatSupport(DXGI_FORMAT(candidate.target),&support))||
            !(support&D3D11_FORMAT_SUPPORT_TEXTURE2D)||!(support&D3D11_FORMAT_SUPPORT_RENDER_TARGET))continue;
        if(FAILED(device->CheckMultisampleQualityLevels(DXGI_FORMAT(candidate.target),1,&quality))||!quality)continue;
        OcclusionSurface result;result.width=width;result.height=height;
        D3D11_TEXTURE2D_DESC td{};td.Width=width;td.Height=height;td.MipLevels=td.ArraySize=1;
        td.SampleDesc.Count=1;td.Format=DXGI_FORMAT(candidate.resource);
        td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
        // Once a capability candidate is admitted, creation failure is a real
        // device error, rather than permission to silently change the recipe.
        check(device->CreateTexture2D(&td,nullptr,&result.texture),"Occlusion scratch texture");
        D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT(candidate.sampled);
        sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
        check(device->CreateShaderResourceView(result.texture.Get(),&sd,&result.view),"Occlusion scratch SRV");
        D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=DXGI_FORMAT(candidate.target);rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
        check(device->CreateRenderTargetView(result.texture.Get(),&rd,&result.target),"Occlusion scratch RTV");
        return result;
    }
    throw std::runtime_error("No source occlusion scratch candidate is supported by this device");
}
} // namespace San14::Render
