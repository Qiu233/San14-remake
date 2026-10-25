#pragma once
#include "CurrentEffectRendererResources.h"
#include "../../Interop/CurrentEffectDirectInput.h"
#include <d3dcompiler.h>

namespace current_effect {
struct ComputeScope {
    ID3D11DeviceContext* c;ComPtr<ID3D11ComputeShader> shader;
    std::array<ID3D11Buffer*,14> constants{};
    std::array<ID3D11ShaderResourceView*,128> resources{};
    std::array<ID3D11UnorderedAccessView*,8> unordered{};
    explicit ComputeScope(ID3D11DeviceContext* value):c(value) {
        c->CSGetShader(&shader,nullptr,nullptr);c->CSGetConstantBuffers(0,14,constants.data());
        c->CSGetShaderResources(0,128,resources.data());c->CSGetUnorderedAccessViews(0,8,unordered.data());
    }
    static void clear(ID3D11DeviceContext* c) {
        std::array<ID3D11ShaderResourceView*,128> srvs{};std::array<ID3D11UnorderedAccessView*,8> uavs{};
        c->CSSetShaderResources(0,128,srvs.data());c->CSSetUnorderedAccessViews(0,8,uavs.data(),nullptr);
    }
    ~ComputeScope() {
        clear(c);c->CSSetShader(shader.Get(),nullptr,0);c->CSSetConstantBuffers(0,14,constants.data());
        c->CSSetShaderResources(0,128,resources.data());c->CSSetUnorderedAccessViews(0,8,unordered.data(),nullptr);
        for(auto* p:constants)if(p)p->Release();for(auto* p:resources)if(p)p->Release();for(auto* p:unordered)if(p)p->Release();
    }
};
struct DirectBuffer {
    UINT bytes{},usage{};ComPtr<ID3D11Buffer> buffer;
    ComPtr<ID3D11ShaderResourceView> view;ComPtr<ID3D11UnorderedAccessView> unordered;
};
class DirectAllocation {
    std::map<UINT,DirectBuffer> buffers;
    std::map<UINT,ComPtr<ID3D11ComputeShader>> programs;
    std::map<std::uint64_t,ComPtr<ID3D11Buffer>> constants;
    ComPtr<ID3D11Buffer> liveReadback;bool readPending{};int lastLive{-1};
    std::uint64_t birthRevision{},readBirthRevision{};
    static std::string string(Bytes bytes,UINT offset,UINT length) {
        require(bytes,offset,length);std::string text(reinterpret_cast<const char*>(bytes.data()+offset),length);
        if(text.empty()||text.find('\0')!=std::string::npos)throw std::runtime_error("Direct Effect shader string");return text;
    }
public:
    San14EffectDirectHeaderV1 declaration{};
    DirectAllocation(ID3D11Device* device,Bytes bytes) {
        if(device->GetCreationFlags()&D3D11_CREATE_DEVICE_SINGLETHREADED)throw std::runtime_error("Direct Effect worker requires a multithread-capable device");
        declaration=value<San14EffectDirectHeaderV1>(bytes,0);const auto& h=declaration;
        if(h.magic!=0x31444543||h.version!=1||!h.requiredKernels||(h.requiredKernels&~0x1FFu))
            throw std::runtime_error("Direct Effect CED1 version or kernels");
        for(const auto& row:records<San14EffectDirectBufferV1>(bytes,h.bufferOffset,h.bufferCount)) {
            if(!row.byteLength||(row.byteLength&3)||row.initialBytes>row.byteLength||!row.usage||(row.usage&~15u)||row.format>3)
                throw std::runtime_error("Direct Effect buffer declaration");
            require(bytes,row.initialOffset,row.initialBytes);
            D3D11_BUFFER_DESC desc{};desc.ByteWidth=row.byteLength;desc.Usage=D3D11_USAGE_DEFAULT;
            if(row.usage&1)desc.BindFlags|=D3D11_BIND_SHADER_RESOURCE;
            if(row.usage&2)desc.BindFlags|=D3D11_BIND_UNORDERED_ACCESS;
            if(row.usage&4)desc.BindFlags|=D3D11_BIND_VERTEX_BUFFER;
            if(row.usage&8)desc.MiscFlags|=D3D11_RESOURCE_MISC_DRAWINDIRECT_ARGS;
            if(row.structureStride){if(row.byteLength%row.structureStride)throw std::runtime_error("Direct Effect structured buffer stride");desc.MiscFlags|=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;desc.StructureByteStride=row.structureStride;}
            else if(row.format==0)desc.MiscFlags|=D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS;
            std::vector<std::uint8_t> initial(row.byteLength);if(row.initialBytes)std::memcpy(initial.data(),bytes.data()+row.initialOffset,row.initialBytes);
            D3D11_SUBRESOURCE_DATA data{};data.pSysMem=initial.data();DirectBuffer buffer;buffer.bytes=row.byteLength;buffer.usage=row.usage;
            check(device->CreateBuffer(&desc,&data,&buffer.buffer),"Direct Effect actual buffer");
            const bool raw=!row.structureStride&&row.format==0;
            const DXGI_FORMAT format=row.structureStride?DXGI_FORMAT_UNKNOWN:raw?DXGI_FORMAT_R32_TYPELESS:
                row.format==1?DXGI_FORMAT_R32_UINT:row.format==2?DXGI_FORMAT_R32_FLOAT:DXGI_FORMAT_R32G32B32A32_FLOAT;
            const UINT width=row.structureStride?row.structureStride:row.format==3?16:4;
            if(row.byteLength%width)throw std::runtime_error("Direct Effect typed buffer length");
            if(row.usage&1){D3D11_SHADER_RESOURCE_VIEW_DESC view{};view.Format=format;view.ViewDimension=raw?D3D11_SRV_DIMENSION_BUFFEREX:D3D11_SRV_DIMENSION_BUFFER;
                view.BufferEx.NumElements=row.byteLength/width;view.BufferEx.Flags=raw?D3D11_BUFFEREX_SRV_FLAG_RAW:0;
                check(device->CreateShaderResourceView(buffer.buffer.Get(),&view,&buffer.view),"Direct Effect buffer SRV");}
            if(row.usage&2){D3D11_UNORDERED_ACCESS_VIEW_DESC view{};view.Format=format;view.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;view.Buffer.NumElements=row.byteLength/width;view.Buffer.Flags=raw?D3D11_BUFFER_UAV_FLAG_RAW:0;
                check(device->CreateUnorderedAccessView(buffer.buffer.Get(),&view,&buffer.unordered),"Direct Effect buffer UAV");}
            if(!buffers.emplace(row.id,std::move(buffer)).second)throw std::runtime_error("Direct Effect duplicate buffer");
        }
        UINT available=0;
        for(const auto& row:records<San14EffectDirectProgramV1>(bytes,h.programOffset,h.programCount)) {
            if(row.kernel>8)throw std::runtime_error("Direct Effect kernel code");
            const auto source=string(bytes,row.sourceOffset,row.sourceBytes),entry=string(bytes,row.entryOffset,row.entryBytes),profile=string(bytes,row.profileOffset,row.profileBytes);
            if(profile!="cs_5_0")throw std::runtime_error("Direct Effect compute profile");
            ComPtr<ID3DBlob> code,error;HRESULT result=D3DCompile(source.data(),source.size(),"project-direct-effect",nullptr,nullptr,entry.c_str(),profile.c_str(),D3DCOMPILE_ENABLE_STRICTNESS,0,&code,&error);
            if(FAILED(result)){std::string message="Direct Effect HLSL compile failed";if(error)message.append(": ").append(static_cast<const char*>(error->GetBufferPointer()),error->GetBufferSize());throw std::runtime_error(message);}
            ComPtr<ID3D11ComputeShader> shader;check(device->CreateComputeShader(code->GetBufferPointer(),code->GetBufferSize(),nullptr,&shader),"Direct Effect actual compute shader");
            if(!programs.emplace(row.kernel,std::move(shader)).second)throw std::runtime_error("Direct Effect duplicate kernel");available|=1u<<row.kernel;
        }
        if((available&h.requiredKernels)!=h.requiredKernels)throw std::runtime_error("Direct Effect required actual programs absent");
        const auto& live=buffers.at(8);if(live.bytes!=4||!(live.usage&2))throw std::runtime_error("Direct Effect live-count buffer");
        D3D11_BUFFER_DESC staging{};staging.ByteWidth=4;staging.Usage=D3D11_USAGE_STAGING;staging.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
        check(device->CreateBuffer(&staging,nullptr,&liveReadback),"Direct Effect live-count readback");
    }
    ID3D11Buffer* vertex() const {const auto& b=buffers.at(declaration.vertexBufferId);if(!(b.usage&4))throw std::runtime_error("Direct Effect vertex output absent");return b.buffer.Get();}
    ID3D11Buffer* arguments() const {const auto& b=buffers.at(declaration.argumentBufferId);if(!(b.usage&8)||declaration.indirectOffset>b.bytes||20>b.bytes-declaration.indirectOffset)throw std::runtime_error("Direct Effect draw argument range");return b.buffer.Get();}
    int live(ID3D11DeviceContext* context) {
        if(readPending){D3D11_MAPPED_SUBRESOURCE mapped{};const auto hr=context->Map(liveReadback.Get(),0,D3D11_MAP_READ,D3D11_MAP_FLAG_DO_NOT_WAIT,&mapped);
            if(hr==DXGI_ERROR_WAS_STILL_DRAWING)return -1;check(hr,"Direct Effect actual live-count map");UINT count;std::memcpy(&count,mapped.pData,4);context->Unmap(liveReadback.Get(),0);readPending=false;
            // A prior empty result remains valid across motion-only updates,
            // but a later birth invalidates it. Waiting for an exact frame here
            // would starve feedback while the owner advances every frame.
            if(readBirthRevision==birthRevision)lastLive=count?1:0;
            else{lastLive=-1;context->CopyResource(liveReadback.Get(),buffers.at(8).buffer.Get());readBirthRevision=birthRevision;readPending=true;}}
        return lastLive;
    }
    void submit(ID3D11Device* device,ID3D11DeviceContext* context,Bytes bytes) {
        const auto h=value<San14EffectDirectCommandHeaderV1>(bytes,0);
        if(h.magic!=0x31434543||h.version!=1)throw std::runtime_error("Direct Effect CEC1 version");
        const auto uploads=records<San14EffectDirectUploadV1>(bytes,h.uploadOffset,h.uploadCount);
        const auto dispatches=records<San14EffectDirectDispatchV1>(bytes,h.dispatchOffset,h.dispatchCount);
        // Validate the complete command batch before its first GPU mutation.
        for(const auto& u:uploads){const auto& buffer=buffers.at(u.bufferId);require(bytes,u.byteOffset,u.byteLength);if(u.bufferOffset>buffer.bytes||u.byteLength>buffer.bytes-u.bufferOffset)throw std::runtime_error("Direct Effect upload range");}
        for(const auto& d:dispatches){(void)programs.at(d.kernel);if(d.groupsX>65535||d.groupsY>65535||d.groupsZ>65535)throw std::runtime_error("Direct Effect dispatch group range");
            std::array<bool,14> slots{};for(const auto& cb:records<San14EffectDirectConstantV1>(bytes,d.constantOffset,d.constantCount)){require(bytes,cb.byteOffset,cb.byteLength);if(cb.slot>=14||slots[cb.slot]||!cb.byteLength||(cb.byteLength&15)||cb.byteLength>65536)throw std::runtime_error("Direct Effect compute constant buffer");slots[cb.slot]=true;}
            std::array<bool,128> srvs{};std::array<bool,8> uavs{};
            for(const auto& r:records<San14EffectDirectBindingV1>(bytes,d.resourceOffset,d.resourceCount))if(r.slot>=128||srvs[r.slot]||!buffers.at(r.bufferId).view)throw std::runtime_error("Direct Effect SRV binding");else srvs[r.slot]=true;
            for(const auto& r:records<San14EffectDirectBindingV1>(bytes,d.unorderedOffset,d.unorderedCount))if(r.slot>=8||uavs[r.slot]||!buffers.at(r.bufferId).unordered)throw std::runtime_error("Direct Effect UAV binding");else uavs[r.slot]=true;
        }
        ComputeScope scope(context);ComputeScope::clear(context);
        for(const auto& u:uploads)if(u.byteLength){D3D11_BOX box{u.bufferOffset,0,0,u.bufferOffset+u.byteLength,1,1};context->UpdateSubresource(buffers.at(u.bufferId).buffer.Get(),0,&box,bytes.data()+u.byteOffset,0,0);}
        bool advanced=false;
        for(const auto& d:dispatches){ComputeScope::clear(context);context->CSSetShader(programs.at(d.kernel).Get(),nullptr,0);
            for(const auto& cb:records<San14EffectDirectConstantV1>(bytes,d.constantOffset,d.constantCount)){
                const std::uint64_t id=(std::uint64_t(cb.slot)<<32)|cb.byteLength;auto& buffer=constants[id];
                if(!buffer){D3D11_BUFFER_DESC desc{};desc.ByteWidth=cb.byteLength;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;check(device->CreateBuffer(&desc,nullptr,&buffer),"Direct Effect dispatch CB");}
                context->UpdateSubresource(buffer.Get(),0,nullptr,bytes.data()+cb.byteOffset,0,0);auto raw=buffer.Get();context->CSSetConstantBuffers(cb.slot,1,&raw);}
            for(const auto& r:records<San14EffectDirectBindingV1>(bytes,d.resourceOffset,d.resourceCount)){auto raw=buffers.at(r.bufferId).view.Get();context->CSSetShaderResources(r.slot,1,&raw);}
            for(const auto& r:records<San14EffectDirectBindingV1>(bytes,d.unorderedOffset,d.unorderedCount)){auto raw=buffers.at(r.bufferId).unordered.Get();context->CSSetUnorderedAccessViews(r.slot,1,&raw,nullptr);}
            context->Dispatch(d.groupsX,d.groupsY,d.groupsZ);if(d.kernel==7)advanced=true;if(d.kernel==1||d.kernel==5)++birthRevision;
        }
        ComputeScope::clear(context);
        if(advanced){lastLive=-1;if(!readPending){context->CopyResource(liveReadback.Get(),buffers.at(8).buffer.Get());readBirthRevision=birthRevision;readPending=true;}}
    }
};
}
