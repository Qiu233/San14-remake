// Minimal original Model draw consumer. Include after terrain.cpp's
// check/load/buffer helpers. Original DXBC/streams/CBs/texels are loaded once.
#pragma once
#include <sstream>
#include <memory>
#include <d3d11_1.h>
#include "../../Runtime/PreparedFileInput.h"
#include "../../Device/ModelTextureMedia.h"
struct NativeModelDraw {
    std::string identity;
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11InputLayout> layout;
    std::vector<ComPtr<ID3D11Buffer>> vertexBuffers;
    std::vector<UINT> strides;
    ComPtr<ID3D11Buffer> indices,vsConstants,psConstants;
    std::array<ComPtr<ID3D11ShaderResourceView>,16> textures;
    std::array<ComPtr<ID3D11SamplerState>,16> samplers;
    // Shared current Tree2 resources belong to GroundDetails' existing wind owner.
    std::array<ComPtr<ID3D11ShaderResourceView>,16> vertexTextures;
    std::array<ComPtr<ID3D11SamplerState>,16> vertexSamplers;
    ComPtr<ID3D11RasterizerState> raster;
    ComPtr<ID3D11DepthStencilState> depth;
    UINT indexCount=0,indexStart=0,indexBits=16,topology=4,stencilReference=0;
    INT baseVertex=0;
    bool instanceStreams=false,vertexResourcesPresent=false;
    // Army's mutable CB values follow the retained physical draw, including
    // the original singular-normal branch. Immutable templates leave this empty.
    std::shared_ptr<std::vector<char>> retainedVs;
};
struct NativeModelInputs {
    std::vector<NativeModelDraw> draws;
    ComPtr<ID3D11BlendState> blend;
};
// A synchronous pass borrows session-owned GPU draws. The producer retains
// the physical owner and rebuilds this ordered list before drawing; no COM
// resource ownership is copied for every selected primitive.
struct NativeModelConstantWindow {
    ID3D11Buffer* buffer{}; // Borrowed from the current Base arena owner.
    UINT firstConstant{},constantCount{}; // Units of16 bytes, aligned to16 constants.
};
struct NativeModelDrawReference {
    const NativeModelDraw* resources{};
    NativeModelConstantWindow vertex,pixel;
    NativeModelDrawReference()=default;
    NativeModelDrawReference(const NativeModelDraw* draw) noexcept:resources(draw) {}
};
struct NativeModelConstantSubmission {
    ID3D11DeviceContext1* context{};
    const std::vector<NativeModelDrawReference>* draws{};
};
struct NativeModelDrawReferences {
    std::vector<NativeModelDrawReference> draws;
    ComPtr<ID3D11BlendState> blend;
    ID3D11DeviceContext1* constantContext{}; // Same synchronous owner as its entries.
};
inline const NativeModelDraw& nativeModelResource(const NativeModelDraw& draw) noexcept {return draw;}
inline const NativeModelDraw& nativeModelResource(const NativeModelDraw* draw) noexcept {return *draw;}
inline const NativeModelDraw& nativeModelResource(const NativeModelDrawReference& draw) noexcept {return *draw.resources;}
inline const NativeModelDrawReference* nativeModelWindows(const NativeModelDraw&) noexcept {return nullptr;}
inline const NativeModelDrawReference* nativeModelWindows(const NativeModelDraw*) noexcept {return nullptr;}
inline const NativeModelDrawReference* nativeModelWindows(const NativeModelDrawReference& draw) noexcept {return &draw;}
inline ID3D11DeviceContext1* nativeModelWindowContext(const NativeModelInputs&) noexcept {return nullptr;}
inline ID3D11DeviceContext1* nativeModelWindowContext(const NativeModelDrawReferences& inputs) noexcept {return inputs.constantContext;}
using San14::Render::loadNativeModelTexture;
static ComPtr<ID3D11InputLayout> loadNativeModelLayout(ID3D11Device* device, const fs::path& directory,
    const std::vector<char>& v, bool& instanceStreams) {
    ComPtr<ID3D11InputLayout> layout;
    struct SourceElement {std::string name;UINT index{},format{},slot{},offset{},inputClass{},stepRate{};};
    std::vector<SourceElement> sourceElements;NativePreparedInputFile elements(directory/"layout.txt");std::string line;
    while(std::getline(elements,line)) {
        if(line.empty())continue;
        SourceElement row;std::istringstream fields(line);
        if(!(fields>>row.name>>row.index>>row.format>>row.slot>>row.offset))
            throw std::runtime_error("Model input layout row incomplete");
        // Older rigid exports have five columns; original broadcast streams
        // carry their actual input class and frequency in columns six/seven.
        if(fields>>row.inputClass) {
            if(!(fields>>row.stepRate))throw std::runtime_error("Model input frequency absent");
        }
        instanceStreams|=row.inputClass==D3D11_INPUT_PER_INSTANCE_DATA;
        sourceElements.push_back(std::move(row));
    }
    // Original 96A3E0 matches the stored attributes first, then supplies
    // absent shader semantics by copying its first matched descriptor.
    // B90 keeps the secondary skin channels in its signature even when
    // the original bSkin2 gate is false. Preserve DXBC and the original VB.
    ComPtr<ID3D11ShaderReflection> reflection;
    check(D3DReflect(v.data(),v.size(),__uuidof(ID3D11ShaderReflection),
        reinterpret_cast<void**>(reflection.GetAddressOf())),"Model input signature");
    D3D11_SHADER_DESC signature{};reflection->GetDesc(&signature);
    std::vector<D3D11_SIGNATURE_PARAMETER_DESC> parameters(signature.InputParameters);
    for(UINT i=0;i<parameters.size();++i)reflection->GetInputParameterDesc(i,&parameters[i]);
    auto matched=std::find_if(sourceElements.begin(),sourceElements.end(),[&](const auto& element) {
        return std::any_of(parameters.begin(),parameters.end(),[&](const auto& p) {
            return element.name==p.SemanticName&&element.index==p.SemanticIndex;
        });
    });
    if(matched==sourceElements.end())throw std::runtime_error("Model has no matching source input attribute");
    const auto fallback=*matched;
    for(const auto& p:parameters) {
        if(p.SystemValueType!=D3D_NAME_UNDEFINED)continue;
        if(std::any_of(sourceElements.begin(),sourceElements.end(),[&](const auto& element) {
            return element.name==p.SemanticName&&element.index==p.SemanticIndex;
        }))continue;
        auto element=fallback;element.name=p.SemanticName;element.index=p.SemanticIndex;
        sourceElements.push_back(std::move(element));
    }
    std::vector<D3D11_INPUT_ELEMENT_DESC> descriptors;
    for(const auto& e:sourceElements)descriptors.push_back({e.name.c_str(),e.index,DXGI_FORMAT(e.format),e.slot,e.offset,D3D11_INPUT_CLASSIFICATION(e.inputClass),e.stepRate});
    check(device->CreateInputLayout(descriptors.data(),UINT(descriptors.size()),v.data(),v.size(),&layout),"Model source layout");
    return layout;
}
// Static media may be borrowed from the application generation. Opening-owned
// constants and depth/stencil metadata always remain in the caller's directory.
static fs::path nativeModelMediaDirectory(const fs::path& directory) {
    NativePreparedInputFile mediaReference(directory/"static-media-root.txt");
    if(mediaReference) {
        std::string source;
        if(!std::getline(mediaReference,source)||source.empty())
            throw std::runtime_error("Model static media reference absent");
        if(source.back()=='\r')source.pop_back();
        if(source.empty())throw std::runtime_error("Model static media reference empty");
        auto path=fs::u8path(source);
        return nativePreparedFilePath(path.is_absolute()?path:directory/path);
    }
    return directory;
}
static NativeModelDraw loadNativeModelDraw(ID3D11Device* device,const fs::path& directory,
    std::string identity,const NativeModelDraw* sharedGeometry=nullptr) {
    NativeModelDraw draw;draw.identity=std::move(identity);
    auto mediaDirectory=nativeModelMediaDirectory(directory);
    auto v=load(mediaDirectory/"vs.dxbc"),p=load(mediaDirectory/"ps.dxbc");
    check(device->CreateVertexShader(v.data(),v.size(),nullptr,&draw.vs),"Model original VS");
    check(device->CreatePixelShader(p.data(),p.size(),nullptr,&draw.ps),"Model original PS");
    draw.vsConstants=buffer(device,load(directory/"vs-b0.bin"),D3D11_BIND_CONSTANT_BUFFER);
    draw.psConstants=buffer(device,load(directory/"ps-b0.bin"),D3D11_BIND_CONSTANT_BUFFER);
    UINT slot,stride;std::string file;NativePreparedInputFile streams(mediaDirectory/"streams.txt");
    while(streams>>slot>>stride>>file) {
        if(slot!=draw.vertexBuffers.size())throw std::runtime_error("Model streams are not in source slot order");
        if(sharedGeometry){
            if(slot>=sharedGeometry->vertexBuffers.size()||sharedGeometry->strides.at(slot)!=stride)
                throw std::runtime_error("Selected material source geometry streams differ");
            draw.vertexBuffers.push_back(sharedGeometry->vertexBuffers[slot]);
        }else draw.vertexBuffers.push_back(buffer(device,load(mediaDirectory/file),D3D11_BIND_VERTEX_BUFFER));
        draw.strides.push_back(stride);
    }
    draw.layout=loadNativeModelLayout(device,mediaDirectory,v,draw.instanceStreams);
    UINT fill,cull,front,depthTest,depthWrite,depthCompare,stencilEnable;
    NativePreparedInputFile metadata(directory/"draw.txt");
    metadata>>draw.indexCount>>draw.indexStart>>draw.baseVertex>>draw.topology>>draw.indexBits>>file>>fill>>cull>>front;
    metadata>>depthTest>>depthWrite>>depthCompare>>stencilEnable;
    if(!metadata)throw std::runtime_error("Model draw source metadata incomplete");
    if(sharedGeometry){
        if(draw.vertexBuffers.size()!=sharedGeometry->vertexBuffers.size()||draw.indexCount!=sharedGeometry->indexCount||
            draw.indexStart!=sharedGeometry->indexStart||draw.baseVertex!=sharedGeometry->baseVertex||
            draw.topology!=sharedGeometry->topology||draw.indexBits!=sharedGeometry->indexBits)
            throw std::runtime_error("Selected material source geometry draw differs");
        draw.indices=sharedGeometry->indices;
    }else draw.indices=buffer(device,load(mediaDirectory/file),D3D11_BIND_INDEX_BUFFER);
    D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_MODE(fill);rs.CullMode=D3D11_CULL_MODE(cull);rs.FrontCounterClockwise=front;rs.DepthClipEnable=TRUE;
    check(device->CreateRasterizerState(&rs,&draw.raster),"Model source group raster");
    D3D11_DEPTH_STENCIL_DESC ds{};ds.DepthEnable=depthTest;ds.DepthWriteMask=depthWrite?D3D11_DEPTH_WRITE_MASK_ALL:D3D11_DEPTH_WRITE_MASK_ZERO;ds.DepthFunc=D3D11_COMPARISON_FUNC(depthCompare);
    ds.StencilEnable=stencilEnable;ds.StencilReadMask=ds.StencilWriteMask=255;
    ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=D3D11_STENCIL_OP_KEEP;ds.FrontFace.StencilPassOp=D3D11_STENCIL_OP_REPLACE;ds.FrontFace.StencilFunc=D3D11_COMPARISON_ALWAYS;ds.BackFace=ds.FrontFace;
    // The same source ordinary late dispatcher writes the current record reference.
    if(stencilEnable)metadata>>draw.stencilReference;
    check(device->CreateDepthStencilState(&ds,&draw.depth),"Model ordinary source depth/stencil");
    UINT resourceFormat,viewFormat;NativePreparedInputFile textures(mediaDirectory/"textures-native.txt");
    while(textures>>slot>>resourceFormat>>viewFormat>>file) {
        if(slot>=draw.textures.size())throw std::runtime_error("Model texture slot out of range");
        draw.textures[slot]=loadNativeModelTexture(device,mediaDirectory/file,DXGI_FORMAT(resourceFormat),DXGI_FORMAT(viewFormat));
    }
    NativePreparedInputFile samplers(mediaDirectory/"samplers-native.txt");
    while(samplers>>slot>>file) {
        if(slot>=draw.samplers.size())throw std::runtime_error("Model sampler slot out of range");
        auto bytes=load(mediaDirectory/file);if(bytes.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Model source sampler descriptor incomplete");
        D3D11_SAMPLER_DESC sd{};std::memcpy(&sd,bytes.data(),sizeof(sd));
        check(device->CreateSamplerState(&sd,&draw.samplers[slot]),"Model source sampler");
    }
    return draw;
}
static NativeModelInputs loadNativeModels(ID3D11Device* device,const fs::path& source) {
    NativeModelInputs result;
    NativePreparedInputFile list(source/"models.txt");if(!list)throw std::runtime_error("models.txt absent");
    std::string relative;
    while(list>>relative)result.draws.push_back(loadNativeModelDraw(device,source/relative,relative));
    D3D11_BLEND_DESC bd{};bd.IndependentBlendEnable=TRUE;
    for(int i=0;i<5;++i){bd.RenderTarget[i].BlendEnable=FALSE;bd.RenderTarget[i].RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;}
    check(device->CreateBlendState(&bd,&result.blend),"Model ordinary54 source disabled blend/all five RGBA");
    return result;
}
template<class ModelInputs>
static void drawNativeModels(ID3D11DeviceContext* context,const ModelInputs& inputs,
    ID3D11RenderTargetView* const* targets,ID3D11DepthStencilView* depthTarget,const D3D11_VIEWPORT& viewport) {
    context->OMSetRenderTargets(5,targets,depthTarget);context->RSSetViewports(1,&viewport);
    context->OMSetBlendState(inputs.blend.Get(),nullptr,~0u);
    context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
    bool vertexResourcesBound=false;
    auto* windowContext=nativeModelWindowContext(inputs);
    for(const auto& item:inputs.draws) {
        const auto& draw=nativeModelResource(item);
        std::vector<ID3D11Buffer*> buffers;for(const auto& b:draw.vertexBuffers)buffers.push_back(b.Get());
        std::vector<UINT> offsets(buffers.size(),0);
        context->IASetVertexBuffers(0,UINT(buffers.size()),buffers.data(),draw.strides.data(),offsets.data());
        context->IASetIndexBuffer(draw.indices.Get(),draw.indexBits==16?DXGI_FORMAT_R16_UINT:DXGI_FORMAT_R32_UINT,0);
        context->IASetInputLayout(draw.layout.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY(draw.topology));
        context->VSSetShader(draw.vs.Get(),nullptr,0);context->PSSetShader(draw.ps.Get(),nullptr,0);
        const auto* windows=nativeModelWindows(item);
        if(windows&&windows->vertex.buffer) {
            auto buffer=windows->vertex.buffer;
            windowContext->VSSetConstantBuffers1(0,1,&buffer,&windows->vertex.firstConstant,&windows->vertex.constantCount);
        } else {auto buffer=draw.vsConstants.Get();context->VSSetConstantBuffers(0,1,&buffer);}
        if(windows&&windows->pixel.buffer) {
            auto buffer=windows->pixel.buffer;
            windowContext->PSSetConstantBuffers1(0,1,&buffer,&windows->pixel.firstConstant,&windows->pixel.constantCount);
        } else {auto buffer=draw.psConstants.Get();context->PSSetConstantBuffers(0,1,&buffer);}
        std::array<ID3D11ShaderResourceView*,16> textures{};std::array<ID3D11SamplerState*,16> samplers{};
        for(size_t i=0;i<textures.size();++i){textures[i]=draw.textures[i].Get();samplers[i]=draw.samplers[i].Get();}
        context->PSSetShaderResources(0,UINT(textures.size()),textures.data());context->PSSetSamplers(0,UINT(samplers.size()),samplers.data());
        if(draw.vertexResourcesPresent) {
            for(size_t i=0;i<textures.size();++i){textures[i]=draw.vertexTextures[i].Get();samplers[i]=draw.vertexSamplers[i].Get();}
            context->VSSetShaderResources(0,UINT(textures.size()),textures.data());context->VSSetSamplers(0,UINT(samplers.size()),samplers.data());
            vertexResourcesBound=true;
        } else if(vertexResourcesBound) {
            std::array<ID3D11ShaderResourceView*,16> none{};context->VSSetShaderResources(0,UINT(none.size()),none.data());
            vertexResourcesBound=false;
        }
        context->RSSetState(draw.raster.Get());context->OMSetDepthStencilState(draw.depth.Get(),draw.stencilReference);
        if(draw.instanceStreams)context->DrawIndexedInstanced(draw.indexCount,1,draw.indexStart,draw.baseVertex,0);
        else context->DrawIndexed(draw.indexCount,draw.indexStart,draw.baseVertex);
    }
    std::array<ID3D11ShaderResourceView*,16> nullViews{};context->PSSetShaderResources(0,UINT(nullViews.size()),nullViews.data());
    if(vertexResourcesBound)context->VSSetShaderResources(0,UINT(nullViews.size()),nullViews.data());
    context->OMSetRenderTargets(0,nullptr,nullptr);
}
