// Original node97 and node137 inputs. Include after the native lab helpers.
#pragma once
#include "RefractionDepth.h"
#include "Interop/RefractionFilterInputs.h"

struct NativeRefractionColorTarget {
    ComPtr<ID3D11Texture2D> texture;
    ComPtr<ID3D11ShaderResourceView> view;
    std::string logicalKey;
};
struct NativeRefractionColors {
    NativeRefractionColorTarget scene97, blurredDepth;
};

// Only states touched by these finite screen draws. Original screen scopes restore
// their caller's framebuffer, viewport, shaders, Cull, depth test and blend.
struct NativeRefractionScreenScope {
    ID3D11DeviceContext* context;
    NativeSourceDepthState& logical;
    D3D11_DEPTH_STENCIL_DESC logicalEntry;
    ComPtr<ID3D11RasterizerState> raster;
    ComPtr<ID3D11BlendState> blend;
    ComPtr<ID3D11DepthStencilState> depth;
    ComPtr<ID3D11DepthStencilView> dsv;
    ComPtr<ID3D11RenderTargetView> targets[D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT];
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11HullShader> hs;
    ComPtr<ID3D11DomainShader> ds;
    ComPtr<ID3D11GeometryShader> gs;
    ComPtr<ID3D11InputLayout> layout;
    ComPtr<ID3D11Buffer> vertex, index, vertexCb, pixelCb;
    ComPtr<ID3D11ShaderResourceView> pixelView;
    ComPtr<ID3D11SamplerState> sampler;
    D3D11_PRIMITIVE_TOPOLOGY topology{};
    DXGI_FORMAT indexFormat{};
    UINT stride=0, vertexOffset=0, indexOffset=0, reference=0, sampleMask=0;
    FLOAT factor[4]{};
    UINT viewportCount=D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE;
    D3D11_VIEWPORT viewports[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{};
    explicit NativeRefractionScreenScope(ID3D11DeviceContext* c,NativeSourceDepthState& source):context(c),logical(source),logicalEntry(source.description) {
        c->RSGetState(&raster);c->RSGetViewports(&viewportCount,viewports);
        c->OMGetBlendState(&blend,factor,&sampleMask);c->OMGetDepthStencilState(&depth,&reference);
        ID3D11RenderTargetView* raw[D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT]{};
        c->OMGetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT,raw,&dsv);
        for(UINT i=0;i<D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT;++i)targets[i].Attach(raw[i]);
        c->VSGetShader(&vs,nullptr,nullptr);c->PSGetShader(&ps,nullptr,nullptr);
        c->HSGetShader(&hs,nullptr,nullptr);c->DSGetShader(&ds,nullptr,nullptr);c->GSGetShader(&gs,nullptr,nullptr);
        c->IAGetInputLayout(&layout);c->IAGetPrimitiveTopology(&topology);
        c->IAGetVertexBuffers(0,1,&vertex,&stride,&vertexOffset);c->IAGetIndexBuffer(&index,&indexFormat,&indexOffset);
        c->VSGetConstantBuffers(0,1,&vertexCb);c->PSGetConstantBuffers(0,1,&pixelCb);
        c->PSGetShaderResources(0,1,&pixelView);c->PSGetSamplers(0,1,&sampler);
    }
    ~NativeRefractionScreenScope() {
        ID3D11ShaderResourceView* empty=nullptr;context->PSSetShaderResources(0,1,&empty);
        ID3D11RenderTargetView* raw[D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT]{};
        for(UINT i=0;i<D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT;++i)raw[i]=targets[i].Get();
        context->OMSetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT,raw,dsv.Get());
        context->RSSetState(raster.Get());context->RSSetViewports(viewportCount,viewports);
        context->OMSetBlendState(blend.Get(),factor,sampleMask);logical.bind(context,depth.Get(),logicalEntry,reference);
        context->VSSetShader(vs.Get(),nullptr,0);context->PSSetShader(ps.Get(),nullptr,0);
        context->HSSetShader(hs.Get(),nullptr,0);context->DSSetShader(ds.Get(),nullptr,0);context->GSSetShader(gs.Get(),nullptr,0);
        context->IASetInputLayout(layout.Get());context->IASetPrimitiveTopology(topology);
        auto vb=vertex.Get();context->IASetVertexBuffers(0,1,&vb,&stride,&vertexOffset);
        context->IASetIndexBuffer(index.Get(),indexFormat,indexOffset);
        auto vc=vertexCb.Get(),pc=pixelCb.Get();context->VSSetConstantBuffers(0,1,&vc);context->PSSetConstantBuffers(0,1,&pc);
        auto sv=pixelView.Get();auto sm=sampler.Get();context->PSSetShaderResources(0,1,&sv);context->PSSetSamplers(0,1,&sm);
    }
};

static NativeRefractionColorTarget createRefractionColorTarget(ID3D11Device* device,
    UINT width,UINT height,UINT mips,DXGI_FORMAT resourceFormat,DXGI_FORMAT viewFormat) {
    NativeRefractionColorTarget out;D3D11_TEXTURE2D_DESC td{};
    td.Width=width;td.Height=height;td.MipLevels=mips;td.ArraySize=1;td.Format=resourceFormat;td.SampleDesc.Count=1;
    td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
    check(device->CreateTexture2D(&td,nullptr,&out.texture),"original refraction color target");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=viewFormat;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=mips;
    check(device->CreateShaderResourceView(out.texture.Get(),&sd,&out.view),"original refraction color SRV");
    return out;
}

struct NativePreparedRefractionScene97 {
    NativeRefractionColorTarget output;
    ComPtr<ID3D11Texture2D> hdr;
    ComPtr<ID3D11ShaderResourceView> input;
    ComPtr<ID3D11RenderTargetView> target;
    NativeRefractionPrograms programs;
    NativeRefractionGeometry geometry;
    NativeRefractionStateOverrides states;
};
static NativePreparedRefractionScene97 prepareNativeRefractionScene97(ID3D11Device* device,
    const fs::path& source,ID3D11Texture2D* hdr,DXGI_FORMAT hdrViewFormat,std::string_view sourceKey,
    const NativeRefractionTargetDeclaration& declared) {
    UINT resource,view,target,width,height;std::ifstream rows(source/"target.txt");rows>>resource>>view>>target>>width>>height;
    if(!rows)throw std::runtime_error("Original Scene97 target absent");
    requireNativeRefractionMedia(declared,resource,view,target,width,height,1);
    NativePreparedRefractionScene97 out;out.hdr=hdr;
    out.output=createRefractionColorTarget(device,declared.width,declared.height,1,DXGI_FORMAT(declared.resourceFormat),DXGI_FORMAT(declared.shaderFormat));
    out.output.logicalKey=declared.logicalKey;
    NativeRefractionConstantFiles constants;out.programs=prepareRefractionPrograms(device,source,constants);
    out.geometry=prepareRefractionGeometry(device,source,out.programs.vertexCode);
    requireRefractionCopyViewport(out.geometry.viewport,declared);
    out.states=prepareRefractionStateOverrides(device,source,true);
    D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=DXGI_FORMAT(declared.targetFormat);rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
    check(device->CreateRenderTargetView(out.output.texture.Get(),&rd,&out.target),"Scene97 original half-size RTV");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=hdrViewFormat;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
    check(device->CreateShaderResourceView(hdr,&sd,&out.input),"Scene97 persistent early HDR SRV");
    requireRefractionCopySourceOwner(source,out.input.Get(),sourceKey);return out;
}
// Call at node97 with the fixed Main HDR object's then-current contents.
static const NativeRefractionColorTarget& executeNativeRefractionScene97(NativePreparedRefractionScene97& prepared,
    ID3D11DeviceContext* context,NativeSourceDepthState& logical) {
    NativeRefractionScreenScope prior(context,logical);
    prepared.states.prepareRasterBlend(prior.raster.Get(),prior.blend.Get());
    D3D11_DEPTH_STENCIL_DESC ds=logical.description;ds.DepthEnable=FALSE;prepared.states.prepareDepth(ds);
    prepared.states.bindRasterBlend(context,prior.factor,prior.sampleMask);logical.bind(context,prepared.states.depth.Get(),ds,prior.reference);
    prepared.programs.bind(context);prepared.geometry.bind(context);
    auto target=prepared.target.Get();context->OMSetRenderTargets(1,&target,nullptr);
    auto input=prepared.input.Get();context->PSSetShaderResources(0,1,&input);
    context->DrawIndexed(prepared.geometry.indexCount,0,0);return prepared.output;
}

struct NativeRefractionFilterTarget {
    NativeRefractionColorTarget output;
    UINT width{},height{},mips{};
    DXGI_FORMAT view{},target{};
};
struct NativeRefractionFilterPass {
    std::string name;
    unsigned sourceIndex{};
    NativeRefractionPrograms programs;
    ComPtr<ID3D11ShaderResourceView> input;
    ComPtr<ID3D11RenderTargetView> target;
    UINT inputSlot{},vertexCount=3,startVertex=0;
    D3D11_VIEWPORT viewport{};
    D3D11_PRIMITIVE_TOPOLOGY topology=D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST;
};
struct NativeRefractionMipZero {
    bool direct{};
    NativeRefractionPrograms programs;
    NativeRefractionGeometry geometry;
    ComPtr<ID3D11RenderTargetView> target;
};
struct NativePreparedRefractionFilters137 {
    ComPtr<ID3D11Texture2D> hdr;
    ComPtr<ID3D11ShaderResourceView> hdrView,mainDepth;
    std::map<std::string,NativeRefractionFilterTarget> targets;
    std::vector<NativeRefractionFilterPass> passes;
    std::array<NativeRefractionColorTarget,2> specular;
    std::array<NativeRefractionMipZero,2> mipZero;
    NativeRefractionColorTarget blurredDepth;
    NativeRefractionStateOverrides states;
};
static NativePreparedRefractionFilters137 prepareNativeRefractionFilters137(ID3D11Device* device,
    const fs::path& source,ID3D11Texture2D* hdr,DXGI_FORMAT hdrViewFormat,ID3D11ShaderResourceView* mainDepthView,
    const NativeRefractionTargetDeclaration& halfDeclaration,const NativeRefractionTargetDeclaration& fullDeclaration,
    const NativeRefractionTargetDeclaration& blurredDeclaration) {
    NativePreparedRefractionFilters137 out;out.hdr=hdr;out.mainDepth=mainDepthView;
    const auto inputs=readNativeRefractionFilterInputs(source);
    for(const auto& [key,target]:inputs.targets) {
        auto image=createRefractionColorTarget(device,target.width,target.height,target.mips,
            DXGI_FORMAT(target.resourceFormat),DXGI_FORMAT(target.shaderFormat));
        image.logicalKey=key;
        out.targets.emplace(key,NativeRefractionFilterTarget{std::move(image),target.width,target.height,target.mips,
            DXGI_FORMAT(target.shaderFormat),DXGI_FORMAT(target.targetFormat)});
    }
    auto selected=[&](const NativeRefractionTargetDeclaration& declared)->const NativeRefractionFilterTarget& {
        auto found=out.targets.find(declared.logicalKey);
        if(found==out.targets.end())throw std::runtime_error("Refraction filter has no created target for the selected logical key");
        const auto& target=found->second;D3D11_TEXTURE2D_DESC texture{};target.output.texture->GetDesc(&texture);
        requireNativeRefractionMedia(declared,unsigned(texture.Format),unsigned(target.view),unsigned(target.target),
            target.width,target.height,texture.MipLevels);
        return target;
    };
    const std::array<const NativeRefractionFilterTarget*,2> specular{&selected(halfDeclaration),&selected(fullDeclaration)};
    out.blurredDepth=selected(blurredDeclaration).output;
    D3D11_TEXTURE2D_DESC hdrDesc{};hdr->GetDesc(&hdrDesc);
    D3D11_SHADER_RESOURCE_VIEW_DESC hd{};hd.Format=hdrViewFormat;hd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;hd.Texture2D.MipLevels=1;
    check(device->CreateShaderResourceView(hdr,&hd,&out.hdrView),"Refraction filter current HDR source view");
    NativeRefractionConstantFiles constants;
    for(unsigned side=0;side<2;++side) {
        const auto& target=*specular[side];out.specular[side]=target.output;
        D3D11_TEXTURE2D_DESC td{};target.output.texture->GetDesc(&td);
        auto& copy=out.mipZero[side];
        copy.direct=hdrDesc.Width==td.Width&&hdrDesc.Height==td.Height&&hdrDesc.Format==td.Format&&
            hdrViewFormat==target.view&&hdrDesc.SampleDesc.Count==1;
        if(!copy.direct) {
            const auto directory=source/("mip0-"+std::to_string(side));
            copy.programs=prepareRefractionPrograms(device,directory,constants);
            copy.geometry=prepareRefractionGeometry(device,directory,copy.programs.vertexCode);
            if(copy.geometry.viewport.Width!=float(td.Width)||copy.geometry.viewport.Height!=float(td.Height))
                throw std::runtime_error("Refraction stock copy viewport differs from its actual destination");
            D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=target.target;rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
            check(device->CreateRenderTargetView(target.output.texture.Get(),&rd,&copy.target),"Refraction sampled mip-zero target");
        }
    }
    out.states=prepareRefractionStateOverrides(device,source,true);
    for(const auto& route:inputs.routes) {
        NativeRefractionFilterPass pass;pass.name=route.directory;pass.sourceIndex=route.sourceIndex;pass.inputSlot=route.inputSlot;
        const auto directory=source/route.directory;
        const auto& destination=out.targets.at(route.targetKey);
        if(route.sourceKey==inputs.mainDepthKey) {
            if(!mainDepthView)throw std::runtime_error("Refraction filter Main depth owner is absent");
            pass.input=mainDepthView;
        } else {
            const auto& image=out.targets.at(route.sourceKey);
            D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=image.view;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;
            sd.Texture2D.MostDetailedMip=route.sourceMip;sd.Texture2D.MipLevels=route.mipCount;
            check(device->CreateShaderResourceView(image.output.texture.Get(),&sd,&pass.input),"Refraction Gaussian source mip view");
        }
        pass.programs=prepareRefractionPrograms(device,directory,constants);
        D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=destination.target;rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
        rd.Texture2D.MipSlice=route.targetMip;
        check(device->CreateRenderTargetView(destination.output.texture.Get(),&rd,&pass.target),"Refraction Gaussian destination mip view");
        pass.viewport=readRefractionViewport(directory);out.passes.push_back(std::move(pass));
    }
    return out;
}
// Mip zero borrows the then-current HDR attachment. The selected resolution
// owns a distinct sampled copy and Gaussian chain; depth filtering is shared.
static void executeNativeRefractionFilters137(NativePreparedRefractionFilters137& prepared,
    ID3D11DeviceContext* context,NativeSourceDepthState& logical,unsigned sourceIndex,NativeRefractionColors& out) {
    if(sourceIndex>1)throw std::runtime_error("Refraction specular source index");
    NativeRefractionScreenScope prior(context,logical);
    prepared.states.prepareRasterBlend(prior.raster.Get(),prior.blend.Get());
    prepared.states.bindRasterBlend(context,prior.factor,prior.sampleMask);
    context->OMSetRenderTargets(0,nullptr,nullptr);
    const auto& copy=prepared.mipZero[sourceIndex];
    if(copy.direct) context->CopySubresourceRegion(prepared.specular[sourceIndex].texture.Get(),0,0,0,0,prepared.hdr.Get(),0,nullptr);
    else {
        auto depth=logical.description;depth.DepthEnable=FALSE;prepared.states.prepareDepth(depth);
        logical.bind(context,prepared.states.depth.Get(),depth,prior.reference);
        copy.programs.bind(context);copy.geometry.bind(context);
        auto target=copy.target.Get();auto input=prepared.hdrView.Get();
        context->OMSetRenderTargets(1,&target,nullptr);context->PSSetShaderResources(0,1,&input);
        context->DrawIndexed(copy.geometry.indexCount,0,0);
        input=nullptr;context->PSSetShaderResources(0,1,&input);context->OMSetRenderTargets(0,nullptr,nullptr);
        logical.bind(context,prior.depth.Get(),prior.logicalEntry,prior.reference);
    }
    for(const auto& pass:prepared.passes) {
        if(pass.sourceIndex!=sourceIndex&&pass.sourceIndex!=2)continue;
        pass.programs.bind(context);auto target=pass.target.Get();auto input=pass.input.Get();
        context->OMSetRenderTargets(1,&target,nullptr);context->PSSetShaderResources(pass.inputSlot,1,&input);
        context->RSSetViewports(1,&pass.viewport);context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(pass.topology);
        context->Draw(pass.vertexCount,pass.startVertex);
        input=nullptr;context->PSSetShaderResources(pass.inputSlot,1,&input);context->OMSetRenderTargets(0,nullptr,nullptr);
    }
    out.blurredDepth=prepared.blurredDepth;
}
