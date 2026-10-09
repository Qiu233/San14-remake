// Fresh normal Main source DOF -> Bloom -> exposure -> tone -> FXAA.
// Include after terrain.cpp's actual helpers. The input is the finite CPU export,
// not an arbitrary render graph. Original DXBC/CBs are used unchanged.
#pragma once
#include "post-native.h"
#include <map>
#include <sstream>
#include <functional>
#include "normal-post-passes-native.h"

struct NormalPostSurface {
    ComPtr<ID3D11Texture2D> texture;
    std::vector<ComPtr<ID3D11ShaderResourceView>> views;
    std::vector<ComPtr<ID3D11RenderTargetView>> targets;
    ComPtr<ID3D11UnorderedAccessView> uav;
    UINT width{},height{};
    DXGI_FORMAT sampledFormat{};
};
struct NormalPostBuffer {
    ComPtr<ID3D11Buffer> buffer;
    ComPtr<ID3D11ShaderResourceView> view;
    ComPtr<ID3D11UnorderedAccessView> uav;
};
struct NativeNormalPost {
    std::map<std::string,NormalPostSurface> surfaces;
    std::map<std::string,NormalPostBuffer> buffers;
    std::map<std::string,ComPtr<ID3D11ShaderResourceView>> externalViews;
    std::map<std::string,std::vector<NormalPostSurface>> textureSides;
    std::map<std::string,std::vector<NormalPostBuffer>> bufferSides;
    std::map<std::string,UINT> selectors;
    std::array<ComPtr<ID3D11Buffer>,2> spriteStaging;
    NormalPostSurface exposure;
    UINT width{},height{};
};
static NormalPostSurface normalPostSurface(ID3D11Device* device,UINT w,UINT h,UINT mips,
    UINT resource,UINT shader,UINT render,UINT unordered,bool hasRt,bool hasUav,
    const D3D11_SUBRESOURCE_DATA* initial=nullptr) {
    D3D11_TEXTURE2D_DESC d{};d.Width=w;d.Height=h;d.MipLevels=mips;d.ArraySize=1;
    d.Format=DXGI_FORMAT(resource);d.SampleDesc.Count=1;d.Usage=D3D11_USAGE_DEFAULT;
    d.BindFlags=D3D11_BIND_SHADER_RESOURCE|(hasRt?D3D11_BIND_RENDER_TARGET:0)|(hasUav?D3D11_BIND_UNORDERED_ACCESS:0);
    NormalPostSurface out;out.width=w;out.height=h;out.sampledFormat=DXGI_FORMAT(shader);
    check(device->CreateTexture2D(&d,initial,&out.texture),"normal post source texture");
    for(UINT mip=0;mip<mips;++mip) {
        D3D11_SHADER_RESOURCE_VIEW_DESC s{};s.Format=DXGI_FORMAT(shader);s.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;
        s.Texture2D.MostDetailedMip=mip;s.Texture2D.MipLevels=1;
        ComPtr<ID3D11ShaderResourceView> view;check(device->CreateShaderResourceView(out.texture.Get(),&s,&view),"normal post single-mip SRV");out.views.push_back(view);
        if(hasRt) {
            D3D11_RENDER_TARGET_VIEW_DESC r{};r.Format=DXGI_FORMAT(render);r.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;r.Texture2D.MipSlice=mip;
            ComPtr<ID3D11RenderTargetView> target;check(device->CreateRenderTargetView(out.texture.Get(),&r,&target),"normal post source mip RTV");out.targets.push_back(target);
        }
    }
    if(hasUav) {
        D3D11_UNORDERED_ACCESS_VIEW_DESC u{};u.Format=DXGI_FORMAT(unordered);u.ViewDimension=D3D11_UAV_DIMENSION_TEXTURE2D;
        check(device->CreateUnorderedAccessView(out.texture.Get(),&u,&out.uav),"normal post source texture UAV");
    }
    return out;
}
static void normalPostUnbind(ID3D11DeviceContext* context) {
    std::array<ID3D11ShaderResourceView*,16> empty{};
    context->VSSetShaderResources(0,UINT(empty.size()),empty.data());
    context->PSSetShaderResources(0,UINT(empty.size()),empty.data());
    context->CSSetShaderResources(0,UINT(empty.size()),empty.data());
    std::array<ID3D11UnorderedAccessView*,8> noUav{};
    context->CSSetUnorderedAccessViews(0,UINT(noUav.size()),noUav.data(),nullptr);
    context->OMSetRenderTargets(0,nullptr,nullptr);
}

static NormalPostSurface createNormalPostFinalOutput(ID3D11Device* device,const fs::path& source,UINT width,UINT height) {
    UINT resource,sampled,rendered,w,h;std::ifstream row(source/"109-fxaa/target.txt");
    if(!(row>>resource>>sampled>>rendered>>w>>h)||w!=width||h!=height)
        throw std::runtime_error("Normal caller Output13BF extent differs");
    return normalPostSurface(device,w,h,1,resource,sampled,rendered,0,true,false);
}

static NormalPostSurface prepareNormalPostExposure(ID3D11Device* device,const fs::path& source) {
    // Controlled prior history already used by this experiment. Native fresh
    // creation itself is data-null; this does not claim its initial value.
    UINT er,es,eu,ew,eh;std::ifstream exposureRow(source/"exposure.txt");
    if(!(exposureRow>>er>>es>>eu>>ew>>eh)||ew!=2||eh!=1)throw std::runtime_error("Normal post Exposure61 descriptor missing");
    const std::array<float,2> previousExposure{1.f,1.f};D3D11_SUBRESOURCE_DATA prior{};
    prior.pSysMem=previousExposure.data();prior.SysMemPitch=prior.SysMemSlicePitch=8;
    return normalPostSurface(device,ew,eh,1,er,es,es,eu,true,true,&prior);
}
static NativeNormalPost prepareNormalPost(ID3D11Device* device,
    const fs::path& source,ID3D11ShaderResourceView* sceneView,
    ID3D11ShaderResourceView* mainDepthView,const NormalPostSurface& callerOutput,UINT width,UINT height,
    const NormalPostSurface* retainedExposure=nullptr) {
    if(!sceneView||!mainDepthView)throw std::runtime_error("Normal post needs actual same Main Scene and GBuffer depth SRVs");
    NativeNormalPost state;state.width=width;state.height=height;
    auto& surfaces=state.surfaces;auto& buffers=state.buffers;
    auto& externalViews=state.externalViews;auto& spriteStaging=state.spriteStaging;
    // Actual114AB40 paired declarations. Unused module parameters are not
    // fabricated as GPU owners; retain only bindings reached by this slice.
    std::map<UINT,UINT> pairedBuffers;
    std::ifstream pairs(source/"history-pairs.txt");std::string category,sourceName;UINT parameter,inputSide,endSide;
    if(!pairs)throw std::runtime_error("Normal post source history catalogue absent");
    while(pairs>>category>>parameter>>inputSide>>endSide>>sourceName)
        if(category=="BufferParameters")pairedBuffers.emplace(parameter,inputSide);
    UINT sceneResourceFormat,sceneShaderFormat,sceneRenderFormat,sw,sh;
    std::ifstream sceneRow(source/"lighting-target.txt");
    if(!(sceneRow>>sceneResourceFormat>>sceneShaderFormat>>sceneRenderFormat>>sw>>sh)||sw!=width||sh!=height)
        throw std::runtime_error("Normal post source Scene descriptor missing or extent differs");
    NormalPostSurface scene;scene.width=width;scene.height=height;scene.sampledFormat=DXGI_FORMAT(sceneShaderFormat);
    ComPtr<ID3D11Resource> actualSceneResource;sceneView->GetResource(&actualSceneResource);
    check(actualSceneResource.As(&scene.texture),"normal post actual Scene texture");
    D3D11_TEXTURE2D_DESC actualSceneDesc{};scene.texture->GetDesc(&actualSceneDesc);
    D3D11_SHADER_RESOURCE_VIEW_DESC actualSceneView{};sceneView->GetDesc(&actualSceneView);
    if(actualSceneDesc.Width!=width||actualSceneDesc.Height!=height||actualSceneDesc.Format!=DXGI_FORMAT(sceneResourceFormat)||
        actualSceneView.Format!=DXGI_FORMAT(sceneShaderFormat)||actualSceneView.ViewDimension!=D3D11_SRV_DIMENSION_TEXTURE2D)
        throw std::runtime_error("Normal post current Scene differs from exported native resource/SRV");
    scene.views.emplace_back(sceneView);
    D3D11_RENDER_TARGET_VIEW_DESC sceneRtv{};sceneRtv.Format=DXGI_FORMAT(sceneRenderFormat);sceneRtv.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
    ComPtr<ID3D11RenderTargetView> sceneTarget;check(device->CreateRenderTargetView(scene.texture.Get(),&sceneRtv,&sceneTarget),"normal post same source Scene RTV");
    scene.targets.push_back(sceneTarget);surfaces.emplace("SceneColor",scene);
    ComPtr<ID3D11Resource> depthResource;mainDepthView->GetResource(&depthResource);
    ComPtr<ID3D11Texture2D> depthTexture;check(depthResource.As(&depthTexture),"normal post same Main depth texture");
    D3D11_TEXTURE2D_DESC depthDesc{};depthTexture->GetDesc(&depthDesc);
    if(depthDesc.Width!=width||depthDesc.Height!=height)throw std::runtime_error("Normal post Main depth extent differs");
    externalViews.emplace("MainDepth",mainDepthView);

    // Original61 is undefined on native fresh creation. This one explicitly
    // retains Root's authorized controlled previousExposure[1,1]. No other
    // source texture or buffer receives a guessed initial-data/zero seed.
    auto& exposure=state.exposure;exposure=retainedExposure?*retainedExposure:prepareNormalPostExposure(device,source);
    surfaces.emplace("Texture61",exposure);surfaces.emplace("Exposure61",exposure);surfaces.emplace("Exposure104",exposure);
    UINT index,w,h,mips,r,s,t,u,rt,ua,count,side;
    auto readTextures=[&](const fs::path& file,bool shape) {
        std::ifstream rows(file);if(!rows)throw std::runtime_error("Normal post source texture declarations absent");
        while(rows>>index>>w>>h>>mips>>r>>s>>t>>u>>rt>>ua>>count>>side) {
            if(index==61)continue;
            if(!count||side>=count)throw std::runtime_error("Normal post exported current texture side invalid");
            auto name=shape?"DOFShape58":"Texture"+std::to_string(index);
            auto& sides=state.textureSides[name];
            for(UINT n=0;n<count;++n)sides.push_back(normalPostSurface(device,w,h,mips,r,s,t,u,rt!=0,ua!=0));
            state.selectors.emplace(name,side);surfaces.emplace(name,sides.at(side));
        }
    };
    readTextures(source/"dof-shape.txt",true);readTextures(source/"dof-textures.txt",false);
    UINT bw,bh,bm,br,bs,bt;std::ifstream bloomRow(source/"bloom.txt");
    if(!(bloomRow>>index>>bw>>bh>>bm>>br>>bs>>bt)||index!=85)throw std::runtime_error("Normal post physical Bloom85 descriptor absent");
    surfaces.emplace("Bloom85",normalPostSurface(device,bw,bh,bm,br,bs,bt,0,true,false));
    auto outputTarget=[&](const std::string& name,const fs::path& directory) {
        UINT rr,ss,tt,ww,hh;std::ifstream row(directory/"target.txt");
        if(!(row>>rr>>ss>>tt>>ww>>hh)||ww!=width||hh!=height)throw std::runtime_error("Normal post final target descriptor differs");
        surfaces.emplace(name,normalPostSurface(device,ww,hh,1,rr,ss,tt,0,true,false));
    };
    outputTarget("Tone108",source/"108-tone");surfaces.emplace("Final109",callerOutput);

    UINT bytes,usage,cpu,bind,misc,stride,format,elements,srvDimension,srvFlags,uavFlags;
    std::ifstream bufferRows(source/"dof-buffers.txt");if(!bufferRows)throw std::runtime_error("Normal post source DOF buffer declarations absent");
    while(bufferRows>>index>>bytes>>usage>>cpu>>bind>>misc>>stride>>format>>elements>>srvDimension>>srvFlags>>uavFlags) {
        D3D11_BUFFER_DESC d{};d.ByteWidth=bytes;d.Usage=D3D11_USAGE(usage);d.CPUAccessFlags=cpu;d.BindFlags=bind;d.MiscFlags=misc;d.StructureByteStride=stride;
        D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.Format=DXGI_FORMAT(format);sv.ViewDimension=D3D11_SRV_DIMENSION(srvDimension);
        if(srvDimension==D3D11_SRV_DIMENSION_BUFFEREX){sv.BufferEx.NumElements=elements;sv.BufferEx.Flags=srvFlags;}
        else sv.Buffer.NumElements=elements;
        D3D11_UNORDERED_ACCESS_VIEW_DESC uv{};uv.Format=DXGI_FORMAT(format);uv.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;uv.Buffer.NumElements=elements;uv.Buffer.Flags=uavFlags;
        auto name="Buffer"+std::to_string(index);auto& sides=state.bufferSides[name];
        const auto pair=pairedBuffers.find(index);const UINT count=pair==pairedBuffers.end()?1u:2u;
        const UINT selected=pair==pairedBuffers.end()?0u:pair->second;
        for(UINT n=0;n<count;++n) {
            NormalPostBuffer out;check(device->CreateBuffer(&d,nullptr,&out.buffer),"normal post original DOF GPU buffer");
            check(device->CreateShaderResourceView(out.buffer.Get(),&sv,&out.view),"normal post original DOF buffer SRV");
            check(device->CreateUnorderedAccessView(out.buffer.Get(),&uv,&out.uav),"normal post original DOF buffer UAV");sides.push_back(out);
        }
        state.selectors.emplace(name,selected);buffers.emplace(name,sides.at(selected));
    }
    std::ifstream stagingRows(source/"dof-staging.txt");UINT stagingSide;
    if(!stagingRows)throw std::runtime_error("Normal post controller staging descriptors absent");
    while(stagingRows>>stagingSide>>bytes>>usage>>cpu>>bind>>misc>>stride) {
        D3D11_BUFFER_DESC d{};d.ByteWidth=bytes;d.Usage=D3D11_USAGE(usage);d.CPUAccessFlags=cpu;
        d.BindFlags=bind;d.MiscFlags=misc;d.StructureByteStride=stride;
        check(device->CreateBuffer(&d,nullptr,&spriteStaging.at(stagingSide)),"normal post source controller staging");
    }
    UINT viewFormat,histElements;std::ifstream histogramRow(source/"histogram.txt");
    if(!(histogramRow>>bytes>>viewFormat>>histElements>>bind>>usage>>cpu>>misc>>stride))throw std::runtime_error("Normal post original histogram descriptor absent");
    D3D11_BUFFER_DESC hd{};hd.ByteWidth=bytes;hd.BindFlags=bind;hd.Usage=D3D11_USAGE(usage);hd.CPUAccessFlags=cpu;hd.MiscFlags=misc;hd.StructureByteStride=stride;
    NormalPostBuffer histogram;check(device->CreateBuffer(&hd,nullptr,&histogram.buffer),"normal post original Histogram8");
    D3D11_SHADER_RESOURCE_VIEW_DESC hv{};hv.Format=DXGI_FORMAT(viewFormat);hv.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;hv.Buffer.NumElements=histElements;
    check(device->CreateShaderResourceView(histogram.buffer.Get(),&hv,&histogram.view),"normal post Histogram8 SRV");
    D3D11_UNORDERED_ACCESS_VIEW_DESC hu{};hu.Format=DXGI_FORMAT(viewFormat);hu.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;hu.Buffer.NumElements=histElements;
    check(device->CreateUnorderedAccessView(histogram.buffer.Get(),&hu,&histogram.uav),"normal post Histogram8 UAV");buffers.emplace("Histogram8",histogram);

    // Exact selected original HDR volume. No replacement LUT or color fit.
    std::ifstream lutFile(source/"108-tone/hdr-lut.tex",std::ios::binary);if(!lutFile)throw std::runtime_error("Normal post source HDR LUT absent");
    UINT lw=u32(lutFile),lh=u32(lutFile),ld=u32(lutFile),lm=u32(lutFile),lf=u32(lutFile),lr,ls;
    std::ifstream lutFormats(source/"108-tone/hdr-resource.txt");if(!(lutFormats>>lr>>ls)||ls!=lf)throw std::runtime_error("Normal post HDR format absent");
    std::vector<std::vector<char>> lutStorage(lm);std::vector<D3D11_SUBRESOURCE_DATA> lutInitial(lm);
    for(UINT mip=0;mip<lm;++mip){auto& data=lutInitial[mip];data.SysMemPitch=u32(lutFile);data.SysMemSlicePitch=u32(lutFile);
        lutStorage[mip].resize(u32(lutFile));lutFile.read(lutStorage[mip].data(),std::streamsize(lutStorage[mip].size()));
        if(!lutFile)throw std::runtime_error("Truncated normal post HDR subresource");data.pSysMem=lutStorage[mip].data();}
    D3D11_TEXTURE3D_DESC ldsc{};ldsc.Width=lw;ldsc.Height=lh;ldsc.Depth=ld;ldsc.MipLevels=lm;ldsc.Format=DXGI_FORMAT(lr);ldsc.Usage=D3D11_USAGE_IMMUTABLE;ldsc.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    ComPtr<ID3D11Texture3D> lut;check(device->CreateTexture3D(&ldsc,lutInitial.data(),&lut),"normal post original HDR3D");
    D3D11_SHADER_RESOURCE_VIEW_DESC lsv{};lsv.Format=DXGI_FORMAT(ls);lsv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE3D;lsv.Texture3D.MipLevels=lm;
    ComPtr<ID3D11ShaderResourceView> lutView;check(device->CreateShaderResourceView(lut.Get(),&lsv,&lutView),"normal post original HDR SRV");externalViews.emplace("hdr-lut.tex",lutView);
    auto compositeBytes=load(source/"108-tone/cb22.bin");float lensGate,ldrGate;
    if(compositeBytes.size()!=208)throw std::runtime_error("Normal post needs complete original CB22");
    std::memcpy(&lensGate,compositeBytes.data()+40,4);std::memcpy(&ldrGate,compositeBytes.data()+20,4);
    if(lensGate>0||ldrGate>0)throw std::runtime_error("Normal current package needs an active Lens/LDR producer beyond this source selection");
    return state;
}

static ComPtr<ID3D11Texture2D> executeNormalPost(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    NativeNormalPost& state,const std::vector<NativeNormalPostPass>& passes,
    const std::function<void()>& before57={}) {
    // The incoming logical description remains independent of ignored D3D
    // hardware fields. Source11462D0 stores entry Test for its restored Write.
    const auto entryDepthDesc=logical.description;
    ComPtr<ID3D11BlendState> entryBlend;float entryBlendFactor[4];UINT entrySampleMask;
    context->OMGetBlendState(&entryBlend,entryBlendFactor,&entrySampleMask);
    context->ClearState();
    auto& surfaces=state.surfaces;auto& buffers=state.buffers;auto& externalViews=state.externalViews;
    auto& spriteStaging=state.spriteStaging;auto& exposure=state.exposure;
    const UINT width=state.width,height=state.height;

    auto srv=[&](const std::string& name,UINT mip)->ID3D11ShaderResourceView* {
        if(name=="Null")return nullptr;
        auto ext=externalViews.find(name);if(ext!=externalViews.end())return ext->second.Get();
        auto buf=buffers.find(name);if(buf!=buffers.end())return buf->second.view.Get();
        auto split=name.find("-mip");std::string physical=name;
        if(split!=std::string::npos){mip=UINT(std::stoul(name.substr(split+4)));physical=name.substr(0,split);}
        return surfaces.at(physical).views.at(mip).Get();
    };
    auto unordered=[&](const std::string& name)->ID3D11UnorderedAccessView* {
        auto b=buffers.find(name);return b!=buffers.end()?b->second.uav.Get():surfaces.at(name).uav.Get();
    };
    auto bindSrv=[&](const std::string& stage,UINT slot,ID3D11ShaderResourceView* view) {
        if(stage=="VS")context->VSSetShaderResources(slot,1,&view);
        else if(stage=="PS")context->PSSetShaderResources(slot,1,&view);
        else if(stage=="CS")context->CSSetShaderResources(slot,1,&view);
        else throw std::runtime_error("Unsupported normal post source shader stage");
    };

    for(const auto& pass:passes) {
        // Original Count0 reads selected controller staging here, after55
        // and before current57/58 produce the new B5 indirect arguments.
        if(pass.name=="057-dof"&&before57)before57();
        normalPostUnbind(context);context->GSSetShader(nullptr,nullptr,0);context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
        for(const auto& binding:pass.constants) {
            auto raw=binding.buffer.Get();
            if(binding.stage=="VS")context->VSSetConstantBuffers(binding.slot,1,&raw);
            else if(binding.stage=="PS")context->PSSetConstantBuffers(binding.slot,1,&raw);
            else context->CSSetConstantBuffers(binding.slot,1,&raw);
        }
        for(const auto& binding:pass.srvs)bindSrv(binding.stage,binding.slot,srv(binding.name,binding.mip));
        if(pass.compute) {
            context->VSSetShader(nullptr,nullptr,0);context->PSSetShader(nullptr,nullptr,0);
            for(const auto& binding:pass.uavs) {
                auto actual=unordered(binding.name);context->CSSetUnorderedAccessViews(binding.slot,1,&actual,nullptr);
            }
            context->CSSetShader(pass.cs.Get(),nullptr,0);
            context->Dispatch(pass.dispatchX,pass.dispatchY,pass.dispatchZ);
        } else {
            context->CSSetShader(nullptr,nullptr,0);
            context->VSSetShader(pass.vs.Get(),nullptr,0);context->PSSetShader(pass.ps.Get(),nullptr,0);
            std::array<ID3D11RenderTargetView*,8> actualTargets{};UINT targetCount=0;
            for(const auto& binding:pass.targets) {
                actualTargets.at(binding.slot)=surfaces.at(binding.name).targets.at(binding.mip).Get();
                targetCount=std::max(targetCount,binding.slot+1);
            }
            context->OMSetRenderTargets(targetCount,actualTargets.data(),nullptr);
            if(pass.clear) {
                const float zero[4]{0,0,0,0};
                for(UINT n=0;n<targetCount;++n)if(actualTargets[n])context->ClearRenderTargetView(actualTargets[n],zero);
            }
            context->OMSetBlendState(pass.blend.Get(),nullptr,~0u);
            auto dd=logical.description;dd.DepthEnable=pass.depthTest!=0;
            dd.DepthWriteMask=pass.depthWrite?D3D11_DEPTH_WRITE_MASK_ALL:D3D11_DEPTH_WRITE_MASK_ZERO;
            dd.DepthFunc=D3D11_COMPARISON_GREATER_EQUAL;dd.StencilEnable=FALSE;
            ComPtr<ID3D11DepthStencilState> depth;check(device->CreateDepthStencilState(&dd,&depth),"normal post source logical depth");
            logical.bind(context,depth.Get(),dd,logical.reference);
            context->RSSetState(pass.raster.Get());context->RSSetViewports(1,&pass.viewport);context->RSSetScissorRects(1,&pass.scissor);
            for(const auto& binding:pass.samplers) {
                auto raw=binding.sampler.Get();context->PSSetSamplers(binding.slot,1,&raw);
            }
            context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(pass.topology);
            if(pass.indirect) {
                auto actual=buffers.at("Buffer"+std::to_string(pass.index)).buffer.Get();
                context->CopyResource(spriteStaging.at(pass.stagingSide).Get(),actual);
                context->DrawInstancedIndirect(actual,pass.offset);
            } else context->DrawInstanced(pass.vertexCount,pass.instanceCount,pass.startVertex,pass.startInstance);
        }

    }
    normalPostUnbind(context);
    auto& final=surfaces.at("Final109");
    normalPostUnbind(context);
    ComPtr<ID3D11DepthStencilState> currentDepthState;UINT currentStencilRef;
    context->OMGetDepthStencilState(&currentDepthState,&currentStencilRef);
    D3D11_DEPTH_STENCIL_DESC exitDepth=logical.description;
    exitDepth.DepthEnable=entryDepthDesc.DepthEnable;
    exitDepth.DepthWriteMask=entryDepthDesc.DepthEnable?D3D11_DEPTH_WRITE_MASK_ALL:D3D11_DEPTH_WRITE_MASK_ZERO;
    exitDepth.DepthFunc=entryDepthDesc.DepthFunc;
    ComPtr<ID3D11DepthStencilState> restoredDepth;
    check(device->CreateDepthStencilState(&exitDepth,&restoredDepth),"normal post source outer depth restoration");
    logical.bind(context,restoredDepth.Get(),exitDepth,currentStencilRef);
    context->OMSetBlendState(entryBlend.Get(),entryBlendFactor,entrySampleMask);
    //108's temporary RT84 is popped before109.109 and the source exit retain
    //the inherited Output13BF771D. Scene2 at194 aliases this current color0;
    //Tone108 is a separate format26 texture, not this output90/91 target.
    auto finalTarget=final.targets.at(0).Get();context->OMSetRenderTargets(1,&finalTarget,nullptr);
    D3D11_VIEWPORT finalViewport{0,0,float(width),float(height),0,1};context->RSSetViewports(1,&finalViewport);

    return final.texture;
}

static void endNormalPost(NativeNormalPost& state) {
    // Source114AB40 runs only after the successful Execute/End sequence.
    // Selectors change aliases; the original pixels are not copied or cleared.
    for(auto& entry:state.textureSides)if(entry.second.size()==2) {
        auto& side=state.selectors.at(entry.first);side^=1;
        state.surfaces.at(entry.first)=entry.second.at(side);
    }
    for(auto& entry:state.bufferSides)if(entry.second.size()==2) {
        auto& side=state.selectors.at(entry.first);side^=1;
        state.buffers.at(entry.first)=entry.second.at(side);
    }
}
