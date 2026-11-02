// Original node187 alpha forward tail. Geometry/material objects are borrowed
// from the current primary population; source DXBC and device owners prepare once.
#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "../../Device/PrimaryPassGpuMedia.h"
struct NativePrimaryForwardGpu {
    std::shared_ptr<const San14::Render::NativePrimaryForwardMedia> immutableMedia;
    ComPtr<ID3D11VertexShader> vertex;
    ComPtr<ID3D11PixelShader> pixel;
    std::vector<ComPtr<ID3D11InputLayout>> layouts;
    ComPtr<ID3D11Buffer> vertexConstants,pixelConstants;
    std::array<ComPtr<ID3D11Buffer>,2> sparseConstants;
    std::vector<std::array<unsigned,2>> vertexWrites,pixelWrites;
    std::array<ComPtr<ID3D11ShaderResourceView>,29> resources;
    std::array<ComPtr<ID3D11SamplerState>,16> samplers;
    ComPtr<ID3D11Buffer> clusterIndices,clusterCounter;
    ComPtr<ID3D11UnorderedAccessView> clusterUav,counterUav;
    ComPtr<ID3D11Texture2D> subShadow;
    ComPtr<ID3D11DepthStencilView> subDepth;
    ComPtr<ID3D11BlendState> blend;
    ComPtr<ID3D11DepthStencilState> depth;
    D3D11_DEPTH_STENCIL_DESC depthDescription{};
    UINT draws{},clusterBytes{},clusterElements{};
};
inline ComPtr<ID3D11Buffer> createNativePrimaryForwardCb(ID3D11Device* device,UINT bytes) {
    D3D11_BUFFER_DESC desc{};desc.ByteWidth=bytes;desc.Usage=D3D11_USAGE_DYNAMIC;
    desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;desc.CPUAccessFlags=D3D11_CPU_ACCESS_WRITE;
    ComPtr<ID3D11Buffer> cb;check(device->CreateBuffer(&desc,nullptr,&cb),"original forward sparse CB allocation");return cb;
}
inline std::vector<std::array<unsigned,2>> loadNativePrimaryForwardWrites(const fs::path& path,unsigned capacity) {
    return San14::Render::NativePrimaryForwardMetadata::loadDefinedRanges(path,capacity);
}
inline void writeNativePrimaryForwardCb(ID3D11DeviceContext* context,ID3D11Buffer* cb,
        const void* bytes,const std::vector<std::array<unsigned,2>>& writes) {
    D3D11_MAPPED_SUBRESOURCE mapped{};
    check(context->Map(cb,0,D3D11_MAP_WRITE_DISCARD,0,&mapped),"original forward CB named writes");
    for(auto& span:writes)std::memcpy(static_cast<char*>(mapped.pData)+span[0],static_cast<const char*>(bytes)+span[0],span[1]);
    context->Unmap(cb,0);
}
inline NativePrimaryForwardGpu prepareNativePrimaryForwardGpu(ID3D11Device* device,ID3D11DeviceContext* context,
        const fs::path& input,
        std::shared_ptr<const San14::Render::NativePrimaryForwardMedia> sharedMedia={},bool requireResident=false) {
    NativePrimaryForwardGpu out;
    if(requireResident&&!sharedMedia)throw std::runtime_error("Primary Forward requires immutable media loaded at startup");
    out.immutableMedia=sharedMedia?std::move(sharedMedia):San14::Render::NativePrimaryForwardMedia::load(device,input);
    const auto& media=*out.immutableMedia;
    out.vertex=media.vertex;out.pixel=media.pixel;out.layouts=media.layouts;
    out.resources=media.resources;out.samplers=media.samplers;out.blend=media.blend;
    out.vertexConstants=createNativePrimaryForwardCb(device,480);out.pixelConstants=createNativePrimaryForwardCb(device,1360);
    out.vertexWrites=media.metadata.vertexWrites;
    out.pixelWrites=media.metadata.pixelWrites;
    struct SparseWrite {unsigned offset{},bytes{};std::vector<char> source;};
    std::array<std::vector<SparseWrite>,2> sparseWrites;std::array<unsigned,2> sparseBytes{};
    NativePreparedInputFile sparse(input/"sparse-cbuffers.txt");unsigned slot,bytes,offset,length;std::string sparseFile;
    while(sparse>>slot>>bytes>>offset>>length>>sparseFile) {
        auto source=load(input/sparseFile);
        if(offset>bytes||length>bytes-offset||source.size()!=length)throw std::runtime_error("Original point/spot sparse write absent");
        sparseBytes.at(slot-1)=bytes;sparseWrites.at(slot-1).push_back({offset,length,std::move(source)});
    }
    for(unsigned i=0;i<out.sparseConstants.size();++i) {
        auto& cb=out.sparseConstants[i];cb=createNativePrimaryForwardCb(device,sparseBytes[i]);
        D3D11_MAPPED_SUBRESOURCE mapped{};check(context->Map(cb.Get(),0,D3D11_MAP_WRITE_DISCARD,0,&mapped),"original point/spot sparse upload");
        for(auto& write:sparseWrites[i])std::memcpy(static_cast<char*>(mapped.pData)+write.offset,write.source.data(),write.bytes);
        context->Unmap(cb.Get(),0);
    }
    if(!out.sparseConstants[0]||!out.sparseConstants[1])throw std::runtime_error("Forward actual point/spot CB owners absent");
    unsigned indexFormat,counterBytes,counterFormat,counterElements,subWidth,subHeight,subLayers,subResource,subSrv,subDsv;
    NativePreparedInputFile allocation(input/"allocation.txt");
    allocation>>out.clusterBytes>>indexFormat>>out.clusterElements>>counterBytes>>counterFormat>>counterElements
        >>subWidth>>subHeight>>subLayers>>subResource>>subSrv>>subDsv;
    if(!allocation)throw std::runtime_error("Original node71/Sub allocation input absent");
    D3D11_BUFFER_DESC indexDesc{};indexDesc.ByteWidth=out.clusterBytes;indexDesc.BindFlags=D3D11_BIND_SHADER_RESOURCE|D3D11_BIND_UNORDERED_ACCESS;
    // Source D20620 allocates before D249 rejects absent local-light banks.
    // No initial data and no clear: do not substitute an invented zero list.
    check(device->CreateBuffer(&indexDesc,nullptr,&out.clusterIndices),"original node71 retained typed indices");
    D3D11_SHADER_RESOURCE_VIEW_DESC indicesView{};indicesView.Format=DXGI_FORMAT(indexFormat);indicesView.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;
    indicesView.Buffer.NumElements=out.clusterElements;
    check(device->CreateShaderResourceView(out.clusterIndices.Get(),&indicesView,&out.resources[16]),"original node71 R16 indices view");
    D3D11_UNORDERED_ACCESS_VIEW_DESC indicesUav{};indicesUav.Format=DXGI_FORMAT(indexFormat);indicesUav.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;
    indicesUav.Buffer.NumElements=out.clusterElements;
    check(device->CreateUnorderedAccessView(out.clusterIndices.Get(),&indicesUav,&out.clusterUav),"original node71 indices UAV");
    indexDesc.ByteWidth=counterBytes;indexDesc.BindFlags=D3D11_BIND_UNORDERED_ACCESS;
    check(device->CreateBuffer(&indexDesc,nullptr,&out.clusterCounter),"original node71 counter");
    indicesUav.Format=DXGI_FORMAT(counterFormat);indicesUav.Buffer.NumElements=counterElements;
    check(device->CreateUnorderedAccessView(out.clusterCounter.Get(),&indicesUav,&out.counterUav),"original node71 counter UAV");
    D3D11_TEXTURE2D_DESC sub{};sub.Width=subWidth;sub.Height=subHeight;sub.ArraySize=subLayers;sub.MipLevels=1;
    sub.Format=DXGI_FORMAT(subResource);sub.SampleDesc.Count=1;sub.BindFlags=D3D11_BIND_DEPTH_STENCIL|D3D11_BIND_SHADER_RESOURCE;
    check(device->CreateTexture2D(&sub,nullptr,&out.subShadow),"original independent SubShadow lazy array");
    D3D11_SHADER_RESOURCE_VIEW_DESC subView{};subView.Format=DXGI_FORMAT(subSrv);subView.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2DARRAY;
    subView.Texture2DArray.MipLevels=1;subView.Texture2DArray.ArraySize=subLayers;
    check(device->CreateShaderResourceView(out.subShadow.Get(),&subView,&out.resources[7]),"original SubShadow array view");
    D3D11_DEPTH_STENCIL_VIEW_DESC subDepth{};subDepth.Format=DXGI_FORMAT(subDsv);subDepth.ViewDimension=D3D11_DSV_DIMENSION_TEXTURE2DARRAY;
    subDepth.Texture2DArray.ArraySize=subLayers;
    check(device->CreateDepthStencilView(out.subShadow.Get(),&subDepth,&out.subDepth),"original SubShadow array DSV");
    return out;
}
// One preparation-time observation for the finite lab, never a zero-list
// initializer or a normal per-request source check. Four R16 words per cluster.
inline void captureNativePrimaryForwardClusterHeader(ID3D11Device* device,ID3D11DeviceContext* context,
        const NativePrimaryForwardGpu& gpu,const fs::path& output,unsigned headerBytes) {
    D3D11_BUFFER_DESC desc{};desc.ByteWidth=headerBytes;desc.Usage=D3D11_USAGE_STAGING;desc.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
    ComPtr<ID3D11Buffer> staging;check(device->CreateBuffer(&desc,nullptr,&staging),"finite actual cluster header staging");
    D3D11_BOX box{0,0,0,headerBytes,1,1};context->CopySubresourceRegion(staging.Get(),0,0,0,0,gpu.clusterIndices.Get(),0,&box);
    D3D11_MAPPED_SUBRESOURCE mapped{};check(context->Map(staging.Get(),0,D3D11_MAP_READ,0,&mapped),"finite actual cluster headers");
    const auto* words=static_cast<const unsigned short*>(mapped.pData);unsigned pointMax=0,spotMax=0,nonzero=0;
    unsigned long long total=0;
    for(unsigned at=0;at<headerBytes/2;at+=4) {
        pointMax=std::max(pointMax,unsigned(words[at+2]));spotMax=std::max(spotMax,unsigned(words[at+3]));
        nonzero+=words[at+2]!=0||words[at+3]!=0;total+=unsigned(words[at+2])+unsigned(words[at+3]);
    }
    std::ofstream binary(output/"forward-cluster-header-observation.bin",std::ios::binary);
    binary.write(static_cast<const char*>(mapped.pData),headerBytes);context->Unmap(staging.Get(),0);
    std::ofstream receipt(output/"forward-cluster-header-observation.json");
    receipt<<"{\"sourceInitialized\":false,\"headerBytes\":"<<headerBytes<<",\"pointCountMax\":"<<pointMax
        <<",\"spotCountMax\":"<<spotMax<<",\"nonzeroCountClusters\":"<<nonzero<<",\"totalCounts\":"<<total<<"}\n";
    std::cout<<"forward actual node71 header pointMax="<<pointMax<<" spotMax="<<spotMax<<" nonzeroClusters="<<nonzero<<'\n';
}
template<class LogicalDepth,class Frame,class EnvironmentQuery> inline void executeNativePrimaryForwardGpu(ID3D11DeviceContext* context,
        LogicalDepth& logical,NativePrimaryForwardGpu& gpu,const Frame& frame,const NativeModelInputs& currentModels,
        ID3D11RenderTargetView* scene,ID3D11DepthStencilView* mainDepth,const D3D11_VIEWPORT& viewport,
        ID3D11ShaderResourceView* shadow,ID3D11ShaderResourceView* ggx,
        EnvironmentQuery&& environmentImage,ID3D11ShaderResourceView* fog) {
    context->OMSetRenderTargets(1,&scene,mainDepth);context->RSSetViewports(1,&viewport);
    context->OMSetBlendState(gpu.blend.Get(),nullptr,~0u);
    // D71500 writes only enable when queue bit7 is clear. Ignored stencil
    // fields and reference belong to the inherited logical frontend state.
    auto depth=logical.description;depth.DepthEnable=TRUE;depth.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ALL;
    depth.DepthFunc=D3D11_COMPARISON_GREATER_EQUAL;depth.StencilEnable=FALSE;
    if(!gpu.depth||std::memcmp(&depth,&gpu.depthDescription,sizeof(depth))!=0) {
        ComPtr<ID3D11Device> device;context->GetDevice(&device);gpu.depth.Reset();
        check(device->CreateDepthStencilState(&depth,&gpu.depth),"original node187 inherited stencil fields");gpu.depthDescription=depth;
    }
    logical.bind(context,gpu.depth.Get(),depth,logical.reference);
    context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
    context->VSSetShader(gpu.vertex.Get(),nullptr,0);context->PSSetShader(gpu.pixel.Get(),nullptr,0);
    ID3D11Buffer* vc=gpu.vertexConstants.Get();context->VSSetConstantBuffers(0,1,&vc);
    ID3D11Buffer* pc[3]{gpu.pixelConstants.Get(),gpu.sparseConstants[0].Get(),gpu.sparseConstants[1].Get()};
    context->PSSetConstantBuffers(0,3,pc);
    std::array<ID3D11ShaderResourceView*,29> views{};
    std::array<ID3D11SamplerState*,16> samplers{};
    for(unsigned i=0;i<views.size();++i)views[i]=gpu.resources[i].Get();
    for(unsigned i=0;i<samplers.size();++i)samplers[i]=gpu.samplers[i].Get();
    views[2]=shadow;
    views[10]=ggx;views[26]=fog;
    // Input slots are a bounded D3D11 resource layout. Reuse raw pointer and
    // zero-offset scratch for this synchronous submission instead of allocating
    // two vectors for every draw. Only the current geometry's count is bound.
    std::array<ID3D11Buffer*,D3D11_IA_VERTEX_INPUT_RESOURCE_SLOT_COUNT> streams{};
    std::array<UINT,D3D11_IA_VERTEX_INPUT_RESOURCE_SLOT_COUNT> offsets{};
    gpu.draws=0;
    for(auto& row:frame.draws) {
        // Each admitted lit Main material installs its own paired resource.
        // No environment query is reached when this draw list is empty.
        const auto environment=environmentImage();
        views[11]=environment.view;samplers[11]=environment.sampler;
        views[6]=row.litView;samplers[6]=row.litSampler;
        auto& geometry=currentModels.draws.at(row.primitiveIndex);
        for(auto pair:{std::array<unsigned,2>{0,0},{1,1},{4,2},{5,3}}) {
            views[pair[0]]=geometry.textures[pair[1]].Get();samplers[pair[0]]=geometry.samplers[pair[1]].Get();
        }
        context->PSSetShaderResources(0,UINT(views.size()),views.data());context->PSSetSamplers(0,UINT(samplers.size()),samplers.data());
        context->RSSetState(geometry.raster.Get());context->IASetInputLayout(gpu.layouts.at(row.templateIndex).Get());
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY(geometry.topology));
        const auto streamCount=geometry.vertexBuffers.size();
        if(streamCount>streams.size())throw std::runtime_error("Forward geometry exceeds Direct3D vertex input slots");
        for(std::size_t i=0;i<streamCount;++i)streams[i]=geometry.vertexBuffers[i].Get();
        context->IASetVertexBuffers(0,UINT(streamCount),streams.data(),geometry.strides.data(),offsets.data());
        context->IASetIndexBuffer(geometry.indices.Get(),geometry.indexBits==16?DXGI_FORMAT_R16_UINT:DXGI_FORMAT_R32_UINT,0);
        writeNativePrimaryForwardCb(context,vc,row.vertex.data(),gpu.vertexWrites);
        writeNativePrimaryForwardCb(context,pc[0],row.pixel.data(),gpu.pixelWrites);
        context->DrawIndexed(geometry.indexCount,geometry.indexStart,geometry.baseVertex);++gpu.draws;
    }
    std::array<ID3D11ShaderResourceView*,29> nulls{};context->PSSetShaderResources(0,UINT(nulls.size()),nulls.data());
}
