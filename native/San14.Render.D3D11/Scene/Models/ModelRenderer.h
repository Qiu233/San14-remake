// Minimal original Model draw consumer. Include after terrain.cpp's
// check/load/buffer helpers. Original DXBC/streams/CBs/texels are loaded once.
#pragma once
#include <sstream>
#include <memory>
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
    ComPtr<ID3D11RasterizerState> raster;
    ComPtr<ID3D11DepthStencilState> depth;
    UINT indexCount=0,indexStart=0,indexBits=16,topology=4,stencilReference=0;
    INT baseVertex=0;
    bool instanceStreams=false;
    // Army's mutable CB values follow the retained physical draw, including
    // the original singular-normal branch. Immutable templates leave this empty.
    std::shared_ptr<std::vector<char>> retainedVs;
};
struct NativeModelInputs {
    std::vector<NativeModelDraw> draws;
    ComPtr<ID3D11BlendState> blend;
};
static ComPtr<ID3D11ShaderResourceView> loadNativeModelTexture(ID3D11Device* device,
    const fs::path& path,DXGI_FORMAT resourceFormat,DXGI_FORMAT viewFormat) {
    std::ifstream f(path,std::ios::binary);if(!f)throw std::runtime_error(path.string());
    D3D11_TEXTURE2D_DESC td{};td.Width=u32(f);td.Height=u32(f);td.ArraySize=u32(f);td.MipLevels=u32(f);
    auto encodedFormat=DXGI_FORMAT(u32(f));if(encodedFormat!=viewFormat||td.ArraySize!=1)throw std::runtime_error("Model source texture view mismatch");
    td.Format=resourceFormat;td.SampleDesc.Count=1;td.Usage=D3D11_USAGE_IMMUTABLE;td.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    std::vector<std::vector<char>> storage(td.MipLevels);std::vector<D3D11_SUBRESOURCE_DATA> initial(td.MipLevels);
    for(UINT mip=0;mip<td.MipLevels;++mip) {
        initial[mip].SysMemPitch=u32(f);initial[mip].SysMemSlicePitch=u32(f);storage[mip].resize(u32(f));
        f.read(storage[mip].data(),storage[mip].size());initial[mip].pSysMem=storage[mip].data();
    }
    ComPtr<ID3D11Texture2D> texture;check(device->CreateTexture2D(&td,initial.data(),&texture),"Model original texture");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=viewFormat;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=td.MipLevels;
    ComPtr<ID3D11ShaderResourceView> view;check(device->CreateShaderResourceView(texture.Get(),&sd,&view),"Model source texture SRV");return view;
}
static NativeModelInputs loadNativeModels(ID3D11Device* device,const fs::path& source) {
    NativeModelInputs result;
    std::ifstream list(source/"models.txt");if(!list)throw std::runtime_error("models.txt absent");
    std::string relative;
    while(list>>relative) {
        fs::path directory=source/relative;NativeModelDraw draw;draw.identity=relative;
        auto v=load(directory/"vs.dxbc"),p=load(directory/"ps.dxbc");
        check(device->CreateVertexShader(v.data(),v.size(),nullptr,&draw.vs),"Model original VS");
        check(device->CreatePixelShader(p.data(),p.size(),nullptr,&draw.ps),"Model original PS");
        draw.vsConstants=buffer(device,load(directory/"vs-b0.bin"),D3D11_BIND_CONSTANT_BUFFER);
        draw.psConstants=buffer(device,load(directory/"ps-b0.bin"),D3D11_BIND_CONSTANT_BUFFER);
        UINT slot,stride;std::string file;std::ifstream streams(directory/"streams.txt");
        while(streams>>slot>>stride>>file) {
            if(slot!=draw.vertexBuffers.size())throw std::runtime_error("Model streams are not in source slot order");
            draw.vertexBuffers.push_back(buffer(device,load(directory/file),D3D11_BIND_VERTEX_BUFFER));draw.strides.push_back(stride);
        }
        struct SourceElement {std::string name;UINT index{},format{},slot{},offset{},inputClass{},stepRate{};};
        std::vector<SourceElement> sourceElements;std::ifstream elements(directory/"layout.txt");std::string line;
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
            draw.instanceStreams|=row.inputClass==D3D11_INPUT_PER_INSTANCE_DATA;
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
        check(device->CreateInputLayout(descriptors.data(),UINT(descriptors.size()),v.data(),v.size(),&draw.layout),"Model source layout");
        UINT fill,cull,front,depthTest,depthWrite,depthCompare,stencilEnable;
        std::ifstream metadata(directory/"draw.txt");
        metadata>>draw.indexCount>>draw.indexStart>>draw.baseVertex>>draw.topology>>draw.indexBits>>file>>fill>>cull>>front;
        metadata>>depthTest>>depthWrite>>depthCompare>>stencilEnable;
        if(!metadata)throw std::runtime_error("Model draw source metadata incomplete");
        draw.indices=buffer(device,load(directory/file),D3D11_BIND_INDEX_BUFFER);
        D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_MODE(fill);rs.CullMode=D3D11_CULL_MODE(cull);rs.FrontCounterClockwise=front;rs.DepthClipEnable=TRUE;
        check(device->CreateRasterizerState(&rs,&draw.raster),"Model source group raster");
        D3D11_DEPTH_STENCIL_DESC ds{};ds.DepthEnable=depthTest;ds.DepthWriteMask=depthWrite?D3D11_DEPTH_WRITE_MASK_ALL:D3D11_DEPTH_WRITE_MASK_ZERO;ds.DepthFunc=D3D11_COMPARISON_FUNC(depthCompare);
        ds.StencilEnable=stencilEnable;ds.StencilReadMask=ds.StencilWriteMask=255;
        ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=D3D11_STENCIL_OP_KEEP;ds.FrontFace.StencilPassOp=D3D11_STENCIL_OP_REPLACE;ds.FrontFace.StencilFunc=D3D11_COMPARISON_ALWAYS;ds.BackFace=ds.FrontFace;
        // The same source ordinary late dispatcher writes the current record reference.
        if(stencilEnable)metadata>>draw.stencilReference;
        check(device->CreateDepthStencilState(&ds,&draw.depth),"Model ordinary source depth/stencil");
        UINT resourceFormat,viewFormat;std::ifstream textures(directory/"textures-native.txt");
        while(textures>>slot>>resourceFormat>>viewFormat>>file) {
            if(slot>=draw.textures.size())throw std::runtime_error("Model texture slot out of range");
            draw.textures[slot]=loadNativeModelTexture(device,directory/file,DXGI_FORMAT(resourceFormat),DXGI_FORMAT(viewFormat));
        }
        std::ifstream samplers(directory/"samplers-native.txt");
        while(samplers>>slot>>file) {
            if(slot>=draw.samplers.size())throw std::runtime_error("Model sampler slot out of range");
            auto bytes=load(directory/file);if(bytes.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Model source sampler descriptor incomplete");
            D3D11_SAMPLER_DESC sd{};std::memcpy(&sd,bytes.data(),sizeof(sd));
            check(device->CreateSamplerState(&sd,&draw.samplers[slot]),"Model source sampler");
        }
        result.draws.push_back(std::move(draw));
    }
    D3D11_BLEND_DESC bd{};bd.IndependentBlendEnable=TRUE;
    for(int i=0;i<5;++i){bd.RenderTarget[i].BlendEnable=FALSE;bd.RenderTarget[i].RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;}
    check(device->CreateBlendState(&bd,&result.blend),"Model ordinary54 source disabled blend/all five RGBA");
    return result;
}
static void drawNativeModels(ID3D11DeviceContext* context,const NativeModelInputs& inputs,
    ID3D11RenderTargetView* const* targets,ID3D11DepthStencilView* depthTarget,const D3D11_VIEWPORT& viewport) {
    context->OMSetRenderTargets(5,targets,depthTarget);context->RSSetViewports(1,&viewport);
    context->OMSetBlendState(inputs.blend.Get(),nullptr,~0u);
    context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
    for(const auto& draw:inputs.draws) {
        std::vector<ID3D11Buffer*> buffers;for(const auto& b:draw.vertexBuffers)buffers.push_back(b.Get());
        std::vector<UINT> offsets(buffers.size(),0);
        context->IASetVertexBuffers(0,UINT(buffers.size()),buffers.data(),draw.strides.data(),offsets.data());
        context->IASetIndexBuffer(draw.indices.Get(),draw.indexBits==16?DXGI_FORMAT_R16_UINT:DXGI_FORMAT_R32_UINT,0);
        context->IASetInputLayout(draw.layout.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY(draw.topology));
        context->VSSetShader(draw.vs.Get(),nullptr,0);context->PSSetShader(draw.ps.Get(),nullptr,0);
        auto vc=draw.vsConstants.Get(),pc=draw.psConstants.Get();context->VSSetConstantBuffers(0,1,&vc);context->PSSetConstantBuffers(0,1,&pc);
        std::array<ID3D11ShaderResourceView*,16> textures{};std::array<ID3D11SamplerState*,16> samplers{};
        for(size_t i=0;i<textures.size();++i){textures[i]=draw.textures[i].Get();samplers[i]=draw.samplers[i].Get();}
        context->PSSetShaderResources(0,UINT(textures.size()),textures.data());context->PSSetSamplers(0,UINT(samplers.size()),samplers.data());
        context->RSSetState(draw.raster.Get());context->OMSetDepthStencilState(draw.depth.Get(),draw.stencilReference);
        if(draw.instanceStreams)context->DrawIndexedInstanced(draw.indexCount,1,draw.indexStart,draw.baseVertex,0);
        else context->DrawIndexed(draw.indexCount,draw.indexStart,draw.baseVertex);
        std::cout<<"Model original "<<draw.identity<<" indices="<<draw.indexCount<<" sourceStart="<<draw.indexStart<<"\n";
    }
    std::array<ID3D11ShaderResourceView*,16> nullViews{};context->PSSetShaderResources(0,UINT(nullViews.size()),nullViews.data());
    context->OMSetRenderTargets(0,nullptr,nullptr);
}
