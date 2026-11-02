#pragma once
#include "../../Runtime/PreparedFileInput.h"
// Include in RenderCore.cpp's native helper scope (ComPtr/fs/load/check).
// Reuse original geometry/shaders/views/states. Only instances and complete
// original camera CBs become mutable; no device work or source reads per request.
#include "../../Camera/CameraState.h"
#include "../../Device/TerrainPrograms.h"

struct NativeTerrainCameraConstant : San14::Render::NativeTerrainProgramConstantLayout {
    std::vector<char> bytes;
};
using NativeTerrainCameraConstants=std::array<NativeTerrainCameraConstant,4>;

inline NativeTerrainCameraConstant prepareNativeTerrainCameraConstant(
    const fs::path& source,const San14::Render::NativeTerrainProgramConstantLayout& layout,const char* constantFile) {
    NativeTerrainCameraConstant result;
    static_cast<San14::Render::NativeTerrainProgramConstantLayout&>(result)=layout;
    result.bytes=load(source/constantFile);
    if(result.bytes.size()!=layout.bufferBytes)throw std::runtime_error("Terrain source complete CB size differs");
    return result;
}
// Explicit standalone compatibility: no application GPU-bank borrow exists.
inline NativeTerrainCameraConstant prepareNativeTerrainCameraConstant(
    const fs::path& source,const char* shaderFile,const char* constantFile,bool vertex) {
    const auto code=load(source/shaderFile);
    return prepareNativeTerrainCameraConstant(source,
        San14::Render::readNativeTerrainProgramConstantLayout({code.data(),code.size()},vertex),constantFile);
}
inline NativeTerrainCameraConstants prepareNativeTerrainCameraConstants(const fs::path& source,
        const San14::Render::NativeTerrainPrograms* programs=nullptr) {
    if(programs) {
        const auto& layouts=programs->constants;
        return {prepareNativeTerrainCameraConstant(source,layouts[0],"near-vs-b0.bin"),
            prepareNativeTerrainCameraConstant(source,layouts[1],"near-ps-b0.bin"),
            prepareNativeTerrainCameraConstant(source,layouts[2],"coarse-vs-b0.bin"),
            prepareNativeTerrainCameraConstant(source,layouts[3],"coarse-ps-b0.bin")};
    }
    return {prepareNativeTerrainCameraConstant(source,"near-vs.dxbc","near-vs-b0.bin",true),
        prepareNativeTerrainCameraConstant(source,"near-ps.dxbc","near-ps-b0.bin",false),
        prepareNativeTerrainCameraConstant(source,"coarse-vs.dxbc","coarse-vs-b0.bin",true),
        prepareNativeTerrainCameraConstant(source,"coarse-ps.dxbc","coarse-ps-b0.bin",false)};
}
inline void applyNativeTerrainCameraConstants(NativeTerrainCameraConstants& constants,
    const source_camera::Prefix& prefix) {
    // OriginalDeferredSceneConstants.PrepareTerrainCamera and TerrainGbufferConstants.
    // mW2P is original row-major4x4; mWV is transpose(WV)'s first three float4.
    constexpr unsigned wvIndices[]{0,4,8,12,1,5,9,13,2,6,10,14};
    std::array<char,48> worldToView{};
    for(unsigned i=0;i<12;i++)std::memcpy(worldToView.data()+i*4,prefix.data()+0x80+wvIndices[i]*4,4);
    for(auto& cb:constants) {
        if(cb.worldToProjection>=0)std::memcpy(cb.bytes.data()+cb.worldToProjection,prefix.data()+0x100,64);
        if(cb.worldToView>=0)std::memcpy(cb.bytes.data()+cb.worldToView,worldToView.data(),48);
        if(cb.eye>=0)std::memcpy(cb.bytes.data()+cb.eye,prefix.data()+0x10,16);
    }
}

struct NativeTerrainCameraGpu {
    // Fixed source programs/layout/geometry are shared by reference.
    ComPtr<ID3D11InputLayout> layout;
    ComPtr<ID3D11Buffer> positions,indices,instances;
    std::array<ComPtr<ID3D11VertexShader>,2> vertexShaders;
    std::array<ComPtr<ID3D11PixelShader>,2> pixelShaders;
    std::array<ComPtr<ID3D11Buffer>,4> buffers;
    NativeTerrainCameraConstants constants;
    std::vector<source_camera::Batch> batches;
    UINT instanceCapacity=1024,instanceCount=0,indexCount=0;
};
inline ComPtr<ID3D11Buffer> terrainCameraDefaultBuffer(ID3D11Device* device,
    const void* data,UINT size,UINT bind) {
    D3D11_BUFFER_DESC description{};description.ByteWidth=size;
    description.Usage=D3D11_USAGE_DEFAULT;description.BindFlags=bind;
    D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=data;ComPtr<ID3D11Buffer> result;
    check(device->CreateBuffer(&description,data?&initial:nullptr,&result),"Terrain mutable complete source buffer");return result;
}
inline NativeTerrainCameraGpu prepareNativeTerrainCameraGpu(ID3D11Device* device,
    const fs::path& source,ID3D11InputLayout* layout,ID3D11Buffer* positions,ID3D11Buffer* indices,
    ID3D11VertexShader* fineVs,ID3D11PixelShader* finePs,ID3D11VertexShader* coarseVs,ID3D11PixelShader* coarsePs,
    UINT indexCount,const San14::Render::NativeTerrainPrograms* programs=nullptr) {
    NativeTerrainCameraGpu owner;owner.layout=layout;owner.positions=positions;owner.indices=indices;owner.indexCount=indexCount;
    owner.vertexShaders[0]=fineVs;owner.vertexShaders[1]=coarseVs;owner.pixelShaders[0]=finePs;owner.pixelShaders[1]=coarsePs;
    owner.constants=prepareNativeTerrainCameraConstants(source,programs);
    for(UINT i=0;i<4;i++){const auto& bytes=owner.constants[i].bytes;
        owner.buffers[i]=terrainCameraDefaultBuffer(device,bytes.data(),UINT(bytes.size()),D3D11_BIND_CONSTANT_BUFFER);}
    const auto initial=load(source/"instances.bin");
    if(initial.size()%sizeof(source_camera::Instance)||initial.size()>owner.instanceCapacity*sizeof(source_camera::Instance))
        throw std::runtime_error("Source terrain instance stream exceeds original1024 capacity");
    // Six-level complete source tree has1024 leaves and at most1024 selected nodes.
    // The original primary producer also caps at1024. No current64-row restriction.
    std::vector<char> allocation(owner.instanceCapacity*sizeof(source_camera::Instance),0);
    std::copy(initial.begin(),initial.end(),allocation.begin());
    // The instance stream is rewritten once before the current Shadow/Main
    // consumers. DISCARD lets D3D11 retain an in-flight frame's backing while
    // this owner supplies the next complete live instance set.
    D3D11_BUFFER_DESC stream{};stream.ByteWidth=UINT(allocation.size());
    stream.Usage=D3D11_USAGE_DYNAMIC;stream.BindFlags=D3D11_BIND_VERTEX_BUFFER;
    stream.CPUAccessFlags=D3D11_CPU_ACCESS_WRITE;
    D3D11_SUBRESOURCE_DATA initialStream{};initialStream.pSysMem=allocation.data();
    check(device->CreateBuffer(&stream,&initialStream,&owner.instances),"Terrain dynamic instance stream");
    owner.instanceCount=UINT(initial.size()/sizeof(source_camera::Instance));
    NativePreparedInputFile input(source/"batches.txt");source_camera::Batch row;
    while(input>>row.group>>row.start>>row.count>>row.coarse)owner.batches.push_back(row);
    if(owner.batches.empty()&&owner.instanceCount)throw std::runtime_error("Source grouped terrain batches absent");
    return owner;
}
inline void updateNativeTerrainCameraGpu(ID3D11DeviceContext* context,NativeTerrainCameraGpu& owner,
    const source_camera::Frame& current) {
    if(current.instances.size()>owner.instanceCapacity)throw std::runtime_error("Terrain request exceeds source instance capacity");
    owner.instanceCount=UINT(current.instances.size());owner.batches=current.batches;
    applyNativeTerrainCameraConstants(owner.constants,current.prefix);
    for(UINT i=0;i<4;i++)context->UpdateSubresource(owner.buffers[i].Get(),0,nullptr,owner.constants[i].bytes.data(),0,0);
    if(owner.instanceCount){
        // Every current live row is supplied after DISCARD. The unused capacity
        // tail has no consumer: both passes draw only the current batch ranges.
        D3D11_MAPPED_SUBRESOURCE mapped{};
        check(context->Map(owner.instances.Get(),0,D3D11_MAP_WRITE_DISCARD,0,&mapped),
            "Terrain current instance stream discard");
        std::memcpy(mapped.pData,current.instances.data(),
            size_t(owner.instanceCount)*sizeof(source_camera::Instance));
        context->Unmap(owner.instances.Get(),0);
    }
}
struct NativeTerrainCameraDrawBindings {
    std::array<ID3D11RenderTargetView*,5> colors{};
    ID3D11DepthStencilView* depthTarget=nullptr;
    D3D11_VIEWPORT viewport{};
    std::array<ID3D11ShaderResourceView*,16> vertexViews{},pixelViews{};
    std::array<ID3D11SamplerState*,16> vertexSamplers{},pixelSamplers{};
};
inline UINT drawNativeTerrainCameraGpu(ID3D11DeviceContext* context,const NativeTerrainCameraGpu& owner,
    const NativeTerrainCameraDrawBindings& binding) {
    ID3D11Buffer* vertices[]{owner.positions.Get(),owner.instances.Get()};const UINT strides[]{12,56},offsets[]{0,0};
    context->IASetInputLayout(owner.layout.Get());context->IASetVertexBuffers(0,2,vertices,strides,offsets);
    context->IASetIndexBuffer(owner.indices.Get(),DXGI_FORMAT_R16_UINT,0);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    context->VSSetShaderResources(0,16,binding.vertexViews.data());context->PSSetShaderResources(0,16,binding.pixelViews.data());
    context->VSSetSamplers(0,16,binding.vertexSamplers.data());context->PSSetSamplers(0,16,binding.pixelSamplers.data());
    context->RSSetViewports(1,&binding.viewport);
    context->OMSetRenderTargets(5,binding.colors.data(),binding.depthTarget);
    UINT draws=0;
    for(auto batch:owner.batches){const UINT program=batch.coarse?1:0;
        context->VSSetShader(owner.vertexShaders[program].Get(),nullptr,0);context->PSSetShader(owner.pixelShaders[program].Get(),nullptr,0);
        auto vertexCb=owner.buffers[program*2].Get(),pixelCb=owner.buffers[program*2+1].Get();
        context->VSSetConstantBuffers(owner.constants[program*2].slot,1,&vertexCb);context->PSSetConstantBuffers(owner.constants[program*2+1].slot,1,&pixelCb);
        context->DrawIndexedInstanced(owner.indexCount,UINT(batch.count),0,0,UINT(batch.start));draws++;
    }
    context->OMSetRenderTargets(0,nullptr,nullptr);return draws;
}
