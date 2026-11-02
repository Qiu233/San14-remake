// Current source-owned51 cities/10 gates. Include after city-model-native.h.
// Once source/device preparation; normal current World values and Camera
// requests perform no original-file reads or device-object creation.
#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "../../Device/PrimaryModelGpuMedia.h"
#include <map>
#include "CityPopulationInputs.h"
struct NativeCityPopulationSource : NativeCityTemplateBank {
    NativeModelContinuous catalog;
    std::vector<NativeModelWorldAdmission> worlds;
    std::vector<unsigned> currentAssets,present;
    std::vector<std::string> identities;
    NativeModelCollectionCaller caller;
};
inline bool updateNativeCityPopulationSource(NativeCityPopulationSource& source,const std::vector<NativePrimaryUpdate>& current) {
    if(current.size()!=source.worlds.size())throw std::runtime_error("Current primary population must retain every source owner identity");
    // Validate before replacing the current catalog; missing/new source assets
    // are explicit import/reload boundaries, not silently retained old poses.
    bool physicalBindingsChanged=false;
    for(std::size_t i=0;i<current.size();++i){const auto& r=current[i];
        if(r.index!=i||r.asset>=source.assets.size()||(r.mode!=0&&r.mode!=3))throw std::runtime_error("Current primary source owner/asset/frustum identity is unclosed");
        physicalBindingsChanged|=source.currentAssets[i]!=r.asset||source.present[i]!=r.present;
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
    return physicalBindingsChanged;
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
    std::shared_ptr<const San14::Render::NativePrimaryModelGpuMedia> immutableMedia;
    NativeModelInputs templates,currentModels;
    std::vector<ComPtr<ID3D11Buffer>> vertexConstants,pixelConstants;
    bool physicalBindingsPending=false;
};

// Standalone/legacy devices retain an explicit import path. Application
// sharedScenes supplies a startup bank and must never call this fallback.
inline std::shared_ptr<const San14::Render::NativePrimaryModelGpuMedia>
loadNativeCityPopulationMedia(ID3D11Device* device,const NativeCityTemplateBank& source,const fs::path& directory) {
    std::vector<San14::Render::NativePrimaryModelGpuDrawSource> declarations;
    NativePreparedInputFile list(directory/"models.txt");std::string relative;
    if(!list)throw std::runtime_error("Primary complete model list is absent");
    while(list>>relative) {
        const auto& metadata=source.templates.at(declarations.size());
        const auto current=directory/fs::u8path(relative);
        San14::Render::NativePrimaryModelGpuDrawSource draw;
        draw.shaderFile=metadata.shaderFile;draw.vertexProgram=metadata.vs;draw.pixelProgram=metadata.ps;
        draw.relativeDirectory=relative;draw.mediaDirectory=nativeModelMediaDirectory(current);
        NativePreparedInputFile geometry(current/"draw.txt");
        San14::Render::readNativePrimaryModelGpuGeometry(geometry,draw);
        declarations.push_back(std::move(draw));
    }
    if(declarations.size()!=source.templates.size())throw std::runtime_error("Primary complete template catalog differs");
    return San14::Render::NativePrimaryModelGpuMedia::load(device,declarations);
}

// Copy only immutable COM references, then create this view's actual source
// depth/stencil state. No programs, streams or texture files are read here.
inline NativeModelInputs prepareNativeCityPopulationTemplates(ID3D11Device* device,const NativeCityTemplateBank& source,
        const fs::path& directory,const San14::Render::NativePrimaryModelGpuMedia& media) {
    if(media.draws.size()!=source.templates.size())throw std::runtime_error("Resident Primary GPU catalog differs from current source templates");
    NativeModelInputs out;out.blend=media.blend;out.draws.reserve(media.draws.size());
    std::map<std::array<unsigned,4>,ComPtr<ID3D11DepthStencilState>> depths;
    for(std::size_t index=0;index<media.draws.size();++index) {
        const auto& immutable=media.draws[index];
        NativeModelDraw draw;draw.vs=immutable.vertex;draw.ps=immutable.pixel;draw.layout=immutable.layout;
        draw.vertexBuffers=immutable.vertexBuffers;draw.strides=immutable.strides;draw.indices=immutable.indices;
        draw.textures=immutable.textures;draw.samplers=immutable.samplers;draw.raster=immutable.raster;
        draw.indexCount=immutable.indexCount;draw.indexStart=immutable.indexStart;draw.baseVertex=immutable.baseVertex;
        draw.topology=immutable.topology;draw.indexBits=immutable.indexBits;
        NativePreparedInputFile input(directory/fs::u8path(immutable.relativeDirectory)/"draw.txt");
        // Skip the resource prefix to reach this view's current depth state.
        // Its immutable geometry was admitted by the startup generation owner.
        unsigned indexCount{},indexStart{},topology{},indexBits{},fill{},cull{},front{};INT baseVertex{};std::string indexFile;
        input>>indexCount>>indexStart>>baseVertex>>topology>>indexBits>>indexFile>>fill>>cull>>front;
        unsigned test{},write{},compare{},stencil{};input>>test>>write>>compare>>stencil;
        if(stencil)input>>draw.stencilReference;
        if(!input)throw std::runtime_error("Current Primary depth/stencil metadata is absent");
        auto& state=depths[{test,write,compare,stencil}];
        if(!state) {
            D3D11_DEPTH_STENCIL_DESC description{};description.DepthEnable=test;
            description.DepthWriteMask=write?D3D11_DEPTH_WRITE_MASK_ALL:D3D11_DEPTH_WRITE_MASK_ZERO;
            description.DepthFunc=D3D11_COMPARISON_FUNC(compare);description.StencilEnable=stencil;
            description.StencilReadMask=description.StencilWriteMask=255;
            description.FrontFace.StencilFunc=D3D11_COMPARISON_ALWAYS;
            description.FrontFace.StencilFailOp=description.FrontFace.StencilDepthFailOp=D3D11_STENCIL_OP_KEEP;
            description.FrontFace.StencilPassOp=D3D11_STENCIL_OP_REPLACE;description.BackFace=description.FrontFace;
            check(device->CreateDepthStencilState(&description,&state),"Primary view-private depth/stencil");
        }
        draw.depth=state;out.draws.push_back(std::move(draw));
    }
    return out;
}
inline void connectNativeCityPopulationCurrentModels(NativeCityPopulation& owner) {
    owner.currentModels.draws.clear();owner.currentModels.blend=owner.templates.blend;
    for(const auto& instance:owner.source.catalog.instances)for(unsigned d=0;d<instance.primitiveCount;++d){const auto& p=owner.source.catalog.primitives[instance.firstPrimitive+d];auto model=owner.templates.draws[p.templateIndex];
        const auto cb=instance.index*owner.source.maxDraws+d;model.vsConstants=owner.vertexConstants[cb];model.psConstants=owner.pixelConstants[cb];
        model.identity=owner.source.identities[instance.index]+"/draw-"+std::to_string(p.drawIndex);owner.currentModels.draws.push_back(model);}
}
inline NativeCityPopulation prepareNativeCityPopulation(ID3D11Device* device,const fs::path& directory,
    const std::vector<NativePrimaryUpdate>& current,
    std::shared_ptr<const San14::Render::NativePrimaryModelGpuMedia> sharedMedia={},bool requireResident=false) {
    if(requireResident&&!sharedMedia)throw std::runtime_error("Shared scenes require Primary GPU media loaded at startup");
    NativeCityPopulation r;r.source=prepareNativeCityPopulationSource(readNativeCityTemplateBank(directory),current);
    r.immutableMedia=sharedMedia?std::move(sharedMedia):loadNativeCityPopulationMedia(device,r.source,directory);
    r.templates=prepareNativeCityPopulationTemplates(device,r.source,directory,*r.immutableMedia);
    std::array<char,160> zeroVertex{};std::array<char,80> zeroPixel{};const auto n=r.source.worlds.size()*r.source.maxDraws;
    r.vertexConstants.reserve(n);r.pixelConstants.reserve(n);for(std::size_t i=0;i<n;++i){r.vertexConstants.push_back(modelContinuousConstantBuffer(device,zeroVertex.data(),160));r.pixelConstants.push_back(modelContinuousConstantBuffer(device,zeroPixel.data(),80));}
    connectNativeCityPopulationCurrentModels(r);return r;
}
inline bool updateNativeCityPopulation(NativeCityPopulation& owner,const std::vector<NativePrimaryUpdate>& current) {
    // This owner imports its templates and allocates each resident CB once.
    // World/OBB/mode/constant bytes always publish; only asset/presence changes
    // alter the validated draw topology and its physical template/CB slots.
    const bool retry=owner.physicalBindingsPending;
    // An exceptional publication may already have changed the CPU source.
    // Keep its connection pending until the complete GPU description succeeds.
    owner.physicalBindingsPending=true;
    const bool changed=updateNativeCityPopulationSource(owner.source,current)||retry;
    if(changed)connectNativeCityPopulationCurrentModels(owner);
    owner.physicalBindingsPending=false;
    return changed;
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
