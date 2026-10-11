// The Fog owner retains its Scene work image and actual selected programs.
// Current publication supplies semantic fields before the existing fullscreen draws.
#pragma once
#include <optional>
#include "ScreenSpaceFogAdmission.h"
#include "HeightFogInputs.h"
#include "HeightFogProgramInputs.h"
struct NativeMapFogConstant {
    bool vertex=false;UINT slot=0;
    ComPtr<ID3D11Buffer> buffer;
    std::vector<char> bytes;
    const source_height_fog_programs::StageLayout* description=nullptr;
};
struct NativeMapFogProgram {
    const source_height_fog_programs::Program* description=nullptr;
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11InputLayout> layout;
    ComPtr<ID3D11Buffer> vertices;
    std::vector<NativeMapFogConstant> constants;
    std::array<ID3D11ShaderResourceView*,32> inputs{};
};
struct NativeMapFogPass {
    source_ssfog_population::Gate populationGate;
    std::string name;UINT stride=0,count=0,topology=0,inputSlot=0;
    UINT enable=0,compare=0,reference=0,readMask=0;
    bool background=false;
    std::vector<char> vertexBytes;
    std::string semantic;
    UINT semanticIndex=0,positionFormat=0,positionOffset=0;
    NativeMapFogProgram program;
    D3D11_VIEWPORT viewport{};
    ComPtr<ID3D11DepthStencilState> drawDepth,exitDepth;
    D3D11_DEPTH_STENCIL_DESC drawDescription{},exitDescription{};
};
struct NativeMapFog {
    ComPtr<ID3D11Texture2D> hdr,scene,mask;
    ComPtr<ID3D11ShaderResourceView> sceneView,maskView,depthView,accelerationView;
    ComPtr<ID3D11SamplerState> maskSampler;
    source_height_fog_programs::Bank programs;
    San14::Render::HeightFogSettings initialHeight;
    ComPtr<ID3D11DepthStencilView> dsv;
    ComPtr<ID3D11RenderTargetView> target;
    std::vector<NativeMapFogPass> passes;
    ComPtr<ID3D11BlendState> blend,endBlend;
    ComPtr<ID3D11RasterizerState> raster;
    D3D11_BLEND_DESC blendDescription{},endBlendDescription{};
    D3D11_RASTERIZER_DESC rasterDescription{};
};
static NativeMapFogProgram prepareNativeMapFogProgram(ID3D11Device* device,const NativeMapFog& fog,
    const NativeMapFogPass& pass,const source_height_fog_programs::Program& selected,
    const fs::path* initialDirectory=nullptr) {
    NativeMapFogProgram result;result.description=&selected;
    const auto& vertex=fog.programs.bytecode.at(selected.vertex);
    const auto& pixel=fog.programs.bytecode.at(selected.pixel);
    check(device->CreateVertexShader(vertex.data(),vertex.size(),nullptr,&result.vs),"Height Fog selected VS");
    check(device->CreatePixelShader(pixel.data(),pixel.size(),nullptr,&result.ps),"Height Fog selected PS");
    D3D11_INPUT_ELEMENT_DESC element{pass.semantic.c_str(),pass.semanticIndex,DXGI_FORMAT(pass.positionFormat),
        pass.inputSlot,pass.positionOffset,D3D11_INPUT_PER_VERTEX_DATA,0};
    check(device->CreateInputLayout(&element,1,vertex.data(),vertex.size(),&result.layout),"Height Fog selected input layout");
    result.vertices=buffer(device,pass.vertexBytes,D3D11_BIND_VERTEX_BUFFER);
    std::map<std::pair<bool,UINT>,std::string> initialFiles;
    if(initialDirectory&&(selected.vertexLayout.slot>=0||selected.pixelLayout.slot>=0)) {
        std::ifstream rows(*initialDirectory/"cbuffers.txt");std::string stage,file;UINT slot;
        if(!rows)throw std::runtime_error("Height Fog initial constant bindings absent");
        while(rows>>stage>>slot>>file)initialFiles.emplace(std::make_pair(stage=="VS",slot),file);
    }
    auto make=[&](bool vertexStage,const source_height_fog_programs::StageLayout& layout) {
        if(layout.slot<0)return;
        NativeMapFogConstant constant;constant.vertex=vertexStage;constant.slot=UINT(layout.slot);
        constant.description=&layout;constant.bytes.resize(layout.bytes);
        if(initialDirectory) {
            auto found=initialFiles.find({vertexStage,constant.slot});
            if(found==initialFiles.end())throw std::runtime_error("Height Fog selected initial constant slot absent");
            constant.bytes=load(*initialDirectory/found->second);
            if(constant.bytes.size()!=layout.bytes)throw std::runtime_error("Height Fog initial constants differ from selected RDEF");
        }
        D3D11_BUFFER_DESC description{};description.ByteWidth=layout.bytes;
        description.Usage=D3D11_USAGE_DEFAULT;description.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=constant.bytes.data();
        check(device->CreateBuffer(&description,&initial,&constant.buffer),"Height Fog selected constant buffer");
        result.constants.push_back(std::move(constant));
    };
    make(true,selected.vertexLayout);make(false,selected.pixelLayout);
    for(const auto& binding:selected.textures) {
        ID3D11ShaderResourceView* resource=nullptr;
        switch(binding.first) {
            case source_height_fog_programs::TextureRole::Mask:resource=fog.maskView.Get();break;
            case source_height_fog_programs::TextureRole::Depth:resource=fog.depthView.Get();break;
            case source_height_fog_programs::TextureRole::SceneColor:resource=fog.sceneView.Get();break;
            case source_height_fog_programs::TextureRole::Acceleration:resource=fog.accelerationView.Get();break;
        }
        if(!resource)throw std::runtime_error("Height Fog selected texture owner absent");
        result.inputs.at(binding.second)=resource;
    }
    return result;
}
static NativeMapFog prepareNativeMapFog(ID3D11Device* device,const fs::path& source,
    ID3D11Texture2D* hdr,ID3D11ShaderResourceView* depthView,ID3D11ShaderResourceView* accelerationView,
    ID3D11DepthStencilView* dsv,UINT width,UINT height) {
    NativeMapFog fog;fog.hdr=hdr;fog.depthView=depthView;fog.accelerationView=accelerationView;fog.dsv=dsv;
    fog.programs=source_height_fog_programs::loadBank(source);
    fog.initialHeight=San14::Render::Interop::readHeightFogSettings(source/"initial-height.bin");
    fog.maskSampler=ambientSampler(device,source/"mask-sampler.bin");
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
    UINT retainedSrvFormat=0,retainedRtFormat=0,initialAdmitted=0;
    while(passes>>relative>>initialAdmitted) {
        if(initialAdmitted>1)throw std::runtime_error("Height Fog initial admission format");
        NativeMapFogPass pass;pass.name=relative;const fs::path directory=source/relative;
        pass.populationGate=source_ssfog_population::load(directory);pass.background=pass.populationGate.background!=0;
        UINT unusedWriteMask;std::ifstream stencil(directory/"stencil.txt");stencil>>pass.enable>>pass.compare>>pass.reference>>pass.readMask>>unusedWriteMask;
        if(!stencil)throw std::runtime_error("Source fog stencil absent");
        auto& vp=pass.viewport;std::ifstream viewport(directory/"viewport.txt");viewport>>vp.TopLeftX>>vp.TopLeftY>>vp.Width>>vp.Height>>vp.MinDepth>>vp.MaxDepth;
        std::ifstream geometry(directory/"geometry.txt");geometry>>pass.stride>>pass.count>>pass.topology;
        std::ifstream layoutSource(directory/"layout.txt");
        layoutSource>>pass.semantic>>pass.semanticIndex>>pass.positionFormat>>pass.inputSlot>>pass.positionOffset;
        if(!layoutSource)throw std::runtime_error("Source fog input layout absent");
        pass.vertexBytes=load(directory/"fullscreen-vb.bin");
        std::string key;UINT resource,srvFormat,rtFormat,w,h;std::ifstream target(directory/"target.txt");target>>key>>resource>>srvFormat>>rtFormat>>w>>h;
        if(!viewport||!geometry||!target||w!=width||h!=height)throw std::runtime_error("Source fog extent/geometry absent or mismatched");
        if(!fog.target){
            D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=DXGI_FORMAT(rtFormat);rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
            check(device->CreateRenderTargetView(hdr,&rd,&fog.target),"125 retained HDR target");
            view(fog.scene.Get(),DXGI_FORMAT(srvFormat),fog.sceneView);retainedSrvFormat=srvFormat;retainedRtFormat=rtFormat;
        }else if(srvFormat!=retainedSrvFormat||rtFormat!=retainedRtFormat)throw std::runtime_error("Source fog passes disagree on shared target views");
        if(initialAdmitted) {
            source_height_fog_programs::Key key;
            std::ifstream selection(directory/"selection.txt");
            selection>>key.feature>>key.background>>key.types[0]>>key.types[1]>>key.types[2];
            if(!selection||key.background!=UINT(pass.background))throw std::runtime_error("Height Fog initial selection absent");
            pass.program=prepareNativeMapFogProgram(device,fog,pass,fog.programs.select(key),&directory);
        }
        fog.passes.push_back(std::move(pass));
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
        if(!pass.program.description)throw std::runtime_error("Admitted Height Fog program was not prepared");
        // An absent stage explicitly unbinds its previous slots. A zero-byte
        // layout never creates a synthetic buffer or loads a residual file.
        std::array<ID3D11Buffer*,D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT> emptyConstants{};
        context->VSSetConstantBuffers(0,UINT(emptyConstants.size()),emptyConstants.data());
        context->PSSetConstantBuffers(0,UINT(emptyConstants.size()),emptyConstants.data());
        for(auto& constant:pass.program.constants) {
            auto raw=constant.buffer.Get();context->UpdateSubresource(raw,0,nullptr,constant.bytes.data(),0,0);
            if(constant.vertex)context->VSSetConstantBuffers(constant.slot,1,&raw);
            else context->PSSetConstantBuffers(constant.slot,1,&raw);
        }
        context->PSSetShaderResources(0,UINT(pass.program.inputs.size()),pass.program.inputs.data());
        for(UINT slot:pass.program.description->samplers){auto raw=fog.maskSampler.Get();context->PSSetSamplers(slot,1,&raw);}
        UINT exitReference=logical.reference;D3D11_DEPTH_STENCIL_DESC ds=logical.description;
        ds.DepthEnable=FALSE;ds.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;D3D11_DEPTH_STENCIL_DESC exit=ds;
        ds.StencilEnable=pass.enable;ds.StencilReadMask=BYTE(pass.readMask);ds.FrontFace.StencilFunc=D3D11_COMPARISON_FUNC(pass.compare);
        ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=ds.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;ds.BackFace=ds.FrontFace;
        if(!pass.drawDepth||std::memcmp(&ds,&pass.drawDescription,sizeof(ds))!=0){pass.drawDepth.Reset();check(device->CreateDepthStencilState(&ds,&pass.drawDepth),"125 retained per-record stencil");pass.drawDescription=ds;}
        context->IASetInputLayout(pass.program.layout.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY(pass.topology));
        auto vertexBuffer=pass.program.vertices.Get();UINT vertexOffset=0;context->IASetVertexBuffers(pass.inputSlot,1,&vertexBuffer,&pass.stride,&vertexOffset);
        context->VSSetShader(pass.program.vs.Get(),nullptr,0);context->PSSetShader(pass.program.ps.Get(),nullptr,0);context->GSSetShader(nullptr,nullptr,0);
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
