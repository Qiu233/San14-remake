// Original category2 City/Gate groups. Shared imports are completed before
// groups are created; each group retains its own local pose and current CBs.
#pragma once
#include <map>
#include <functional>
#include "city-accessory-pose-native.h"
#include "city-accessory-next-source/accessory-population-native.h"
#include "city-accessory-next-source/material-source/accessory-glyph-native.h"

struct NativeAccessoryMaterial {
    std::array<ComPtr<ID3D11ShaderResourceView>,16> textures;
    std::array<ComPtr<ID3D11SamplerState>,16> samplers;
    accessory_glyph::Primary glyph;
};
struct NativeCityAccessoryGroup {
    NativeAccessoryPoseOwner pose;
    NativeModelInputs models;
    float controllerFrame24{};
    int materialForce{};
    NativeAccessoryPoseRoot root{};
};
struct NativeCityAccessoryGpu {
    source_accessory_population::Profile population;
    source_accessory_population::Frame frame;
    NativeAccessoryPoseSource poseSource;
    NativeModelInputs templates;
    std::vector<std::array<char,80>> materialConstants;
    std::map<std::pair<int,int>,NativeAccessoryMaterial> materials;
    std::map<unsigned,NativeCityAccessoryGroup> groups;
    std::vector<unsigned> visible;
    unsigned draws{},created{},released{};
};
inline NativeAccessoryMaterial prepareNativeAccessoryMaterial(ID3D11Device* device,ID3D11DeviceContext* context,
    accessory_glyph::StockProducer& stock,std::map<unsigned,accessory_glyph::Atlas>& atlases,
    const fs::path& source,const fs::path& relative) {
    NativeAccessoryMaterial material;const auto directory=source/relative;
    UINT slot,ordinal,runtime,key;std::string file,sampler;
    std::ifstream textures(directory/"textures.txt");
    while(textures>>slot>>ordinal>>runtime>>key>>file>>sampler) {
        if(!runtime)continue;
        auto job=accessory_glyph::read(directory/file);std::array<unsigned,8> header{};
        std::memcpy(header.data(),job.data(),32);
        accessory_glyph::Atlas* atlas=nullptr;
        if(header[7]) {
            auto found=atlases.find(header[3]);
            if(found==atlases.end())found=atlases.emplace(header[3],accessory_glyph::Atlas::create(device,source,header[3])).first;
            atlas=&found->second;
        }
        material.glyph=stock.execute(device,context,source,relative,atlas);
        material.textures.at(slot)=material.glyph.srv;
        material.samplers.at(slot)=material.glyph.sourceSampler;
    }
    UINT resourceFormat,viewFormat;std::ifstream formats(directory/"texture-formats.txt");
    while(formats>>slot>>resourceFormat>>viewFormat>>file)
        material.textures.at(slot)=loadNativeModelTexture(device,directory/file,DXGI_FORMAT(resourceFormat),DXGI_FORMAT(viewFormat));
    std::ifstream samplers(directory/"samplers-native.txt");
    while(samplers>>slot>>file)material.samplers.at(slot)=accessory_glyph::sampler(device,directory/file);
    return material;
}
inline NativeCityAccessoryGpu prepareNativeCityAccessoryGpu(ID3D11Device* device,ID3D11DeviceContext* context,
    const fs::path& input,bool loadGeometry=true) {
    NativeCityAccessoryGpu gpu;
    gpu.population=source_accessory_population::loadProfile(input/"population/population-profile.bin");
    gpu.poseSource=prepareNativeAccessoryPoseSource(input.parent_path()/"city-accessory-input/pose-native");
    if(loadGeometry)gpu.templates=loadNativeModels(device,input/"population");
    std::ifstream models(input/"population"/"models.txt");std::string relative;
    while(models>>relative) {
        auto bytes=load(input/"population"/relative/"ps-b0.bin");std::array<char,80> material;
        std::memcpy(material.data(),bytes.data(),80);gpu.materialConstants.push_back(material);
    }
    const auto materialInput=input/"material-source";
    accessory_glyph::StockProducer stock(device,materialInput);std::map<unsigned,accessory_glyph::Atlas> atlases;
    std::ifstream records(materialInput/"material-ready.txt");
    int manager,force;unsigned fields[7];std::string directory;
    while(records>>manager>>force>>fields[0]>>fields[1]>>fields[2]>>fields[3]>>fields[4]>>fields[5]>>fields[6]>>directory)
        gpu.materials.emplace(std::make_pair(manager,force),prepareNativeAccessoryMaterial(device,context,stock,atlases,materialInput,directory));
    return gpu;
}
inline ComPtr<ID3D11Buffer> createNativeAccessoryCurrentCb(ID3D11Device* device,UINT size) {
    D3D11_BUFFER_DESC desc{};desc.ByteWidth=size;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
    ComPtr<ID3D11Buffer> result;check(device->CreateBuffer(&desc,nullptr,&result),"Accessory current full CB");return result;
}
// Called for a real eligible App update. The phase callback owns the original
// SequentialPlayer wrapper, including its default loop policy. Numeric frame
// identity is never used to pick a pose or population.
inline unsigned updateNativeCityAccessoryGpu(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeCityAccessoryGpu& gpu,const source_camera::Frame& camera,int appMode,float ecB0,double distance90,
    bool actualEligibleUpdate,unsigned actualFps,
    const std::function<float(unsigned,float)>& returnedPlayerPhase) {
    std::array<std::array<float,4>,6> planes;
    for(unsigned i=0;i<6;++i)std::memcpy(planes[i].data(),&camera.planes[i].equation,16);
    gpu.frame=source_accessory_population::prepare(gpu.population,reinterpret_cast<const char*>(camera.prefix.data()),
        reinterpret_cast<const char*>(camera.prefix.data()),appMode,ecB0,distance90,planes,false);
    gpu.visible.clear();gpu.created=gpu.released=0;
    for(auto it=gpu.groups.begin();actualEligibleUpdate&&it!=gpu.groups.end();) {
        auto unit=gpu.population.placements[it->first].unitIndex;
        if(std::find(gpu.frame.admittedUnits.begin(),gpu.frame.admittedUnits.end(),unit)==gpu.frame.admittedUnits.end()) {
            it=gpu.groups.erase(it);++gpu.released;
        }else ++it;
    }
    for(auto& request:gpu.frame.requests) {
        const auto& placement=gpu.population.placements[request.placementIndex];
        const auto& unit=gpu.population.units[request.unitIndex];
        auto found=gpu.groups.find(request.placementIndex);
        const bool created=found==gpu.groups.end();
        if(created&&!actualEligibleUpdate)continue;
        if(created) {
            NativeCityAccessoryGroup group;group.pose=createNativeAccessoryPoseOwner(gpu.poseSource);
            group.models=gpu.templates;
            for(unsigned i=0;i<group.models.draws.size();++i) {
                auto& draw=group.models.draws[i];draw.identity="accessory/"+std::to_string(request.placementIndex)+"/draw-"+std::to_string(i);
                draw.vsConstants=createNativeAccessoryCurrentCb(device,6256);draw.psConstants=createNativeAccessoryCurrentCb(device,80);
            }
            found=gpu.groups.emplace(request.placementIndex,std::move(group)).first;++gpu.created;
        }
        auto& group=found->second;
        if(actualEligibleUpdate)std::memcpy(&group.root,placement.root.data(),48);
        if(actualEligibleUpdate&&(created||group.materialForce!=unit.force)) {
            for(unsigned i=0;i<group.models.draws.size();++i) {
                auto& draw=group.models.draws[i];
                const auto& material=gpu.materials.at({i==0?1:i==1?0:-1,i<2?unit.force:-1});
                draw.textures=material.textures;draw.samplers=material.samplers;
            }
            group.materialForce=unit.force;
        }
        bool animate=request.animateNear&&actualEligibleUpdate;
        float phase=animate?returnedPlayerPhase(request.placementIndex,
            source_accessory_population::D(group.controllerFrame24,gpu.poseSource.frameRate)):0;
        const auto& pose=sampleNativeAccessoryPose(gpu.poseSource,group.pose,phase,group.root,animate);
        if(animate) {
            float rounded=std::floor(source_accessory_population::A(source_accessory_population::M(phase,gpu.poseSource.frameRate),.5f));
            group.controllerFrame24=group.controllerFrame24>rounded?rounded:
                source_accessory_population::A(rounded,source_accessory_population::D(gpu.poseSource.frameRate,float(actualFps)));
        }
        for(unsigned i=0;i<group.models.draws.size();++i) {
            auto& draw=group.models.draws[i];const auto& rows=pose.palettes[i].rows;
            auto vs=source_accessory_population::vertex(reinterpret_cast<const char*>(camera.prefix.data()),reinterpret_cast<const float*>(rows.data()),unsigned(rows.size()));
            float spec[4];int materialId;std::memcpy(spec,gpu.materialConstants[i].data(),16);
            std::memcpy(&materialId,gpu.materialConstants[i].data()+64,4);
            auto ps=source_accessory_population::regularPixel(reinterpret_cast<const char*>(camera.prefix.data()),spec,materialId);
            context->UpdateSubresource(draw.vsConstants.Get(),0,nullptr,vs.data(),0,0);
            context->UpdateSubresource(draw.psConstants.Get(),0,nullptr,ps.data(),0,0);
        }
        if(request.mainVisible)gpu.visible.push_back(request.placementIndex);
    }
    return unsigned(gpu.visible.size()*gpu.templates.draws.size());
}
inline void drawNativeCityAccessoryGpu(ID3D11DeviceContext* context,NativeCityAccessoryGpu& gpu,
    ID3D11RenderTargetView* const* targets,ID3D11DepthStencilView* depth,const D3D11_VIEWPORT& viewport) {
    gpu.draws=0;
    for(unsigned identity:gpu.visible) {
        auto& models=gpu.groups.at(identity).models;drawNativeModels(context,models,targets,depth,viewport);
        gpu.draws+=unsigned(models.draws.size());
    }
}
