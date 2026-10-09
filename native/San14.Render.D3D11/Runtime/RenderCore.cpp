// Campaign map renderer. Prepared resources are imported once; retained
// GPU owners are advanced exclusively by client frame requests.
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
#include "../Scene/Models/ArmyUnitHeight.h"
#include "../Scene/SceneEnvironmentPacket.h"
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
#include "../Device/DepthState.h"
#include "../Passes/Shadows/ShadowMask.h"
#include "../Passes/Environment/AmbientRenderer.h"
#include "../Passes/Lighting/DeferredLighting.h"
#include "../Passes/PostProcessing/PostTargets.h"
#include "../Scene/Models/ModelRenderer.h"
#include "../Camera/CameraState.h"
#include "../Camera/CameraMotion.h"
#include "../Camera/CameraClock.h"
#include "../Camera/CameraRig.h"
#include "../Scene/Models/ModelFrame.h"
#include "../Scene/Models/ModelView.h"
#include "../Scene/Models/StaticModelResources.h"
#include "../Scene/Terrain/TerrainCameraConstants.h"
#include "../Passes/Shadows/ShadowMap.h"
#include "../Device/DepthMirror.h"
#include "../Passes/Shadows/ShadowReceiver.h"
#include "../Passes/Environment/Ocean/RefractionDepth.h"
#include "../Passes/Environment/Ocean/OceanTextures.h"
#include "../Passes/Lighting/HdrRenderer.h"
#include "../Passes/Lighting/DeferredCameraConstants.h"
#include "../Passes/Shadows/ShadowCameraFrame.h"
#include "../Passes/Shadows/ShadowCameraConstants.h"
#include "../Passes/Environment/Ocean/OceanRenderer.h"
#include "../Passes/Environment/Ocean/WaterCameraConstants.h"
#include "../Passes/Environment/Ocean/OceanFft.h"
#include "../Passes/Environment/Ocean/RefractionColor.h"
#include "../Passes/Environment/Ocean/OceanPass.h"
#include "../Passes/PostProcessing/PostProcessRenderer.h"
#include "../Passes/PostProcessing/PostProcessFrame.h"
#include "../Passes/PostProcessing/PostProcessCamera.h"
#include "../Device/RenderTargetScope.h"
#include "FrameClock.h"
#include "../Passes/Environment/MapFog.h"
#include "../Passes/Environment/SkyRenderer.h"
#include "../Passes/Environment/CloudRenderer.h"
#include "../Passes/Environment/AmbientCloudFrame.h"
#include "../Passes/Shadows/SubShadow.h"
#include "../Passes/Effects/EffectResources.h"
#include "../Passes/Effects/EffectRenderer.h"
#include "../Passes/Effects/EffectCameraFrame.h"
#include "../Passes/Environment/AtmosphereCameraFrame.h"
#include "../Passes/Environment/WeatherFogFrame.h"
#include "../Passes/Environment/WeatherFogConstants.h"
#include "../Passes/Environment/FogAccelerationPass.h"
#include "../Passes/Effects/SpecialEffectState.h"
#include "../Passes/PostProcessing/MapExposure.h"
#include "../Scene/Terrain/TerritoryRenderer.h"
#include "../Scene/Terrain/ZoneLayer.h"
#include "../Scene/Models/CityModels.h"
#include "../Scene/Models/CityPopulation.h"
#include "../Scene/Models/AreaPrimaryPopulation.h"
#include "../Passes/Shadows/JointPopulationShadow.h"
#include "../Passes/Shadows/CityGateShadow.h"
#include "../Passes/Shadows/CityAreaShadow.h"
#include "../Passes/Forward/PrimaryForwardRenderer.h"
#include "../Passes/Forward/PrimaryForwardFrame.h"
#include "../Scene/Models/Accessories/CityAccessoryRenderer.h"
#include "DefaultAnimationPhase.h"
#include "../Scene/Models/BaseVisualWorld.h"
#include "../Scene/Models/AreaCapitalAccessories.h"
#include "../Scene/Models/CityGateFlagImports.h"
#include "../Scene/Models/ArmyPopulation.h"
#include "../Scene/Models/ArmyAnimation.h"
#include "../Scene/Models/ArmyGraphicsActions.h"
#include "../Scene/Models/AnimationCadence.h"
#include "../Scene/Models/ArmyWorldMaterialImport.h"
#include "../Scene/Models/ArmyWorldMaterialBank.h"
#include "../Scene/Models/ArmyWorldModeImport.h"
#include "../Scene/Models/ArmyWorldModeBounds.h"
#include "../Scene/Models/ArmyWorldModelPool.h"
#include "../Scene/Models/SharedCategory2Accessories.h"
#include "../Passes/Shadows/SeasonShadow.h"
#include "../Passes/Lighting/SeasonHdrConstants.h"
#include "../Passes/Environment/SeasonAtmosphereConstants.h"
#include "../Passes/PostProcessing/SeasonPostProcess.h"
#include "../Passes/Environment/CloudWeather.h"
#include "../Passes/Environment/AmbientFrame.h"
#include "../Passes/Environment/MoonCamera.h"
#include "../Passes/Environment/HeightFog.h"
#include "../Passes/Forward/PrimaryForwardResourceBank.h"
#include "../Passes/Environment/CloudClip.h"
#include "../Scene/Terrain/SeasonTerrainPalette.h"
#include "../Passes/Effects/EffectAdmission.h"
#include "../Scene/Terrain/GroundDetails.h"
#include "../Scene/Models/FieldDisplay.h"
#include "RenderSession.h"
#include "../Scene/Terrain/TerrainPass.h"
#include "../Device/GBufferTargets.h"
#include "../Passes/Lighting/DeferredLightingPass.h"
#include "../Passes/Environment/AtmospherePass.h"
#include "../Passes/Environment/CloudEnvironmentPass.h"
#include "../Passes/Shadows/ShadowPass.h"
#include "../Passes/PostProcessing/NormalPostProcess.h"
#include "../Scene/Terrain/TerritoryPass.h"
#include "../Scene/Models/ArmyRenderWorld.h"
#include "../Scene/Models/BaseRenderWorld.h"
San14::Render::RenderSession San14::Render::createRenderSession(RenderHost& host, RenderConfiguration configuration) {
    RenderCallbackScope callbacks{host};
    const auto& resources=configuration.resources;
    const auto& input=resources.terrain();
    const auto modelSource=resources.directory(ResourceGroup::Models);
    const auto shadowSource=resources.directory(ResourceGroup::ShadowCasters);
    const bool sourceArmyVisuals=configuration.world.armies;
    const bool sourceAccessoryVisuals=configuration.world.accessories;
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
    {
        std::ifstream reference(resources.directory(ResourceGroup::Stencil)/"terrain-reference.txt");reference>>stencilRef;
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
    width=configuration.width;height=configuration.height;
    ComPtr<ID3D11Device> device;ComPtr<ID3D11DeviceContext> context;D3D_FEATURE_LEVEL level;
    device=host.device;context=host.context;level=device->GetFeatureLevel();
    fs::path lightingSource;
    ComPtr<ID3D11Texture2D> ggx;
    ComPtr<ID3D11Texture2D> litColor,finalColor;
    CloudEnvironmentPass clouds;
    NativeFrameClock commonClock;NativeFrameClockPublication commonPublication;
    UINT commonBaseSettingsFlags=0;
    commonClock=prepareNativeFrameClock(resources.directory(ResourceGroup::FrameClock));
    {
        commonBaseSettingsFlags=commonClock.settingsFlags10;
        commonClock.settingsFlags10=host.input.contains(FrameInputFlag::Paused)?commonBaseSettingsFlags|commonClock.pauseMask:commonBaseSettingsFlags;
        commonPublication=advanceNativeFrameClock(commonClock,host.input.deltaSeconds);
    }
    const auto normalNextSource=resources.directory(ResourceGroup::PostEffects);
    const auto normalInitialSource=normalNextSource/"prior-cpu";
    const float initialCommonClock=commonPublication.common254;

    UINT sourceHostFlags3E4=0,contextFlags3F4=0,actualViewRainFlags19B4=0;
    uint64_t actualHostQ7F8=0;
    {
        std::ifstream entry(resources.directory(ResourceGroup::DeferredLighting)/"main-entry.txt");
        entry>>sourceHostFlags3E4>>actualHostQ7F8>>actualViewRainFlags19B4;
        if(!entry)throw std::runtime_error("Original Main entry fields absent");
    }
    FogAccelerationPass fogAcceleration;NativeSpecialEffects specialEffects;
    NormalPostProcess postProcessing;
    TerritoryPass territory;
    AtmospherePass atmosphere;
    std::optional<StaticModelResources> staticModels;
    BaseRenderWorld bases{sourceAccessoryVisuals};
    source_default_player_phase::Profile defaultPlayerPhases;
    ground_details::GroundDetails groundDetails;
    SceneEnvironment sceneEnvironment;
    unsigned accessoryOrdinaryCount=0;
    ArmyRenderWorld armies{sourceArmyVisuals,sourceAccessoryVisuals};
    ShadowPass shadows;
    const auto jointShadowInput=resources.directory(ResourceGroup::BaseShadows);
    std::vector<unsigned> ordinaryPopulationCounts;
    CameraRig cameraRig;
    const auto& staticCamera=cameraRig.frame();
    const auto& currentDisplay=cameraRig.display();
    San14::Render::FieldDisplay fieldDisplay;
    const auto& acceptedCameraPacket=sceneEnvironment.directCameraPacket();
    NativeDeferredCameraProfile deferredCameraProfile;NativeDeferredCameraFields deferredCameraFields;
    source_weather_fog::Profile weatherCameraProfile;source_weather_fog::Frame weatherCameraFrame;
    source_camera::V3 globalAtmosphereRgb{};
    NativeWeatherFogConsumers weatherConsumers;
    UINT64 completedMainRequests=1;
    int currentRenderMode=0;
    const source_current_season::Frame* currentSeason=nullptr;
    currentSeason=host.currentSeason?&*host.currentSeason:nullptr;
    source_current_season_atmosphere::CloudLightInput currentCloudLights{};
    source_view::Profile viewProfile;source_view::Frame currentView;
    {
        cameraRig.prepare(resources.directory(ResourceGroup::Camera),
            resources.directory(ResourceGroup::DistantMap),resources.directory(ResourceGroup::CameraMotion));
        // Initialize only this renderer's consumed fields from project rules
        // and the current resource declarations; season publishes live fields.
        sceneEnvironment.initialize(SceneEnvironmentPacket::decodeInitial(
            load(resources.directory(ResourceGroup::CameraInitialState)/"profile.bin")));
        // The source map reaches a settled display via its first-dirty updater.
        // Prepare the held World once; accepted camera requests reuse this owner.
        territory.prepareZones(resources.directory(ResourceGroup::TerritoryZones));
        staticModels.emplace(modelSource);
        bases.preparePrimary(device.Get(),resources,host.areaCapitalWorld);
        groundDetails.prepareResources(device.Get(),resources.directory(ResourceGroup::GroundDetails),
            currentSeason?resources.directory(ResourceGroup::SeasonalGroundDetails):fs::path{});
        deferredCameraProfile=prepareNativeDeferredCameraProfile(resources.directory(ResourceGroup::DeferredCamera));
        weatherCameraProfile=source_weather_fog::readProfile(resources.directory(ResourceGroup::Weather)/"initial"/"weather");
        weatherConsumers=prepareNativeWeatherFogConsumers(resources.directory(ResourceGroup::WeatherFog));
        if(sourceAccessoryVisuals||sourceArmyVisuals)
            defaultPlayerPhases=source_default_player_phase::readProfile(
                resources.directory(ResourceGroup::AnimationDefaults)/"default-player-loop-source.bin");
        bases.prepareAccessories(device.Get(),context.Get(),resources,host.areaCapitalWorld,
            defaultPlayerPhases,deferredCameraProfile,weatherConsumers);
        bases.withAccessories([&](auto& cityAccessories,auto& areaAccessories) {
            armies.prepare(device.Get(),context.Get(),resources,cityAccessories,areaAccessories);
        });
        const auto globalRgb=load(resources.directory(ResourceGroup::Weather)/"initial"/"weather"/"effective-light-rgb.bin");
        if(globalRgb.size()!=sizeof(globalAtmosphereRgb))throw std::runtime_error("Original global80 RGB extent differs");
        std::memcpy(&globalAtmosphereRgb,globalRgb.data(),globalRgb.size());
        shadows.preparePopulation(jointShadowInput,staticModels->catalog(),bases.citySource(),bases.primary());
        shadows.bindFieldDisplay(fieldDisplay,cameraRig.distantProfile(),staticModels->catalog());
        shadows.prepareRaster(resources.directory(ResourceGroup::ShadowRaster));
        viewProfile=source_view::loadProfile(resources.directory(ResourceGroup::View)/"profile.bin");
        // Normal ready STA, Camera kind0: D57E50/EBD510 select K1890's
        // original white Static. FE8 belongs to the older empty-registry lab.
        shadows.prepareDirectionalDiffuse(device.Get(),resources.directory(ResourceGroup::ShadowReceivers));
    }
    auto publishCurrentModels=[&]() {
        armies.publishArmy(host.armyWorld);
        bases.publish(host.cityGateWorld,host.areaCapitalWorld,armies);
        if(bases.populationChanged()) {
            shadows.publishPrimaryPopulation(staticModels->catalog(),bases.primary(),
                *staticModels);
        }
        if(sourceArmyVisuals)armies.synchronize(device.Get(),*host.armyWorld);
    };
    auto atmosphereFrame=[&]() {
        return clouds.atmosphereFrame(currentSeason,currentCloudLights,acceptedCameraPacket,
            weatherCameraFrame,globalAtmosphereRgb,staticCamera.prefix);
    };
    auto prepareAcceptedCamera=[&]() {

        territory.setVisible(!host.input.contains(FrameInputFlag::HideTerritory));
        currentSeason=host.currentSeason?&*host.currentSeason:nullptr;
        currentCloudLights={host.input.currentRawSunPosition,host.input.currentCloudGeometry40};
        territory.publishOwnership(context.Get(),host.territoryOwnership);
        publishCurrentModels();
        cameraRig.accept(host.input);
        currentRenderMode=currentDisplay.mode.sceneMode;
        shadows.selectFamily(currentRenderMode);
        fieldDisplay.capture(currentDisplay);
        if(currentSeason) {
            SceneEnvironmentPacket::acceptSeason(sceneEnvironment,*currentSeason);
            shadows.publishSeason(*currentSeason);
            std::memcpy(weatherCameraFrame.main72.data(),currentSeason->main.data()+0x340,72);
        }else {
            const auto directCamera=cameraRig.prepareDirectPrefix();
            sceneEnvironment.acceptCamera(directCamera,SceneEnvironmentPacket::decodeCamera(directCamera));
            weatherCameraFrame=source_weather_fog::prepareCurrent(weatherCameraProfile,staticCamera.eye.Y,sceneEnvironment.camera().weatherHeight);
        }
        atmosphere.publishAcceptedCamera(atmosphereFrame());
        staticModels->selectCamera(staticCamera,fieldDisplay);
        bases.selectCamera(staticCamera);
        shadows.prepareCamera(staticCamera,fieldDisplay);
        ordinaryPopulationCounts=shadows.frame().ordinaryRecordCounts;
        bases.withAccessories([&](auto& cityAccessories,auto& areaAccessories) {
            armies.selectCamera(context.Get(),staticCamera,cityAccessories,areaAccessories,
                viewProfile,ordinaryPopulationCounts,accessoryOrdinaryCount);
        });
        if(ordinaryPopulationCounts.size()!=bases.plan().ordinaryCounts.size())throw std::runtime_error("Field/City ordinary record identities differ");
        // D12A80 consumes this accepted Main, retaining its actual prepared
        // weather/wind tail. Geometry matrices use the ordinary source camera.
        sceneEnvironment.publishVegetationCamera();
        const auto& mode=currentDisplay.mode;
        const auto& groundCaller=bases.citySource().caller;
        const auto& groundPopulation=groundDetails.prepare({staticCamera,
            sceneEnvironment.vegetation(),commonPublication,
            {viewProfile.commonPresent!=0,viewProfile.commonFlags,viewProfile.commonExclusion},commonClock.settingsFlags10,
            {groundCaller.callerFirst,groundCaller.callerSecond,groundCaller.callerShadow,groundCaller.registered3330,groundCaller.successful3340,0},
            {mode.sceneMode,mode.zoomLevel,true,false,0},currentSeason?currentSeason->season:0});
        if(groundPopulation.seasonPopulationChanged)

        for(size_t record=0;record<viewProfile.records.size();++record)
            ordinaryPopulationCounts[record]+=groundDetails.preparedCountForRecord(viewProfile.records[record].mask);
        // The joint source profile already counts every admitted primary World
        // once before its ordinary54/55 split. Do not add the rows again here.
        source_view::Inputs population;
        population.ordinaryCounts=ordinaryPopulationCounts;
        population.collectionPrimitiveCounts=shadows.frame().primitiveCounts;
        population.subWrapperCount1BFC=0; // This finite renderer owns no optional wrapper.
        population.terrainPresent=!staticCamera.instances.empty();
        population.selectedCompactPresent=shadows.frame().passes[0].shadowAvailable!=0;
        population.selectedLitPresent=shadows.directionalView()!=nullptr;
        if(currentSeason) {
            viewProfile.mainA00=shadows.seasonParameters().selectedSignedA00;
            viewProfile.mainA28=shadows.seasonParameters().scaleA28;
            viewProfile.mainA3C=source_current_season_shadow::mainWord<uint32_t>(*currentSeason,0xa3c);
        }
        currentView=source_view::prepareCurrent(viewProfile,population);
        actualViewRainFlags19B4=currentView.rain19B4;
        deferredCameraFields=prepareNativeDeferredCameraFields(staticCamera.prefix);
        fieldDisplay.finishPreparation();

    };
    const auto animationFps=source_animation_cadence::readProfile(
        resources.directory(ResourceGroup::AnimationTiming)/"fps-source.bin");
    armies.requireAnimationTiming(animationFps);
    host.animationUpdate=[&](unsigned fpsMode) {
        if(fpsMode>=animationFps.fps.size())throw std::runtime_error("Actual FPS manager mode range");
        publishCurrentModels();
        const auto eventCamera=cameraRig.publishedEventCamera(host.input);
        const auto zoom=source_camera::Zoom{host.input.band,host.input.fraction,
            source_camera::A(float(host.input.band),source_camera::D(float(host.input.fraction),10000))};
        const auto mode=source_far_main::currentMode(zoom);
        bases.withAccessories([&](auto& cityAccessories,auto& areaAccessories) {
            armies.updateAnimation(device.Get(),context.Get(),host.armyWorld,host.input,eventCamera,mode,
                animationFps,fpsMode,cityAccessories,areaAccessories,host.animationState);
        });
    };
    if(host.input.contains(FrameInputFlag::InitialAnimationUpdate))host.animationUpdate(0); // explicit initial normal Map callback
    host.armyUnitPositions=[&]() {
        if(!sourceArmyVisuals)return std::vector<ArmyLabelUnitPosition>{};
        return armies.labelPositions(*host.armyWorld);
    };
    auto beginAcceptedMain=[&](float currentClock) {

        prepareAcceptedCamera(); // Current Main is available to node1, not only geometry.
        postProcessing.updateMapExposure(context.Get(),currentSeason,
            {sceneEnvironment.camera().exposure,sceneEnvironment.camera().luminance},sceneEnvironment.exposureRectangleFlags());
        fogAcceleration.updateCamera(context.Get(),staticCamera.prefix);
        specialEffects.queueFlags=0;contextFlags3F4=0; // EA9300 accepted node1.

        fogAcceleration.execute(device.Get(),context.Get());
        clouds.updateAcceptedFrame(device.Get(),context.Get(),
            {resources.directory(ResourceGroup::CloudWeather),resources.directory(ResourceGroup::AmbientFrame)},
            {currentSeason,currentCloudLights,currentClock});
    };
    auto publishThinSurfaceFlags=[&](UINT node) {
        if(actualViewRainFlags19B4!=0)specialEffects.queueFlags|=0x400;

    };
    auto finishNormalApply=[&]() {
        actualHostQ7F8|=3; // EB8027/EB8040 retained Host writer.
        specialEffects.queueFlags|=0x20; // E9E6D4 outer193, including zero draw.

    };
    {
        UINT sourceResource,sourceShader,sourceTarget,sourceWidth,sourceHeight;
        std::ifstream sourceExtent(resources.directory(ResourceGroup::PostTargets)/"lighting-target.txt");
        sourceExtent>>sourceResource>>sourceShader>>sourceTarget>>sourceWidth>>sourceHeight;
        if(!sourceExtent||width!=sourceWidth||height!=sourceHeight)throw std::runtime_error("Re-export size-dependent source inputs before resizing the full experiment");
        lightingSource=resources.directory(ResourceGroup::Lighting);
        // Source resource initialization precedes Main traversal. Running it
        // between68 and78 would clear the real receiver's retained state.
        ggx=generateGgx(device.Get(),context.Get(),lightingSource);
        clouds.prepareAmbient(device.Get(),context.Get(),resources.directory(ResourceGroup::Ambient));
        context->ClearState(); // Bootstrap ends before any Main entry; not a per-request operation.
        {

            specialEffects=createNativeSpecialEffectsWithoutParticleOwner(sourceHostFlags3E4);
            fogAcceleration.prepare(device.Get(),resources.directory(ResourceGroup::FogAcceleration));
        }
        postProcessing.prepareExposure(device.Get(),context.Get(),resources.directory(ResourceGroup::MapExposure),normalInitialSource);
        litColor=createSourceHdr(device.Get(),resources.directory(ResourceGroup::DeferredLighting)/"node78-inner02",width,height);
        clouds.prepareClouds(device.Get(),resources.directory(ResourceGroup::Clouds),
            resources.directory(ResourceGroup::CloudAnimation),resources.directory(ResourceGroup::AmbientClouds),
            resources.directory(ResourceGroup::AmbientStars),litColor.Get());
        beginAcceptedMain(initialCommonClock);
    }
    TerrainPass terrainPass{device.Get(),context.Get(),input,groupedTerrain};
    GBufferTargets geometryTargets{device.Get(),context.Get(),
        {width,height,resourceFormats,formats,depthFormat,targetNames,surfaces,
            cull,front,depthCompare,colorMasks,stencilEnable,stencilRef,depthClear}};
    postProcessing.prepareOutput(device.Get(),resources.directory(ResourceGroup::FinalOutput),width,height);
    geometryTargets.prepareExpandedTargets(device.Get(),formats[3]);
    const bool hasVisibleModels=fs::exists(modelSource/"models.txt");
    if(hasVisibleModels){
        {
            staticModels->prepareGeometry(device.Get());
            shadows.connectGeometry(staticModels->geometry(),bases.primary().currentModels);
            staticModels->prepareStencil(device.Get(),resources.directory(ResourceGroup::View),currentView,sourceHostFlags3E4);
            terrainPass.prepareCamera(device.Get(),input,indexCount,
                currentSeason?resources.directory(ResourceGroup::SeasonalResources):fs::path{});
            shadows.prepareGpu(device.Get(),context.Get(),geometryTargets.depthDescription(),stencilRef,jointShadowInput/"gpu");

        }

    }
    // The real caller output survives the nested Main targets and is also
    // the output inherited by normal post109. Retain attachments/rects only.
    {auto raw=postProcessing.outputTarget();context->OMSetRenderTargets(1,&raw,nullptr);}
    context->RSSetViewports(1,&geometryTargets.viewport());context->RSSetScissorRects(1,&geometryTargets.scissor());
    auto callerTargets=NativeMainTargetScope::retain(context.Get());
    NativeSourceDepthState sourceDepth{geometryTargets.depthDescription(),stencilRef};
    auto drawOpaqueGeometry=[&]() {
        geometryTargets.beginGeometry(context.Get());
        {
            const auto& staticSubmission=staticModels->updateSubmission(context.Get(),currentView);
            bases.updateSubmission(context.Get(),staticSubmission);
        }
        {
            groundDetails.update(context.Get());
            auto drawGroundNode=[&](uint32_t node){
                for(size_t record=0;record<viewProfile.records.size();++record){
                    const auto& sourceRecord=viewProfile.records[record];
                    source_view::Primitive primitive{0,0,groundDetails.preparedExclusion(),1u<<record};
                    groundDetails.draw(context.Get(),node,{sourceRecord.mask,currentView.ordinaryCounts[record],sourceHostFlags3E4,
                        (sourceRecord.classification&1)==0,source_view::ordinary54(viewProfile,currentView,primitive,record,sourceHostFlags3E4)},geometryTargets.colors().data(),geometryTargets.depthView(),geometryTargets.viewport());
                }
            };
            bases.withAccessories([&](auto& cityAccessories,auto& areaAccessories) {
                armies.drawOpaque(context.Get(),geometryTargets.colors().data(),geometryTargets.depthView(),
                    geometryTargets.viewport(),cityAccessories,areaAccessories);
            });
            bases.drawOrdinary(context.Get(),geometryTargets.colors().data(),geometryTargets.depthView(),geometryTargets.viewport());
            drawGroundNode(54);
            bases.drawAlpha(context.Get(),geometryTargets.colors().data(),geometryTargets.depthView(),geometryTargets.viewport());
            drawGroundNode(55);
            const auto& detail=groundDetails.currentStatistics();

        }
    {
        NativeTerrainCameraDrawBindings binding;
        binding.colors=geometryTargets.colors();binding.depthTarget=geometryTargets.depthView();binding.viewport=geometryTargets.viewport();
        binding.raster=geometryTargets.rasterState();binding.depthState=geometryTargets.depthState();binding.stencilReference=stencilRef;binding.blend=geometryTargets.blendState();
        terrainPass.draw(context.Get(),staticCamera,
            currentSeason?std::optional<uint32_t>{currentSeason->season}:std::nullopt,binding);
    }
        sourceDepth.bind(context.Get(),geometryTargets.depthState(),geometryTargets.depthDescription(),stencilRef);
    };
    drawOpaqueGeometry();
    auto mainDepth=createNativeDepthMirror(device.Get(),geometryTargets.depthTexture(),shaderFormats[5]);
    updateNativeDepthMirror(context.Get(),mainDepth,"59 after geometry");
    {
        source_territory_gpu::Targets territoryTargets;
        territoryTargets.storage={geometryTargets.textures()[0].Get(),geometryTargets.textures()[2].Get(),geometryTargets.textures()[1].Get(),geometryTargets.textures()[3].Get()};
        territoryTargets.output={geometryTargets.colors()[0],geometryTargets.colors()[2],geometryTargets.colors()[1],geometryTargets.colors()[3]};
        territoryTargets.sampledFormat={shaderFormats[0],shaderFormats[2],shaderFormats[1],shaderFormats[3]};
        territoryTargets.depth=mainDepth.depth.Get();
        territoryTargets.primaryDSV=geometryTargets.depthView();
        territory.prepare(device.Get(),context.Get(),resources.directory(ResourceGroup::Territory),
            territoryTargets,sourceDepth.description,geometryTargets.rasterDescription(),host.territoryOwnership);
    }
    auto drawCurrentTerritory=[&](UINT node,UINT providerDrawFlags){

        territory.draw(context.Get(),cameraRig.zoom().band,cameraRig.zoom().fraction,
            cameraRig.distance(),staticCamera.prefix,sceneEnvironment.territoryReferenceHeight(),
            postProcessing.mapExposureView(),providerDrawFlags,sourceDepth);
        updateNativeDepthMirror(context.Get(),mainDepth,node==122?"122 PB Pop40000007":"60 PB Pop40000007");


    };
    drawCurrentTerritory(60,1);
    publishThinSurfaceFlags(60);
    callerTargets.restore(context.Get());
    OceanPass ocean;
    postProcessing.prepareSeason(resources.directory(ResourceGroup::SeasonalResources),currentSeason);
    auto executeCurrentPost=[&](float currentClock) {
        return postProcessing.execute(device.Get(),context.Get(),sourceDepth,
            resources.directory(ResourceGroup::SeasonalResources),currentSeason,staticCamera.prefix,currentClock);
    };
    ComPtr<ID3D11Texture2D> postInputSnapshot,cloudInputSnapshot;
    NativeSourceDepthState cloudEntryLogical{};
    ComPtr<ID3D11DepthStencilState> cloudEntryDepth;
    ComPtr<ID3D11BlendState> cloudEntryBlend;
    ComPtr<ID3D11RasterizerState> cloudEntryRaster;
    UINT cloudEntryReference=0,cloudEntrySampleMask=~0u;float cloudEntryFactor[4]{};
    const auto receiver68Source=resources.directory(ResourceGroup::ShadowReceivers)/"initial"/"rs68";
    const auto receiver119Source=resources.directory(ResourceGroup::ShadowReceivers)/"initial"/"rs119";
    NativeRs69Fallback fallback69;
    DeferredLightingPass hdr78,hdr123,hdr166;
    auto lightingFrame=[&](const source_water_camera::RefractionDerived* refraction=nullptr) {
        return DeferredLightingFrame{deferredCameraProfile,deferredCameraFields,currentSeason,
            weatherConsumers,weatherCameraFrame,refraction};
    };
    const auto waterCameraInput=resources.directory(ResourceGroup::WaterCamera);
    ComPtr<ID3D11RenderTargetView> indirectTarget;
    NativePrimaryForwardSource primaryForwardSource;
    NativePrimaryForwardGpu primaryForwardGpu;
    NativePrimaryForwardFrame primaryForwardFrame;
    bool primaryForwardReady=false;
    auto drawCurrentPrimaryForward=[&]() {

        if(!primaryForwardReady) {
            const auto directory=resources.directory(ResourceGroup::ForwardLighting);
            primaryForwardSource=prepareNativePrimaryForwardCurrentBankSource(directory);
            primaryForwardGpu=prepareNativePrimaryForwardCurrentBankGpu(device.Get(),context.Get(),directory);

            primaryForwardReady=true;
        }
        auto pass=std::find_if(hdr166.bank().passes.begin(),hdr166.bank().passes.end(),[](auto& p){return p.identity=="node166-inner02";});
        if(pass==hdr166.bank().passes.end())throw std::runtime_error("Forward actual current inner02 producer absent");
        auto globals=std::find_if(pass->constants.begin(),pass->constants.end(),[](auto& cb){return !cb.vertex&&cb.slot==0;});
        if(globals==pass->constants.end())throw std::runtime_error("Forward actual current HDR globals absent");
        primaryForwardFrame=prepareNativePrimaryForwardCurrent(primaryForwardSource,bases.primary().source,bases.plan(),
            sceneEnvironment.camera().eye,globals->bytes.data(),globals->bytes.size(),
            shadows.geometryReceiverConstants().data(),shadows.geometryReceiverConstants().size());
         //Allocation observation, not a source-pixel render.
        executeNativePrimaryForwardGpu(context.Get(),sourceDepth,primaryForwardGpu,primaryForwardFrame,bases.primary().currentModels,
            pass->target.Get(),geometryTargets.depthView(),geometryTargets.viewport(),shadows.shadowView(),shadows.directionalView(),hdr166.bank().ggx.Get(),
            clouds.ambientView(),fogAcceleration.samplingView());

    };
    auto isolateCurrentShadowVisibility=[&]() {

    };
    {
        auto abTarget=geometryTargets.colors()[0];context->OMSetRenderTargets(1,&abTarget,geometryTargets.depthView()); //62
        shadows.prepareContact(device.Get(),resources.directory(ResourceGroup::ContactShadows),sourceDepth,geometryTargets.rasterDescription(),geometryTargets.blendDescription(),
            mainDepth,geometryTargets.textures()[1].Get(),shaderFormats[1],geometryTargets.textures()[0].Get(),geometryTargets.depthView());
        shadows.executeContact(context.Get(),sourceDepth,geometryTargets.viewport());
        D3D11_RENDER_TARGET_VIEW_DESC rsDesc{};rsDesc.Format=formats[2];rsDesc.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
        rsDesc.Texture2DArray.FirstArraySlice=3;rsDesc.Texture2DArray.ArraySize=1;
        check(device->CreateRenderTargetView(geometryTargets.textures()[2].Get(),&rsDesc,&indirectTarget),"Main indirect RS3 target");
    }
    callerTargets.restore(context.Get()); //64
    {auto raw=indirectTarget.Get();context->OMSetRenderTargets(1,&raw,geometryTargets.depthView());} //67
    {
        shadows.prepareGeometryReceiver(device.Get(),context.Get(),receiver68Source,input/"rs68"/"directional-diffuse.tex",
            geometryTargets.textures()[2].Get(),formats[2],width,height,currentSeason,staticCamera);
        shadows.executeGeometryReceiver(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView());
    }
    fallback69=prepareRs69Fallback(device.Get(),input,geometryTargets.textures()[2].Get(),formats[2],width,height);
    executeRs69Fallback(device.Get(),context.Get(),sourceDepth,fallback69,geometryTargets.depthView());
    isolateCurrentShadowVisibility();
    callerTargets.restore(context.Get()); //70
    DXGI_FORMAT lightingShaderFormat=DXGI_FORMAT_UNKNOWN,lightingTargetFormat=DXGI_FORMAT_UNKNOWN;
    {
        {
            auto earlySource=resources.directory(ResourceGroup::DeferredLighting);
            D3D11_RENDER_TARGET_VIEW_DESC hdrDesc{};hdrDesc.Format=DXGI_FORMAT_R11G11B10_FLOAT;hdrDesc.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
            ComPtr<ID3D11RenderTargetView> hdrTarget;check(device->CreateRenderTargetView(litColor.Get(),&hdrDesc,&hdrTarget),"Main73 retained HDR target");
            auto mainTargets=NativeMainTargetScope::main73(hdrTarget.Get(),geometryTargets.depthView(),geometryTargets.viewport(),geometryTargets.scissor());
            mainTargets.restore(context.Get());
            hdr78.prepare(device.Get(),earlySource,"passes.txt",litColor.Get(),geometryTargets.textures(),shaderFormats,ggx.Get(),sourceAccessoryVisuals?resources.directory(ResourceGroup::BaseAccessories)/"hdr/initial":fs::path{});
            {
                hdr78.update(context.Get(),lightingFrame());
            }
            hdr78.execute(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView(),clouds.ambientView(),nullptr,nullptr,
                nullptr,nullptr,initialCommonClock,currentView.flags19AC,currentView.ocean19B0);
            mainTargets.restore(context.Get());
            lightingShaderFormat=lightingTargetFormat=DXGI_FORMAT_R11G11B10_FLOAT;
            {
                ocean.prepareSceneCopy(device.Get(),context.Get(),sourceDepth,
                    resources.directory(ResourceGroup::Refraction)/"copy-scene97",litColor.Get(),lightingShaderFormat);
            }
            ocean.prepareDepthCopy(device.Get(),context.Get(),sourceDepth,
                resources.directory(ResourceGroup::Refraction)/"copy97",mainDepth.depth.Get(),width,height);
            mainTargets.restore(context.Get());
            {
                const OceanSources oceanSources{resources.directory(ResourceGroup::OceanTextures),
                    resources.directory(ResourceGroup::Water),waterCameraInput,resources.directory(ResourceGroup::OceanWaves),
                    resources.directory(ResourceGroup::OceanClock),resources.directory(ResourceGroup::Refraction)/"copy97"};
                const OceanTargets oceanTargets{geometryTargets.textures(),geometryTargets.colors(),formats[3],geometryTargets.depthView(),width,height};
                ocean.prepareWater(device.Get(),context.Get(),oceanSources,oceanTargets,staticCamera.prefix,initialCommonClock);
                ocean.executeWater(device.Get(),context.Get(),sourceDepth,initialCommonClock);
                ;
                updateNativeDepthMirror(context.Get(),mainDepth,"117 after Ocean");
                mainTargets.restore(context.Get());
            }
            {
                // 118 binds the existing RS3 and water-updated Main D1 without
                // clearing. 119 selects the Ocean group (process50). The same
                // explicitly empty SDF owner used at69 preserves RG at120;
                // 121 pops this target scope without copying depth.
                auto rsTarget=indirectTarget.Get();context->OMSetRenderTargets(1,&rsTarget,geometryTargets.depthView()); //118
                shadows.prepareWaterReceiver(device.Get(),context.Get(),receiver119Source,
                    geometryTargets.textures()[2].Get(),formats[2],width,height,currentSeason,staticCamera);
                shadows.executeWaterReceiver(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView());
                isolateCurrentShadowVisibility();
                mainTargets.restore(context.Get()); //121
                drawCurrentTerritory(122,2);
                publishThinSurfaceFlags(122);
                hdr123.prepare(device.Get(),earlySource,"ocean-passes.txt",litColor.Get(),geometryTargets.textures(),shaderFormats,ggx.Get());
                {
                    hdr123.initializeRefractionFrame(context.Get(),lightingFrame(),waterCameraInput/"receiver-refraction-writes.txt",ocean.refractionCamera());
                }
                hdr123.execute(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView(),clouds.ambientView(),ocean.refractionDepth(),ocean.transmittedScene(),
                    nullptr,nullptr,initialCommonClock,currentView.flags19AC,currentView.ocean19B0);
                mainTargets.restore(context.Get());
                atmosphere.prepare(device.Get(),context.Get(),resources.directory(ResourceGroup::Sky),
                    resources.directory(ResourceGroup::Atmosphere),resources.directory(ResourceGroup::AtmosphereCamera)/"initial",
                    litColor.Get(),mainDepth.depth.Get(),geometryTargets.depthView(),width,height,atmosphereFrame());
                atmosphere.executeSky(device.Get(),context.Get(),sourceDepth);
                mainTargets.restore(context.Get());

                clouds.execute(device.Get(),context.Get(),sourceDepth,geometryTargets.depthView(),initialCommonClock,UINT(actualHostQ7F8));
                mainTargets.restore(context.Get());
                atmosphere.executeFog(device.Get(),context.Get(),sourceDepth,
                    resources.directory(ResourceGroup::HeightFog)/"source-profile.bin",currentSeason,initialCommonClock,
                    std::optional<unsigned>(currentView.flags19AC));
                ;
                mainTargets.restore(context.Get());
                ocean.prepareFilters(device.Get(),resources.directory(ResourceGroup::Refraction)/"filter137",litColor.Get(),lightingShaderFormat,mainDepth.depth.Get());
                ocean.executeFilters(context.Get(),sourceDepth);
                ;
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
                context->OMSetRenderTargets(0,nullptr,ocean.depthTarget()); //138
                ;
                applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,141);
                mainTargets.restore(context.Get()); //142, B616 has no mirror
                geometryTargets.sixTargetScope().restore(context.Get()); //143
                std::array<ID3D11RenderTargetView*,6> sixExpected{};
                for(UINT i=0;i<6;++i)sixExpected[i]=geometryTargets.sixTargetScope().colors[i].Get();
                ;
                applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,147);
                updateNativeDepthMirror(context.Get(),mainDepth,"148 after source special zero-tail state");
                mainTargets.restore(context.Get());
                // Q400 closes150/154;142 belongs to distinct B616, not MainD1.
                geometryTargets.sixTargetScope().restore(context.Get()); //156
                ;
                updateNativeDepthMirror(context.Get(),mainDepth,"160 Rain writers pending");
                mainTargets.restore(context.Get());
                publishThinSurfaceFlags(161);
                //162/165 retain their source RS3 scope; this finite cohort
                //has no process60 writer between them.
                context->OMSetRenderTargets(1,&rsTarget,geometryTargets.depthView());
                mainTargets.restore(context.Get());
                hdr166.prepare(device.Get(),earlySource,"late-passes.txt",litColor.Get(),geometryTargets.textures(),shaderFormats,ggx.Get(),sourceAccessoryVisuals?resources.directory(ResourceGroup::BaseAccessories)/"hdr/initial":fs::path{});
                {
                    hdr166.initializeRefractionFrame(context.Get(),lightingFrame(),waterCameraInput/"receiver-refraction-writes.txt",ocean.refractionCamera());
                }
                hdr166.execute(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView(),clouds.ambientView(),ocean.refractionDepth(),ocean.transmittedScene(),
                    ocean.filteredSpecular(),ocean.blurredDepth(),initialCommonClock,currentView.flags19AC,currentView.ocean19B0);
                mainTargets.restore(context.Get()); //165 and late HDR return to73
                ;
                {
                    drawCurrentPrimaryForward(); //184..187, before Effects188 and mirror191.
                    //188 reads160.189's independent View1 remains outside this
                    //cohort; do not duplicate View0's population into it.

                    updateNativeDepthMirror(context.Get(),mainDepth,"191 before normal post; particle owner pending");
                    groundDetails.finishRenderNode();
                    callerTargets.restore(context.Get());
                    D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.Format=lightingShaderFormat;sv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sv.Texture2D.MipLevels=1;
                    ComPtr<ID3D11ShaderResourceView> scene;check(device->CreateShaderResourceView(litColor.Get(),&sv,&scene),"normal post retained HDR input");
                    postProcessing.prepare(device.Get(),normalInitialSource,
                        resources.directory(ResourceGroup::PostAnimation),resources.directory(ResourceGroup::PostCamera),
                        scene.Get(),mainDepth.depth.Get(),width,height);

                    finalColor=executeCurrentPost(initialCommonClock);
                    finishNormalApply();
                    {
                        const auto postCaller=NativeMainTargetScope::retain(context.Get());
                        auto sameOutput=postProcessing.outputTarget();
                        context->OMSetRenderTargets(1,&sameOutput,geometryTargets.depthView()); //194, no clear
                        ;

                        postCaller.restore(context.Get()); //203
                        ComPtr<ID3D11DepthStencilView> tailDepth;context->OMGetRenderTargets(0,nullptr,&tailDepth);
                        //204 is open;205 makes no attachment change.206 only
                        //clears depth/stencil if the actual caller has a DSV.
                        if(tailDepth)context->ClearDepthStencilView(tailDepth.Get(),D3D11_CLEAR_DEPTH|D3D11_CLEAR_STENCIL,depthClear,0);

                    }
                }
            }
        }

    }
        auto executeRetainedMain=[&](float currentClock) {
        {
            // Normal1184320: publish completed geometry BEFORE the shared
            // grid clock writer starts the next job. No frame-number gate.
            ocean.publishCompletedGeometry(context.Get(),currentClock);

        }
        beginAcceptedMain(currentClock);
        {
            shadows.updateAcceptedFrame(context.Get(),sourceDepth,bases.populationChanged(),currentSeason,staticCamera);
        }
        drawOpaqueGeometry();updateNativeDepthMirror(context.Get(),mainDepth,"59 new Main geometry");

        drawCurrentTerritory(60,1);
        publishThinSurfaceFlags(60);
        callerTargets.restore(context.Get());
        auto abTarget=geometryTargets.colors()[0];context->OMSetRenderTargets(1,&abTarget,geometryTargets.depthView());
        shadows.executeContact(context.Get(),sourceDepth,geometryTargets.viewport());callerTargets.restore(context.Get());
        auto rsTarget=indirectTarget.Get();context->OMSetRenderTargets(1,&rsTarget,geometryTargets.depthView());
        shadows.executeGeometryReceiver(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView());
        executeRs69Fallback(device.Get(),context.Get(),sourceDepth,fallback69,geometryTargets.depthView());callerTargets.restore(context.Get());
        isolateCurrentShadowVisibility();
        auto mainTargets=NativeMainTargetScope::main73(postProcessing.sceneTarget(),geometryTargets.depthView(),geometryTargets.viewport(),geometryTargets.scissor());
        mainTargets.restore(context.Get());
        {
            hdr78.update(context.Get(),lightingFrame());
        }
        hdr78.execute(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView(),clouds.ambientView(),nullptr,nullptr,
            nullptr,nullptr,currentClock,currentView.flags19AC,currentView.ocean19B0);
        mainTargets.restore(context.Get());
        ocean.copyScene(context.Get(),sourceDepth);
        ocean.copyDepth(context.Get(),sourceDepth);mainTargets.restore(context.Get());
        ocean.updateCamera(context.Get(),staticCamera.prefix);
        ocean.executeWater(device.Get(),context.Get(),sourceDepth,currentClock);
        updateNativeDepthMirror(context.Get(),mainDepth,"117 new Main Ocean");mainTargets.restore(context.Get());
        context->OMSetRenderTargets(1,&rsTarget,geometryTargets.depthView());
        shadows.executeWaterReceiver(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView());mainTargets.restore(context.Get());
        isolateCurrentShadowVisibility();
        drawCurrentTerritory(122,2);
        publishThinSurfaceFlags(122);
        {
            hdr123.update(context.Get(),lightingFrame(&ocean.refractionCamera()));
        }
        hdr123.execute(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView(),clouds.ambientView(),ocean.refractionDepth(),ocean.transmittedScene(),
            nullptr,nullptr,currentClock,currentView.flags19AC,currentView.ocean19B0);
        atmosphere.updateCamera(context.Get(),atmosphereFrame());
        mainTargets.restore(context.Get());atmosphere.executeSky(device.Get(),context.Get(),sourceDepth);mainTargets.restore(context.Get());
        clouds.execute(device.Get(),context.Get(),sourceDepth,geometryTargets.depthView(),currentClock,UINT(actualHostQ7F8));mainTargets.restore(context.Get());
        atmosphere.executeFog(device.Get(),context.Get(),sourceDepth,
            resources.directory(ResourceGroup::HeightFog)/"source-profile.bin",currentSeason,currentClock,currentView.flags19AC);mainTargets.restore(context.Get());
        ocean.executeFilters(context.Get(),sourceDepth);mainTargets.restore(context.Get());
        std::array<ID3D11ShaderResourceView*,32> empty{};context->VSSetShaderResources(0,32,empty.data());context->PSSetShaderResources(0,32,empty.data());
        context->OMSetRenderTargets(0,nullptr,ocean.depthTarget());applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,141);
        mainTargets.restore(context.Get());geometryTargets.sixTargetScope().restore(context.Get());applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,147);
        updateNativeDepthMirror(context.Get(),mainDepth,"148 new Main special");mainTargets.restore(context.Get());
        geometryTargets.sixTargetScope().restore(context.Get());updateNativeDepthMirror(context.Get(),mainDepth,"160 new Main rain source-closed");mainTargets.restore(context.Get());
        publishThinSurfaceFlags(161);
        context->OMSetRenderTargets(1,&rsTarget,geometryTargets.depthView());mainTargets.restore(context.Get());
        {
            hdr166.update(context.Get(),lightingFrame(&ocean.refractionCamera()));
        }
        hdr166.execute(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView(),clouds.ambientView(),ocean.refractionDepth(),ocean.transmittedScene(),ocean.filteredSpecular(),ocean.blurredDepth(),
            currentClock,currentView.flags19AC,currentView.ocean19B0);
        drawCurrentPrimaryForward();

        mainTargets.restore(context.Get());

        updateNativeDepthMirror(context.Get(),mainDepth,"191 new Main; particle owner pending");callerTargets.restore(context.Get());
        groundDetails.finishRenderNode();
        executeCurrentPost(currentClock);
        finishNormalApply();
        //194/203 alias the same Final109;206 remains closed with null callerDSV.
        const auto postCaller=NativeMainTargetScope::retain(context.Get());auto sameOutput=postProcessing.outputTarget();
        context->OMSetRenderTargets(1,&sameOutput,geometryTargets.depthView());postCaller.restore(context.Get());
        ++completedMainRequests;

        };
        {
            // All initialized owners remain in this coroutine frame. Godot
            // drives subsequent requests; no native window or polling thread.
            co_yield RenderOutput{finalColor.Get(),staticCamera.prefix,completedMainRequests};
            const UINT baseFlags=commonBaseSettingsFlags;
            for(;;) {
                commonClock.settingsFlags10=host.input.contains(FrameInputFlag::Paused)?baseFlags|commonClock.pauseMask:baseFlags;
                commonPublication=advanceNativeFrameClock(commonClock,host.input.deltaSeconds);
                executeRetainedMain(commonPublication.common254);
                co_yield RenderOutput{finalColor.Get(),staticCamera.prefix,completedMainRequests};
            }
        }
    co_return 0;
}
