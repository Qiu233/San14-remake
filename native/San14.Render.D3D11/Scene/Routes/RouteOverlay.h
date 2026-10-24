#pragma once
#include "../../Interop/RouteOverlayInput.h"
#include "RouteStripRenderer.h"
#include <memory>
#include <optional>
#include <map>
#include <span>
#include <sstream>
#include <cmath>

// Project route program: actual world XYZ/UV, original selected images/G1T
// samplers and source queue state. CNS ToneMap/HeightFog appearance remains a
// later visual restoration step. The original9DXBC are not GPU-created here.
namespace San14::Render {
class NativeRouteOverlay {
    struct Program {
        ComPtr<ID3D11VertexShader> vertex;
        ComPtr<ID3D11PixelShader> pixel;
        ComPtr<ID3D11InputLayout> layout;
        ComPtr<ID3D11Buffer> constants;
        ComPtr<ID3D11RasterizerState> raster;
        ComPtr<ID3D11BlendState> blend;
        std::array<ComPtr<ID3D11DepthStencilState>,2> drawDepth,restoreDepth;
        std::array<D3D11_DEPTH_STENCIL_DESC,2> drawDescription{},restoreDescription{};
        static std::shared_ptr<Program> prepare(ID3D11Device* device) {
            static constexpr char shader[]=R"(
cbuffer Current : register(b0) {
    row_major float4x4 worldToClip;
    float4 diffuse;
    float4 alphaMapping;
};
struct Vertex {float3 position:POSITION;float2 uv:TEXCOORD0;};
struct Fragment {float4 position:SV_Position;float2 uv:TEXCOORD0;};
Fragment vertexMain(Vertex input) {
    Fragment o;o.position=mul(float4(input.position,1),worldToClip);o.uv=input.uv;return o;
}
Texture2D<float4> sourceImage:register(t0);
SamplerState sourceSampler:register(s0);
float4 pixelMain(Fragment input):SV_Target {
    float4 sampled=sourceImage.Sample(sourceSampler,input.uv);
    sampled.a=saturate(sampled.a*alphaMapping.x+alphaMapping.y);
    clip(sampled.a-alphaMapping.z);
    return sampled*diffuse;
})";
            auto out=std::make_shared<Program>();ComPtr<ID3DBlob> vs,ps,error;
            auto compile=[&](const char* entry,const char* profile,ComPtr<ID3DBlob>& code) {
                HRESULT result=D3DCompile(shader,sizeof(shader)-1,"project-route-program",nullptr,nullptr,entry,profile,
                    D3DCOMPILE_ENABLE_STRICTNESS|D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&code,&error);
                if(FAILED(result))throw std::runtime_error(error?std::string(static_cast<const char*>(error->GetBufferPointer()),error->GetBufferSize()):"Project route program compilation failed");
            };
            compile("vertexMain","vs_5_0",vs);compile("pixelMain","ps_5_0",ps);
            check(device->CreateVertexShader(vs->GetBufferPointer(),vs->GetBufferSize(),nullptr,&out->vertex),"Project route VS");
            check(device->CreatePixelShader(ps->GetBufferPointer(),ps->GetBufferSize(),nullptr,&out->pixel),"Project route PS");
            const D3D11_INPUT_ELEMENT_DESC elements[]{
                {"POSITION",0,DXGI_FORMAT_R32G32B32_FLOAT,0,0,D3D11_INPUT_PER_VERTEX_DATA,0},
                {"TEXCOORD",0,DXGI_FORMAT_R32G32_FLOAT,0,12,D3D11_INPUT_PER_VERTEX_DATA,0}};
            check(device->CreateInputLayout(elements,2,vs->GetBufferPointer(),vs->GetBufferSize(),&out->layout),"Route XYZUV20 layout");
            D3D11_BUFFER_DESC cb{};cb.ByteWidth=96;cb.Usage=D3D11_USAGE_DYNAMIC;cb.BindFlags=D3D11_BIND_CONSTANT_BUFFER;cb.CPUAccessFlags=D3D11_CPU_ACCESS_WRITE;
            check(device->CreateBuffer(&cb,nullptr,&out->constants),"Route current camera/tint constants");
            D3D11_RASTERIZER_DESC raster{};raster.FillMode=D3D11_FILL_SOLID;raster.CullMode=D3D11_CULL_BACK;raster.DepthClipEnable=TRUE;
            check(device->CreateRasterizerState(&raster,&out->raster),"Route queue200 solid back-cull raster");
            //D43190/D712F0: packed0100000000000000 selects RT0, disables
            //blend, enables allRGBA. These unused factors only make a legal
            //D3D descriptor; they are not an invented source blend formula.
            D3D11_BLEND_DESC blend{};auto& target=blend.RenderTarget[0];
            target.BlendEnable=FALSE;target.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
            target.SrcBlend=target.SrcBlendAlpha=D3D11_BLEND_ONE;target.DestBlend=target.DestBlendAlpha=D3D11_BLEND_ZERO;
            target.BlendOp=target.BlendOpAlpha=D3D11_BLEND_OP_ADD;
            check(device->CreateBlendState(&blend,&out->blend),"Route actual queue200 RGBA blend-disabled state");
            for(unsigned reverse=0;reverse<2;reverse++) {
                auto& d=out->drawDescription[reverse];d.DepthEnable=TRUE;d.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ALL;
                d.DepthFunc=reverse?D3D11_COMPARISON_GREATER_EQUAL:D3D11_COMPARISON_LESS_EQUAL;
                d.StencilEnable=TRUE;d.StencilReadMask=d.StencilWriteMask=255;
                d.FrontFace.StencilFunc=D3D11_COMPARISON_ALWAYS;
                d.FrontFace.StencilFailOp=d.FrontFace.StencilDepthFailOp=D3D11_STENCIL_OP_KEEP;
                d.FrontFace.StencilPassOp=D3D11_STENCIL_OP_REPLACE;d.BackFace=d.FrontFace;
                check(device->CreateDepthStencilState(&d,&out->drawDepth[reverse]),"Route queue200 type1 stencil replacement");
                auto& ordinary=out->restoreDescription[reverse];ordinary=d;ordinary.StencilEnable=FALSE;
                ordinary.StencilReadMask=ordinary.StencilWriteMask=0;
                ordinary.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;ordinary.BackFace=ordinary.FrontFace;
                check(device->CreateDepthStencilState(&ordinary,&out->restoreDepth[reverse]),"Route ordinary queue depth restoration");
            }
            return out;
        }
    };
    struct Image {ComPtr<ID3D11ShaderResourceView> texture;ComPtr<ID3D11SamplerState> sampler;};
    struct Media {
        std::shared_ptr<Program> program;
        std::map<unsigned,Image> groups;
        static std::shared_ptr<Media> prepare(ID3D11Device* device,const fs::path& root,std::shared_ptr<Program> program) {
            auto out=std::make_shared<Media>();out->program=program?std::move(program):Program::prepare(device);
            std::ifstream groups(root/"texture-groups.txt");unsigned name,object,count;std::string relative;
            if(!groups)throw std::runtime_error("Route actual TextureGroups are absent");
            while(groups>>name>>object>>count>>relative) {
                //C15440's listOffset0 and C2FA00's category13 entry0 both
                //survive BF5FD0/BF47F0;97F630 selects actual image0/UV0.
                if(object!=0||count==0)throw std::runtime_error("Route TextureGroup's source-selected image is unsupported");
                std::ifstream images(root/relative/"images.txt");unsigned slot,resource,view;std::string texture,sampler;
                if(!(images>>slot>>resource>>view>>texture>>sampler)||slot!=0)throw std::runtime_error("Route selected image0 is absent");
                Image image;image.texture=loadNativeModelTexture(device,root/relative/texture,DXGI_FORMAT(resource),DXGI_FORMAT(view));
                auto bytes=load(root/relative/sampler);
                if(bytes.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Route G1T sampler extent is invalid");
                D3D11_SAMPLER_DESC d;std::memcpy(&d,bytes.data(),sizeof(d));
                check(device->CreateSamplerState(&d,&image.sampler),"Route actual selected G1T sampler");
                if(!out->groups.emplace(name,std::move(image)).second)throw std::runtime_error("Route TextureGroup identity is duplicated");
            }
            if(!groups.eof()||out->groups.empty())throw std::runtime_error("Route TextureGroup list is invalid");
            return out;
        }
        const Image& image(unsigned bank,unsigned role)const {
            static constexpr unsigned keys[2][4]{{0xD7157FCA,0x83DC9465,0x5A883F24,0x3133E9E3},
                {0xE57FC322,0xF7F0860D,0xCE9C30CC,0xA547DB8B}};
            auto at=groups.find(keys[bank][role]);if(at==groups.end())throw std::runtime_error("Route selected TextureGroup is absent");return at->second;
        }
    };
    struct Endpoint {
        San14RouteEndpointV1 value{};
        std::array<unsigned,3> references{};
        std::array<std::array<float,2>,4> directions{};
    };
    struct Geometry {
        NativeRouteStripGeometry strip;
        std::vector<ComPtr<ID3D11SamplerState>> samplers;
        std::map<std::pair<int,int>,Endpoint> endpoints;
        std::vector<NativeRouteStripVertex> endpointVertices;
        ComPtr<ID3D11Buffer> endpointBuffer;
        bool rotating{};
    };
    std::shared_ptr<Media> media;
    Geometry geometry;
    std::uint64_t revision{},resourceGeneration{};
    std::array<float,4> globalTint{};
    bool viewPrepared{};

    static void makeEndpointVertices(Geometry& g,bool advance) {
        using namespace source_camera;
        const auto step=sinCos(3.1415927410125732f/512.f);
        std::size_t offset=0;
        for(auto& [key,endpoint]:g.endpoints) {
            auto& e=endpoint.value;bool rotating=e.surface>=15&&e.surface<=18;
            if(advance&&rotating)for(auto& direction:endpoint.directions) {
                float x=direction[0],z=direction[1];direction={A(M(step[0],z),M(step[1],x)),S(M(step[1],z),M(step[0],x))};
            }
            float radius=e.surface==16?2500.f:rotating?800.f:350.f;
            std::array<NativeRouteStripVertex,4> quad;
            for(unsigned i=0;i<4;i++) {
                auto d=endpoint.directions[i];quad[i]={A(e.x,M(d[0],radius)),e.y,A(e.z,M(d[1],radius)),i==1||i==2?1.f:0.f,i>=2?1.f:0.f};
            }
            for(unsigned i:{0u,1u,2u,2u,3u,0u})g.endpointVertices[offset++]=quad[i];
        }
    }
public:
    //AFDF30's real per-View preparation producer, not a missing-owner white fallback.
    void beginView()noexcept {globalTint={1,1,1,1};viewPrepared=true;}
    void accept(ID3D11Device* device,const San14RouteOverlayInputV1& input) {
        if(input.size!=sizeof(input)||input.version!=1)throw std::runtime_error("Route overlay input format is unsupported");
        if(input.updateKind==San14RouteUpdateKind::Retain)return;
        if(input.revision<=revision)throw std::runtime_error("Route overlay replacement is not newer than its installed publication");
        if(input.updateKind==San14RouteUpdateKind::Clear) {
            if(input.vertexCount||input.spanCount||input.endpointCount)throw std::runtime_error("Route clear contains geometry");
            geometry=Geometry{};revision=input.revision;return;
        }
        if(input.updateKind!=San14RouteUpdateKind::Replace)throw std::runtime_error("Route overlay update kind is invalid");
        if((input.vertexCount&&!input.vertices)||(input.spanCount&&!input.spans)||(input.endpointCount&&!input.endpoints))throw std::runtime_error("Route overlay geometry pointer is absent");
        if(input.vertexCount>std::numeric_limits<UINT>::max()/sizeof(San14RouteVertexV1))throw std::runtime_error("Route vertex extent exceeds native allocation");
        auto candidateMedia=media;
        if(input.resourceDirectory) {
            if(!input.resourceGeneration)throw std::runtime_error("Route resource generation is absent");
            candidateMedia=Media::prepare(device,fs::path(input.resourceDirectory),media?media->program:nullptr);
        } else if(input.resourceGeneration!=resourceGeneration)throw std::runtime_error("Route replacement refers to an uninstalled media generation");
        if(!candidateMedia&&(input.spanCount||input.endpointCount))throw std::runtime_error("Route original media is absent");
        Geometry candidate;std::vector<NativeRouteStripRange> ranges;
        for(unsigned i=0;i<input.spanCount;i++) {
            const auto& span=input.spans[i];
            if(span.first>input.vertexCount||span.count>input.vertexCount-span.first||span.bank>1||span.role>1||
                (span.topology!=D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP&&span.topology!=D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST))throw std::runtime_error("Route actual primitive span is invalid");
            if(!span.count)continue;
            const auto& image=candidateMedia->image(span.bank,span.role);
            ranges.push_back({span.first,span.count,D3D11_PRIMITIVE_TOPOLOGY(span.topology),image.texture});candidate.samplers.push_back(image.sampler);
        }
        for(unsigned i=0;i<input.vertexCount;i++) {
            const auto& v=input.vertices[i];if(!std::isfinite(v.x)||!std::isfinite(v.y)||!std::isfinite(v.z)||!std::isfinite(v.u)||!std::isfinite(v.v))throw std::runtime_error("Route vertex is nonfinite");
        }
        candidate.strip=NativeRouteStripGeometry::prepare(device,
            {reinterpret_cast<const NativeRouteStripVertex*>(input.vertices),input.vertexCount},ranges);
        for(unsigned i=0;i<input.endpointCount;i++) {
            const auto& e=input.endpoints[i];
            if(e.hex<0||e.hex>=48400||e.surface<0||e.surface>255||e.bank<0||e.bank>1||e.channel<0||e.channel>=3||
                !std::isfinite(e.x)||!std::isfinite(e.y)||!std::isfinite(e.z))throw std::runtime_error("Route endpoint is invalid");
            (void)candidateMedia->image(e.bank,e.surface>=15&&e.surface<=18?3:2);
            auto key=std::pair(e.hex,e.bank);auto [at,inserted]=candidate.endpoints.try_emplace(key);
            if(inserted) {
                at->second.value=e;
                if(auto old=geometry.endpoints.find(key);old!=geometry.endpoints.end())at->second.directions=old->second.directions;
                else {
                    auto sc=source_camera::sinCos(1.5707963705062866f);float x=1.f,z=0.f;
                    for(auto& direction:at->second.directions) {direction={x,z};float oldX=x;x=source_camera::A(source_camera::M(sc[0],z),source_camera::M(sc[1],x));z=source_camera::S(source_camera::M(sc[1],z),source_camera::M(sc[0],oldX));}
                }
            }
            ++at->second.references[e.channel];candidate.rotating|=e.surface>=15&&e.surface<=18;
        }
        if(candidate.endpoints.size()>std::numeric_limits<UINT>::max()/(6*sizeof(NativeRouteStripVertex)))throw std::runtime_error("Route endpoint extent exceeds native allocation");
        candidate.endpointVertices.resize(candidate.endpoints.size()*6);makeEndpointVertices(candidate,false);
        if(!candidate.endpointVertices.empty()) {
            D3D11_BUFFER_DESC d{};d.ByteWidth=UINT(candidate.endpointVertices.size()*sizeof(NativeRouteStripVertex));d.Usage=D3D11_USAGE_DYNAMIC;d.BindFlags=D3D11_BIND_VERTEX_BUFFER;d.CPUAccessFlags=D3D11_CPU_ACCESS_WRITE;
            D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=candidate.endpointVertices.data();
            check(device->CreateBuffer(&d,&initial,&candidate.endpointBuffer),"Route shared endpoint vertex buffer");
        }
        // Parsing/upload precedes installation. A preparation failure retains
        // the preceding media, geometry and endpoint directions/counts.
        media=std::move(candidateMedia);geometry=std::move(candidate);resourceGeneration=input.resourceGeneration;revision=input.revision;
    }
    bool empty()const noexcept {return geometry.strip.ranges.empty()&&geometry.endpoints.empty();}
    void draw(ID3D11DeviceContext* context,NativeSourceDepthState& sourceDepth,const source_camera::Frame& camera,
        unsigned sourceHostFlags) {
        if(empty())return;
        if(!viewPrepared||!media)throw std::runtime_error("Route current View/color owner is absent");
        auto& program=*media->program;
        struct Constants {source_camera::Matrix worldToClip;std::array<float,4> diffuse,alpha;};
        static_assert(sizeof(Constants)==96);
        //C2FA00 stage0 ordinal0 + entry7=16 ->980E00 vSAMap=(1,1,0,0).
        //Actual module10041 excludes the older half-scale branch. D27BC0's
        //route material ref0 produces vATest=1/255 before diffuse modulation.
        //Primitive tint isFFFFFFFF; beginView published the real global tint.
        Constants current{camera.worldToClip,globalTint,{1.f,0.f,1.f/255.f,0.f}};
        D3D11_MAPPED_SUBRESOURCE mapped{};check(context->Map(program.constants.Get(),0,D3D11_MAP_WRITE_DISCARD,0,&mapped),"Route current constants");
        std::memcpy(mapped.pData,&current,sizeof(current));context->Unmap(program.constants.Get(),0);
        auto cb=program.constants.Get();context->VSSetConstantBuffers(0,1,&cb);context->PSSetConstantBuffers(0,1,&cb);
        context->IASetInputLayout(program.layout.Get());context->IASetIndexBuffer(nullptr,DXGI_FORMAT_UNKNOWN,0);
        context->VSSetShader(program.vertex.Get(),nullptr,0);context->PSSetShader(program.pixel.Get(),nullptr,0);
        context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
        context->RSSetState(program.raster.Get());context->OMSetBlendState(program.blend.Get(),nullptr,0xFFFFFFFF);
        unsigned reverse=(sourceHostFlags>>29)&1;
        sourceDepth.bind(context,program.drawDepth[reverse].Get(),program.drawDescription[reverse],0);
        UINT stride=20,offset=0;
        if(geometry.strip.vertices) {
            auto vb=geometry.strip.vertices.Get();context->IASetVertexBuffers(0,1,&vb,&stride,&offset);
            for(std::size_t i=0;i<geometry.strip.ranges.size();i++) {
                const auto& range=geometry.strip.ranges[i];auto image=range.stage0.Get();auto sampler=geometry.samplers[i].Get();
                context->PSSetShaderResources(0,1,&image);context->PSSetSamplers(0,1,&sampler);
                context->IASetPrimitiveTopology(range.topology);context->Draw(range.count,range.first);
            }
        }
        if(geometry.endpointBuffer) {
            //0130F0->015480: only actual special endpoint draws advance pi/512.
            //Ordinary-only publications retain the uploaded buffer across frames.
            if(geometry.rotating) {
                makeEndpointVertices(geometry,true);
                check(context->Map(geometry.endpointBuffer.Get(),0,D3D11_MAP_WRITE_DISCARD,0,&mapped),"Route actual-draw endpoint rotation");
                std::memcpy(mapped.pData,geometry.endpointVertices.data(),geometry.endpointVertices.size()*sizeof(NativeRouteStripVertex));context->Unmap(geometry.endpointBuffer.Get(),0);
            }
            auto vb=geometry.endpointBuffer.Get();context->IASetVertexBuffers(0,1,&vb,&stride,&offset);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            unsigned first=0;
            for(const auto& [key,endpoint]:geometry.endpoints) {
                const auto& e=endpoint.value;const auto& selected=media->image(e.bank,e.surface>=15&&e.surface<=18?3:2);
                auto image=selected.texture.Get();auto sampler=selected.sampler.Get();context->PSSetShaderResources(0,1,&image);context->PSSetSamplers(0,1,&sampler);
                context->Draw(6,first);first+=6;
            }
        }
        sourceDepth.bind(context,program.restoreDepth[reverse].Get(),program.restoreDescription[reverse],0);
        ID3D11ShaderResourceView* none=nullptr;context->PSSetShaderResources(0,1,&none);
    }
};
}
