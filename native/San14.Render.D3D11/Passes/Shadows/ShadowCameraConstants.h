// Dynamic adapters for the existing source-created Shadow CPU and complete
// static Model population. Include after model-native.h/shadow-native.h and
// the usual load/check helpers. Device work is prepared once, not per frame.
#pragma once
#include "ShadowCameraFrame.h"
#include <array>
#include <fstream>
#include <vector>
namespace source_shadow_gpu {
struct Template {unsigned modelTemplate{},bytes{},localToProjection{};int pixelProgram=-1;};
struct Profile {unsigned depthTest{},depthWrite{},depthCompare{},stencil{},shadowClass{};std::vector<unsigned> sortFields;std::vector<Template> templates;float mainNear{},mainFar{};source_camera::V4 basisB{},basisC{},direction{};unsigned loops{};std::vector<int> sourceIndexToOrdinal;};
struct Draw {unsigned pass{},primitiveIndex{},bytes{};std::array<char,112> constants{};};
struct Plan {std::vector<Draw> draws;};
// Imported device/template/sort/catalog data stays owned once. Normal mode
// selection updates only the source receiver parameters, not large vectors.
inline void setCurrentParameters(Profile& owned,const Profile& current){owned.mainNear=current.mainNear;owned.mainFar=current.mainFar;owned.basisB=current.basisB;owned.basisC=current.basisC;owned.direction=current.direction;owned.loops=current.loops;}
inline Profile loadProfile(const std::filesystem::path& file){std::ifstream s(file,std::ios::binary);using source_camera::read;if(!s||read<unsigned>(s)!=0x31504753u)throw std::runtime_error("Shadow GPU SGP1 profile absent");Profile p;p.depthTest=read<unsigned>(s);p.depthWrite=read<unsigned>(s);p.depthCompare=read<unsigned>(s);p.stencil=read<unsigned>(s);p.shadowClass=read<unsigned>(s);int count=read<int>(s);p.sortFields.resize(count);for(auto& field:p.sortFields){field=read<unsigned>(s);if(field!=14&&field!=3)throw std::runtime_error("Current Shadow sort requires an unprovided tuple/primitive field");}count=read<int>(s);p.templates.resize(count);for(auto& t:p.templates){t.modelTemplate=read<unsigned>(s);t.bytes=read<unsigned>(s);t.localToProjection=read<unsigned>(s);t.pixelProgram=read<int>(s);if(t.pixelProgram!=-1||(t.localToProjection?t.bytes!=64:t.bytes!=112))throw std::runtime_error("Current static Shadow profile needs its active alpha/nonrigid source owner");}p.mainNear=read<float>(s);p.mainFar=read<float>(s);p.basisB=read<source_camera::V4>(s);p.basisC=read<source_camera::V4>(s);p.direction=read<source_camera::V4>(s);p.loops=read<unsigned>(s);return p;}
template<class Catalog> inline void bindCatalog(Profile& p,const Catalog& models){unsigned maximum=0;for(auto& m:models.instances)maximum=std::max(maximum,m.index);p.sourceIndexToOrdinal.assign(maximum+1,-1);for(size_t i=0;i<models.instances.size();i++)p.sourceIndexToOrdinal[models.instances[i].index]=int(i);if(p.templates.size()!=models.templateCount)throw std::runtime_error("Shadow template catalog differs from source Model catalog");}
template<class Primitive> inline std::array<char,112> rigidConstants(const Template& t,const Primitive& primitive,const source_camera::Matrix& shadow){std::array<char,112> bytes{};if(t.localToProjection){std::array<float,12> rows;std::memcpy(rows.data(),primitive.vertex.data()+112,48);auto& r=rows;source_camera::Matrix world{r[0],r[4],r[8],0,r[1],r[5],r[9],0,r[2],r[6],r[10],0,r[3],r[7],r[11],1};auto combined=source_camera::compose(world,shadow);std::memcpy(bytes.data(),combined.data(),64);}else{std::memcpy(bytes.data(),shadow.data(),64);std::memcpy(bytes.data()+64,primitive.vertex.data()+112,48);}return bytes;}
template<class Catalog> inline Plan preparePlan(const Profile& p,const source_shadow_camera::Frame& frame,const Catalog& models){Plan plan;if(p.sourceIndexToOrdinal.empty())throw std::runtime_error("Bind actual complete Model catalog once before shadow requests");for(unsigned pass=0;pass<unsigned(frame.passCount);pass++){if(!frame.passes[pass].shadowAvailable)throw std::runtime_error("Selected caster pass needs its actual retained final5D0");std::vector<unsigned> rows;for(unsigned i=0;i<models.primitives.size();i++){auto& primitive=models.primitives[i];int ordinal=p.sourceIndexToOrdinal.at(primitive.instanceIndex);if(ordinal<0)throw std::runtime_error("Shadow primitive has no source candidate");if(frame.admissions.at(ordinal).mask&(1u<<pass))rows.push_back(i);}std::vector<unsigned> scratch(rows.size());for(unsigned field:p.sortFields)for(unsigned shift=0;shift<64;shift+=8){std::array<size_t,256> counts{};auto key=[&](unsigned i){auto& primitive=models.primitives[i];return field==14?frame.admissions[p.sourceIndexToOrdinal[primitive.instanceIndex]].firstAcceptedDistanceKey:primitive.tableOwner;};for(unsigned i:rows)counts[(key(i)>>shift)&255]++;size_t total=0;for(auto& n:counts){size_t old=n;n=total;total+=old;}for(unsigned i:rows)scratch[counts[(key(i)>>shift)&255]++]=i;rows.swap(scratch);}for(unsigned i:rows){auto& primitive=models.primitives[i];auto& t=p.templates.at(primitive.templateIndex);plan.draws.push_back({pass,i,t.bytes,rigidConstants(t,primitive,frame.passes[pass].shadow)});}}return plan;}
inline float rowDotX(source_camera::V4 v,source_camera::V4 r){using namespace source_shadow_camera;return A(A(A(M(v.X,r.X),M(v.Y,r.Y)),M(v.Z,r.Z)),M(v.W,r.W));}
inline source_camera::V4 transform(source_camera::V4 v,const source_camera::Matrix& m){using namespace source_shadow_camera;return {A(A(A(M(v.Y,m[4]),M(v.X,m[0])),M(v.Z,m[8])),M(v.W,m[12])),rowDotX(v,{m[1],m[5],m[9],m[13]}),rowDotX(v,{m[2],m[6],m[10],m[14]}),rowDotX(v,{m[3],m[7],m[11],m[15]})};}
inline std::array<char,768> receiverConstants(const Profile& p,const source_shadow_camera::Profile& cpu,const source_camera::Frame& camera,const source_shadow_camera::Frame& shadow,const std::array<char,768>& original){using namespace source_shadow_camera;auto bytes=original;std::array<Matrix,4> textures;for(unsigned i=0;i<unsigned(shadow.passCount);i++){textures[i]=i!=0&&!(shadow.touchedMask&(1u<<i))?Matrix{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1}:textureMatrix(shadow.passes[i].shadow);std::memcpy(bytes.data()+96+i*64,textures[i].data(),64);}for(unsigned i=0;i<4;i++)std::memcpy(bytes.data()+352+i*16,&shadow.atlas[i].scaleOffset,16);std::memcpy(bytes.data()+416,&shadow.depthPlane,16);std::memcpy(bytes.data()+432,&shadow.relativeEnds,16);V4 scales{D(1,float(cpu.width)),D(1,float(cpu.height)),float(cpu.width),float(cpu.height)};std::memcpy(bytes.data()+448,&scales,16);std::memcpy(bytes.data()+464,&p.loops,4);
 for(unsigned i=0;i<unsigned(shadow.passCount);i++){auto b=transform(p.basisB,textures[i]),c=transform(p.basisC,textures[i]);std::memcpy(bytes.data()+480+i*32,&b,16);std::memcpy(bytes.data()+496+i*32,&c,16);auto inverse=i==0||(shadow.touchedMask&(1u<<i))?source_matrix::inverse(textures[i]):textures[i];V4 plane{rowDotX(p.direction,row(inverse,0)),rowDotX(p.direction,row(inverse,1)),rowDotX(p.direction,row(inverse,2)),shadow.compactFar};std::memcpy(bytes.data()+608+i*16,&plane,16);}
 Matrix worldToProjection=matrix(camera.prefix.data()+0x100);V4 depth;if(p.mainFar>p.mainNear){float f=D(-1,S(p.mainFar,p.mainNear));depth={M(worldToProjection[3],f),M(worldToProjection[7],f),M(worldToProjection[11],f),M(S(worldToProjection[15],p.mainFar),f)};}else depth={0,0,0,1};auto inverse=source_matrix::inverse(textures[0]);V4 depthPlane{dotPlane(depth,row(inverse,0)),dotPlane(depth,row(inverse,1)),dotPlane(depth,row(inverse,2)),dotPlane(depth,row(inverse,3))};std::memcpy(bytes.data()+672,&depthPlane,16);auto projectionToWorld=source_matrix::inverse(worldToProjection);std::memcpy(bytes.data()+688,projectionToWorld.data(),64);return bytes;}
} // source_shadow_gpu CPU helpers

#ifndef SHADOW_CAMERA_GPU_CPU_ONLY
#include "ShadowRaster.h"
struct NativeShadowCameraGpuTemplate {ComPtr<ID3D11VertexShader> vertex;ComPtr<ID3D11InputLayout> layout;ComPtr<ID3D11Buffer> constants;};
struct NativeShadowCameraGpu {NativeShadowMap map;NativeShadowRasterGpu raster;source_shadow_gpu::Profile profile;std::vector<NativeShadowCameraGpuTemplate> templates;ComPtr<ID3D11DepthStencilState> depth;D3D11_DEPTH_STENCIL_DESC depthDescription{};};
inline NativeShadowCameraGpu prepareNativeShadowCameraGpu(ID3D11Device* device,source_shadow_gpu::Profile profile,const source_shadow_camera::Profile& cpu,const NativeModelContinuous& catalog,const NativeModelInputs& completeModels,const fs::path& input,std::array<NativeShadowRasterSource,2> rasterSource){NativeShadowCameraGpu out;out.raster=prepareNativeShadowRasterGpu(device,std::move(rasterSource));source_shadow_gpu::bindCatalog(profile,catalog);out.profile=std::move(profile);out.templates.resize(out.profile.templates.size());D3D11_TEXTURE2D_DESC td{};td.Width=cpu.width;td.Height=cpu.height;td.ArraySize=td.MipLevels=1;td.Format=DXGI_FORMAT_R32_TYPELESS;td.SampleDesc.Count=1;td.BindFlags=D3D11_BIND_DEPTH_STENCIL|D3D11_BIND_SHADER_RESOURCE;check(device->CreateTexture2D(&td,nullptr,&out.map.texture),"retained original shadow EA depth");D3D11_DEPTH_STENCIL_VIEW_DESC dv{};dv.Format=DXGI_FORMAT_D32_FLOAT;dv.ViewDimension=D3D11_DSV_DIMENSION_TEXTURE2D;check(device->CreateDepthStencilView(out.map.texture.Get(),&dv,&out.map.depth),"shadow EA DSV40");D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.Format=DXGI_FORMAT_R32_FLOAT;sv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sv.Texture2D.MipLevels=1;check(device->CreateShaderResourceView(out.map.texture.Get(),&sv,&out.map.view),"shadow EA SRV41");auto& ds=out.depthDescription;ds.DepthEnable=out.profile.depthTest!=0;ds.DepthWriteMask=out.profile.depthWrite?D3D11_DEPTH_WRITE_MASK_ALL:D3D11_DEPTH_WRITE_MASK_ZERO;ds.DepthFunc=D3D11_COMPARISON_FUNC(out.profile.depthCompare);ds.StencilEnable=out.profile.stencil!=0;ds.StencilReadMask=ds.StencilWriteMask=255;ds.FrontFace.StencilFunc=D3D11_COMPARISON_ALWAYS;ds.FrontFace.StencilPassOp=ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=D3D11_STENCIL_OP_KEEP;ds.BackFace=ds.FrontFace;check(device->CreateDepthStencilState(&ds,&out.depth),"actual RenderType2 reached depth compare");
 for(auto& source:out.profile.templates){auto& t=out.templates.at(source.modelTemplate);auto directory=input/("template-"+std::to_string(source.modelTemplate));auto v=load(directory/"vs.dxbc");check(device->CreateVertexShader(v.data(),v.size(),nullptr,&t.vertex),"source-selected static caster VS");struct Element{std::string name;UINT index,format,slot,offset;};std::vector<Element> elements;Element e;std::ifstream rows(directory/"layout.txt");while(rows>>e.name>>e.index>>e.format>>e.slot>>e.offset)elements.push_back(e);std::vector<D3D11_INPUT_ELEMENT_DESC> layout;for(auto& a:elements)layout.push_back({a.name.c_str(),a.index,DXGI_FORMAT(a.format),a.slot,a.offset,D3D11_INPUT_PER_VERTEX_DATA,0});check(device->CreateInputLayout(layout.data(),UINT(layout.size()),v.data(),v.size(),&t.layout),"actual caster ISGN input layout");D3D11_BUFFER_DESC cb{};cb.ByteWidth=source.bytes;cb.Usage=D3D11_USAGE_DEFAULT;cb.BindFlags=D3D11_BIND_CONSTANT_BUFFER;check(device->CreateBuffer(&cb,nullptr,&t.constants),"mutable complete original caster CB");}return out;}
template<class LogicalDepth> inline void executeNativeShadowCameraGpu(ID3D11DeviceContext* context,
        LogicalDepth& logical,NativeShadowCameraGpu& gpu,const source_shadow_camera::Frame& frame,
        const source_shadow_gpu::Plan& plan,const NativeModelContinuous& catalog,
        const NativeModelInputs& completeModels,unsigned renderMode) {
    retainNativeShadowRasterExit(context,gpu.raster);
    std::array<ID3D11ShaderResourceView*,3> nulls{};
    context->PSSetShaderResources(0,3,nulls.data());
    context->OMSetRenderTargets(0,nullptr,gpu.map.depth.Get());
    logical.bind(context,gpu.depth.Get(),gpu.depthDescription,0);
    context->ClearDepthStencilView(gpu.map.depth.Get(),D3D11_CLEAR_DEPTH,1.f,0);
    context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
    context->GSSetShader(nullptr,nullptr,0);context->PSSetShader(nullptr,nullptr,0);
    gpu.map.draws=0;unsigned currentPass=~0u,currentTemplate=~0u;
    for(auto& row:plan.draws) {
        auto& primitive=catalog.primitives.at(row.primitiveIndex);
        auto& geometry=completeModels.draws.at(row.primitiveIndex);
        auto& t=gpu.templates.at(primitive.templateIndex);
        if(currentPass!=row.pass) {
            auto& a=frame.atlas.at(row.pass);
            D3D11_VIEWPORT vp{float(a.x-1),float(a.y-1),float(a.width+2),float(a.height+2),0,1};
            D3D11_RECT scissor{a.x,a.y,a.x+a.width,a.y+a.height};
            context->RSSetViewports(1,&vp);context->RSSetScissorRects(1,&scissor);
            // The queue forces raster for every primitive. Bias belongs to the
            // pass, so switching passes must bind even if the template repeats.
            context->RSSetState(gpu.raster.passes.at(renderMode).at(row.pass).Get());
            currentPass=row.pass;
        }
        if(currentTemplate!=primitive.templateIndex) {
            context->VSSetShader(t.vertex.Get(),nullptr,0);context->IASetInputLayout(t.layout.Get());
            auto cb=t.constants.Get();context->VSSetConstantBuffers(0,1,&cb);
            currentTemplate=primitive.templateIndex;
        }
        std::vector<ID3D11Buffer*> streams;
        for(auto& b:geometry.vertexBuffers)streams.push_back(b.Get());
        std::vector<UINT> offsets(streams.size(),0);
        context->IASetVertexBuffers(0,UINT(streams.size()),streams.data(),geometry.strides.data(),offsets.data());
        context->IASetIndexBuffer(geometry.indices.Get(),geometry.indexBits==16?DXGI_FORMAT_R16_UINT:DXGI_FORMAT_R32_UINT,0);
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY(geometry.topology));
        context->UpdateSubresource(t.constants.Get(),0,nullptr,row.constants.data(),0,0);
        context->DrawIndexed(geometry.indexCount,geometry.indexStart,geometry.baseVertex);++gpu.map.draws;
    }
    context->OMSetRenderTargets(0,nullptr,nullptr);
    context->RSSetState(gpu.raster.exit.Get());
}
// Existing receiver68/119 device owners retain their exact shader/groups/
// samplers/depth logical states. Replace its immutable CB only at preparation,
// then update the complete source768 packet before the existing execute helper.
struct NativeShadowCameraReceiver {std::array<char,768> current;};
template<class Receiver> inline NativeShadowCameraReceiver prepareNativeShadowCameraReceiver(ID3D11Device* device,Receiver& receiver,const fs::path& source){auto bytes=load(source/"ps-b0.bin");if(bytes.size()!=768)throw std::runtime_error("Current source receiver requires exact768B globals");NativeShadowCameraReceiver out;std::memcpy(out.current.data(),bytes.data(),768);D3D11_BUFFER_DESC d{};d.ByteWidth=768;d.Usage=D3D11_USAGE_DEFAULT;d.BindFlags=D3D11_BIND_CONSTANT_BUFFER;D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=bytes.data();check(device->CreateBuffer(&d,&initial,&receiver.constants),"mutable complete source receiver768");return out;}
template<class Receiver> inline void updateNativeShadowCameraReceiver(ID3D11DeviceContext* context,Receiver& receiver,NativeShadowCameraReceiver& owned,const NativeShadowCameraGpu& gpu,const source_shadow_camera::Profile& cpu,const source_camera::Frame& camera,const source_shadow_camera::Frame& shadow){auto bytes=source_shadow_gpu::receiverConstants(gpu.profile,cpu,camera,shadow,owned.current);owned.current=bytes;context->UpdateSubresource(receiver.constants.Get(),0,nullptr,bytes.data(),0,0);}
#endif
