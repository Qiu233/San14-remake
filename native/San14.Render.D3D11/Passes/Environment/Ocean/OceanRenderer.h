// Original Main WV water. Prepare device objects once, then execute source strips.
#pragma once
#include "OceanClock.h"
#include "OceanDrawState.h"
#include "../../../Device/RenderTargetScope.h"
#include "Interop/OceanMaterialConstants.h"
#include "OceanGrid.h"
#include <memory>
#include <optional>
struct NativeOceanGeometry {
    std::array<ComPtr<ID3D11Buffer>,3> wave;
    ComPtr<ID3D11Buffer> farVertices,waveIndices,farIndices;
    std::array<UINT,3> strides{},offsets{};
};
struct NativeOceanStrip {
    UINT ordinal=0,flat=0,indexCount=0,startIndex=0;
    int baseVertex=0;
    std::array<UINT,3> offsets{};
};
// The ordinary constructor allocates one initially empty texture-group entry.
// Group ownership and selected index admit bubble scalar setters independently
// of child/metadata readiness. Current resource rebinding remains separate.
struct OceanBubbleMaskGroup {
    std::array<std::optional<OceanNativeTexture>,1> entries;
};
struct NativeOcean116 {
    NativeOceanGeometry geometry;
    std::vector<NativeOceanStrip> strips;
    ComPtr<ID3D11VertexShader> vertexShader;
    ComPtr<ID3D11PixelShader> pixelShader;
    ComPtr<ID3D11InputLayout> layout;
    std::array<ComPtr<ID3D11Buffer>,2> vertexConstants,pixelConstants;
    std::array<std::vector<char>,2> vertexBytes,pixelBytes;
    San14::Render::OceanClock clock;
    San14::Render::OceanSurfaceSettings surface;
    San14::Render::OceanOpticsSettings optics;
    San14::Render::OceanTextureMaterialState textureMaterial;
    std::unique_ptr<OceanBubbleMaskGroup> bubbleMaskGroup;
    int bubbleMaskIndex=-1;
    bool constantsDirty=false;
    OceanNativeTextures textures; // Retains already uploaded external resources; no second upload.
    ComPtr<ID3D11ShaderResourceView> refractionDepth;
    bool refractionMaterialBound=false;
    ComPtr<ID3D11SamplerState> refractionSampler;
    std::array<ComPtr<ID3D11RenderTargetView>,6> targets;
    ComPtr<ID3D11DepthStencilView> depthTarget;
    NativeOceanDrawState drawState;
    D3D11_VIEWPORT viewport{};
};

static void applyNativeOceanSurfaceSettings(NativeOcean116& ocean,const San14::Render::OceanSurfaceSettings& settings) {
    auto accepted=settings;
    if(!ocean.bubbleMaskGroup||ocean.bubbleMaskIndex<0) {
        accepted.bubbleMode=ocean.surface.bubbleMode;
        accepted.bubblePeriods=ocean.surface.bubblePeriods;
    }
    ocean.surface=accepted;
}
static void applyNativeOceanTextureMaterialSettings(NativeOcean116& ocean,
    const San14::Render::OceanTextureMaterialSettings& settings) {
    San14::Render::applyOceanTextureMaterialSettings(ocean.textureMaterial,settings,ocean.textures.materialBindings());
}
static void applyNativeOceanOpticsSettings(NativeOcean116& ocean,const San14::Render::OceanOpticsSettings& settings) {
    ocean.optics=San14::Render::acceptOceanOpticsSettings(settings);
}

// Material callbacks publish phases even when elapsed time is unchanged.
// The accepted Common clock is advanced only by the Ocean lifetime owner.
static void writeNativeOceanAnimation(NativeOcean116& ocean) {
    const auto phases=San14::Render::deriveOceanAnimationPhases(ocean.clock,ocean.surface,ocean.textures.normal3d?ocean.textures.normal3d->depth:0u);
    San14::Render::Interop::writeOceanAnimationPhases(ocean.vertexBytes,ocean.pixelBytes,phases,ocean.textures.normal3d.has_value());
    San14::Render::Interop::writeOceanSurfaceConstants(ocean.vertexBytes,ocean.surface);
    San14::Render::Interop::writeOceanOpticsConstants(ocean.pixelBytes,ocean.optics,ocean.refractionMaterialBound);
    San14::Render::Interop::writeOceanMaterialUvConstants(ocean.vertexBytes,ocean.pixelBytes,
        ocean.textureMaterial,ocean.textures.materialBindings());
    San14::Render::Interop::writeOceanTextureWaveConstants(ocean.vertexBytes,ocean.textureMaterial);
    ocean.constantsDirty=true;
}

// 1183A40 publishes the completed CPU buffer into the retained GPU wave VB.
// The producer preserves the source float3+half2 layout and owns its lifetime.
static void updateNativeOceanGeometry(ID3D11DeviceContext* context,NativeOcean116& ocean,
    const void* sourceVertices,UINT bytes) {
    D3D11_BOX range{0,0,0,bytes,1,1};
    context->UpdateSubresource(ocean.geometry.wave[0].Get(),0,&range,sourceVertices,0,0);
}

// Target/texture identities are persistent. Reprepare on actual source reload or
// target resize/replacement; ordinary requests perform no file or resource reads.
static NativeOcean116 prepareNativeOcean116(ID3D11Device* device,
    const fs::path& source,const OceanNativeTextures& textures,
    const San14::Render::OceanGrid& grid,const void* initialWaves,
    ID3D11ShaderResourceView* refractionDepth,const fs::path& refractionSampler,
    const NativeMainTargetScope& targets,
    const San14::Render::OceanSurfaceSettings& surface,const San14::Render::OceanOpticsSettings& optics,
    bool refractionMaterialBound) {
    NativeOcean116 result;
    result.textures=textures;result.refractionDepth=refractionDepth;
    if(targets.colorCount!=6 || targets.viewportCount!=1 || !targets.depth)
        throw std::runtime_error("Ocean requires its actual six-target geometry scope");
    result.depthTarget=targets.depth;result.viewport=targets.viewports[0];result.drawState.device=device;
    for(UINT i=0;i<6;++i)result.targets[i]=targets.colors[i];
    result.bubbleMaskGroup=std::make_unique<OceanBubbleMaskGroup>();result.bubbleMaskIndex=0;
    applyNativeOceanSurfaceSettings(result,surface);
    applyNativeOceanOpticsSettings(result,optics);result.refractionMaterialBound=refractionMaterialBound;
    // The static stream layout and topology are constructed from the named
    // renderer declaration. Completed FFT output supplies the first wave side.
    D3D11_BUFFER_DESC wave{};
    wave.ByteWidth=UINT(grid.vertexCount()*16);wave.Usage=D3D11_USAGE_DEFAULT;
    wave.BindFlags=D3D11_BIND_VERTEX_BUFFER;
    D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=initialWaves;
    check(device->CreateBuffer(&wave,&initial,&result.geometry.wave[0]),"Ocean constructed mutable wave VB");
    std::vector<char> uv(grid.bubbleUvs.size()*sizeof(San14::Render::OceanBubbleUv));
    std::memcpy(uv.data(),grid.bubbleUvs.data(),uv.size());
    result.geometry.wave[2]=buffer(device,uv,D3D11_BIND_VERTEX_BUFFER);
    result.geometry.strides={16,0,4};
    std::vector<std::uint16_t> narrowIndices(grid.indices.begin(),grid.indices.end());
    std::vector<char> indices(narrowIndices.size()*sizeof(std::uint16_t));
    std::memcpy(indices.data(),narrowIndices.data(),indices.size());
    result.geometry.waveIndices=buffer(device,indices,D3D11_BIND_INDEX_BUFFER);
    std::vector<char> farIndices(sizeof(grid.farIndices));
    std::memcpy(farIndices.data(),grid.farIndices.data(),farIndices.size());
    result.geometry.farIndices=buffer(device,farIndices,D3D11_BIND_INDEX_BUFFER);
    auto vertex=load(source/"shaders/vs.dxbc"),pixel=load(source/"shaders/ps.dxbc");
    check(device->CreateVertexShader(vertex.data(),vertex.size(),nullptr,&result.vertexShader),"original WV VS");
    check(device->CreatePixelShader(pixel.data(),pixel.size(),nullptr,&result.pixelShader),"original WV PS");
    std::array<D3D11_INPUT_ELEMENT_DESC,4> elements{{
        {"POSITION",0,DXGI_FORMAT_R32G32B32_FLOAT,0,0,D3D11_INPUT_PER_VERTEX_DATA,0},
        {"NORMAL",0,DXGI_FORMAT_R16G16_FLOAT,0,12,D3D11_INPUT_PER_VERTEX_DATA,0},
        {"TEXCOORD",0,DXGI_FORMAT_R32G32B32_FLOAT,1,0,D3D11_INPUT_PER_INSTANCE_DATA,1},
        {"TEXCOORD",1,DXGI_FORMAT_R16G16_FLOAT,2,0,D3D11_INPUT_PER_VERTEX_DATA,0}}};
    check(device->CreateInputLayout(elements.data(),UINT(elements.size()),vertex.data(),vertex.size(),&result.layout),"original WV input layout");
    for(UINT flat=0;flat<2;++flat) {
        std::string prefix=flat?"ocean-far":"ocean-wave";
        result.vertexBytes[flat]=load(source/(prefix+"-vs-cb0.bin"));
        result.pixelBytes[flat]=load(source/(prefix+"-ps-cb0.bin"));
        if(result.vertexBytes[flat].size()!=384||result.pixelBytes[flat].size()!=864)
            throw std::runtime_error("Ocean material requires selected VS384/PS864 layout");
        for(UINT stage=0;stage<2;++stage) {
            auto& bytes=stage?result.pixelBytes[flat]:result.vertexBytes[flat];
            auto& target=stage?result.pixelConstants[flat]:result.vertexConstants[flat];
            D3D11_BUFFER_DESC desc{};desc.ByteWidth=UINT(bytes.size());
            desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
            D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=bytes.data();
            check(device->CreateBuffer(&desc,&initial,&target),"original WV mutable complete CB");
        }
    }
    auto samplerBytes=load(refractionSampler);D3D11_SAMPLER_DESC sd{};
    if(samplerBytes.size()!=sizeof(sd))throw std::runtime_error("WV refraction sampler requires52B");
    std::memcpy(&sd,samplerBytes.data(),sizeof(sd));
    check(device->CreateSamplerState(&sd,&result.refractionSampler),"original WV refraction sampler");
    return result;
}

static UINT executeNativeOcean116(ID3D11DeviceContext* context,
    NativeSourceDepthState& logical,NativeOcean116& ocean,std::uint32_t group,
    const San14::Render::CommonRenderPolicy& policy) {
    if(!oceanDrawEnabled(group,ocean.optics.refractionTransparency,policy))return 0;
    writeNativeOceanAnimation(ocean);
    if(ocean.constantsDirty) {
        for(UINT flat=0;flat<2;++flat) {
            context->UpdateSubresource(ocean.vertexConstants[flat].Get(),0,nullptr,ocean.vertexBytes[flat].data(),0,0);
            context->UpdateSubresource(ocean.pixelConstants[flat].Get(),0,nullptr,ocean.pixelBytes[flat].data(),0,0);
        }
        ocean.constantsDirty=false;
    }
    NativeOceanDrawScope drawState(ocean.drawState,context,logical,group);
    std::array<ID3D11ShaderResourceView*,32> pixelViews{};
    pixelViews[0]=oceanTextureView(ocean.textures.normal3d);pixelViews[2]=ocean.refractionDepth.Get();
    pixelViews[3]=oceanTextureView(ocean.textures.normalNoise2d);pixelViews[4]=oceanTextureView(ocean.textures.wvBubble);
    context->PSSetShaderResources(0,UINT(pixelViews.size()),pixelViews.data());
    std::array<ID3D11ShaderResourceView*,2> vertexViews{oceanTextureView(ocean.textures.wvHeightMask),oceanTextureView(ocean.textures.horizontalDisplacement)};
    context->VSSetShaderResources(0,2,vertexViews.data());
    std::array<ID3D11SamplerState*,5> pixelSamplers{oceanTextureSampler(ocean.textures.normal3d),nullptr,ocean.refractionSampler.Get(),oceanTextureSampler(ocean.textures.normalNoise2d),oceanTextureSampler(ocean.textures.wvBubble)};
    std::array<ID3D11SamplerState*,2> vertexSamplers{oceanTextureSampler(ocean.textures.wvHeightMask),oceanTextureSampler(ocean.textures.horizontalDisplacement)};
    context->PSSetSamplers(0,5,pixelSamplers.data());context->VSSetSamplers(0,2,vertexSamplers.data());
    context->IASetInputLayout(ocean.layout.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    context->VSSetShader(ocean.vertexShader.Get(),nullptr,0);context->PSSetShader(ocean.pixelShader.Get(),nullptr,0);
    context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
    context->RSSetViewports(1,&ocean.viewport);
    std::array<ID3D11RenderTargetView*,6> rt{};
    for(UINT i=0;i<6;++i)rt[i]=ocean.targets[i].Get();
    context->OMSetRenderTargets(UINT(rt.size()),rt.data(),ocean.depthTarget.Get());
    for(const auto& packet:ocean.strips) {
        ID3D11Buffer* vertices[]{packet.flat?ocean.geometry.farVertices.Get():ocean.geometry.wave[0].Get(),ocean.geometry.wave[1].Get(),ocean.geometry.wave[2].Get()};
        context->IASetVertexBuffers(0,3,vertices,ocean.geometry.strides.data(),packet.offsets.data());
        context->IASetIndexBuffer(packet.flat?ocean.geometry.farIndices.Get():ocean.geometry.waveIndices.Get(),DXGI_FORMAT_R16_UINT,0);
        auto vc=ocean.vertexConstants[packet.flat].Get(),pc=ocean.pixelConstants[packet.flat].Get();
        context->VSSetConstantBuffers(0,1,&vc);context->PSSetConstantBuffers(0,1,&pc);
        context->DrawIndexed(packet.indexCount,packet.startIndex,packet.baseVertex);
    }
    // Source117 owns attachment Pop and Main mirror copy. Release bindings for
    // that transition. The draw scope then publishes the reached exit state.
    context->OMSetRenderTargets(0,nullptr,nullptr);
    pixelViews.fill(nullptr);vertexViews.fill(nullptr);
    context->PSSetShaderResources(0,UINT(pixelViews.size()),pixelViews.data());context->VSSetShaderResources(0,2,vertexViews.data());
    return UINT(ocean.strips.size());
}
