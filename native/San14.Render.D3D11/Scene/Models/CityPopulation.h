// Current source-owned51 cities/10 gates. Include after city-model-native.h.
// Once source/device preparation; normal current World values and Camera
// requests perform no original-file reads or device-object creation.
#pragma once
#include <map>
#include "CityPopulationInputs.h"
struct NativeCityPopulationSource : NativeCityTemplateBank {
    NativeModelContinuous catalog;
    std::vector<NativeModelWorldAdmission> worlds;
    std::vector<unsigned> currentAssets,present;
    std::vector<std::string> identities;
    NativeModelCollectionCaller caller;
};
inline void updateNativeCityPopulationSource(NativeCityPopulationSource& source,const std::vector<NativePrimaryUpdate>& current) {
    if(current.size()!=source.worlds.size())throw std::runtime_error("Current primary population must retain every source owner identity");
    // Validate before replacing the current catalog; missing/new source assets
    // are explicit import/reload boundaries, not silently retained old poses.
    for(std::size_t i=0;i<current.size();++i){const auto& r=current[i];
        if(r.index!=i||r.asset>=source.assets.size()||(r.mode!=0&&r.mode!=3))throw std::runtime_error("Current primary source owner/asset/frustum identity is unclosed");
        const auto& a=source.assets[r.asset];if(r.draws.size()!=(r.present?a.draws.size():0))throw std::runtime_error("Current primary complete LOCAL/World rows absent");
        if(r.world.exclusion78!=a.exclusion)throw std::runtime_error("Actual current primary light exclusion changed; complete source record routing required");
        for(std::size_t d=0;d<r.draws.size();++d)if(r.draws[d].draw!=a.draws[d].draw)throw std::runtime_error("Current primary actual draw publication order differs");
    }
    source.catalog.primitives.clear();source.catalog.primitives.reserve(current.size()*source.maxDraws);
    for(std::size_t i=0;i<current.size();++i){const auto& r=current[i];const auto& a=source.assets[r.asset];
        source.worlds[i]=r.world;source.currentAssets[i]=r.asset;source.present[i]=r.present;
        auto& instance=source.catalog.instances[i];instance.index=unsigned(i);instance.mode=r.mode;
        instance.firstPrimitive=unsigned(source.catalog.primitives.size());instance.primitiveCount=unsigned(r.draws.size());
        instance.hasUnsupported=a.unsupported;instance.forceDistance=a.forceDistance;instance.distanceBias=a.bias;instance.obb=r.obb;
        for(std::size_t d=0;d<r.draws.size();++d){const auto& asset=a.draws[d];NativeModelContinuousPrimitive p;
            p.instanceIndex=unsigned(i);p.drawIndex=asset.draw;p.templateIndex=asset.templateIndex;p.tableOwner=asset.table;p.vertex=prepareNativePrimaryVertex(asset,r.draws[d]);p.pixel=asset.pixel;
            source.catalog.primitives.push_back(p);}
    }
}
inline NativeCityPopulationSource prepareNativeCityPopulationSource(NativeCityTemplateBank bank,
    const std::vector<NativePrimaryUpdate>& current) {
    NativeCityPopulationSource result;
    static_cast<NativeCityTemplateBank&>(result)=std::move(bank);
    result.catalog.templateCount=unsigned(result.templates.size());result.catalog.commonMode=result.commonMode;
    result.catalog.sortFields=result.sortFields;
    result.catalog.instances.resize(current.size());result.worlds.resize(current.size());
    result.currentAssets.resize(current.size());result.present.resize(current.size());
    result.identities.reserve(current.size());
    for(const auto& row:current)result.identities.push_back("citygate-primary/"+std::to_string(row.index));
    updateNativeCityPopulationSource(result,current);
    return result;
}
struct NativeCityPopulationPlan {NativeModelContinuousPlan models;std::vector<unsigned> ordinaryCounts;};
template<class AcceptedCamera>
inline NativeCityPopulationPlan selectNativeCityPopulation(const NativeCityPopulationSource& source,const AcceptedCamera& camera) {
    NativeCityPopulationPlan r;r.models=selectNativeModelContinuousFromCameraFrame(source.catalog,camera);r.ordinaryCounts.resize(source.recordCount);
    auto eligible=[&](unsigned index){return source.present[index]&&nativeCityModelCurrentMasks(source.worlds[index],source.caller).firstSlot;};
    auto remove=[&](auto& ids){ids.erase(std::remove_if(ids.begin(),ids.end(),[&](unsigned i){return !eligible(i);}),ids.end());};
    remove(r.models.visibleInstances);remove(r.models.visibleUnsupportedInstances);
    r.models.draws.erase(std::remove_if(r.models.draws.begin(),r.models.draws.end(),[&](const auto& d){const auto& p=source.catalog.primitives[d.primitiveIndex];return !eligible(p.instanceIndex)||source.templates[p.templateIndex].allowedRecords==0;}),r.models.draws.end());
    for(const auto& d:r.models.draws){const auto& p=source.catalog.primitives[d.primitiveIndex];auto bits=source.templates[p.templateIndex].allowedRecords;
        for(unsigned record=0;record<source.recordCount;++record)if(bits&(1u<<record))++r.ordinaryCounts[record];}
    return r;
}

struct NativeCityPopulation {
    NativeCityPopulationSource source;
    NativeModelInputs templates,currentModels;
    std::vector<ComPtr<ID3D11Buffer>> vertexConstants,pixelConstants;
};

// Declaration/geometry/texture device pools are import-once, keyed by actual
// source resource paths or D3D descriptors. No per-owner mesh/image copies.
inline NativeModelInputs prepareNativeCityPopulationTemplates(ID3D11Device* device,const NativeCityTemplateBank& source,const fs::path& directory) {
    NativeModelInputs out;
    std::map<std::string,ComPtr<ID3D11Buffer>> vertices,indices;
    std::map<std::uint64_t,ComPtr<ID3D11VertexShader>> vertexShaders;
    std::map<std::uint64_t,ComPtr<ID3D11PixelShader>> pixelShaders;
    std::map<std::string,ComPtr<ID3D11InputLayout>> layouts;
    std::map<std::string,ComPtr<ID3D11ShaderResourceView>> textures;
    std::map<std::array<unsigned,13>,ComPtr<ID3D11SamplerState>> samplers;
    std::map<std::array<unsigned,3>,ComPtr<ID3D11RasterizerState>> rasters;
    std::map<std::array<unsigned,4>,ComPtr<ID3D11DepthStencilState>> depths;
    std::ifstream list(directory/"models.txt");std::string relative;
    while(list>>relative){const auto index=out.draws.size();const auto& metadata=source.templates.at(index);fs::path dir=directory/relative;NativeModelDraw d;
        const auto vsKey=(std::uint64_t(metadata.shaderFile)<<32)|metadata.vs,psKey=(std::uint64_t(metadata.shaderFile)<<32)|metadata.ps;
        auto v=load(dir/"vs.dxbc");auto& vs=vertexShaders[vsKey];if(!vs)check(device->CreateVertexShader(v.data(),v.size(),nullptr,&vs),"Primary original shared VS");d.vs=vs;
        auto& ps=pixelShaders[psKey];if(!ps){auto p=load(dir/"ps.dxbc");check(device->CreatePixelShader(p.data(),p.size(),nullptr,&ps),"Primary original shared PS");}d.ps=ps;
        struct Element{std::string name;unsigned index,format,slot,offset;};std::vector<Element> elements;Element e;std::ifstream layout(dir/"layout.txt");
        std::string layoutKey=std::to_string(vsKey);while(layout>>e.name>>e.index>>e.format>>e.slot>>e.offset){elements.push_back(e);layoutKey+="/"+e.name+":"+std::to_string(e.index)+":"+std::to_string(e.format)+":"+std::to_string(e.slot)+":"+std::to_string(e.offset);}
        auto& il=layouts[layoutKey];if(!il){std::vector<D3D11_INPUT_ELEMENT_DESC> desc;for(const auto& a:elements)desc.push_back({a.name.c_str(),a.index,DXGI_FORMAT(a.format),a.slot,a.offset,D3D11_INPUT_PER_VERTEX_DATA,0});
            check(device->CreateInputLayout(desc.data(),UINT(desc.size()),v.data(),v.size(),&il),"Primary original shared layout");}d.layout=il;
        unsigned slot,stride;std::string file;std::ifstream streams(dir/"streams.txt");while(streams>>slot>>stride>>file){if(slot!=d.vertexBuffers.size())throw std::runtime_error("Primary original stream order differs");
            auto path=fs::weakly_canonical(dir/file);auto& vb=vertices[path.string()];if(!vb)vb=buffer(device,load(path),D3D11_BIND_VERTEX_BUFFER);d.vertexBuffers.push_back(vb);d.strides.push_back(stride);}
        unsigned fill,cull,front,test,write,compare,stencil;std::ifstream draw(dir/"draw.txt");
        draw>>d.indexCount>>d.indexStart>>d.baseVertex>>d.topology>>d.indexBits>>file>>fill>>cull>>front>>test>>write>>compare>>stencil;
        if(!draw)throw std::runtime_error("Primary original draw metadata absent");if(stencil)draw>>d.stencilReference;
        auto path=fs::weakly_canonical(dir/file);auto& ib=indices[path.string()];if(!ib)ib=buffer(device,load(path),D3D11_BIND_INDEX_BUFFER);d.indices=ib;
        auto& rs=rasters[{fill,cull,front}];if(!rs){D3D11_RASTERIZER_DESC r{};r.FillMode=D3D11_FILL_MODE(fill);r.CullMode=D3D11_CULL_MODE(cull);r.FrontCounterClockwise=front;r.DepthClipEnable=TRUE;check(device->CreateRasterizerState(&r,&rs),"Primary source shared raster");}d.raster=rs;
        auto& ds=depths[{test,write,compare,stencil}];if(!ds){D3D11_DEPTH_STENCIL_DESC r{};r.DepthEnable=test;r.DepthWriteMask=write?D3D11_DEPTH_WRITE_MASK_ALL:D3D11_DEPTH_WRITE_MASK_ZERO;r.DepthFunc=D3D11_COMPARISON_FUNC(compare);r.StencilEnable=stencil;r.StencilReadMask=r.StencilWriteMask=255;
            r.FrontFace.StencilFunc=D3D11_COMPARISON_ALWAYS;r.FrontFace.StencilFailOp=r.FrontFace.StencilDepthFailOp=D3D11_STENCIL_OP_KEEP;r.FrontFace.StencilPassOp=D3D11_STENCIL_OP_REPLACE;r.BackFace=r.FrontFace;check(device->CreateDepthStencilState(&r,&ds),"Primary source shared depth/stencil");}d.depth=ds;
        unsigned resource,view;std::ifstream tex(dir/"textures-native.txt");while(tex>>slot>>resource>>view>>file){if(slot>=16)throw std::runtime_error("Primary texture slot");auto p=fs::weakly_canonical(dir/file);
            auto& t=textures[p.string()+"/"+std::to_string(resource)+"/"+std::to_string(view)];if(!t)t=loadNativeModelTexture(device,p,DXGI_FORMAT(resource),DXGI_FORMAT(view));d.textures[slot]=t;}
        std::ifstream sampler(dir/"samplers-native.txt");while(sampler>>slot>>file){if(slot>=16)throw std::runtime_error("Primary sampler slot");auto b=load(dir/file);if(b.size()!=52)throw std::runtime_error("Primary sampler descriptor");
            std::array<unsigned,13> key{};std::memcpy(key.data(),b.data(),52);auto& s=samplers[key];if(!s){D3D11_SAMPLER_DESC descriptor{};std::memcpy(&descriptor,b.data(),52);check(device->CreateSamplerState(&descriptor,&s),"Primary original shared sampler");}d.samplers[slot]=s;}
        out.draws.push_back(d);
    }
    if(out.draws.size()!=source.templates.size())throw std::runtime_error("Primary complete template catalog differs");
    D3D11_BLEND_DESC bd{};bd.IndependentBlendEnable=TRUE;for(unsigned i=0;i<5;++i){bd.RenderTarget[i].BlendEnable=FALSE;bd.RenderTarget[i].RenderTargetWriteMask=15;}
    check(device->CreateBlendState(&bd,&out.blend),"Primary original five MRT blend");return out;
}
inline void connectNativeCityPopulationCurrentModels(NativeCityPopulation& owner) {
    owner.currentModels.draws.clear();owner.currentModels.blend=owner.templates.blend;
    for(const auto& instance:owner.source.catalog.instances)for(unsigned d=0;d<instance.primitiveCount;++d){const auto& p=owner.source.catalog.primitives[instance.firstPrimitive+d];auto model=owner.templates.draws[p.templateIndex];
        const auto cb=instance.index*owner.source.maxDraws+d;model.vsConstants=owner.vertexConstants[cb];model.psConstants=owner.pixelConstants[cb];
        model.identity=owner.source.identities[instance.index]+"/draw-"+std::to_string(p.drawIndex);owner.currentModels.draws.push_back(model);}
}
inline NativeCityPopulation prepareNativeCityPopulation(ID3D11Device* device,const fs::path& directory,
    const std::vector<NativePrimaryUpdate>& current) {
    NativeCityPopulation r;r.source=prepareNativeCityPopulationSource(readNativeCityTemplateBank(directory),current);
    r.templates=prepareNativeCityPopulationTemplates(device,r.source,directory);
    std::array<char,160> zeroVertex{};std::array<char,80> zeroPixel{};const auto n=r.source.worlds.size()*r.source.maxDraws;
    r.vertexConstants.reserve(n);r.pixelConstants.reserve(n);for(std::size_t i=0;i<n;++i){r.vertexConstants.push_back(modelContinuousConstantBuffer(device,zeroVertex.data(),160));r.pixelConstants.push_back(modelContinuousConstantBuffer(device,zeroPixel.data(),80));}
    connectNativeCityPopulationCurrentModels(r);return r;
}
inline void updateNativeCityPopulation(NativeCityPopulation& owner,const std::vector<NativePrimaryUpdate>& current) {
    updateNativeCityPopulationSource(owner.source,current);connectNativeCityPopulationCurrentModels(owner); // existing COM owners, no new device objects.
}
inline NativeCityModelQueues updateSelectNativeCityPopulation(ID3D11DeviceContext* context,const NativeCityPopulation& owner,const NativeCityPopulationPlan& plan) {
    auto selected=updateSelectNativeModelContinuous(context,plan.models,owner.currentModels);NativeCityModelQueues r;r.ordinary54.blend=r.alpha55.blend=selected.blend;
    for(std::size_t i=0;i<selected.draws.size();++i){auto p=owner.source.catalog.primitives[plan.models.draws[i].primitiveIndex];
        (owner.source.templates[p.templateIndex].queue==54?r.ordinary54:r.alpha55).draws.push_back(std::move(selected.draws[i]));}return r;
}
inline NativeCityModelQueues mergeNativeCityPopulationWithField54(const NativeModelContinuousPlan& fieldPlan,const NativeModelInputs& field,
    const NativeCityPopulationSource& source,const NativeCityPopulationPlan& plan,const NativeCityModelQueues& population) {
    NativeCityModelSource q;for(const auto& p:source.catalog.primitives)q.queues.push_back(source.templates[p.templateIndex].queue);
    NativeCityModelPlan p;p.models=plan.models;return mergeNativeCityModelsWithField54(fieldPlan,field,q,p,population);
}
