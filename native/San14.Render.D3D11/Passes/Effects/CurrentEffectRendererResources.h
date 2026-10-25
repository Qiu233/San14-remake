#pragma once
#include "../../Interop/CurrentEffectInput.h"
#include <span>
#include <limits>

namespace current_effect {
using Bytes=std::span<const std::uint8_t>;
inline void require(Bytes bytes,std::uint32_t offset,std::uint64_t length) {
    if(offset>bytes.size()||length>bytes.size()-offset)throw std::runtime_error("Current Effect package boundary");
}
template<class T> T value(Bytes bytes,std::uint32_t offset) {
    require(bytes,offset,sizeof(T));T result;std::memcpy(&result,bytes.data()+offset,sizeof(T));return result;
}
template<class T> std::vector<T> records(Bytes bytes,std::uint32_t offset,std::uint32_t count) {
    require(bytes,offset,std::uint64_t(count)*sizeof(T));std::vector<T> result(count);
    if(count)std::memcpy(result.data(),bytes.data()+offset,result.size()*sizeof(T));return result;
}
inline ComPtr<ID3D11Buffer> makeBuffer(ID3D11Device* device,Bytes bytes,UINT binding,bool dynamic=false) {
    if(bytes.empty()||bytes.size()>std::numeric_limits<UINT>::max())throw std::runtime_error("Current Effect buffer size");
    D3D11_BUFFER_DESC desc{};desc.ByteWidth=UINT(bytes.size());desc.BindFlags=binding;
    desc.Usage=dynamic?D3D11_USAGE_DYNAMIC:D3D11_USAGE_IMMUTABLE;
    desc.CPUAccessFlags=dynamic?D3D11_CPU_ACCESS_WRITE:0;
    D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=bytes.data();ComPtr<ID3D11Buffer> result;
    check(device->CreateBuffer(&desc,&initial,&result),"Current Effect buffer");return result;
}
struct Pipeline {
    UINT pass{},bank{};ComPtr<ID3D11VertexShader> vertex;ComPtr<ID3D11PixelShader> pixel;
    ComPtr<ID3D11InputLayout> layout;
};
struct Mesh {UINT stride{},indexFormat{},vertexBytes{},indexBytes{};ComPtr<ID3D11Buffer> vertices,indices;};
struct Texture {ComPtr<ID3D11Texture2D> texture;ComPtr<ID3D11ShaderResourceView> view;};
struct Resources {
    std::uint64_t generation{};
    std::map<UINT,Pipeline> pipelines;std::map<UINT,Mesh> meshes;
    std::map<UINT,Texture> textures;std::map<UINT,ComPtr<ID3D11SamplerState>> samplers;

    void add(ID3D11Device* device,Bytes bytes) {
        const auto h=value<San14EffectResourceHeaderV1>(bytes,0);
        if(h.magic!=0x31524543||h.version!=1)throw std::runtime_error("Current Effect CER1 version");
        for(const auto& row:records<San14EffectPipelineV1>(bytes,h.pipelineOffset,h.pipelineCount)) {
            if(pipelines.contains(row.id))continue;
            Pipeline p;p.pass=row.renderingType;p.bank=row.runtimeBank;Bytes vertex;
            for(const auto& s:records<San14EffectStageV1>(bytes,row.stageOffset,row.stageCount)) {
                require(bytes,s.byteOffset,s.byteLength);const auto code=bytes.subspan(s.byteOffset,s.byteLength);
                if(code.empty())throw std::runtime_error("Current Effect stage DXBC is empty");
                if(s.stage==1){check(device->CreateVertexShader(code.data(),code.size(),nullptr,&p.vertex),"Current Effect VS");vertex=code;}
                else if(s.stage==0)check(device->CreatePixelShader(code.data(),code.size(),nullptr,&p.pixel),"Current Effect PS");
                else throw std::runtime_error("Current Effect selected stage requires its actual consumer");
            }
            if(!p.vertex||!p.pixel)throw std::runtime_error("Current Effect requires selected VS and PS");
            auto source=records<San14EffectElementV1>(bytes,row.elementOffset,row.elementCount);
            std::vector<D3D11_INPUT_ELEMENT_DESC> elements;
            for(const auto& e:source) {
                if(!std::memchr(e.semantic,0,sizeof(e.semantic)))throw std::runtime_error("Current Effect semantic termination");
                elements.push_back({e.semantic,e.index,DXGI_FORMAT(e.format),e.slot,e.offset,D3D11_INPUT_CLASSIFICATION(e.classification),e.stepRate});
            }
            if(elements.empty())throw std::runtime_error("Current Effect input layout is absent");
            check(device->CreateInputLayout(elements.data(),UINT(elements.size()),vertex.data(),vertex.size(),&p.layout),"Current Effect input layout");
            pipelines.emplace(row.id,std::move(p));
        }
        for(const auto& row:records<San14EffectMeshV1>(bytes,h.meshOffset,h.meshCount)) {
            if(meshes.contains(row.id))continue;
            require(bytes,row.vertexOffset,row.vertexBytes);require(bytes,row.indexOffset,row.indexBytes);
            if(!row.stride||row.vertexBytes%row.stride||
                (row.indexFormat!=DXGI_FORMAT_R16_UINT&&row.indexFormat!=DXGI_FORMAT_R32_UINT))
                throw std::runtime_error("Current Effect mesh buffer format");
            Mesh mesh;mesh.stride=row.stride;mesh.indexFormat=row.indexFormat;mesh.vertexBytes=row.vertexBytes;mesh.indexBytes=row.indexBytes;
            mesh.vertices=makeBuffer(device,bytes.subspan(row.vertexOffset,row.vertexBytes),D3D11_BIND_VERTEX_BUFFER);
            mesh.indices=makeBuffer(device,bytes.subspan(row.indexOffset,row.indexBytes),D3D11_BIND_INDEX_BUFFER);
            meshes.emplace(row.id,std::move(mesh));
        }
        for(const auto& row:records<San14EffectTextureV1>(bytes,h.textureOffset,h.textureCount)) {
            if(textures.contains(row.id))continue;
            if(!row.width||!row.height||!row.mipCount||row.subresourceCount!=row.mipCount)
                throw std::runtime_error("Current Effect 2D texture mip declarations");
            std::vector<D3D11_SUBRESOURCE_DATA> initial;
            for(const auto& s:records<San14EffectSubresourceV1>(bytes,row.subresourceOffset,row.subresourceCount)) {
                require(bytes,s.byteOffset,s.byteLength);initial.push_back({bytes.data()+s.byteOffset,s.rowPitch,s.slicePitch});
            }
            D3D11_TEXTURE2D_DESC desc{};desc.Width=row.width;desc.Height=row.height;desc.ArraySize=1;desc.MipLevels=row.mipCount;
            desc.Format=DXGI_FORMAT(row.resourceFormat);desc.SampleDesc.Count=1;desc.Usage=D3D11_USAGE_IMMUTABLE;desc.BindFlags=D3D11_BIND_SHADER_RESOURCE;
            Texture texture;check(device->CreateTexture2D(&desc,initial.data(),&texture.texture),"Current Effect normal texture");
            D3D11_SHADER_RESOURCE_VIEW_DESC view{};view.Format=DXGI_FORMAT(row.viewFormat);view.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;view.Texture2D.MipLevels=row.mipCount;
            check(device->CreateShaderResourceView(texture.texture.Get(),&view,&texture.view),"Current Effect texture view");
            textures.emplace(row.id,std::move(texture));
        }
        for(const auto& row:records<San14EffectSamplerV1>(bytes,h.samplerOffset,h.samplerCount)) {
            if(samplers.contains(row.id))continue;
            if(row.byteLength!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Current Effect sampler descriptor size");
            const auto desc=value<D3D11_SAMPLER_DESC>(bytes,row.byteOffset);ComPtr<ID3D11SamplerState> sampler;
            check(device->CreateSamplerState(&desc,&sampler),"Current Effect sampler");samplers.emplace(row.id,std::move(sampler));
        }
    }
};
}
