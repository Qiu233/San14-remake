// Source StaticNature125 ScreenSpaceFog. Prepared resources retain one fixed
// SourceMain generation; each execute copies current Scene and advances24 phases.
#pragma once
#include <optional>
#include "ScreenSpaceFogAdmission.h"
struct NativeMapFogPhase {UINT offset=0;float velocity=0;};
struct NativeMapFogConstant {
    bool vertex=false;UINT slot=0;
    ComPtr<ID3D11Buffer> buffer;
    std::vector<char> bytes;
    std::vector<NativeMapFogPhase> phases;
};
struct NativeMapFogPass {
    source_ssfog_population::Gate populationGate;
    std::string name;UINT stride=0,count=0,topology=0,inputSlot=0;
    UINT enable=0,compare=0,reference=0,readMask=0;
    bool background=false;
    ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11InputLayout> layout;ComPtr<ID3D11Buffer> vertices;
    std::vector<NativeMapFogConstant> constants;
    std::vector<std::pair<UINT,ComPtr<ID3D11SamplerState>>> samplers;
    std::array<ID3D11ShaderResourceView*,3> inputs{};
    D3D11_VIEWPORT viewport{};
    ComPtr<ID3D11DepthStencilState> drawDepth,exitDepth;
    D3D11_DEPTH_STENCIL_DESC drawDescription{},exitDescription{};
};
struct NativeMapFog {
    ComPtr<ID3D11Texture2D> hdr,scene,mask;
    ComPtr<ID3D11ShaderResourceView> sceneView,maskView,depthView;
    ComPtr<ID3D11DepthStencilView> dsv;
    ComPtr<ID3D11RenderTargetView> target;
    std::vector<NativeMapFogPass> passes;
    ComPtr<ID3D11BlendState> blend,endBlend;
    ComPtr<ID3D11RasterizerState> raster;
    D3D11_BLEND_DESC blendDescription{},endBlendDescription{};
    D3D11_RASTERIZER_DESC rasterDescription{};
};
// Match the already closed998EE0 hourly phase reduction: every arithmetic
// intermediate is float32; the signed fractional reduction itself is double.
__declspec(noinline) static float nativeMapFogMul(float a,float b){return a*b;}
__declspec(noinline) static float nativeMapFogAdd(float a,float b){return a+b;}
__declspec(noinline) static float nativeMapFogSub(float a,float b){return a-b;}
__declspec(noinline) static float nativeMapFogDiv(float a,float b){return a/b;}
static float nativeMapFogFraction(float value){return float(double(value)-std::trunc(double(value)));}
static float nativeMapFogPhase(float velocity,float currentCommonClock) {
    float cycles=std::floor(nativeMapFogDiv(currentCommonClock,3600.f));
    float local=nativeMapFogSub(currentCommonClock,nativeMapFogMul(cycles,3600.f));
    float fraction=nativeMapFogFraction(nativeMapFogMul(3600.f,velocity));
    return nativeMapFogFraction(nativeMapFogAdd(nativeMapFogMul(velocity,local),nativeMapFogMul(fraction,cycles)));
}
static void updateNativeMapFogPhases(NativeMapFogConstant& constant,float currentCommonClock) {
    for(const auto& phase:constant.phases){float value=nativeMapFogPhase(phase.velocity,currentCommonClock);std::memcpy(constant.bytes.data()+phase.offset,&value,4);}
}
static NativeMapFog prepareNativeMapFog(ID3D11Device* device,const fs::path& source,
    ID3D11Texture2D* hdr,ID3D11ShaderResourceView* depthView,ID3D11DepthStencilView* dsv,UINT width,UINT height) {
    NativeMapFog fog;fog.hdr=hdr;fog.depthView=depthView;fog.dsv=dsv;
    D3D11_TEXTURE2D_DESC sceneDesc{};hdr->GetDesc(&sceneDesc);sceneDesc.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    check(device->CreateTexture2D(&sceneDesc,nullptr,&fog.scene),"125 retained SceneBuffer6EE83296");
    fog.mask=loadTexture(device,source/"height-fog-mask.tex");
    UINT maskFormat;std::ifstream maskMeta(source/"mask-view-format.txt");maskMeta>>maskFormat;
    if(!maskMeta)throw std::runtime_error("Source fog mask view format absent");
    auto view=[&](ID3D11Texture2D* texture,DXGI_FORMAT format,ComPtr<ID3D11ShaderResourceView>& result) {
        D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=format;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
        check(device->CreateShaderResourceView(texture,&sd,&result),"125 retained source SRV");
    };
    view(fog.mask.Get(),DXGI_FORMAT(maskFormat),fog.maskView);
    std::ifstream passes(source/"passes.txt");std::string relative;if(!passes)throw std::runtime_error("Source fog pass list absent");
    UINT retainedSrvFormat=0,retainedRtFormat=0;
    while(passes>>relative) {
        NativeMapFogPass pass;pass.name=relative;const fs::path directory=source/relative;
        pass.populationGate=source_ssfog_population::load(directory);pass.background=pass.populationGate.background!=0;
        auto vb=load(directory/"vs.dxbc"),pb=load(directory/"ps.dxbc");
        check(device->CreateVertexShader(vb.data(),vb.size(),nullptr,&pass.vs),"125 original VS");
        check(device->CreatePixelShader(pb.data(),pb.size(),nullptr,&pass.ps),"125 original PS");
        std::ifstream phaseRows(directory/"phase-velocities.txt");std::vector<NativeMapFogPhase> phases;NativeMapFogPhase phase;
        if(!phaseRows)throw std::runtime_error("Source fog phase velocities absent");
        while(phaseRows>>phase.offset>>phase.velocity)phases.push_back(phase);
        std::ifstream cbRows(directory/"cbuffers.txt");std::string stage,file;UINT slot;
        if(!cbRows)throw std::runtime_error("Source fog constants absent");
        while(cbRows>>stage>>slot>>file){NativeMapFogConstant constant;constant.vertex=stage=="VS";constant.slot=slot;constant.bytes=load(directory/file);
            if(constant.vertex)constant.buffer=buffer(device,constant.bytes,D3D11_BIND_CONSTANT_BUFFER);
            else {
                constant.phases=phases;
                for(const auto& p:phases)if(p.offset+4>constant.bytes.size())throw std::runtime_error("Source fog phase outside original CB");
                D3D11_BUFFER_DESC d{};d.ByteWidth=UINT(constant.bytes.size());d.Usage=D3D11_USAGE_DEFAULT;d.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
                D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=constant.bytes.data();check(device->CreateBuffer(&d,&initial,&constant.buffer),"125 retained time constants");
            }
            pass.constants.push_back(std::move(constant));
        }
        std::ifstream textures(directory/"textures.txt");std::string role;
        if(!textures)throw std::runtime_error("Source fog texture roles absent");
        while(textures>>slot>>role){if(role=="HeightFogMask")pass.inputs.at(slot)=fog.maskView.Get();else if(role=="SceneDepth")pass.inputs.at(slot)=depthView;
            else if(role=="SceneColor")pass.inputs.at(slot)=nullptr;else throw std::runtime_error("Unknown source fog role: "+role);}
        std::ifstream samplerRows(directory/"samplers.txt");if(!samplerRows)throw std::runtime_error("Source fog samplers absent");
        while(samplerRows>>slot>>file){auto bytes=load(directory/file);D3D11_SAMPLER_DESC desc{};
            if(bytes.size()!=sizeof(desc))throw std::runtime_error("Source fog sampler requires52B");std::memcpy(&desc,bytes.data(),sizeof(desc));
            ComPtr<ID3D11SamplerState> sampler;check(device->CreateSamplerState(&desc,&sampler),"125 retained source sampler");pass.samplers.emplace_back(slot,std::move(sampler));}
        UINT unusedWriteMask;std::ifstream stencil(directory/"stencil.txt");stencil>>pass.enable>>pass.compare>>pass.reference>>pass.readMask>>unusedWriteMask;
        if(!stencil)throw std::runtime_error("Source fog stencil absent");
        auto& vp=pass.viewport;std::ifstream viewport(directory/"viewport.txt");viewport>>vp.TopLeftX>>vp.TopLeftY>>vp.Width>>vp.Height>>vp.MinDepth>>vp.MaxDepth;
        std::ifstream geometry(directory/"geometry.txt");geometry>>pass.stride>>pass.count>>pass.topology;
        std::string semantic;UINT semanticIndex,positionFormat,positionOffset;
        std::ifstream layoutSource(directory/"layout.txt");layoutSource>>semantic>>semanticIndex>>positionFormat>>pass.inputSlot>>positionOffset;
        if(!layoutSource)throw std::runtime_error("Source fog input layout absent");
        D3D11_INPUT_ELEMENT_DESC element{semantic.c_str(),semanticIndex,DXGI_FORMAT(positionFormat),pass.inputSlot,positionOffset,D3D11_INPUT_PER_VERTEX_DATA,0};
        check(device->CreateInputLayout(&element,1,vb.data(),vb.size(),&pass.layout),"125 original POSITION layout");
        pass.vertices=buffer(device,load(directory/"fullscreen-vb.bin"),D3D11_BIND_VERTEX_BUFFER);
        std::string key;UINT resource,srvFormat,rtFormat,w,h;std::ifstream target(directory/"target.txt");target>>key>>resource>>srvFormat>>rtFormat>>w>>h;
        if(!viewport||!geometry||!target||w!=width||h!=height)throw std::runtime_error("Source fog extent/geometry absent or mismatched");
        if(!fog.target){
            D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=DXGI_FORMAT(rtFormat);rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
            check(device->CreateRenderTargetView(hdr,&rd,&fog.target),"125 retained HDR target");
            view(fog.scene.Get(),DXGI_FORMAT(srvFormat),fog.sceneView);retainedSrvFormat=srvFormat;retainedRtFormat=rtFormat;
        }else if(srvFormat!=retainedSrvFormat||rtFormat!=retainedRtFormat)throw std::runtime_error("Source fog passes disagree on shared target views");
        // The source packet's three selected slots are fixed t0/t1/t2.
        pass.inputs.at(2)=fog.sceneView.Get();fog.passes.push_back(std::move(pass));
    }
    return fog;
}
static void executeNativeMapFog(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeSourceDepthState& logical,NativeMapFog& fog,float currentCommonClock,
    std::optional<unsigned> currentView19AC=std::nullopt) {
    context->OMSetRenderTargets(0,nullptr,nullptr);std::array<ID3D11ShaderResourceView*,32> empty{};
    context->PSSetShaderResources(0,UINT(empty.size()),empty.data());context->CopyResource(fog.scene.Get(),fog.hdr.Get());
    ComPtr<ID3D11BlendState> entryBlend;float entryFactor[4];UINT entrySampleMask;
    context->OMGetBlendState(&entryBlend,entryFactor,&entrySampleMask);
    D3D11_BLEND_DESC bd{};bd.RenderTarget[0].RenderTargetWriteMask=15;if(entryBlend)entryBlend->GetDesc(&bd);
    BYTE entryAlpha=bd.RenderTarget[0].RenderTargetWriteMask&8;bd.RenderTarget[0].BlendEnable=FALSE;bd.RenderTarget[0].RenderTargetWriteMask=7;
    if(!fog.blend||std::memcmp(&bd,&fog.blendDescription,sizeof(bd))!=0){fog.blend.Reset();check(device->CreateBlendState(&bd,&fog.blend),"125 retained RGB-only blend off");fog.blendDescription=bd;}
    ComPtr<ID3D11RasterizerState> entryRaster;context->RSGetState(&entryRaster);
    D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.DepthClipEnable=TRUE;if(entryRaster)entryRaster->GetDesc(&rs);rs.CullMode=D3D11_CULL_NONE;
    if(!fog.raster||std::memcmp(&rs,&fog.rasterDescription,sizeof(rs))!=0){fog.raster.Reset();check(device->CreateRasterizerState(&rs,&fog.raster),"125 retained CullNONE");fog.rasterDescription=rs;}
    unsigned submitted=0;
    for(auto& pass:fog.passes) {
        // A legacy static snapshot retains its imported cohort. Normal Main
        // supplies the same request's actual View mask, including zero.
        if(currentView19AC&&!source_ssfog_population::admitted(pass.populationGate,*currentView19AC))continue;
        for(auto& constant:pass.constants){auto raw=constant.buffer.Get();
            if(constant.vertex)context->VSSetConstantBuffers(constant.slot,1,&raw);
            else {updateNativeMapFogPhases(constant,currentCommonClock);context->UpdateSubresource(raw,0,nullptr,constant.bytes.data(),0,0);context->PSSetConstantBuffers(constant.slot,1,&raw);}}
        context->PSSetShaderResources(0,UINT(pass.inputs.size()),pass.inputs.data());
        for(auto& sampler:pass.samplers){auto raw=sampler.second.Get();context->PSSetSamplers(sampler.first,1,&raw);}
        UINT exitReference=logical.reference;D3D11_DEPTH_STENCIL_DESC ds=logical.description;
        ds.DepthEnable=FALSE;ds.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;D3D11_DEPTH_STENCIL_DESC exit=ds;
        ds.StencilEnable=pass.enable;ds.StencilReadMask=BYTE(pass.readMask);ds.FrontFace.StencilFunc=D3D11_COMPARISON_FUNC(pass.compare);
        ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=ds.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;ds.BackFace=ds.FrontFace;
        if(!pass.drawDepth||std::memcmp(&ds,&pass.drawDescription,sizeof(ds))!=0){pass.drawDepth.Reset();check(device->CreateDepthStencilState(&ds,&pass.drawDepth),"125 retained per-record stencil");pass.drawDescription=ds;}
        context->IASetInputLayout(pass.layout.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY(pass.topology));
        auto vertexBuffer=pass.vertices.Get();UINT vertexOffset=0;context->IASetVertexBuffers(pass.inputSlot,1,&vertexBuffer,&pass.stride,&vertexOffset);
        context->VSSetShader(pass.vs.Get(),nullptr,0);context->PSSetShader(pass.ps.Get(),nullptr,0);context->GSSetShader(nullptr,nullptr,0);
        context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
        context->RSSetState(fog.raster.Get());context->RSSetViewports(1,&pass.viewport);context->OMSetBlendState(fog.blend.Get(),nullptr,~0u);
        logical.bind(context,pass.drawDepth.Get(),ds,pass.reference);auto raw=fog.target.Get();context->OMSetRenderTargets(1,&raw,fog.dsv.Get());context->Draw(pass.count,0);++submitted;
        context->OMSetRenderTargets(0,nullptr,nullptr);context->PSSetShaderResources(0,UINT(empty.size()),empty.data());
        // Per-record restores its entry stencil only. Background disables
        // stencil; neither path restores depth testing/writes or CullNONE.
        if(pass.background){exit=ds;exit.StencilEnable=FALSE;exitReference=pass.reference;}
        if(!pass.exitDepth||std::memcmp(&exit,&pass.exitDescription,sizeof(exit))!=0){pass.exitDepth.Reset();check(device->CreateDepthStencilState(&exit,&pass.exitDepth),"125 retained source stencil exit");pass.exitDescription=exit;}
        logical.bind(context,pass.exitDepth.Get(),exit,exitReference);
        std::cout<<"source mapFog125 "<<pass.name<<" draw="<<pass.count<<" stencil="<<pass.reference<<'/'<<pass.readMask<<" RGB7 no-clear\n";
    }
    bd.RenderTarget[0].RenderTargetWriteMask=BYTE(7|entryAlpha);
    if(!fog.endBlend||std::memcmp(&bd,&fog.endBlendDescription,sizeof(bd))!=0){fog.endBlend.Reset();check(device->CreateBlendState(&bd,&fog.endBlend),"125 retained alpha-write restore");fog.endBlendDescription=bd;}
    context->OMSetBlendState(fog.endBlend.Get(),nullptr,~0u);
    std::cout<<"source mapFog125 draws="<<submitted<<" retained SceneBuffer copy, source height-layer clock="<<currentCommonClock<<'\n';
}
static void drawNativeMapFog(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    const fs::path& source,ID3D11Texture2D* hdr,ID3D11ShaderResourceView* depthView,
    ID3D11DepthStencilView* dsv,UINT width,UINT height,float currentCommonClock=.016f) {
    auto fog=prepareNativeMapFog(device,source,hdr,depthView,dsv,width,height);
    executeNativeMapFog(device,context,logical,fog,currentCommonClock);
}
