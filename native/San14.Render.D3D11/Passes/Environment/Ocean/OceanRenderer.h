// Original Main WV water. Prepare device objects once, then execute source strips.
#pragma once
#include <xmmintrin.h>
#include "OceanGrid.h"
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
struct NativeOceanClock {
    double accumulatedTime=0;
    float waveTime=0,normalSpeed=0,lastCommonClock=0;
    UINT bubbleMode=0;
    std::array<float,2> bubblePeriods{};
};
struct NativeOcean116 {
    NativeOceanGeometry geometry;
    std::vector<NativeOceanStrip> strips;
    ComPtr<ID3D11VertexShader> vertexShader;
    ComPtr<ID3D11PixelShader> pixelShader;
    ComPtr<ID3D11InputLayout> layout;
    std::array<ComPtr<ID3D11Buffer>,2> vertexConstants,pixelConstants;
    std::array<std::vector<char>,2> vertexBytes,pixelBytes;
    NativeOceanClock clock;
    bool constantsDirty=false;
    OceanNativeTextures textures; // Retains already uploaded external resources; no second upload.
    ComPtr<ID3D11ShaderResourceView> refractionDepth;
    ComPtr<ID3D11SamplerState> refractionSampler;
    std::array<ComPtr<ID3D11RenderTargetView>,6> targets;
    ComPtr<ID3D11DepthStencilView> depthTarget;
    ComPtr<ID3D11RasterizerState> raster;
    ComPtr<ID3D11BlendState> blend;
    ComPtr<ID3D11DepthStencilState> depth;
    D3D11_DEPTH_STENCIL_DESC depthDescription{};
    D3D11_VIEWPORT viewport{};
    UINT stencilReference=0,colorMasks=0;
};

// Original117DEFD/1184A49 preserve distinct double20 and float738 clocks.
// The selected RDEF consumes only these named phase lanes from the clock.
static bool advanceNativeOcean116(NativeOcean116& ocean,float currentCommonClock) {
    auto& clock=ocean.clock;
    float delta=_mm_cvtss_f32(_mm_sub_ss(_mm_set_ss(currentCommonClock),_mm_set_ss(clock.lastCommonClock)));
    // EBAC40's actual node116 CPU90 owns a single 3600-second correction.
    // This is neither Common258 nor node3's uncorrected CC difference.
    if(clock.lastCommonClock>currentCommonClock)
        delta=_mm_cvtss_f32(_mm_add_ss(_mm_set_ss(delta),_mm_set_ss(3600.f)));
    clock.lastCommonClock=currentCommonClock;
    if(delta==0)return false;
    clock.accumulatedTime+=double(delta);clock.waveTime=delta+clock.waveTime;
    // 117C520: the current volume depth comes from the original texture;
    // float20 and the product are rounded separately before modff.
    float integral;
    const float rate=clock.normalSpeed/float(ocean.textures.normal3d.depth);
    const float scaled=rate*float(clock.accumulatedTime);
    const float normalPhase=std::modf(scaled,&integral);
    auto store=[](std::vector<char>& bytes,UINT offset,float value){std::memcpy(bytes.data()+offset,&value,4);};
    for(UINT flat=0;flat<2;++flat) {
        store(ocean.vertexBytes[flat],200,normalPhase); // vNormalNoiseParam.z
        store(ocean.pixelBytes[flat],104,normalPhase);
        store(ocean.vertexBytes[flat],264,clock.waveTime); // vWaveParameter.z
    }
    // AAADC0: surface/inner array color row2.w uses a double fmod, parity,
    // then the original paired float quintic. Far explicitly zeroes both.
    for(UINT layer=0;layer<2;++layer) {
        float phase;
        if(clock.bubbleMode<2)phase=float(clock.bubbleMode);
        else {
            const float period=clock.bubblePeriods[layer];
            if(period<=0)continue; // Source does not replace this lane.
            phase=float(std::fmod(clock.accumulatedTime,double(period))/double(period));
            if(static_cast<long long>(clock.accumulatedTime/double(period))&1)phase=1.0f-phase;
            phase=((phase*6.0f-15.0f)*phase+10.0f)*phase*phase*phase;
        }
        store(ocean.pixelBytes[0],layer?364:300,phase);
    }
    ocean.constantsDirty=true;
    return true;
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
    const std::array<ComPtr<ID3D11Texture2D>,5>& targets,
    const std::array<ID3D11RenderTargetView*,5>& rt,DXGI_FORMAT rrFormat,
    ID3D11DepthStencilView* dsv,UINT width,UINT height,
    const fs::path& clockInput,float initialCommonClock) {
    NativeOcean116 result;
    result.textures=textures;result.refractionDepth=refractionDepth;result.depthTarget=dsv;
    result.viewport={0,0,float(width),float(height),0,1};
    std::ifstream clock(clockInput);
    clock>>result.clock.accumulatedTime>>result.clock.waveTime>>result.clock.normalSpeed
        >>result.clock.bubbleMode>>result.clock.bubblePeriods[0]>>result.clock.bubblePeriods[1];
    if(!clock)throw std::runtime_error("Original Ocean clock parameters absent");
    result.clock.lastCommonClock=initialCommonClock;
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
    auto vertex=load(source/"ocean-210A931F-p0.dxbc"),pixel=load(source/"ocean-210A931F-p1.dxbc");
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
        for(UINT stage=0;stage<2;++stage) {
            auto& bytes=stage?result.pixelBytes[flat]:result.vertexBytes[flat];
            auto& target=stage?result.pixelConstants[flat]:result.vertexConstants[flat];
            D3D11_BUFFER_DESC desc{};desc.ByteWidth=UINT(bytes.size());
            desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
            D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=bytes.data();
            check(device->CreateBuffer(&desc,&initial,&target),"original WV mutable complete CB");
        }
    }
    UINT cull,front,compare,masks,stencilEnabled,reference,test,write,blendEnabled;float clear;
    std::ifstream raster(source/"ocean-wave-raster.txt"),depthBlend(source/"ocean-depth-blend.txt");
    raster>>cull>>front>>compare>>masks>>stencilEnabled>>reference>>clear;
    depthBlend>>test>>write>>blendEnabled;
    if(!raster||!depthBlend)throw std::runtime_error("Original WV state absent");
    result.stencilReference=reference;result.colorMasks=masks;
    D3D11_RASTERIZER_DESC rd{};rd.FillMode=D3D11_FILL_SOLID;rd.CullMode=D3D11_CULL_MODE(cull);
    rd.FrontCounterClockwise=front;rd.DepthClipEnable=TRUE;
    check(device->CreateRasterizerState(&rd,&result.raster),"original WV raster");
    auto& dd=result.depthDescription;
    dd.DepthEnable=test;dd.DepthWriteMask=write?D3D11_DEPTH_WRITE_MASK_ALL:D3D11_DEPTH_WRITE_MASK_ZERO;
    dd.DepthFunc=D3D11_COMPARISON_FUNC(compare); // Preparation seed; execution inherits logical compare.
    dd.StencilEnable=stencilEnabled;dd.StencilReadMask=dd.StencilWriteMask=255;
    dd.FrontFace.StencilFunc=D3D11_COMPARISON_ALWAYS;
    dd.FrontFace.StencilFailOp=dd.FrontFace.StencilDepthFailOp=D3D11_STENCIL_OP_KEEP;
    dd.FrontFace.StencilPassOp=D3D11_STENCIL_OP_REPLACE;dd.BackFace=dd.FrontFace;
    check(device->CreateDepthStencilState(&dd,&result.depth),"original WV depth/stencil");
    // D3C310 does not assign color masks. These are the imported inherited
    // source-created Main masks, including all six original MRT writes.
    D3D11_BLEND_DESC bd{};bd.IndependentBlendEnable=TRUE;
    for(UINT i=0;i<6;++i){bd.RenderTarget[i].BlendEnable=blendEnabled;bd.RenderTarget[i].RenderTargetWriteMask=BYTE((masks>>(4*i))&15);}
    check(device->CreateBlendState(&bd,&result.blend),"original WV six color masks");
    for(UINT i=0;i<5;++i)result.targets[i]=rt[i];
    D3D11_RENDER_TARGET_VIEW_DESC rr{};rr.Format=rrFormat;rr.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
    rr.Texture2DArray.FirstArraySlice=1;rr.Texture2DArray.ArraySize=1;
    check(device->CreateRenderTargetView(targets[3].Get(),&rr,&result.targets[5]),"original WV RR1");
    auto samplerBytes=load(refractionSampler);D3D11_SAMPLER_DESC sd{};
    if(samplerBytes.size()!=sizeof(sd))throw std::runtime_error("WV refraction sampler requires52B");
    std::memcpy(&sd,samplerBytes.data(),sizeof(sd));
    check(device->CreateSamplerState(&sd,&result.refractionSampler),"original WV refraction sampler");
    return result;
}

static UINT executeNativeOcean116(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeSourceDepthState& logical,NativeOcean116& ocean,float currentCommonClock) {
    advanceNativeOcean116(ocean,currentCommonClock);
    if(ocean.constantsDirty) {
        for(UINT flat=0;flat<2;++flat) {
            context->UpdateSubresource(ocean.vertexConstants[flat].Get(),0,nullptr,ocean.vertexBytes[flat].data(),0,0);
            context->UpdateSubresource(ocean.pixelConstants[flat].Get(),0,nullptr,ocean.pixelBytes[flat].data(),0,0);
        }
        ocean.constantsDirty=false;
    }
    // WV changes Test/Write and its stencil writer, but leaves depth compare.
    // Recreate this device state only if an actual inherited compare changes.
    if(ocean.depthDescription.DepthFunc!=logical.description.DepthFunc) {
        ocean.depthDescription.DepthFunc=logical.description.DepthFunc;
        ocean.depth.Reset();
        check(device->CreateDepthStencilState(&ocean.depthDescription,&ocean.depth),"original WV changed inherited depth compare");
    }
    std::array<ID3D11ShaderResourceView*,32> pixelViews{};
    pixelViews[0]=ocean.textures.normal3d.view.Get();pixelViews[2]=ocean.refractionDepth.Get();
    pixelViews[3]=ocean.textures.normalNoise2d.view.Get();pixelViews[4]=ocean.textures.wvBubble.view.Get();
    context->PSSetShaderResources(0,UINT(pixelViews.size()),pixelViews.data());
    std::array<ID3D11ShaderResourceView*,2> vertexViews{ocean.textures.wvHeightMask.view.Get(),ocean.textures.horizontalDisplacement.view.Get()};
    context->VSSetShaderResources(0,2,vertexViews.data());
    std::array<ID3D11SamplerState*,5> pixelSamplers{ocean.textures.normal3d.sampler.Get(),nullptr,ocean.refractionSampler.Get(),ocean.textures.normalNoise2d.sampler.Get(),ocean.textures.wvBubble.sampler.Get()};
    std::array<ID3D11SamplerState*,2> vertexSamplers{ocean.textures.wvHeightMask.sampler.Get(),ocean.textures.horizontalDisplacement.sampler.Get()};
    context->PSSetSamplers(0,5,pixelSamplers.data());context->VSSetSamplers(0,2,vertexSamplers.data());
    context->IASetInputLayout(ocean.layout.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    context->VSSetShader(ocean.vertexShader.Get(),nullptr,0);context->PSSetShader(ocean.pixelShader.Get(),nullptr,0);
    context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
    context->RSSetViewports(1,&ocean.viewport);context->RSSetState(ocean.raster.Get());
    logical.bind(context,ocean.depth.Get(),ocean.depthDescription,ocean.stencilReference);
    context->OMSetBlendState(ocean.blend.Get(),nullptr,~0u);
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
    // that transition; leave reached depth/stencil, blend and raster state.
    context->OMSetRenderTargets(0,nullptr,nullptr);
    pixelViews.fill(nullptr);vertexViews.fill(nullptr);
    context->PSSetShaderResources(0,UINT(pixelViews.size()),pixelViews.data());context->VSSetShaderResources(0,2,vertexViews.data());
    return UINT(ocean.strips.size());
}
