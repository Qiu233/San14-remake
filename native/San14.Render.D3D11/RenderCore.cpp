// Original Main renderer. The client session and experiment CLI share this
// pipeline, with one retained set of original resources and device objects.
#define NOMINMAX
#include <d3d11.h>
#include <d3dcompiler.h>
#include <d3d11shader.h>
#include <wrl/client.h>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <vector>
#include <array>
#include <map>
#include <string>
#include <cstring>
#include <cmath>
#include <algorithm>
#include <chrono>
#include <stdexcept>
#include "ArmyUnitHeight.h"
#include "MainCameraInitialNumeric.h"
using Microsoft::WRL::ComPtr;
namespace fs = std::filesystem;
static void check(HRESULT h, const char* what) {
    if (FAILED(h)) { std::cerr << what << " HRESULT=0x" << std::hex << h << '\n'; throw std::runtime_error(what); }
}
static std::vector<char> load(const fs::path& path) {
    std::ifstream f(path, std::ios::binary);
    if (!f) throw std::runtime_error("Missing input: " + path.string());
    return {std::istreambuf_iterator<char>(f), std::istreambuf_iterator<char>()};
}
static uint32_t u32(std::istream& f) { uint32_t n; f.read(reinterpret_cast<char*>(&n), 4); return n; }
static ComPtr<ID3D11Buffer> buffer(ID3D11Device* d, const std::vector<char>& b, UINT binding) {
    D3D11_BUFFER_DESC desc{}; desc.ByteWidth=UINT(b.size()); desc.Usage=D3D11_USAGE_IMMUTABLE; desc.BindFlags=binding;
    D3D11_SUBRESOURCE_DATA data{}; data.pSysMem=b.data(); ComPtr<ID3D11Buffer> out;
    check(d->CreateBuffer(&desc,&data,&out),"buffer"); return out;
}
static ComPtr<ID3D11Texture2D> loadTexture(ID3D11Device* device,const fs::path& path) {
    std::ifstream f(path,std::ios::binary);if(!f)throw std::runtime_error(path.string());
    D3D11_TEXTURE2D_DESC d{};d.Width=u32(f);d.Height=u32(f);d.ArraySize=u32(f);d.MipLevels=u32(f);d.Format=DXGI_FORMAT(u32(f));d.SampleDesc.Count=1;d.Usage=D3D11_USAGE_IMMUTABLE;d.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    std::vector<std::vector<char>> storage(d.ArraySize*d.MipLevels);std::vector<D3D11_SUBRESOURCE_DATA> initial(storage.size());
    for(size_t i=0;i<storage.size();++i){initial[i].SysMemPitch=u32(f);initial[i].SysMemSlicePitch=u32(f);storage[i].resize(u32(f));f.read(storage[i].data(),storage[i].size());initial[i].pSysMem=storage[i].data();}
    ComPtr<ID3D11Texture2D> texture;check(device->CreateTexture2D(&d,initial.data(),&texture),"source texture");return texture;
}
static UINT pixelBytes(DXGI_FORMAT format) {
    switch(format) {
        case DXGI_FORMAT_R32G32B32A32_FLOAT: return 16;
        case DXGI_FORMAT_R16G16B16A16_FLOAT: case DXGI_FORMAT_R16G16B16A16_UNORM: return 8;
        case DXGI_FORMAT_R32G32_FLOAT: return 8;
        case DXGI_FORMAT_D32_FLOAT_S8X24_UINT: return 8;
        case DXGI_FORMAT_R32_FLOAT: case DXGI_FORMAT_D32_FLOAT:
        case DXGI_FORMAT_R8G8B8A8_UNORM: case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:
        case DXGI_FORMAT_B8G8R8A8_UNORM: case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:
        case DXGI_FORMAT_R16G16_FLOAT: case DXGI_FORMAT_R16G16_UNORM: case DXGI_FORMAT_R10G10B10A2_UNORM:
        case DXGI_FORMAT_R11G11B10_FLOAT: return 4;
        case DXGI_FORMAT_R16_FLOAT: return 2;
        case DXGI_FORMAT_R8_UNORM: return 1;
        default: throw std::runtime_error("Unsupported readback format " + std::to_string(format));
    }
}
static float halfFloat(uint16_t bits) {
    uint32_t sign=(bits&0x8000u)<<16, exponent=(bits>>10)&31u, fraction=bits&1023u, raw;
    if(exponent==0) {
        if(fraction==0)raw=sign;
        else { int shift=0; while((fraction&1024u)==0){fraction<<=1; ++shift;}
            raw=sign|uint32_t(127-14-shift)<<23|(fraction&1023u)<<13; }
    } else raw=sign|((exponent==31?255:exponent+112)<<23)|fraction<<13;
    float value; std::memcpy(&value,&raw,4); return value;
}
static int storedChannels(DXGI_FORMAT format) {
    if(format==DXGI_FORMAT_R11G11B10_FLOAT)return 3;
    if(format==DXGI_FORMAT_R32G32_FLOAT||format==DXGI_FORMAT_R16G16_FLOAT||format==DXGI_FORMAT_R16G16_UNORM)return 2;
    if(format==DXGI_FORMAT_R32_FLOAT||format==DXGI_FORMAT_D32_FLOAT||format==DXGI_FORMAT_D32_FLOAT_S8X24_UINT||format==DXGI_FORMAT_R16_FLOAT||format==DXGI_FORMAT_R8_UNORM)return 1;
    return 4;
}
static std::array<float,4> decode(const char* p, DXGI_FORMAT format) {
    std::array<float,4> out{0,0,0,1};
    if(format==DXGI_FORMAT_R32G32B32A32_FLOAT) std::memcpy(out.data(),p,16);
    else if(format==DXGI_FORMAT_R32G32_FLOAT) std::memcpy(out.data(),p,8);
    else if(format==DXGI_FORMAT_R32_FLOAT || format==DXGI_FORMAT_D32_FLOAT || format==DXGI_FORMAT_D32_FLOAT_S8X24_UINT)
        std::memcpy(out.data(),p,4);
    else if(format==DXGI_FORMAT_R16G16B16A16_FLOAT || format==DXGI_FORMAT_R16G16_FLOAT || format==DXGI_FORMAT_R16_FLOAT) {
        int count= format==DXGI_FORMAT_R16G16B16A16_FLOAT?4:format==DXGI_FORMAT_R16G16_FLOAT?2:1;
        for(int i=0;i<count;++i){uint16_t bits;std::memcpy(&bits,p+i*2,2);out[i]=halfFloat(bits);}
    } else if(format==DXGI_FORMAT_R16G16B16A16_UNORM||format==DXGI_FORMAT_R16G16_UNORM) {
        int count=format==DXGI_FORMAT_R16G16B16A16_UNORM?4:2;
        for(int i=0;i<count;++i){uint16_t bits;std::memcpy(&bits,p+i*2,2);out[i]=float(bits)/65535;}
    } else if(format==DXGI_FORMAT_R8_UNORM) out[0]=float(uint8_t(*p))/255;
    else if(format==DXGI_FORMAT_R11G11B10_FLOAT) {
        uint32_t bits;std::memcpy(&bits,p,4);
        for(int i=0;i<3;++i){int mantissa=i==2?5:6;uint32_t value=(bits>>(i==0?0:i==1?11:22))&((1u<<(mantissa+5))-1);
            uint32_t fraction=value&((1u<<mantissa)-1),exponent=value>>mantissa;
            out[i]=exponent==0?std::ldexp(float(fraction),1-15-mantissa):exponent==31?(fraction?NAN:INFINITY):std::ldexp(1.f+float(fraction)/float(1u<<mantissa),int(exponent)-15);}
    }
    else if(format==DXGI_FORMAT_R10G10B10A2_UNORM) {
        uint32_t bits;std::memcpy(&bits,p,4);
        for(int i=0;i<3;++i)out[i]=float((bits>>(i*10))&1023u)/1023;out[3]=float(bits>>30)/3;
    } else {
        bool bgra=format==DXGI_FORMAT_B8G8R8A8_UNORM||format==DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
        for(int i=0;i<4;++i)out[i]=float(uint8_t(p[bgra&&i<3?2-i:i]))/255;
    }
    return out;
}
static void bmp(const fs::path& path, const std::vector<char>& image, DXGI_FORMAT format, UINT w, UINT h) {
    BITMAPFILEHEADER file{};file.bfType=0x4d42;file.bfOffBits=sizeof(file)+sizeof(BITMAPINFOHEADER);file.bfSize=file.bfOffBits+w*h*4;
    BITMAPINFOHEADER info{};info.biSize=sizeof(info);info.biWidth=w;info.biHeight=-int(h);info.biPlanes=1;info.biBitCount=32;info.biCompression=BI_RGB;
    std::ofstream f(path,std::ios::binary);f.write(reinterpret_cast<char*>(&file),sizeof(file));f.write(reinterpret_cast<char*>(&info),sizeof(info));
    UINT bytes=pixelBytes(format);std::array<uint8_t,4> row;
    for(UINT i=0;i<w*h;++i){auto value=decode(image.data()+i*bytes,format);for(int c=0;c<3;++c)row[2-c]=uint8_t(std::clamp(value[c],0.f,1.f)*255+.5f);row[3]=255;f.write(reinterpret_cast<char*>(row.data()),4);}
}
#include "Pipeline/source-depth-state-native.h"
#include "Pipeline/rs68.h"
#include "Pipeline/ambient-native.h"
#include "Pipeline/lighting.h"
#include "Pipeline/post-native.h"
#include "Pipeline/model-native.h"
#include "Pipeline/camera-continuous-native.h"
#include "Pipeline/camera-motion-native.h"
#include "Pipeline/camera-clock-native.h"
#include "Pipeline/model-continuous-native.h"
#include "Pipeline/view-continuous-native.h"
#include "Pipeline/view-gpu-native.h"
#include "Pipeline/terrain-camera-gpu-native.h"
#include "Pipeline/shadow-native.h"
#include "Pipeline/depth-mirror-native.h"
#include "Pipeline/shadow-receiver-native.h"
#include "Pipeline/refraction-depth-native.h"
#include "Pipeline/ocean-textures-native.h"
#include "Pipeline/source-hdr-native.h"
#include "Pipeline/deferred-camera-gpu-native.h"
#include "Pipeline/shadow-camera-continuous-native.h"
#include "Pipeline/shadow-camera-gpu-native.h"
#include "Pipeline/ocean-native.h"
#include "Pipeline/water-camera-gpu-native.h"
#include "Pipeline/ocean-fft-native.h"
#include "Pipeline/refraction-color-native.h"
#include "Pipeline/normal-post-native.h"
#include "Pipeline/normal-post-continuous-native.h"
#include "Pipeline/normal-post-camera-native.h"
#include "Pipeline/main-target-scopes-native.h"
#include "Pipeline/native-presentation.h"
#include "Pipeline/frame-clock-native.h"
#include "Pipeline/map-fog-native.h"
#include "Pipeline/main-sky-native.h"
#include "Pipeline/main-cloud-native.h"
#include "Pipeline/ambient-cloud-continuous-native.h"
#include "Pipeline/sub-shadow-native.h"
#include "Pipeline/effect-provider-native.h"
#include "Pipeline/effect-consumer-native.h"
#include "Pipeline/effect-camera-continuous-native.h"
#include "Pipeline/atmosphere-camera-continuous-native.h"
#include "Pipeline/weather-fog-camera-continuous-native.h"
#include "Pipeline/weather-fog-camera-gpu-native.h"
#include "Pipeline/fog-accel-native.h"
#include "Pipeline/fog-accel-camera-gpu-native.h"
#include "Pipeline/special-effect-state-native.h"
#include "Pipeline/map-exposure-native.h"
#include "Pipeline/visual-layer/territory-gpu-native.h"
#include "Pipeline/visual-layer/zone-layer-native.h"
#include "Pipeline/city-model-native.h"
#include "Pipeline/city-model-population-native.h"
#include "Pipeline/area-primary-native.h"
#include "Pipeline/joint-population-shadow-native.h"
#include "Pipeline/city-gate-shadow-native.h"
#include "Pipeline/city-area-shadow-native.h"
#include "Pipeline/primary-forward-native.h"
#include "Pipeline/primary-tail-source-review/primary-forward-continuous-native.h"
#include "Pipeline/city-accessory-gpu-native.h"
#include "Pipeline/default-player-phase-native.h"
#include "Pipeline/base-visual-world-native.h"
#include "AreaCapitalAccessories.h"
#include "CityGateFlagImports.h"
#include "Pipeline/army-population-native.h"
#include "Pipeline/army-current-animation-native.h"
#include "Pipeline/army-graphics-actions-native.h"
#include "Pipeline/animation-cadence-source/cadence-native.h"
#include "ArmyWorldMaterialImport.h"
#include "ArmyWorldMaterialBank.h"
#include "ArmyWorldModeImport.h"
#include "ArmyWorldModeBounds.h"
#include "ArmyWorldModelPool.h"
#include "SharedCategory2Accessories.h"
#include "Pipeline/current-season-shadow-native.h"
#include "Pipeline/current-season-client-input/current-season-hdr-gpu.h"
#include "Pipeline/current-season-client-input/current-season-atmosphere-gpu.h"
#include "Pipeline/current-season-post-native.h"
#include "Pipeline/current-cloud-weather-native.h"
#include "Pipeline/current-ambient-frame-native.h"
#include "Pipeline/current-main-sky-moon-native.h"
#include "Pipeline/current-height-fog-native.h"
#include "Pipeline/primary-forward-current-bank-native.h"
#include "Pipeline/work-cloud-clip-native.h"
#include "Pipeline/current-season-client-input/current-season-terrain-palette-native.h"
#include "Pipeline/effect-field-admission-native.h"
#include "GroundDetails.h"
#include "FieldDisplay.h"
#ifdef SAN14_RENDER_SESSION
#include "RenderSession.h"
#define SAN14_RENDER_RETURN(value) co_return value
#else
#define SAN14_RENDER_RETURN(value) return value
#endif
static void traceNativeDepthState(ID3D11DeviceContext* context,const char* phase) {
    ComPtr<ID3D11DepthStencilState> state;UINT reference;context->OMGetDepthStencilState(&state,&reference);
    D3D11_DEPTH_STENCIL_DESC d{};
    if(state)state->GetDesc(&d);
    else {d.DepthEnable=TRUE;d.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ALL;d.DepthFunc=D3D11_COMPARISON_LESS;}
    std::cout<<"native depth state "<<phase<<" test="<<d.DepthEnable<<" write="<<d.DepthWriteMask
        <<" compare="<<d.DepthFunc<<" stencil="<<d.StencilEnable<<" ref="<<reference<<'\n';
}
static void traceNativeTargetBinding(ID3D11DeviceContext* context,const char* phase,
    UINT count,ID3D11RenderTargetView* const* expected,ID3D11DepthStencilView* expectedDepth) {
    ID3D11RenderTargetView* actual[8]{};ComPtr<ID3D11DepthStencilView> depth;
    context->OMGetRenderTargets(8,actual,&depth);UINT active=0;bool equal=depth.Get()==expectedDepth;
    for(UINT i=0;i<8;++i){if(actual[i])active=i+1;equal&=actual[i]==(i<count?expected[i]:nullptr);if(actual[i])actual[i]->Release();}
    std::cout<<"native target binding "<<phase<<" actualColors="<<active<<" expectedColors="<<count<<" actualIdentity="<<equal<<'\n';
}
static float sourceCommonClock(const fs::path& directory) {
    float value;std::ifstream input(directory/"source-common-clock.txt");
    if(!(input>>value))throw std::runtime_error("Source Common254 clock input absent");
    return value;
}
static void captureSourceShadowMap(ID3D11Device* device,ID3D11DeviceContext* context,
    const NativeShadowMap& map,UINT compare,const fs::path& output) {
    D3D11_TEXTURE2D_DESC td{};map.texture->GetDesc(&td);td.Usage=D3D11_USAGE_STAGING;td.BindFlags=0;td.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
    ComPtr<ID3D11Texture2D> staging;check(device->CreateTexture2D(&td,nullptr,&staging),"shadow depth readback");context->CopyResource(staging.Get(),map.texture.Get());
    D3D11_MAPPED_SUBRESOURCE data{};check(context->Map(staging.Get(),0,D3D11_MAP_READ,0,&data),"shadow depth map");
    UINT covered=0,nonfinite=0;float lo=1,hi=0;std::ofstream file(output/"shadow-depth.bin",std::ios::binary);
    for(UINT y=0;y<td.Height;++y){const char* row=static_cast<const char*>(data.pData)+y*data.RowPitch;file.write(row,td.Width*4);
        for(UINT x=0;x<td.Width;++x){float z;std::memcpy(&z,row+x*4,4);covered+=z!=1;nonfinite+=!std::isfinite(z);lo=std::min(lo,z);hi=std::max(hi,z);}}
    context->Unmap(staging.Get(),0);
    std::cout<<"source shadow casterDraws="<<map.draws<<" extent="<<td.Width<<'x'<<td.Height<<" compare="<<compare<<" clear=1 covered="<<covered<<" nonfinite="<<nonfinite<<" min="<<lo<<" max="<<hi<<'\n';
}
static NativeShadowMap drawSourceCasterMap(ID3D11Device* device,ID3D11DeviceContext* context,
    const fs::path& source,const fs::path& modelSource,const NativeModelInputs& models,const fs::path& output) {
    UINT test,write,compare,stencil;std::ifstream state(source/"queue-depth.txt");state>>test>>write>>compare>>stencil;
    if(!state)throw std::runtime_error("Source reached shadow depth state absent");
    D3D11_DEPTH_STENCIL_DESC ds{};ds.DepthEnable=test;ds.DepthWriteMask=write?D3D11_DEPTH_WRITE_MASK_ALL:D3D11_DEPTH_WRITE_MASK_ZERO;
    ds.DepthFunc=D3D11_COMPARISON_FUNC(compare);ds.StencilEnable=stencil;
    ds.FrontFace.StencilFunc=D3D11_COMPARISON_ALWAYS;ds.FrontFace.StencilPassOp=ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=D3D11_STENCIL_OP_KEEP;ds.BackFace=ds.FrontFace;
    ComPtr<ID3D11DepthStencilState> depthState;check(device->CreateDepthStencilState(&ds,&depthState),"source reached shadow depth");
    D3D11_RASTERIZER_DESC rs{};models.draws.at(0).raster->GetDesc(&rs);rs.ScissorEnable=TRUE;
    ComPtr<ID3D11RasterizerState> raster;check(device->CreateRasterizerState(&rs,&raster),"source caster group raster with atlas scissor");
    auto map=produceNativeShadowMap(device,context,source,modelSource,models,depthState.Get(),raster.Get());
    captureSourceShadowMap(device,context,map,compare,output);
    return map;
}
static NativeShadowMap drawSourceCasterPopulation(ID3D11Device* device,ID3D11DeviceContext* context,
    const fs::path& source,const fs::path& visibleModelSource,const NativeModelInputs& visibleModels,const fs::path& output) {
    std::ifstream reference(source/"model-source.txt");std::string relative;
    if(!reference)return drawSourceCasterMap(device,context,source,visibleModelSource,visibleModels,output);
    std::getline(reference,relative);auto casterSource=source/relative;
    auto casters=loadNativeModels(device,casterSource);
    return drawSourceCasterMap(device,context,source,casterSource,casters,output);
}
#ifdef SAN14_RENDER_SESSION
San14::Render::RenderSession San14::Render::createRenderSession(RenderHost& host) {
    const fs::path input=host.sourceDirectory,output=input.parent_path()/"client-runtime";
    fs::create_directories(output);
    const fs::path modelSource=input.parent_path()/"model-continuous-input";
    const fs::path shadowSource=input.parent_path()/"shadow-caster-input";
    constexpr bool earlyHdrOnly=false,oceanGbuffer=false,historyContinuation=false;
    constexpr bool sourceCameraContinuous=true,visibleView=false,hiddenView=false,sourceCameraPan=false,sourceStaticModels=true;
    constexpr double requestedFocusDeltaX=0,requestedCameraDistance=-1;
    constexpr int requestedWheelDelta=0,requestedCameraElapsedMs=-1;
    constexpr bool isolateShadowVisibility=false,primaryForwardPreparationOnly=false;
    const bool sourceArmyVisuals=(host.worldDetails&2)!=0;
    const bool sourceAccessoryVisuals=sourceArmyVisuals||(host.worldDetails&1)!=0;
    constexpr float actualArmyReturnedPhase=0,fixedRendererDelta=0;
    constexpr bool runLoop=true,snapshotAtEnd=false,postCaptureDiagnostics=false;
    constexpr UINT64 maximumRequests=0;
    constexpr bool oceanFftContinuation=true,fullMainContinuation=true,wantsContinuation=true;
    constexpr bool oceanEffects=true,oceanNormal=true,oceanLighting=true,oceanEnabled=true,sourceHdr=true;
    // The product has no live particle owner yet. The saved particle cohort
    // belongs to the offline renderer experiment, not campaign state.
    constexpr bool observedParticleExperiment=false;
    constexpr bool saveImage=false,fullMode=false;
#else
int main(int argc,char**argv) try {
    if(argc<3){std::cerr<<"terrain.exe input-directory output-directory [width height saveImage mode models-directory shadow-directory lighting-directory]\n";SAN14_RENDER_RETURN(2);}
    fs::path input=argv[1],output=argv[2];fs::create_directories(output);
    fs::path modelSource=argc>=8&&std::string(argv[7])!="-"?fs::path(argv[7]):input.parent_path()/"model-input";
    fs::path shadowSource=argc>=9&&std::string(argv[8])!="-"?fs::path(argv[8]):input.parent_path()/"shadow-input";
    bool earlyHdrOnly=argc>=7&&std::string(argv[6])=="early-hdr";
    bool oceanGbuffer=argc>=7&&std::string(argv[6])=="ocean-gbuffer";
    bool historyContinuation=argc>=7&&std::string(argv[6])=="ocean-history";
    const bool sourceCameraContinuous=argc>=7&&(std::string(argv[6])=="ocean-camera-view"||std::string(argv[6])=="ocean-camera-view-hidden"||std::string(argv[6])=="ocean-camera-run");
    const bool visibleView=argc>=7&&(std::string(argv[6])=="ocean-view"||std::string(argv[6])=="ocean-camera-view");
    const bool hiddenView=argc>=7&&(std::string(argv[6])=="ocean-view-hidden"||std::string(argv[6])=="ocean-camera-view-hidden");
    const bool sourceCameraPan=argc>=7&&std::string(argv[6])=="ocean-camera";
    const bool sourceStaticModels=sourceCameraPan||sourceCameraContinuous||(argc>=7&&std::string(argv[6])=="ocean-static");
    const double requestedFocusDeltaX=sourceCameraPan||sourceCameraContinuous?(argc>=15?std::stod(argv[14]):sourceCameraPan?1000.:0.):0.;
    const double requestedCameraDistance=sourceCameraPan||sourceCameraContinuous?(argc>=16?std::stod(argv[15]):-1.):-1.;
    const int requestedWheelDelta=sourceCameraPan||sourceCameraContinuous?(argc>=17?std::stoi(argv[16]):0):0;
    const int requestedCameraElapsedMs=sourceCameraPan||sourceCameraContinuous?(argc>=18?std::stoi(argv[17]):-1):-1;
    // Finite city diagnostic only: the held absent SDF owner leaves RS3.rgb
    // zero. Replace visibility alpha by1 to isolate shadowTerm's contribution.
    const bool isolateShadowVisibility=argc>=19&&std::string(argv[18])=="shadow-unoccluded";
    const bool primaryForwardPreparationOnly=argc>=19&&std::string(argv[18])=="forward-prepare-only";
    const bool sourceArmyVisuals=sourceStaticModels&&argc>=20&&std::string(argv[19])=="world-details";
    const bool sourceAccessoryVisuals=sourceArmyVisuals||(sourceStaticModels&&argc>=20&&std::string(argv[19])=="city-accessories");
    // A returned animation phase is an explicit finite experiment input. The
    // normal player wrapper/cadence is independent of the Main renderer clock.
    const float actualArmyReturnedPhase=argc>=21?std::stof(argv[20]):0;
    if(sourceStaticModels&&(argc<8||std::string(argv[7])=="-"))modelSource=input.parent_path()/"model-continuous-input";
    const bool runLoop=visibleView||hiddenView||sourceCameraContinuous||(argc>=7&&std::string(argv[6])=="ocean-run");
    if(runLoop&&requestedCameraElapsedMs>=0)throw std::runtime_error("Controlled camera milliseconds are a finite experiment input; continuous camera uses its independent native timer");
    const UINT64 maximumRequests=runLoop?(argc>=12?std::stoull(argv[11]):visibleView?0:2):0;
    const float fixedRendererDelta=runLoop&&argc>=13?std::stof(argv[12]):0;
    const bool snapshotAtEnd=runLoop&&argc>=14&&std::string(argv[13])=="snapshot";
    bool oceanFftContinuation=runLoop||(argc>=7&&std::string(argv[6])=="ocean-fft");
    const bool postCaptureDiagnostics=!runLoop&&(argc<11||std::string(argv[10])!="no-post-capture");
    bool fullMainContinuation=argc>=7&&(std::string(argv[6])=="ocean-continuous"||oceanFftContinuation||sourceStaticModels);
    bool wantsContinuation=historyContinuation||fullMainContinuation;
    bool oceanEffects=argc>=7&&(std::string(argv[6])=="ocean-effects"||wantsContinuation);
    const bool observedParticleExperiment=oceanEffects;
    bool oceanNormal=argc>=7&&(std::string(argv[6])=="ocean-normal"||oceanEffects);
    bool oceanLighting=argc>=7&&(std::string(argv[6])=="ocean-lighting"||oceanNormal);
    bool oceanEnabled=oceanGbuffer||oceanLighting;
    bool sourceHdr=earlyHdrOnly||oceanEnabled;
    const bool saveImage=argc>=6&&std::atoi(argv[5])!=0;
    const bool fullMode=argc>=7&&std::string(argv[6])=="full";
#endif
    UINT width,height,instanceCount,indexCount;std::array<DXGI_FORMAT,5> formats;DXGI_FORMAT depthFormat;
    UINT cull,front,depthCompare,colorMasks,stencilEnable,stencilRef;
    std::ifstream scene(input/"scene.txt");if(!scene)throw std::runtime_error("scene.txt absent");
    scene>>width>>height>>instanceCount>>indexCount;
    for(auto& format:formats){int n;scene>>n;format=DXGI_FORMAT(n);}int df;scene>>df;depthFormat=DXGI_FORMAT(df);
    scene>>cull>>front>>depthCompare>>colorMasks>>stencilEnable>>stencilRef;
    float depthClear=1;
    scene>>depthClear;
    std::ifstream sourceRaster(input/"terrain-raster.txt");
    if(sourceRaster)sourceRaster>>cull>>front>>depthCompare>>colorMasks>>stencilEnable>>stencilRef>>depthClear;
    if(sourceHdr) {
        std::ifstream reference(input.parent_path()/"stencil-visible-input"/"terrain-reference.txt");reference>>stencilRef;
        if(!reference)throw std::runtime_error("Actual source Terrain stencil absent");stencilEnable=1;
    }
    bool groupedTerrain=fs::exists(input/"batches.txt");
    if(groupedTerrain){std::ifstream geometry(input/"geometry.txt");geometry>>width>>height>>instanceCount>>indexCount;}
    // Source DXGI resource, SRV and RTV/DSV formats are distinct. In particular,
    // typeless allocation must preserve the original sRGB/linear consumer views.
    std::array<DXGI_FORMAT,6> resourceFormats,shaderFormats;
    std::array<std::string,6> targetNames;std::array<UINT,6> surfaces{};
    for(int i=0;i<5;++i)resourceFormats[i]=shaderFormats[i]=formats[i];
    resourceFormats[5]=shaderFormats[5]=depthFormat;
    std::ifstream targetRows(input/"targets.txt");
    if(targetRows){UINT r,s,t;for(int i=0;i<6;++i){targetRows>>targetNames[i]>>surfaces[i]>>r>>s>>t;resourceFormats[i]=DXGI_FORMAT(r);shaderFormats[i]=DXGI_FORMAT(s);if(i<5)formats[i]=DXGI_FORMAT(t);else depthFormat=DXGI_FORMAT(t);}}
#ifdef SAN14_RENDER_SESSION
    width=host.width;height=host.height;
#else
    if(argc>=5&&std::atoi(argv[3])>0&&std::atoi(argv[4])>0){width=std::atoi(argv[3]);height=std::atoi(argv[4]);}
#endif
    auto start=std::chrono::steady_clock::now();
    ComPtr<ID3D11Device> device;ComPtr<ID3D11DeviceContext> context;D3D_FEATURE_LEVEL level;
#ifdef SAN14_RENDER_SESSION
    device=host.device;context=host.context;level=device->GetFeatureLevel();
#else
    check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&device,&level,&context),"D3D11 device");
#endif
    for(DXGI_FORMAT f:{DXGI_FORMAT_BC5_TYPELESS,DXGI_FORMAT_BC5_UNORM}) {
        UINT support=0;check(device->CheckFormatSupport(f,&support),"Ocean normal volume format support");
        std::cout<<"Ocean volume format="<<f<<" Texture3D="<<bool(support&D3D11_FORMAT_SUPPORT_TEXTURE3D)<<" support="<<support<<'\n';
    }
#ifndef SAN14_RENDER_SESSION
    if(argc>=7&&std::string(argv[6])=="ocean-textures") {
        auto textures=loadOceanTextures(device.Get(),input.parent_path()/"ocean-texture-input");
        for(const auto* t:{&textures.normal3d,&textures.normalNoise2d,&textures.wvBubble,&textures.wvHeightMask}) {
            D3D11_SHADER_RESOURCE_VIEW_DESC d{};t->view->GetDesc(&d);
            std::cout<<"Ocean original texture extent="<<t->width<<'x'<<t->height<<'x'<<t->depth
                <<" layers="<<t->layers<<" mips="<<t->mips<<" resource/SRV="<<t->resourceFormat<<'/'<<d.Format
                <<" dimension="<<d.ViewDimension<<'\n';
        }
        std::cout<<"Ocean original four texture uploads elapsedMs="<<std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-start).count()<<'\n';
        SAN14_RENDER_RETURN(0);
    }
    if(argc>=7&&std::string(argv[6])=="shadow"){
        auto models=loadNativeModels(device.Get(),modelSource);
        auto map=drawSourceCasterPopulation(device.Get(),context.Get(),shadowSource,modelSource,models,output);
        std::cout<<"shadow experiment elapsedMs="<<std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-start).count()<<'\n';SAN14_RENDER_RETURN(0);
    }
    if(argc>=7&&std::string(argv[6])=="ggx"){
        generateGgx(device.Get(),context.Get(),input.parent_path()/"lighting-input",output);SAN14_RENDER_RETURN(0);
    }
    if(argc>=7&&std::string(argv[6])=="ambient"){
        produceAmbientCube(device.Get(),context.Get(),input.parent_path()/"ambient-input",output);SAN14_RENDER_RETURN(0);
    }
    if(argc>=7&&std::string(argv[6])=="effect-providers") {
        for(DXGI_FORMAT format:{DXGI_FORMAT_R32G32B32A32_FLOAT,DXGI_FORMAT_B8G8R8A8_UNORM,DXGI_FORMAT_R8G8B8A8_UNORM,DXGI_FORMAT_R32G32_FLOAT}) {
            UINT support;check(device->CheckFormatSupport(format,&support),"Effect source input format support");
            std::cout<<"source Effect IA format="<<format<<" vertexBuffer="<<bool(support&D3D11_FORMAT_SUPPORT_IA_VERTEX_BUFFER)<<'\n';
        }
        auto providers=loadNativeEffectProviders(device.Get(),input.parent_path()/"effect-native-provider-input");
        for(const auto& entry:providers.textures) {
            D3D11_TEXTURE2D_DESC td{};entry.second.texture->GetDesc(&td);
            D3D11_SHADER_RESOURCE_VIEW_DESC sd{};entry.second.view->GetDesc(&sd);
            std::cout<<"source Effect texture "<<entry.first<<" extent="<<td.Width<<'x'<<td.Height
                <<" mips="<<td.MipLevels<<" resource/SRV="<<td.Format<<'/'<<sd.Format<<'\n';
        }
        SAN14_RENDER_RETURN(0);
    }
    if(argc>=7&&std::string(argv[6])=="effect-inputs") {
        auto effects=loadNativeEffectInputs(device.Get(),input.parent_path()/"effect-consumer-input"/"source-main");
        std::cout<<"source Effect pipelines/CB/layout/VB/IB/provider creation complete; no draw\n";
        SAN14_RENDER_RETURN(0);
    }
    bool shadowLighting=argc>=7&&(std::string(argv[6])=="full-shadow"||sourceHdr);
    bool disableAmbient=argc>=7&&std::string(argv[6])=="lighting-no-ambient";
    bool floatLighting=argc>=7&&(std::string(argv[6])=="lighting-float"||disableAmbient);
    bool wantsLighting=argc>=7&&(std::string(argv[6])=="full"||shadowLighting||floatLighting);
#else
    constexpr bool shadowLighting=true,disableAmbient=false,floatLighting=false,wantsLighting=true;
#endif
    fs::path lightingSource;
    ComPtr<ID3D11Texture2D> ggx;ComPtr<ID3D11ShaderResourceView> ambient;
    ComPtr<ID3D11Texture2D> litColor,finalColor;
    NativeMainCloud mainCloud;
    std::optional<NativeCurrentCloudWeather> currentCloudWeather;
    NativeAmbient environment;NativeAmbientCloudContinuous environmentCloud;
    std::optional<NativeCurrentAmbientFrame> currentAmbient;
    NativeFrameClock commonClock;NativeFrameClockPublication commonPublication;
    UINT commonBaseSettingsFlags=0;
    if(runLoop||sourceStaticModels)commonClock=prepareNativeFrameClock(input.parent_path()/"frame-clock-input");
    if(runLoop){
        commonBaseSettingsFlags=commonClock.settingsFlags10;
#ifdef SAN14_RENDER_SESSION
        commonClock.settingsFlags10=(host.input.flags&2)?commonBaseSettingsFlags|commonClock.pauseMask:commonBaseSettingsFlags;
        commonPublication=advanceNativeFrameClock(commonClock,host.input.deltaSeconds);
#else
        commonPublication=advanceNativeFrameClock(commonClock,fixedRendererDelta!=0?fixedRendererDelta:.016f);
#endif
    }
    const auto normalNextSource=input.parent_path()/"normal-post-next-input";
    const auto normalInitialSource=normalNextSource/"prior-cpu";
    const float initialCommonClock=runLoop?commonPublication.common254:oceanLighting?sourceCommonClock(normalInitialSource):0;
    const float nextCommonClock=wantsContinuation&&!runLoop?sourceCommonClock(normalNextSource):0;
    UINT sourceHostFlags3E4=0,contextFlags3F4=0,actualViewRainFlags19B4=0;
    uint64_t actualHostQ7F8=0;
    if(sourceHdr) {
        std::ifstream entry(input.parent_path()/"early-hdr-input"/"main-entry.txt");
        entry>>sourceHostFlags3E4>>actualHostQ7F8>>actualViewRainFlags19B4;
        if(!entry)throw std::runtime_error("Original Main entry fields absent");
    }
    NativeEffectInputs effects;NativeFogAcceleration fogAcceleration;NativeSpecialEffects specialEffects;
    NativeMapExposure mapExposure;
    NormalPostSurface retainedNormalExposure;
    UINT64 mapExposurePublicationDraws=0,mapExposureHalfDraws=0;
    std::vector<ComPtr<ID3D11Texture2D>> mapExposureBeforeMainReceipts,mapExposureAfterApplyReceipts;
    source_territory_gpu::Owner territory;
    source_territory_gpu::Targets territoryTargets;
    source_territory::Controls territoryControls;
    source_zone::Profile territoryZoneProfile;
    source_zone::Owner territoryZoneOwner;
    source_zone::Current territoryZoneCurrent;
#ifdef SAN14_RENDER_SESSION
    std::optional<San14::Render::TerritoryOwnershipPalette> ownershipPalette;
    std::uint64_t ownershipAppliedRevision=0;
    auto publishCurrentWorldOwnership=[&]() {
        if(!ownershipPalette)return;
        if(San14::Render::publishCurrentTerritoryOwnership(host.territoryOwnership,*ownershipPalette,
            territory.profile,territoryZoneProfile,territoryZoneOwner,territoryZoneCurrent,ownershipAppliedRevision)) {
            // Only ownership changed. Other six source channels and their
            // existing buffer bytes stay owned by their ordinary producers.
            std::copy_n(territoryZoneOwner.zones.data(),source_zone::cells,territory.profile.zones.data());
            D3D11_BOX layer0{0,0,0,source_zone::cells,1,1};
            context->UpdateSubresource(territory.zones.Get(),0,&layer0,territoryZoneOwner.zones.data(),0,0);
#ifdef SAN14_TERRITORY_GPU_PROOF
            D3D11_BUFFER_DESC receiptDescription{};territory.zones->GetDesc(&receiptDescription);
            receiptDescription.Usage=D3D11_USAGE_STAGING;receiptDescription.BindFlags=0;
            receiptDescription.CPUAccessFlags=D3D11_CPU_ACCESS_READ;receiptDescription.MiscFlags=0;
            ComPtr<ID3D11Buffer> receiptBuffer;
            check(device->CreateBuffer(&receiptDescription,nullptr,&receiptBuffer),"finite territory GPU receipt");
            context->CopyResource(receiptBuffer.Get(),territory.zones.Get());
            D3D11_MAPPED_SUBRESOURCE receipt{};
            check(context->Map(receiptBuffer.Get(),0,D3D11_MAP_READ,0,&receipt),"finite territory GPU map");
            const bool exact=std::memcmp(receipt.pData,territoryZoneOwner.zones.data(),territoryZoneOwner.zones.size())==0;
            context->Unmap(receiptBuffer.Get(),0);
            if(!exact)throw std::runtime_error("Actual territory GPU bytes differ from current seven-layer owner");
            std::cout<<"TERRITORY_GPU_EXACT revision="<<ownershipAppliedRevision<<" bytes="<<territoryZoneOwner.zones.size()<<'\n';
#endif
        }
    };
#endif
    bool territoryDisplayEnabled=true;
    int territoryDisplayStyle=0;
    unsigned territoryZoneInitialUpdates=0;
    float heldMainB1C=0;
    UINT64 territoryDraws=0,mapExposureDraws=0;
    // Finite experiment receipts only; the continuous Main does no readback.
    std::array<ComPtr<ID3D11Texture2D>,4> territoryDepthReceipts;
    std::array<ComPtr<ID3D11Texture2D>,4> territoryWaterColorReceipts;
    unsigned territoryReceiptSide=0;
#ifdef SAN14_WATER_STAGE_PROOF
    // Finite diagnostic build only: retain the actual stage pixels without
    // changing any Main input, pass or production request path.
    struct WaterProofSurface {ComPtr<ID3D11Texture2D> texture;DXGI_FORMAT format;UINT slice;};
    std::map<std::string,WaterProofSurface> waterProofSurfaces;
    auto captureWaterProof=[&](const std::string& stage,ID3D11Texture2D* original,DXGI_FORMAT format,UINT slice=0){
        const std::string name=std::string(territoryReceiptSide?"current-":"cold-")+stage;
        auto& retained=waterProofSurfaces[name];retained.format=format;retained.slice=slice;
        D3D11_TEXTURE2D_DESC description{};original->GetDesc(&description);
        description.BindFlags=0;description.MiscFlags=0;
        if(!retained.texture)check(device->CreateTexture2D(&description,nullptr,&retained.texture),"finite water stage snapshot");
        context->CopyResource(retained.texture.Get(),original);
    };
#endif
    source_aux_camera::FogAccelerationConstants fogCamera;
    NativeEffectCameraContinuous effectCamera;source_effect_camera::Frame effectCameraFrame;
    std::optional<source_field_effect::Registry> fieldEffects;
    source_field_effect::Snapshot currentFieldEffects;
    NativeAtmosphereCamera atmosphereCamera;
    bool atmosphereCameraReady=false;
    NativeModelContinuous staticCatalog;NativeModelInputs allStaticModels;NativeModelContinuousPlan staticPlan;
    NativeCityPopulation cityModels;NativeCityPopulationPlan cityPlan;
    NativeCityPopulation areaModels;NativeCityAreaPrimary primaryModels;
    bool primaryPopulationChanged=false;
    NativeCityAccessoryGpu cityAccessories;
    NativeCityAccessoryGpu areaAccessories;
    source_default_player_phase::Profile defaultPlayerPhases;
    San14::Render::CityGateFlagImports cityFlagImports;
    UINT64 appliedCityGateRevision=0;
    UINT64 appliedAreaCapitalRevision=0;
    ground_details::GroundDetails groundDetails;
    std::vector<char> currentMain18C0;
    uint32_t groundSourceFlags14=0,groundFrustumType64=0,groundCulling450=0,groundScanning454=0;
    uint16_t groundAppBaseFlags=0;
    unsigned accessoryOrdinaryCount=0;
    NativeArmyPopulation armyModels;NativeArmyPlan armyPlan;NativeArmyQueues armyQueues;
    std::vector<float> armyReturnedPhases;
    std::optional<ArmyWorldTemplateLibrary> armyWorldTemplates;
    ArmyWorldPopulationState armyWorldState;
    NativeArmyAnimationGroups armyAnimationGroups;
    std::optional<ArmyWorldModelPool<NativeArmyPopulation>> armyModelPool;
    NativeSharedAccessories sharedCityAccessories,sharedAreaAccessories;
    uint64_t armyCandidateRevision{},armyBindingRevision{},armyRenderRevision{};
    std::map<int32_t,bool> armyPriorVisible;
    std::map<int32_t,std::pair<uint32_t,uint32_t>> armyUnitSelections;
    std::map<int32_t,ArmyUnitHeight> armyUnitHeights;
    std::vector<bool> armySuccessfulSamples;
    std::optional<NativeArmyGraphicsActions> armyGraphicsActions;
    std::vector<const NativeAccessoryPoseSource*> armyActivePoses;
    std::vector<bool> armyMainMembership;
    NativeJointPopulationShadow jointShadow;
    std::optional<NativeCityGateShadowSource> currentCityGateShadow;
#ifdef SAN14_RENDER_SESSION
    const auto currentPrimaryInput=input.parent_path()/"base-visual-world-input"/"primary-templates";
    const auto jointShadowInput=input.parent_path()/"city-gate-shadow-input";
#else
    const auto currentPrimaryInput=input.parent_path()/"city-model-population-input";
    const auto jointShadowInput=input.parent_path()/"joint-population-shadow-input";
#endif
    NativeCityModelQueues ordinaryModelQueues;
    NativeModelContinuousPlan submittedStaticPlan;
    std::vector<unsigned> ordinaryPopulationCounts;
    source_camera::Profile staticCameraProfile;source_camera::Frame staticCamera;
    source_camera_motion::Profile cameraMotionProfile;source_camera_motion::Runtime cameraMotion;
    auto& staticCameraRuntime=cameraMotion.camera;
    source_camera::GroundHeightProfile cameraGround;
    source_camera_motion::TerrainQualifier cameraTerrainQualifier;
    NativeCameraClock cameraClock;
    std::optional<int32_t> controlledCameraClock;
    source_camera::RenderModePrefixProfile mainDirectProfile;source_camera::RenderModePrefixState mainDirectState;
    source_far_main::Profile farMainProfile;source_far_main::Owner farMainOwner;
    source_far_main::Current currentDisplay;
    San14::Render::FieldDisplay fieldDisplay;
    source_camera::Prefix mainDirectPrefix{};
    NativeDeferredCameraProfile deferredCameraProfile;NativeDeferredCameraFields deferredCameraFields;
    source_weather_fog::Profile weatherCameraProfile;source_weather_fog::Frame weatherCameraFrame;
    source_camera::V3 globalAtmosphereRgb{};
    NativeWeatherFogConsumers weatherConsumers;
    UINT64 completedMainRequests=1,acceptedCameraRequests=0,acceptedWheelRequests=0,cameraTickCount=0;
    std::array<source_shadow_camera::Profile,2> shadowCameraProfiles;
    std::array<source_shadow_gpu::Profile,2> shadowGpuParameters;
    std::array<NativeShadowRasterSource,2> shadowRasterParameters;
    const source_shadow_camera::Profile* shadowCameraProfile=&shadowCameraProfiles[0];
    int currentRenderMode=0;
    unsigned currentShadowFamily=0;
    const source_current_season::Frame* currentSeason=nullptr;
#ifdef SAN14_RENDER_SESSION
    currentSeason=host.currentSeason?&*host.currentSeason:nullptr;
#endif
    source_current_season_shadow::Parameters currentShadowParameters;
    source_current_season_atmosphere::CloudLightInput currentCloudLights{};
    source_shadow_camera::Frame shadowCameraFrame;
    source_shadow_camera::Owner shadowHistory;
    source_view::Profile viewProfile;source_view::Frame currentView;NativeModelStencilContinuous modelStencil;
    ComPtr<ID3D11ShaderResourceView> directionalDiffuse;
    if(sourceStaticModels) {
        staticCameraProfile=source_camera::loadProfile(input.parent_path()/"camera-continuous-input"/"profile.bin");
        mainDirectProfile=source_camera::loadRenderModePrefixProfile(input.parent_path()/"camera-continuous-input"/"render-mode-prefix-profile.bin");
        mainDirectState=source_camera::createRenderModePrefixState(mainDirectProfile);
        farMainProfile=source_far_main::loadProfile(input.parent_path()/"far-main-source-input"/"profile.bin");
        farMainOwner=source_far_main::createOwner(farMainProfile);
        cameraMotionProfile=source_camera_motion::loadProfile(input.parent_path()/"camera-motion-input"/"motion-profile.bin");
        cameraMotion=source_camera_motion::createRuntime(staticCameraProfile);
        cameraGround=source_camera::loadGroundHeightProfile(input.parent_path()/"camera-continuous-input"/"ground-height-current");
        cameraTerrainQualifier=[&](int band,const source_camera::State& candidate,double minimum,double maximum,double requested,double& pitch){
            auto result=source_camera::qualifyTerrainPitch(cameraGround,{band,candidate,minimum,maximum,requested});pitch=result.pitch;return result.accepted;
        };
        cameraClock=prepareNativeCameraClock();
        // Initialize only this renderer's consumed fields from project rules
        // and the current resource declarations; season publishes live fields.
        currentMain18C0=own_main_init::decode(load(input.parent_path()/"main-camera-initial-input"/"profile.bin"));
        heldMainB1C=source_camera::get<float>(reinterpret_cast<const uint8_t*>(currentMain18C0.data())+0xb1c);
        territoryControls.providerFlags=1; // Actual original node60 graph flag.
        territoryZoneProfile=source_zone::load(input.parent_path()/"territory-zone-source-input");
        // The source map reaches a settled display via its first-dirty updater.
        // Prepare the held World once; accepted camera requests reuse this owner.
        territoryZoneInitialUpdates=source_zone::updateUntilSettled(territoryZoneProfile,territoryZoneOwner,territoryZoneCurrent);
        staticCatalog=prepareNativeModelContinuous(modelSource);
        cityModels=prepareNativeCityPopulation(device.Get(),currentPrimaryInput);
#ifdef SAN14_RENDER_SESSION
        if(!host.areaCapitalWorld)throw std::runtime_error("Native Main requires its actual Area Capital publication");
        const auto& initialAreas=*host.areaCapitalWorld;
        areaModels=prepareNativeAreaPrimaryPopulation(device.Get(),cityModels,
            prepareNativeAreaPrimarySource(cityModels.source,
                readNativeBaseVisualPrimary(initialAreas.models.primary.data(),initialAreas.models.primary.size()),
                initialAreas.identities,cityModels.source.caller));
        publishNativeCityAreaPrimary(primaryModels,cityModels,areaModels);
#else
        primaryModels.source=cityModels.source;primaryModels.currentModels=cityModels.currentModels;
#endif
        const auto groundInput=input.parent_path()/"ground-detail-input";
        std::ifstream groundReference(groundInput/"source-directory.txt");std::string groundRelative;
        if(!std::getline(groundReference,groundRelative)||groundRelative.empty())throw std::runtime_error("Ground detail source directory import absent");
        groundDetails.loadOnce(device.Get(),groundInput/fs::path(groundRelative),
            currentSeason?input.parent_path()/"ground-detail-season-source-input":fs::path{});
        std::ifstream groundSettings(groundInput/"common-source.txt");
        groundSettings>>groundSourceFlags14>>groundFrustumType64>>groundCulling450>>groundScanning454>>std::hex>>groundAppBaseFlags;
        if(!groundSettings)throw std::runtime_error("Ground detail saved Common/App import absent");
#ifndef SAN14_RENDER_SESSION
        jointShadow.catalog=prepareNativeModelContinuous(jointShadowInput/"catalog");
#endif
        deferredCameraProfile=prepareNativeDeferredCameraProfile(input.parent_path()/"deferred-camera-continuous-input");
        weatherCameraProfile=source_weather_fog::readProfile(input.parent_path()/"weather-fog-camera-input"/"initial"/"weather");
        weatherConsumers=prepareNativeWeatherFogConsumers(input.parent_path()/"weather-fog-consumer-input");
        if(sourceAccessoryVisuals||sourceArmyVisuals)
            defaultPlayerPhases=source_default_player_phase::readProfile(
                input.parent_path()/"default-player-phase-input"/"default-player-loop-source.bin");
        if(sourceAccessoryVisuals) {
            const auto accessorySource=input.parent_path()/"city-accessory-next-input";
            cityAccessories=prepareNativeCityAccessoryGpu(device.Get(),context.Get(),accessorySource,
#ifdef SAN14_RENDER_SESSION
                false
#else
                true
#endif
                );
            if(cityAccessories.poseSource.duration8!=defaultPlayerPhases.categories[2].duration8)
                throw std::runtime_error("Accessory source clip and default player duration differ");
#ifdef SAN14_RENDER_SESSION
            cityFlagImports=San14::Render::loadCityGateFlagImports(accessorySource/"material-source",cityAccessories);
            areaAccessories=prepareNativeAreaCapitalAccessories(cityAccessories,
                readNativeAreaCapitalAccessories(host.areaCapitalWorld->models.accessories));
#endif
            auto camera=prepareNativeDeferredCameraProfile(accessorySource/"hdr/initial/camera-source");
            deferredCameraProfile.constants.merge(camera.constants);
            auto weather=prepareNativeWeatherFogConsumers(accessorySource/"hdr/initial/weather-source");
            weatherConsumers.insert(weatherConsumers.end(),weather.begin(),weather.end());
        }
        if(sourceArmyVisuals
#ifdef SAN14_RENDER_SESSION
            ||sourceAccessoryVisuals
#endif
            ) {
            const auto armyDirectory=input.parent_path()/"army-visual-input";
#ifdef SAN14_RENDER_SESSION
            armyModels=prepareNativeArmyPopulation(device.Get(),context.Get(),armyDirectory,false);
            const auto materials=loadArmyWorldMaterialImport(armyDirectory,armyModels.source.templateCount);
            armyWorldTemplates.emplace(armyModels.source,materials.materialTemplates,
                captureCompletedArmyWorldMaterials(armyModels,materials));
            prepareArmyWorldMaterialBank(device.Get(),context.Get(),armyDirectory,armyModels,*armyWorldTemplates,materials);
            armyWorldTemplates->constructors=readArmyWorldConstructors(input.parent_path()/"army-graphics-action-input"/"constructor-worlds.bin");
            auto modelCapacities=readArmyWorldPoolCapacities(input.parent_path()/"army-graphics-action-input"/"pool-banks.bin");
            armyGraphicsActions=loadNativeArmyGraphicsActions(input.parent_path()/"army-graphics-action-input",armyModels.source);
            for(unsigned category=4;category<=9;++category){
                const auto directory=category<7?"army-mode-category"+std::to_string(category):
                    "army-naval-category-"+std::to_string(category);
                auto resource=importArmyWorldModeResources(device.Get(),input.parent_path()/directory,
                    armyModels.source,armyWorldTemplates->tableOwners);
                appendArmyWorldModeResources(std::move(resource),armyModels,*armyWorldTemplates,*armyGraphicsActions,modelCapacities);
            }
            armyModelPool.emplace(modelCapacities,
                armyWorldTemplates->constructors,armyModels.source.poses,armyModels.source.defaultActions);
#ifdef SAN14_SHARED_POOL_PROOF
            std::cout<<"SharedPool capacity="<<modelCapacities[2]<<" banks=1\n";
#endif
            if(sourceAccessoryVisuals){
                cityAccessories.templates.blend=armyModels.templates.blend;
                for(const auto& primitive:armyWorldTemplates->primitives[2])
                    cityAccessories.templates.draws.push_back(armyModels.templates.draws.at(primitive.sourceTemplate));
                areaAccessories.templates=cityAccessories.templates;
                sharedCityAccessories=prepareNativeSharedAccessories(cityAccessories.population,false);
                sharedAreaAccessories=prepareNativeSharedAccessories(areaAccessories.population,true);
            }
#else
            armyModels=prepareNativeArmyPopulation(device.Get(),context.Get(),armyDirectory);
#endif
            armyReturnedPhases.assign(armyModels.source.actors.size(),actualArmyReturnedPhase);
        }
        const auto globalRgb=load(input.parent_path()/"weather-fog-camera-input"/"initial"/"weather"/"effective-light-rgb.bin");
        if(globalRgb.size()!=sizeof(globalAtmosphereRgb))throw std::runtime_error("Original global80 RGB extent differs");
        std::memcpy(&globalAtmosphereRgb,globalRgb.data(),globalRgb.size());
        const auto& jointInput=jointShadowInput;
        shadowCameraProfiles[0]=source_shadow_camera::loadProfile(jointInput/"initial"/"cpu-profile.bin");
        shadowCameraProfiles[1]=source_shadow_camera::loadProfile(jointInput/"mode1"/"cpu-profile.bin");
        shadowGpuParameters[0]=source_shadow_gpu::loadProfile(jointInput/"initial"/"gpu-profile.bin");
        shadowGpuParameters[1]=source_shadow_gpu::loadProfile(jointInput/"mode1"/"gpu-profile.bin");
#ifdef SAN14_RENDER_SESSION
        currentCityGateShadow=loadNativeCityGateShadowSource(jointInput,staticCatalog,cityModels.source);
        publishNativeCityAreaJointShadow(jointShadow,*currentCityGateShadow,staticCatalog,primaryModels,
            shadowCameraProfiles,shadowGpuParameters);
#endif
        fieldDisplay.bind(farMainProfile,staticCatalog,shadowCameraProfiles);
        const auto shadowRasterInput=input.parent_path()/"shadow-raster-source-input";
        shadowRasterParameters[0]=loadNativeShadowRasterSource(shadowRasterInput/"initial");
        shadowRasterParameters[1]=loadNativeShadowRasterSource(shadowRasterInput/"mode1");
        viewProfile=source_view::loadProfile(input.parent_path()/"view-continuous-input"/"profile.bin");
        // Normal ready STA, Camera kind0: D57E50/EBD510 select K1890's
        // original white Static. FE8 belongs to the older empty-registry lab.
        auto diffuse=loadTexture(device.Get(),input.parent_path()/"normal-pattern-receiver-input"/"white-80F9.tex");
        D3D11_SHADER_RESOURCE_VIEW_DESC diffuseView{};
        diffuseView.Format=DXGI_FORMAT_B8G8R8A8_UNORM;
        diffuseView.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;
        diffuseView.Texture2D.MipLevels=1;
        check(device->CreateShaderResourceView(diffuse.Get(),&diffuseView,&directionalDiffuse),"Normal source K1890 white80F9 owner");
    }
    auto requestSourceCamera=[&](double deltaX,double deltaZ,bool reset,double distance=-1.) {
        const auto& pose=reset?staticCameraProfile.initial:staticCameraRuntime.pose;
        source_camera_motion::Request request{0,{static_cast<float>(pose.x+deltaX),static_cast<float>(pose.y),static_cast<float>(pose.z+deltaZ),static_cast<float>(pose.w)},distance>=0?distance:pose.distance,pose.yaw,pose.pitch,0};
        const auto target=source_camera::classify(staticCameraProfile,request.distance);
        if(target.band<2||source_camera::classifyRenderMode(target)>1)return;
        if(source_camera_motion::applyImmediate(staticCameraProfile,cameraMotion,request,cameraTerrainQualifier))++acceptedCameraRequests;
    };
    auto requestSourceWheel=[&](int delta,int canonicalX,int canonicalY) {
        if(cameraMotion.requestMode!=0)return;
        const auto inputFrame=source_camera_motion::prepareCurrent(staticCameraProfile,cameraMotion);
        auto request=source_camera_motion::buildWheel(staticCameraProfile,cameraMotionProfile,cameraMotion,delta,
            source_camera_motion::cursorRay(inputFrame.prefix,canonicalX,canonicalY),source_camera_motion::eyeForward(inputFrame),cameraTerrainQualifier);
        if(!request)return;
        const auto target=source_camera::classify(staticCameraProfile,request->distance);
        // These CPU requests are original. This renderer's current Scene/program
        // publication admits only the already joined band2+ / mode0,1 range.
        if(target.band<2||source_camera::classifyRenderMode(target)>1)return;
        const int32_t sample=sampleNativeCameraClock(cameraClock);
        if(source_camera_motion::queueTimed(cameraMotion,*request,sample)){
            ++acceptedCameraRequests;++acceptedWheelRequests;
            if(requestedCameraElapsedMs>=0)controlledCameraClock=source_camera_motion::signedWord(uint32_t(sample)+uint32_t(requestedCameraElapsedMs));
        }
    };
    auto syncArmyModels=[&]() {
#ifdef SAN14_RENDER_SESSION
        if(!sourceArmyVisuals)return;
        auto& pool=*armyModelPool;const auto& candidates=*host.armyWorld;
        pool.releaseAbsent(candidates.unitCenters);
        const bool bindingsChanged=armyBindingRevision!=pool.bindingRevision;
        if(armyCandidateRevision==candidates.revision&&!bindingsChanged&&
            armyWorldState.appliedMaterialRevision==armyWorldTemplates->materialRevision)return;
        ArmyWorldPublication active=candidates;active.revision=++armyRenderRevision;active.actors.clear();
        for(const auto& candidate:candidates.actors){
            auto key=candidate.key;
            if(key.descriptor==0x7A0||key.descriptor==0x818){auto member=pool.member(GraphicsModelFamily::Army,key.unit,key.descriptor,key.instance);
                if(!member)continue;key=*member;}
            auto* slot=pool.find(key);if(!slot)continue;
            auto current=candidate;current.key=key;current.action=armyWorldTemplates->defaultActionOrdinals[key.category];
            current.world=slot->presented.world;current.bounds=slot->presented.bounds;
            active.actors.push_back(current);
        }
        // A logical key can bind another physical slot after leaving view.
        // Draw objects belong to that slot, rather than the previous key.
        if(bindingsChanged)armyWorldState.primitiveIndices.clear();
        publishArmyWorldPopulation(armyModels,armyWorldState,*armyWorldTemplates,active,
            [&](const ArmyWorldActorKey& key,unsigned primitive,unsigned index,const std::string& identity){
                auto& model=resolveNativePhysicalModel(device.Get(),*pool.find(key),primitive,armyModels.templates.draws.at(index),
                    armyModels.source.vertices.at(index));
                // Material changes preserve the physical draw and its private CBs.
                const auto& material=armyModels.templates.draws.at(index);
                model.textures=material.textures;model.samplers=material.samplers;
#ifdef SAN14_ARMY_MATERIAL_PROOF
                if(key.category==2||key.category==3){
                    const auto actor=std::find_if(candidates.actors.begin(),candidates.actors.end(),
                        [&](const auto& candidate){return candidate.key==key;});
                    std::cout<<"ArmyMaterial unit="<<key.unit<<" category="<<key.category<<" ordinal="<<key.instance
                        <<" draw="<<primitive<<" force="<<actor->material.force<<" color="<<actor->material.color49
                        <<" glyph="<<actor->material.glyph<<" template="<<index
                        <<" srv="<<reinterpret_cast<uintptr_t>(model.textures[0].Get())
                        <<" cb="<<reinterpret_cast<uintptr_t>(model.vsConstants.Get())<<':'
                        <<reinterpret_cast<uintptr_t>(model.psConstants.Get())<<'\n';
                }
#endif
                auto draw=model;draw.identity=identity;return draw;
            });
        armyReturnedPhases.resize(armyModels.poses.size());armySuccessfulSamples.assign(armyModels.poses.size(),false);
        armyActivePoses.resize(armyModels.poses.size());armyMainMembership.assign(armyModels.poses.size(),false);
        for(const auto& [key,index]:armyWorldState.actorIndices){
            auto& slot=*pool.find(key);auto& actor=armyModels.source.actors[index];
            actor.world=slot.presented.world;actor.obb=slot.presented.bounds;
            armyModels.poses[index]=slot.pose;armyModels.poseCaches[index]=slot.cache;
            const auto& clock=slot.clock;int raw=clock.poseRawAction;
            const auto& action=armyGraphicsActions->rows.at({unsigned(key.category),unsigned(raw)});
            armyActivePoses[index]=action.pose?&*action.pose:&armyModels.source.poses[key.category];
            armyReturnedPhases[index]=clock.returnedPhase;armySuccessfulSamples[index]=clock.hasSuccessfulSample;
            // Ordinary TGK21 v28 publishes750/778/7C8. Banner7F0 requires
            // the independent retiring branch, which is not this population.
            armyMainMembership[index]=key.category!=2;
        }
        armyCandidateRevision=candidates.revision;armyBindingRevision=pool.bindingRevision;
        std::cout<<"source ArmyWorld revision="<<armyWorldState.appliedRevision
            <<" bound="<<pool.boundCount(GraphicsModelFamily::Army)<<" materialUnavailable="<<armyWorldState.materialUnavailable.size();
        for(const auto& candidate:candidates.actors)if(candidate.key.category<2)if(auto* slot=pool.find(candidate.key)){
            std::cout<<" firstBodySlot="<<slot->index<<" firstBodyHeldPhase="<<slot->clock.returnedPhase
                <<" firstBodyHeldAction="<<slot->clock.selectedRawAction24
                <<" firstBodyRoot="<<slot->presented.world.translation.x<<','<<slot->presented.world.translation.y<<','<<slot->presented.world.translation.z;
            break;
        }
        std::cout<<'\n';
#endif
    };
    auto publishCurrentModels=[&]() {
#ifdef SAN14_RENDER_SESSION
        if(sourceArmyVisuals)armyModelPool->snapshot(armyModels,armyWorldState);
        if(sourceArmyVisuals){
            observeArmyUnitHeightRoster(armyUnitHeights,host.armyWorld->unitCenters);
            for(const auto& [id,current]:host.armyWorld->unitModelModes){
                const auto selection=std::pair{current,host.armyWorld->unitNavalFormations.at(id)};
                auto previous=armyUnitSelections.try_emplace(id,std::pair{0u,0u}).first;
                if(previous->second==selection)continue;
                // CE1C8D30 clears the old descriptor chains immediately,
                // before visibility/admission or the next model acquisition.
                if(current==1){
                    for(int descriptor:{0x750,0x778,0x7C8,0x840})
                        armyModelPool->releaseDescriptor(GraphicsModelFamily::Army,id,descriptor);
                    if(armyWorldNavalCategory(selection.second)<0)
                        armyModelPool->releaseDescriptor(GraphicsModelFamily::Army,id,0x818);
                    armyModelPool->releaseDescriptor(GraphicsModelFamily::Army,id,0x7A0);
                }else if(current==2)for(int descriptor:{0x750,0x778,0x7C8,0x818,0x840})
                    armyModelPool->releaseDescriptor(GraphicsModelFamily::Army,id,descriptor);
                else for(int descriptor:{0x818,0x7A0,0x840})
                    armyModelPool->releaseDescriptor(GraphicsModelFamily::Army,id,descriptor);
                previous->second=selection;armyUnitHeights[id].dirty30=true;
            }
            for(auto it=armyUnitSelections.begin();it!=armyUnitSelections.end();)
                if(!host.armyWorld->unitCenters.contains(it->first))it=armyUnitSelections.erase(it);else ++it;
        }
        primaryPopulationChanged=false;
        if(!host.cityGateWorld)throw std::runtime_error("Native Main requires its actual City/Gate World publication");
        const auto& bases=*host.cityGateWorld;
        if(appliedCityGateRevision!=bases.revision) {
            if(sourceAccessoryVisuals)San14::Render::requireCurrentCityGateFlagImports(bases,cityFlagImports);
            auto primaries=readNativeBaseVisualPrimary(bases.primary.data(),bases.primary.size());
            updateNativeCityPopulation(cityModels,primaries);
            if(sourceAccessoryVisuals){
                auto current=readNativeBaseVisualAccessories(bases.accessories.data(),bases.accessories.size());
                observeNativeSharedAccessorySource(sharedCityAccessories,cityAccessories.population,current);
                publishNativeBaseVisualAccessories(cityAccessories,std::move(current));
            }
            primaryPopulationChanged=true;
            appliedCityGateRevision=bases.revision;
            std::cout<<"source CityGateWorld revision="<<appliedCityGateRevision
                <<" owners="<<cityModels.source.worlds.size()<<" primitives="<<cityModels.source.catalog.primitives.size()<<'\n';
        }
        const auto& capitals=*host.areaCapitalWorld;
        if(appliedAreaCapitalRevision!=capitals.models.revision) {
            if(capitals.identities!=areaModels.source.identities)throw std::runtime_error("Current Area roster needs an actual rebind/import");
            if(sourceAccessoryVisuals)San14::Render::requireCurrentCityGateFlagImports(capitals.models,cityFlagImports);
            updateNativeCityPopulation(areaModels,readNativeBaseVisualPrimary(capitals.models.primary.data(),capitals.models.primary.size()));
            if(sourceAccessoryVisuals){
                auto current=readNativeAreaCapitalAccessories(capitals.models.accessories);
                observeNativeSharedAccessorySource(sharedAreaAccessories,areaAccessories.population,current);
                publishNativeBaseVisualAccessories(areaAccessories,std::move(current));
            }
            appliedAreaCapitalRevision=capitals.models.revision;primaryPopulationChanged=true;
        }
        if(primaryPopulationChanged) {
            publishNativeCityAreaPrimary(primaryModels,cityModels,areaModels);
            publishNativeCityAreaJointShadow(jointShadow,*currentCityGateShadow,staticCatalog,primaryModels,
                shadowCameraProfiles,shadowGpuParameters);
            if(!allStaticModels.draws.empty())connectNativeCityAreaJointShadowGeometry(jointShadow,allStaticModels,primaryModels);
            std::cout<<"source AreaCapitalWorld revision="<<appliedAreaCapitalRevision
                <<" owners="<<areaModels.source.worlds.size()<<" primitives="<<areaModels.source.catalog.primitives.size()
                <<" jointOwners="<<jointShadow.catalog.instances.size()<<" jointPrimitives="<<jointShadow.catalog.primitives.size()<<'\n';
        }
        syncArmyModels();
#endif
    };
    auto prepareAcceptedCamera=[&]() {
        if(!sourceStaticModels)return;
#ifdef SAN14_RENDER_SESSION
        territoryDisplayEnabled=(host.input.flags&8)==0;
        currentSeason=host.currentSeason?&*host.currentSeason:nullptr;
        currentCloudLights={host.input.currentRawSunPosition,host.input.currentCloudGeometry40};
        publishCurrentWorldOwnership();
#endif
        publishCurrentModels();
#ifdef SAN14_RENDER_SESSION
        if(host.input.flags&1) {
            staticCameraRuntime.pose=host.input.controller;
            staticCameraRuntime.zoom={host.input.band,host.input.fraction,
                source_camera::A(float(host.input.band),source_camera::D(float(host.input.fraction),10000))};
            staticCamera=source_camera::preparePublished(staticCameraProfile,host.input.camera,host.input.controller);
        }else {
#endif
        const int32_t inertiaClock=sampleNativeCameraClock(cameraClock);
        const int32_t animationClock=controlledCameraClock?*controlledCameraClock:sampleNativeCameraClock(cameraClock);
        controlledCameraClock.reset();
        source_camera_motion::tick(staticCameraProfile,cameraMotionProfile,cameraMotion,inertiaClock,animationClock,cameraMotionProfile.inertiaTimeout,cameraTerrainQualifier);
        ++cameraTickCount;
        staticCamera=source_camera::prepareCurrent(staticCameraProfile,staticCameraRuntime);
#ifdef SAN14_RENDER_SESSION
        }
#endif
        currentDisplay=source_far_main::prepareCurrent(farMainProfile,farMainOwner,staticCameraRuntime.zoom,staticCamera.prefix);
        currentRenderMode=currentDisplay.mode.ownerMode288;
        currentShadowFamily=currentRenderMode==0?0u:1u;
        fieldDisplay.capture(currentDisplay);
        if(fieldEffects)currentFieldEffects=fieldEffects->capture(currentDisplay);
        if(currentSeason) {
            mainDirectPrefix=currentSeason->main;
            source_current_season::publishMain(*currentSeason,
                {reinterpret_cast<uint8_t*>(currentMain18C0.data()),currentMain18C0.size()});
            currentShadowParameters=source_current_season_shadow::prepareUnclampedCompact(*currentSeason);
            source_current_season_shadow::publish(currentShadowParameters,
                shadowCameraProfiles[currentShadowFamily],shadowGpuParameters[currentShadowFamily]);
            std::memcpy(weatherCameraFrame.main72.data(),currentSeason->main.data()+0x340,72);
        }else {
            mainDirectPrefix=source_camera::prepareCurrentRenderModeDirectPrefix(mainDirectProfile,mainDirectState,
                source_camera::prepareCurrentRenderMode(staticCameraRuntime.zoom),staticCamera.prefix);
            weatherCameraFrame=source_weather_fog::prepareCurrent(weatherCameraProfile,staticCamera.eye.Y,source_camera::get<float>(mainDirectPrefix.data()+0x210));
        }
        shadowCameraProfile=&shadowCameraProfiles[currentShadowFamily];
        if(atmosphereCameraReady){
            if(currentSeason){publishNativeCurrentSeasonAtmosphere(atmosphereCamera,mainCloud,*currentSeason,&currentCloudLights);
                publishNativeCurrentMainSkyMoonFace(atmosphereCamera,*currentSeason);}
            else {
                publishNativeAtmosphereCurrentDirectPrefix(atmosphereCamera,mainDirectPrefix);
                publishNativeAtmosphereWeather(atmosphereCamera,mainCloud,weatherCameraFrame,globalAtmosphereRgb);
            }
        }
        staticPlan=selectNativeModelContinuousFromCameraFrame(staticCatalog,staticCamera);
        fieldDisplay.selectOrdinary(staticPlan,staticCatalog);
        cityPlan=selectNativeCityPopulation(primaryModels.source,staticCamera);
        fieldDisplay.publishShadow(shadowCameraProfiles[currentShadowFamily],currentShadowFamily);
        shadowCameraFrame=source_shadow_camera::prepareCurrent(*shadowCameraProfile,staticCamera,jointShadow.catalog,shadowHistory);
        ordinaryPopulationCounts=shadowCameraFrame.ordinaryRecordCounts;
        if(sourceAccessoryVisuals) {
#ifdef SAN14_RENDER_SESSION
            accessoryOrdinaryCount=selectNativeSharedAccessories(context.Get(),*armyModelPool,
                sharedAreaAccessories,areaAccessories,staticCamera);
            accessoryOrdinaryCount+=selectNativeSharedAccessories(context.Get(),*armyModelPool,
                sharedCityAccessories,cityAccessories,staticCamera);
#else
            const auto& mode=currentDisplay.mode;
            accessoryOrdinaryCount=updateNativeCityAccessoryGpu(device.Get(),context.Get(),cityAccessories,staticCamera,
                mode.ownerMode288,mode.ecB0Index,staticCameraRuntime.pose.distance,
                true,
                30,
                [&](unsigned,float requested) {
                    return source_default_player_phase::defaultReturnedPhase(defaultPlayerPhases.categories[2],requested);
                });
#endif
            for(unsigned i=0;i<viewProfile.records.size();++i)
                if(viewProfile.records[i].mask==1)ordinaryPopulationCounts[i]+=accessoryOrdinaryCount;
        }
        if(sourceArmyVisuals) {
            armyPlan=selectNativeArmyPopulation(armyModels,staticCamera,armyReturnedPhases,
#ifdef SAN14_RENDER_SESSION
                &armySuccessfulSamples,&armyActivePoses,&armyMainMembership
#else
                nullptr
#endif
                );
            for(unsigned i=0;i<viewProfile.records.size();++i)
                for(unsigned j=0;j<armyModels.source.recordMasks.size();++j)
                    if(viewProfile.records[i].mask==armyModels.source.recordMasks[j])ordinaryPopulationCounts[i]+=armyPlan.ordinaryCounts[j];
        }
        if(ordinaryPopulationCounts.size()!=cityPlan.ordinaryCounts.size())throw std::runtime_error("Field/City ordinary record identities differ");
        // D12A80 consumes this accepted Main, retaining its actual prepared
        // weather/wind tail. Geometry matrices use the ordinary source camera.
        std::memcpy(currentMain18C0.data(),mainDirectPrefix.data(),mainDirectPrefix.size());
        std::array<float,16> groundW2P;std::array<float,12> groundWV;
        std::memcpy(groundW2P.data(),staticCamera.prefix.data()+0x100,64);
        for(size_t row=0;row<3;++row)for(size_t column=0;column<4;++column)
            groundWV[row*4+column]=source_camera::get<float>(staticCamera.prefix.data()+0x80+column*16+row*4);
        const auto& mode=currentDisplay.mode;
        const auto& groundCaller=cityModels.source.caller;
        const auto& groundPopulation=groundDetails.prepare({staticCamera,
            {reinterpret_cast<const uint8_t*>(currentMain18C0.data()),currentMain18C0.size()},commonPublication,groundW2P,groundWV,
            {viewProfile.commonPresent!=0,viewProfile.commonFlags,groundSourceFlags14,viewProfile.commonExclusion,groundFrustumType64},
            commonClock.settingsFlags10,groundCulling450,groundScanning454,
            groundCaller.callerFirst,groundCaller.callerSecond,groundCaller.callerShadow,groundCaller.registered3330,groundCaller.successful3340,0,
            groundAppBaseFlags,mode.ownerMode288,mode.ecB0Index,true,false,0,currentSeason?currentSeason->season:0});
        if(groundPopulation.seasonPopulationChanged)
            std::cout<<"source GroundDetails settled seasonGroup="<<groundPopulation.seasonGroup
                <<" wholeInstances="<<groundPopulation.wholeInstances
                <<" selectedInstances="<<groundPopulation.selectedInstances
                <<" preparedPrimitives="<<groundPopulation.preparedPrimitives<<'\n';
        for(size_t record=0;record<viewProfile.records.size();++record)
            ordinaryPopulationCounts[record]+=groundDetails.preparedCountForRecord(viewProfile.records[record].mask);
        // The joint source profile already counts every admitted primary World
        // once before its ordinary54/55 split. Do not add the rows again here.
        source_view::Inputs population;
        population.ordinaryCounts=ordinaryPopulationCounts;
        population.collectionPrimitiveCounts=shadowCameraFrame.primitiveCounts;
        population.subWrapperCount1BFC=0; // This finite renderer owns no optional wrapper.
        population.terrainPresent=!staticCamera.instances.empty();
        population.selectedCompactPresent=shadowCameraFrame.passes[0].shadowAvailable!=0;
        population.selectedLitPresent=directionalDiffuse!=nullptr;
        if(currentSeason) {
            viewProfile.mainA00=currentShadowParameters.selectedSignedA00;
            viewProfile.mainA28=currentShadowParameters.scaleA28;
            viewProfile.mainA3C=source_current_season_shadow::mainWord<uint32_t>(*currentSeason,0xa3c);
        }
        currentView=source_view::prepareCurrent(viewProfile,population);
        actualViewRainFlags19B4=currentView.rain19B4;
        deferredCameraFields=prepareNativeDeferredCameraFields(staticCamera.prefix);
        fieldDisplay.finishPreparation();
        std::cout<<"current source Main terrain="<<staticCamera.instances.size()<<" Model="<<staticPlan.visibleInstances.size()<<'/'<<staticPlan.draws.size()
            <<" View="<<std::hex<<currentView.flags19AC<<std::dec<<'\n';
    };
#ifdef SAN14_RENDER_SESSION
    const auto animationFps=source_animation_cadence::readProfile(
        input.parent_path()/"animation-cadence-input"/"fps-source.bin");
    if((sourceArmyVisuals||sourceAccessoryVisuals)&&armyModels.source.defaultActions.fps!=animationFps.fps)
        throw std::runtime_error("Army and App imported FPS tables differ");
    host.animationUpdate=[&](unsigned fpsMode) {
        if(fpsMode>=animationFps.fps.size())throw std::runtime_error("Actual FPS manager mode range");
        publishCurrentModels();
        const auto eventCamera=source_camera::preparePublished(staticCameraProfile,host.input.camera,host.input.controller);
        const auto zoom=source_camera::Zoom{host.input.band,host.input.fraction,
            source_camera::A(float(host.input.band),source_camera::D(float(host.input.fraction),10000))};
        const auto mode=source_far_main::currentMode(zoom);
        resetNativeArmyAnimationGroups(armyAnimationGroups); // one manager, including an empty Army
        if(sourceArmyVisuals) {
            std::map<int32_t,std::vector<const ArmyWorldActor*>> candidates;
            for(const auto& actor:host.armyWorld->actors)candidates[actor.key.unit].push_back(&actor);
            std::array<std::vector<NativeAccessoryPoseJoint>,7> sampledGroupLocals;
            auto materialReady=[&](const ArmyWorldActor& actor){
                for(const auto& primitive:armyWorldTemplates->primitives[unsigned(actor.key.category)])
                    if(primitive.clothMaterial&&!armyWorldTemplates->materials.count({primitive.sourceTemplate,actor.material}))return false;
                return true;
            };
            for(const auto& [id,center]:host.armyWorld->unitCenters) {
                const unsigned modelMode=host.armyWorld->unitModelModes.at(id);
                const bool siege=modelMode==2,naval=modelMode==1;
                auto& height=armyUnitHeights[id];height.observeMotion(host.armyWorld->unitMotionRevisions.at(id));
                auto currentCenter=center;currentCenter[1]=height.y14;
                auto gate=std::floor(mode.ecB0Index)<4?source_accessory_population::unitAdmission(currentCenter,
                    reinterpret_cast<const char*>(eventCamera.prefix.data()),reinterpret_cast<const char*>(eventCamera.prefix.data()),
                    mode.ownerMode288,mode.ecB0Index,host.input.controller.distance):source_accessory_population::UnitAdmission{};
                const bool reached=gate.visible||armyPriorVisible[id];armyPriorVisible[id]=gate.visible;
                unsigned resolved=0;
                if(reached){
                    if(!gate.visible)armyModelPool->releaseUnit(id);
                    else for(int descriptor:{0x750,0x778,0x7A0,0x7C8,0x7F0,0x818,0x840})
                    for(const auto* actor:candidates[id])if(actor->key.descriptor==descriptor&&materialReady(*actor)){
                        const bool existing=(actor->key.descriptor==0x7A0||actor->key.descriptor==0x818)&&armyModelPool->member(GraphicsModelFamily::Army,
                            id,actor->key.descriptor,actor->key.instance).has_value();
                        if(!existing&&armyModelPool->acquire(actor->key))height.dirty30=true;
                        if((naval?actor->key.descriptor==0x818:siege?actor->key.descriptor==0x7A0:actor->key.category<2)&&
                            (existing||armyModelPool->find(actor->key)))++resolved;
                    }
                }
                const bool dirtyBeforeV30=height.dirty30;
                // Imported siege nodes are all ready; absent list nodes do not
                // mark remainingDirty in this source branch.
                const bool setter=height.updateGround(reached,true,host.armyWorld->unitGroundHeights.at(id),
                    naval?resolved==1:siege||resolved==19,modelMode);
                syncArmyModels();
                // Complete this unit before the next caller can take any slots.
                float siegeChainFrame=-1,navalChainFrame=-1;
                for(const auto* candidate:candidates[id]) {
                auto actor=*candidate;
                if(actor.key.descriptor==0x7A0||actor.key.descriptor==0x818){
                    auto member=armyModelPool->member(GraphicsModelFamily::Army,id,actor.key.descriptor,actor.key.instance);
                    if(!member)continue;actor.key=*member;
                }
                auto index=armyWorldState.actorIndices.find(actor.key);if(index==armyWorldState.actorIndices.end())continue;
                auto& presented=armyModels.source.actors[index->second];
                if(setter){
                    if(actor.key.descriptor==0x7A0||actor.key.descriptor==0x818){
                        actor.world.scale=presented.world.scale; // v30 does not write scale.
                        actor.world.translation.y=height.y14;
                        actor.bounds=buildArmyWorldModeBounds(armyWorldTemplates->localBounds.at(unsigned(actor.key.category)),actor.world);
                    }
                    publishArmyWorldPresentation(presented,actor);
                }
                const unsigned category=unsigned(actor.key.category);
                const bool siegeBody=category>=4&&category<=6,navalBody=category>=7&&category<=9;
                const unsigned graphics=unsigned(host.armyWorld->graphicsActions.at(id));
                const bool attacking=graphics==2&&host.armyWorld->unitSelectedFormations.at(id)!=8;
                const unsigned raw=category<2?graphics:siegeBody&&attacking?2u:0u;
                auto actionRow=armyGraphicsActions->rows.find({category,raw});
                const auto* action=actionRow==armyGraphicsActions->rows.end()?nullptr:&actionRow->second;
                auto& physical=*armyModelPool->find(actor.key);auto& player=physical.clock;
                bool eligible=reached;
                if(category==2||category==3)eligible=eligible&&gate.animateNear;
                if(category==3)eligible=eligible&&std::floor(mode.ecB0Index)<1;
                if(navalBody)eligible=eligible&&gate.animateNear;
                if(siegeBody)eligible=eligible&&(graphics==2?
                    host.armyWorld->unitSelectedFormations.at(id)!=8:dirtyBeforeV30);
                const NativeArmyAnimationCallback callback{true,eligible,int(raw),
                    siegeBody?siegeChainFrame:navalBody?navalChainFrame:-1,fpsMode};
                const auto step=action?consumeNativeArmyAnimation(action->player,armyModels.source.defaultActions.fps,category,
                    player,armyAnimationGroups,callback,
                    [&](float requested,bool){return std::optional<float>(source_default_player_phase::defaultReturnedPhase(
                        {action->player.clipDuration,action->player.loopDeclaration},requested));}):
                    consumeNativeArmyUnavailableAction(player,callback);
                if(siegeBody&&eligible)siegeChainFrame=step.nextFrame;
                if(navalBody&&eligible)navalChainFrame=step.nextFrame;
                armyReturnedPhases[index->second]=player.returnedPhase;
                armySuccessfulSamples[index->second]=player.hasSuccessfulSample;
                if(step.succeeded){
                    const auto* pose=action->pose?&*action->pose:&armyModels.source.poses[category];
                    armyActivePoses[index->second]=pose;
                    // Consume every accepted action/phase in event order.
                    // Sparse local writes must survive A→B→A before one draw.
                    auto& owner=armyModels.poses[index->second];auto& cache=armyModels.poseCaches[index->second];
                    const int slot=action->player.grouped?nativeArmyAnimationGroupSlot(category,raw):-1;
                    if(step.cached)
                        publishNativeArmySharedLocal(*pose,owner,cache,sampledGroupLocals[size_t(slot)],
                            player.returnedPhase,presented.world);
                    else{
                        prepareNativeArmyCurrentPose(*pose,owner,cache,player.returnedPhase,presented.world,true,category>=2);
                        if(slot>=0)sampledGroupLocals[size_t(slot)]=owner.frame.localPose;
                    }
                }
                physical.presented={presented.world,presented.obb};
                physical.pose=armyModels.poses[index->second];physical.cache=armyModels.poseCaches[index->second];
                }
            }
            for(auto it=armyPriorVisible.begin();it!=armyPriorVisible.end();)
                if(!host.armyWorld->unitCenters.count(it->first))it=armyPriorVisible.erase(it);else ++it;
        }
        if(sourceAccessoryVisuals) {
            const auto& action=armyGraphicsActions->rows.at({2,0});
            updateNativeSharedAccessoryFamily(device.Get(),*armyModelPool,sharedAreaAccessories,areaAccessories,
                GraphicsModelFamily::AreaCapital,eventCamera,mode.ownerMode288,mode.ecB0Index,
                host.input.controller.distance,action,armyModels.source.poses[2],animationFps.fps,fpsMode,armyAnimationGroups);
            updateNativeSharedAccessoryFamily(device.Get(),*armyModelPool,sharedCityAccessories,cityAccessories,
                GraphicsModelFamily::City,eventCamera,mode.ownerMode288,mode.ecB0Index,
                host.input.controller.distance,action,armyModels.source.poses[2],animationFps.fps,fpsMode,armyAnimationGroups);
            updateNativeSharedAccessoryFamily(device.Get(),*armyModelPool,sharedCityAccessories,cityAccessories,
                GraphicsModelFamily::Gate,eventCamera,mode.ownerMode288,mode.ecB0Index,
                host.input.controller.distance,action,armyModels.source.poses[2],animationFps.fps,fpsMode,armyAnimationGroups);
        }
        auto& state=host.animationState;++state.updates;state.fpsMode=fpsMode;
        state.armyPlayers=sourceArmyVisuals?unsigned(armyWorldState.actorIndices.size()):0;
        state.cityGroups=sourceAccessoryVisuals?unsigned(armyModelPool->boundCount(GraphicsModelFamily::City)+
            armyModelPool->boundCount(GraphicsModelFamily::Gate)):0;
        state.areaGroups=sourceAccessoryVisuals?unsigned(armyModelPool->boundCount(GraphicsModelFamily::AreaCapital)):0;
        state.firstBodyPhase=state.firstFlagPhase=0;
        bool body=false,flag=false;
        if(sourceArmyVisuals)for(const auto& actor:host.armyWorld->actors){
            auto key=actor.key;
            if(key.descriptor==0x7A0||key.descriptor==0x818){auto member=armyModelPool->member(GraphicsModelFamily::Army,key.unit,key.descriptor,key.instance);
                if(!member)continue;key=*member;}
            if(auto* slot=armyModelPool->find(key)) {
            if((key.category<2||key.category>=4)&&!body){state.firstBodyPhase=slot->clock.returnedPhase;body=true;}
            if(actor.key.category==2&&!flag){state.firstFlagPhase=slot->clock.returnedPhase;flag=true;}
            }
        }
    };
    if(host.input.flags&4)host.animationUpdate(0); // explicit initial normal Map callback
    host.armyUnitPositions=[&]() {
        if(!sourceArmyVisuals)return std::vector<ArmyLabelUnitPosition>{};
        return readArmyLabelUnitPositions(host.armyWorld->unitCenters,armyUnitHeights);
    };
#endif
    auto beginAcceptedMain=[&](float currentClock) {
        if(sourceStaticModels&&!runLoop)
            commonPublication=advanceNativeFrameClock(commonClock,currentClock-commonClock.current3348);
        prepareAcceptedCamera(); // Current Main is available to node1, not only geometry.
        if(sourceStaticModels){
            const auto parent=NativeMainTargetScope::retain(context.Get());
            ComPtr<ID3D11RasterizerState> priorRaster;context->RSGetState(&priorRaster);
            source_map_exposure::Selection exposureSelection;
            exposureSelection.applyReferencePresent=true; // This normal pipeline owns the Apply/Exposure module.
            source_map_exposure::SelectedLight exposureLight;
            if(currentSeason&&currentSeason->exposure.present){
                exposureLight={currentSeason->exposure.type,currentSeason->exposure.flags,currentSeason->exposure.rgb};
                exposureSelection.selectedLight=&exposureLight;
            }
            if(currentSeason)mapExposure.profile.cameraSpecial1510=source_camera::get<uint32_t>(
                reinterpret_cast<const uint8_t*>(currentMain18C0.data())+0x1510);
            const auto packet=executeNativeMapExposure(context.Get(),mapExposure,mainDirectPrefix,exposureSelection);
            ++mapExposureDraws;
            if(packet.halfRectangle)++mapExposureHalfDraws;
            if(!runLoop){
                D3D11_TEXTURE2D_DESC descriptor{};mapExposure.texture->GetDesc(&descriptor);descriptor.BindFlags=0;
                ComPtr<ID3D11Texture2D> copy;check(device->CreateTexture2D(&descriptor,nullptr,&copy),"finite node1 exposure receipt");
                context->OMSetRenderTargets(0,nullptr,nullptr);context->CopyResource(copy.Get(),mapExposure.texture.Get());
                mapExposureBeforeMainReceipts.push_back(copy);
            }
            parent.restore(context.Get());context->RSSetState(priorRaster.Get());
        }
        if(sourceStaticModels)updateNativeFogAccelerationCamera(context.Get(),fogCamera,fogAcceleration,staticCamera.prefix);
        specialEffects.queueFlags=0;contextFlags3F4=0; // EA9300 accepted node1.
        std::cout<<"source Main entry Q3F0=0 Q3F4=0 retainedHost7F8="<<std::hex<<actualHostQ7F8<<std::dec<<" clock="<<currentClock<<'\n';
        if(oceanEffects)executeNativeFogAcceleration(device.Get(),context.Get(),fogAcceleration);
        if(oceanLighting) {
            float currentPlanet=0;
            if(currentSeason) {
                if(!currentCloudWeather || std::none_of(currentCloudWeather->profiles.seasons.begin(),
                    currentCloudWeather->profiles.seasons.end(),[&](const auto& profile){return profile.season==currentSeason->season;}))
                    currentCloudWeather=readNativeCurrentCloudWeather(
                    input.parent_path()/"current-cloud-weather-input");
                const auto weather=publishNativeCurrentCloudWeatherBeforeClock(*currentCloudWeather,*currentSeason,mainCloud,environmentCloud);
                currentPlanet=weather.input[0x228/4];
                if(!currentAmbient)currentAmbient=prepareNativeCurrentAmbientFrame(device.Get(),
                    input.parent_path()/"current-ambient-frame-input",environment,environmentCloud);
            }
            const float node3Delta=advanceNativeMainCloud(mainCloud,currentClock); //once before geometry.
            if(currentSeason) {
                prepareNativeCurrentAmbientInputs(*currentAmbient,*currentSeason,currentCloudLights);
                std::array<source_camera::V4,6> cloudClip{};
                for(unsigned face:{0u,1u,4u,5u})cloudClip[face]=source_work_cloud_clip::prepareCurrentWorkCloudClip(
                    currentPlanet,currentAmbient->current.faces[face].camera,2);
                publishNativeCurrentAmbientFrame(context.Get(),*currentAmbient,environment,environmentCloud,&cloudClip);
            }
            updateNativeAmbientCloudContinuous(context.Get(),environmentCloud,mainCloud);
            const auto parent=NativeMainTargetScope::retain(context.Get());
            // Current source Main1680 exists and Aux12 bit0 is clear.
            // Refresh the actual returned SRV; generation selection can change.
            ambient=executeNativeAmbientNode5(context.Get(),environment,currentClock,true,node3Delta);
            parent.restore(context.Get());
            std::cout<<"source Main node3 Cloud phaseClock="<<mainCloud.lastUpdateClock<<'\n';
            std::cout<<"source Main node5 state168="<<std::hex<<environment.state168<<std::dec
                <<" selected="<<environment.selected<<" starPhase="<<environment.starAnimation.phase<<'\n';
        }
    };
    auto publishThinSurfaceFlags=[&](UINT node) {
        if(actualViewRainFlags19B4!=0)specialEffects.queueFlags|=0x400;
        std::cout<<"source Main node"<<node<<" View19B4="<<actualViewRainFlags19B4
            <<" Q3F0="<<std::hex<<specialEffects.queueFlags<<" Q3F4="<<contextFlags3F4<<std::dec<<'\n';
    };
    auto finishNormalApply=[&]() {
        actualHostQ7F8|=3; // EB8027/EB8040 retained Host writer.
        specialEffects.queueFlags|=0x20; // E9E6D4 outer193, including zero draw.
        std::cout<<"source Main193 exit Q3F0="<<std::hex<<specialEffects.queueFlags<<" Q3F4="<<contextFlags3F4
            <<" retainedHost7F8="<<actualHostQ7F8<<std::dec<<'\n';
    };
    if(wantsLighting) {
        UINT sourceResource,sourceShader,sourceTarget,sourceWidth,sourceHeight;
        std::ifstream sourceExtent(input.parent_path()/"post-input"/"lighting-target.txt");
        sourceExtent>>sourceResource>>sourceShader>>sourceTarget>>sourceWidth>>sourceHeight;
        if(!sourceExtent||width!=sourceWidth||height!=sourceHeight)throw std::runtime_error("Re-export size-dependent source inputs before resizing the full experiment");
#ifdef SAN14_RENDER_SESSION
        lightingSource=input.parent_path()/"lighting-input";
#else
        lightingSource=argc>=10&&std::string(argv[9])!="-"?fs::path(argv[9]):input.parent_path()/(shadowLighting?"lighting-shadow-input":"lighting-input");
#endif
        // Source resource initialization precedes Main traversal. Running it
        // between68 and78 would clear the real receiver's retained state.
        ggx=generateGgx(device.Get(),context.Get(),lightingSource,output);
        if(oceanLighting) {
            environment=prepareNativeAmbient(device.Get(),input.parent_path()/"ambient-input");
            // Explicit backend startup supplies all history slots. This does
            // not claim the original engine's elapsed initialization history.
            for(UINT slot=0;slot<3;++slot)executeNativeAmbientBatch(context.Get(),environment,slot);
            executeNativeAmbientBlend(context.Get(),environment,0,0);environment.selected=0;
            if(!runLoop)captureNativeAmbientCube(device.Get(),context.Get(),environment,output);
            ambient=environment.selectedView();
        } else ambient=produceAmbientCube(device.Get(),context.Get(),input.parent_path()/"ambient-input",output);
        context->ClearState(); // Bootstrap ends before any Main entry; not a per-request operation.
        if(oceanEffects) {
            if(observedParticleExperiment) {
                effects=loadNativeEffectInputs(device.Get(),input.parent_path()/"effect-consumer-input"/"source-main");
                if(sourceStaticModels){
                    effectCamera=prepareNativeEffectCameraContinuous(device.Get(),input.parent_path()/"effect-camera-continuous-input",effects);
                    fieldEffects.emplace(farMainProfile,effectCamera.profile,
                        source_field_effect::loadProfile(input.parent_path()/"effect-field-admission-input/profile.bin"));
                }
            }
            if(observedParticleExperiment) {
                specialEffects=loadNativeSpecialEffects(input.parent_path()/"effect-consumer-input"/"specialized141-147");
                if(specialEffects.hostFlags!=sourceHostFlags3E4)throw std::runtime_error("D33 and Main entry use different original Host fields");
            } else specialEffects=createNativeSpecialEffectsWithoutParticleOwner(sourceHostFlags3E4);
            fogAcceleration=prepareNativeFogAcceleration(device.Get(),input.parent_path()/"fog-accel-input");
            if(sourceStaticModels)fogCamera=prepareNativeFogAccelerationCamera(device.Get(),input.parent_path()/"fog-accel-input",fogAcceleration);
        }
        if(sourceStaticModels){
            mapExposure=prepareNativeMapExposure(device.Get(),input.parent_path()/"map-exposure-input");
            // Existing controlled Exposure61 prior[1,1] supplies this experiment's
            // bootstrap left value through the real stock publication. It is not
            // a claim about the original data-null allocation's first contents.
            retainedNormalExposure=prepareNormalPostExposure(device.Get(),normalInitialSource);
            const auto parent=NativeMainTargetScope::retain(context.Get());
            ComPtr<ID3D11RasterizerState> priorRaster;context->RSGetState(&priorRaster);
            publishNativeMapExposureAfterApply(context.Get(),mapExposure,retainedNormalExposure.views[0].Get());
            ++mapExposurePublicationDraws;
            parent.restore(context.Get());context->RSSetState(priorRaster.Get());
        }
        if(sourceHdr)litColor=createSourceHdr(device.Get(),input.parent_path()/"early-hdr-input"/"node78-inner02",width,height);
        if(oceanLighting) {
            mainCloud=prepareNativeMainCloud(device.Get(),input.parent_path()/"main-cloud-input",
                input.parent_path()/"main-cloud-continuous-input",litColor.Get());
            environmentCloud=prepareNativeAmbientCloudContinuous(device.Get(),input.parent_path()/"ambient-cloud-continuous-input",environment,mainCloud);
            environment.starAnimation=prepareNativeAmbientStarContinuous(input.parent_path()/"ambient-stars-continuous-input");
            environment.starAnimationEnabled=true;
        }
        if(sourceHdr)beginAcceptedMain(initialCommonClock);
    }
    auto vsBytes=load(input/(groupedTerrain?"near-vs.dxbc":"terrain-vs.dxbc")),psBytes=load(input/(groupedTerrain?"near-ps.dxbc":"terrain-ps.dxbc"));
    ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;
    check(device->CreateVertexShader(vsBytes.data(),vsBytes.size(),nullptr,&vs),"original terrain VS");
    check(device->CreatePixelShader(psBytes.data(),psBytes.size(),nullptr,&ps),"original terrain PS");
    ComPtr<ID3D11ShaderReflection> reflection;
    check(D3DReflect(vsBytes.data(),vsBytes.size(),__uuidof(ID3D11ShaderReflection),reinterpret_cast<void**>(reflection.GetAddressOf())),"VS reflection");
    D3D11_SHADER_DESC shaderDesc{};reflection->GetDesc(&shaderDesc);
    std::vector<D3D11_INPUT_ELEMENT_DESC> elements;
    std::array<DXGI_FORMAT,5> inputFormats{DXGI_FORMAT_R32G32B32_FLOAT,DXGI_FORMAT_R32G32B32_FLOAT,DXGI_FORMAT_R32G32B32_FLOAT,DXGI_FORMAT_R32G32B32A32_FLOAT,DXGI_FORMAT_R32G32B32A32_FLOAT};
    std::array<UINT,5> offsets{0,0,12,24,40};
    for(UINT i=0;i<shaderDesc.InputParameters;++i){D3D11_SIGNATURE_PARAMETER_DESC p{};reflection->GetInputParameterDesc(i,&p);if(p.SystemValueType!=D3D_NAME_UNDEFINED)continue;
        UINT n=UINT(elements.size());if(n>=5)throw std::runtime_error("Unexpected Landscape vertex signature");
        elements.push_back({p.SemanticName,p.SemanticIndex,inputFormats[n],n?1u:0u,offsets[n],n?D3D11_INPUT_PER_INSTANCE_DATA:D3D11_INPUT_PER_VERTEX_DATA,n?1u:0u});
        std::cout<<"input "<<p.SemanticName<<p.SemanticIndex<<" slot="<<(n?1:0)<<" offset="<<offsets[n]<<'\n';
    }
    ComPtr<ID3D11InputLayout> layout;
    check(device->CreateInputLayout(elements.data(),UINT(elements.size()),vsBytes.data(),vsBytes.size(),&layout),"Landscape input layout");
    auto positions=buffer(device.Get(),load(input/"positions.bin"),D3D11_BIND_VERTEX_BUFFER);
    auto instances=buffer(device.Get(),load(input/"instances.bin"),D3D11_BIND_VERTEX_BUFFER);
    auto indices=buffer(device.Get(),load(input/"indices.bin"),D3D11_BIND_INDEX_BUFFER);
    auto vsCb=buffer(device.Get(),load(input/(groupedTerrain?"near-vs-b0.bin":"vs-b0.bin")),D3D11_BIND_CONSTANT_BUFFER);
    auto psCb=buffer(device.Get(),load(input/(groupedTerrain?"near-ps-b0.bin":"ps-b0.bin")),D3D11_BIND_CONSTANT_BUFFER);
    ComPtr<ID3D11VertexShader> coarseVs;ComPtr<ID3D11PixelShader> coarsePs;
    ComPtr<ID3D11Buffer> coarseVsCb,coarsePsCb;
    if(groupedTerrain){
        auto cv=load(input/"coarse-vs.dxbc"),cp=load(input/"coarse-ps.dxbc");
        check(device->CreateVertexShader(cv.data(),cv.size(),nullptr,&coarseVs),"original coarse terrain VS");
        check(device->CreatePixelShader(cp.data(),cp.size(),nullptr,&coarsePs),"original coarse terrain PS");
        coarseVsCb=buffer(device.Get(),load(input/"coarse-vs-b0.bin"),D3D11_BIND_CONSTANT_BUFFER);
        coarsePsCb=buffer(device.Get(),load(input/"coarse-ps-b0.bin"),D3D11_BIND_CONSTANT_BUFFER);
    }
    ID3D11Buffer* vb[]{positions.Get(),instances.Get()};UINT strides[]{12,56},vbOffsets[]{0,0};
    context->IASetInputLayout(layout.Get());context->IASetVertexBuffers(0,2,vb,strides,vbOffsets);context->IASetIndexBuffer(indices.Get(),DXGI_FORMAT_R16_UINT,0);
    context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);context->VSSetShader(vs.Get(),nullptr,0);context->PSSetShader(ps.Get(),nullptr,0);
    auto cb=vsCb.Get();context->VSSetConstantBuffers(0,1,&cb);cb=psCb.Get();context->PSSetConstantBuffers(0,1,&cb);
    std::vector<ComPtr<ID3D11Texture2D>> inputTextures;std::vector<ComPtr<ID3D11ShaderResourceView>> views;
    std::array<ID3D11ShaderResourceView*,16> terrainVertexViews{},terrainPixelViews{};
    std::array<ID3D11SamplerState*,16> terrainVertexSamplers{},terrainPixelSamplers{};
    std::ifstream textureRows(input/"textures.txt");std::string stage,name;UINT slot;
    while(textureRows>>stage>>slot>>name){
        auto texture=loadTexture(device.Get(),input/name);D3D11_TEXTURE2D_DESC d{};texture->GetDesc(&d);ComPtr<ID3D11ShaderResourceView> view;
        check(device->CreateShaderResourceView(texture.Get(),nullptr,&view),"source texture view");auto raw=view.Get();
        if(stage=="VS"){context->VSSetShaderResources(slot,1,&raw);terrainVertexViews.at(slot)=raw;}
        else{context->PSSetShaderResources(slot,1,&raw);terrainPixelViews.at(slot)=raw;}
        inputTextures.push_back(texture);views.push_back(view);std::cout<<"texture "<<stage<<slot<<' '<<d.Width<<'x'<<d.Height<<" layers="<<d.ArraySize<<" mips="<<d.MipLevels<<" format="<<d.Format<<'\n';
    }
    std::vector<ComPtr<ID3D11SamplerState>> samplers;std::ifstream samplerRows(input/"samplers.txt");
    UINT filter,u,v,w,aniso,comparison;float bias,minLod,maxLod;
    while(samplerRows>>stage>>slot>>filter>>u>>v>>w>>bias>>aniso>>comparison>>minLod>>maxLod){
        D3D11_SAMPLER_DESC d{};d.Filter=D3D11_FILTER(filter);d.AddressU=D3D11_TEXTURE_ADDRESS_MODE(u);d.AddressV=D3D11_TEXTURE_ADDRESS_MODE(v);d.AddressW=D3D11_TEXTURE_ADDRESS_MODE(w);d.MipLODBias=bias;d.MaxAnisotropy=aniso;d.ComparisonFunc=D3D11_COMPARISON_FUNC(comparison);d.MinLOD=minLod;d.MaxLOD=maxLod;
        ComPtr<ID3D11SamplerState> sampler;check(device->CreateSamplerState(&d,&sampler),"source sampler");auto raw=sampler.Get();
        if(stage=="VS"){context->VSSetSamplers(slot,1,&raw);terrainVertexSamplers.at(slot)=raw;}
        else{context->PSSetSamplers(slot,1,&raw);terrainPixelSamplers.at(slot)=raw;}
        samplers.push_back(sampler);
    }
    std::array<ComPtr<ID3D11Texture2D>,5> targets;std::array<ComPtr<ID3D11RenderTargetView>,5> rtViews;std::array<ID3D11RenderTargetView*,5> rt{};
    D3D11_TEXTURE2D_DESC td{};td.Width=width;td.Height=height;td.ArraySize=td.MipLevels=1;td.SampleDesc.Count=1;td.Usage=D3D11_USAGE_DEFAULT;td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
    float clear[4]{};
    // This fixed scene's RS object 521C7DED has exactly five layers in source.
    // Node53 writes its surfaces0/2, not two unrelated textures or a five-MRT SRV.
    // Source85C125A6 (RR) is a distinct three-layer array; its surface0 is
    // the fourth MRT. The normal map is source7E94C315 (the second MRT).
    for(int i=0;i<5;++i){td.Format=resourceFormats[i];td.ArraySize=i==2?5:i==3?3:1;
        if(i==4&&!targetNames[4].empty()&&targetNames[4]==targetNames[2])targets[i]=targets[2];
        else check(device->CreateTexture2D(&td,nullptr,&targets[i]),"G-buffer target");
        D3D11_RENDER_TARGET_VIEW_DESC view{};view.Format=formats[i];
        if(i==2||i==3||i==4){view.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;view.Texture2DArray.FirstArraySlice=surfaces[i];view.Texture2DArray.ArraySize=1;}
        else view.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
        check(device->CreateRenderTargetView(targets[i].Get(),&view,&rtViews[i]),"G-buffer RTV");rt[i]=rtViews[i].Get();context->ClearRenderTargetView(rt[i],clear);}
    td.ArraySize=1;
    td.Format=resourceFormats[5];td.BindFlags=D3D11_BIND_DEPTH_STENCIL|D3D11_BIND_SHADER_RESOURCE;ComPtr<ID3D11Texture2D> depth;check(device->CreateTexture2D(&td,nullptr,&depth),"source depth target");
    ComPtr<ID3D11DepthStencilView> dsv;D3D11_DEPTH_STENCIL_VIEW_DESC depthView{};depthView.Format=depthFormat;depthView.ViewDimension=D3D11_DSV_DIMENSION_TEXTURE2D;
    check(device->CreateDepthStencilView(depth.Get(),&depthView,&dsv),"depth DSV");context->ClearDepthStencilView(dsv.Get(),D3D11_CLEAR_DEPTH|D3D11_CLEAR_STENCIL,depthClear,0);
    D3D11_DEPTH_STENCIL_DESC ds{};ds.DepthEnable=TRUE;ds.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ALL;ds.DepthFunc=D3D11_COMPARISON_FUNC(depthCompare);ds.StencilEnable=stencilEnable;ds.StencilReadMask=ds.StencilWriteMask=255;
    ds.FrontFace.StencilFunc=ds.BackFace.StencilFunc=D3D11_COMPARISON_ALWAYS;ds.FrontFace.StencilPassOp=ds.BackFace.StencilPassOp=D3D11_STENCIL_OP_REPLACE;
    ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=ds.BackFace.StencilFailOp=ds.BackFace.StencilDepthFailOp=D3D11_STENCIL_OP_KEEP;
    ComPtr<ID3D11DepthStencilState> depthState;check(device->CreateDepthStencilState(&ds,&depthState),"depth/stencil state");context->OMSetDepthStencilState(depthState.Get(),stencilRef);
    D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.CullMode=D3D11_CULL_MODE(cull);rs.FrontCounterClockwise=front;rs.DepthClipEnable=TRUE;
    ComPtr<ID3D11RasterizerState> raster;check(device->CreateRasterizerState(&rs,&raster),"rasterizer");context->RSSetState(raster.Get());
    D3D11_BLEND_DESC blend{};blend.IndependentBlendEnable=TRUE;for(int i=0;i<5;++i)blend.RenderTarget[i].RenderTargetWriteMask=uint8_t((colorMasks>>(i*4))&15);
    ComPtr<ID3D11BlendState> blendState;check(device->CreateBlendState(&blend,&blendState),"color masks");context->OMSetBlendState(blendState.Get(),nullptr,~0u);
    D3D11_VIEWPORT vp{0,0,float(width),float(height),0,1};
    D3D11_RECT mainScissor{0,0,LONG(width),LONG(height)};
    NormalPostSurface normalOutput;
    if(oceanNormal)normalOutput=createNormalPostFinalOutput(device.Get(),input.parent_path()/"normal-post-input",width,height);
    NativeMainTargetScope sixGbufferTargets;
    if(oceanLighting) {
        sixGbufferTargets.colorCount=6;sixGbufferTargets.depth=dsv;
        for(UINT i=0;i<5;++i)sixGbufferTargets.colors[i]=rtViews[i];
        D3D11_RENDER_TARGET_VIEW_DESC rr1{};rr1.Format=formats[3];rr1.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
        rr1.Texture2DArray.FirstArraySlice=1;rr1.Texture2DArray.ArraySize=1;
        check(device->CreateRenderTargetView(targets[3].Get(),&rr1,&sixGbufferTargets.colors[5]),"143/156 original RR1 surface view");
        sixGbufferTargets.viewportCount=sixGbufferTargets.scissorCount=1;
        sixGbufferTargets.viewports[0]=vp;sixGbufferTargets.scissors[0]=mainScissor;
    }
    NativeShadowMap shadowMap;NativeModelInputs visibleModels;
    NativeShadowCameraGpu shadowCameraGpu;source_shadow_gpu::Plan shadowGpuPlan;
    NativeTerrainCameraGpu terrainCamera;
    source_current_season_terrain::PaletteBank seasonPalette;
    source_current_season_terrain::Binding seasonPaletteBinding;
    const bool hasVisibleModels=fs::exists(modelSource/"models.txt");
    if(hasVisibleModels){
        if(sourceStaticModels) {
            allStaticModels=prepareNativeModelContinuousGpu(device.Get(),staticCatalog,modelSource);
            connectNativeJointPopulationShadowGeometry(jointShadow,allStaticModels,primaryModels.currentModels);
            modelStencil=prepareNativeModelStencilContinuous(device.Get(),input.parent_path()/"view-continuous-input",staticCatalog,allStaticModels,currentView,sourceHostFlags3E4);
            terrainCamera=prepareNativeTerrainCameraGpu(device.Get(),input,layout.Get(),positions.Get(),indices.Get(),vs.Get(),ps.Get(),coarseVs.Get(),coarsePs.Get(),indexCount);
            if(currentSeason) {
                seasonPalette=source_current_season_terrain::readPaletteBank(input.parent_path()/"current-season-resource-input");
                seasonPaletteBinding=source_current_season_terrain::prepareBinding(psBytes);
            }
            const auto shadowGpuInput=jointShadowInput/"gpu";
            shadowCameraGpu=prepareNativeShadowCameraGpu(device.Get(),shadowGpuParameters[currentShadowFamily],*shadowCameraProfile,jointShadow.catalog,jointShadow.allModels,shadowGpuInput,shadowRasterParameters);
            shadowGpuPlan=source_shadow_gpu::preparePlan(shadowCameraGpu.profile,shadowCameraFrame,jointShadow.catalog);
            NativeSourceDepthState shadowEntry{ds,stencilRef};
            executeNativeShadowCameraGpu(context.Get(),shadowEntry,shadowCameraGpu,shadowCameraFrame,shadowGpuPlan,jointShadow.catalog,jointShadow.allModels,currentShadowFamily);
            shadowMap=shadowCameraGpu.map;
            if(!runLoop)captureSourceShadowMap(device.Get(),context.Get(),shadowMap,shadowCameraGpu.profile.depthCompare,output);
            std::cout<<"source static catalogue instances="<<staticCatalog.instances.size()<<" primitives="<<staticCatalog.primitives.size()
                <<" templates="<<staticCatalog.templateCount<<" current="<<staticPlan.visibleInstances.size()<<'/'<<staticPlan.draws.size()<<'\n';
        } else {
            visibleModels=loadNativeModels(device.Get(),modelSource);
            if(sourceHdr)applySourceModelStencil(device.Get(),visibleModels,input.parent_path()/"stencil-visible-input");
        }
        auto& models=visibleModels;
        if(shadowLighting&&!sourceStaticModels)shadowMap=drawSourceCasterPopulation(device.Get(),context.Get(),shadowSource,modelSource,models,output);
    }
    // The real caller output survives the nested Main targets and is also
    // the output inherited by normal post109. Retain attachments/rects only.
    if(oceanNormal){auto raw=normalOutput.targets[0].Get();context->OMSetRenderTargets(1,&raw,nullptr);}
    else context->OMSetRenderTargets(0,nullptr,nullptr);
    context->RSSetViewports(1,&vp);context->RSSetScissorRects(1,&mainScissor);
    auto callerTargets=NativeMainTargetScope::retain(context.Get());
    std::vector<std::array<UINT,4>> terrainBatches;
    if(groupedTerrain) {
        std::ifstream batches(input/"batches.txt");std::array<UINT,4> row;
        while(batches>>row[0]>>row[1]>>row[2]>>row[3])terrainBatches.push_back(row);
    }
    NativeSourceDepthState sourceDepth{ds,stencilRef};UINT drawCount=0;
#ifdef SAN14_ARMY_GPU_PROOF
    ComPtr<ID3D11Query> armyGpuProof;
    D3D11_QUERY_DESC armyQueryDescription{D3D11_QUERY_PIPELINE_STATISTICS,0};
    check(device->CreateQuery(&armyQueryDescription,&armyGpuProof),"Army finite GPU proof query");
#endif
#ifdef SAN14_GROUND_GPU_PROOF
    std::array<ComPtr<ID3D11Query>,2> groundGpuProof;
    D3D11_QUERY_DESC groundQueryDescription{D3D11_QUERY_PIPELINE_STATISTICS,0};
    for(auto& query:groundGpuProof)check(device->CreateQuery(&groundQueryDescription,&query),"Ground detail finite GPU proof query");
#endif
    auto drawOpaqueGeometry=[&]() {
        std::array<ID3D11ShaderResourceView*,32> empty{};
        context->VSSetShaderResources(0,32,empty.data());context->PSSetShaderResources(0,32,empty.data());
        context->CSSetShaderResources(0,32,empty.data());
        std::array<ID3D11UnorderedAccessView*,8> noUav{};context->CSSetUnorderedAccessViews(0,8,noUav.data(),nullptr);
        context->OMSetRenderTargets(0,nullptr,nullptr);
        // Actual node33: depth/stencil only. Fresh color initialization stays
        // at allocation above; no invented per-request G-buffer color clear.
        context->ClearDepthStencilView(dsv.Get(),D3D11_CLEAR_DEPTH|D3D11_CLEAR_STENCIL,depthClear,0);
        if(sourceStaticModels) {
            visibleModels=updateSelectNativeModelContinuous(context.Get(),staticPlan,allStaticModels);
            submittedStaticPlan=updateNativeModelStencilContinuous(modelStencil,currentView,staticPlan,visibleModels);
            auto cityQueues=updateSelectNativeCityAreaPrimary(context.Get(),primaryModels,cityPlan);
            ordinaryModelQueues=mergeNativeCityPopulationWithField54(submittedStaticPlan,visibleModels,primaryModels.source,cityPlan,cityQueues);
        }
        if(sourceStaticModels){
            groundDetails.update(context.Get());
            auto drawGroundNode=[&](uint32_t node){
#ifdef SAN14_GROUND_GPU_PROOF
                context->Begin(groundGpuProof[node-54].Get());
#endif
                for(size_t record=0;record<viewProfile.records.size();++record){
                    const auto& sourceRecord=viewProfile.records[record];
                    source_view::Primitive primitive{0,0,ground_detail_source::effectiveExclusion(
                        {viewProfile.commonPresent!=0,viewProfile.commonFlags,groundDetails.currentStatistics().currentAux14,viewProfile.commonExclusion,groundFrustumType64}),1u<<record};
                    groundDetails.draw(context.Get(),node,{sourceRecord.mask,currentView.ordinaryCounts[record],sourceHostFlags3E4,
                        (sourceRecord.classification&1)==0,source_view::ordinary54(viewProfile,currentView,primitive,record,sourceHostFlags3E4)},rt.data(),dsv.Get(),vp);
                }
#ifdef SAN14_GROUND_GPU_PROOF
                context->End(groundGpuProof[node-54].Get());
#endif
            };
            if(sourceArmyVisuals) {
                armyQueues=updateSelectNativeArmyPopulation(context.Get(),armyModels,armyPlan);
#ifdef SAN14_ARMY_GPU_PROOF
                context->Begin(armyGpuProof.Get());
#endif
                drawNativeModels(context.Get(),armyQueues.ordinary49,rt.data(),dsv.Get(),vp);
                drawNativeModels(context.Get(),armyQueues.alpha50,rt.data(),dsv.Get(),vp);
                drawNativeModels(context.Get(),armyQueues.ordinary54,rt.data(),dsv.Get(),vp);
                drawNativeModels(context.Get(),armyQueues.alpha55,rt.data(),dsv.Get(),vp);
#ifdef SAN14_ARMY_GPU_PROOF
                context->End(armyGpuProof.Get());context->Flush();
                D3D11_QUERY_DATA_PIPELINE_STATISTICS statistics{};HRESULT result;
                while((result=context->GetData(armyGpuProof.Get(),&statistics,sizeof(statistics),0))==S_FALSE)Sleep(1);
                check(result,"Army finite GPU proof receipt");
                std::cout<<"ArmyWorld GPU vertex="<<statistics.VSInvocations<<" pixel="<<statistics.PSInvocations<<'\n';
#endif
                std::cout<<"source ArmyWorld actors="<<armyModels.source.actors.size()<<" visible="<<armyPlan.visibleActors.size()
                    <<" draws="<<(armyQueues.ordinary49.draws.size()+armyQueues.alpha50.draws.size()+
                        armyQueues.ordinary54.draws.size()+armyQueues.alpha55.draws.size())<<'\n';
#if defined(SAN14_ARMY_MODE_PROOF) && defined(SAN14_RENDER_SESSION)
                for(const auto& [id,mode]:host.armyWorld->unitModelModes){
                    unsigned body=0,siege=0,naval=0,flags=0;
                    for(const auto& [binding,ref]:armyModelPool->bindings)
                        if(binding.family==GraphicsModelFamily::Army&&binding.actor.unit==id){
                            body+=binding.actor.category<2;siege+=binding.actor.category>=4&&binding.actor.category<=6;
                            naval+=binding.actor.category>=7;flags+=binding.actor.category==3;
                        }
                    auto* banner=armyModelPool->find({id,2,0x7F0,0});
                    std::cout<<"ArmyMode unit="<<id<<" mode="<<mode<<" body="<<body<<" siege="<<siege<<" flags="<<flags
                        <<" banner="<<(banner?int(banner->index):-1)<<" bannerFrame="<<(banner?banner->clock.hFrame24:0)
                        <<" naval="<<naval<<'\n';
                }
                for(const auto& current:host.armyWorld->actors)if(current.key.category>=4){
                    const bool naval=current.key.descriptor==0x818;
                    const auto member=armyModelPool->member(GraphicsModelFamily::Army,current.key.unit,current.key.descriptor,current.key.instance);
                    if(member)if(auto* slot=armyModelPool->find(*member)){
                        std::cout<<(naval?"ArmyNaval unit=":"ArmySiege unit=")<<current.key.unit<<" category="<<member->category<<" requested="<<current.key.category
                            <<" ordinal="<<current.key.instance<<" slot="<<slot->index
                            <<" action="<<slot->clock.selectedRawAction24<<" hframe="<<slot->clock.hFrame24
                            <<" phase="<<slot->clock.returnedPhase<<" root="<<slot->presented.world.translation.x<<','
                            <<slot->presented.world.translation.y<<','<<slot->presented.world.translation.z;
                        if(naval){const auto& height=armyUnitHeights.at(current.key.unit);
                            const auto& scale=slot->presented.world.scale;
                            std::cout<<" unitY="<<height.y14<<" dirty="<<height.dirty30
                                <<" scale="<<scale.x<<','<<scale.y<<','<<scale.z<<','<<scale.w;}
                        std::cout<<" cb=";
                        for(const auto& [draw,model]:slot->models){
                            D3D11_BUFFER_DESC v{},p{};model.vsConstants->GetDesc(&v);model.psConstants->GetDesc(&p);
                            std::cout<<draw<<':'<<v.ByteWidth<<':'<<p.ByteWidth;
                            if(naval)std::cout<<':'<<uintptr_t(model.vsConstants.Get())<<':'<<uintptr_t(model.psConstants.Get());
                            std::cout<<',';
                        }
                        std::cout<<'\n';
                    }
                }
#endif
            }
            if(sourceAccessoryVisuals) {
#ifdef SAN14_RENDER_SESSION
                drawNativeSharedAccessories(context.Get(),*armyModelPool,sharedAreaAccessories,areaAccessories,rt.data(),dsv.Get(),vp);
                drawNativeSharedAccessories(context.Get(),*armyModelPool,sharedCityAccessories,cityAccessories,rt.data(),dsv.Get(),vp);
#else
                drawNativeCityAccessoryGpu(context.Get(),cityAccessories,rt.data(),dsv.Get(),vp);
#endif
            }
            drawNativeModels(context.Get(),ordinaryModelQueues.ordinary54,rt.data(),dsv.Get(),vp);
            drawGroundNode(54);
            drawNativeModels(context.Get(),ordinaryModelQueues.alpha55,rt.data(),dsv.Get(),vp);
            drawGroundNode(55);
            const auto& detail=groundDetails.currentStatistics();
            std::cout<<"source GroundDetails instances="<<detail.selectedInstances<<" prepared="<<detail.preparedPrimitives
                <<" draws="<<detail.nonemptyDraws<<" opaque="<<detail.opaqueDraws<<" alpha="<<detail.alphaDraws
                <<" appFlags="<<std::hex<<detail.currentAppFlags<<" aux14="<<detail.currentAux14<<std::dec<<" wind="<<detail.windUpdated<<'\n';
        }else if(hasVisibleModels)drawNativeModels(context.Get(),visibleModels,rt.data(),dsv.Get(),vp);
    if(sourceStaticModels) {
        if(currentSeason)source_current_season_terrain::patchNativeCurrent(
            seasonPalette,seasonPaletteBinding,currentSeason->season,terrainCamera);
        updateNativeTerrainCameraGpu(context.Get(),terrainCamera,staticCamera);
        NativeTerrainCameraDrawBindings binding;
        binding.colors=rt;binding.depthTarget=dsv.Get();binding.viewport=vp;
        binding.raster=raster.Get();binding.depthState=depthState.Get();binding.stencilReference=stencilRef;binding.blend=blendState.Get();
        binding.vertexViews=terrainVertexViews;binding.pixelViews=terrainPixelViews;
        binding.vertexSamplers=terrainVertexSamplers;binding.pixelSamplers=terrainPixelSamplers;
        drawCount=drawNativeTerrainCameraGpu(context.Get(),terrainCamera,binding);
    }else {
    context->IASetInputLayout(layout.Get());context->IASetVertexBuffers(0,2,vb,strides,vbOffsets);
    context->IASetIndexBuffer(indices.Get(),DXGI_FORMAT_R16_UINT,0);
    context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    context->VSSetShaderResources(0,16,terrainVertexViews.data());context->PSSetShaderResources(0,16,terrainPixelViews.data());
    context->VSSetSamplers(0,16,terrainVertexSamplers.data());context->PSSetSamplers(0,16,terrainPixelSamplers.data());
    context->RSSetState(raster.Get());context->OMSetDepthStencilState(depthState.Get(),stencilRef);
    context->OMSetBlendState(blendState.Get(),nullptr,~0u);
    context->RSSetViewports(1,&vp);context->OMSetRenderTargets(5,rt.data(),dsv.Get());
    drawCount=0;
    if(groupedTerrain){
        for(const auto& batch:terrainBatches){
            const UINT group=batch[0],first=batch[1],count=batch[2],coarse=batch[3];
            context->VSSetShader(coarse?coarseVs.Get():vs.Get(),nullptr,0);
            context->PSSetShader(coarse?coarsePs.Get():ps.Get(),nullptr,0);
            auto vcb=coarse?coarseVsCb.Get():vsCb.Get();auto pcb=coarse?coarsePsCb.Get():psCb.Get();
            context->VSSetConstantBuffers(0,1,&vcb);context->PSSetConstantBuffers(0,1,&pcb);
            context->DrawIndexedInstanced(indexCount,count,0,0,first);++drawCount;
            std::cout<<"terrain group="<<group<<" first="<<first<<" count="<<count<<" coarse="<<coarse<<'\n';
        }
    }else{
        context->VSSetShader(vs.Get(),nullptr,0);context->PSSetShader(ps.Get(),nullptr,0);
        auto vcb=vsCb.Get(),pcb=psCb.Get();context->VSSetConstantBuffers(0,1,&vcb);context->PSSetConstantBuffers(0,1,&pcb);
        context->DrawIndexedInstanced(indexCount,instanceCount,0,0,0);++drawCount;
    }
        context->OMSetRenderTargets(0,nullptr,nullptr);
    }
        sourceDepth.bind(context.Get(),depthState.Get(),ds,stencilRef);
    };
    drawOpaqueGeometry();
    const auto coldGroundStatistics=groundDetails.currentStatistics();
    auto mainDepth=createNativeDepthMirror(device.Get(),depth.Get(),shaderFormats[5]);
    updateNativeDepthMirror(context.Get(),mainDepth,sourceHdr?"59 after geometry":"diagnostic geometry completion");
    if(sourceStaticModels){
        territoryTargets.storage={targets[0].Get(),targets[2].Get(),targets[1].Get(),targets[3].Get()};
        territoryTargets.output={rt[0],rt[2],rt[1],rt[3]};
        territoryTargets.sampledFormat={shaderFormats[0],shaderFormats[2],shaderFormats[1],shaderFormats[3]};
        territoryTargets.depth=mainDepth.depth.Get();
        territoryTargets.primaryDSV=dsv.Get();
        territory=source_territory_gpu::prepare(device.Get(),input.parent_path()/"territory-visual-input",territoryTargets,sourceDepth.description,rs);
        territory.profile.zones=territoryZoneOwner.zones;
        context->UpdateSubresource(territory.zones.Get(),0,nullptr,territoryZoneOwner.zones.data(),0,0);
#ifdef SAN14_RENDER_SESSION
        ownershipPalette.emplace(territory.profile);
        publishCurrentWorldOwnership();
#endif
    }
    auto drawCurrentTerritory=[&](UINT node,UINT providerDrawFlags){
        if(!sourceStaticModels)return;
        territoryControls.styleSelector=territoryDisplayStyle;
        territoryControls.screenMapMask=source_zone::displayMask(territoryDisplayEnabled,territoryDisplayStyle,
            territoryZoneCurrent.membership!=nullptr,!territoryZoneCurrent.referenceLists[0].empty()||!territoryZoneCurrent.referenceLists[1].empty());
        const unsigned side=territoryReceiptSide;
        auto capture=[&](ComPtr<ID3D11Texture2D>& copy,ID3D11Texture2D* original){
#ifndef SAN14_WATER_STAGE_PROOF
            if(runLoop)return;
#endif
            D3D11_TEXTURE2D_DESC descriptor{};original->GetDesc(&descriptor);
            descriptor.BindFlags=0;descriptor.MiscFlags=0;
            if(!copy)check(device->CreateTexture2D(&descriptor,nullptr,&copy),"finite territory stage receipt");
            context->CopyResource(copy.Get(),original);
        };
        capture(territoryDepthReceipts[side*2+(node==122)],mainDepth.sampled.Get());
        if(node==122)capture(territoryWaterColorReceipts[side*2],targets[0].Get());
#ifdef SAN14_WATER_STAGE_PROOF
        if(node==122){
            captureWaterProof("rr0-before122",targets[3].Get(),formats[3]);
            captureWaterProof("exposure-before122",mapExposure.texture.Get(),DXGI_FORMAT_R32G32B32A32_FLOAT);
        }
#endif
        source_territory::Selection selection{staticCameraRuntime.zoom.band,staticCameraRuntime.zoom.fraction,staticCameraRuntime.pose.distance,territoryControls};
        source_territory_gpu::update(context.Get(),territory,selection,staticCamera.prefix,heldMainB1C);
        source_territory_gpu::execute(context.Get(),territory,territoryTargets,mapExposure.view.Get(),providerDrawFlags,source_territory::initializedStencilMask,sourceDepth);
        updateNativeDepthMirror(context.Get(),mainDepth,node==122?"122 PB Pop40000007":"60 PB Pop40000007");
        if(node==122)capture(territoryWaterColorReceipts[side*2+1],targets[0].Get());
#ifdef SAN14_WATER_STAGE_PROOF
        if(node==122){
            captureWaterProof("rr0-after122",targets[3].Get(),formats[3]);
            captureWaterProof("rs-after122",targets[2].Get(),formats[2]);
            captureWaterProof("ns-after122",targets[1].Get(),formats[1]);
        }
#endif
        ++territoryDraws;
        std::cout<<"source territory node"<<node<<" drawFlags="<<providerDrawFlags
            <<" stencil="<<source_territory::drawStencilReference(providerDrawFlags,source_territory::initializedStencilMask)
            <<" latestD1="<<(node==122?117:59)<<'\n';
    };
    drawCurrentTerritory(60,1);
    if(sourceHdr)publishThinSurfaceFlags(60);
    if(sourceHdr)callerTargets.restore(context.Get());
    NativeRefractionDepth refractionDepth;
    NativeRefractionColors refractionColors;
    NativeNormalPost normalPost;
    std::vector<NativeNormalPostPass> normalPostUnion;
    NativeNormalPostContinuous postContinuous;
    NativeNormalPostCamera postCamera;NativeNormalPostCameraProjection postCameraProjection;
    source_current_season_resources::Bank seasonLuts;
    int loadedLutSeason=-1;
    source_current_season_post::CallerSources currentPostCallers;
    if(currentSeason)currentPostCallers=source_current_season_post::readCallerSources(
        input.parent_path()/"current-season-resource-input"/"post-caller"/"caller-source.bin");
    UINT64 actualStagingMaps=0,failedStagingMaps=0;
    const auto normalThirdSource=input.parent_path()/"normal-post-third-input";
    const float thirdCommonClock=oceanFftContinuation&&!runLoop?sourceCommonClock(normalThirdSource):0;
    auto executeCurrentPost=[&](float currentClock,const fs::path& directory) {
        if(currentSeason) {
            if(loadedLutSeason!=int(currentSeason->season)) {
                const bool loaded=std::any_of(seasonLuts.catalog.luts.begin(),seasonLuts.catalog.luts.end(),
                    [&](const auto& row){return row.season==currentSeason->season;});
                if(!loaded)source_current_season_resources::loadResources(device.Get(),seasonLuts,
                    source_current_season_resources::readCatalog(input.parent_path()/"current-season-resource-input"));
                loadedLutSeason=int(currentSeason->season);
            }
            source_current_season_resources::installCurrent(seasonLuts,*currentSeason,normalPost);
            const auto caller=source_current_season_post::prepareCaller(currentPostCallers,*currentSeason,
                postCamera.selectedLightPresent!=0);
            source_current_season_post::publishMode6SelectedSun(*currentSeason,caller.selection,postCamera);
            const auto values=source_current_season_post::prepareSimpleCurrent(*currentSeason,
                caller.adaptationLocked,caller.lockedAdaptationBits,caller.autoExposure);
            source_current_season_post::publishSimpleCurrent(values,postContinuous);
            postCameraProjection=source_current_season_post::publishProjectionBeforeBegin(
                *currentSeason,postCamera,postContinuous);
        } else if(sourceStaticModels)postCameraProjection=updateNativeNormalPostCameraBeforeBegin(postCamera,staticCamera.prefix,postContinuous);
        auto plan=beginNativeNormalPostContinuousFromCommon(postContinuous,currentClock);
        auto passes=selectNativeNormalPostContinuousPasses(plan,normalPostUnion);
        uploadNativeNormalPostContinuousConstants(context.Get(),postContinuous,plan,passes);
        NativeNormalPostStagingMap mapped;bool readStaging=false;
        auto before57=[&]() {
            if(plan.needStagingRead) {
                mapped.side=plan.selectedStagingSide;D3D11_MAPPED_SUBRESOURCE bytes{};
                auto buffer=normalPost.spriteStaging.at(mapped.side).Get();
                const HRESULT result=context->Map(buffer,0,D3D11_MAP_READ,0,&bytes);
                mapped.returnedNull=FAILED(result);
                ++actualStagingMaps;if(mapped.returnedNull)++failedStagingMaps;
                if(!mapped.returnedNull){std::memcpy(mapped.bytes.data(),bytes.pData,mapped.bytes.size());context->Unmap(buffer,0);}
                readStaging=true;
            }
            applyNativeNormalPostBefore57(postContinuous,plan,readStaging?&mapped:nullptr,passes);
        };
        auto result=executeNormalPost(device.Get(),context.Get(),sourceDepth,normalPost,passes,directory,postCaptureDiagnostics,before57);
        endNormalPost(normalPost);
        finishEndReturnedNativeNormalPostContinuous(postContinuous,plan);
        if(sourceStaticModels){
            const auto parent=NativeMainTargetScope::retain(context.Get());
            ComPtr<ID3D11RasterizerState> priorRaster;context->RSGetState(&priorRaster);
            publishNativeMapExposureAfterApply(context.Get(),mapExposure,normalPost.exposure.views[0].Get());
            ++mapExposurePublicationDraws;
            if(!runLoop){
                D3D11_TEXTURE2D_DESC descriptor{};mapExposure.texture->GetDesc(&descriptor);descriptor.BindFlags=0;
                ComPtr<ID3D11Texture2D> copy;check(device->CreateTexture2D(&descriptor,nullptr,&copy),"finite post-Apply exposure receipt");
                context->OMSetRenderTargets(0,nullptr,nullptr);context->CopyResource(copy.Get(),mapExposure.texture.Get());
                mapExposureAfterApplyReceipts.push_back(copy);
            }
            parent.restore(context.Get());context->RSSetState(priorRaster.Get());
        }
        std::cout<<"source continuous193 clock="<<currentClock<<" mask="<<std::hex<<plan.dofMask<<std::dec
            <<" count="<<plan.countAtPrepare<<"->"<<postContinuous.count10<<" history="<<plan.history16
            <<" staging="<<plan.selectedStagingSide<<" actualMap="<<readStaging<<" rearClear="<<plan.rearClear
            <<" foreClear="<<plan.foreClear<<'\n';
        if(postCaptureDiagnostics) {
            std::ofstream receipt(directory/"normal-post-continuous-result.json");receipt<<std::setprecision(std::numeric_limits<float>::max_digits10);
            receipt<<"{\"scope\":\"Original fixed-Main continuous Prepare/Init/Before/Finish/End; actual GPU staging\",\"common254\":"<<currentClock
                <<",\"actualDelta\":"<<plan.actualDelta<<",\"dofMask\":"<<plan.dofMask<<",\"countAtPrepare\":"<<plan.countAtPrepare
                <<",\"countAfterReturn\":"<<postContinuous.count10<<",\"history16\":"<<plan.history16<<",\"stagingSide\":"<<plan.selectedStagingSide
                <<",\"actualMap\":"<<(readStaging?"true":"false")<<",\"mapReturnedNull\":"<<(mapped.returnedNull?"true":"false")
                <<",\"rearClear\":"<<(plan.rearClear?"true":"false")<<",\"foreClear\":"<<(plan.foreClear?"true":"false")<<",\"mappedWords\":[";
            if(readStaging&&!mapped.returnedNull)for(UINT n=0;n<8;++n){UINT word;std::memcpy(&word,mapped.bytes.data()+n*4,4);if(n)receipt<<',';receipt<<word;}
            receipt<<"],\"orderedPasses\":[";for(UINT n=0;n<plan.orderedPasses.size();++n){if(n)receipt<<',';receipt<<'"'<<plan.orderedPasses[n]<<'"';}receipt<<"]}\n";
        }
        return result;
    };
    ComPtr<ID3D11Texture2D> postInputSnapshot,cloudInputSnapshot;
    NativeSourceDepthState cloudEntryLogical{};
    ComPtr<ID3D11DepthStencilState> cloudEntryDepth;
    ComPtr<ID3D11BlendState> cloudEntryBlend;
    ComPtr<ID3D11RasterizerState> cloudEntryRaster;
    UINT cloudEntryReference=0,cloudEntrySampleMask=~0u;float cloudEntryFactor[4]{};
    fs::path copy97Source=input.parent_path()/"refraction-source-input"/"copy97";
    if(!sourceHdr&&(fullMode||shadowLighting)&&fs::exists(copy97Source/"target.txt"))
        refractionDepth=produceRefractionDepth(device.Get(),context.Get(),sourceDepth,copy97Source,mainDepth.depth.Get(),width,height);
    bool rs68Executed;
    NativeSubShadow subShadow;
    NativeSourceShadowReceiver receiver68,receiver119;
    const fs::path receiver68Source=sourceStaticModels
        ?input.parent_path()/"normal-pattern-receiver-input"/"initial"/"rs68":shadowSource/"rs68";
    const fs::path receiver119Source=sourceStaticModels
        ?input.parent_path()/"normal-pattern-receiver-input"/"initial"/"rs119":input.parent_path()/"post-water-rs-input"/"rs119";
    NativeShadowCameraReceiver shadowReceiver68Camera,shadowReceiver119Camera;
    auto updateCurrentShadowReceiver=[&](NativeSourceShadowReceiver& receiver,NativeShadowCameraReceiver& owned) {
        if(currentSeason)source_current_season_shadow::publishNormalPs5ReceiverBase(*currentSeason,currentShadowParameters,owned.current);
        updateNativeShadowCameraReceiver(context.Get(),receiver,owned,shadowCameraGpu,*shadowCameraProfile,staticCamera,shadowCameraFrame);
    };
    NativeRs69Fallback fallback69;
    NativeSourceHdrPasses hdr78,hdr123,hdr166;
    auto updateCurrentHdrLighting=[&](NativeSourceHdrPasses& passes) {
        if(currentSeason)updateNativeCurrentSeasonHdr(context.Get(),*currentSeason,passes);
        else updateNativeWeatherFogHdr(context.Get(),weatherConsumers,weatherCameraFrame,passes);
    };
    auto prepareCurrentEffects=[&]() {
        if(currentSeason)effectCamera.profile.tangent=currentSeason->atmosphere[196/4];
        effectCameraFrame=updateNativeEffectCameraContinuous(context.Get(),effectCamera,effects,staticCamera.prefix,
            [&](unsigned index,const source_effect_camera::Candidate&) {
                // The retained imported cohort already passed its original
                // admission. This seam adds Field registration/group masks.
                return fieldEffects->preserveImportedAdmission(currentFieldEffects,index);
            });
        if(currentSeason)updateNativeCurrentSeasonEffectFog(context.Get(),weatherConsumers,*currentSeason,effectCamera,effects);
        else updateNativeWeatherFogEffects(context.Get(),weatherConsumers,weatherCameraFrame,effectCamera,effects);
    };
    auto appendAccessoryHdr=[&](NativeSourceHdrPasses& destination,const char* list) {
        if(!sourceAccessoryVisuals)return;
        auto source=prepareSourceHdrPasses(device.Get(),input.parent_path()/"city-accessory-next-input/hdr/initial",list,
            litColor.Get(),targets,shaderFormats,ggx.Get(),true);
        // The appended recipes borrow the destination's retained views. The
        // temporary catalogue owns distinct SRVs for the same G-buffer images.
        for(auto& pass:source.passes)for(auto& view:pass.fixedPixelViews) {
            for(unsigned i=0;i<source.gbuffer.size();++i)
                if(view==source.gbuffer[i].Get())view=destination.gbuffer[i].Get();
            if(view==source.ggx.Get())view=destination.ggx.Get();
        }
        destination.passes.insert(destination.passes.begin(),std::make_move_iterator(source.passes.begin()),std::make_move_iterator(source.passes.end()));
    };
    NativePreparedRefractionDepth preparedDepth97;
    NativePreparedRefractionScene97 preparedScene97;
    NativePreparedRefractionFilters137 preparedFilters137;
    NativeMainSky mainSky;NativeMapFog mapFog;
    std::optional<source_current_height_fog::Profile> currentHeightFog;
    auto publishCurrentHeightFog=[&]() {
        if(!currentSeason)return;
        if(!currentHeightFog)currentHeightFog=source_current_height_fog::readProfile(
            input.parent_path()/"current-height-fog-input"/"source-profile.bin");
        publishNativeCurrentHeightFog(*currentHeightFog,*currentSeason,mapFog);
    };
    OceanNativeTextures oceanTextures;NativeOcean116 ocean116;
    NativeWaterCameraGpu waterCamera;NativeWaterRefractionGpu waterRefraction123,waterRefraction166;
    std::array<float,8> completedWaveBounds{};
    const auto waterCameraInput=input.parent_path()/"water-camera-continuous-input";
    NativeOceanFftSchedule oceanFft;
    ComPtr<ID3D11RenderTargetView> indirectTarget;
    NativePrimaryForwardSource primaryForwardSource;
    NativePrimaryForwardGpu primaryForwardGpu;
    NativePrimaryForwardFrame primaryForwardFrame;
    bool primaryForwardReady=false;
    auto drawCurrentPrimaryForward=[&]() {
        if(!sourceStaticModels)return;
        if(!primaryForwardReady) {
#ifdef SAN14_RENDER_SESSION
            const auto directory=input.parent_path()/"primary-forward-current-bank-input";
            primaryForwardSource=prepareNativePrimaryForwardCurrentBankSource(directory);
            primaryForwardGpu=prepareNativePrimaryForwardCurrentBankGpu(device.Get(),context.Get(),directory);
#else
            const auto directory=input.parent_path()/"primary-tail-continuous-input";
            primaryForwardSource=prepareNativePrimaryForwardSource(directory);
            primaryForwardGpu=prepareNativePrimaryForwardGpu(device.Get(),context.Get(),directory);
#endif
            if(!runLoop)captureNativePrimaryForwardClusterHeader(device.Get(),context.Get(),primaryForwardGpu,output,65280);
            primaryForwardReady=true;
        }
        auto pass=std::find_if(hdr166.passes.begin(),hdr166.passes.end(),[](auto& p){return p.identity=="node166-inner02";});
        if(pass==hdr166.passes.end())throw std::runtime_error("Forward actual current inner02 producer absent");
        auto globals=std::find_if(pass->constants.begin(),pass->constants.end(),[](auto& cb){return !cb.vertex&&cb.slot==0;});
        if(globals==pass->constants.end())throw std::runtime_error("Forward actual current HDR globals absent");
        primaryForwardFrame=prepareNativePrimaryForwardCurrent(primaryForwardSource,primaryModels.source,cityPlan,
            mainDirectPrefix.data(),globals->bytes.data(),globals->bytes.size(),
            shadowReceiver68Camera.current.data(),shadowReceiver68Camera.current.size());
        if(primaryForwardPreparationOnly)return; //Allocation observation, not a source-pixel render.
        executeNativePrimaryForwardGpu(context.Get(),sourceDepth,primaryForwardGpu,primaryForwardFrame,primaryModels.currentModels,
            pass->target.Get(),dsv.Get(),vp,shadowCameraGpu.map.view.Get(),directionalDiffuse.Get(),hdr166.ggx.Get(),
            ambient.Get(),fogAcceleration.view.Get());
        std::cout<<"source node187 current primary forward draws="<<primaryForwardGpu.draws<<'\n';
    };
    auto isolateCurrentShadowVisibility=[&]() {
        if(isolateShadowVisibility){const float value[]{0,0,0,1};context->ClearRenderTargetView(indirectTarget.Get(),value);}
    };
    if(sourceHdr) {
        auto abTarget=rt[0];context->OMSetRenderTargets(1,&abTarget,dsv.Get()); //62
        subShadow=prepareNativeSubShadow63(device.Get(),input.parent_path()/"sub-shadow-input",sourceDepth,rs,blend,
            mainDepth,targets[1].Get(),shaderFormats[1],targets[0].Get(),dsv.Get());
        executeNativeSubShadow63(context.Get(),sourceDepth,subShadow,vp);
        D3D11_RENDER_TARGET_VIEW_DESC rsDesc{};rsDesc.Format=formats[2];rsDesc.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
        rsDesc.Texture2DArray.FirstArraySlice=3;rsDesc.Texture2DArray.ArraySize=1;
        check(device->CreateRenderTargetView(targets[2].Get(),&rsDesc,&indirectTarget),"Main indirect RS3 target");
    }
    if(sourceHdr)callerTargets.restore(context.Get()); //64
    if(sourceHdr){auto raw=indirectTarget.Get();context->OMSetRenderTargets(1,&raw,dsv.Get());} //67
    if(shadowLighting){
        if(!directionalDiffuse){auto diffuseTexture=loadTexture(device.Get(),input/"rs68"/"directional-diffuse.tex");
            check(device->CreateShaderResourceView(diffuseTexture.Get(),nullptr,&directionalDiffuse),"held original directional diffuse");}
        receiver68=prepareSourceShadowReceiver(device.Get(),receiver68Source,shadowMap.view.Get(),directionalDiffuse.Get(),targets[2].Get(),formats[2],width,height);
        if(sourceStaticModels){
            shadowReceiver68Camera=prepareNativeShadowCameraReceiver(device.Get(),receiver68,receiver68Source);
            updateCurrentShadowReceiver(receiver68,shadowReceiver68Camera);
        }
        executeSourceShadowReceiver(device.Get(),context.Get(),sourceDepth,receiver68,mainDepth.depth.Get(),dsv.Get());
        rs68Executed=true;
    }else rs68Executed=drawRs68(device.Get(),context.Get(),input,targets[2].Get(),depth.Get(),dsv.Get(),formats[2],shaderFormats[5],width,height);
    fallback69=prepareRs69Fallback(device.Get(),input,targets[2].Get(),formats[2],width,height);
    bool rs69Executed=executeRs69Fallback(device.Get(),context.Get(),sourceDepth,fallback69,dsv.Get());
    isolateCurrentShadowVisibility();
    if(sourceHdr)callerTargets.restore(context.Get()); //70
    DXGI_FORMAT lightingShaderFormat=DXGI_FORMAT_UNKNOWN,lightingTargetFormat=DXGI_FORMAT_UNKNOWN;
    if(wantsLighting){
        if(sourceHdr) {
            auto earlySource=input.parent_path()/"early-hdr-input";
            D3D11_RENDER_TARGET_VIEW_DESC hdrDesc{};hdrDesc.Format=DXGI_FORMAT_R11G11B10_FLOAT;hdrDesc.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
            ComPtr<ID3D11RenderTargetView> hdrTarget;check(device->CreateRenderTargetView(litColor.Get(),&hdrDesc,&hdrTarget),"Main73 retained HDR target");
            auto mainTargets=NativeMainTargetScope::main73(hdrTarget.Get(),dsv.Get(),vp,mainScissor);
            mainTargets.restore(context.Get());
            hdr78=prepareSourceHdrPasses(device.Get(),earlySource,"passes.txt",litColor.Get(),targets,shaderFormats,ggx.Get(),sourceStaticModels);
            appendAccessoryHdr(hdr78,"passes.txt");
            if(sourceStaticModels){
                updateNativeDeferredCameraGpu(context.Get(),deferredCameraProfile,deferredCameraFields,hdr78);
                updateCurrentHdrLighting(hdr78);
            }
            executeSourceHdrPasses(device.Get(),context.Get(),sourceDepth,hdr78,mainDepth.depth.Get(),dsv.Get(),ambient.Get(),nullptr,nullptr,
                nullptr,nullptr,initialCommonClock,currentView.flags19AC,currentView.ocean19B0);
            mainTargets.restore(context.Get());
            lightingShaderFormat=lightingTargetFormat=DXGI_FORMAT_R11G11B10_FLOAT;
            if(oceanLighting) {
                preparedScene97=prepareNativeRefractionScene97(device.Get(),input.parent_path()/"refraction-source-input"/"copy-scene97",litColor.Get(),lightingShaderFormat);
                refractionColors.scene97=executeNativeRefractionScene97(preparedScene97,context.Get(),sourceDepth);
            }
            preparedDepth97=prepareNativeRefractionDepth(device.Get(),copy97Source,mainDepth.depth.Get(),width,height);
            refractionDepth=executeNativeRefractionDepth(preparedDepth97,context.Get(),sourceDepth);
            mainTargets.restore(context.Get());
            if(oceanEnabled) {
                oceanTextures=loadOceanTextures(device.Get(),input.parent_path()/"ocean-texture-input");
                ocean116=prepareNativeOcean116(device.Get(),input.parent_path()/"water-input",oceanTextures,
                    refractionDepth.view.Get(),copy97Source/"target-sampler-desc.bin",targets,rt,formats[3],dsv.Get(),width,height,
                    input.parent_path()/"ocean-prepare-input"/"ocean-clock.txt",initialCommonClock);
                if(sourceStaticModels) {
                    waterCamera=prepareNativeWaterCameraGpu(device.Get(),waterCameraInput/"geometry-profile.bin",ocean116);
                    const auto bounds=load(waterCameraInput/"initial"/"runtime-geometry"/"ocean-current-phase-wave-bounds.bin");
                    if(bounds.size()!=sizeof(completedWaveBounds))throw std::runtime_error("Completed Ocean wave AABB source extent differs");
                    std::memcpy(completedWaveBounds.data(),bounds.data(),bounds.size());
                    updateNativeWaterCameraGpu(waterCamera,context.Get(),ocean116,staticCamera.prefix,completedWaveBounds);
                }
                if(oceanFftContinuation)oceanFft=prepareNativeOceanFftSchedule(input.parent_path()/"ocean-fft-input");
                executeNativeOcean116(device.Get(),context.Get(),sourceDepth,ocean116,initialCommonClock);
                if(oceanNormal)traceNativeDepthState(context.Get(),"116 Ocean exit");
                updateNativeDepthMirror(context.Get(),mainDepth,"117 after Ocean");
                mainTargets.restore(context.Get());
            }
            if(oceanLighting) {
                // 118 binds the existing RS3 and water-updated Main D1 without
                // clearing. 119 selects the Ocean group (process50). The same
                // explicitly empty SDF owner used at69 preserves RG at120;
                // 121 pops this target scope without copying depth.
                auto rsTarget=indirectTarget.Get();context->OMSetRenderTargets(1,&rsTarget,dsv.Get()); //118
                receiver119=prepareSourceShadowReceiver(device.Get(),receiver119Source,shadowMap.view.Get(),directionalDiffuse.Get(),targets[2].Get(),formats[2],width,height);
                if(sourceStaticModels){
                    shadowReceiver119Camera=prepareNativeShadowCameraReceiver(device.Get(),receiver119,receiver119Source);
                    updateCurrentShadowReceiver(receiver119,shadowReceiver119Camera);
                }
                executeSourceShadowReceiver(device.Get(),context.Get(),sourceDepth,receiver119,mainDepth.depth.Get(),dsv.Get());
                isolateCurrentShadowVisibility();
                mainTargets.restore(context.Get()); //121
                drawCurrentTerritory(122,2);
                publishThinSurfaceFlags(122);
                hdr123=prepareSourceHdrPasses(device.Get(),earlySource,"ocean-passes.txt",litColor.Get(),targets,shaderFormats,ggx.Get(),sourceStaticModels);
                if(sourceStaticModels){
                    updateNativeDeferredCameraGpu(context.Get(),deferredCameraProfile,deferredCameraFields,hdr123);
                    waterRefraction123=prepareNativeWaterRefractionGpu(waterCameraInput/"receiver-refraction-writes.txt",hdr123);
                    updateNativeWaterRefractionGpu(context.Get(),waterRefraction123,waterCamera.refraction,hdr123);
                    updateCurrentHdrLighting(hdr123);
                }
                executeSourceHdrPasses(device.Get(),context.Get(),sourceDepth,hdr123,mainDepth.depth.Get(),dsv.Get(),ambient.Get(),refractionDepth.view.Get(),refractionColors.scene97.view.Get(),
                    nullptr,nullptr,initialCommonClock,currentView.flags19AC,currentView.ocean19B0);
#ifdef SAN14_WATER_STAGE_PROOF
                captureWaterProof("hdr123",litColor.Get(),DXGI_FORMAT_R11G11B10_FLOAT);
                captureWaterProof("scene97",refractionColors.scene97.texture.Get(),DXGI_FORMAT_R11G11B10_FLOAT);
#endif
                mainTargets.restore(context.Get());
                mainSky=prepareNativeMainSky(device.Get(),input.parent_path()/"main-sky-input",litColor.Get(),dsv.Get(),width,height);
                const auto fogSource=input.parent_path()/(sourceStaticModels?"ssfog-population-input":"atmosphere-input");
                mapFog=prepareNativeMapFog(device.Get(),fogSource,litColor.Get(),mainDepth.depth.Get(),dsv.Get(),width,height);
                if(sourceStaticModels){
                    atmosphereCamera=prepareNativeAtmosphereCamera(device.Get(),input.parent_path()/"atmosphere-camera-continuous-input"/"initial",mainSky,mapFog);
                    if(currentSeason){publishNativeCurrentSeasonAtmosphere(atmosphereCamera,mainCloud,*currentSeason,&currentCloudLights);
                        publishNativeCurrentMainSkyMoonFace(atmosphereCamera,*currentSeason);}
                    else {
                        publishNativeAtmosphereCurrentDirectPrefix(atmosphereCamera,mainDirectPrefix);
                        publishNativeAtmosphereWeather(atmosphereCamera,mainCloud,weatherCameraFrame,globalAtmosphereRgb);
                    }
                    atmosphereCameraReady=true;
                    updateNativeAtmosphereCamera(context.Get(),atmosphereCamera,staticCamera.prefix,mainSky,mainCloud,mapFog);
                }
                executeNativeMainSky(device.Get(),context.Get(),sourceDepth,mainSky);
                mainTargets.restore(context.Get());
                if(historyContinuation) {
                    cloudEntryLogical=sourceDepth;context->OMGetDepthStencilState(&cloudEntryDepth,&cloudEntryReference);
                    context->OMGetBlendState(&cloudEntryBlend,cloudEntryFactor,&cloudEntrySampleMask);context->RSGetState(&cloudEntryRaster);
                    D3D11_TEXTURE2D_DESC d{};litColor->GetDesc(&d);
                    check(device->CreateTexture2D(&d,nullptr,&cloudInputSnapshot),"isolated cloud incoming HDR snapshot");
                    context->OMSetRenderTargets(0,nullptr,nullptr);context->CopyResource(cloudInputSnapshot.Get(),litColor.Get());mainTargets.restore(context.Get());
                }
                executeNativeMainCloud(device.Get(),context.Get(),sourceDepth,mainCloud,dsv.Get(),initialCommonClock,UINT(actualHostQ7F8));
                mainTargets.restore(context.Get());
                publishCurrentHeightFog();
                executeNativeMapFog(device.Get(),context.Get(),sourceDepth,mapFog,initialCommonClock,
                    sourceStaticModels?std::optional<unsigned>(currentView.flags19AC):std::nullopt);
                if(oceanNormal)traceNativeDepthState(context.Get(),"125 fog exit");
                mainTargets.restore(context.Get());
                preparedFilters137=prepareNativeRefractionFilters137(device.Get(),input.parent_path()/"refraction-source-input"/"filter137",litColor.Get(),lightingShaderFormat,mainDepth.depth.Get());
                executeNativeRefractionFilters137(preparedFilters137,context.Get(),sourceDepth,refractionColors);
                if(oceanNormal)traceNativeDepthState(context.Get(),"137 filter exit");
                mainTargets.restore(context.Get());
                //143/156 are actual six-MRT pushes even when this finite
                //cohort still lacks the specialized Effect/Rain draw bodies.
                //Whole array SRVs overlap every slice RTV; clear the source
                //helper inputs before returning those arrays to output use.
                std::array<ID3D11ShaderResourceView*,32> noInputs{};
                context->VSSetShaderResources(0,UINT(noInputs.size()),noInputs.data());
                context->PSSetShaderResources(0,UINT(noInputs.size()),noInputs.data());
                //138 is B616's depth-only row. Its NeverInternalCopy1
                //callback makes142's requested mirror update a no-op.
                context->OMSetRenderTargets(0,nullptr,refractionDepth.depth.Get()); //138
                if(oceanEffects)traceNativeTargetBinding(context.Get(),"138",0,nullptr,refractionDepth.depth.Get());
                if(oceanEffects)applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,141);
                mainTargets.restore(context.Get()); //142, B616 has no mirror
                sixGbufferTargets.restore(context.Get()); //143
                std::array<ID3D11RenderTargetView*,6> sixExpected{};
                for(UINT i=0;i<6;++i)sixExpected[i]=sixGbufferTargets.colors[i].Get();
                if(oceanEffects)traceNativeTargetBinding(context.Get(),"143",6,sixExpected.data(),dsv.Get());
                if(oceanEffects)applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,147);
                updateNativeDepthMirror(context.Get(),mainDepth,oceanEffects?"148 after source special zero-tail state":"148 specialized writers pending");
                mainTargets.restore(context.Get());
                // Q400 closes150/154;142 belongs to distinct B616, not MainD1.
                sixGbufferTargets.restore(context.Get()); //156
                if(oceanEffects)traceNativeTargetBinding(context.Get(),"156",6,sixExpected.data(),dsv.Get());
                updateNativeDepthMirror(context.Get(),mainDepth,"160 Rain writers pending");
                mainTargets.restore(context.Get());
                publishThinSurfaceFlags(161);
                //162/165 retain their source RS3 scope; this finite cohort
                //has no process60 writer between them.
                context->OMSetRenderTargets(1,&rsTarget,dsv.Get());
                mainTargets.restore(context.Get());
                hdr166=prepareSourceHdrPasses(device.Get(),earlySource,"late-passes.txt",litColor.Get(),targets,shaderFormats,ggx.Get(),sourceStaticModels);
                appendAccessoryHdr(hdr166,"late-passes.txt");
                if(sourceStaticModels){
                    updateNativeDeferredCameraGpu(context.Get(),deferredCameraProfile,deferredCameraFields,hdr166);
                    waterRefraction166=prepareNativeWaterRefractionGpu(waterCameraInput/"receiver-refraction-writes.txt",hdr166);
                    updateNativeWaterRefractionGpu(context.Get(),waterRefraction166,waterCamera.refraction,hdr166);
                    updateCurrentHdrLighting(hdr166);
                }
                executeSourceHdrPasses(device.Get(),context.Get(),sourceDepth,hdr166,mainDepth.depth.Get(),dsv.Get(),ambient.Get(),refractionDepth.view.Get(),refractionColors.scene97.view.Get(),
                    refractionColors.specular.view.Get(),refractionColors.blurredDepth.view.Get(),initialCommonClock,currentView.flags19AC,currentView.ocean19B0);
                mainTargets.restore(context.Get()); //165 and late HDR return to73
                if(oceanNormal)traceNativeDepthState(context.Get(),"166 late HDR exit");
                if(oceanNormal) {
                    drawCurrentPrimaryForward(); //184..187, before Effects188 and mirror191.
                    //188 reads160.189's independent View1 remains outside this
                    //cohort; do not duplicate View0's population into it.
                    if(observedParticleExperiment) {
                        if(sourceStaticModels)prepareCurrentEffects();
                        std::cout<<"source D32 Effect188 sections="<<((specialEffects.queueFlags&0x40)?"head-only":"tail/head/extra")
                            <<" current packet=head148/tail0/extra0 Q="<<std::hex<<specialEffects.queueFlags<<std::dec<<'\n';
                        drawNativeEffects188(device.Get(),context.Get(),sourceDepth,effects,mainDepth.depth.Get(),fogAcceleration.view.Get());
                        traceNativeDepthState(context.Get(),"188 Effect exit");
                    }
                    updateNativeDepthMirror(context.Get(),mainDepth,observedParticleExperiment?"191 after Effects188":"191 before normal post; particle owner pending");
                    if(sourceStaticModels)groundDetails.finishRenderNode();
                    callerTargets.restore(context.Get());
                    D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.Format=lightingShaderFormat;sv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sv.Texture2D.MipLevels=1;
                    ComPtr<ID3D11ShaderResourceView> scene;check(device->CreateShaderResourceView(litColor.Get(),&sv,&scene),"normal post retained HDR input");
                    normalPost=prepareNormalPost(device.Get(),normalInitialSource,scene.Get(),mainDepth.depth.Get(),normalOutput,width,height,
                        sourceStaticModels?&retainedNormalExposure:nullptr);
                    const auto continuousSource=input.parent_path()/"normal-post-continuous-input";
                    postContinuous=prepareNativeNormalPostContinuous(continuousSource);
                    if(sourceStaticModels)postCamera=prepareNativeNormalPostCamera(input.parent_path()/"normal-post-camera-input");
                    normalPostUnion=loadNativeNormalPostPasses(device.Get(),continuousSource,width,height);
                    if(historyContinuation) {
                        D3D11_TEXTURE2D_DESC d{};litColor->GetDesc(&d);
                        check(device->CreateTexture2D(&d,nullptr,&postInputSnapshot),"isolated normal post incoming HDR snapshot");
                        context->OMSetRenderTargets(0,nullptr,nullptr);context->CopyResource(postInputSnapshot.Get(),litColor.Get());callerTargets.restore(context.Get());
                    }
                    finalColor=executeCurrentPost(initialCommonClock,output);
                    finishNormalApply();
                    if(oceanEffects) {
                        const auto postCaller=NativeMainTargetScope::retain(context.Get());
                        auto sameOutput=normalOutput.targets[0].Get();
                        context->OMSetRenderTargets(1,&sameOutput,dsv.Get()); //194, no clear
                        traceNativeTargetBinding(context.Get(),"194",1,&sameOutput,dsv.Get());
                        std::cout<<"source194 CurrentColor=Final109 + primaryMainD1; unresolved post-FXAA populations not submitted\n";
                        postCaller.restore(context.Get()); //203
                        ComPtr<ID3D11DepthStencilView> tailDepth;context->OMGetRenderTargets(0,nullptr,&tailDepth);
                        //204 is open;205 makes no attachment change.206 only
                        //clears depth/stencil if the actual caller has a DSV.
                        if(tailDepth)context->ClearDepthStencilView(tailDepth.Get(),D3D11_CLEAR_DEPTH|D3D11_CLEAR_STENCIL,depthClear,0);
                        std::cout<<"source203 caller restore;204 open;206 depth/stencil clear="<<(tailDepth?"executed":"closed: callerDSV=null")<<'\n';
                    }
                }
            }
        } else
        litColor=drawLighting(device.Get(),context.Get(),lightingSource,input,input.parent_path()/"post-input",input.parent_path()/"ambient-input",targets,shaderFormats,depth.Get(),dsv.Get(),ggx.Get(),ambient.Get(),width,height,lightingShaderFormat,lightingTargetFormat,floatLighting,disableAmbient);
        if(!floatLighting&&!sourceHdr){
        D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.Format=lightingShaderFormat;sv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sv.Texture2D.MipLevels=1;
        ComPtr<ID3D11ShaderResourceView> scene;check(device->CreateShaderResourceView(litColor.Get(),&sv,&scene),"resolved source Scene SRV for post");
        finalColor=producePostOutput(device.Get(),context.Get(),input.parent_path()/"post-input",output,scene.Get(),width,height);
        }
    }
    auto readback=[&](ID3D11Texture2D* texture,DXGI_FORMAT format,const std::string& label,UINT surface,UINT mip=0){
        D3D11_TEXTURE2D_DESC d{};texture->GetDesc(&d);d.Usage=D3D11_USAGE_STAGING;d.BindFlags=0;d.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
        ComPtr<ID3D11Texture2D> staging;check(device->CreateTexture2D(&d,nullptr,&staging),"staging");context->CopyResource(staging.Get(),texture);D3D11_MAPPED_SUBRESOURCE mapped{};UINT subresource=surface*d.MipLevels+mip;check(context->Map(staging.Get(),subresource,D3D11_MAP_READ,0,&mapped),"readback");
        UINT w=std::max(1u,d.Width>>(subresource%d.MipLevels)),h=std::max(1u,d.Height>>(subresource%d.MipLevels));
        UINT size=pixelBytes(format);std::vector<char> result(w*h*size);for(UINT y=0;y<h;++y)std::memcpy(result.data()+y*w*size,static_cast<const char*>(mapped.pData)+y*mapped.RowPitch,w*size);context->Unmap(staging.Get(),subresource);
        std::ofstream f(output/(label+".bin"),std::ios::binary);f.write(result.data(),result.size());
        std::array<double,4> sum{};std::array<float,4> lo{INFINITY,INFINITY,INFINITY,INFINITY},hi{-INFINITY,-INFINITY,-INFINITY,-INFINITY};UINT nonzero=0,nonfinite=0,covered=0;
        int channels=storedChannels(format);
        for(UINT i=0;i<w*h;++i){auto p=decode(result.data()+i*size,format);bool nz=false;for(int c=0;c<channels;++c){if(!std::isfinite(p[c]))++nonfinite;else{lo[c]=std::min(lo[c],p[c]);hi[c]=std::max(hi[c],p[c]);sum[c]+=p[c];}nz|=p[c]!=0;}nonzero+=nz;covered+=p[0]!=depthClear;}
        if(format==DXGI_FORMAT_D32_FLOAT_S8X24_UINT) {
            std::array<UINT,256> counts{};for(UINT i=0;i<w*h;++i)++counts[uint8_t(result[i*size+4])];
            std::cout<<label<<" stencilCounts=";for(UINT i=0;i<256;++i)if(counts[i])std::cout<<i<<':'<<counts[i]<<',';std::cout<<'\n';
        }
        std::cout<<label<<" format="<<format<<" extent="<<w<<'x'<<h<<" channels="<<channels<<" nonzero="<<nonzero<<" nonfinite="<<nonfinite;
        if(label=="depth")std::cout<<" coveredDepthPixels="<<covered;
        std::cout<<" min=";for(int c=0;c<channels;++c)std::cout<<lo[c]<<',';std::cout<<" max=";for(int c=0;c<channels;++c)std::cout<<hi[c]<<',';std::cout<<" mean=";for(int c=0;c<channels;++c)std::cout<<sum[c]/(w*h)<<',';std::cout<<'\n';return result;
    };
#ifdef SAN14_WATER_STAGE_PROOF
    auto saveWaterProof=[&]{
        const std::string side=territoryReceiptSide?"current":"cold";
        const unsigned offset=territoryReceiptSide*2;
        readback(territoryDepthReceipts[offset].Get(),depthFormat,"water-proof-"+side+"-d1-before60",0);
        readback(territoryDepthReceipts[offset+1].Get(),depthFormat,"water-proof-"+side+"-d1-before122",0);
        readback(territoryWaterColorReceipts[offset].Get(),formats[0],"water-proof-"+side+"-ab-before122",0);
        readback(territoryWaterColorReceipts[offset+1].Get(),formats[0],"water-proof-"+side+"-ab-after122",0);
        for(const auto& [name,surface]:waterProofSurfaces)if(name.starts_with(side+"-")) {
            if(name.ends_with("-rs-after122")) {
                D3D11_TEXTURE2D_DESC descriptor{};surface.texture->GetDesc(&descriptor);
                for(UINT layer=0;layer<descriptor.ArraySize;++layer)
                    readback(surface.texture.Get(),surface.format,"water-proof-"+name+"-layer"+std::to_string(layer),layer);
            }else {
                readback(surface.texture.Get(),surface.format,"water-proof-"+name,surface.slice);
                if(name.ends_with("-rr0-after122")) {
                    D3D11_TEXTURE2D_DESC descriptor{};surface.texture->GetDesc(&descriptor);
                    for(UINT layer=1;layer<descriptor.ArraySize;++layer)
                        readback(surface.texture.Get(),surface.format,"water-proof-"+side+"-rr"+std::to_string(layer)+"-after122",layer);
                }
            }
        }
        readback(mapExposure.texture.Get(),DXGI_FORMAT_R32G32B32A32_FLOAT,"water-proof-"+side+"-exposure-after193",0);
        readback(litColor.Get(),DXGI_FORMAT_R11G11B10_FLOAT,"water-proof-"+side+"-hdr191",0);
        readback(finalColor.Get(),DXGI_FORMAT_B8G8R8A8_UNORM,"water-proof-"+side+"-final",0);
        auto save=[&](const std::string& name,const void* data,size_t size){
            std::ofstream stream(output/("water-proof-"+side+"-"+name+".bin"),std::ios::binary);
            stream.write(static_cast<const char*>(data),size);
        };
        save("camera",staticCamera.prefix.data(),staticCamera.prefix.size());
        save("territory-cb",territory.cpu.constants.data(),territory.cpu.constants.size());
        std::ofstream state(output/("water-proof-"+side+"-state.json"));
        state<<std::setprecision(std::numeric_limits<float>::max_digits10)
            <<"{\"request\":"<<completedMainRequests<<",\"commonClock\":"<<commonPublication.common254
            <<",\"territoryDisplayEnabled\":"<<(territoryDisplayEnabled?"true":"false")
            <<",\"displayMask\":"<<territoryControls.screenMapMask
            <<",\"view19AC\":"<<currentView.flags19AC<<",\"view19B0\":"<<currentView.ocean19B0
            <<",\"renderMode\":"<<currentRenderMode<<",\"band\":"<<staticCameraRuntime.zoom.band
            <<",\"fraction\":"<<staticCameraRuntime.zoom.fraction<<",\"width\":"<<width<<",\"height\":"<<height
            <<",\"nsFormat\":"<<formats[1]<<",\"rsFormat\":"<<formats[2]<<",\"rrFormat\":"<<formats[3]
            <<",\"hdr123Passes\":[";
        for(size_t n=0;n<hdr123.passes.size();++n){
            const auto& pass=hdr123.passes[n];const auto& gate=pass.populationGate.value();
            if(n)state<<',';
            state<<"{\"identity\":\""<<pass.identity<<"\",\"innerBit\":"<<gate.innerBit
                <<",\"selector\":"<<unsigned(gate.selector)
                <<",\"admitted\":"<<(source_hdr_population::admitted(gate,currentView.flags19AC,currentView.ocean19B0)?"true":"false")
                <<",\"reference\":"<<pass.reference<<",\"readMask\":"<<pass.readMask<<'}';
            for(const auto& cb:pass.constants)if(!cb.vertex)
                save("hdr123-ps-slot"+std::to_string(cb.slot),cb.bytes.data(),cb.bytes.size());
        }
        state<<"]}\n";
        std::cout<<"WATER_STAGE_PROOF side="<<side<<" request="<<completedMainRequests<<" clock="<<commonPublication.common254<<'\n';
    };
#endif
    if(!runLoop) {
    if(sourceStaticModels)readback(mapExposure.texture.Get(),DXGI_FORMAT_R32G32B32A32_FLOAT,"map-exposure-cold",0);
    for(int i=0;i<5;++i){auto result=readback(targets[i].Get(),formats[i],"gbuffer"+std::to_string(i),surfaces[i]);if(i==0&&!litColor&&saveImage)bmp(output/"terrain-albedo.bmp",result,formats[i],width,height);}
    if(oceanEnabled)readback(targets[3].Get(),formats[3],"ocean-rr1",1);
    readback(depth.Get(),depthFormat,"depth",surfaces[5]);
    if(sourceHdr)readback(mainDepth.sampled.Get(),depthFormat,"depth-mirror191",0);
    if(refractionDepth.texture)readback(refractionDepth.texture.Get(),DXGI_FORMAT_R32_FLOAT,"refraction-depth97",0);
    if(refractionColors.scene97.texture)readback(refractionColors.scene97.texture.Get(),DXGI_FORMAT_R11G11B10_FLOAT,"refraction-scene97",0);
    if(refractionColors.specular.texture)for(UINT mip=0;mip<7;++mip)readback(refractionColors.specular.texture.Get(),DXGI_FORMAT_R11G11B10_FLOAT,"refraction-specular137-mip"+std::to_string(mip),0,mip);
    if(refractionColors.blurredDepth.texture)readback(refractionColors.blurredDepth.texture.Get(),DXGI_FORMAT_R32_FLOAT,"refraction-blurred-depth137",0);
    if(rs68Executed||rs69Executed)readback(targets[2].Get(),formats[2],"rs3",3);
    for(const auto& entry:mainCloud.surfaces)readback(entry.second.texture.Get(),entry.second.sampled,"main-cloud-"+entry.first,0);
    if(fogAcceleration.texture)readback(fogAcceleration.texture.Get(),DXGI_FORMAT_R32G32_FLOAT,"fog-acceleration",0);
    for(const auto& entry:subShadow.surfaces){D3D11_SHADER_RESOURCE_VIEW_DESC sd{};entry.second.view->GetDesc(&sd);
        readback(entry.second.texture.Get(),sd.Format,"sub-shadow63-"+entry.first,0);}
    if(litColor)readback(litColor.Get(),lightingTargetFormat,oceanNormal?"post-scene":disableAmbient?"lighting-no-ambient-diagnostic":floatLighting?"lighting-float-diagnostic":"lighting-scene",0);
    if(finalColor){auto result=readback(finalColor.Get(),DXGI_FORMAT_B8G8R8A8_UNORM_SRGB,"final-output",0);if(!sourceCameraPan&&saveImage)bmp(output/"native-final-output.bmp",result,DXGI_FORMAT_B8G8R8A8_UNORM_SRGB,width,height);}
    }
    if(fullMainContinuation) {
        ComPtr<ID3D11Query> mainInterval,mainStart,mainEnd;
        D3D11_QUERY_DESC timing{};timing.Query=D3D11_QUERY_TIMESTAMP_DISJOINT;
        check(device->CreateQuery(&timing,&mainInterval),"Main diagnostic timing interval");
        timing.Query=D3D11_QUERY_TIMESTAMP;
        check(device->CreateQuery(&timing,&mainStart),"Main diagnostic timing start");
        check(device->CreateQuery(&timing,&mainEnd),"Main diagnostic timing end");
        auto executeRetainedMain=[&](float currentClock,const std::string& label,bool captureFrameDiagnostics=true) {
        const auto renderStart=std::chrono::steady_clock::now();
        context->Begin(mainInterval.Get());context->End(mainStart.Get());
        if(oceanFftContinuation) {
            // Normal1184320: publish completed geometry BEFORE the shared
            // grid clock writer starts the next job. No frame-number gate.
            const auto& published=publishNativeOceanFftCompleted(oceanFft);
            updateNativeOceanGeometry(context.Get(),ocean116,published.data(),UINT(published.size()*sizeof(NativeOceanFftVertex)));
            if(sourceStaticModels)completedWaveBounds=oceanFft.publishedBounds;
            advanceNativeOcean116(ocean116,currentClock);
            queueNativeOceanFft(oceanFft,ocean116.clock.accumulatedTime);
            std::cout<<"source Ocean FFT completedSide="<<oceanFft.publishedSide<<" publishedPhase="<<oceanFft.publishedPhase
                <<" pendingSide="<<oceanFft.fft.activeSide<<" pendingPhase="<<oceanFft.fft.phase<<" bytes="<<(published.size()*sizeof(NativeOceanFftVertex))<<'\n';
        }
        beginAcceptedMain(currentClock);
        if(sourceStaticModels){
            if(primaryPopulationChanged)source_shadow_gpu::bindCatalog(shadowCameraGpu.profile,jointShadow.catalog);
            source_shadow_gpu::setCurrentParameters(shadowCameraGpu.profile,shadowGpuParameters[currentShadowFamily]);
            shadowGpuPlan=source_shadow_gpu::preparePlan(shadowCameraGpu.profile,shadowCameraFrame,jointShadow.catalog);
            executeNativeShadowCameraGpu(context.Get(),sourceDepth,shadowCameraGpu,shadowCameraFrame,shadowGpuPlan,jointShadow.catalog,jointShadow.allModels,currentShadowFamily);
            shadowMap=shadowCameraGpu.map;
            updateCurrentShadowReceiver(receiver68,shadowReceiver68Camera);
            updateCurrentShadowReceiver(receiver119,shadowReceiver119Camera);
        }
        drawOpaqueGeometry();updateNativeDepthMirror(context.Get(),mainDepth,"59 new Main geometry");
        territoryReceiptSide=1;
        drawCurrentTerritory(60,1);
        publishThinSurfaceFlags(60);
        callerTargets.restore(context.Get());
        auto abTarget=rt[0];context->OMSetRenderTargets(1,&abTarget,dsv.Get());
        executeNativeSubShadow63(context.Get(),sourceDepth,subShadow,vp);callerTargets.restore(context.Get());
        auto rsTarget=indirectTarget.Get();context->OMSetRenderTargets(1,&rsTarget,dsv.Get());
        executeSourceShadowReceiver(device.Get(),context.Get(),sourceDepth,receiver68,mainDepth.depth.Get(),dsv.Get());
        executeRs69Fallback(device.Get(),context.Get(),sourceDepth,fallback69,dsv.Get());callerTargets.restore(context.Get());
        isolateCurrentShadowVisibility();
        auto mainTargets=NativeMainTargetScope::main73(normalPost.surfaces.at("SceneColor").targets[0].Get(),dsv.Get(),vp,mainScissor);
        mainTargets.restore(context.Get());
        if(sourceStaticModels){
            updateNativeDeferredCameraGpu(context.Get(),deferredCameraProfile,deferredCameraFields,hdr78);
        updateCurrentHdrLighting(hdr78);
        }
        executeSourceHdrPasses(device.Get(),context.Get(),sourceDepth,hdr78,mainDepth.depth.Get(),dsv.Get(),ambient.Get(),nullptr,nullptr,
            nullptr,nullptr,currentClock,currentView.flags19AC,currentView.ocean19B0);
        mainTargets.restore(context.Get());
        executeNativeRefractionScene97(preparedScene97,context.Get(),sourceDepth);
        executeNativeRefractionDepth(preparedDepth97,context.Get(),sourceDepth);mainTargets.restore(context.Get());
        if(sourceStaticModels)updateNativeWaterCameraGpu(waterCamera,context.Get(),ocean116,staticCamera.prefix,completedWaveBounds);
        executeNativeOcean116(device.Get(),context.Get(),sourceDepth,ocean116,currentClock);
        updateNativeDepthMirror(context.Get(),mainDepth,"117 new Main Ocean");mainTargets.restore(context.Get());
        context->OMSetRenderTargets(1,&rsTarget,dsv.Get());
        executeSourceShadowReceiver(device.Get(),context.Get(),sourceDepth,receiver119,mainDepth.depth.Get(),dsv.Get());mainTargets.restore(context.Get());
        isolateCurrentShadowVisibility();
        drawCurrentTerritory(122,2);
        publishThinSurfaceFlags(122);
        if(sourceStaticModels){
            updateNativeDeferredCameraGpu(context.Get(),deferredCameraProfile,deferredCameraFields,hdr123);
            updateNativeWaterRefractionGpu(context.Get(),waterRefraction123,waterCamera.refraction,hdr123);
        updateCurrentHdrLighting(hdr123);
        }
        executeSourceHdrPasses(device.Get(),context.Get(),sourceDepth,hdr123,mainDepth.depth.Get(),dsv.Get(),ambient.Get(),refractionDepth.view.Get(),refractionColors.scene97.view.Get(),
            nullptr,nullptr,currentClock,currentView.flags19AC,currentView.ocean19B0);
#ifdef SAN14_WATER_STAGE_PROOF
        captureWaterProof("hdr123",litColor.Get(),DXGI_FORMAT_R11G11B10_FLOAT);
        captureWaterProof("scene97",refractionColors.scene97.texture.Get(),DXGI_FORMAT_R11G11B10_FLOAT);
#endif
        if(sourceStaticModels)updateNativeAtmosphereCamera(context.Get(),atmosphereCamera,staticCamera.prefix,mainSky,mainCloud,mapFog);
        mainTargets.restore(context.Get());executeNativeMainSky(device.Get(),context.Get(),sourceDepth,mainSky);mainTargets.restore(context.Get());
        executeNativeMainCloud(device.Get(),context.Get(),sourceDepth,mainCloud,dsv.Get(),currentClock,UINT(actualHostQ7F8));mainTargets.restore(context.Get());
        publishCurrentHeightFog();
        executeNativeMapFog(device.Get(),context.Get(),sourceDepth,mapFog,currentClock,currentView.flags19AC);mainTargets.restore(context.Get());
        executeNativeRefractionFilters137(preparedFilters137,context.Get(),sourceDepth,refractionColors);mainTargets.restore(context.Get());
        std::array<ID3D11ShaderResourceView*,32> empty{};context->VSSetShaderResources(0,32,empty.data());context->PSSetShaderResources(0,32,empty.data());
        context->OMSetRenderTargets(0,nullptr,refractionDepth.depth.Get());applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,141);
        mainTargets.restore(context.Get());sixGbufferTargets.restore(context.Get());applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,147);
        updateNativeDepthMirror(context.Get(),mainDepth,"148 new Main special");mainTargets.restore(context.Get());
        sixGbufferTargets.restore(context.Get());updateNativeDepthMirror(context.Get(),mainDepth,"160 new Main rain source-closed");mainTargets.restore(context.Get());
        publishThinSurfaceFlags(161);
        context->OMSetRenderTargets(1,&rsTarget,dsv.Get());mainTargets.restore(context.Get());
        if(sourceStaticModels){
            updateNativeDeferredCameraGpu(context.Get(),deferredCameraProfile,deferredCameraFields,hdr166);
            updateNativeWaterRefractionGpu(context.Get(),waterRefraction166,waterCamera.refraction,hdr166);
        updateCurrentHdrLighting(hdr166);
        }
        executeSourceHdrPasses(device.Get(),context.Get(),sourceDepth,hdr166,mainDepth.depth.Get(),dsv.Get(),ambient.Get(),refractionDepth.view.Get(),refractionColors.scene97.view.Get(),refractionColors.specular.view.Get(),refractionColors.blurredDepth.view.Get(),
            currentClock,currentView.flags19AC,currentView.ocean19B0);
        drawCurrentPrimaryForward();
        if(observedParticleExperiment) {
            if(sourceStaticModels)prepareCurrentEffects();
        }
        mainTargets.restore(context.Get());
        if(observedParticleExperiment)drawNativeEffects188(device.Get(),context.Get(),sourceDepth,effects,mainDepth.depth.Get(),fogAcceleration.view.Get());
        updateNativeDepthMirror(context.Get(),mainDepth,observedParticleExperiment?"191 new Main Effects":"191 new Main; particle owner pending");callerTargets.restore(context.Get());
        if(sourceStaticModels)groundDetails.finishRenderNode();
        context->End(mainEnd.Get());context->End(mainInterval.Get());
        const double mainSubmitMs=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-renderStart).count();
        executeCurrentPost(currentClock,output/label);
        finishNormalApply();
        //194/203 alias the same Final109;206 remains closed with null callerDSV.
        const auto postCaller=NativeMainTargetScope::retain(context.Get());auto sameOutput=normalOutput.targets[0].Get();
        context->OMSetRenderTargets(1,&sameOutput,dsv.Get());postCaller.restore(context.Get());
        D3D11_QUERY_DATA_TIMESTAMP_DISJOINT interval{};UINT64 begin=0,end=0;
        if(context->GetData(mainInterval.Get(),&interval,sizeof(interval),D3D11_ASYNC_GETDATA_DONOTFLUSH)==S_OK&&!interval.Disjoint&&
           context->GetData(mainStart.Get(),&begin,sizeof(begin),D3D11_ASYNC_GETDATA_DONOTFLUSH)==S_OK&&
           context->GetData(mainEnd.Get(),&end,sizeof(end),D3D11_ASYNC_GETDATA_DONOTFLUSH)==S_OK)
            std::cout<<"fixed-input Main through191 plus193 submitCpuMs="<<(mainSubmitMs+normalPost.lastSubmitMs)
                <<" gpuMs="<<(double(end-begin)*1000/double(interval.Frequency)+normalPost.lastGpuMs)<<" captures excluded\n";
        std::cout<<"complete fixed-input Main followup postCaptures="<<postCaptureDiagnostics<<" elapsedMs="
            <<std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-renderStart).count()<<'\n';
        if(captureFrameDiagnostics) {
        readback(mainCloud.lastRgb().texture.Get(),mainCloud.lastRgb().sampled,label+"-cloud-rgb",0);
        readback(mainCloud.lastAlpha().texture.Get(),mainCloud.lastAlpha().sampled,label+"-cloud-alpha",0);
        readback(litColor.Get(),lightingTargetFormat,label+"-post-scene",0);
        auto pixels=readback(finalColor.Get(),DXGI_FORMAT_B8G8R8A8_UNORM_SRGB,label+"-final",0);
        if(sourceCameraPan&&saveImage)bmp(output/"native-camera-final.bmp",pixels,DXGI_FORMAT_B8G8R8A8_UNORM_SRGB,width,height);
        if(oceanFftContinuation) {
            const auto& vertices=oceanFft.fft.vertices[oceanFft.publishedSide];
            std::ofstream cpu(output/(label+"-wave-cpu.bin"),std::ios::binary);
            cpu.write(reinterpret_cast<const char*>(vertices.data()),vertices.size()*sizeof(NativeOceanFftVertex));
            D3D11_BUFFER_DESC desc{};ocean116.geometry.wave[0]->GetDesc(&desc);
            desc.Usage=D3D11_USAGE_STAGING;desc.BindFlags=0;desc.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
            ComPtr<ID3D11Buffer> staging;check(device->CreateBuffer(&desc,nullptr,&staging),"FFT diagnostic staging");
            context->CopyResource(staging.Get(),ocean116.geometry.wave[0].Get());D3D11_MAPPED_SUBRESOURCE map{};
            check(context->Map(staging.Get(),0,D3D11_MAP_READ,0,&map),"FFT diagnostic wave readback");
            std::ofstream gpu(output/(label+"-wave-gpu.bin"),std::ios::binary);gpu.write(static_cast<const char*>(map.pData),desc.ByteWidth);
            context->Unmap(staging.Get(),0);
            std::ofstream state(output/(label+"-wave-state.json"));state<<std::setprecision(std::numeric_limits<double>::max_digits10)
                <<"{\"clock\":"<<ocean116.clock.accumulatedTime<<",\"publishedSide\":"<<oceanFft.publishedSide
                <<",\"publishedPhase\":"<<oceanFft.publishedPhase<<",\"pendingSide\":"<<oceanFft.fft.activeSide
                <<",\"pendingPhase\":"<<oceanFft.fft.phase<<",\"publishedBounds\":[";
            for(UINT n=0;n<8;++n){if(n)state<<',';state<<oceanFft.publishedBounds[n];}state<<"]}\n";
        }
        }
        ++completedMainRequests;
        return std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-renderStart).count();
        };
        if(runLoop) {
#ifdef SAN14_RENDER_SESSION
            // All initialized owners remain in this coroutine frame. Godot
            // drives subsequent requests; no native window or polling thread.
#ifdef SAN14_WATER_STAGE_PROOF
            saveWaterProof();
#endif
            co_yield RenderOutput{finalColor.Get(),staticCamera.prefix,completedMainRequests};
            const UINT baseFlags=commonBaseSettingsFlags;
            for(;;) {
                commonClock.settingsFlags10=(host.input.flags&2)?baseFlags|commonClock.pauseMask:baseFlags;
                commonPublication=advanceNativeFrameClock(commonClock,host.input.deltaSeconds);
                executeRetainedMain(commonPublication.common254,"runtime",false);
#ifdef SAN14_WATER_STAGE_PROOF
                saveWaterProof();
#endif
                co_yield RenderOutput{finalColor.Get(),staticCamera.prefix,completedMainRequests};
            }
#else
            NativePresentation presentation;
            const bool hasPresentation=visibleView||hiddenView;
            if(hasPresentation) {
                presentation=prepareNativePresentation(device.Get(),width,height,sourceCameraContinuous);
                presentNativeFinal(context.Get(),presentation,finalColor.Get());
                if(visibleView)ShowWindow(presentation.window,SW_SHOWNOACTIVATE);
            }
            UINT64 requests=1;double totalSubmitMs=0;UINT baseFlags=commonClock.settingsFlags10;
            if(sourceCameraContinuous&&(requestedFocusDeltaX!=0||requestedCameraDistance>=0))requestSourceCamera(requestedFocusDeltaX,0,false,requestedCameraDistance);
            if(sourceCameraContinuous&&requestedWheelDelta!=0)requestSourceWheel(requestedWheelDelta,int(width)/2,int(height)/2);
            auto lastTick=std::chrono::steady_clock::now(),loopStarted=lastTick;
            // Continuous rendering writes no per-request diagnostics or files.
            // Failures still report on stderr. The finite experiment modes keep logs.
            std::cout.setstate(std::ios::failbit);
            while(maximumRequests==0||requests<maximumRequests) {
                if(hasPresentation&&!pumpNativePresentation(presentation))break;
                if(sourceCameraContinuous&&(presentation.resetCamera||presentation.focusDeltaX!=0||presentation.focusDeltaZ!=0)) {
                    requestSourceCamera(presentation.focusDeltaX,presentation.focusDeltaZ,presentation.resetCamera);
                    presentation.focusDeltaX=presentation.focusDeltaZ=0;presentation.resetCamera=false;
                }
                if(sourceCameraContinuous&&!presentation.wheelEvents.empty()){
                    RECT client{};GetClientRect(presentation.window,&client);
                    for(const auto& event:presentation.wheelEvents){
                        const auto point=source_camera_motion::canonicalCursor(event.clientX,event.clientY,client.right,client.bottom,int(width),int(height));
                        requestSourceWheel(event.delta,point.x,point.y);
                    }
                    presentation.wheelEvents.clear();
                }
                const auto tick=std::chrono::steady_clock::now();
                const float delta=fixedRendererDelta!=0?fixedRendererDelta:float(std::chrono::duration<double>(tick-lastTick).count());
                lastTick=tick;
                commonClock.settingsFlags10=hasPresentation&&presentation.paused?baseFlags|commonClock.pauseMask:baseFlags;
                commonPublication=advanceNativeFrameClock(commonClock,delta); // Exactly once, before Main consumers.
                totalSubmitMs+=executeRetainedMain(commonPublication.common254,"runtime",false);
                if(hasPresentation)presentNativeFinal(context.Get(),presentation,finalColor.Get());
                ++requests;
            }
            std::cout.clear();
            const double elapsed=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-loopStarted).count();
            if(snapshotAtEnd) {
                auto pixels=readback(finalColor.Get(),DXGI_FORMAT_B8G8R8A8_UNORM_SRGB,"runtime-final",0);
                readback(litColor.Get(),lightingTargetFormat,"runtime-scene",0);
                if(hasPresentation){const auto parent=NativeMainTargetScope::retain(context.Get());context->OMSetRenderTargets(0,nullptr,nullptr);
                    context->CopyResource(presentation.backbuffer.Get(),finalColor.Get());
                    readback(presentation.backbuffer.Get(),DXGI_FORMAT_B8G8R8A8_UNORM,"runtime-backbuffer",0);parent.restore(context.Get());}
                if(saveImage)bmp(output/"native-runtime-final.bmp",pixels,DXGI_FORMAT_B8G8R8A8_UNORM_SRGB,width,height);
            }
            std::ofstream receipt(output/"runtime-result.json");receipt<<std::setprecision(std::numeric_limits<float>::max_digits10);
            receipt<<"{\"scope\":\"Continuous native Main; selected caller delta through original FrameSync; held optics/season/World and explicit bootstrap history\",\"requests\":"<<requests
                <<",\"common254\":"<<commonPublication.common254<<",\"common258\":"<<commonPublication.common258
                <<",\"fixedCallerDelta\":"<<fixedRendererDelta<<",\"retainedSubmitMs\":"<<totalSubmitMs<<",\"loopElapsedMs\":"<<elapsed
                <<",\"postCount10\":"<<postContinuous.count10<<",\"postHistory16\":"<<postContinuous.history16<<",\"postStaging28\":"<<postContinuous.stagingSide28
                <<",\"actualStagingMaps\":"<<actualStagingMaps<<",\"failedStagingMaps\":"<<failedStagingMaps
                <<",\"oceanPublishedPhase\":"<<oceanFft.publishedPhase<<",\"oceanPendingPhase\":"<<oceanFft.fft.phase
                <<",\"oceanPublishedSide\":"<<oceanFft.publishedSide<<",\"oceanPendingSide\":"<<oceanFft.fft.activeSide
                <<",\"environmentState168\":"<<environment.state168<<",\"environmentSelected\":"<<environment.selected
                <<",\"presentation\":"<<(hasPresentation?"true":"false")<<",\"visibleWindow\":"<<(visibleView?"true":"false")
                <<",\"snapshotAtEnd\":"<<(snapshotAtEnd?"true":"false")<<",\"sourceCameraConsumers\":"<<(sourceCameraContinuous?"true":"false")
                <<",\"acceptedCameraRequests\":"<<acceptedCameraRequests<<",\"acceptedWheelRequests\":"<<acceptedWheelRequests
                <<",\"cameraTicks\":"<<cameraTickCount<<",\"cameraTimer\":\""<<(cameraClock.performanceCounter?"QPC":"clock()")<<"\",\"cameraTimerFrequency\":"<<cameraClock.frequency
                <<",\"perRequestDiagnosticReadback\":false}\n";
#ifdef SAN14_GROUND_GPU_PROOF
            D3D11_QUERY_DATA_PIPELINE_STATISTICS groundOpaque{},groundAlpha{};
            check(context->GetData(groundGpuProof[0].Get(),&groundOpaque,sizeof(groundOpaque),0),"Ground detail opaque GPU proof receipt");
            check(context->GetData(groundGpuProof[1].Get(),&groundAlpha,sizeof(groundAlpha),0),"Ground detail alpha GPU proof receipt");
            if(!groundOpaque.VSInvocations&&!groundAlpha.VSInvocations)throw std::runtime_error("Ground detail original shaders had zero GPU vertex invocations");
            if(!groundOpaque.PSInvocations&&!groundAlpha.PSInvocations)throw std::runtime_error("Ground detail original shaders had zero GPU pixel invocations");
            const auto& groundWarm=groundDetails.currentStatistics();
            std::ofstream groundReceipt(output/"ground-details-gpu-result.json");
            groundReceipt<<"{\"requests\":"<<requests<<",\"coldInstances\":"<<coldGroundStatistics.selectedInstances
                <<",\"coldPrepared\":"<<coldGroundStatistics.preparedPrimitives<<",\"coldDraws\":"<<coldGroundStatistics.nonemptyDraws
                <<",\"panInstances\":"<<groundWarm.selectedInstances<<",\"panPrepared\":"<<groundWarm.preparedPrimitives<<",\"panDraws\":"<<groundWarm.nonemptyDraws
                <<",\"opaqueVSInvocations\":"<<groundOpaque.VSInvocations<<",\"opaquePSInvocations\":"<<groundOpaque.PSInvocations
                <<",\"alphaVSInvocations\":"<<groundAlpha.VSInvocations<<",\"alphaPSInvocations\":"<<groundAlpha.PSInvocations
                <<",\"windUpdated\":"<<(groundWarm.windUpdated?"true":"false")<<",\"appFlags\":"<<groundWarm.currentAppFlags<<",\"aux14\":"<<groundWarm.currentAux14
                <<",\"originalArmyDebugPopulation\":"<<(sourceArmyVisuals?"true":"false")<<"}\n";
#endif
            if(hasPresentation&&IsWindow(presentation.window))DestroyWindow(presentation.window);
            std::cout<<"native continuous requests="<<requests<<" common254="<<commonPublication.common254<<" retainedSubmitMs="<<totalSubmitMs
                <<" loopElapsedMs="<<elapsed<<" per-request diagnostics=off\n";
#endif
        } else {
            if(sourceCameraPan) {
                // One explicit caller request, then the ordinary Main flow.
                // No camera oracle or frame-number algorithm selection.
                if(requestedFocusDeltaX!=0||requestedCameraDistance>=0)requestSourceCamera(requestedFocusDeltaX,0,false,requestedCameraDistance);
                if(requestedWheelDelta!=0)requestSourceWheel(requestedWheelDelta,int(width)/2,int(height)/2);
                std::cout<<"source caller camera request focusDeltaX="<<requestedFocusDeltaX<<" actualFocusX="<<staticCameraRuntime.pose.x<<'\n';
            }
            executeRetainedMain(nextCommonClock,"full-main-followup");
            if(oceanFftContinuation)executeRetainedMain(thirdCommonClock,"full-main-third");
        }
    }
    if(historyContinuation) {
        std::cout<<"initial whole-scene load/render/capture elapsedMs="<<std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-start).count()<<'\n';
        // Two isolated normal consumers share their true persistent owners.
        // Reuse the same captured incoming scene to avoid applying DOF/cloud
        // twice to an already processed image. This is not a second fullMain.
        normalPostUnbind(context.Get());context->CopyResource(litColor.Get(),postInputSnapshot.Get());
        const auto postStart=std::chrono::steady_clock::now();
        executeCurrentPost(nextCommonClock,output/"normal-post-followup");
        finishNormalApply();
        std::cout<<"normal193 followup including diagnostics elapsedMs="<<std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-postStart).count()<<'\n';
        normalPostUnbind(context.Get());context->CopyResource(litColor.Get(),cloudInputSnapshot.Get());
        sourceDepth.bind(context.Get(),cloudEntryDepth.Get(),cloudEntryLogical.description,cloudEntryReference);
        context->OMSetBlendState(cloudEntryBlend.Get(),cloudEntryFactor,cloudEntrySampleMask);context->RSSetState(cloudEntryRaster.Get());
        const auto cloudStart=std::chrono::steady_clock::now();
        advanceNativeMainCloud(mainCloud,nextCommonClock); //isolated node3/124 diagnostic.
        executeNativeMainCloud(device.Get(),context.Get(),sourceDepth,mainCloud,dsv.Get(),nextCommonClock,UINT(actualHostQ7F8));
        std::cout<<"MainCloud124 followup submitCpuMs="<<std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-cloudStart).count()<<'\n';
        readback(mainCloud.lastRgb().texture.Get(),mainCloud.lastRgb().sampled,"main-cloud-followup-rgb",0);
        readback(mainCloud.lastAlpha().texture.Get(),mainCloud.lastAlpha().sampled,"main-cloud-followup-alpha",0);
        readback(mainCloud.surfaces.at("bayer-info").texture.Get(),mainCloud.surfaces.at("bayer-info").sampled,"main-cloud-followup-info",0);
    }
    if(sourceStaticModels) {
        if(!runLoop){
            for(size_t i=0;i<mapExposureBeforeMainReceipts.size();++i)
                readback(mapExposureBeforeMainReceipts[i].Get(),DXGI_FORMAT_R32G32B32A32_FLOAT,"map-exposure-before-main-"+std::to_string(i),0);
            for(size_t i=0;i<mapExposureAfterApplyReceipts.size();++i)
                readback(mapExposureAfterApplyReceipts[i].Get(),DXGI_FORMAT_R32G32B32A32_FLOAT,"map-exposure-after-apply-"+std::to_string(i),0);
        }
        if(!runLoop)for(unsigned side=0;side<2;++side){
            const std::string name=side?"current":"cold";
            for(unsigned stage=0;stage<2;++stage)if(territoryDepthReceipts[side*2+stage])
                readback(territoryDepthReceipts[side*2+stage].Get(),depthFormat,"territory-"+name+"-d1-before"+(stage?"122":"60"),0);
            if(territoryWaterColorReceipts[side*2])readback(territoryWaterColorReceipts[side*2].Get(),formats[0],"territory-"+name+"-ab-before122",0);
            if(territoryWaterColorReceipts[side*2+1])readback(territoryWaterColorReceipts[side*2+1].Get(),formats[0],"territory-"+name+"-ab-after122",0);
        }
        std::ofstream receipt(output/"source-static-models-result.json");
        const bool cameraChanged=staticCameraRuntime.pose.x!=staticCameraProfile.initial.x||staticCameraRuntime.pose.y!=staticCameraProfile.initial.y||staticCameraRuntime.pose.z!=staticCameraProfile.initial.z
            ||staticCameraRuntime.pose.distance!=staticCameraProfile.initial.distance||staticCameraRuntime.pose.yaw!=staticCameraProfile.initial.yaw||staticCameraRuntime.pose.pitch!=staticCameraProfile.initial.pitch;
        receipt<<std::setprecision(std::numeric_limits<double>::max_digits10)<<"{\"scope\":\"Whole implemented Main with held season, lens and World state; original camera input/tick and all selected camera consumers\",\"instances\":"<<staticCatalog.instances.size()
            <<",\"primitives\":"<<staticCatalog.primitives.size()<<",\"templates\":"<<staticCatalog.templateCount<<",\"visibleInstances\":"<<staticPlan.visibleInstances.size()
            <<",\"orderedPrimitives\":"<<staticPlan.draws.size()<<",\"terrainInstances\":"<<staticCamera.instances.size()<<",\"shadowDraws\":"<<shadowCameraGpu.map.draws
            <<",\"cityVisibleInstances\":"<<cityPlan.models.visibleInstances.size()<<",\"cityOrderedPrimitives\":"<<cityPlan.models.draws.size()
            <<",\"primaryOwners\":"<<cityModels.source.worlds.size()<<",\"primaryPrimitives\":"<<cityModels.source.catalog.primitives.size()<<",\"primaryTemplates\":"<<cityModels.source.catalog.templateCount
            <<",\"shadowCatalogInstances\":"<<jointShadow.catalog.instances.size()<<",\"shadowCatalogPrimitives\":"<<jointShadow.catalog.primitives.size()<<",\"shadowTemplates\":"<<jointShadow.catalog.templateCount
            <<",\"ordinary54Draws\":"<<ordinaryModelQueues.ordinary54.draws.size()<<",\"alpha55Draws\":"<<ordinaryModelQueues.alpha55.draws.size()
            <<",\"accessoryGroups\":"<<cityAccessories.groups.size()<<",\"accessoryOrdinaryCount\":"<<accessoryOrdinaryCount<<",\"accessoryDraws\":"<<cityAccessories.draws
            <<",\"armySourceActors\":"<<armyModels.source.actors.size()<<",\"armyVisibleActors\":"<<armyPlan.visibleActors.size()<<",\"armyDraws\":"<<armyPlan.draws.size()<<",\"armyReturnedPhase\":"<<actualArmyReturnedPhase
            <<",\"effectDraws\":"<<effectCameraFrame.draws.size()<<",\"waterStrips\":"<<waterCamera.current.strips.size()<<",\"viewFlags\":"<<currentView.flags19AC
            <<",\"requests\":"<<completedMainRequests<<",\"requestedFocusDeltaX\":"<<requestedFocusDeltaX<<",\"acceptedCameraRequests\":"<<acceptedCameraRequests
            <<",\"acceptedWheelRequests\":"<<acceptedWheelRequests<<",\"requestedWheelDelta\":"<<requestedWheelDelta
            <<",\"controlledCameraElapsedMs\":"<<requestedCameraElapsedMs<<",\"cameraTicks\":"<<cameraTickCount
            <<",\"cameraTimer\":\""<<(cameraClock.performanceCounter?"QPC":"clock()")<<"\",\"cameraTimerFrequency\":"<<cameraClock.frequency
            <<",\"cameraRequestMode\":"<<cameraMotion.requestMode<<",\"transitionElapsedMs\":"<<(cameraMotion.transition?cameraMotion.transition->elapsed:-1)
            <<",\"territoryDraws\":"<<territoryDraws<<",\"mapExposureDraws\":"<<mapExposureDraws<<",\"heldMainB1C\":"<<heldMainB1C
            <<",\"territoryLayerMask\":"<<territoryControls.screenMapMask<<",\"territoryZoneInitialUpdates\":"<<territoryZoneInitialUpdates
            <<",\"mapExposurePublicationDraws\":"<<mapExposurePublicationDraws<<",\"mapExposureHalfDraws\":"<<mapExposureHalfDraws
            <<",\"normalPatternReceiver\":true,\"selectedLitObject\":\"80F9DB7B\",\"selectedLitViewFormat\":87"
            <<",\"primaryForwardDraws\":"<<primaryForwardGpu.draws<<",\"primaryForwardReady\":"<<(primaryForwardReady?"true":"false")
            <<",\"primaryForwardPreparationOnly\":"<<(primaryForwardPreparationOnly?"true":"false")
            <<",\"sourceCasterForcedRaster\":true,\"sourceCasterSlopeBias\":"<<shadowCameraGpu.raster.source[currentShadowFamily].passes[0].SlopeScaledDepthBias
            <<",\"sourceCasterBiasSelector\":"<<shadowCameraGpu.raster.source[currentShadowFamily].selector
            <<",\"sourceCasterDepthBias\":["
            <<shadowCameraGpu.raster.source[currentShadowFamily].passes[0].DepthBias<<','
            <<shadowCameraGpu.raster.source[currentShadowFamily].passes[1].DepthBias<<','
            <<shadowCameraGpu.raster.source[currentShadowFamily].passes[2].DepthBias<<','
            <<shadowCameraGpu.raster.source[currentShadowFamily].passes[3].DepthBias<<']'
            <<",\"shadowVisibilityExperiment\":"<<(isolateShadowVisibility?"true":"false")
            <<",\"receiver68Reference\":"<<receiver68.reference<<",\"receiver68ReadMask\":"<<receiver68.readMask
            <<",\"receiver119Reference\":"<<receiver119.reference<<",\"receiver119ReadMask\":"<<receiver119.readMask
            <<",\"distance\":"<<staticCameraRuntime.pose.distance<<",\"pitch\":"<<staticCameraRuntime.pose.pitch<<",\"zoomBand\":"<<staticCameraRuntime.zoom.band<<",\"zoomFraction\":"<<staticCameraRuntime.zoom.fraction
            <<",\"renderMode\":"<<currentRenderMode<<",\"shadowPasses\":"<<shadowCameraFrame.passCount
            <<",\"cameraChanged\":"<<(cameraChanged?"true":"false")<<",\"fullSourceSort\":true,\"imageSaved\":"
            <<(saveImage&&(!runLoop||snapshotAtEnd)?"true":"false")<<"}\n";
        std::ofstream prefix(output/"current-camera-prefix.bin",std::ios::binary);
        prefix.write(reinterpret_cast<const char*>(staticCamera.prefix.data()),staticCamera.prefix.size());
        if(snapshotAtEnd||!runLoop) {
            readback(mapExposure.texture.Get(),DXGI_FORMAT_R32G32B32A32_FLOAT,"map-exposure-current",0);
            const auto directory=output/"current-producers";fs::create_directories(directory);
            auto save=[&](const std::string& name,const void* data,size_t bytes){std::ofstream file(directory/name,std::ios::binary);file.write(static_cast<const char*>(data),bytes);};
            save("weather-main72.bin",weatherCameraFrame.main72.data(),weatherCameraFrame.main72.size());
            save("territory-ps-b0.bin",territory.cpu.constants.data(),territory.cpu.constants.size());
            save("territory-zone-layers.bin",territoryZoneOwner.zones.data(),territoryZoneOwner.zones.size());
            save("territory-region-colors.bin",territoryZoneProfile.regionColors.data(),sizeof(territoryZoneProfile.regionColors));
            save("map-exposure-packet32.bin",mapExposure.current.texels.data(),sizeof(mapExposure.current.texels));
            save("ordinary-record-counts.bin",ordinaryPopulationCounts.data(),ordinaryPopulationCounts.size()*sizeof(unsigned));
            std::ofstream actualCasterRaster(directory/"shadow-caster-raster.txt");
            for(unsigned pass=0;pass<unsigned(shadowCameraFrame.passCount);++pass) {
                D3D11_RASTERIZER_DESC state{};
                shadowCameraGpu.raster.passes[currentShadowFamily][pass]->GetDesc(&state);
                actualCasterRaster<<pass<<' '<<state.DepthBias<<' '<<state.SlopeScaledDepthBias<<' '<<state.DepthBiasClamp
                    <<' '<<state.FillMode<<' '<<state.CullMode<<' '<<state.FrontCounterClockwise<<' '<<state.DepthClipEnable
                    <<' '<<state.ScissorEnable<<' '<<state.MultisampleEnable<<' '<<state.AntialiasedLineEnable<<'\n';
            }
            std::ofstream actualShadowPackets(directory/"shadow-draw-plan-cb.bin",std::ios::binary);
            for(const auto& draw:shadowGpuPlan.draws){actualShadowPackets.write(reinterpret_cast<const char*>(&draw.pass),4);actualShadowPackets.write(reinterpret_cast<const char*>(&draw.primitiveIndex),4);actualShadowPackets.write(reinterpret_cast<const char*>(&draw.bytes),4);actualShadowPackets.write(draw.constants.data(),draw.bytes);}
            std::ofstream cityConstants(directory/"city-ordinary-constants.bin",std::ios::binary);
            for(const auto& draw:cityPlan.models.draws){cityConstants.write(draw.vertex.data(),draw.vertex.size());cityConstants.write(draw.pixel.data(),draw.pixel.size());}
            std::ofstream forwardConstants(directory/"primary-forward-plan-cb.bin",std::ios::binary);
            for(const auto& draw:primaryForwardFrame.draws) {
                forwardConstants.write(reinterpret_cast<const char*>(&draw.primitiveIndex),4);
                forwardConstants.write(reinterpret_cast<const char*>(&draw.templateIndex),4);
                forwardConstants.write(draw.vertex.data(),draw.vertex.size());forwardConstants.write(draw.pixel.data(),draw.pixel.size());
            }
            std::ofstream opaqueOrder(directory/"ordinary54-order.txt");
            for(const auto& draw:ordinaryModelQueues.ordinary54.draws)opaqueOrder<<draw.identity<<'\n';
            std::ofstream alphaOrder(directory/"alpha55-order.txt");
            for(const auto& draw:ordinaryModelQueues.alpha55.draws)alphaOrder<<draw.identity<<'\n';
            save("main-direct-prefix.bin",mainDirectPrefix.data(),mainDirectPrefix.size());
            save("rs68-ps-b0.bin",shadowReceiver68Camera.current.data(),shadowReceiver68Camera.current.size());
            save("rs119-ps-b0.bin",shadowReceiver119Camera.current.data(),shadowReceiver119Camera.current.size());
            for(const auto* hdr:{&hdr78,&hdr123,&hdr166})for(const auto& pass:hdr->passes)for(const auto& cb:pass.constants)if(!cb.vertex&&cb.slot==0)
                save(pass.identity+"-ps-b0.bin",cb.bytes.data(),cb.bytes.size());
            for(const auto& cb:effectCamera.constants)save("effect-"+std::to_string(cb.pass)+(cb.vertex?"-vs-b":"-ps-b")+std::to_string(cb.slot)+".bin",cb.bytes.data(),cb.bytes.size());
            save("atmosphere-prepared-main.bin",atmosphereCamera.current.preparedMain.data(),atmosphereCamera.current.preparedMain.size());
            save("sky-vs-b0.bin",atmosphereCamera.current.skyVertex.data(),atmosphereCamera.current.skyVertex.size());
            save("sky-ps-b0.bin",atmosphereCamera.current.skyPixel.data(),atmosphereCamera.current.skyPixel.size());
            save("post-lens-projection.bin",postCameraProjection.value.data(),sizeof(postCameraProjection.value));
            for(const auto& item:postContinuous.staticConstants)if(item.first.size()>=8&&item.first.compare(item.first.size()-8,8,"cb22.bin")==0)
                save(item.first.substr(0,item.first.find('/'))+"-cb22.bin",item.second.data(),item.second.size());
        }
    }
    std::cout<<"terrainDraws="<<drawCount<<" instances="<<(sourceStaticModels?terrainCamera.instanceCount:instanceCount)<<" indices="<<indexCount<<" depthClear="<<depthClear<<" depthCompare="<<depthCompare<<" extent="<<width<<'x'<<height<<" elapsedMs="<<std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-start).count()<<'\n';SAN14_RENDER_RETURN(0);
#ifdef SAN14_RENDER_SESSION
}
#else
}catch(const std::exception& e){std::cerr<<e.what()<<'\n';return 1;}
#endif
#undef SAN14_RENDER_RETURN

