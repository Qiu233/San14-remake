// Original node187 alpha forward tail. Geometry/material objects are borrowed
// from the current primary population; source DXBC and device owners prepare once.
#pragma once
struct NativePrimaryForwardGpu {
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
    std::ifstream source(path);std::vector<std::array<unsigned,2>> writes;unsigned offset,bytes;
    while(source>>offset>>bytes) {
        if(offset>capacity||bytes>capacity-offset)throw std::runtime_error("Forward source CB write outside allocation");
        writes.push_back({offset,bytes});
    }
    if(writes.empty())throw std::runtime_error("Forward CB source-defined writes absent");return writes;
}
inline void writeNativePrimaryForwardCb(ID3D11DeviceContext* context,ID3D11Buffer* cb,
        const void* bytes,const std::vector<std::array<unsigned,2>>& writes) {
    D3D11_MAPPED_SUBRESOURCE mapped{};
    check(context->Map(cb,0,D3D11_MAP_WRITE_DISCARD,0,&mapped),"original forward CB named writes");
    for(auto& span:writes)std::memcpy(static_cast<char*>(mapped.pData)+span[0],static_cast<const char*>(bytes)+span[0],span[1]);
    context->Unmap(cb,0);
}
inline ComPtr<ID3D11ShaderResourceView> createNativePrimaryForwardWhiteArray(ID3D11Device* device,
        const fs::path& pixelPath,bool cube) {
    auto pixels=load(pixelPath);D3D11_TEXTURE2D_DESC texture{};
    texture.Width=texture.Height=texture.MipLevels=1;texture.ArraySize=cube?6:1;
    texture.Format=DXGI_FORMAT_B8G8R8A8_TYPELESS;texture.SampleDesc.Count=1;
    texture.BindFlags=D3D11_BIND_SHADER_RESOURCE;texture.MiscFlags=cube?D3D11_RESOURCE_MISC_TEXTURECUBE:0;
    std::array<D3D11_SUBRESOURCE_DATA,6> initial;
    for(auto& side:initial)side={pixels.data(),4,4};
    ComPtr<ID3D11Texture2D> owner;check(device->CreateTexture2D(&texture,initial.data(),&owner),"original Common330/340 white array");
    D3D11_SHADER_RESOURCE_VIEW_DESC view{};view.Format=DXGI_FORMAT_B8G8R8A8_UNORM;
    if(cube){view.ViewDimension=D3D11_SRV_DIMENSION_TEXTURECUBEARRAY;view.TextureCubeArray.MipLevels=1;view.TextureCubeArray.NumCubes=1;}
    else{view.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2DARRAY;view.Texture2DArray.MipLevels=1;view.Texture2DArray.ArraySize=1;}
    ComPtr<ID3D11ShaderResourceView> result;check(device->CreateShaderResourceView(owner.Get(),&view,&result),"original Point/Spot array view");return result;
}
inline NativePrimaryForwardGpu prepareNativePrimaryForwardGpu(ID3D11Device* device,ID3D11DeviceContext* context,
        const fs::path& input) {
    NativePrimaryForwardGpu out;auto vertex=load(input/"vs.dxbc"),pixel=load(input/"ps.dxbc");
    check(device->CreateVertexShader(vertex.data(),vertex.size(),nullptr,&out.vertex),"original forward VS0");
    check(device->CreatePixelShader(pixel.data(),pixel.size(),nullptr,&out.pixel),"original forward PS11");
    out.vertexConstants=createNativePrimaryForwardCb(device,480);out.pixelConstants=createNativePrimaryForwardCb(device,1360);
    out.vertexWrites=loadNativePrimaryForwardWrites(input/"vs-defined-ranges.txt",480);
    out.pixelWrites=loadNativePrimaryForwardWrites(input/"ps-defined-ranges.txt",1360);
    unsigned templateCount{};std::ifstream count(input/"template-count.txt");count>>templateCount;
    if(!count||!templateCount)throw std::runtime_error("Forward actual template catalog absent");out.layouts.resize(templateCount);
    std::ifstream eligible(input/"templates.txt");unsigned templateIndex,admitted;std::string relative;
    while(eligible>>templateIndex>>admitted>>relative) {
        if(!admitted)continue;
        struct Element{std::string name;UINT index,format,slot,offset;};Element row;std::vector<Element> elements;
        std::ifstream rows(input/relative/"layout.txt");
        while(rows>>row.name>>row.index>>row.format>>row.slot>>row.offset)elements.push_back(row);
        std::vector<D3D11_INPUT_ELEMENT_DESC> layout;
        for(auto& e:elements)layout.push_back({e.name.c_str(),e.index,DXGI_FORMAT(e.format),e.slot,e.offset,D3D11_INPUT_PER_VERTEX_DATA,0});
        check(device->CreateInputLayout(layout.data(),UINT(layout.size()),vertex.data(),vertex.size(),&out.layouts.at(templateIndex)),"original forward selected layout");
    }
    struct SparseWrite {unsigned offset{},bytes{};std::vector<char> source;};
    std::array<std::vector<SparseWrite>,2> sparseWrites;std::array<unsigned,2> sparseBytes{};
    std::ifstream sparse(input/"sparse-cbuffers.txt");unsigned slot,bytes,offset,length;std::string sparseFile;
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
    std::ifstream samplerRows(input/"samplers.txt");std::string file;
    while(samplerRows>>slot>>file) {
        // Environment sampling belongs to the selected image wrapper, even
        // when an older input directory still lists a fixed sampler here.
        if(slot==11)continue;
        auto source=load(input/file);D3D11_SAMPLER_DESC desc{};std::memcpy(&desc,source.data(),sizeof(desc));
        check(device->CreateSamplerState(&desc,&out.samplers.at(slot)),"original forward source sampler");
    }
    out.resources[8]=createNativePrimaryForwardWhiteArray(device,input/"fallback8-pixel4.bin",true);
    out.resources[9]=createNativePrimaryForwardWhiteArray(device,input/"fallback9-pixel4.bin",false);
    auto white=load(input/"volume-white-pixel4.bin");D3D11_TEXTURE3D_DESC volume{};
    volume.Width=volume.Height=volume.Depth=volume.MipLevels=1;volume.Format=DXGI_FORMAT_B8G8R8A8_TYPELESS;volume.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    D3D11_SUBRESOURCE_DATA volumeData{white.data(),4,4};ComPtr<ID3D11Texture3D> volumeOwner;
    check(device->CreateTexture3D(&volume,&volumeData,&volumeOwner),"original Common348 white volume");
    D3D11_SHADER_RESOURCE_VIEW_DESC volumeView{};volumeView.Format=DXGI_FORMAT_B8G8R8A8_UNORM;
    volumeView.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE3D;volumeView.Texture3D.MipLevels=1;
    check(device->CreateShaderResourceView(volumeOwner.Get(),&volumeView,&out.resources[28]),"original white volume view");
    unsigned indexFormat,counterBytes,counterFormat,counterElements,subWidth,subHeight,subLayers,subResource,subSrv,subDsv;
    std::ifstream allocation(input/"allocation.txt");
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
    D3D11_BLEND_DESC blend{};blend.RenderTarget[0].RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
    check(device->CreateBlendState(&blend,&out.blend),"original forward no blend / full mask");
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
        std::vector<ID3D11Buffer*> streams;for(auto& stream:geometry.vertexBuffers)streams.push_back(stream.Get());
        std::vector<UINT> offsets(streams.size(),0);
        context->IASetVertexBuffers(0,UINT(streams.size()),streams.data(),geometry.strides.data(),offsets.data());
        context->IASetIndexBuffer(geometry.indices.Get(),geometry.indexBits==16?DXGI_FORMAT_R16_UINT:DXGI_FORMAT_R32_UINT,0);
        writeNativePrimaryForwardCb(context,vc,row.vertex.data(),gpu.vertexWrites);
        writeNativePrimaryForwardCb(context,pc[0],row.pixel.data(),gpu.pixelWrites);
        context->DrawIndexed(geometry.indexCount,geometry.indexStart,geometry.baseVertex);++gpu.draws;
    }
    std::array<ID3D11ShaderResourceView*,29> nulls{};context->PSSetShaderResources(0,UINT(nulls.size()),nulls.data());
}
