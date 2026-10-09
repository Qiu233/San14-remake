#pragma once
// Minimal native PB2SM consumer shared by the actual Main graph stages.
// Owns the stock5 pre-copy and PB2SM programs, complete CB, typed zone buffer,
// EditAB/RS/NS targets and source EditEM0 copy. Main/depth/Exposure are borrowed.
// NativeSourceDepthState must be declared by the caller's source state header.
#include "TerritoryState.h"
#include <d3d11.h>
#include <wrl/client.h>
#include <array>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>

namespace source_territory_gpu {
using Microsoft::WRL::ComPtr;
struct Targets {
    // Original PB2SM order: AB, RS array/surface0, NS, RR surface0.
    // Root terrain rt[] order differs: Root binds {rt[0],rt[2],rt[1],rt[3]}.
    std::array<ID3D11Texture2D*,4> storage{};
    std::array<ID3D11RenderTargetView*,4> output{};
    std::array<DXGI_FORMAT,4> sampledFormat{};
    ID3D11ShaderResourceView* depth=nullptr; // actual latest published D1 mirror SRV.
    ID3D11DepthStencilView* primaryDSV=nullptr; // current primary D1 DSV, not sampled mirror.
};
struct Owner {
    source_territory::Profile profile;
    source_territory::Owner cpu;
    ComPtr<ID3D11VertexShader> vertex;
    ComPtr<ID3D11PixelShader> pixel;
    ComPtr<ID3D11VertexShader> stockVertex;
    ComPtr<ID3D11PixelShader> stockPixel;
    ComPtr<ID3D11InputLayout> stockLayout;
    ComPtr<ID3D11Buffer> stockVertices,stockIndices,stockConstants;
    ComPtr<ID3D11Buffer> constants,zones;
    ComPtr<ID3D11ShaderResourceView> zoneView;
    std::array<ComPtr<ID3D11Texture2D>,4> copies;
    std::array<ComPtr<ID3D11ShaderResourceView>,4> copyViews;
    std::array<ComPtr<ID3D11ShaderResourceView>,3> mainCopyInputs;
    std::array<ComPtr<ID3D11RenderTargetView>,4> copyOutputs;
    std::array<ComPtr<ID3D11SamplerState>,5> samplers;
    std::array<ComPtr<ID3D11SamplerState>,3> copySamplers;
    ComPtr<ID3D11DepthStencilState> sourceStencil;
    ComPtr<ID3D11BlendState> disabledBlend;
    ComPtr<ID3D11RasterizerState> cullNone;
    D3D11_DEPTH_STENCIL_DESC sourceStencilDescription{};
};
inline void require(HRESULT result,const char* operation) {
    if(FAILED(result))throw std::runtime_error(operation);
}
inline std::vector<std::uint8_t> read(const std::filesystem::path& path) {
    std::ifstream input(path,std::ios::binary|std::ios::ate);
    if(!input)throw std::runtime_error("Original PB2SM input is absent");
    std::vector<std::uint8_t> result(static_cast<std::size_t>(input.tellg()));input.seekg(0);
    input.read(reinterpret_cast<char*>(result.data()),result.size());
    if(!input)throw std::runtime_error("Original PB2SM input is incomplete");return result;
}
inline Owner prepare(ID3D11Device* device,const std::filesystem::path& directory,
    const Targets& target,const D3D11_DEPTH_STENCIL_DESC& inheritedLogicalDepth,
    const D3D11_RASTERIZER_DESC& inheritedRaster) {
    Owner owner;owner.profile=source_territory::loadProfile(directory);owner.cpu=source_territory::construct(owner.profile);
    auto vertex=read(directory/"territory-vs.dxbc"),pixel=read(directory/"territory-ps.dxbc");
    require(device->CreateVertexShader(vertex.data(),vertex.size(),nullptr,&owner.vertex),"Original PB2SM VS0 creation failed");
    require(device->CreatePixelShader(pixel.data(),pixel.size(),nullptr,&owner.pixel),"Original PB2SM PS1 creation failed");
    auto stockVertex=read(directory/"stock-copy-vs.dxbc"),stockPixel=read(directory/"stock-copy-ps.dxbc");
    require(device->CreateVertexShader(stockVertex.data(),stockVertex.size(),nullptr,&owner.stockVertex),"Original rectangle stock VS creation failed");
    require(device->CreatePixelShader(stockPixel.data(),stockPixel.size(),nullptr,&owner.stockPixel),"Original Common278 stock5 PS creation failed");
    const D3D11_INPUT_ELEMENT_DESC stockLayout[]={{"POSITION",0,DXGI_FORMAT_R32G32B32_FLOAT,0,0,D3D11_INPUT_PER_VERTEX_DATA,0},
        {"COLOR",0,DXGI_FORMAT_R8G8B8A8_UNORM,0,12,D3D11_INPUT_PER_VERTEX_DATA,0},
        {"TEXCOORD",0,DXGI_FORMAT_R32G32_FLOAT,0,16,D3D11_INPUT_PER_VERTEX_DATA,0}};
    require(device->CreateInputLayout(stockLayout,3,stockVertex.data(),stockVertex.size(),&owner.stockLayout),"Original stock rectangle layout creation failed");
    D3D11_TEXTURE2D_DESC extent{};target.storage[0]->GetDesc(&extent);
    struct StockVertex {float x,y,z;std::uint32_t color;float u,v;};
    // Source961C10 command49 rectangle is TL/BL/BR/TR, Indexed6.
    const StockVertex rectangle[]={{0,0,0,0xFFFFFFFFu,0,0},{0,float(extent.Height),0,0xFFFFFFFFu,0,1},
        {float(extent.Width),float(extent.Height),0,0xFFFFFFFFu,1,1},{float(extent.Width),0,0,0xFFFFFFFFu,1,0}};
    static_assert(sizeof(StockVertex)==24);
    auto immutable=[&](const void* data,UINT bytes,UINT binding,ComPtr<ID3D11Buffer>& output){
        D3D11_BUFFER_DESC b{};b.ByteWidth=bytes;b.Usage=D3D11_USAGE_IMMUTABLE;b.BindFlags=binding;
        D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=data;
        require(device->CreateBuffer(&b,&initial,&output),"Original stock pre-copy immutable input creation failed");};
    immutable(rectangle,sizeof(rectangle),D3D11_BIND_VERTEX_BUFFER,owner.stockVertices);
    const std::uint16_t indices[]={0,1,2,0,2,3};
    immutable(indices,sizeof(indices),D3D11_BIND_INDEX_BUFFER,owner.stockIndices);
    // Source-created StockScreen scales29E0/29E4 and colors29F0 are all1.
    const float stockConstants[]={2.f/float(extent.Width),-2.f/float(extent.Height),-1,1,1,1,1,1};
    immutable(stockConstants,sizeof(stockConstants),D3D11_BIND_CONSTANT_BUFFER,owner.stockConstants);
    D3D11_BUFFER_DESC buffer{};buffer.ByteWidth=source_territory::byteCount;buffer.Usage=D3D11_USAGE_DEFAULT;buffer.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
    D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=owner.cpu.constants.data();
    require(device->CreateBuffer(&buffer,&initial,&owner.constants),"Original PB2SM complete6576B CB creation failed");
    buffer.ByteWidth=static_cast<UINT>(owner.profile.zones.size());buffer.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    initial.pSysMem=owner.profile.zones.data();
    require(device->CreateBuffer(&buffer,&initial,&owner.zones),"Original seven-layer byte zone buffer creation failed");
    D3D11_SHADER_RESOURCE_VIEW_DESC zone{};zone.Format=DXGI_FORMAT(owner.profile.typedBufferFormat);
    zone.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;zone.Buffer.FirstElement=0;
    zone.Buffer.NumElements=buffer.ByteWidth/owner.profile.typedBufferElementBytes;
    require(device->CreateShaderResourceView(owner.zones.Get(),&zone,&owner.zoneView),"Original R8_UINT zone SRV creation failed");
    for(unsigned n=0;n<4;n++) {
        if(!target.storage[n]||!target.output[n])throw std::runtime_error("PB2SM needs actual Main GBuffer storage and four output views");
        D3D11_TEXTURE2D_DESC descriptor{};target.storage[n]->GetDesc(&descriptor);
        // PB2SM declares an actual Texture2DArray only for RS(t3). Other
        // copied inputs are single-surface2D views, including RR source surface0.
        if(n!=1)descriptor.ArraySize=1;
        descriptor.BindFlags=D3D11_BIND_SHADER_RESOURCE|(n<3?D3D11_BIND_RENDER_TARGET:0);descriptor.Usage=D3D11_USAGE_DEFAULT;
        descriptor.CPUAccessFlags=0;descriptor.MiscFlags=0;
        require(device->CreateTexture2D(&descriptor,nullptr,&owner.copies[n]),"PB2SM copied GBuffer allocation failed");
        D3D11_SHADER_RESOURCE_VIEW_DESC view{};view.Format=target.sampledFormat[n];
        if(n==1) {
            view.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2DARRAY;view.Texture2DArray.MipLevels=descriptor.MipLevels;
            view.Texture2DArray.FirstArraySlice=0;view.Texture2DArray.ArraySize=descriptor.ArraySize;
        } else {view.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;view.Texture2D.MipLevels=descriptor.MipLevels;}
        require(device->CreateShaderResourceView(owner.copies[n].Get(),&view,&owner.copyViews[n]),"PB2SM copied GBuffer SRV creation failed");
        if(n<3)require(device->CreateShaderResourceView(target.storage[n],&view,&owner.mainCopyInputs[n]),"Original stock5 Main GBuffer SRV creation failed");
    }
    auto copyOutput=[&](unsigned output,unsigned texture,unsigned slice){
        D3D11_RENDER_TARGET_VIEW_DESC view{};view.Format=target.sampledFormat[texture];
        if(texture==1){view.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;view.Texture2DArray.FirstArraySlice=slice;view.Texture2DArray.ArraySize=1;}
        else view.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
        require(device->CreateRenderTargetView(owner.copies[texture].Get(),&view,&owner.copyOutputs[output]),"Original stock5 Edit target creation failed");};
    // Source PB pre-copy order is different from the actual PB output order.
    copyOutput(0,0,0);copyOutput(1,2,0);copyOutput(2,1,0);copyOutput(3,1,1);
    for(unsigned n=0;n<5;n++) {
        auto bytes=read(directory/("sampler-"+std::to_string(n+1)+".bin"));
        if(bytes.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Original PB2SM sampler52B absent");
        D3D11_SAMPLER_DESC sampler;std::memcpy(&sampler,bytes.data(),sizeof(sampler));
        require(device->CreateSamplerState(&sampler,&owner.samplers[n]),"Original PB2SM GBuffer sampler creation failed");
    }
    for(unsigned n=0;n<3;n++) {
        auto bytes=read(directory/("stock-copy-sampler-"+std::to_string(n)+".bin"));
        if(bytes.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Original stock5 sampler52B absent");
        D3D11_SAMPLER_DESC sampler;std::memcpy(&sampler,bytes.data(),sizeof(sampler));
        require(device->CreateSamplerState(&sampler,&owner.copySamplers[n]),"Original stock5 source wrapper sampler creation failed");
    }
    // EB0780 first disables depth, but the actual ScreenMap draw enters an
    // E726E0 scope: native comparison2=EQUAL, read mask7F, operations0=KEEP.
    // It attaches primary D1 for stencil while t1 reads the independent
    // mirror published at the immediately preceding source Pop/copy node.
    auto depth=inheritedLogicalDepth;depth.DepthEnable=FALSE;depth.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;
    depth.StencilEnable=TRUE;depth.StencilReadMask=0x7F;
    depth.FrontFace.StencilFunc=D3D11_COMPARISON_EQUAL;
    depth.FrontFace.StencilFailOp=depth.FrontFace.StencilDepthFailOp=depth.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;
    depth.BackFace=depth.FrontFace;
    owner.sourceStencilDescription=depth;
    require(device->CreateDepthStencilState(&depth,&owner.sourceStencil),"Original PB2SM Equal/read7F stencil state failed");
    D3D11_BLEND_DESC blend{};blend.IndependentBlendEnable=TRUE;
    for(auto& attachment:blend.RenderTarget) {
        attachment.BlendEnable=FALSE;attachment.SrcBlend=attachment.SrcBlendAlpha=D3D11_BLEND_ONE;
        attachment.DestBlend=attachment.DestBlendAlpha=D3D11_BLEND_ZERO;
        attachment.BlendOp=attachment.BlendOpAlpha=D3D11_BLEND_OP_ADD;attachment.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
    }
    require(device->CreateBlendState(&blend,&owner.disabledBlend),"Original PB2SM unblended full-color state failed");
    auto raster=inheritedRaster;raster.CullMode=D3D11_CULL_NONE;
    require(device->CreateRasterizerState(&raster,&owner.cullNone),"Original EB0780 CullNONE state creation failed");
    return owner;
}
inline void update(ID3D11DeviceContext* context,Owner& owner,const source_territory::Selection& selection,
    const std::array<std::uint8_t,0x3c0>& currentCamera,float currentMainB1C) {
    source_territory::writeCurrent(owner.profile,owner.cpu,selection,currentCamera,currentMainB1C);
    context->UpdateSubresource(owner.constants.Get(),0,nullptr,owner.cpu.constants.data(),0,0);
}
inline void execute(ID3D11DeviceContext* context,Owner& owner,const Targets& target,
    ID3D11ShaderResourceView* currentCommonExposure,UINT providerDrawFlags,UINT screenMapStencilMask,
    NativeSourceDepthState& logical) {
    if(!target.depth||!target.primaryDSV||!currentCommonExposure)throw std::runtime_error("PB2SM requires latest D1 mirror, primary D1 DSV and current Common Exposure");
    std::array<ComPtr<ID3D11RenderTargetView>,8> previousTargets;
    std::array<ID3D11RenderTargetView*,8> old{};ComPtr<ID3D11DepthStencilView> previousDepth;
    context->OMGetRenderTargets(8,old.data(),&previousDepth);
    for(unsigned n=0;n<8;n++)previousTargets[n].Attach(old[n]);
    ComPtr<ID3D11DepthStencilState> previousState;UINT previousStencil;
    context->OMGetDepthStencilState(&previousState,&previousStencil);
    const auto entryLogical=logical;
    ComPtr<ID3D11BlendState> previousBlend;float previousFactor[4];UINT previousSampleMask;
    context->OMGetBlendState(&previousBlend,previousFactor,&previousSampleMask);
    context->OMSetRenderTargets(0,nullptr,nullptr);
    UINT stencilReference=source_territory::drawStencilReference(providerDrawFlags,screenMapStencilMask);
    auto drawLogical=logical.description;drawLogical.DepthEnable=FALSE;drawLogical.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;
    drawLogical.StencilEnable=TRUE;drawLogical.StencilReadMask=0x7F;drawLogical.FrontFace=owner.sourceStencilDescription.FrontFace;drawLogical.BackFace=drawLogical.FrontFace;
    logical.bind(context,owner.sourceStencil.Get(),drawLogical,stencilReference);
    context->OMSetBlendState(owner.disabledBlend.Get(),nullptr,0xFFFFFFFFu);context->RSSetState(owner.cullNone.Get());
    context->GSSetShader(nullptr,nullptr,0);context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
    // Original Common278 stockbinding5 Indexed6: AB/NS/RS ->
    // EditAB/EditNS/EditRS0/EditRS1, with the same source stencil scope.
    const std::array<ID3D11ShaderResourceView*,3> copyInputs{owner.mainCopyInputs[0].Get(),owner.mainCopyInputs[2].Get(),owner.mainCopyInputs[1].Get()};
    const std::array<ID3D11SamplerState*,3> copySamplers{owner.copySamplers[0].Get(),owner.copySamplers[1].Get(),owner.copySamplers[2].Get()};
    std::array<ID3D11RenderTargetView*,4> copyOutputs;for(unsigned n=0;n<4;n++)copyOutputs[n]=owner.copyOutputs[n].Get();
    context->VSSetShader(owner.stockVertex.Get(),nullptr,0);context->PSSetShader(owner.stockPixel.Get(),nullptr,0);
    context->IASetInputLayout(owner.stockLayout.Get());UINT stride=24,offset=0;auto stockVertices=owner.stockVertices.Get();
    context->IASetVertexBuffers(0,1,&stockVertices,&stride,&offset);
    context->IASetIndexBuffer(owner.stockIndices.Get(),DXGI_FORMAT_R16_UINT,0);
    context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    auto stockConstants=owner.stockConstants.Get();context->VSSetConstantBuffers(0,1,&stockConstants);
    context->PSSetShaderResources(0,3,copyInputs.data());context->PSSetSamplers(0,3,copySamplers.data());
    context->OMSetRenderTargets(4,copyOutputs.data(),target.primaryDSV);context->DrawIndexed(6,0,0);
    context->OMSetRenderTargets(0,nullptr,nullptr);std::array<ID3D11ShaderResourceView*,3> noCopyInputs{};context->PSSetShaderResources(0,3,noCopyInputs.data());
    // Source optional EM-RR0 -> EditEM0 same-size/same-format959FD0
    // copy is not a zero clear. Current source-created GBufferLayout-only
    // publication does not bind this copied owner to PB slot5: 98B890(count3)
    // clears id131, 98AE30 publishes0, and final97EEE0 sends that actual0.
    context->CopySubresourceRegion(owner.copies[3].Get(),0,0,0,0,target.storage[3],0,nullptr);
    std::array<ID3D11ShaderResourceView*,6> textures{currentCommonExposure,target.depth,
        owner.copyViews[0].Get(),owner.copyViews[1].Get(),owner.copyViews[2].Get(),nullptr};
    context->PSSetShaderResources(0,6,textures.data());auto zones=owner.zoneView.Get();context->PSSetShaderResources(15,1,&zones);
    std::array<ID3D11SamplerState*,5> samplers;for(unsigned n=0;n<5;n++)samplers[n]=owner.samplers[n].Get();
    context->PSSetSamplers(1,5,samplers.data());auto constants=owner.constants.Get();context->PSSetConstantBuffers(0,1,&constants);
    context->VSSetShader(owner.vertex.Get(),nullptr,0);context->PSSetShader(owner.pixel.Get(),nullptr,0);
    context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    context->OMSetRenderTargets(4,target.output.data(),target.primaryDSV);context->Draw(4,0);
    textures.fill(nullptr);context->PSSetShaderResources(0,6,textures.data());zones=nullptr;context->PSSetShaderResources(15,1,&zones);
    context->OMSetRenderTargets(8,old.data(),previousDepth.Get());logical.bind(context,previousState.Get(),entryLogical.description,entryLogical.reference);
    context->OMSetBlendState(previousBlend.Get(),previousFactor,previousSampleMask);
}
} // namespace source_territory_gpu
