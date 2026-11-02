#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "../../Interop/RouteOverlayInput.h"
#include "RouteStripRenderer.h"
#include "RouteGpuMedia.h"
#include <memory>
#include <optional>
#include <map>
#include <span>
#include <sstream>
#include <cmath>

// Original route DXBC, retained geometry/media, and same-frame named constants.
// Resource-generation replacement retires shader and texture owners together.
namespace San14::Render {
class NativeRouteOverlay {
    struct Program {
        RouteOriginalPrograms original;
        ComPtr<ID3D11RasterizerState> raster;
        ComPtr<ID3D11BlendState> blend;
        std::array<ComPtr<ID3D11DepthStencilState>,2> drawDepth,restoreDepth;
        std::array<D3D11_DEPTH_STENCIL_DESC,2> drawDescription{},restoreDescription{};
        static std::shared_ptr<Program> prepare(ID3D11Device* device,const NativeRouteGpuMedia& media) {
            auto out=std::make_shared<Program>();
            out->original.borrow(device,media.program);
            out->raster=media.raster;out->blend=media.blend;
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
    std::shared_ptr<const NativeRouteGpuMedia> media;
    std::shared_ptr<Program> currentProgram;
    struct Resident {std::shared_ptr<const NativeRouteGpuMedia> media;std::shared_ptr<Program> current;};
    std::map<fs::path,Resident> resident_;
    bool requireResident_{};
    Geometry geometry;
    std::uint64_t revision{},resourceGeneration{};
    std::array<float,4> globalTint{};
    bool viewPrepared{};

    HdrHeightFogState heightState_;
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
    NativeRouteOverlay()=default;
    NativeRouteOverlay(ID3D11Device* device,const std::vector<NativeRouteGpuBorrowed>& resident,bool required)
        :requireResident_(required) {
        if(required&&resident.empty())throw std::runtime_error("Scene route GPU generation is absent");
        for(const auto& source:resident) {
            if(!source.media)throw std::runtime_error("Scene borrowed route media is absent");
            auto current=Program::prepare(device,*source.media);
            if(!resident_.emplace(nativePreparedFilePath(source.directory),Resident{source.media,std::move(current)}).second)
                throw std::runtime_error("Scene route GPU directory is duplicated");
        }
    }
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
        auto candidateMedia=media;auto candidateProgram=currentProgram;
        if(input.resourceDirectory) {
            if(!input.resourceGeneration)throw std::runtime_error("Route resource generation is absent");
            auto prepared=resident_.find(nativePreparedFilePath(fs::path(input.resourceDirectory)));
            if(prepared!=resident_.end()) {candidateMedia=prepared->second.media;candidateProgram=prepared->second.current;}
            else if(requireResident_)throw std::runtime_error("Route update did not borrow its declared startup GPU generation");
            else {candidateMedia=NativeRouteGpuMedia::prepare(device,fs::path(input.resourceDirectory));
                candidateProgram=Program::prepare(device,*candidateMedia);}
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
        media=std::move(candidateMedia);currentProgram=std::move(candidateProgram);geometry=std::move(candidate);resourceGeneration=input.resourceGeneration;revision=input.revision;
    }
    bool empty()const noexcept {return geometry.strip.ranges.empty()&&geometry.endpoints.empty();}
    void draw(ID3D11DeviceContext* context,NativeSourceDepthState& sourceDepth,const source_camera::Frame& camera,
        unsigned sourceHostFlags,const RouteAcceptedInputs& accepted) {
        if(empty())return;
        if(!viewPrepared||!media)throw std::runtime_error("Route current View/color owner is absent");
        if(!currentProgram)throw std::runtime_error("Route View current program owner is absent");
        auto& program=*currentProgram;
        auto nextHeight=prepareHdrHeightFog(heightState_,accepted.heightSettings,camera.eye.Z,accepted.heightEnabled);
        RouteOriginalFrame frame;
        frame.worldToClip=camera.worldToClip;frame.eye=camera.eye;frame.clipPlanes=accepted.clipPlanes;
        frame.heightFog=prepareHdrHeightFogValues(nextHeight,accepted.commonClock,false);
        // Same accepted ToneMap owner as normal Exposure, before its fullscreen
        // curve normalization. Type3 consumes original W, not a sampled texel.
        float white=accepted.exposureLuminance<=1e-6f?1e-6f:accepted.exposureLuminance;
        float square=source_camera::M(white,white);
        frame.toneMap={source_camera::D(1.f,square),source_camera::M(square,.5f)};
        frame.exposure=accepted.exposure;frame.exposureSampler=accepted.exposureSampler;
        program.original.bind(context,frame,globalTint);
        heightState_=std::move(nextHeight);
        D3D11_MAPPED_SUBRESOURCE mapped{};
        context->IASetIndexBuffer(nullptr,DXGI_FORMAT_UNKNOWN,0);
        context->RSSetState(program.raster.Get());context->OMSetBlendState(program.blend.Get(),nullptr,0xFFFFFFFF);
        unsigned reverse=(sourceHostFlags>>29)&1;
        sourceDepth.bind(context,program.drawDepth[reverse].Get(),program.drawDescription[reverse],0);
        UINT stride=20,offset=0;
        if(geometry.strip.vertices) {
            auto vb=geometry.strip.vertices.Get();context->IASetVertexBuffers(0,1,&vb,&stride,&offset);
            for(std::size_t i=0;i<geometry.strip.ranges.size();i++) {
                const auto& range=geometry.strip.ranges[i];auto image=range.stage0.Get();auto sampler=geometry.samplers[i].Get();
                program.original.bindImage(context,image,sampler);
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
                auto image=selected.texture.Get();auto sampler=selected.sampler.Get();program.original.bindImage(context,image,sampler);
                context->Draw(6,first);first+=6;
            }
        }
        sourceDepth.bind(context,program.restoreDepth[reverse].Get(),program.restoreDescription[reverse],0);
        program.original.clearImages(context);
    }
};
}
