#pragma once
#include "CurrentEffectDirect.h"
#include "../Lighting/CurrentClusterLights.h"

namespace current_effect {
struct PointLightSource {
    UINT capacity{},mainCapacity{},genericAddress{},mainGenericAddress{},extraFlags{},attenuation{};
    float occlusion{},scatter[2]{},exposure{};std::int64_t ordinal{};
};
struct PointLightJob {
    PointLightSource source;
    ID3D11ShaderResourceView* objects{};ID3D11ShaderResourceView* linked{};ID3D11ShaderResourceView* main{};
};
struct LightingBuffer {
    ComPtr<ID3D11Buffer> owner;ComPtr<ID3D11ShaderResourceView> view;ComPtr<ID3D11UnorderedAccessView> unordered;
};
class LightingQueue {
    std::map<std::uint64_t,PointLightSource> sources;
    ComPtr<ID3D11ComputeShader> projection;
    std::map<UINT,ComPtr<ID3D11ComputeShader>> clusterPrograms;
    ComPtr<ID3D11Buffer> projectionConstants,resetConstants,cullConstants;
    LightingBuffer positions,attributes,extra,scatter,allocator;
    UINT allocated{};bool configured{};UINT resetProgram{},cullProgram{},indexFormat{},headerElements{};
    UINT resetGroups[3]{},cullGroups[3]{};
    std::array<std::uint8_t,16> resetBytes{};std::array<std::uint8_t,400> cullBytes{};
    NativeSourceClusterLights frame;
    static LightingBuffer typed(ID3D11Device* device,UINT elements,DXGI_FORMAT format,UINT width,bool shaderResource=true) {
        if(!elements||elements>std::numeric_limits<UINT>::max()/width)throw std::runtime_error("Effect point-light buffer size");
        D3D11_BUFFER_DESC desc{};desc.ByteWidth=elements*width;desc.Usage=D3D11_USAGE_DEFAULT;
        desc.BindFlags=D3D11_BIND_UNORDERED_ACCESS|(shaderResource?D3D11_BIND_SHADER_RESOURCE:0);LightingBuffer result;
        check(device->CreateBuffer(&desc,nullptr,&result.owner),"Effect actual light queue buffer");
        D3D11_UNORDERED_ACCESS_VIEW_DESC uav{};uav.Format=format;uav.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;uav.Buffer.NumElements=elements;
        check(device->CreateUnorderedAccessView(result.owner.Get(),&uav,&result.unordered),"Effect actual light queue UAV");
        if(shaderResource){D3D11_SHADER_RESOURCE_VIEW_DESC srv{};srv.Format=format;srv.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;srv.Buffer.NumElements=elements;
            check(device->CreateShaderResourceView(result.owner.Get(),&srv,&result.view),"Effect actual light queue SRV");}
        return result;
    }
    static ComPtr<ID3D11Buffer> cb(ID3D11Device* device,UINT size) {
        D3D11_BUFFER_DESC desc{};desc.ByteWidth=size;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        ComPtr<ID3D11Buffer> result;check(device->CreateBuffer(&desc,nullptr,&result),"Effect light current CB");return result;
    }
public:
    void setSource(ID3D11Device* device,std::uint64_t id,Bytes bytes,std::int64_t ordinal) {
        if(bytes.empty()){sources.erase(id);return;}
        if(bytes.size()<64||value<UINT>(bytes,0)!=0x31504C43||value<UINT>(bytes,4)!=1)throw std::runtime_error("Effect CLP1 source ABI");
        PointLightSource source;source.capacity=value<UINT>(bytes,8);source.mainCapacity=value<UINT>(bytes,12);
        source.genericAddress=value<UINT>(bytes,16);source.mainGenericAddress=value<UINT>(bytes,20);
        source.extraFlags=value<UINT>(bytes,24);source.attenuation=value<UINT>(bytes,28);source.occlusion=value<float>(bytes,32);
        source.scatter[0]=value<float>(bytes,36);source.scatter[1]=value<float>(bytes,40);source.exposure=value<float>(bytes,44);source.ordinal=ordinal;
        if(!source.capacity||!source.mainCapacity||source.extraFlags!=7||source.attenuation!=142)throw std::runtime_error("Effect point-light queue packing");
        if(!projection){const UINT offset=value<UINT>(bytes,48),length=value<UINT>(bytes,52);require(bytes,offset,length);
            ComPtr<ID3DBlob> code,error;const auto hr=D3DCompile(bytes.data()+offset,length,"project-linked-point-lights",nullptr,nullptr,"ProjectPointLights","cs_5_0",D3DCOMPILE_ENABLE_STRICTNESS,0,&code,&error);
            if(FAILED(hr)){std::string message="Effect point-light projection compile failed";if(error)message.append(": ").append(static_cast<const char*>(error->GetBufferPointer()),error->GetBufferSize());throw std::runtime_error(message);}
            check(device->CreateComputeShader(code->GetBufferPointer(),code->GetBufferSize(),nullptr,&projection),"Effect actual point-light projection");projectionConstants=cb(device,48);}
        sources[id]=source;
    }
    void release(std::uint64_t id) {sources.erase(id);}
    const auto& registrations() const {return sources;}
    void configure(ID3D11Device* device,Bytes bytes) {
        if(bytes.size()<128||value<UINT>(bytes,0)!=0x31434C43||value<UINT>(bytes,4)!=1)throw std::runtime_error("Effect CLC1 frame ABI");
        auto program=[&](UINT at,UINT programId){auto& shader=clusterPrograms[programId];if(shader)return;const auto off=value<UINT>(bytes,at),len=value<UINT>(bytes,at+4);require(bytes,off,len);
            check(device->CreateComputeShader(bytes.data()+off,len,nullptr,&shader),"Effect normal CLC program");};
        resetProgram=value<UINT>(bytes,72);cullProgram=value<UINT>(bytes,76);program(8,resetProgram);program(16,cullProgram);
        for(const auto part:{std::array<UINT,3>{24,16,0},std::array<UINT,3>{32,400,1}}){const UINT off=value<UINT>(bytes,part[0]),len=value<UINT>(bytes,part[0]+4);require(bytes,off,len);
            if(len!=part[1])throw std::runtime_error("Effect current CLC constant extent");if(part[2])std::memcpy(cullBytes.data(),bytes.data()+off,len);else std::memcpy(resetBytes.data(),bytes.data()+off,len);}
        for(UINT i=0;i<3;++i){resetGroups[i]=value<UINT>(bytes,40+i*4);cullGroups[i]=value<UINT>(bytes,52+i*4);if(resetGroups[i]>65535||cullGroups[i]>65535)throw std::runtime_error("Effect CLC dispatch size");}
        headerElements=value<UINT>(bytes,64);indexFormat=value<UINT>(bytes,68);
        if(indexFormat!=DXGI_FORMAT_R16_UINT&&indexFormat!=DXGI_FORMAT_R32_UINT)throw std::runtime_error("Effect CLC index view format");
        std::memcpy(frame.depthParameters.data(),bytes.data()+80,16);std::memcpy(frame.patchCounts.data(),bytes.data()+96,8);
        frame.scaling=value<UINT>(bytes,104)?std::optional<float>(value<float>(bytes,108)):std::nullopt;
        frame.fadeDistance=value<UINT>(bytes,112)?std::optional<std::array<float,2>>(std::array<float,2>{value<float>(bytes,116),value<float>(bytes,120)}):std::nullopt;
        if(!resetConstants){resetConstants=cb(device,16);cullConstants=cb(device,400);allocator=typed(device,1,DXGI_FORMAT_R32_UINT,4,false);}
        configured=true;
    }
    bool hasConfiguration() const {return configured;}
    const NativeSourceClusterLights& current() const {return frame;}
    void prepare(ID3D11Device* device,ID3D11DeviceContext* context,std::vector<PointLightJob> jobs,
        ID3D11UnorderedAccessView* indices,ID3D11ShaderResourceView* indexView) {
        frame.computeSucceeded=false;if(!configured)return;
        std::sort(jobs.begin(),jobs.end(),[](const auto& a,const auto& b){return a.source.ordinal<b.source.ordinal;});
        std::uint64_t total=0;for(const auto& job:jobs){if(!job.objects||!job.linked||!job.main)throw std::runtime_error("Effect light projection has no real allocation views");total+=job.source.capacity;}
        if(total>65535)throw std::runtime_error("Effect point-light source count exceeds its ordinary index representation");
        const UINT count=UINT(total);if(!count&&!allocated)return;
        if(count>allocated){allocated=count;positions=typed(device,count,DXGI_FORMAT_R32G32B32A32_FLOAT,16);
            // The owned typed buffer stores a padded float4 per attribute;
            // normal Buffer<float3> consumers read its unchanged xyz lanes.
            attributes=typed(device,count*3,DXGI_FORMAT_R32G32B32A32_FLOAT,16);
            extra=typed(device,count*3,DXGI_FORMAT_R32_UINT,4);scatter=typed(device,count*2,DXGI_FORMAT_R32_FLOAT,4);}
        if(!indices||!indexView)throw std::runtime_error("Effect light queue has no actual node71 index owner");
        D3D11_UNORDERED_ACCESS_VIEW_DESC indexDesc{};indices->GetDesc(&indexDesc);
        if(indexDesc.Format!=DXGI_FORMAT(indexFormat)||headerElements>indexDesc.Buffer.NumElements)throw std::runtime_error("Effect CLC retained index declaration mismatch");
        // Previous Main's pixel bindings may retain these same queue views.
        // Release them before this frame writes the buffers as UAVs.
        std::array<ID3D11ShaderResourceView*,6> empty{};context->PSSetShaderResources(16,6,empty.data());
        ComputeScope scope(context);ComputeScope::clear(context);UINT first=0;
        for(const auto& job:jobs){const auto& s=job.source;std::array<std::uint8_t,48> constants{};
            const UINT integers[]{s.capacity,s.genericAddress,s.mainCapacity,s.mainGenericAddress,s.extraFlags,3,s.attenuation};std::memcpy(constants.data(),integers,sizeof(integers));
            std::memcpy(constants.data()+28,&s.occlusion,4);std::memcpy(constants.data()+32,s.scatter,8);std::memcpy(constants.data()+40,&s.exposure,4);std::memcpy(constants.data()+44,&first,4);
            context->UpdateSubresource(projectionConstants.Get(),0,nullptr,constants.data(),0,0);auto buffer=projectionConstants.Get();context->CSSetConstantBuffers(0,1,&buffer);
            ID3D11ShaderResourceView* srvs[]{job.objects,job.linked,job.main};context->CSSetShaderResources(0,3,srvs);
            ID3D11UnorderedAccessView* uavs[]{positions.unordered.Get(),attributes.unordered.Get(),extra.unordered.Get(),scatter.unordered.Get()};context->CSSetUnorderedAccessViews(0,4,uavs,nullptr);
            context->CSSetShader(projection.Get(),nullptr,0);context->Dispatch((s.capacity+63)/64,1,1);ComputeScope::clear(context);first+=s.capacity;}
        context->UpdateSubresource(resetConstants.Get(),0,nullptr,resetBytes.data(),0,0);auto buffer=resetConstants.Get();context->CSSetConstantBuffers(0,1,&buffer);
        auto alloc=allocator.unordered.Get();context->CSSetUnorderedAccessViews(1,1,&alloc,nullptr);context->CSSetShader(clusterPrograms.at(resetProgram).Get(),nullptr,0);context->Dispatch(resetGroups[0],resetGroups[1],resetGroups[2]);ComputeScope::clear(context);
        std::memcpy(cullBytes.data()+48,&count,4);UINT spots=0;std::memcpy(cullBytes.data()+52,&spots,4);
        context->UpdateSubresource(cullConstants.Get(),0,nullptr,cullBytes.data(),0,0);buffer=cullConstants.Get();context->CSSetConstantBuffers(0,1,&buffer);
        ID3D11ShaderResourceView* positionViews[]{positions.view.Get(),nullptr};context->CSSetShaderResources(0,2,positionViews);
        ID3D11UnorderedAccessView* outputs[]{indices,allocator.unordered.Get()};context->CSSetUnorderedAccessViews(0,2,outputs,nullptr);
        context->CSSetShader(clusterPrograms.at(cullProgram).Get(),nullptr,0);context->Dispatch(cullGroups[0],cullGroups[1],cullGroups[2]);ComputeScope::clear(context);
        frame.resources={indexView,positions.view.Get(),attributes.view.Get(),extra.view.Get(),nullptr,nullptr};
        frame.pointCount=count;frame.spotCount=0;frame.computeSucceeded=true;
    }
};
}
