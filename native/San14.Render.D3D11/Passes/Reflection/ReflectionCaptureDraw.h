#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "ReflectionCaptureTargets.h"
#include "../../Scene/Reflection/ReflectionSceneFrame.h"
#include <tuple>
#include <fstream>
#include <sstream>
#include <type_traits>
#include <span>
#include <cstddef>

namespace San14::Render::Reflection {
// One selected original state12 packet. Geometry is borrowed from the actual
// current native model owner; material/shader resources remain session-owned.
struct CapturePacketSource {
    fs::path directory;
    bool declaredNull{};
    std::string unsupportedBoundary;
};
using CapturePacketKey=std::tuple<ModelFamily,UINT,UINT,UINT,std::uint64_t>;
inline CapturePacketKey packetKey(const PrimitiveReference& p) {
    return {p.family,p.modelIndex,p.sourceDrawIndex,p.templateIndex,p.tableOwner};
}
using CapturePacketCatalog=std::map<CapturePacketKey,CapturePacketSource>;
inline CapturePacketCatalog loadCapturePacketCatalog(const fs::path& sourceDirectory) {
    NativePreparedInputFile rows(sourceDirectory/"packets.txt");
    if(!rows)throw std::runtime_error("Reflection packets.txt absent");
    CapturePacketCatalog result;std::string line;
    while(std::getline(rows,line)) {
        if(line.empty())continue;std::istringstream row(line);
        UINT family,model,draw,sourceTemplate,status;std::uint64_t owner;std::string relative,extra;
        if(!(row>>family>>model>>draw>>sourceTemplate>>owner>>status>>relative)||row>>extra||family>2||status>2)
            throw std::runtime_error("Reflection packet catalog row format");
        CapturePacketKey key{ModelFamily(family),model,draw,sourceTemplate,owner};
        CapturePacketSource selected;selected.declaredNull=status==0;
        if(status==1)selected.directory=sourceDirectory/relative;
        if(status==2){auto boundary=load(sourceDirectory/relative);selected.unsupportedBoundary.assign(boundary.begin(),boundary.end());
            if(selected.unsupportedBoundary.empty())throw std::runtime_error("Reflection unsupported packet boundary absent");}
        if(!result.emplace(key,std::move(selected)).second)
            throw std::runtime_error("Reflection duplicate packet identity");
    }
    return result;
}

class CaptureDrawCache {
    struct Entry {NativeModelDraw draw;std::vector<char> vertex,pixel;bool tree2{};UINT groupFlags{};};
public:
    explicit CaptureDrawCache(ID3D11Device* device):device_(device) {
        D3D11_BLEND_DESC desc{};desc.IndependentBlendEnable=TRUE;
        for(UINT target=0;target<4;++target)desc.RenderTarget[target].RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
        check(device->CreateBlendState(&desc,&blend_),"Reflection actual four-MRT disabled blend");
    }
    template<class CurrentTree2Wind>
    void draw(ID3D11DeviceContext* context,const PrimitiveReference& primitive,
        const CapturePacketCatalog& catalog,const CaptureFace& face,const CaptureWork& work,
        float actualRange,const std::array<Float4,6>& actualClipPlanes,const CurrentTree2Wind* actualTree2Wind) {
        auto selected=catalog.find(packetKey(primitive));
        if(selected==catalog.end())throw std::runtime_error("Reflection actual state12 packet absent");
        if(selected->second.declaredNull)return; // Explicit original null table slot.
        if(!selected->second.unsupportedBoundary.empty())throw std::runtime_error("Reflection actual selected packet boundary: "+selected->second.unsupportedBoundary);
        if(!primitive.geometry||primitive.currentVertex.size()<160||!(actualRange>0))
            throw std::runtime_error("Reflection current rigid geometry/transform/range absent");
        auto [found,created]=entries_.try_emplace(packetKey(primitive));auto& e=found->second;
        if(created) {
            e.draw=loadNativeModelDraw(device_.Get(),selected->second.directory,"Reflection state12",primitive.geometry);
            e.vertex=load(selected->second.directory/"vs-b0.bin");e.pixel=load(selected->second.directory/"ps-b0.bin");
            e.tree2=e.vertex.size()==368;
            if((e.vertex.size()!=256&&!e.tree2)||e.pixel.size()!=176)throw std::runtime_error("Reflection state12 exact rigid/Tree2 ABI");
            if(e.tree2){UINT version,kind;NativePreparedInputFile profile(selected->second.directory/"capture-profile.txt");
                if(!(profile>>version>>kind>>e.groupFlags)||version!=1||kind!=1)
                    throw std::runtime_error("Reflection Tree2 actual capture profile absent");}
            e.draw.vsConstants=dynamicConstants(UINT(e.vertex.size()));e.draw.psConstants=dynamicConstants(176);
            // The model/actor pointer is a current-frame borrow. The cache
            // keeps only shader/layout/material ownership, never stale CPU
            // pointers or old geometry resources after actor retirement.
            e.draw.vertexBuffers.clear();e.draw.strides.clear();e.draw.indices.Reset();
        }
        std::memcpy(e.vertex.data(),face.worldToProjection.data(),64);
        std::memcpy(e.vertex.data()+64,primitive.currentVertex.data()+64,96);
        std::memcpy(e.vertex.data()+160,actualClipPlanes.data(),96);
        NativeModelDraw currentWindResources;
        if(e.tree2) {
            if constexpr(std::is_same_v<CurrentTree2Wind,std::nullptr_t>) {
                throw std::runtime_error("Reflection Tree2 capture requires the actual shared current wind owner");
            } else {
                if(!actualTree2Wind)throw std::runtime_error("Reflection Tree2 capture shared current wind owner absent");
                std::array<std::uint8_t,272> normal{};
                std::memcpy(normal.data(),e.vertex.data(),160);std::memcpy(normal.data()+160,e.vertex.data()+256,112);
                // Reuse the same uploaded wind/history and real VS resources as
                // normal Root/ground Tree2. This does not advance wind or use a
                // Main-visible candidate as a capture geometry substitute.
                actualTree2Wind->writeCurrentTree2Vs(std::span<std::uint8_t>(normal),e.groupFlags);
                actualTree2Wind->attachCurrentTree2Resources(currentWindResources,e.groupFlags);
                std::memcpy(e.vertex.data()+256,normal.data()+160,112);
            }
        }
        // The source EC7100 capture scope replaces only these current module
        // values. cSpec/alpha, retained J30 scales and actual Main3CC DO rate
        // remain the real exporter-produced state12 material template.
        const Float4 direction{0,-1,0,0},color{1,1,1,1},ambient{};
        copy(e.pixel,0,direction);copy(e.pixel,16,color);copy(e.pixel,32,ambient);
        copy(e.pixel,48,work.topDownEye);copy(e.pixel,96,work.position);
        copy(e.pixel,112,Float4{1/actualRange,0,0,0});
        context->UpdateSubresource(e.draw.vsConstants.Get(),0,nullptr,e.vertex.data(),0,0);
        context->UpdateSubresource(e.draw.psConstants.Get(),0,nullptr,e.pixel.data(),0,0);
        bind(context,e.draw,*primitive.geometry,e.tree2?&currentWindResources:nullptr);
    }
    void clear(){entries_.clear();}
private:
    static void copy(std::vector<char>& bytes,UINT offset,const Float4& value){std::memcpy(bytes.data()+offset,value.data(),16);}
    ComPtr<ID3D11Buffer> dynamicConstants(UINT bytes) {
        ComPtr<ID3D11Buffer> result;D3D11_BUFFER_DESC desc{};desc.ByteWidth=bytes;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        check(device_->CreateBuffer(&desc,nullptr,&result),"Reflection current state12 constants");return result;
    }
    void bind(ID3D11DeviceContext* context,const NativeModelDraw& draw,const NativeModelDraw& geometry,const NativeModelDraw* currentWind)const {
        if(draw.indexCount!=geometry.indexCount||draw.indexStart!=geometry.indexStart||draw.baseVertex!=geometry.baseVertex||
            draw.indexBits!=geometry.indexBits||draw.topology!=geometry.topology)throw std::runtime_error("Reflection current geometry draw range differs from selected packet");
        std::vector<ID3D11Buffer*> streams;for(const auto& stream:geometry.vertexBuffers)streams.push_back(stream.Get());
        std::vector<UINT> offsets(streams.size());context->IASetVertexBuffers(0,UINT(streams.size()),streams.data(),geometry.strides.data(),offsets.data());
        context->IASetIndexBuffer(geometry.indices.Get(),draw.indexBits==16?DXGI_FORMAT_R16_UINT:DXGI_FORMAT_R32_UINT,0);
        context->IASetInputLayout(draw.layout.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY(draw.topology));
        context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
        context->VSSetShader(draw.vs.Get(),nullptr,0);context->PSSetShader(draw.ps.Get(),nullptr,0);
        auto vc=draw.vsConstants.Get(),pc=draw.psConstants.Get();context->VSSetConstantBuffers(0,1,&vc);context->PSSetConstantBuffers(0,1,&pc);
        std::array<ID3D11ShaderResourceView*,16> resources{};std::array<ID3D11SamplerState*,16> samplers{};
        for(UINT i=0;i<16;++i){resources[i]=draw.textures[i].Get();samplers[i]=draw.samplers[i].Get();}
        // A proved fresh null P26 stays a genuine native null SRV. The actual
        // material export controls retained non-null values; no fake cube.
        context->PSSetShaderResources(0,16,resources.data());context->PSSetSamplers(0,16,samplers.data());
        std::array<ID3D11ShaderResourceView*,16> vertexResources{};std::array<ID3D11SamplerState*,16> vertexSamplers{};
        if(currentWind)for(UINT i=0;i<16;++i){vertexResources[i]=currentWind->vertexTextures[i].Get();vertexSamplers[i]=currentWind->vertexSamplers[i].Get();}
        context->VSSetShaderResources(0,16,vertexResources.data());context->VSSetSamplers(0,16,vertexSamplers.data());
        context->RSSetState(draw.raster.Get());context->OMSetBlendState(blend_.Get(),nullptr,~0u);
        context->OMSetDepthStencilState(draw.depth.Get(),draw.stencilReference);
        if(draw.instanceStreams)context->DrawIndexedInstanced(draw.indexCount,1,draw.indexStart,draw.baseVertex,0);
        else context->DrawIndexed(draw.indexCount,draw.indexStart,draw.baseVertex);
        if(currentWind){vertexResources.fill(nullptr);context->VSSetShaderResources(0,16,vertexResources.data());}
    }
    ComPtr<ID3D11Device> device_;ComPtr<ID3D11BlendState> blend_;std::map<CapturePacketKey,Entry> entries_;
};
} // namespace San14::Render::Reflection
