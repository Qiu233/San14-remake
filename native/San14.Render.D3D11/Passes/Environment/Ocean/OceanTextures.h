// Include after the lab's ComPtr/fs/load/check helpers. No draw or context mutation.
#pragma once
#include <sstream>
#include <optional>
#include "OceanTextureMaterialSettings.h"

struct OceanNativeTexture {
    ComPtr<ID3D11Resource> resource;
    ComPtr<ID3D11ShaderResourceView> view;
    ComPtr<ID3D11SamplerState> sampler;
    UINT width=0,height=0,depth=0,mips=0,layers=0;
    DXGI_FORMAT resourceFormat=DXGI_FORMAT_UNKNOWN,viewFormat=DXGI_FORMAT_UNKNOWN;
};
struct OceanNativeTextures {
    std::optional<OceanNativeTexture> normal3d;       // PS t0/s0, unchanged original BC5 volume.
    std::optional<OceanNativeTexture> normalNoise2d;  // PS t3/s3, unchanged original BC4 planar.
    std::optional<OceanNativeTexture> wvBubble;       // WV PS t4/s4, original60 BGRA slices.
    std::optional<OceanNativeTexture> wvHeightMask;   // WV VS t0/s0, original R32 height mask.
    std::optional<OceanNativeTexture> horizontalDisplacement; // WV VS t1/s1: selected WaveMap442=0, source-null.
    San14::Render::OceanMaterialTextureBindings materialBindings() const {
        return {normal3d.has_value(),normalNoise2d.has_value(),wvHeightMask.has_value()};
    }
};
static ID3D11ShaderResourceView* oceanTextureView(const std::optional<OceanNativeTexture>& texture) {
    return texture?texture->view.Get():nullptr;
}
static ID3D11SamplerState* oceanTextureSampler(const std::optional<OceanNativeTexture>& texture) {
    return texture?texture->sampler.Get():nullptr;
}
static OceanNativeTextures loadOceanTextures(ID3D11Device* device,const fs::path& source) {
    std::ifstream rows(source/"ocean-textures.tsv");
    if(!rows)throw std::runtime_error("ocean-textures.tsv missing");
    OceanNativeTextures result;unsigned seen=0;std::string line;
    while(std::getline(rows,line)) {
        if(line.empty())continue;
        std::istringstream row(line);std::string prefix;
        UINT slot,dimension,width,height,depth,mips,layers,resourceFormat,viewFormat;
        if(!(row>>prefix>>slot>>dimension>>width>>height>>depth>>mips>>layers>>resourceFormat>>viewFormat))
            throw std::runtime_error("Malformed original Ocean texture row");
        OceanNativeTexture* binding;unsigned role;
        if(prefix=="normal3d") {binding=&result.normal3d.emplace();role=1;
            if(slot!=0||dimension!=8||layers!=1||resourceFormat!=82||viewFormat!=83)
                throw std::runtime_error("Original Ocean normal3d resource/SRV contract differs");
        } else if(prefix=="normal-noise2d") {binding=&result.normalNoise2d.emplace();role=2;
            if(slot!=3||dimension!=4||layers!=1||resourceFormat!=79||viewFormat!=80)
                throw std::runtime_error("Original Ocean normal noise resource/SRV contract differs");
        } else if(prefix=="wv-bubble") {binding=&result.wvBubble.emplace();role=4;
            if(slot!=4||dimension!=5||resourceFormat!=90||viewFormat!=91)
                throw std::runtime_error("Original Ocean WV bubble resource/SRV contract differs");
        } else if(prefix=="wv-height-mask") {binding=&result.wvHeightMask.emplace();role=8;
            if(slot!=0||dimension!=4||layers!=1||resourceFormat!=39||viewFormat!=41)
                throw std::runtime_error("Original Ocean WV height resource/SRV contract differs");
        } else throw std::runtime_error("Unknown original Ocean texture role: "+prefix);
        if(seen&role)throw std::runtime_error("Duplicate original Ocean texture role");
        seen|=role;
        if(!width||!height||!depth||!mips||!layers||(dimension!=8&&depth!=1))
            throw std::runtime_error("Invalid original Ocean texture extent");
        std::vector<std::vector<char>> storage(size_t(mips)*layers);
        std::vector<D3D11_SUBRESOURCE_DATA> initial(storage.size());
        for(UINT layer=0;layer<layers;++layer)for(UINT mip=0;mip<mips;++mip) {
            UINT subresource=mip+layer*mips;
            UINT w=std::max(1u,width>>mip),h=std::max(1u,height>>mip),z=std::max(1u,depth>>mip);
            std::string file=prefix+(layers==1?"":"-layer"+std::to_string(layer))+"-mip"+std::to_string(mip)+".bin";
            storage[subresource]=load(source/file);
            UINT pitch=viewFormat==83?((w+3)/4)*16:viewFormat==80?((w+3)/4)*8:w*4;
            UINT slice=pitch*((viewFormat==83||viewFormat==80)?(h+3)/4:h);
            if(storage[subresource].size()!=size_t(slice)*z)
                throw std::runtime_error("Original Ocean encoded mip pitch/body mismatch: "+file);
            initial[subresource]={storage[subresource].data(),pitch,slice};
        }
        D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT(viewFormat);
        if(dimension==8) {
            D3D11_TEXTURE3D_DESC td{};td.Width=width;td.Height=height;td.Depth=depth;td.MipLevels=mips;
            td.Format=DXGI_FORMAT(resourceFormat);td.Usage=D3D11_USAGE_IMMUTABLE;td.BindFlags=D3D11_BIND_SHADER_RESOURCE;
            ComPtr<ID3D11Texture3D> texture;
            check(device->CreateTexture3D(&td,initial.data(),&texture),"Ocean original BC5 Texture3D");
            check(texture.As(&binding->resource),"Ocean original Texture3D resource");
            sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE3D;sd.Texture3D.MipLevels=mips;
        } else {
            D3D11_TEXTURE2D_DESC td{};td.Width=width;td.Height=height;td.MipLevels=mips;td.ArraySize=layers;
            td.Format=DXGI_FORMAT(resourceFormat);td.SampleDesc.Count=1;td.Usage=D3D11_USAGE_IMMUTABLE;
            td.BindFlags=D3D11_BIND_SHADER_RESOURCE;ComPtr<ID3D11Texture2D> texture;
            check(device->CreateTexture2D(&td,initial.data(),&texture),"Ocean original Texture2D/Array");
            check(texture.As(&binding->resource),"Ocean original Texture2D/Array resource");
            if(dimension==5) {
                sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2DARRAY;
                sd.Texture2DArray.MipLevels=mips;sd.Texture2DArray.ArraySize=layers;
            } else {sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=mips;}
        }
        check(device->CreateShaderResourceView(binding->resource.Get(),&sd,&binding->view),"Ocean original typed SRV");
        auto bytes=load(source/(prefix+"-sampler.bin"));D3D11_SAMPLER_DESC sampler{};
        if(bytes.size()!=sizeof(sampler))throw std::runtime_error("Original Ocean sampler must be52B");
        std::memcpy(&sampler,bytes.data(),sizeof(sampler));
        check(device->CreateSamplerState(&sampler,&binding->sampler),"Ocean original source sampler");
        binding->width=width;binding->height=height;binding->depth=depth;binding->mips=mips;binding->layers=layers;
        binding->resourceFormat=DXGI_FORMAT(resourceFormat);binding->viewFormat=DXGI_FORMAT(viewFormat);
    }
    if(seen!=15)throw std::runtime_error("All four original Ocean texture roles required");
    return result;
}
