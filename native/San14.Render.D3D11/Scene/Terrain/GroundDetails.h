#pragma once
// Include after RenderCore's check/load/u32/buffer helpers. This reuses its
// original model importer; source packets and device objects load only once.
#include "../Models/ModelRenderer.h"
#include "GroundDetailSubmission.hpp"
#include "GroundDetailAdmission.hpp"
#include "GroundDetailSeason.h"
#include "TreeWindFrame.h"
#include "GroundDetailFrame.h"
#include "../Models/ModelStencilRules.h"
#include <span>
#include <optional>
#include <unordered_map>

namespace ground_details {
using Microsoft::WRL::ComPtr;
using ground_detail_source::Affine48;
struct Statistics {
    uint32_t selectedInstances{},preparedPrimitives{},nonemptyDraws{},opaqueDraws{},alphaDraws{};
    uint16_t currentAppFlags{};uint32_t currentPlacementFlags{};bool normalFirst{},normalSecond{},windUpdated{};
    uint32_t seasonGroup{},wholeInstances{};bool seasonPopulationChanged{};
};
struct RecordInputs {
    uint32_t excludedCategories,ordinaryPrimitiveCount,contextFlags;
    bool ordinaryGeometryKind;
    San14::Render::ModelStencilDirective dispatcherStencil;
};
// Source D7AD80 publications are complete typed records, not initialized spare
// capacity. The native owned constructor starts each count at zero.
struct WindObjects {std::span<const uint8_t> point,spot,area;};

class GroundDetails {
    struct TypedBuffer {ComPtr<ID3D11Buffer> buffer;ComPtr<ID3D11ShaderResourceView> srv;ComPtr<ID3D11UnorderedAccessView> uav;};
    struct Volume {ComPtr<ID3D11Texture3D> texture;ComPtr<ID3D11ShaderResourceView> srv;ComPtr<ID3D11UnorderedAccessView> uav;};
    struct DrawData {uint32_t templateIndex,sourceDraw,queue,groupFlags;bool tree;
        std::vector<char> vertex,pixel;};
    ground_detail_source::Packet packet;
    std::optional<ground_detail_season_source::BankPair> seasons;
    uint32_t wholeInstances{};
    std::vector<ground_detail_source::PrimitiveSource> parts;
    std::vector<ground_detail_source::TemplateWorld> worlds;
    ground_detail_source::OrderedPopulation population;
    NativeModelInputs models;std::vector<DrawData> drawData;
    std::unordered_map<std::string,size_t> drawByPath;
    std::vector<ComPtr<ID3D11Buffer>> instanceStreams;
    std::array<ComPtr<ID3D11DepthStencilState>,2> depth,depthWithoutStencil;
    std::array<TypedBuffer,2> interactionVector,interactionHeight,phase;
    std::array<TypedBuffer,3> windObjects;
    std::array<uint32_t,3> windCounts{}; // D02BF0 source owner initialization
    std::array<Volume,2> volumes;
    ComPtr<ID3D11SamplerState> windSampler;
    ComPtr<ID3D11ComputeShader> windShader;ComPtr<ID3D11Buffer> windConstants;
    tree2_source::WorldRandom random;tree2_source::WindOwner windOwner;
    tree2_source::CurrentWind currentWind{};tree2_source::GraphicsHistory graphics;
    VisibilityPolicy visibilityPolicy;
    tree2_source::WindEnvironment currentEnvironment{};
    std::array<float,16> currentW2P{};std::array<float,12> currentWV{};
    ground_detail_source::WorldCommon currentCommon{};
    uint32_t commonSettingsFlags{},currentViewIndex{},effectiveExclusion{};
    bool ordinaryCamera{},viewPresent{},windOccurrence{},prepared=false,uploaded=false;
    Statistics statistics;

    static ComPtr<ID3D11Buffer> mutableBuffer(ID3D11Device* device,UINT bytes,UINT bind,
        bool dynamic,const void* initial=nullptr) {
        D3D11_BUFFER_DESC d{};d.ByteWidth=bytes;d.BindFlags=bind;
        d.Usage=dynamic?D3D11_USAGE_DYNAMIC:D3D11_USAGE_DEFAULT;
        d.CPUAccessFlags=dynamic?D3D11_CPU_ACCESS_WRITE:0;
        D3D11_SUBRESOURCE_DATA data{};data.pSysMem=initial;ComPtr<ID3D11Buffer> result;
        check(device->CreateBuffer(&d,initial?&data:nullptr,&result),"Ground details source mutable buffer");return result;
    }
    static TypedBuffer typed(ID3D11Device* device,UINT bytes,DXGI_FORMAT format,UINT elementBytes,
        bool gpuOutput,bool dynamic,const void* initial=nullptr) {
        TypedBuffer r;r.buffer=mutableBuffer(device,bytes,D3D11_BIND_SHADER_RESOURCE|(gpuOutput?D3D11_BIND_UNORDERED_ACCESS:0),dynamic,initial);
        D3D11_SHADER_RESOURCE_VIEW_DESC s{};s.Format=format;s.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;s.Buffer.NumElements=bytes/elementBytes;
        check(device->CreateShaderResourceView(r.buffer.Get(),&s,&r.srv),"Ground details typed SRV");
        if(gpuOutput){D3D11_UNORDERED_ACCESS_VIEW_DESC u{};u.Format=format;u.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;u.Buffer.NumElements=s.Buffer.NumElements;
            check(device->CreateUnorderedAccessView(r.buffer.Get(),&u,&r.uav),"Ground details typed UAV");}return r;
    }
    static Volume volume(ID3D11Device* device,const void* initial) {
        Volume r;D3D11_TEXTURE3D_DESC d{};d.Width=d.Height=d.Depth=64;d.MipLevels=1;
        d.Format=DXGI_FORMAT_R16G16B16A16_TYPELESS;d.Usage=D3D11_USAGE_DEFAULT;d.BindFlags=D3D11_BIND_SHADER_RESOURCE|D3D11_BIND_UNORDERED_ACCESS;
        D3D11_SUBRESOURCE_DATA data{};data.pSysMem=initial;data.SysMemPitch=64*8;data.SysMemSlicePitch=64*64*8;
        check(device->CreateTexture3D(&d,&data,&r.texture),"Ground details original wind volume");
        D3D11_SHADER_RESOURCE_VIEW_DESC s{};s.Format=DXGI_FORMAT_R16G16B16A16_FLOAT;s.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE3D;s.Texture3D.MipLevels=1;
        check(device->CreateShaderResourceView(r.texture.Get(),&s,&r.srv),"Ground details wind volume SRV");
        D3D11_UNORDERED_ACCESS_VIEW_DESC u{};u.Format=s.Format;u.ViewDimension=D3D11_UAV_DIMENSION_TEXTURE3D;u.Texture3D.WSize=64;
        check(device->CreateUnorderedAccessView(r.texture.Get(),&u,&r.uav),"Ground details wind volume UAV");return r;
    }
    static void upload(ID3D11DeviceContext* context,ID3D11Buffer* target,const void* data,size_t bytes) {
        if(!bytes)return;D3D11_MAPPED_SUBRESOURCE mapped{};
        check(context->Map(target,0,D3D11_MAP_WRITE_DISCARD,0,&mapped),"Ground details source stream upload");
        std::memcpy(mapped.pData,data,bytes);context->Unmap(target,0);
    }
    static std::span<uint8_t> bytes(std::vector<char>& data) {return {reinterpret_cast<uint8_t*>(data.data()),data.size()};}

public:
    GroundDetails()=default;
    GroundDetails(const GroundDetails&)=delete;
    GroundDetails& operator=(const GroundDetails&)=delete;

    void prepareResources(ID3D11Device* device,const fs::path& resourceDirectory,const fs::path& seasonRoot={}) {
        std::ifstream reference(resourceDirectory/"source-directory.txt");std::string relative;
        if(!std::getline(reference,relative)||relative.empty())throw std::runtime_error("Ground detail source directory import absent");
        loadOnce(device,resourceDirectory/fs::path(relative),seasonRoot);
        std::ifstream settings(resourceDirectory/"common-source.txt");
        settings>>visibilityPolicy.placementFlags>>visibilityPolicy.frustumKind>>visibilityPolicy.cullingMode>>visibilityPolicy.scanningOrder>>std::hex>>visibilityPolicy.baseAppFlags;
        if(!settings)throw std::runtime_error("Ground detail saved Common/App import absent");
    }

private:
    // The declared resource directory contains the model/population packet.
    void loadOnce(ID3D11Device* device,const fs::path& sourceReviewRoot,const fs::path& seasonRoot={}) {
        if(!models.draws.empty())throw std::runtime_error("Ground details already loaded; resource reload needs a new component");
        const auto source=sourceReviewRoot/"packet"/"gpu-templates";
        packet=ground_detail_source::read((sourceReviewRoot/"packet"/"producer"/"ground-detail-source.bin").wstring());
        if(!seasonRoot.empty())seasons=ground_detail_season_source::loadOnce(seasonRoot);
        parts=ground_detail_source::readPrimitiveOrder(packet,(source/"template-lods.txt").wstring());
        worlds=ground_detail_source::readTemplateWorld(packet,(source/"template-world.bin").wstring());
        models=loadNativeModels(device,source);
        struct Meta {uint32_t ti,draw,queue,group;};std::unordered_map<std::string,Meta> metadata;
        std::ifstream file(source/"draw-producers.txt");std::string path;Meta m;
        while(file>>path>>m.ti>>m.draw>>m.queue>>std::hex>>m.group>>std::dec)metadata.emplace(path,m);
        if(!file.eof())throw std::runtime_error("Ground details draw producer directory incomplete");
        for(size_t i=0;i<models.draws.size();++i){auto& model=models.draws[i];const auto info=metadata.at(model.identity);
            const auto& t=packet.templates.at(info.ti);const auto& original=t.draws.at(info.draw);
            const bool tree=original.providerClass==0xAB9C4087;
            DrawData d{info.ti,info.draw,info.queue,info.group,tree,::load(source/model.identity/"vs-b0.bin"),::load(source/model.identity/"ps-b0.bin")};
            if(d.vertex.size()!=(tree?272u:160u)||d.pixel.size()!=80||(info.queue!=54&&info.queue!=55)||((info.group>>1)&15)!=0)
                throw std::runtime_error("Ground details actual source shader/selector profile differs");
            model.vsConstants=mutableBuffer(device,UINT(d.vertex.size()),D3D11_BIND_CONSTANT_BUFFER,false,d.vertex.data());
            model.psConstants=mutableBuffer(device,80,D3D11_BIND_CONSTANT_BUFFER,false,d.pixel.data());
            drawByPath.emplace(model.identity,i);drawData.push_back(std::move(d));
        }
        if(metadata.size()!=models.draws.size())throw std::runtime_error("Ground details template metadata identity differs");
        std::vector<uint32_t> capacities(packet.templates.size());
        for(const auto& b:packet.banks){capacities[b.templateIndex]+=uint32_t(b.instances.size());wholeInstances+=uint32_t(b.instances.size());}
        if(seasons)capacities.assign(seasons->streamCapacity.begin(),seasons->streamCapacity.end());
        instanceStreams.resize(packet.templates.size()*3);
        for(size_t i=0;i<capacities.size();++i)if(capacities[i])for(size_t lod=0;lod<3;++lod)
            instanceStreams[i*3+lod]=mutableBuffer(device,capacities[i]*48,D3D11_BIND_VERTEX_BUFFER,true);
        for(size_t reverse=0;reverse<2;++reverse)for(size_t stencil=0;stencil<2;++stencil){
            D3D11_DEPTH_STENCIL_DESC d{};d.DepthEnable=TRUE;d.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ALL;
            d.DepthFunc=reverse?D3D11_COMPARISON_GREATER_EQUAL:D3D11_COMPARISON_LESS_EQUAL;
            d.StencilEnable=stencil!=0;d.StencilReadMask=d.StencilWriteMask=255;
            d.FrontFace.StencilFailOp=d.FrontFace.StencilDepthFailOp=D3D11_STENCIL_OP_KEEP;d.FrontFace.StencilPassOp=D3D11_STENCIL_OP_REPLACE;d.FrontFace.StencilFunc=D3D11_COMPARISON_ALWAYS;d.BackFace=d.FrontFace;
            auto& target=stencil?depth[reverse]:depthWithoutStencil[reverse];
            check(device->CreateDepthStencilState(&d,&target),"Ground details normal54/55 source depth/stencil");
        }
        const auto wind=sourceReviewRoot/"terrain-callbacks"/"wind"/"packet";
        auto vector=::load(wind/"interaction-vector-r8uint.bin"),height=::load(wind/"interaction-height-r16float.bin"),phases=::load(wind/"wind-phase-r32float.bin"),volumeBytes=::load(wind/"wind-and-ripple-rgba16float.bin");
        if(vector.size()!=1048576||height.size()!=524288||phases.size()!=2097152||volumeBytes.size()!=2097152)
            throw std::runtime_error("Ground details source wind initializer sizes differ");
        for(size_t side=0;side<2;++side){
            interactionVector[side]=typed(device,UINT(vector.size()),DXGI_FORMAT_R8_UINT,1,true,false,vector.data());
            interactionHeight[side]=typed(device,UINT(height.size()),DXGI_FORMAT_R16_FLOAT,2,true,false,height.data());
            phase[side]=typed(device,UINT(phases.size()),DXGI_FORMAT_R32_FLOAT,4,true,false,phases.data());
            volumes[side]=volume(device,volumeBytes.data());
        }
        // BF58A0 defaults -1 -> D5F170 capacity128; D02BF0 counts0.
        // No initialized spare record bytes were supplied by the source creator.
        windObjects[0]=typed(device,128*92,DXGI_FORMAT_R32_FLOAT,4,false,true);
        windObjects[1]=typed(device,128*48,DXGI_FORMAT_R32_FLOAT,4,false,true);
        windObjects[2]=typed(device,128*76,DXGI_FORMAT_R32_FLOAT,4,false,true);
        auto shader=::load(wind/"shader"/"2801580A-program-0.dxbc");
        check(device->CreateComputeShader(shader.data(),shader.size(),nullptr,&windShader),"Ground details original Tree2Wind CS");
        auto reset=::load(wind/"shader"/"shader-reset-b0-64.bin");
        if(reset.size()!=64)throw std::runtime_error("Ground details Tree2Wind64B reset absent");
        windConstants=mutableBuffer(device,64,D3D11_BIND_CONSTANT_BUFFER,false,reset.data());
        auto sampler=::load(wind/"wind-s8-s9-sampler-d3d11.bin");
        if(sampler.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Ground details actual wind sampler absent");
        D3D11_SAMPLER_DESC desc;std::memcpy(&desc,sampler.data(),sizeof(desc));
        check(device->CreateSamplerState(&desc,&windSampler),"Ground details original wind8/9 sampler");
    }
public:
    // Caller publishes its current point/spot/area population before prepare.
    // Empty spans retain the genuine owned zero counts; nonzero uploads use
    // original record widths and the actual default source capacity128.
    void publishWindObjects(ID3D11DeviceContext* context,const WindObjects& objects) {
        const std::array<std::span<const uint8_t>,3> rows{objects.point,objects.spot,objects.area};
        constexpr uint32_t sizes[3]{92,48,76};
        for(size_t i=0;i<3;++i){if(rows[i].size()%sizes[i]||rows[i].size()/sizes[i]>128)throw std::runtime_error("Current wind object publication exceeds source record/capacity");
            upload(context,windObjects[i].buffer.Get(),rows[i].data(),rows[i].size());windCounts[i]=uint32_t(rows[i].size()/sizes[i]);}
    }
    const Statistics& prepare(const FrameInputs& f) {
        if(models.draws.empty()||f.population.selectedView>=32)
            throw std::runtime_error("Ground details current Main/View input absent");
        statistics={};population={};uploaded=false;prepared=true;
        if(seasons){
            statistics.seasonPopulationChanged=ground_detail_season_source::installSettled(*seasons,packet,f.season);
            if(statistics.seasonPopulationChanged){
                wholeInstances=0;for(const auto& bank:packet.banks)wholeInstances+=uint32_t(bank.instances.size());
            }
            statistics.seasonGroup=seasons->currentGroup;
        }
        statistics.wholeInstances=wholeInstances;
        currentEnvironment=f.environment;
        const auto matrices=cameraMatrices(f.camera);currentW2P=matrices.worldToProjection;currentWV=matrices.worldToView;
        currentViewIndex=f.population.selectedView;commonSettingsFlags=f.commonSettingsFlags;
        currentCommon={f.view.present,f.view.categoryFlags,visibilityPolicy.placementFlags,f.view.excludedCategories,visibilityPolicy.frustumKind};
        statistics.currentAppFlags=ground_detail_source::normalAppTail(visibilityPolicy.baseAppFlags,f.display.mode,f.display.zoomLevel);
        bool selected=f.display.overlaySelected;uint32_t overlay=f.display.placementOverlay;
        if(f.display.hostPlacementCallbackExecuted){overlay=ground_detail_source::hostPlacementOverlay(selected?overlay:0x01FFFFFFu,statistics.currentAppFlags);selected=true;}
        currentCommon.flags14=ground_detail_source::placementAux14(currentCommon.flags14,selected,overlay);
        statistics.currentPlacementFlags=currentCommon.flags14;effectiveExclusion=ground_detail_source::effectiveExclusion(currentCommon);
        viewPresent=(f.population.registeredViews&(1u<<f.population.selectedView))!=0;
        ordinaryCamera=currentEnvironment.ordinaryCamera;
        windOccurrence=viewPresent&&(f.population.successfulViews&(1u<<f.population.selectedView))!=0&&(f.population.selectedView==0||f.population.selectedView==6);
        if(windOccurrence)currentWind=tree2_source::advance(windOwner,random,currentEnvironment,f.clock);
        if((f.commonSettingsFlags&0x200000u)!=0)
            throw std::runtime_error("Ground details current interaction writer requires its actual shader/owner publication interface");
        for(auto d:ground_detail_source::dispatch(f.population.firstViews,f.population.secondViews,f.population.shadowViews,f.population.registeredViews,currentCommon))if(d.view==f.population.selectedView){
            statistics.normalFirst=(d.first&f.population.successfulViews)!=0;statistics.normalSecond=(d.second&f.population.successfulViews)!=0;
        }
        if(!statistics.normalFirst&&!statistics.normalSecond)return statistics;
        auto view=ground_detail_source::prepare(f.camera,visibilityPolicy.cullingMode,visibilityPolicy.scanningOrder);
        population=ground_detail_source::aggregate(packet,view,ground_detail_source::select(packet,view),parts,worlds);
        for(const auto& stream:population.streams)statistics.selectedInstances+=uint32_t(stream.rows.size());
        statistics.preparedPrimitives=uint32_t(population.submissions.size());
        for(const auto& s:population.submissions)if(!population.streams[s.streamIndex].rows.empty()){
            ++statistics.nonemptyDraws;const auto& d=drawData[drawByPath.at(s.primitive.relativePath)];
            if(d.queue==54)++statistics.opaqueDraws;else ++statistics.alphaDraws;
        }return statistics;
    }
    uint32_t preparedExclusion() const noexcept {return effectiveExclusion;}
    uint32_t preparedCountForRecord(uint32_t excludedCategories) const {
        if(!prepared||!statistics.normalFirst||(excludedCategories&effectiveExclusion)!=0)return 0;
        return statistics.preparedPrimitives;
    }
    // Call before any normal draw and before publishing the current wind side.
    void update(ID3D11DeviceContext* context) {
        if(!prepared||uploaded)throw std::runtime_error("Ground details requires one update after current prepare");
        tree2_source::WindAdmissionInputs a{currentViewIndex,commonSettingsFlags,viewPresent,currentCommon.present,ordinaryCamera,bool(windShader),
            bool(windObjects[0].srv),bool(windObjects[1].srv),bool(windObjects[2].srv),bool(volumes[0].uav)&&bool(volumes[1].uav),bool(phase[0].uav)&&bool(phase[1].uav),currentEnvironment.windExtent};
        auto next=graphics;
        if(windOccurrence&&tree2_source::beginWindUpdate(next,currentEnvironment,0,a)){
            std::array<ID3D11ShaderResourceView*,16> none{};context->VSSetShaderResources(0,16,none.data());
            std::array<uint8_t,64> cb{};tree2_source::writeComputeFrame(cb,currentEnvironment,currentWind,next,0,windCounts[0],windCounts[1],windCounts[2]);
            context->UpdateSubresource(windConstants.Get(),0,nullptr,cb.data(),0,0);auto c=windConstants.Get();context->CSSetConstantBuffers(0,1,&c);
            const auto side=next.phaseSide[0];ID3D11ShaderResourceView* inputs[4]{windObjects[0].srv.Get(),windObjects[1].srv.Get(),windObjects[2].srv.Get(),phase[1-side].srv.Get()};
            ID3D11UnorderedAccessView* outputs[3]{volumes[0].uav.Get(),volumes[1].uav.Get(),phase[side].uav.Get()};
            context->CSSetShader(windShader.Get(),nullptr,0);context->CSSetShaderResources(0,4,inputs);context->CSSetUnorderedAccessViews(0,3,outputs,nullptr);
            context->Dispatch(8,8,8); // original DXBC threads8^3 and source64^3 coverage
            ID3D11UnorderedAccessView* nullUavs[3]{};ID3D11ShaderResourceView* nullSrvs[4]{};
            context->CSSetUnorderedAccessViews(0,3,nullUavs,nullptr);context->CSSetShaderResources(0,4,nullSrvs);context->CSSetShader(nullptr,nullptr,0);
            graphics=next;statistics.windUpdated=true;
        }
        for(size_t i=0;i<population.streams.size();++i){const auto& rows=population.streams[i].rows;
            if(!rows.empty())upload(context,instanceStreams[i].Get(),rows.data(),rows.size()*48);
        }
        for(const auto& submission:population.submissions){if(population.streams[submission.streamIndex].rows.empty())continue;
            const auto i=drawByPath.at(submission.primitive.relativePath);auto& d=drawData[i];auto& model=models.draws[i];
            std::memcpy(d.vertex.data(),currentW2P.data(),64);std::memcpy(d.pixel.data()+16,currentWV.data(),48);
            if(d.tree)tree2_source::writeVsFrame(bytes(d.vertex),currentEnvironment,currentWind,graphics,d.groupFlags);
            context->UpdateSubresource(model.vsConstants.Get(),0,nullptr,d.vertex.data(),0,0);context->UpdateSubresource(model.psConstants.Get(),0,nullptr,d.pixel.data(),0,0);
        }
        uploaded=true;
    }
    // BF8610 belongs after the original render-node work, not prepare/update.
    void finishRenderNode() {if(windOccurrence)tree2_source::finishRenderNode(graphics,currentWind);}
    void draw(ID3D11DeviceContext* context,uint32_t node54or55,const RecordInputs& record,
        ID3D11RenderTargetView* const* targets,ID3D11DepthStencilView* dsv,const D3D11_VIEWPORT& viewport) {
        if(!uploaded)throw std::runtime_error("Ground details draw needs current uploaded streams/CB");
        if(node54or55!=54&&node54or55!=55)throw std::runtime_error("Ground details supports normal54/55 only");
        if(!statistics.normalFirst||!record.ordinaryGeometryKind||!record.dispatcherStencil.submit||!record.ordinaryPrimitiveCount||(record.excludedCategories&effectiveExclusion)!=0)return;
        const uint32_t reference=record.dispatcherStencil.reference,reverse=(record.contextFlags>>29)&1;
        context->OMSetRenderTargets(5,targets,dsv);context->RSSetViewports(1,&viewport);context->OMSetBlendState(models.blend.Get(),nullptr,~0u);
        context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
        for(const auto& s:population.submissions){const auto& stream=population.streams[s.streamIndex];if(stream.rows.empty())continue;
            const auto i=drawByPath.at(s.primitive.relativePath);const auto& d=drawData[i];const auto& model=models.draws[i];if(d.queue!=node54or55)continue;
            std::array<ID3D11Buffer*,2> vb{model.vertexBuffers[0].Get(),instanceStreams[s.streamIndex].Get()};
            if(!model.instanceStreams||model.vertexBuffers.size()!=2||model.strides[1]!=48)throw std::runtime_error("Prepared ground detail lacks actual48B stream1");
            UINT strides[2]{model.strides[0],48},offsets[2]{};context->IASetVertexBuffers(0,2,vb.data(),strides,offsets);
            context->IASetIndexBuffer(model.indices.Get(),model.indexBits==16?DXGI_FORMAT_R16_UINT:DXGI_FORMAT_R32_UINT,0);
            context->IASetInputLayout(model.layout.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY(model.topology));
            context->VSSetShader(model.vs.Get(),nullptr,0);context->PSSetShader(model.ps.Get(),nullptr,0);
            auto vc=model.vsConstants.Get(),pc=model.psConstants.Get();context->VSSetConstantBuffers(0,1,&vc);context->PSSetConstantBuffers(0,1,&pc);
            std::array<ID3D11ShaderResourceView*,16> ps{};std::array<ID3D11SamplerState*,16> samplers{};
            for(size_t n=0;n<16;++n){ps[n]=model.textures[n].Get();samplers[n]=model.samplers[n].Get();}
            context->PSSetShaderResources(0,16,ps.data());context->PSSetSamplers(0,16,samplers.data());
            std::array<ID3D11ShaderResourceView*,10> vs{};std::array<ID3D11SamplerState*,10> vsSamplers{};
            if(d.tree){vs[0]=interactionVector[graphics.interactionSide].srv.Get();vs[1]=interactionHeight[graphics.interactionSide].srv.Get();
                vs[3]=phase[graphics.phaseSide[0]].srv.Get();vs[8]=volumes[0].srv.Get();vs[9]=volumes[1].srv.Get();vsSamplers[8]=vsSamplers[9]=windSampler.Get();}
            context->VSSetShaderResources(0,10,vs.data());context->VSSetSamplers(0,10,vsSamplers.data());
            context->RSSetState(model.raster.Get());context->OMSetDepthStencilState(record.dispatcherStencil.enabled?depth[reverse].Get():depthWithoutStencil[reverse].Get(),reference);
            context->DrawIndexedInstanced(model.indexCount,UINT(stream.rows.size()),model.indexStart,model.baseVertex,0);
        }
        std::array<ID3D11ShaderResourceView*,16> none{};context->VSSetShaderResources(0,16,none.data());context->PSSetShaderResources(0,16,none.data());
        context->OMSetDepthStencilState(depthWithoutStencil[reverse].Get(),reference); // D48D05 disables only stencil
    }
    const Statistics& currentStatistics() const {return statistics;}
};
} // namespace ground_details
