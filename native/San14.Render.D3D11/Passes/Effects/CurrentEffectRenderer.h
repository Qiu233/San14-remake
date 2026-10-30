#pragma once
#include "CurrentEffectRendererResources.h"
#include "CurrentEffectDirect.h"
#include "CurrentEffectDirectLighting.h"
#include <functional>

namespace current_effect {
struct ExternalSampling {ID3D11ShaderResourceView* view{};ID3D11SamplerState* sampler{};};
struct GlobalViews {
    ID3D11ShaderResourceView* depth{};ID3D11ShaderResourceView* exposure{};
    ID3D11ShaderResourceView* fog{};ID3D11ShaderResourceView* scene{};
    ID3D11SamplerState* exposureSampler{};
    bool exposureAbsent{};
    // Independent offscreen consumers capture their own current target at the
    // actual row boundary. Map callers retain their separately owned capture.
    std::function<ID3D11ShaderResourceView*()> captureScene;
    std::function<ExternalSampling(UINT)> patternLighting;
    std::array<ID3D11ShaderResourceView*,6> clustered{};
    UINT pointCount{},spotCount{};
    bool clusterCurrent{};
};
struct DrawScope {
    UINT sourceNode{188},view{},timing{0xFFFFFFFFu};
    bool ui{};
    static DrawScope uiOffscreen(UINT selector) {
        if(selector!=2&&selector!=3)throw std::runtime_error("UI effect selector requires its source layer");
        const UINT view=selector+1;return {view,view,0xFFFFFFFFu,true};
    }
};
struct OwnedDraw : San14EffectDrawV1 { std::uint64_t sourceOwner{}; };
struct Frame {
    std::vector<std::uint8_t> bytes;
    San14EffectFrameHeaderV1 header{};
    std::vector<OwnedDraw> draws;
    std::vector<San14EffectConstantV1> constants;
    std::vector<San14EffectBindingV1> textures,samplers;
    bool mainBankZeroOwner{};
};
template<class T> void subrange(const std::vector<T>& list,UINT first,UINT count) {
    if(first>list.size()||count>list.size()-first)throw std::runtime_error("Current Effect draw binding range");
}
// This scope owns all device bindings touched by the new consumer. It also
// restores the separate logical source depth state, not a canonicalized GetDesc.
struct DeviceScope {
    ID3D11DeviceContext* c;NativeSourceDepthState& logical;NativeSourceDepthState original;
    ComPtr<ID3D11BlendState> blend;FLOAT factors[4]{};UINT mask{};
    ComPtr<ID3D11DepthStencilState> depth;UINT reference{};
    ComPtr<ID3D11RasterizerState> raster;ComPtr<ID3D11InputLayout> layout;
    ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11GeometryShader> gs;ComPtr<ID3D11HullShader> hs;ComPtr<ID3D11DomainShader> ds;
    std::array<ID3D11Buffer*,14> vcb{},pcb{};std::array<ID3D11ShaderResourceView*,128> vres{},pres{};
    std::array<ID3D11SamplerState*,16> vsam{},psam{};std::array<ID3D11Buffer*,2> vertices{};
    UINT strides[2]{},offsets[2]{};ComPtr<ID3D11Buffer> indices;DXGI_FORMAT indexFormat{};UINT indexOffset{};
    D3D11_PRIMITIVE_TOPOLOGY topology{};
    DeviceScope(ID3D11DeviceContext* context,NativeSourceDepthState& state):c(context),logical(state),original(state) {
        c->OMGetBlendState(&blend,factors,&mask);c->OMGetDepthStencilState(&depth,&reference);c->RSGetState(&raster);
        c->IAGetInputLayout(&layout);c->IAGetPrimitiveTopology(&topology);c->IAGetVertexBuffers(0,2,vertices.data(),strides,offsets);
        c->IAGetIndexBuffer(&indices,&indexFormat,&indexOffset);
        c->VSGetShader(&vs,nullptr,nullptr);c->PSGetShader(&ps,nullptr,nullptr);c->GSGetShader(&gs,nullptr,nullptr);
        c->HSGetShader(&hs,nullptr,nullptr);c->DSGetShader(&ds,nullptr,nullptr);
        c->VSGetConstantBuffers(0,14,vcb.data());c->PSGetConstantBuffers(0,14,pcb.data());
        c->VSGetShaderResources(0,128,vres.data());c->PSGetShaderResources(0,128,pres.data());
        c->VSGetSamplers(0,16,vsam.data());c->PSGetSamplers(0,16,psam.data());
    }
    template<class T,std::size_t N> static void release(std::array<T*,N>& values){for(auto* value:values)if(value)value->Release();}
    ~DeviceScope() {
        c->OMSetBlendState(blend.Get(),factors,mask);c->OMSetDepthStencilState(depth.Get(),reference);logical=original;c->RSSetState(raster.Get());
        c->IASetInputLayout(layout.Get());c->IASetPrimitiveTopology(topology);c->IASetVertexBuffers(0,2,vertices.data(),strides,offsets);
        c->IASetIndexBuffer(indices.Get(),indexFormat,indexOffset);
        c->VSSetShader(vs.Get(),nullptr,0);c->PSSetShader(ps.Get(),nullptr,0);c->GSSetShader(gs.Get(),nullptr,0);
        c->HSSetShader(hs.Get(),nullptr,0);c->DSSetShader(ds.Get(),nullptr,0);
        c->VSSetConstantBuffers(0,14,vcb.data());c->PSSetConstantBuffers(0,14,pcb.data());
        c->VSSetShaderResources(0,128,vres.data());c->PSSetShaderResources(0,128,pres.data());
        c->VSSetSamplers(0,16,vsam.data());c->PSSetSamplers(0,16,psam.data());
        release(vcb);release(pcb);release(vres);release(pres);release(vsam);release(psam);release(vertices);
    }
};

class Renderer {
    DrawScope drawScope;
    LightingQueue lighting;
    Resources resources;Frame frame;ComPtr<ID3D11Buffer> dynamic;UINT dynamicBytes{};
    std::uint64_t sequence{};
    bool mainBankZeroOwner{};
    std::uint64_t nextDirectId{1};
    std::map<std::uint64_t,std::unique_ptr<DirectAllocation>> direct;
    struct DirectFrame {Resources resources;Frame frame;std::int64_t submissionOrdinal{};};
    std::map<std::uint64_t,DirectFrame> directFrames;
    std::map<std::uint64_t,ComPtr<ID3D11Buffer>> constants;
    template<class T> using Key=std::array<std::uint8_t,sizeof(T)>;
    std::map<Key<D3D11_BLEND_DESC>,ComPtr<ID3D11BlendState>> blends;
    std::map<Key<D3D11_DEPTH_STENCIL_DESC>,ComPtr<ID3D11DepthStencilState>> depths;
    std::map<Key<D3D11_RASTERIZER_DESC>,ComPtr<ID3D11RasterizerState>> rasters;
    template<class T> static Key<T> key(const T& desc){Key<T> result;std::memcpy(result.data(),&desc,sizeof(desc));return result;}

    static Frame decode(Bytes bytes,const Resources& resources,bool gpuOutput,const DrawScope& scope) {
        Frame f;f.header=value<San14EffectFrameHeaderV1>(bytes,0);const auto& h=f.header;
        if(h.magic!=0x31464543||(h.version!=1&&h.version!=2&&h.version!=3))throw std::runtime_error("Current Effect CEF version");
        if(h.version==3) {
            require(bytes,48,16);const UINT flags=value<UINT>(bytes,48);
            if((flags&~1u)||value<UINT>(bytes,52)||value<UINT>(bytes,56)||value<UINT>(bytes,60)||h.drawOffset<64||h.streamOffset<64)
                throw std::runtime_error("Current Effect CEF3 owner header");
            f.mainBankZeroOwner=(flags&1)!=0;
            if(f.mainBankZeroOwner&&(scope.ui||scope.sourceNode!=188||scope.view!=0))
                throw std::runtime_error("Current Effect Main owner belongs to another View");
        }
        require(bytes,h.streamOffset,h.streamBytes);
        if(gpuOutput&&h.streamBytes)throw std::runtime_error("Direct Effect material frame contains a CPU vertex stream");
        const UINT stride=h.version==1?100u:108u;
        require(bytes,h.drawOffset,std::uint64_t(h.drawCount)*stride);
        f.draws.reserve(h.drawCount);
        for(UINT i=0;i<h.drawCount;++i) {
            OwnedDraw draw;static_cast<San14EffectDrawV1&>(draw)=value<San14EffectDrawV1>(bytes,h.drawOffset+i*stride);
            if(h.version>=2)draw.sourceOwner=value<std::uint64_t>(bytes,h.drawOffset+i*stride+100);
            f.draws.push_back(draw);
        }
        f.constants=records<San14EffectConstantV1>(bytes,h.constantOffset,h.constantCount);
        f.textures=records<San14EffectBindingV1>(bytes,h.textureBindingOffset,h.textureBindingCount);
        f.samplers=records<San14EffectBindingV1>(bytes,h.samplerBindingOffset,h.samplerBindingCount);
        for(const auto& cb:f.constants) {
            require(bytes,cb.byteOffset,cb.byteLength);
            if(cb.stage>1||cb.slot>=14||!cb.byteLength||(cb.byteLength&15)||cb.byteLength>65536)
                throw std::runtime_error("Current Effect constant buffer ABI");
        }
        for(const auto& b:f.textures) {
            if(b.stage>1||b.slot>=128||b.semantic>12)throw std::runtime_error("Current Effect texture binding ABI");
            if(scope.ui&&b.semantic!=0&&b.semantic!=1&&b.semantic!=2&&b.semantic!=4)
                throw std::runtime_error("UI Effect requires its actual uploaded global resource");
            if(b.semantic==0)(void)resources.textures.at(b.resourceId);
        }
        for(const auto& b:f.samplers) {
            if(b.stage>1||b.slot>=16||(b.semantic!=0&&b.semantic!=2&&b.semantic!=6))throw std::runtime_error("Current Effect sampler needs its actual uploaded or exposure provider");
            if(scope.ui&&b.semantic!=0)throw std::runtime_error("UI Effect sampler requires its normal uploaded owner");
            if(b.semantic==0)(void)resources.samplers.at(b.resourceId);
        }
        for(const auto& d:f.draws) {
            const auto& pipeline=resources.pipelines.at(d.pipelineId);
            if(pipeline.pass!=d.renderingType)throw std::runtime_error("Current Effect selected pipeline/pass mismatch");
            const bool ordinary=d.sourceNode==scope.sourceNode&&d.view==scope.view&&d.timing==scope.timing;
            const bool story=!scope.ui&&scope.sourceNode==188&&scope.view==0&&d.sourceNode==189&&d.view==1&&d.timing==0xFFFFFFFFu&&d.sourceOwner!=0;
            const bool special=!scope.ui&&scope.sourceNode==188&&scope.view==0&&d.view==0&&
                d.timing==0xFFFFFFFFu&&d.configuration==6&&(d.sourceNode==141||d.sourceNode==147)&&
                (d.renderingType==64||d.renderingType==65||d.renderingType==95||d.renderingType==96||
                 d.renderingType==147||d.renderingType==148||d.renderingType==169||d.renderingType==170);
            if((!ordinary&&!story&&!special)||(d.configuration==6&&!special))
                throw std::runtime_error("Current Effect row requires its distinct View/timing/special-tail consumer");
            if(special&&pipeline.bank!=(d.sourceNode==141?3u:2u))throw std::runtime_error("Special Effect selected original bank mismatch");
            if(d.blendBytes!=sizeof(D3D11_BLEND_DESC))throw std::runtime_error("Current Effect blend recipe size");
            require(bytes,d.blendOffset,d.blendBytes);
            subrange(f.constants,d.firstConstant,d.constantCount);subrange(f.textures,d.firstTexture,d.textureCount);subrange(f.samplers,d.firstSampler,d.samplerCount);
            if(!d.dynamicStride||(!gpuOutput&&(d.dynamicOffset>h.streamBytes||std::uint64_t(d.dynamicStride)*d.dynamicCount>h.streamBytes-d.dynamicOffset)))
                throw std::runtime_error("Current Effect dynamic stream range");
            if(d.meshId!=0xFFFFFFFFu) {
                const auto& mesh=resources.meshes.at(d.meshId);const UINT size=mesh.indexFormat==DXGI_FORMAT_R32_UINT?4:2;
                if(d.baseVertex!=0||(std::uint64_t(d.indexStart)+d.indexCount)*size>mesh.indexBytes||(!gpuOutput&&d.dynamicCount!=1))
                    throw std::runtime_error("Current Effect original indexed draw range");
            }
            else if(d.dynamicStride!=32&&d.dynamicStride!=64)throw std::runtime_error("Current Effect sprite stride");
            if(d.topology<1||d.topology>5)throw std::runtime_error("Current Effect primitive topology");
            if(gpuOutput&&(d.topology!=4||d.dynamicOffset!=0))throw std::runtime_error("Direct Effect expanded output topology or offset");
        }
        f.bytes.assign(bytes.begin(),bytes.end());return f;
    }
    static ID3D11ShaderResourceView* texture(const San14EffectBindingV1& b,const GlobalViews& globals,const Resources& resources) {
        ID3D11ShaderResourceView* result{};
        switch(b.semantic){case 0:result=resources.textures.at(b.resourceId).view.Get();break;
            case 1:result=globals.depth;break;case 2:result=globals.exposure;break;case 3:result=globals.fog;break;case 4:result=globals.scene;break;
            case 5:return nullptr; // The current Camera explicitly supplied no IES wrapper.
            case 6:
                if(!globals.patternLighting)throw std::runtime_error("Current Effect Pattern owner is unavailable");
                return globals.patternLighting(b.resourceId).view; // A real selected wrapper may be null.
            default:
                if(b.semantic>=7&&b.semantic<=12) {
                    if(!globals.clusterCurrent)throw std::runtime_error("Current Effect node71 owner is unavailable");
                    const UINT lane=b.semantic-7;result=globals.clustered[lane];
                    if(!result&&lane&&(lane<4?globals.pointCount:globals.spotCount)==0)return nullptr;
                }
                break;
        }
        if(!result&&!(b.semantic==2&&globals.exposureAbsent))
            throw std::runtime_error("Current Effect selected global texture provider is unavailable");
        return result;
    }
    static ID3D11SamplerState* sampler(const San14EffectBindingV1& b,const GlobalViews& globals,const Resources& resources) {
        if(b.semantic==0)return resources.samplers.at(b.resourceId).Get();
        ID3D11SamplerState* result{};
        if(b.semantic==2)result=globals.exposureSampler;
        else if(b.semantic==6&&globals.patternLighting)result=globals.patternLighting(b.resourceId).sampler;
        if(!result)throw std::runtime_error("Current Effect selected external sampler is unavailable");
        return result;
    }
public:
    explicit Renderer(DrawScope scope={}):drawScope(scope){}
    std::uint64_t createDirect(ID3D11Device* device,Bytes bytes) {
        auto allocation=std::make_unique<DirectAllocation>(device,bytes);const auto id=nextDirectId++;
        direct.emplace(id,std::move(allocation));return id;
    }
    void submitDirect(ID3D11Device* device,ID3D11DeviceContext* context,std::uint64_t id,Bytes bytes) {direct.at(id)->submit(device,context,bytes);}
    int directLive(ID3D11DeviceContext* context,std::uint64_t id) {return direct.at(id)->live(context);}
    void releaseDirect(std::uint64_t id) {lighting.release(id);directFrames.erase(id);direct.erase(id);}
    void setDirectLighting(ID3D11Device* device,std::uint64_t id,Bytes bytes,std::int64_t ordinal) {
        (void)direct.at(id);lighting.setSource(device,id,bytes,ordinal);
    }
    void configureLighting(ID3D11Device* device,Bytes bytes) {lighting.configure(device,bytes);}
    bool needsLighting() const {return lighting.hasConfiguration();}
    const NativeSourceClusterLights& clusterLights() const {return lighting.current();}
    void prepareLights(ID3D11Device* device,ID3D11DeviceContext* context,ID3D11UnorderedAccessView* indices,ID3D11ShaderResourceView* indexView) {
        std::vector<PointLightJob> jobs;
        for(const auto& [id,source]:lighting.registrations()) {const auto& allocation=*direct.at(id);
            jobs.push_back({source,allocation.resource(3),allocation.resource(9),allocation.resource(1)});}
        lighting.prepare(device,context,std::move(jobs),indices,indexView);
    }
    void setDirectDraw(ID3D11Device* device,std::uint64_t id,const San14CurrentEffectInputV1& input,std::int64_t submissionOrdinal) {
        const auto& allocation=*direct.at(id);
        if(input.size!=sizeof(input)||input.version!=1||input.updateKind>2||(!input.resources&&input.resourceBytes)||(!input.frame&&input.frameBytes))throw std::runtime_error("Direct Effect draw input ABI");
        auto candidate=directFrames[id];
        if(input.resources){if(!input.resourceGeneration)throw std::runtime_error("Direct Effect material generation absent");if(candidate.resources.generation!=input.resourceGeneration){candidate={};candidate.resources.generation=input.resourceGeneration;}candidate.resources.add(device,{input.resources,input.resourceBytes});}
        if(input.updateKind==1){if(!input.frame||candidate.resources.generation!=input.resourceGeneration)throw std::runtime_error("Direct Effect material generation mismatch");candidate.frame=decode({input.frame,input.frameBytes},candidate.resources,true,drawScope);
            for(const auto& row:candidate.frame.draws)if(row.dynamicStride!=allocation.declaration.vertexStride||(row.meshId!=0xFFFFFFFFu)!=(allocation.declaration.genericType==7))throw std::runtime_error("Direct Effect material/output kind mismatch");}
        else if(input.updateKind==2)candidate.frame={};
        candidate.submissionOrdinal=submissionOrdinal;directFrames[id]=std::move(candidate);
    }
    void accept(ID3D11Device* device,ID3D11DeviceContext* context,const San14CurrentEffectInputV1& input) {
        if(input.size!=sizeof(input)||input.version!=1||input.updateKind>2)throw std::runtime_error("Current Effect input ABI");
        if(!input.resources&&input.resourceBytes||!input.frame&&input.frameBytes)throw std::runtime_error("Current Effect input pointer is absent");
        Resources candidate=resources;
        if(input.resources) {
            if(!input.resourceGeneration)throw std::runtime_error("Current Effect generation identity is absent");
            if(candidate.generation!=input.resourceGeneration){candidate=Resources{};candidate.generation=input.resourceGeneration;}
            candidate.add(device,{input.resources,input.resourceBytes});
        }
        if(input.updateKind==0) {
            if(input.resources&&candidate.generation!=resources.generation&&!frame.draws.empty())
                throw std::runtime_error("Current Effect generation replacement requires its matching frame");
            if(candidate.generation!=resources.generation)mainBankZeroOwner=false;
            resources=std::move(candidate);return;
        }
        Frame replacement;
        if(input.updateKind==1) {
            if(!input.frame||candidate.generation!=input.resourceGeneration)throw std::runtime_error("Current Effect frame generation is not installed");
            replacement=decode({input.frame,input.frameBytes},candidate,false,drawScope);
            if(replacement.header.streamBytes) {
                Bytes bytes(replacement.bytes.data()+replacement.header.streamOffset,replacement.header.streamBytes);
                if(bytes.size()>dynamicBytes){dynamic=makeBuffer(device,bytes,D3D11_BIND_VERTEX_BUFFER,true);dynamicBytes=UINT(bytes.size());}
                else {D3D11_MAPPED_SUBRESOURCE mapped{};check(context->Map(dynamic.Get(),0,D3D11_MAP_WRITE_DISCARD,0,&mapped),"Current Effect dynamic map");std::memcpy(mapped.pData,bytes.data(),bytes.size());context->Unmap(dynamic.Get(),0);}
            }
        }
        // The actual renderer allocation survives an empty queue/Clear. A
        // resource generation replacement retires the previous allocation.
        if(candidate.generation!=resources.generation)mainBankZeroOwner=false;
        mainBankZeroOwner|=replacement.mainBankZeroOwner;
        resources=std::move(candidate);frame=std::move(replacement);sequence=input.sequence;
    }

private:
    void drawRows(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,const GlobalViews& globals,
            const Frame& frame,const Resources& resources,DirectAllocation* gpu=nullptr,UINT selectedNode=0xFFFFFFFFu) {
        if(frame.draws.empty())return;
        // Validate external providers before any draw in this frame scope.
        for(const auto& draw:frame.draws) {
            if(selectedNode!=0xFFFFFFFFu&&draw.sourceNode!=selectedNode)continue;
            for(UINT i=0;i<draw.textureCount;++i) {
                const auto& binding=frame.textures[draw.firstTexture+i];
                if(binding.semantic!=4||!globals.captureScene)(void)texture(binding,globals,resources);
            }
            for(UINT i=0;i<draw.samplerCount;++i)(void)sampler(frame.samplers[draw.firstSampler+i],globals,resources);
        }
        DeviceScope scope(context,logical);const Bytes bytes(frame.bytes);
        context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
        for(const auto& d:frame.draws) {
            if(selectedNode!=0xFFFFFFFFu&&d.sourceNode!=selectedNode)continue;
            const auto& p=resources.pipelines.at(d.pipelineId);
            context->VSSetShader(p.vertex.Get(),nullptr,0);context->PSSetShader(p.pixel.Get(),nullptr,0);context->IASetInputLayout(p.layout.Get());
            const bool special=d.sourceNode==141||d.sourceNode==147;
            auto depth=scope.original.description;
            depth.DepthWriteMask=(d.meshId==0xFFFFFFFFu?special:(d.headerFlags&8)!=0)?D3D11_DEPTH_WRITE_MASK_ALL:D3D11_DEPTH_WRITE_MASK_ZERO;
            auto& ds=depths[key(depth)];if(!ds)check(device->CreateDepthStencilState(&depth,&ds),"Current Effect inherited depth state");logical.bind(context,ds.Get(),depth,scope.original.reference);
            auto blend=value<D3D11_BLEND_DESC>(bytes,d.blendOffset);
            if(scope.blend){D3D11_BLEND_DESC inherited;scope.blend->GetDesc(&inherited);for(UINT rt=0;rt<8;++rt){blend.RenderTarget[rt].RenderTargetWriteMask=inherited.RenderTarget[rt].RenderTargetWriteMask;if(rt)blend.RenderTarget[rt].BlendEnable=inherited.RenderTarget[rt].BlendEnable;}}
            blend.RenderTarget[0].BlendEnable=special?FALSE:TRUE;
            auto& bs=blends[key(blend)];if(!bs)check(device->CreateBlendState(&blend,&bs),"Current Effect actual blend recipe");context->OMSetBlendState(bs.Get(),scope.factors,scope.mask);
            for(UINT i=0;i<d.constantCount;++i) {
                const auto& cb=frame.constants[d.firstConstant+i];const auto k=(std::uint64_t(cb.stage)<<56)|(std::uint64_t(cb.slot)<<48)|cb.byteLength;
                auto& buffer=constants[k];if(!buffer){D3D11_BUFFER_DESC desc{};desc.ByteWidth=cb.byteLength;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;check(device->CreateBuffer(&desc,nullptr,&buffer),"Current Effect current constant buffer");}
                context->UpdateSubresource(buffer.Get(),0,nullptr,frame.bytes.data()+cb.byteOffset,0,0);auto raw=buffer.Get();
                if(cb.stage==1)context->VSSetConstantBuffers(cb.slot,1,&raw);else context->PSSetConstantBuffers(cb.slot,1,&raw);
            }
            auto rowGlobals=globals;
            if(globals.captureScene&&std::any_of(frame.textures.begin()+d.firstTexture,
                    frame.textures.begin()+d.firstTexture+d.textureCount,[](const auto& b){return b.semantic==4;}))
                rowGlobals.scene=globals.captureScene();
            for(UINT i=0;i<d.textureCount;++i){const auto& b=frame.textures[d.firstTexture+i];auto view=texture(b,rowGlobals,resources);if(b.stage==1)context->VSSetShaderResources(b.slot,1,&view);else context->PSSetShaderResources(b.slot,1,&view);}
            for(UINT i=0;i<d.samplerCount;++i){const auto& b=frame.samplers[d.firstSampler+i];auto selectedSampler=sampler(b,globals,resources);if(b.stage==1)context->VSSetSamplers(b.slot,1,&selectedSampler);else context->PSSetSamplers(b.slot,1,&selectedSampler);}
            context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY(d.topology));
            if(d.meshId!=0xFFFFFFFFu){const auto& mesh=resources.meshes.at(d.meshId);ID3D11Buffer* vb[]{mesh.vertices.Get(),gpu?gpu->vertex():dynamic.Get()};UINT strides[]{mesh.stride,d.dynamicStride},offsets[]{0,d.dynamicOffset};context->IASetVertexBuffers(0,2,vb,strides,offsets);context->IASetIndexBuffer(mesh.indices.Get(),DXGI_FORMAT(mesh.indexFormat),0);}
            else {auto vb=gpu?gpu->vertex():dynamic.Get();context->IASetVertexBuffers(0,1,&vb,&d.dynamicStride,&d.dynamicOffset);}
            D3D11_RASTERIZER_DESC raster{};raster.FillMode=D3D11_FILL_SOLID;raster.DepthClipEnable=TRUE;if(scope.raster)scope.raster->GetDesc(&raster);
            const UINT mode=d.meshId==0xFFFFFFFFu?0:d.headerFlags&0x30;
            const UINT passes=mode==0x20?2:1;
            for(UINT pass=0;pass<passes;++pass){const UINT sourceCull=mode==0x20?(pass==0?3:2):(mode==0x10?2:1);
                // Command 1D uses KTGL codes: 1 none, 2 back, 3 front.
                // The D3D11 converter table is {1,3,2}, not an identity cast.
                const UINT nativeCull=sourceCull==2?3:(sourceCull==3?2:1);
                raster.CullMode=D3D11_CULL_MODE(nativeCull);auto& rs=rasters[key(raster)];if(!rs)check(device->CreateRasterizerState(&raster,&rs),"Current Effect source cull");context->RSSetState(rs.Get());
                if(gpu){if(d.meshId!=0xFFFFFFFFu)context->DrawIndexedInstancedIndirect(gpu->arguments(),gpu->declaration.indirectOffset);else context->DrawInstancedIndirect(gpu->arguments(),gpu->declaration.indirectOffset);}
                else if(d.meshId!=0xFFFFFFFFu)context->DrawIndexed(d.indexCount,d.indexStart,d.baseVertex);else context->Draw(d.dynamicCount,0);}
        }
    }
public:
    UINT drawCount() const {
        UINT count=UINT(frame.draws.size());for(const auto& [id,current]:directFrames)count+=UINT(current.frame.draws.size());return count;
    }
    void drawCurrentScope(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,const GlobalViews& globals,UINT selectedNode=0xFFFFFFFFu) {
        drawRows(device,context,logical,globals,frame,resources,nullptr,selectedNode);
        std::vector<std::pair<std::int64_t,std::uint64_t>> order;order.reserve(directFrames.size());
        for(const auto& [id,current]:directFrames)order.emplace_back(current.submissionOrdinal,id);
        std::sort(order.begin(),order.end());
        for(const auto& [ordinal,id]:order){const auto& current=directFrames.at(id);drawRows(device,context,logical,globals,current.frame,current.resources,direct.at(id).get(),selectedNode);}
    }
    bool hasMainBankZeroOwner() const { return !drawScope.ui&&drawScope.sourceNode==188&&mainBankZeroOwner; }
    UINT drawCount(UINT node) const {
        auto count=[&](const Frame& f){return UINT(std::count_if(f.draws.begin(),f.draws.end(),[&](const auto& row){return row.sourceNode==node;}));};
        UINT total=count(frame);for(const auto& [id,current]:directFrames)total+=count(current.frame);return total;
    }
    void drawSpecial(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,const GlobalViews& globals,UINT node) {
        if(drawScope.ui||(node!=141&&node!=147))throw std::runtime_error("Special Effect requires its Main source node");
        drawCurrentScope(device,context,logical,globals,node);
    }
    void draw188(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,const GlobalViews& globals) {
        if(drawScope.ui)throw std::runtime_error("UI effect rows do not belong to map188");
        drawCurrentScope(device,context,logical,globals,188);
    }
    void draw189(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,const GlobalViews& globals) {
        if(drawScope.ui)throw std::runtime_error("Story View1 is not a UI offscreen population");
        bool hasRows=std::any_of(frame.draws.begin(),frame.draws.end(),[](const auto& row){return row.sourceNode==189;});
        if(!hasRows)for(const auto& [id,current]:directFrames)
            if(std::any_of(current.frame.draws.begin(),current.frame.draws.end(),[](const auto& row){return row.sourceNode==189;})){hasRows=true;break;}
        if(!hasRows)return;
        // D75E50 derives the full fresh rectangle from the actual caller RTV.
        ComPtr<ID3D11RenderTargetView> color;ComPtr<ID3D11DepthStencilView> depth;
        context->OMGetRenderTargets(1,&color,&depth);
        if(!color)throw std::runtime_error("Story View1 current color target is absent");
        ComPtr<ID3D11Resource> resource;color->GetResource(&resource);ComPtr<ID3D11Texture2D> image;
        check(resource.As(&image),"Story View1 actual target texture");D3D11_TEXTURE2D_DESC desc{};image->GetDesc(&desc);
        D3D11_VIEWPORT viewport{0,0,FLOAT(desc.Width),FLOAT(desc.Height),0,1};context->RSSetViewports(1,&viewport);
        drawCurrentScope(device,context,logical,globals,189);
        context->RSSetViewports(1,&viewport); // source exit restores the full current wrapper extent
    }
    void retireOwner(std::uint64_t owner) {
        if(!owner)throw std::runtime_error("Story Effect owner token is zero");
        std::erase_if(frame.draws,[&](const auto& row){return row.sourceOwner==owner;});
        for(auto& [id,current]:directFrames)
            std::erase_if(current.frame.draws,[&](const auto& row){return row.sourceOwner==owner;});
    }
    void drawUi(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,const GlobalViews& globals) {
        if(!drawScope.ui)throw std::runtime_error("Map effect rows do not belong to UI offscreen scope");
        drawCurrentScope(device,context,logical,globals);
    }
};
}
