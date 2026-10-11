#pragma once
#include "SkyPatternMaterial.h"
#include "SkyPatternProgramBank.h"
#include "../../Device/RenderTargetScope.h"
#include <map>
#include <cmath>
#include <d3d11.h>
#include <wrl/client.h>

namespace San14::Render {
// These are the complete prepared noise group, not the selected shader's read
// set. The same image owners also supply Main and WORK Cloud.
struct NativeSkyPatternNoise {
    struct Image {
        Microsoft::WRL::ComPtr<ID3D11ShaderResourceView> view;
        Microsoft::WRL::ComPtr<ID3D11SamplerState> sampler;
    };
    std::array<Image,3> layers;
    Image cirrus;
    const Image& image(std::uint32_t role) const {return role==3?cirrus:layers.at(role);}
};

class NativeSkyPatternRenderer {
    template<class T> using Ptr=Microsoft::WRL::ComPtr<T>;
    struct Program {
        SkyPatternMaterialLayout layout;
        Ptr<ID3D11VertexShader> vertex;
        Ptr<ID3D11PixelShader> pixel;
        Ptr<ID3D11Buffer> vertexConstants,pixelConstants;
        std::vector<char> vertexBytes,pixelBytes;
    };
    // D49 owns the target/rect scope;1192BD0 temporarily changes only depth
    // test/write, slot-zero blend enable and Cull. Fixed alpha testing has no
    // D3D11 state: the imported actual pass interface is the supported off route.
    struct DrawScope {
        ID3D11DeviceContext* context;
        NativeMainTargetScope targets;
        Ptr<ID3D11DepthStencilState> depth;
        Ptr<ID3D11RasterizerState> raster;
        Ptr<ID3D11BlendState> blend;
        UINT reference{},sampleMask{};
        float factors[4]{};
        explicit DrawScope(ID3D11DeviceContext* value):context(value),targets(NativeMainTargetScope::retain(value)) {
            context->OMGetDepthStencilState(&depth,&reference);
            context->RSGetState(&raster);
            context->OMGetBlendState(&blend,factors,&sampleMask);
        }
        ~DrawScope() {
            context->OMSetDepthStencilState(depth.Get(),reference);
            context->RSSetState(raster.Get());
            context->OMSetBlendState(blend.Get(),factors,sampleMask);
            targets.restore(context);
        }
    };
public:
    explicit NativeSkyPatternRenderer(std::filesystem::path source):source_(std::move(source)),bank_(SkyPatternProgramBank::read(source_)) {}
    const SkyPatternProgramBank& bank() const {return bank_;}

    // The caller publishes its matrix after this target bind and before any
    // program/material preparation. The scope restores on exceptional exits.
    template<class PrepareMaterial>
    void execute(ID3D11Device* device,ID3D11DeviceContext* context,
        ID3D11RenderTargetView* target,UINT width,UINT height,
        SkyPatternVariant& retained,
        const NativeSkyPatternNoise& noise,PrepareMaterial&& prepareMaterial) {
        DrawScope scope(context);
        std::array<ID3D11ShaderResourceView*,128> empty{};
        context->PSSetShaderResources(0,UINT(empty.size()),empty.data());
        context->VSSetShaderResources(0,UINT(empty.size()),empty.data());
        context->OMSetRenderTargets(1,&target,nullptr);
        D3D11_VIEWPORT viewport{0,0,float(width),float(height),0,1};
        D3D11_RECT scissor{0,0,LONG(width),LONG(height)};
        context->RSSetViewports(1,&viewport);context->RSSetScissorRects(1,&scissor);

        // This callback first publishes worldToPattern, then prepares the
        // temporary cameras. Subsequent errors retain that earlier publication.
        const auto [material,selection]=prepareMaterial();
        const auto selected=bank_.select(selection,retained);
        auto& program=prepareProgram(device,selected.program);
        if(program.layout.usesIntegration&&material.volume.densityParameters[0]<1&&
            material.volume.densityParameters[3]>0) {
            const auto& ray=material.raySamples;
            if(!std::isfinite(ray.X)||!std::isfinite(ray.Y)||!std::isfinite(ray.W)||
                !(ray.Y>0)||!(ray.Y+std::min(0.f,ray.X)>0)||!(ray.W>0)||std::ceil(ray.Y)>4096)
                throw std::runtime_error("Pattern integration exceeds the supported material budget");
        }
        program.layout.vertex.pack(material,program.vertexBytes);
        program.layout.pixel.pack(material,program.pixelBytes);
        context->UpdateSubresource(program.vertexConstants.Get(),0,nullptr,program.vertexBytes.data(),0,0);
        context->UpdateSubresource(program.pixelConstants.Get(),0,nullptr,program.pixelBytes.data(),0,0);
        prepareDrawState(device,scope);
        context->OMSetDepthStencilState(drawDepth_.Get(),scope.reference);
        context->RSSetState(drawRaster_.Get());
        context->OMSetBlendState(drawBlend_.Get(),scope.factors,scope.sampleMask);
        for(const auto& route:program.layout.textures) {
            auto view=noise.image(route.role).view.Get();
            context->PSSetShaderResources(route.slot,1,&view);
        }
        for(const auto& route:program.layout.samplers) {
            const auto texture=std::find_if(program.layout.textures.begin(),program.layout.textures.end(),
                [&](const auto& value){return value.slot==route.textureSlot;});
            auto sampler=noise.image(texture->role).sampler.Get();
            context->PSSetSamplers(route.slot,1,&sampler);
        }
        auto vertexConstants=program.vertexConstants.Get(),pixelConstants=program.pixelConstants.Get();
        context->VSSetConstantBuffers(program.layout.vertex.slot,1,&vertexConstants);
        context->PSSetConstantBuffers(program.layout.pixel.slot,1,&pixelConstants);
        context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
        context->VSSetShader(program.vertex.Get(),nullptr,0);context->PSSetShader(program.pixel.Get(),nullptr,0);
        context->GSSetShader(nullptr,nullptr,0);context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
        context->Draw(4,0);
        ID3D11ShaderResourceView* nullView=nullptr;
        for(const auto& route:program.layout.textures)context->PSSetShaderResources(route.slot,1,&nullView);
        ID3D11Buffer* nullBuffer=nullptr;
        context->VSSetConstantBuffers(program.layout.vertex.slot,1,&nullBuffer);
        context->PSSetConstantBuffers(program.layout.pixel.slot,1,&nullBuffer);
        context->VSSetShader(nullptr,nullptr,0);context->PSSetShader(nullptr,nullptr,0);
    }
private:
    Program& prepareProgram(ID3D11Device* device,const SkyPatternProgramReference& reference) {
        if(reference.recipe<0||reference.vertex<0||reference.pixel<0)
            throw std::runtime_error("The selected Pattern pass has no drawable shader stages");
        if(auto found=programs_.find(reference.recipe);found!=programs_.end())return found->second;
        const auto recipe=source_/"recipes"/std::to_string(reference.recipe);
        std::ifstream unsupported(recipe/"unsupported.txt");
        if(unsupported)throw std::runtime_error("Selected Pattern recipe is unsupported: "+
            std::string(std::istreambuf_iterator<char>(unsupported),{}));
        Program candidate;candidate.layout=Interop::readSkyPatternMaterialLayout(recipe/"layout.bin");
        const auto vertex=load(source_/"programs"/(std::to_string(reference.vertex)+".dxbc"));
        const auto pixel=load(source_/"programs"/(std::to_string(reference.pixel)+".dxbc"));
        check(device->CreateVertexShader(vertex.data(),vertex.size(),nullptr,&candidate.vertex),"Pattern selected VS");
        check(device->CreatePixelShader(pixel.data(),pixel.size(),nullptr,&candidate.pixel),"Pattern selected PS");
        auto constants=[&](std::uint32_t bytes,std::vector<char>& data,Ptr<ID3D11Buffer>& buffer) {
            data.resize(bytes);
            D3D11_BUFFER_DESC description{};description.ByteWidth=bytes;description.Usage=D3D11_USAGE_DEFAULT;
            description.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
            D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=data.data();
            check(device->CreateBuffer(&description,&initial,&buffer),"Pattern selected named constants");
        };
        constants(candidate.layout.vertex.bytes,candidate.vertexBytes,candidate.vertexConstants);
        constants(candidate.layout.pixel.bytes,candidate.pixelBytes,candidate.pixelConstants);
        return programs_.emplace(reference.recipe,std::move(candidate)).first->second;
    }
    void prepareDrawState(ID3D11Device* device,const DrawScope& entry) {
        if(!drawDepth_||entry.depth.Get()!=entryDepth_.Get()) {
            D3D11_DEPTH_STENCIL_DESC value{};
            value.DepthEnable=TRUE;value.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ALL;value.DepthFunc=D3D11_COMPARISON_LESS;
            value.StencilReadMask=value.StencilWriteMask=255;
            value.FrontFace=value.BackFace={D3D11_STENCIL_OP_KEEP,D3D11_STENCIL_OP_KEEP,D3D11_STENCIL_OP_KEEP,D3D11_COMPARISON_ALWAYS};
            if(entry.depth)entry.depth->GetDesc(&value);
            value.DepthEnable=FALSE;value.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;
            Ptr<ID3D11DepthStencilState> prepared;
            check(device->CreateDepthStencilState(&value,&prepared),"Pattern inherited temporary depth");
            drawDepth_=std::move(prepared);entryDepth_=entry.depth;
        }
        if(!drawRaster_||entry.raster.Get()!=entryRaster_.Get()) {
            D3D11_RASTERIZER_DESC value{};value.FillMode=D3D11_FILL_SOLID;value.CullMode=D3D11_CULL_BACK;value.DepthClipEnable=TRUE;
            if(entry.raster)entry.raster->GetDesc(&value);
            value.CullMode=D3D11_CULL_NONE;
            Ptr<ID3D11RasterizerState> prepared;
            check(device->CreateRasterizerState(&value,&prepared),"Pattern inherited temporary raster");
            drawRaster_=std::move(prepared);entryRaster_=entry.raster;
        }
        if(!drawBlend_||entry.blend.Get()!=entryBlend_.Get()) {
            D3D11_BLEND_DESC value{};
            for(auto& target:value.RenderTarget) {
                target.SrcBlend=target.SrcBlendAlpha=D3D11_BLEND_ONE;
                target.DestBlend=target.DestBlendAlpha=D3D11_BLEND_ZERO;
                target.BlendOp=target.BlendOpAlpha=D3D11_BLEND_OP_ADD;target.RenderTargetWriteMask=15;
            }
            if(entry.blend)entry.blend->GetDesc(&value);
            value.RenderTarget[0].BlendEnable=FALSE;
            Ptr<ID3D11BlendState> prepared;
            check(device->CreateBlendState(&value,&prepared),"Pattern inherited temporary slot-zero blend");
            drawBlend_=std::move(prepared);entryBlend_=entry.blend;
        }
    }
    std::filesystem::path source_;
    SkyPatternProgramBank bank_;
    std::map<std::int32_t,Program> programs_;
    Ptr<ID3D11DepthStencilState> entryDepth_,drawDepth_;
    Ptr<ID3D11RasterizerState> entryRaster_,drawRaster_;
    Ptr<ID3D11BlendState> entryBlend_,drawBlend_;
};
} // namespace San14::Render
