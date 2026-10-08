#pragma once
#include <fstream>
#include <filesystem>
#include <wrl/client.h>

// Finite debug endpoint only. Called once on the renderer thread after the
// accepted output has completed; no Main, camera or clock is advanced here.
inline void saveFinalOutputProbe(ID3D11Device* device,ID3D11DeviceContext* context,
        ID3D11Texture2D* finalTexture,
        const std::filesystem::path& directory){
    using Microsoft::WRL::ComPtr;
    std::filesystem::create_directories(directory);
    std::ofstream metadata(directory/"descriptor.json");metadata<<"{\"textures\":[";
    bool first=true;
    for(const auto& entry:{std::pair{"native-final",finalTexture}}){
        D3D11_TEXTURE2D_DESC source{};entry.second->GetDesc(&source);
        if(source.MipLevels!=1||source.ArraySize!=1||source.SampleDesc.Count!=1||
                (source.Format!=DXGI_FORMAT_B8G8R8A8_TYPELESS&&source.Format!=DXGI_FORMAT_B8G8R8A8_UNORM&&
                source.Format!=DXGI_FORMAT_B8G8R8A8_UNORM_SRGB))throw std::runtime_error("Output probe requires current single BGRA8 surface");
        auto descriptor=source;descriptor.Usage=D3D11_USAGE_STAGING;descriptor.BindFlags=0;
        descriptor.CPUAccessFlags=D3D11_CPU_ACCESS_READ;descriptor.MiscFlags=0;
        ComPtr<ID3D11Texture2D> staging;
        HRESULT result=device->CreateTexture2D(&descriptor,nullptr,&staging);if(FAILED(result))throw result;
        context->CopyResource(staging.Get(),entry.second);
        D3D11_MAPPED_SUBRESOURCE mapped{};result=context->Map(staging.Get(),0,D3D11_MAP_READ,0,&mapped);
        if(FAILED(result))throw result;
        std::ofstream output(directory/(std::string(entry.first)+".bgra"),std::ios::binary);
        for(UINT y=0;y<source.Height;++y)output.write(static_cast<const char*>(mapped.pData)+size_t(y)*mapped.RowPitch,size_t(source.Width)*4);
        context->Unmap(staging.Get(),0);
        if(!output)throw std::runtime_error("Output probe write failed");
        if(!first)metadata<<',';first=false;
        metadata<<"{\"name\":\""<<entry.first<<"\",\"width\":"<<source.Width<<",\"height\":"<<source.Height
            <<",\"format\":"<<unsigned(source.Format)<<",\"packedRowBytes\":"<<source.Width*4<<'}';
    }
    metadata<<"],\"readbacks\":1,\"advancedMain\":false}\n";
}
