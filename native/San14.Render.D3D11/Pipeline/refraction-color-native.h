// Original node97 and node137 inputs. Include after the native lab helpers.
#pragma once
#include "refraction-depth-native.h"

struct NativeRefractionColorTarget {
    ComPtr<ID3D11Texture2D> texture;
    ComPtr<ID3D11ShaderResourceView> view;
};
struct NativeRefractionColors {
    NativeRefractionColorTarget scene97, specular, blurredDepth;
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
    const fs::path& source,ID3D11Texture2D* hdr,DXGI_FORMAT hdrViewFormat) {
    UINT resource,view,target,width,height;std::ifstream rows(source/"target.txt");rows>>resource>>view>>target>>width>>height;
    if(!rows)throw std::runtime_error("Original Scene97 target absent");
    NativePreparedRefractionScene97 out;out.hdr=hdr;
    out.output=createRefractionColorTarget(device,width,height,1,DXGI_FORMAT(resource),DXGI_FORMAT(view));
    NativeRefractionConstantFiles constants;out.programs=prepareRefractionPrograms(device,source,constants);
    out.geometry=prepareRefractionGeometry(device,source,out.programs.vertexCode);
    out.states=prepareRefractionStateOverrides(device,source,true);
    D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=DXGI_FORMAT(target);rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
    check(device->CreateRenderTargetView(out.output.texture.Get(),&rd,&out.target),"Scene97 original half-size RTV");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=hdrViewFormat;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
    check(device->CreateShaderResourceView(hdr,&sd,&out.input),"Scene97 persistent early HDR SRV");return out;
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
    NativeRefractionPrograms programs;
    ComPtr<ID3D11ShaderResourceView> input;
    ComPtr<ID3D11RenderTargetView> target;
    UINT inputSlot{},vertexCount=3,startVertex=0;
    D3D11_VIEWPORT viewport{};
    D3D11_PRIMITIVE_TOPOLOGY topology=D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST;
};
struct NativePreparedRefractionFilters137 {
    ComPtr<ID3D11Texture2D> hdr;
    ComPtr<ID3D11ShaderResourceView> mainDepth;
    std::map<std::string,NativeRefractionFilterTarget> targets;
    std::vector<NativeRefractionFilterPass> passes;
    NativeRefractionColorTarget specular,blurredDepth;
    NativeRefractionStateOverrides states;
};
static NativePreparedRefractionFilters137 prepareNativeRefractionFilters137(ID3D11Device* device,
    const fs::path& source,ID3D11Texture2D* hdr,DXGI_FORMAT hdrViewFormat,ID3D11ShaderResourceView* mainDepthView) {
    NativePreparedRefractionFilters137 out;out.hdr=hdr;out.mainDepth=mainDepthView;
    std::ifstream rows(source/"targets.txt");std::string key;UINT width,height,mips,resource,view,target;
    if(!rows)throw std::runtime_error("Original137 target list absent");
    while(rows>>key>>width>>height>>mips>>resource>>view>>target) {
        out.targets.emplace(key,NativeRefractionFilterTarget{createRefractionColorTarget(device,width,height,mips,DXGI_FORMAT(resource),DXGI_FORMAT(view)),
            width,height,mips,DXGI_FORMAT(view),DXGI_FORMAT(target)});
    }
    if(!rows.eof())throw std::runtime_error("Invalid original137 target list");
    const auto& specular=out.targets.at("93637E41");const auto& blurred=out.targets.at("18C86C69");
    D3D11_TEXTURE2D_DESC hdrDesc{},specDesc{};hdr->GetDesc(&hdrDesc);specular.output.texture->GetDesc(&specDesc);
    if(hdrDesc.Width!=specular.width||hdrDesc.Height!=specular.height||hdrDesc.Format!=specDesc.Format||hdrViewFormat!=specular.view||hdrDesc.SampleDesc.Count!=1)
        throw std::runtime_error("Original137 mip0 requires matching persistent HDR attachment");
    if(specular.mips!=7)throw std::runtime_error("Original137 requires seven specular mips");
    out.specular=specular.output;out.blurredDepth=blurred.output;out.states=prepareRefractionStateOverrides(device,source,true);
    NativeRefractionConstantFiles constants;std::ifstream passes(source/"passes.txt");std::string relative;
    if(!passes)throw std::runtime_error("Original137 pass list absent");
    while(passes>>relative) {
        NativeRefractionFilterPass pass;pass.name=relative;auto directory=source/relative;
        UINT sourceMip,mipCount,targetMip;std::string sourceKey,targetKey;
        std::ifstream inputs(directory/"input.txt"),outputs(directory/"target.txt");
        inputs>>pass.inputSlot>>sourceKey>>sourceMip>>mipCount;outputs>>targetKey>>targetMip;
        if(!inputs||!outputs||pass.inputSlot!=0)throw std::runtime_error("Original137 source/target view absent");
        const auto& destination=out.targets.at(targetKey);
        if(targetMip>=destination.mips)throw std::runtime_error("Original137 target mip out of range");
        if(sourceKey=="E13DDB4F") {
            if(!mainDepthView||sourceMip!=0||mipCount!=1)throw std::runtime_error("Original137 Main D1 input absent");
            pass.input=mainDepthView;
        } else {
            const auto& selected=out.targets.at(sourceKey);
            if(sourceMip+mipCount>selected.mips)throw std::runtime_error("Original137 source mip out of range");
            D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=selected.view;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;
            sd.Texture2D.MostDetailedMip=sourceMip;sd.Texture2D.MipLevels=mipCount;
            check(device->CreateShaderResourceView(selected.output.texture.Get(),&sd,&pass.input),"Original137 explicit source mip SRV");
        }
        pass.programs=prepareRefractionPrograms(device,directory,constants);
        D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=destination.target;rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;rd.Texture2D.MipSlice=targetMip;
        check(device->CreateRenderTargetView(destination.output.texture.Get(),&rd,&pass.target),"Original137 explicit target mip RTV");
        pass.viewport=readRefractionViewport(directory);out.passes.push_back(std::move(pass));
    }
    if(out.passes.size()!=14)throw std::runtime_error("Original137 requires12 specular and2 depth draws");return out;
}
// After node123, before137: copy new HDR mip0, then execute all14 original draws.
static void executeNativeRefractionFilters137(NativePreparedRefractionFilters137& prepared,
    ID3D11DeviceContext* context,NativeSourceDepthState& logical,NativeRefractionColors& out) {
    NativeRefractionScreenScope prior(context,logical);
    prepared.states.prepareRasterBlend(prior.raster.Get(),prior.blend.Get());
    prepared.states.bindRasterBlend(context,prior.factor,prior.sampleMask);
    // No depth override in the original137 helper: inherit the logical entry.
    context->OMSetRenderTargets(0,nullptr,nullptr);
    context->CopySubresourceRegion(prepared.specular.texture.Get(),0,0,0,0,prepared.hdr.Get(),0,nullptr);
    for(const auto& pass:prepared.passes) {
        pass.programs.bind(context);auto target=pass.target.Get();auto input=pass.input.Get();
        context->OMSetRenderTargets(1,&target,nullptr);context->PSSetShaderResources(pass.inputSlot,1,&input);
        context->RSSetViewports(1,&pass.viewport);context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(pass.topology);
        context->Draw(pass.vertexCount,pass.startVertex);
        input=nullptr;context->PSSetShaderResources(pass.inputSlot,1,&input);context->OMSetRenderTargets(0,nullptr,nullptr);
    }
    out.specular=prepared.specular;out.blurredDepth=prepared.blurredDepth;
}

// Transitional single-use APIs. Persistent callers keep the prepared objects.
static NativeRefractionColorTarget produceRefractionScene97(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    const fs::path& source,ID3D11Texture2D* hdr,DXGI_FORMAT hdrViewFormat) {
    auto prepared=prepareNativeRefractionScene97(device,source,hdr,hdrViewFormat);
    return executeNativeRefractionScene97(prepared,context,logical);
}
static void produceRefractionFilters137(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    const fs::path& source,ID3D11Texture2D* hdr,DXGI_FORMAT hdrViewFormat,ID3D11ShaderResourceView* mainDepthView,NativeRefractionColors& out) {
    auto prepared=prepareNativeRefractionFilters137(device,source,hdr,hdrViewFormat,mainDepthView);
    executeNativeRefractionFilters137(prepared,context,logical,out);
}
