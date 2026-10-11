#pragma once
// Common node1 Exposure. This independent2x1 float owner is not post193
// Exposure61. Static installed programs/resources are imported once.
#include "../../Camera/CameraState.h"
#include <optional>

namespace source_map_exposure {
using source_camera::get;using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
struct Profile {uint32_t flags8{};int32_t tone3A4{};uint32_t cameraSpecial1510{},customPixelAbsent{};int32_t viewMask19B8{};};
struct SelectedLight {uint32_t type{},flags{};std::array<float,3> baselineRgb{};};
struct Selection {
    // Presence comes from the actual independently owned Apply reference.
    // Supplying a LightData payload alone cannot open the source branch.
    bool applyReferencePresent=false;
    const SelectedLight* selectedLight=nullptr;
    std::optional<int32_t> viewMask19B8;
};
struct Packet {std::array<float,8> texels{};float mode{},luminance{};bool halfRectangle=false;};
struct CameraInputs {float exposure{},luminance{};};
inline float F(uint32_t bits){float v;std::memcpy(&v,&bits,4);return v;}
inline Profile readProfile(const std::filesystem::path& source){
    std::ifstream input(source/"profile.bin",std::ios::binary);auto p=source_camera::read<Profile>(input);
    if(input.peek()!=std::char_traits<char>::eof()||!p.customPixelAbsent||(p.flags8&((1u<<22)|(1u<<23))))
        throw std::runtime_error("Map Exposure fixed stock/mode-invariant source profile differs");return p;
}
inline float selectMode(uint8_t pixel,bool linear,int cameraMode,uint32_t flags,int tone){
    bool fixedHalf=pixel<64?((0x18000781cull>>pixel)&1)!=0:((0x1c03e204000f000ull>>(pixel-64))&1)!=0;
    if(fixedHalf)return .5f;
    if((flags&(1u<<22))==0||cameraMode>=0)return linear?.5f:(flags&(1u<<23))!=0?1.f:.5f;
    int selected=flags&(1u<<23)?std::clamp(tone,0,3):tone;return selected==1?.1f:0.f;
}
inline float reciprocalTone(float luminance,float mode,uint32_t flags,int tone){
    if(mode!=.1f&&mode!=.5f)return 0;
    int selected=flags&(1u<<23)?std::clamp(tone,0,3):tone;bool first=selected==1;
    float a=first?F(0x3e6147ae):F(0x3e19999a),b=first?F(0x3e99999a):.5f,
        c=first?F(0x3cf5c28f):F(0x3d4ccccd),d=first?F(0x3b03126f):F(0x3b83126f),e=first?F(0x3d086595):F(0x3d89374c);
    float product=M(a,luminance),denominator=A(M(A(product,b),luminance),F(0x3d75c28f)),numerator=A(M(A(product,c),luminance),d);
    float ratio=S(D(numerator,denominator),e);return ratio>0?D(1,ratio):0;
}
inline bool selectedLuminance(const Selection& selection,float& luminance){
    auto light=selection.selectedLight;
    if(!selection.applyReferencePresent||light==nullptr)return false;
    uint32_t type=light->type;if((type<3||type>5)&&(light->flags&1)==0)return false;
    float green=M(light->baselineRgb[1],F(0x3f1645a2)),red=M(light->baselineRgb[0],F(0x3e991687)),blue=M(light->baselineRgb[2],F(0x3de978d5));
    luminance=A(A(green,red),blue);return true;
}
inline Packet prepareCurrent(const Profile& profile,const CameraInputs& camera,const Selection& selection={}){
    Packet p;p.mode=.5f;p.luminance=camera.luminance;selectedLuminance(selection,p.luminance);
    p.texels[0]=camera.exposure;p.texels[4]=p.mode;
    p.texels[5]=reciprocalTone(p.luminance,p.mode,profile.flags8,profile.tone3A4);
    p.halfRectangle=((profile.flags8&(1u<<25))!=0||(profile.cameraSpecial1510&1)!=0)&&selection.viewMask19B8.value_or(profile.viewMask19B8)!=1;return p;
}
}

#ifndef SOURCE_MAP_EXPOSURE_CPU_ONLY
struct NativeMapExposure {
    source_map_exposure::Profile profile;
    ComPtr<ID3D11Texture2D> texture;
    ComPtr<ID3D11ShaderResourceView> view;
    ComPtr<ID3D11RenderTargetView> target;
    std::array<ComPtr<ID3D11Texture2D>,3> inputs;
    std::array<ComPtr<ID3D11ShaderResourceView>,3> inputViews;
    ComPtr<ID3D11VertexShader> vertex;
    ComPtr<ID3D11PixelShader> pixel;
    ComPtr<ID3D11InputLayout> layout;
    ComPtr<ID3D11Buffer> vertexConstants,pixelConstants,fullQuad,halfQuad,publicationQuad,publicationIndices;
    ComPtr<ID3D11SamplerState> sampler,outputSampler;
    ComPtr<ID3D11BlendState> blend;
    ComPtr<ID3D11RasterizerState> raster;
    D3D11_VIEWPORT viewport{};
    UINT ring=0;bool hasOutput=false;
    source_map_exposure::Packet current;
};
static NativeMapExposure prepareNativeMapExposure(ID3D11Device* device,const fs::path& source){
    NativeMapExposure out;out.profile=source_map_exposure::readProfile(source);
    UINT resource,sampled,target,width,height;
    std::ifstream descriptor(source/"target.txt");descriptor>>resource>>sampled>>target>>width>>height;
    if(!descriptor||resource!=1||sampled!=2||target!=2||width!=2||height!=1)throw std::runtime_error("Map Exposure native1/2/2 2x1 target differs");
    D3D11_TEXTURE2D_DESC td{};td.Width=width;td.Height=height;td.MipLevels=td.ArraySize=1;
    td.Format=DXGI_FORMAT(resource);td.SampleDesc.Count=1;td.Usage=D3D11_USAGE_DEFAULT;
    td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
    check(device->CreateTexture2D(&td,nullptr,&out.texture),"node1 original Map Exposure FloatRGBA128 output");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT(sampled);sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
    check(device->CreateShaderResourceView(out.texture.Get(),&sd,&out.view),"node1 Map Exposure SRV2");
    D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=DXGI_FORMAT(target);rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
    check(device->CreateRenderTargetView(out.texture.Get(),&rd,&out.target),"node1 Map Exposure RTV2");
    td.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    for(UINT i=0;i<3;i++){
        check(device->CreateTexture2D(&td,nullptr,&out.inputs[i]),"node1 original Exposure ring input");
        check(device->CreateShaderResourceView(out.inputs[i].Get(),&sd,&out.inputViews[i]),"node1 Exposure ring SRV2");
    }
    auto vs=load(source/"vs.dxbc"),ps=load(source/"ps.dxbc");
    check(device->CreateVertexShader(vs.data(),vs.size(),nullptr,&out.vertex),"node1 original stock screen VS");
    check(device->CreatePixelShader(ps.data(),ps.size(),nullptr,&out.pixel),"node1 original default3 PS");
    std::array<D3D11_INPUT_ELEMENT_DESC,3> input{{{"POSITION",0,DXGI_FORMAT_R32G32B32_FLOAT,0,0,D3D11_INPUT_PER_VERTEX_DATA,0},
        {"COLOR",0,DXGI_FORMAT_R8G8B8A8_UNORM,0,12,D3D11_INPUT_PER_VERTEX_DATA,0},
        {"TEXCOORD",0,DXGI_FORMAT_R32G32_FLOAT,0,16,D3D11_INPUT_PER_VERTEX_DATA,0}}};
    check(device->CreateInputLayout(input.data(),UINT(input.size()),vs.data(),vs.size(),&out.layout),"node1 stock POSITION3/COLOR4/TEXCOORD2");
    out.vertexConstants=buffer(device,load(source/"vs-b0.bin"),D3D11_BIND_CONSTANT_BUFFER);
    out.pixelConstants=buffer(device,load(source/"ps-b0.bin"),D3D11_BIND_CONSTANT_BUFFER);
    out.fullQuad=buffer(device,load(source/"full-quad.bin"),D3D11_BIND_VERTEX_BUFFER);
    out.halfQuad=buffer(device,load(source/"half-quad.bin"),D3D11_BIND_VERTEX_BUFFER);
    // EB85AF/EB85D1 -> stock49: left1x1 destination, source61 left-half UV.
    // The actual stock rectangle uses TL/BL/BR/TR and Indexed6.
    struct Vertex {float x,y,z;uint32_t color;float u,v;};
    const Vertex publication[]={{0,0,0,0xffffffff,0,0},{0,1,0,0xffffffff,0,1},
        {1,1,0,0xffffffff,.5f,1},{1,0,0,0xffffffff,.5f,0}};
    const uint16_t publicationIndices[]={0,1,2,0,2,3};
    auto bytesOf=[](const void* source,size_t size){std::vector<char> bytes(size);std::memcpy(bytes.data(),source,size);return bytes;};
    out.publicationQuad=buffer(device,bytesOf(publication,sizeof publication),D3D11_BIND_VERTEX_BUFFER);
    out.publicationIndices=buffer(device,bytesOf(publicationIndices,sizeof publicationIndices),D3D11_BIND_INDEX_BUFFER);
    auto readSampler=[&](const char* filename,ComPtr<ID3D11SamplerState>& output) {
        const auto bytes=load(source/filename);D3D11_SAMPLER_DESC sampler{};
        if(bytes.size()!=sizeof sampler)throw std::runtime_error("Map Exposure sampler source shape");
        std::memcpy(&sampler,bytes.data(),sizeof sampler);
        check(device->CreateSamplerState(&sampler,&output),"Map Exposure imported sampler");
    };
    readSampler("sampler-desc.bin",out.sampler);
    readSampler("output-sampler-desc.bin",out.outputSampler);
    D3D11_BLEND_DESC blend{};for(auto& rt:blend.RenderTarget){rt.SrcBlend=rt.SrcBlendAlpha=D3D11_BLEND_ONE;
        rt.DestBlend=rt.DestBlendAlpha=D3D11_BLEND_ZERO;rt.BlendOp=rt.BlendOpAlpha=D3D11_BLEND_OP_ADD;}blend.RenderTarget[0].RenderTargetWriteMask=15;
    check(device->CreateBlendState(&blend,&out.blend),"node1 no blend/writeRGBA");
    D3D11_RASTERIZER_DESC raster{};raster.FillMode=D3D11_FILL_SOLID;raster.CullMode=D3D11_CULL_NONE;raster.DepthClipEnable=TRUE;
    check(device->CreateRasterizerState(&raster,&out.raster),"node1 screen rectangle cullnone");
    out.viewport={0,0,2,1,0,1};return out;
}
static source_map_exposure::Packet executeNativeMapExposure(ID3D11DeviceContext* context,NativeMapExposure& owner,
    const source_map_exposure::CameraInputs& camera,const source_map_exposure::Selection& selected={}){
    auto packet=source_map_exposure::prepareCurrent(owner.profile,camera,selected);
    if(packet.halfRectangle&&!owner.hasOutput)throw std::runtime_error("Map Exposure first half draw requires actual retained output");
    owner.ring=(owner.ring+1)%3;
    context->UpdateSubresource(owner.inputs[owner.ring].Get(),0,nullptr,packet.texels.data(),32,32);
    // Known territory ABI borrows this output at t0. Unbind before making it
    // an RTV, then bind the different source ring texture to the same slot.
    ID3D11ShaderResourceView* empty=nullptr;context->PSSetShaderResources(0,1,&empty);
    context->OMSetRenderTargets(0,nullptr,nullptr);
    auto target=owner.target.Get();context->OMSetRenderTargets(1,&target,nullptr);
    context->RSSetViewports(1,&owner.viewport);context->RSSetState(owner.raster.Get());
    context->OMSetBlendState(owner.blend.Get(),nullptr,~0u);
    context->IASetInputLayout(owner.layout.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    auto vb=packet.halfRectangle?owner.halfQuad.Get():owner.fullQuad.Get();UINT stride=24,offset=0;
    context->IASetVertexBuffers(0,1,&vb,&stride,&offset);
    context->VSSetShader(owner.vertex.Get(),nullptr,0);context->PSSetShader(owner.pixel.Get(),nullptr,0);
    context->GSSetShader(nullptr,nullptr,0);context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
    auto vcb=owner.vertexConstants.Get(),pcb=owner.pixelConstants.Get();
    context->VSSetConstantBuffers(0,1,&vcb);context->PSSetConstantBuffers(0,1,&pcb);
    auto input=owner.inputViews[owner.ring].Get();auto sampler=owner.sampler.Get();
    context->PSSetShaderResources(0,1,&input);context->PSSetSamplers(0,1,&sampler);context->Draw(4,0);
    context->PSSetShaderResources(0,1,&empty);context->OMSetRenderTargets(0,nullptr,nullptr);
    // No DSV was attached: depth/stencil do not participate and logical source
    // storage remains unchanged. Caller rebinds parent attachments/viewport.
    owner.current=packet;owner.hasOutput=true;return packet;
}
// Original post-Apply EB8515..8604/EB99DF..9A51. Borrow the actual retained
// R32 Exposure61, write only global50's left texel, preserve the right one.
// The caller restores its parent attachments/viewport and source stock scope.
static void publishNativeMapExposureAfterApply(ID3D11DeviceContext* context,
    NativeMapExposure& owner,ID3D11ShaderResourceView* actualExposure61) {
    ID3D11ShaderResourceView* empty=nullptr;context->PSSetShaderResources(0,1,&empty);
    context->OMSetRenderTargets(0,nullptr,nullptr);
    auto target=owner.target.Get();context->OMSetRenderTargets(1,&target,nullptr);
    context->RSSetViewports(1,&owner.viewport);context->RSSetState(owner.raster.Get());
    context->OMSetBlendState(owner.blend.Get(),nullptr,~0u);
    context->IASetInputLayout(owner.layout.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    auto vb=owner.publicationQuad.Get();UINT stride=24,offset=0;
    context->IASetVertexBuffers(0,1,&vb,&stride,&offset);
    context->IASetIndexBuffer(owner.publicationIndices.Get(),DXGI_FORMAT_R16_UINT,0);
    context->VSSetShader(owner.vertex.Get(),nullptr,0);context->PSSetShader(owner.pixel.Get(),nullptr,0);
    context->GSSetShader(nullptr,nullptr,0);context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
    auto vcb=owner.vertexConstants.Get(),pcb=owner.pixelConstants.Get();auto sampler=owner.sampler.Get();
    context->VSSetConstantBuffers(0,1,&vcb);context->PSSetConstantBuffers(0,1,&pcb);
    context->PSSetShaderResources(0,1,&actualExposure61);context->PSSetSamplers(0,1,&sampler);
    context->DrawIndexed(6,0,0);
    context->PSSetShaderResources(0,1,&empty);context->OMSetRenderTargets(0,nullptr,nullptr);
    owner.hasOutput=true;
}
#endif
