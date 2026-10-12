#pragma once
#include "ShadowReceiverResources.h"
#include "ShadowReceiverOperation.h"
#include "../Environment/SkyPatternPalette.h"
#include "../../Device/DepthState.h"

namespace San14::Render {
struct NativeSourceShadowReceiver {
    struct ConstantStage {ComPtr<ID3D11Buffer> buffer;std::vector<char> bytes;};
    struct Constants {ConstantStage vertex,pixel;};
    struct DepthPair {
        D3D11_COMPARISON_FUNC comparison;BYTE writeMask{},readMask{};UINT stencilFunction{};
        D3D11_DEPTH_STENCIL_DESC drawDescription{},exitDescription{};
        ComPtr<ID3D11DepthStencilState> draw,exit;
    };
    ShadowReceiverNode node;
    ShadowReceiverMaterial material;
    std::map<std::uint32_t,ShadowReceiverShaderState> shaderStates;
    std::map<std::pair<std::uint32_t,std::int32_t>,Constants> constants;
    std::vector<DepthPair> depths;
    ComPtr<ID3D11RasterizerState> rasterEntry,raster;
    ComPtr<ID3D11BlendState> blendEntry,drawBlend,exitBlend;
    bool rasterPrepared{},blendPrepared{};
    explicit NativeSourceShadowReceiver(const NativeShadowReceiverResources& resources,std::uint32_t index)
        :node(resources.node(index)) {
        for(const auto id:{node.primary,node.noSub})shaderStates.try_emplace(id,resources.provider(id).bank);
    }
    void preparePipeline(ID3D11Device* device,ID3D11RasterizerState* currentRaster,ID3D11BlendState* currentBlend) {
        if(!rasterPrepared||(currentRaster!=rasterEntry.Get()&&currentRaster!=raster.Get())) {
            D3D11_RASTERIZER_DESC description{};description.FillMode=D3D11_FILL_SOLID;
            description.CullMode=D3D11_CULL_BACK;description.DepthClipEnable=TRUE;
            if(currentRaster)currentRaster->GetDesc(&description);
            description.CullMode=D3D11_CULL_NONE;
            ComPtr<ID3D11RasterizerState> next;
            check(device->CreateRasterizerState(&description,&next),"Receiver inherited raster");
            raster=std::move(next);rasterEntry=currentRaster;rasterPrepared=true;
        }
        if(!blendPrepared||(currentBlend!=blendEntry.Get()&&currentBlend!=drawBlend.Get()&&currentBlend!=exitBlend.Get())) {
            D3D11_BLEND_DESC description{};
            for(auto& target:description.RenderTarget) {
                target.SrcBlend=target.SrcBlendAlpha=D3D11_BLEND_ONE;
                target.DestBlend=target.DestBlendAlpha=D3D11_BLEND_ZERO;
                target.BlendOp=target.BlendOpAlpha=D3D11_BLEND_OP_ADD;target.RenderTargetWriteMask=15;
            }
            if(currentBlend)currentBlend->GetDesc(&description);
            description.RenderTarget[0].BlendEnable=FALSE;
            description.RenderTarget[0].RenderTargetWriteMask=BYTE(node.colorMask);
            ComPtr<ID3D11BlendState> nextDraw,nextExit;
            check(device->CreateBlendState(&description,&nextDraw),"Receiver declared color writes");
            description.RenderTarget[0].RenderTargetWriteMask=15;
            check(device->CreateBlendState(&description,&nextExit),"Receiver exit color writes");
            drawBlend=std::move(nextDraw);exitBlend=std::move(nextExit);blendEntry=currentBlend;blendPrepared=true;
        }
    }
    DepthPair& depth(ID3D11Device* device,const D3D11_DEPTH_STENCIL_DESC& entry,std::uint32_t group) {
        const UINT function=node.process||group?D3D11_COMPARISON_EQUAL:D3D11_COMPARISON_NOT_EQUAL;
        const BYTE readMask=BYTE(node.process?(0xf0u|(~group&15u)):group?(~group&15u):255u);
        auto found=std::find_if(depths.begin(),depths.end(),[&](const auto& value) {
            return value.comparison==entry.DepthFunc&&value.writeMask==entry.StencilWriteMask&&
                value.readMask==readMask&&value.stencilFunction==function;
        });
        if(found!=depths.end())return *found;
        DepthPair pair{};pair.comparison=entry.DepthFunc;pair.writeMask=entry.StencilWriteMask;
        pair.readMask=readMask;pair.stencilFunction=function;
        auto description=entry;description.DepthEnable=FALSE;description.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;
        description.StencilEnable=TRUE;description.StencilReadMask=readMask;
        description.FrontFace.StencilFunc=D3D11_COMPARISON_FUNC(function);
        description.FrontFace.StencilFailOp=description.FrontFace.StencilDepthFailOp=
            description.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;
        description.BackFace=description.FrontFace;pair.drawDescription=description;
        check(device->CreateDepthStencilState(&description,&pair.draw),"Receiver group stencil");
        description.StencilEnable=FALSE;pair.exitDescription=description;
        check(device->CreateDepthStencilState(&description,&pair.exit),"Receiver exit stencil disabled");
        depths.push_back(std::move(pair));return depths.back();
    }
};

inline bool drawSourceShadowReceiver(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeSourceDepthState& logical,NativeSourceShadowReceiver& receiver,
    const NativeShadowReceiverResources& resources,std::uint32_t providerId,
    const ShadowReceiverProgramSelection& selection,ID3D11ShaderResourceView* shadow,
    ID3D11ShaderResourceView* depth,ID3D11ShaderResourceView* lit,ID3D11SamplerState* litSampler,
    std::uint32_t group) {
    const auto& program=selection.program;
    if(program.vertex<0||program.pixel<0)return false;
    const auto& provider=resources.provider(providerId);
    const auto& recipe=provider.recipes.at(program.recipe);
    if(!recipe.layout)throw std::runtime_error("Selected receiver recipe: "+recipe.unsupported);
    const auto& layout=*recipe.layout;
    std::vector<char> vertex(layout.vertex.bytes),pixel(layout.pixel.bytes);
    layout.vertex.pack(receiver.material,vertex);layout.pixel.pack(receiver.material,pixel);
    for(const auto& route:layout.textures) {
        switch(route.role) {
            case ShadowReceiverTextureRole::Shadow:if(!shadow)throw std::runtime_error("Receiver shadow view is unavailable");break;
            case ShadowReceiverTextureRole::Lit:if(!litSampler)throw std::runtime_error("Receiver actual Lit sampler is unavailable");break;
            case ShadowReceiverTextureRole::Depth:if(!depth)throw std::runtime_error("Receiver current depth mirror is unavailable");break;
            default:throw std::runtime_error("Selected receiver texture role needs its actual optional producer");
        }
    }
    auto& constants=receiver.constants[{providerId,program.recipe}];
    auto stage=[&](const ShadowReceiverStageLayout& stageLayout,
        NativeSourceShadowReceiver::ConstantStage& state,std::vector<char>& bytes,bool isVertex) {
        if(stageLayout.slot<0)return;
        if(!state.buffer)state.buffer=buffer(device,bytes,D3D11_BIND_CONSTANT_BUFFER);
        else if(state.bytes!=bytes)context->UpdateSubresource(state.buffer.Get(),0,nullptr,bytes.data(),0,0);
        state.bytes=std::move(bytes);auto cb=state.buffer.Get();
        if(isVertex)context->VSSetConstantBuffers(UINT(stageLayout.slot),1,&cb);
        else context->PSSetConstantBuffers(UINT(stageLayout.slot),1,&cb);
    };
    stage(layout.vertex,constants.vertex,vertex,true);stage(layout.pixel,constants.pixel,pixel,false);
    for(const auto& route:layout.textures) {
        auto view=route.role==ShadowReceiverTextureRole::Shadow?shadow:
            route.role==ShadowReceiverTextureRole::Lit?lit:depth;
        context->PSSetShaderResources(route.slot,1,&view);
    }
    for(const auto& route:layout.samplers) {
        ID3D11SamplerState* sampler{};
        switch(route.role) {
            case ShadowReceiverTextureRole::Shadow:sampler=resources.shadowSampler();break;
            case ShadowReceiverTextureRole::Lit:sampler=litSampler;break;
            case ShadowReceiverTextureRole::ShadowPointSampler:sampler=resources.pointSampler();break;
            default:throw std::runtime_error("Selected receiver sampler role needs its actual producer");
        }
        context->PSSetSamplers(route.slot,1,&sampler);
    }
    ComPtr<ID3D11RasterizerState> entryRaster;context->RSGetState(&entryRaster);
    ComPtr<ID3D11BlendState> entryBlend;FLOAT factors[4];UINT sampleMask;
    context->OMGetBlendState(&entryBlend,factors,&sampleMask);
    receiver.preparePipeline(device,entryRaster.Get(),entryBlend.Get());
    auto& state=receiver.depth(device,logical.description,group);
    context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    context->VSSetShader(provider.vertices.at(program.vertex).Get(),nullptr,0);
    context->PSSetShader(provider.pixels.at(program.pixel).Get(),nullptr,0);context->GSSetShader(nullptr,nullptr,0);
    context->RSSetState(receiver.raster.Get());context->OMSetBlendState(receiver.drawBlend.Get(),factors,sampleMask);
    logical.bind(context,state.draw.Get(),state.drawDescription,receiver.node.process);
    context->Draw(4,0);
    for(const auto& route:layout.textures) {ID3D11ShaderResourceView* empty{};context->PSSetShaderResources(route.slot,1,&empty);}
    logical.bind(context,state.exit.Get(),state.exitDescription,receiver.node.process);
    context->OMSetBlendState(receiver.exitBlend.Get(),factors,sampleMask);
    return true;
}

inline void executeSourceShadowReceiver(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeSourceDepthState& logical,NativeSourceShadowReceiver& receiver,
    const NativeShadowReceiverResources& resources,ID3D11ShaderResourceView* shadow,
    ID3D11ShaderResourceView* depth,std::uint32_t depthObject,const source_current_season::Frame& current,
    const PreparedMapView& view,const SkyPatternPalette& palette,float commonClock) {
    if(!(view.frame().activeGroups&0x9fffffffu)||(receiver.node.process&&!view.frame().oceanGroups))return;
    if(depthObject!=receiver.node.depthObject)throw std::runtime_error("Receiver depth declaration does not match its actual producer");
    const bool special=palette.atmosphere&&current.shadowReceiver.patternCameraBranch!=5;
    const auto providerId=(view.frame().subShadowPrimitiveCount&15)==0?
        receiver.node.noSub:receiver.node.primary;
    if(providerId==receiver.node.primary)
        throw std::runtime_error("Selected Primary receiver needs its actual optional SubShadow CPU owner");
    const auto& provider=resources.provider(providerId);
    auto& request=receiver.shaderStates.at(providerId);
    for(const auto index:prepareShadowReceiverRecords(current)) {
        const auto& light=current.lights[index];
        installShadowReceiverRecord(receiver.material,light,current.shadowReceiver);
        request.installRecordBias(light.receiverBias.scale);
        const auto feature=prepareShadowReceiverFeature(light,current,view);
        const auto sampling=special?PatternSampling{
            palette.selected?palette.selected->view.Get():resources.fallback().view.Get(),
            palette.selected?palette.selected->sampler.Get():resources.fallback().sampler.Get()}:
            resources.ordinaryLit(light.object,current.patternCamera.mergedOverrideEnabled);
        const auto groups=special?std::vector<ShadowReceiverPatternGroup>{{15,nullptr}}:
            prepareShadowReceiverPatternGroups(current.lights,index,receiver.node.process?
                view.frame().oceanGroups:view.frame().activeGroups&0x9fffffffu);
        for(const auto& group:groups) {
            const auto changed=special?installShadowReceiverPattern(receiver.material.pattern,current.shadowReceiver.patternScale,
                palette.selected?&palette.worldToPattern:nullptr):
                installOrdinaryPattern(receiver.material.pattern,current.patternCamera,
                    group.modifier?*group.modifier:PatternLightModifier{},resources.mergedPresent(),commonClock);
            const auto selected=provider.bank.select(feature,request.requested,request.committed);
            drawSourceShadowReceiver(device,context,logical,receiver,resources,providerId,selected,
                shadow,depth,sampling.view,sampling.sampler,group.mask);
            // Each normal group callback resets only its reached Pattern setters.
            // Fatal exceptions preserve the earlier partial writes.
            if(changed)receiver.material.pattern.reset();
        }
        receiver.material.receiverBias={0,1};request.finishRecord();
    }
}
} // namespace San14::Render
