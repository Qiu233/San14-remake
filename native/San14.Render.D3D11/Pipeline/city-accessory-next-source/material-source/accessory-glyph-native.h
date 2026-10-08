#pragma once
#include <d3d11.h>
#include <wrl/client.h>
#include <array>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <vector>

// Call this import-time producer before entering the scene's normal render
// sequence. It leaves its own stock draw state bound, just like a native job.
// The caller owns the returned COM resources until all borrowing draws finish.
namespace accessory_glyph {
using Microsoft::WRL::ComPtr;
inline void check(HRESULT hr) { if (FAILED(hr)) throw std::runtime_error("accessory glyph D3D11 allocation/draw setup failed"); }
inline std::vector<uint8_t> read(const std::filesystem::path& path) {
    std::ifstream stream(path,std::ios::binary|std::ios::ate);
    if(!stream) throw std::runtime_error("accessory glyph input file missing");
    auto size=stream.tellg(); std::vector<uint8_t> bytes(static_cast<size_t>(size));
    stream.seekg(0);stream.read(reinterpret_cast<char*>(bytes.data()),size);
    if(!stream) throw std::runtime_error("accessory glyph input file truncated");return bytes;
}
inline ComPtr<ID3D11Buffer> immutable_buffer(ID3D11Device* device,UINT binding,const std::vector<uint8_t>& bytes) {
    D3D11_BUFFER_DESC desc{};desc.ByteWidth=static_cast<UINT>(bytes.size());desc.Usage=D3D11_USAGE_IMMUTABLE;desc.BindFlags=binding;
    D3D11_SUBRESOURCE_DATA data{};data.pSysMem=bytes.data();ComPtr<ID3D11Buffer> result;check(device->CreateBuffer(&desc,&data,&result));return result;
}
inline ComPtr<ID3D11SamplerState> sampler(ID3D11Device* device,const std::filesystem::path& path) {
    auto bytes=read(path);if(bytes.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("accessory source sampler is not52 bytes");
    D3D11_SAMPLER_DESC desc;std::memcpy(&desc,bytes.data(),sizeof desc);ComPtr<ID3D11SamplerState> result;check(device->CreateSamplerState(&desc,&result));return result;
}
struct Atlas {
    ComPtr<ID3D11Texture2D> texture;ComPtr<ID3D11ShaderResourceView> srv;ComPtr<ID3D11SamplerState> sourceSampler;
    static Atlas create(ID3D11Device* device,const std::filesystem::path& input,uint32_t utf16CodeUnit) {
        Atlas result;char code[9];sprintf_s(code,"%04X",utf16CodeUnit);
        auto bytes=read(input/"atlases"/code/"atlas.bin");if(bytes.size()!=1024u*1024*4)throw std::runtime_error("accessory source atlas size differs");
        D3D11_TEXTURE2D_DESC desc{};desc.Width=desc.Height=1024;desc.MipLevels=desc.ArraySize=1;desc.Format=DXGI_FORMAT_B8G8R8A8_TYPELESS;
        desc.SampleDesc.Count=1;desc.Usage=D3D11_USAGE_IMMUTABLE;desc.BindFlags=D3D11_BIND_SHADER_RESOURCE;
        D3D11_SUBRESOURCE_DATA data{};data.pSysMem=bytes.data();data.SysMemPitch=4096;
        check(device->CreateTexture2D(&desc,&data,&result.texture));D3D11_SHADER_RESOURCE_VIEW_DESC view{};
        view.Format=DXGI_FORMAT_B8G8R8A8_UNORM;view.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;view.Texture2D.MipLevels=1;
        check(device->CreateShaderResourceView(result.texture.Get(),&view,&result.srv));result.sourceSampler=sampler(device,input/"common/glyph-atlas-sampler.bin");return result;
    }
};
struct Primary {
    uint32_t manager{},force{},proxy{},code{};bool producerCompleted{};
    ComPtr<ID3D11Texture2D> texture;ComPtr<ID3D11ShaderResourceView> srv;ComPtr<ID3D11RenderTargetView> rtv;ComPtr<ID3D11SamplerState> sourceSampler;
};
class StockProducer {
    ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;ComPtr<ID3D11InputLayout> layout;
    ComPtr<ID3D11Buffer> vsCb,psCb,indices;ComPtr<ID3D11RasterizerState> raster;
    ComPtr<ID3D11DepthStencilState> depth;ComPtr<ID3D11BlendState> blend;
public:
    StockProducer(ID3D11Device* device,const std::filesystem::path& input) {
        auto vertex=read(input/"common/glyph-vs.dxbc"),pixel=read(input/"common/glyph-ps.dxbc");
        check(device->CreateVertexShader(vertex.data(),vertex.size(),nullptr,&vs));check(device->CreatePixelShader(pixel.data(),pixel.size(),nullptr,&ps));
        D3D11_INPUT_ELEMENT_DESC elements[]={
            {"POSITION",0,DXGI_FORMAT_R32G32B32_FLOAT,0,0,D3D11_INPUT_PER_VERTEX_DATA,0},
            {"COLOR",0,DXGI_FORMAT_R8G8B8A8_UNORM,0,12,D3D11_INPUT_PER_VERTEX_DATA,0},
            {"TEXCOORD",0,DXGI_FORMAT_R32G32_FLOAT,0,16,D3D11_INPUT_PER_VERTEX_DATA,0}};
        check(device->CreateInputLayout(elements,3,vertex.data(),vertex.size(),&layout));
        vsCb=immutable_buffer(device,D3D11_BIND_CONSTANT_BUFFER,read(input/"common/glyph-vs-b0.bin"));
        psCb=immutable_buffer(device,D3D11_BIND_CONSTANT_BUFFER,read(input/"common/glyph-ps-b0.bin"));
        indices=immutable_buffer(device,D3D11_BIND_INDEX_BUFFER,read(input/"common/glyph-indices.bin"));
        D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.CullMode=D3D11_CULL_NONE;rs.DepthClipEnable=TRUE;
        check(device->CreateRasterizerState(&rs,&raster));D3D11_DEPTH_STENCIL_DESC ds{};ds.DepthFunc=D3D11_COMPARISON_ALWAYS;
        ds.StencilReadMask=ds.StencilWriteMask=0xff;ds.FrontFace={D3D11_STENCIL_OP_KEEP,D3D11_STENCIL_OP_KEEP,D3D11_STENCIL_OP_KEEP,D3D11_COMPARISON_ALWAYS};ds.BackFace=ds.FrontFace;
        check(device->CreateDepthStencilState(&ds,&depth));
        D3D11_BLEND_DESC bs{};auto& target=bs.RenderTarget[0];target.BlendEnable=TRUE;target.SrcBlend=D3D11_BLEND_SRC_ALPHA;
        target.DestBlend=D3D11_BLEND_INV_SRC_ALPHA;target.BlendOp=D3D11_BLEND_OP_ADD;target.SrcBlendAlpha=D3D11_BLEND_ZERO;
        target.DestBlendAlpha=D3D11_BLEND_ONE;target.BlendOpAlpha=D3D11_BLEND_OP_ADD;target.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
        check(device->CreateBlendState(&bs,&blend));
    }
    Primary execute(ID3D11Device* device,ID3D11DeviceContext* context,const std::filesystem::path& input,
                    const std::filesystem::path& relativeMaterial,const Atlas* actualSourceAtlas) {
        auto path=input/relativeMaterial;auto job=read(path/"glyph-job.bin");
        if(job.size()!=48&&job.size()!=144)throw std::runtime_error("accessory glyph job size differs");
        std::array<uint32_t,8> header;std::memcpy(header.data(),job.data(),32);
        std::array<float,4> clear;std::memcpy(clear.data(),job.data()+32,16);
        bool hasGlyph=header[7]!=0;
        if(hasGlyph&&(!actualSourceAtlas||job.size()!=144))throw std::runtime_error("accessory job lacks its source atlas");
        Primary result;result.manager=header[0];result.force=header[1];result.proxy=header[2];result.code=header[3];
        D3D11_TEXTURE2D_DESC desc{};desc.Width=128;desc.Height=64;desc.MipLevels=desc.ArraySize=1;desc.Format=static_cast<DXGI_FORMAT>(header[4]);
        desc.SampleDesc.Count=1;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
        check(device->CreateTexture2D(&desc,nullptr,&result.texture));D3D11_SHADER_RESOURCE_VIEW_DESC srvDesc{};
        srvDesc.Format=static_cast<DXGI_FORMAT>(header[5]);srvDesc.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;srvDesc.Texture2D.MipLevels=1;
        check(device->CreateShaderResourceView(result.texture.Get(),&srvDesc,&result.srv));D3D11_RENDER_TARGET_VIEW_DESC rtvDesc{};
        rtvDesc.Format=static_cast<DXGI_FORMAT>(header[6]);rtvDesc.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
        check(device->CreateRenderTargetView(result.texture.Get(),&rtvDesc,&result.rtv));result.sourceSampler=sampler(device,path/"primary-sampler.bin");
        context->ClearRenderTargetView(result.rtv.Get(),clear.data());
        if(hasGlyph){
            std::vector<uint8_t> vertices(job.begin()+48,job.end());auto vertex=immutable_buffer(device,D3D11_BIND_VERTEX_BUFFER,vertices);
            ID3D11RenderTargetView* target=result.rtv.Get();context->OMSetRenderTargets(1,&target,nullptr);context->OMSetDepthStencilState(depth.Get(),0);
            float blendFactor[4]={};context->OMSetBlendState(blend.Get(),blendFactor,0xffffffff);context->RSSetState(raster.Get());
            D3D11_VIEWPORT viewport{0,0,128,64,0,1};context->RSSetViewports(1,&viewport);context->IASetInputLayout(layout.Get());
            context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);ID3D11Buffer* vb=vertex.Get();UINT stride=24,offset=0;
            context->IASetVertexBuffers(0,1,&vb,&stride,&offset);context->IASetIndexBuffer(indices.Get(),DXGI_FORMAT_R16_UINT,0);
            context->VSSetShader(vs.Get(),nullptr,0);context->PSSetShader(ps.Get(),nullptr,0);context->GSSetShader(nullptr,nullptr,0);
            context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
            ID3D11Buffer* vertexCb=vsCb.Get();ID3D11Buffer* pixelCb=psCb.Get();context->VSSetConstantBuffers(0,1,&vertexCb);context->PSSetConstantBuffers(0,1,&pixelCb);
            ID3D11ShaderResourceView* atlas=actualSourceAtlas->srv.Get();ID3D11SamplerState* atlasSampler=actualSourceAtlas->sourceSampler.Get();
            context->PSSetShaderResources(0,1,&atlas);context->PSSetSamplers(0,1,&atlasSampler);context->DrawIndexed(6,0,0);
            ID3D11ShaderResourceView* empty=nullptr;context->PSSetShaderResources(0,1,&empty);
            context->OMSetRenderTargets(0,nullptr,nullptr);
        }
        // This field follows the completed native producer, never just import.
        result.producerCompleted=true;return result;
    }
};
} // namespace accessory_glyph
