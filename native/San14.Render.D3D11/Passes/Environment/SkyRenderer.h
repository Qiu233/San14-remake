#pragma once
#include "SkyMaterialInputs.h"
#include "SkyDrawState.h"
#include "MainSkyBlendState.h"
#include "../../Device/DepthState.h"

// Device resources belong to the selected Main Sky generation. Numeric camera,
// weather and lighting values are published independently of program creation.
struct NativeMainSky {
    UINT width=0,height=0;
    AmbientNativeProgram program;
    std::vector<AmbientNativeTextureBinding> textures;
    std::array<ID3D11ShaderResourceView*,128> views{};
    std::array<ID3D11SamplerState*,16> samplers{};
    ComPtr<ID3D11Buffer> vertices,vertexConstants,pixelConstants;
    San14::Render::Interop::SkyMaterialLayout materialLayout;
    std::vector<char> vertexBytes,pixelBytes;
    NativeMainSkyBlendCache blend;
    ComPtr<ID3D11DepthStencilState> depth;
    ComPtr<ID3D11RasterizerState> raster,rasterEntry;
    D3D11_DEPTH_STENCIL_DESC depthDescription{};
};
static NativeMainSky prepareNativeMainSky(ID3D11Device* device,const fs::path& source,
    UINT width,UINT height) {
    NativeMainSky sky;sky.width=width;sky.height=height;
    sky.program=ambientProgram(device,source,"main-sky");
    sky.textures=ambientTextures(device,source,"main-sky-textures.tsv",std::nullopt);
    sky.vertices=buffer(device,load(source/"quad-position-xy.bin"),D3D11_BIND_VERTEX_BUFFER);
    sky.materialLayout=San14::Render::Interop::readSkyMaterialLayout(source/"material-layout.bin");
    sky.vertexBytes.resize(sky.materialLayout.vertex.byteSize);
    sky.pixelBytes.resize(sky.materialLayout.pixel.byteSize);
    auto constantBuffer=[&](const std::vector<char>& bytes) {
        D3D11_BUFFER_DESC desc{};desc.ByteWidth=UINT(bytes.size());desc.Usage=D3D11_USAGE_DEFAULT;
        desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=bytes.data();ComPtr<ID3D11Buffer> result;
        check(device->CreateBuffer(&desc,&initial,&result),"Main Sky mutable selected-layout constants");
        return result;
    };
    sky.vertexConstants=constantBuffer(sky.vertexBytes);
    sky.pixelConstants=constantBuffer(sky.pixelBytes);
    for(auto& texture:sky.textures) {
        if(texture.slot>=sky.samplers.size())throw std::runtime_error("Main Sky texture/sampler slot exceeds the supported pair interface");
        sky.views[texture.slot]=texture.view.Get();sky.samplers[texture.slot]=texture.sampler.Get();
    }
    return sky;
}
static void publishNativeMainSkyMaterial(ID3D11DeviceContext* context,NativeMainSky& sky,
    const San14::Render::SkyMaterialState& state) {
    sky.materialLayout.vertex.pack(state,sky.vertexBytes);
    sky.materialLayout.pixel.pack(state,sky.pixelBytes);
    context->UpdateSubresource(sky.vertexConstants.Get(),0,nullptr,sky.vertexBytes.data(),0,0);
    context->UpdateSubresource(sky.pixelConstants.Get(),0,nullptr,sky.pixelBytes.data(),0,0);
}
static void prepareNativeMainSkyReachedState(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeSourceDepthState& logical,NativeMainSky& sky) {
    const auto ds=reachedMainSkyDepth(logical.description);
    if(!sky.depth||!sameDepthStencilDescription(ds,sky.depthDescription)) {
        sky.depth.Reset();check(device->CreateDepthStencilState(&ds,&sky.depth),"Main Sky reached depth/stencil");
        sky.depthDescription=ds;
    }
    ComPtr<ID3D11RasterizerState> entryRaster;context->RSGetState(&entryRaster);
    if(!sky.raster||(entryRaster.Get()!=sky.rasterEntry.Get()&&entryRaster.Get()!=sky.raster.Get())) {
        D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.DepthClipEnable=TRUE;
        if(entryRaster)entryRaster->GetDesc(&rs);
        rs=reachedMainSkyRaster(rs);
        sky.raster.Reset();check(device->CreateRasterizerState(&rs,&sky.raster),"Main Sky CullNone");
        sky.rasterEntry=entryRaster;
    }
    context->RSSetState(sky.raster.Get());logical.bind(context,sky.depth.Get(),ds,0);
}
static void executeNativeMainSky(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeMainSky& sky,const San14::Render::SkyDrawPolicy& policy,
    const San14::Render::CommonRenderPolicy& common,UINT targetCount) {
    context->PSSetShaderResources(0,UINT(sky.views.size()),sky.views.data());
    context->PSSetSamplers(0,UINT(sky.samplers.size()),sky.samplers.data());
    bindNativeMainSkyBlend(device,context,sky.blend,policy.suppressStars,false,common.skyAlphaBlend,targetCount);
    context->IASetInputLayout(sky.program.layout.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    auto vb=sky.vertices.Get();UINT stride=8,offset=0;context->IASetVertexBuffers(0,1,&vb,&stride,&offset);
    context->VSSetShader(sky.program.vs.Get(),nullptr,0);context->PSSetShader(sky.program.ps.Get(),nullptr,0);
    context->GSSetShader(nullptr,nullptr,0);context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
    auto vcb=sky.vertexConstants.Get(),pcb=sky.pixelConstants.Get();
    context->VSSetConstantBuffers(sky.materialLayout.vertex.slot,1,&vcb);
    context->PSSetConstantBuffers(sky.materialLayout.pixel.slot,1,&pcb);
    // The caller's actual Scene HDR target remains bound. Sky neither pushes
    // a private target nor clears it, and the reached state continues to Cloud.
    context->Draw(4,0);
    std::array<ID3D11ShaderResourceView*,128> empty{};
    context->PSSetShaderResources(0,UINT(empty.size()),empty.data());
}
