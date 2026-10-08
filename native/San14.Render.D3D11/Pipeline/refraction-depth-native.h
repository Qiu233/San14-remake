// Original node97: then-current Main D1 -> the distinct readable D32 target.
// Include after ComPtr/fs/load/buffer/check and NativeSourceDepthState.
#pragma once
#include <map>

struct NativeRefractionDepth {
    ComPtr<ID3D11Texture2D> texture;
    ComPtr<ID3D11DepthStencilView> depth;
    ComPtr<ID3D11ShaderResourceView> view;
};

struct NativeRefractionConstantBinding {
    std::string stage;
    UINT slot{};
    ComPtr<ID3D11Buffer> buffer; // DEFAULT: a source CB writer may update it later.
};
struct NativeRefractionSamplerBinding {
    UINT slot{};
    ComPtr<ID3D11SamplerState> sampler;
};
struct NativeRefractionPrograms {
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    std::vector<char> vertexCode; // Used by the one-time original input layout.
    std::vector<NativeRefractionConstantBinding> constants;
    std::vector<NativeRefractionSamplerBinding> samplers;
    void bind(ID3D11DeviceContext* context) const {
        context->VSSetShader(vs.Get(),nullptr,0);context->PSSetShader(ps.Get(),nullptr,0);
        context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
        for(const auto& binding:constants) {
            auto raw=binding.buffer.Get();
            if(binding.stage=="VS")context->VSSetConstantBuffers(binding.slot,1,&raw);
            else context->PSSetConstantBuffers(binding.slot,1,&raw);
        }
        for(const auto& binding:samplers) {
            auto raw=binding.sampler.Get();context->PSSetSamplers(binding.slot,1,&raw);
        }
    }
};

// Preparation-local reuse by the existing file identity, without content hashes.
struct NativeRefractionConstantFiles {
    std::map<fs::path,ComPtr<ID3D11Buffer>> buffers;
    ComPtr<ID3D11Buffer> get(ID3D11Device* device,const fs::path& file) {
        auto key=fs::absolute(file).lexically_normal();auto found=buffers.find(key);
        if(found!=buffers.end())return found->second;
        auto bytes=load(key);D3D11_BUFFER_DESC desc{};desc.ByteWidth=UINT(bytes.size());
        desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=bytes.data();ComPtr<ID3D11Buffer> result;
        check(device->CreateBuffer(&desc,&initial,&result),"original refraction CB");
        buffers.emplace(key,result);return result;
    }
};
static NativeRefractionPrograms prepareRefractionPrograms(ID3D11Device* device,const fs::path& source,
    NativeRefractionConstantFiles& constants,bool depthCopy=false) {
    NativeRefractionPrograms out;out.vertexCode=load(source/"vs.dxbc");auto pixelCode=load(source/"ps.dxbc");
    check(device->CreateVertexShader(out.vertexCode.data(),out.vertexCode.size(),nullptr,&out.vs),"original refraction VS");
    check(device->CreatePixelShader(pixelCode.data(),pixelCode.size(),nullptr,&out.ps),"original refraction PS");
    if(depthCopy)out.constants.push_back({"VS",0,constants.get(device,source/"vs-b0.bin")});
    else {
        std::ifstream cb(source/"cbuffers.txt"),sm(source/"samplers.txt");std::string stage,file;UINT slot;
        if(!cb||!sm)throw std::runtime_error("Original refraction CB/sampler list absent");
        while(cb>>stage>>slot>>file) {
            if(slot!=0||(stage!="VS"&&stage!="PS"))throw std::runtime_error("Unexpected refraction CB register");
            out.constants.push_back({stage,slot,constants.get(device,source/file)});
        }
        while(sm>>slot>>file) {
            if(slot!=0)throw std::runtime_error("Unexpected refraction sampler register");
            auto bytes=load(source/file);D3D11_SAMPLER_DESC desc{};
            if(bytes.size()!=sizeof(desc))throw std::runtime_error("Original refraction sampler requires52B");
            std::memcpy(&desc,bytes.data(),sizeof(desc));ComPtr<ID3D11SamplerState> sampler;
            check(device->CreateSamplerState(&desc,&sampler),"original refraction sampler");
            out.samplers.push_back({slot,sampler});
        }
        if(!cb.eof()||!sm.eof())throw std::runtime_error("Invalid refraction CB/sampler list");
    }
    return out;
}
static D3D11_VIEWPORT readRefractionViewport(const fs::path& source) {
    D3D11_VIEWPORT v{};std::ifstream rows(source/"viewport.txt");rows>>v.TopLeftX>>v.TopLeftY>>v.Width>>v.Height>>v.MinDepth>>v.MaxDepth;
    if(!rows)throw std::runtime_error("Original refraction viewport absent");return v;
}
struct NativeRefractionGeometry {
    ComPtr<ID3D11Buffer> vertex,index;
    ComPtr<ID3D11InputLayout> layout;
    UINT stride{},indexCount{};
    DXGI_FORMAT indexFormat{};
    D3D11_PRIMITIVE_TOPOLOGY topology{};
    D3D11_VIEWPORT viewport{};
    void bind(ID3D11DeviceContext* context) const {
        auto raw=vertex.Get();UINT offset=0;context->IASetVertexBuffers(0,1,&raw,&stride,&offset);
        context->IASetIndexBuffer(index.Get(),indexFormat,0);context->IASetInputLayout(layout.Get());
        context->IASetPrimitiveTopology(topology);context->RSSetViewports(1,&viewport);
    }
};
static NativeRefractionGeometry prepareRefractionGeometry(ID3D11Device* device,const fs::path& source,
    const std::vector<char>& vertexCode) {
    NativeRefractionGeometry out;out.vertex=buffer(device,load(source/"vertices.bin"),D3D11_BIND_VERTEX_BUFFER);
    out.index=buffer(device,load(source/"indices.bin"),D3D11_BIND_INDEX_BUFFER);
    UINT bits,topology;std::ifstream geometry(source/"geometry.txt");geometry>>out.stride>>out.indexCount>>bits>>topology;
    if(!geometry||(bits!=16&&bits!=32))throw std::runtime_error("Original refraction geometry absent");
    out.indexFormat=bits==16?DXGI_FORMAT_R16_UINT:DXGI_FORMAT_R32_UINT;out.topology=D3D11_PRIMITIVE_TOPOLOGY(topology);
    struct Element {std::string name;UINT index,format,slot,offset;};Element row;std::vector<Element> elements;
    std::ifstream file(source/"layout.txt");while(file>>row.name>>row.index>>row.format>>row.slot>>row.offset)elements.push_back(row);
    if(!file.eof()||elements.empty())throw std::runtime_error("Original refraction input layout absent");
    std::vector<D3D11_INPUT_ELEMENT_DESC> descriptors;
    for(const auto& e:elements)descriptors.push_back({e.name.c_str(),e.index,DXGI_FORMAT(e.format),e.slot,e.offset,D3D11_INPUT_PER_VERTEX_DATA,0});
    check(device->CreateInputLayout(descriptors.data(),UINT(descriptors.size()),vertexCode.data(),vertexCode.size(),&out.layout),"original refraction input layout");
    out.viewport=readRefractionViewport(source);return out;
}

static bool sameRefractionStencilFace(const D3D11_DEPTH_STENCILOP_DESC& a,const D3D11_DEPTH_STENCILOP_DESC& b) {
    return a.StencilFailOp==b.StencilFailOp&&a.StencilDepthFailOp==b.StencilDepthFailOp&&a.StencilPassOp==b.StencilPassOp&&a.StencilFunc==b.StencilFunc;
}
static bool sameRefractionDepthDescription(const D3D11_DEPTH_STENCIL_DESC& a,const D3D11_DEPTH_STENCIL_DESC& b) {
    return a.DepthEnable==b.DepthEnable&&a.DepthWriteMask==b.DepthWriteMask&&a.DepthFunc==b.DepthFunc&&a.StencilEnable==b.StencilEnable&&
        a.StencilReadMask==b.StencilReadMask&&a.StencilWriteMask==b.StencilWriteMask&&
        sameRefractionStencilFace(a.FrontFace,b.FrontFace)&&sameRefractionStencilFace(a.BackFace,b.BackFace);
}

// Only the inherited state fields need the real execution entry. Cache the
// temporary override until that actual state changes; no per-request files or textures.
struct NativeRefractionStateOverrides {
    ComPtr<ID3D11Device> device;
    UINT cull{},blendEnabled{};
    bool colorDefaults=false,rasterReady=false,blendReady=false,depthReady=false;
    ComPtr<ID3D11RasterizerState> rasterEntry,raster;
    ComPtr<ID3D11BlendState> blendEntry,blend;
    ComPtr<ID3D11DepthStencilState> depth;
    D3D11_DEPTH_STENCIL_DESC depthDescription{};
    void prepareRasterBlend(ID3D11RasterizerState* entryRaster,ID3D11BlendState* entryBlend) {
        if(!rasterReady||rasterEntry.Get()!=entryRaster) {
            D3D11_RASTERIZER_DESC desc{};desc.FillMode=D3D11_FILL_SOLID;desc.DepthClipEnable=TRUE;
            if(colorDefaults)desc.CullMode=D3D11_CULL_BACK;
            if(entryRaster)entryRaster->GetDesc(&desc);desc.CullMode=D3D11_CULL_MODE(cull);
            check(device->CreateRasterizerState(&desc,raster.ReleaseAndGetAddressOf()),"original refraction temporary Cull");
            rasterEntry=entryRaster;rasterReady=true;
        }
        if(!blendReady||blendEntry.Get()!=entryBlend) {
            D3D11_BLEND_DESC desc{};
            if(colorDefaults)for(auto& rt:desc.RenderTarget) {
                rt.SrcBlend=rt.SrcBlendAlpha=D3D11_BLEND_ONE;rt.DestBlend=rt.DestBlendAlpha=D3D11_BLEND_ZERO;
                rt.BlendOp=rt.BlendOpAlpha=D3D11_BLEND_OP_ADD;rt.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
            }
            if(entryBlend)entryBlend->GetDesc(&desc);desc.RenderTarget[0].BlendEnable=blendEnabled;
            check(device->CreateBlendState(&desc,blend.ReleaseAndGetAddressOf()),"original refraction slot0 blend");
            blendEntry=entryBlend;blendReady=true;
        }
    }
    void prepareDepth(const D3D11_DEPTH_STENCIL_DESC& requested) {
        if(!depthReady||!sameRefractionDepthDescription(depthDescription,requested)) {
            check(device->CreateDepthStencilState(&requested,depth.ReleaseAndGetAddressOf()),"original refraction depth override");
            depthDescription=requested;depthReady=true;
        }
    }
    void bindRasterBlend(ID3D11DeviceContext* context,const FLOAT* factor,UINT sampleMask) const {
        context->RSSetState(raster.Get());context->OMSetBlendState(blend.Get(),factor,sampleMask);
    }
};
static NativeRefractionStateOverrides prepareRefractionStateOverrides(ID3D11Device* device,const fs::path& source,bool colorDefaults) {
    NativeRefractionStateOverrides out;out.device=device;out.colorDefaults=colorDefaults;
    std::ifstream raster(source/"raster-cull.txt"),blend(source/"blend-enabled.txt");raster>>out.cull;blend>>out.blendEnabled;
    if(!raster||!blend)throw std::runtime_error("Original refraction raster/blend absent");return out;
}

struct NativePreparedRefractionDepth {
    NativeRefractionDepth output;
    ComPtr<ID3D11ShaderResourceView> input;
    NativeRefractionPrograms programs;
    NativeRefractionGeometry geometry;
    NativeRefractionStateOverrides states;
    ComPtr<ID3D11DepthStencilState> exitDepth;
    D3D11_DEPTH_STENCIL_DESC exitDescription{};
    bool exitDepthReady=false;
    UINT depthTest{},depthWrite{};
    D3D11_COMPARISON_FUNC depthCompare{};
};
static NativePreparedRefractionDepth prepareNativeRefractionDepth(ID3D11Device* device,
    const fs::path& source,ID3D11ShaderResourceView* input,UINT width,UINT height) {
    UINT resource,shader,target,sourceWidth,sourceHeight;std::ifstream file(source/"target.txt");
    file>>resource>>shader>>target>>sourceWidth>>sourceHeight;
    if(!file||width!=sourceWidth||height!=sourceHeight)throw std::runtime_error("Re-export node97 for the Main extent");
    NativePreparedRefractionDepth out;out.input=input;
    D3D11_TEXTURE2D_DESC td{};td.Width=width;td.Height=height;td.ArraySize=td.MipLevels=1;
    td.Format=DXGI_FORMAT(resource);td.SampleDesc.Count=1;td.BindFlags=D3D11_BIND_DEPTH_STENCIL|D3D11_BIND_SHADER_RESOURCE;
    check(device->CreateTexture2D(&td,nullptr,&out.output.texture),"node97 original target");
    D3D11_DEPTH_STENCIL_VIEW_DESC dd{};dd.Format=DXGI_FORMAT(target);dd.ViewDimension=D3D11_DSV_DIMENSION_TEXTURE2D;
    check(device->CreateDepthStencilView(out.output.texture.Get(),&dd,&out.output.depth),"node97 original DSV");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT(shader);sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
    check(device->CreateShaderResourceView(out.output.texture.Get(),&sd,&out.output.view),"node97 original SRV");
    NativeRefractionConstantFiles constants;out.programs=prepareRefractionPrograms(device,source,constants,true);
    out.geometry=prepareRefractionGeometry(device,source,out.programs.vertexCode);
    UINT compare;std::ifstream depth(source/"depth.txt");depth>>out.depthTest>>out.depthWrite>>compare;
    if(!depth)throw std::runtime_error("node97 source depth absent");out.depthCompare=D3D11_COMPARISON_FUNC(compare);
    out.states=prepareRefractionStateOverrides(device,source,false);return out;
}
static const NativeRefractionDepth& executeNativeRefractionDepth(NativePreparedRefractionDepth& prepared,
    ID3D11DeviceContext* context,NativeSourceDepthState& logical) {
    ComPtr<ID3D11RasterizerState> priorRaster;ComPtr<ID3D11BlendState> priorBlend;
    context->RSGetState(&priorRaster);FLOAT factor[4]{};UINT sampleMask;
    context->OMGetBlendState(&priorBlend,factor,&sampleMask);
    ComPtr<ID3D11DepthStencilState> inherited;UINT reference=0;context->OMGetDepthStencilState(&inherited,&reference);
    const D3D11_DEPTH_STENCIL_DESC entry=logical.description;
    D3D11_DEPTH_STENCIL_DESC ds=entry;ds.DepthEnable=prepared.depthTest;
    ds.DepthWriteMask=prepared.depthWrite?D3D11_DEPTH_WRITE_MASK_ALL:D3D11_DEPTH_WRITE_MASK_ZERO;ds.DepthFunc=prepared.depthCompare;
    prepared.states.prepareRasterBlend(priorRaster.Get(),priorBlend.Get());prepared.states.prepareDepth(ds);
    // CE E9E6F0 restores saved Host+138 after 95A7B0 restores entry1BF.
    // The explicit write1D8=1 persists; stencil fields are never overridden.
    D3D11_DEPTH_STENCIL_DESC exit=entry;exit.DepthWriteMask=ds.DepthWriteMask;
    if(!prepared.exitDepthReady||!sameRefractionDepthDescription(prepared.exitDescription,exit)) {
        check(prepared.states.device->CreateDepthStencilState(&exit,prepared.exitDepth.ReleaseAndGetAddressOf()),"node97 source depth exit");
        prepared.exitDescription=exit;prepared.exitDepthReady=true;
    }
    prepared.programs.bind(context);prepared.geometry.bind(context);
    context->OMSetRenderTargets(0,nullptr,prepared.output.depth.Get());auto input=prepared.input.Get();context->PSSetShaderResources(0,1,&input);
    logical.bind(context,prepared.states.depth.Get(),ds,reference);prepared.states.bindRasterBlend(context,factor,sampleMask);
    context->DrawIndexed(prepared.geometry.indexCount,0,0);context->OMSetRenderTargets(0,nullptr,nullptr);
    input=nullptr;context->PSSetShaderResources(0,1,&input);
    // Source screen end restores Cull/test; E9E6F0 also restores compare.
    // Retain write1 and the former blend/viewport/IA effects.
    context->RSSetState(priorRaster.Get());logical.bind(context,prepared.exitDepth.Get(),exit,reference);
    return prepared.output;
}

// Transitional single-use API. A persistent caller owns prepare+execute instead.
static NativeRefractionDepth produceRefractionDepth(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    const fs::path& source,ID3D11ShaderResourceView* input,UINT width,UINT height) {
    auto prepared=prepareNativeRefractionDepth(device,source,input,width,height);
    return executeNativeRefractionDepth(prepared,context,logical);
}
