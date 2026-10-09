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
#include "../Camera/MainCameraInitialNumeric.h"
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
#include "../Passes/Environment/FogAccelerationRenderer.h"
#include "../Passes/Environment/FogAccelerationConstants.h"
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
    NativeFogAcceleration fogAcceleration;NativeSpecialEffects specialEffects;
    NormalPostProcess postProcessing;
    TerritoryPass territory;
    float heldMainB1C=0;
    source_aux_camera::FogAccelerationConstants fogCamera;
    AtmospherePass atmosphere;
    std::optional<StaticModelResources> staticModels;
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
    ShadowPass shadows;
    const auto currentPrimaryInput=resources.directory(ResourceGroup::BaseVisuals)/"primary-templates";
    const auto jointShadowInput=resources.directory(ResourceGroup::BaseShadows);
    NativeCityModelQueues ordinaryModelQueues;
    std::vector<unsigned> ordinaryPopulationCounts;
    source_camera::Profile staticCameraProfile;source_camera::Frame staticCamera;
    source_camera_motion::Profile cameraMotionProfile;source_camera_motion::Runtime cameraMotion;
    auto& staticCameraRuntime=cameraMotion.camera;
    source_camera::GroundHeightProfile cameraGround;
    source_camera_motion::TerrainQualifier cameraTerrainQualifier;
    NativeCameraClock cameraClock;
    source_camera::RenderModePrefixProfile mainDirectProfile;source_camera::RenderModePrefixState mainDirectState;
    source_far_main::Profile farMainProfile;source_far_main::Owner farMainOwner;
    source_far_main::Current currentDisplay;
    San14::Render::FieldDisplay fieldDisplay;
    source_camera::Prefix mainDirectPrefix{};
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
        staticCameraProfile=source_camera::loadProfile(resources.directory(ResourceGroup::Camera)/"profile.bin");
        mainDirectProfile=source_camera::loadRenderModePrefixProfile(resources.directory(ResourceGroup::Camera)/"render-mode-prefix-profile.bin");
        mainDirectState=source_camera::createRenderModePrefixState(mainDirectProfile);
        farMainProfile=source_far_main::loadProfile(resources.directory(ResourceGroup::DistantMap)/"profile.bin");
        farMainOwner=source_far_main::createOwner(farMainProfile);
        cameraMotionProfile=source_camera_motion::loadProfile(resources.directory(ResourceGroup::CameraMotion)/"motion-profile.bin");
        cameraMotion=source_camera_motion::createRuntime(staticCameraProfile);
        cameraGround=source_camera::loadGroundHeightProfile(resources.directory(ResourceGroup::Camera)/"ground-height-current");
        cameraTerrainQualifier=[&](int band,const source_camera::State& candidate,double minimum,double maximum,double requested,double& pitch){
            auto result=source_camera::qualifyTerrainPitch(cameraGround,{band,candidate,minimum,maximum,requested});pitch=result.pitch;return result.accepted;
        };
        cameraClock=prepareNativeCameraClock();
        // Initialize only this renderer's consumed fields from project rules
        // and the current resource declarations; season publishes live fields.
        currentMain18C0=own_main_init::decode(load(resources.directory(ResourceGroup::CameraInitialState)/"profile.bin"));
        heldMainB1C=source_camera::get<float>(reinterpret_cast<const uint8_t*>(currentMain18C0.data())+0xb1c);
        // The source map reaches a settled display via its first-dirty updater.
        // Prepare the held World once; accepted camera requests reuse this owner.
        territory.prepareZones(resources.directory(ResourceGroup::TerritoryZones));
        staticModels.emplace(modelSource);
        cityModels=prepareNativeCityPopulation(device.Get(),currentPrimaryInput);
        if(!host.areaCapitalWorld)throw std::runtime_error("Native Main requires its actual Area Capital publication");
        const auto& initialAreas=*host.areaCapitalWorld;
        areaModels=prepareNativeAreaPrimaryPopulation(device.Get(),cityModels,
            prepareNativeAreaPrimarySource(cityModels.source,
                readNativeBaseVisualPrimary(initialAreas.models.primary.data(),initialAreas.models.primary.size()),
                initialAreas.identities,cityModels.source.caller));
        publishNativeCityAreaPrimary(primaryModels,cityModels,areaModels);
        const auto groundInput=resources.directory(ResourceGroup::GroundDetails);
        std::ifstream groundReference(groundInput/"source-directory.txt");std::string groundRelative;
        if(!std::getline(groundReference,groundRelative)||groundRelative.empty())throw std::runtime_error("Ground detail source directory import absent");
        groundDetails.loadOnce(device.Get(),groundInput/fs::path(groundRelative),
            currentSeason?resources.directory(ResourceGroup::SeasonalGroundDetails):fs::path{});
        std::ifstream groundSettings(groundInput/"common-source.txt");
        groundSettings>>groundSourceFlags14>>groundFrustumType64>>groundCulling450>>groundScanning454>>std::hex>>groundAppBaseFlags;
        if(!groundSettings)throw std::runtime_error("Ground detail saved Common/App import absent");
        deferredCameraProfile=prepareNativeDeferredCameraProfile(resources.directory(ResourceGroup::DeferredCamera));
        weatherCameraProfile=source_weather_fog::readProfile(resources.directory(ResourceGroup::Weather)/"initial"/"weather");
        weatherConsumers=prepareNativeWeatherFogConsumers(resources.directory(ResourceGroup::WeatherFog));
        if(sourceAccessoryVisuals||sourceArmyVisuals)
            defaultPlayerPhases=source_default_player_phase::readProfile(
                resources.directory(ResourceGroup::AnimationDefaults)/"default-player-loop-source.bin");
        if(sourceAccessoryVisuals) {
            const auto accessorySource=resources.directory(ResourceGroup::BaseAccessories);
            cityAccessories=prepareNativeCityAccessoryGpu(device.Get(),context.Get(),accessorySource,
                false
                );
            if(cityAccessories.poseSource.duration8!=defaultPlayerPhases.categories[2].duration8)
                throw std::runtime_error("Accessory source clip and default player duration differ");
            cityFlagImports=San14::Render::loadCityGateFlagImports(accessorySource/"material-source",cityAccessories);
            areaAccessories=prepareNativeAreaCapitalAccessories(cityAccessories,
                readNativeAreaCapitalAccessories(host.areaCapitalWorld->models.accessories));
            auto camera=prepareNativeDeferredCameraProfile(accessorySource/"hdr/initial/camera-source");
            deferredCameraProfile.constants.merge(camera.constants);
            auto weather=prepareNativeWeatherFogConsumers(accessorySource/"hdr/initial/weather-source");
            weatherConsumers.insert(weatherConsumers.end(),weather.begin(),weather.end());
        }
        if(sourceArmyVisuals
            ||sourceAccessoryVisuals
            ) {
            const auto armyDirectory=resources.directory(ResourceGroup::Armies);
            armyModels=prepareNativeArmyPopulation(device.Get(),context.Get(),armyDirectory,false);
            const auto materials=loadArmyWorldMaterialImport(armyDirectory,armyModels.source.templateCount);
            armyWorldTemplates.emplace(armyModels.source,materials.materialTemplates,
                captureCompletedArmyWorldMaterials(armyModels,materials));
            prepareArmyWorldMaterialBank(device.Get(),context.Get(),armyDirectory,armyModels,*armyWorldTemplates,materials);
            armyWorldTemplates->constructors=readArmyWorldConstructors(resources.directory(ResourceGroup::ArmyActions)/"constructor-worlds.bin");
            auto modelCapacities=readArmyWorldPoolCapacities(resources.directory(ResourceGroup::ArmyActions)/"pool-banks.bin");
            armyGraphicsActions=loadNativeArmyGraphicsActions(resources.directory(ResourceGroup::ArmyActions),armyModels.source);
            for(unsigned category=4;category<=9;++category){
                const auto directory=category<7?"army-mode-category"+std::to_string(category):
                    "army-naval-category-"+std::to_string(category);
                auto resource=importArmyWorldModeResources(device.Get(),resources.root()/directory,
                    armyModels.source,armyWorldTemplates->tableOwners);
                appendArmyWorldModeResources(std::move(resource),armyModels,*armyWorldTemplates,*armyGraphicsActions,modelCapacities);
            }
            armyModelPool.emplace(modelCapacities,
                armyWorldTemplates->constructors,armyModels.source.poses,armyModels.source.defaultActions);
            if(sourceAccessoryVisuals){
                cityAccessories.templates.blend=armyModels.templates.blend;
                for(const auto& primitive:armyWorldTemplates->primitives[2])
                    cityAccessories.templates.draws.push_back(armyModels.templates.draws.at(primitive.sourceTemplate));
                areaAccessories.templates=cityAccessories.templates;
                sharedCityAccessories=prepareNativeSharedAccessories(cityAccessories.population,false);
                sharedAreaAccessories=prepareNativeSharedAccessories(areaAccessories.population,true);
            }
            armyReturnedPhases.assign(armyModels.source.actors.size(),0.f);
        }
        const auto globalRgb=load(resources.directory(ResourceGroup::Weather)/"initial"/"weather"/"effective-light-rgb.bin");
        if(globalRgb.size()!=sizeof(globalAtmosphereRgb))throw std::runtime_error("Original global80 RGB extent differs");
        std::memcpy(&globalAtmosphereRgb,globalRgb.data(),globalRgb.size());
        shadows.preparePopulation(jointShadowInput,staticModels->catalog(),cityModels.source,primaryModels);
        shadows.bindFieldDisplay(fieldDisplay,farMainProfile,staticModels->catalog());
        shadows.prepareRaster(resources.directory(ResourceGroup::ShadowRaster));
        viewProfile=source_view::loadProfile(resources.directory(ResourceGroup::View)/"profile.bin");
        // Normal ready STA, Camera kind0: D57E50/EBD510 select K1890's
        // original white Static. FE8 belongs to the older empty-registry lab.
        shadows.prepareDirectionalDiffuse(device.Get(),resources.directory(ResourceGroup::ShadowReceivers));
    }
    auto syncArmyModels=[&]() {
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

    };
    auto publishCurrentModels=[&]() {
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
            shadows.publishPrimaryPopulation(staticModels->catalog(),primaryModels,
                *staticModels);

        }
        syncArmyModels();
    };
    auto atmosphereFrame=[&]() {
        return clouds.atmosphereFrame(currentSeason,currentCloudLights,mainDirectPrefix,
            weatherCameraFrame,globalAtmosphereRgb,staticCamera.prefix);
    };
    auto prepareAcceptedCamera=[&]() {

        territory.setVisible(!host.input.contains(FrameInputFlag::HideTerritory));
        currentSeason=host.currentSeason?&*host.currentSeason:nullptr;
        currentCloudLights={host.input.currentRawSunPosition,host.input.currentCloudGeometry40};
        territory.publishOwnership(context.Get(),host.territoryOwnership);
        publishCurrentModels();
        if(host.input.contains(FrameInputFlag::PublishedCamera)) {
            staticCameraRuntime.pose=host.input.controller;
            staticCameraRuntime.zoom={host.input.band,host.input.fraction,
                source_camera::A(float(host.input.band),source_camera::D(float(host.input.fraction),10000))};
            staticCamera=source_camera::preparePublished(staticCameraProfile,host.input.camera,host.input.controller);
        }else {
        const int32_t inertiaClock=sampleNativeCameraClock(cameraClock);
        const int32_t animationClock=sampleNativeCameraClock(cameraClock);
        source_camera_motion::tick(staticCameraProfile,cameraMotionProfile,cameraMotion,inertiaClock,animationClock,cameraMotionProfile.inertiaTimeout,cameraTerrainQualifier);
        staticCamera=source_camera::prepareCurrent(staticCameraProfile,staticCameraRuntime);
        }
        currentDisplay=source_far_main::prepareCurrent(farMainProfile,farMainOwner,staticCameraRuntime.zoom,staticCamera.prefix);
        currentRenderMode=currentDisplay.mode.ownerMode288;
        shadows.selectFamily(currentRenderMode);
        fieldDisplay.capture(currentDisplay);
        if(currentSeason) {
            mainDirectPrefix=currentSeason->main;
            source_current_season::publishMain(*currentSeason,
                {reinterpret_cast<uint8_t*>(currentMain18C0.data()),currentMain18C0.size()});
            shadows.publishSeason(*currentSeason);
            std::memcpy(weatherCameraFrame.main72.data(),currentSeason->main.data()+0x340,72);
        }else {
            mainDirectPrefix=source_camera::prepareCurrentRenderModeDirectPrefix(mainDirectProfile,mainDirectState,
                source_camera::prepareCurrentRenderMode(staticCameraRuntime.zoom),staticCamera.prefix);
            weatherCameraFrame=source_weather_fog::prepareCurrent(weatherCameraProfile,staticCamera.eye.Y,source_camera::get<float>(mainDirectPrefix.data()+0x210));
        }
        atmosphere.publishAcceptedCamera(atmosphereFrame());
        staticModels->selectCamera(staticCamera,fieldDisplay);
        cityPlan=selectNativeCityPopulation(primaryModels.source,staticCamera);
        shadows.prepareCamera(staticCamera,fieldDisplay);
        ordinaryPopulationCounts=shadows.frame().ordinaryRecordCounts;
        if(sourceAccessoryVisuals) {
            accessoryOrdinaryCount=selectNativeSharedAccessories(context.Get(),*armyModelPool,
                sharedAreaAccessories,areaAccessories,staticCamera);
            accessoryOrdinaryCount+=selectNativeSharedAccessories(context.Get(),*armyModelPool,
                sharedCityAccessories,cityAccessories,staticCamera);
            for(unsigned i=0;i<viewProfile.records.size();++i)
                if(viewProfile.records[i].mask==1)ordinaryPopulationCounts[i]+=accessoryOrdinaryCount;
        }
        if(sourceArmyVisuals) {
            armyPlan=selectNativeArmyPopulation(armyModels,staticCamera,armyReturnedPhases,
                &armySuccessfulSamples,&armyActivePoses,&armyMainMembership
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
    if(host.input.contains(FrameInputFlag::InitialAnimationUpdate))host.animationUpdate(0); // explicit initial normal Map callback
    host.armyUnitPositions=[&]() {
        if(!sourceArmyVisuals)return std::vector<ArmyLabelUnitPosition>{};
        return readArmyLabelUnitPositions(host.armyWorld->unitCenters,armyUnitHeights);
    };
    auto beginAcceptedMain=[&](float currentClock) {

        prepareAcceptedCamera(); // Current Main is available to node1, not only geometry.
        postProcessing.updateMapExposure(context.Get(),currentSeason,
            reinterpret_cast<const uint8_t*>(currentMain18C0.data()),mainDirectPrefix);
        updateNativeFogAccelerationCamera(context.Get(),fogCamera,fogAcceleration,staticCamera.prefix);
        specialEffects.queueFlags=0;contextFlags3F4=0; // EA9300 accepted node1.

        executeNativeFogAcceleration(device.Get(),context.Get(),fogAcceleration);
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
            fogAcceleration=prepareNativeFogAcceleration(device.Get(),resources.directory(ResourceGroup::FogAcceleration));
            fogCamera=prepareNativeFogAccelerationCamera(device.Get(),resources.directory(ResourceGroup::FogAcceleration),fogAcceleration);
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
            shadows.connectGeometry(staticModels->geometry(),primaryModels.currentModels);
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
            auto cityQueues=updateSelectNativeCityAreaPrimary(context.Get(),primaryModels,cityPlan);
            ordinaryModelQueues=mergeNativeCityPopulationWithField54(staticSubmission.plan,staticSubmission.models,primaryModels.source,cityPlan,cityQueues);
        }
        {
            groundDetails.update(context.Get());
            auto drawGroundNode=[&](uint32_t node){
                for(size_t record=0;record<viewProfile.records.size();++record){
                    const auto& sourceRecord=viewProfile.records[record];
                    source_view::Primitive primitive{0,0,ground_detail_source::effectiveExclusion(
                        {viewProfile.commonPresent!=0,viewProfile.commonFlags,groundDetails.currentStatistics().currentAux14,viewProfile.commonExclusion,groundFrustumType64}),1u<<record};
                    groundDetails.draw(context.Get(),node,{sourceRecord.mask,currentView.ordinaryCounts[record],sourceHostFlags3E4,
                        (sourceRecord.classification&1)==0,source_view::ordinary54(viewProfile,currentView,primitive,record,sourceHostFlags3E4)},geometryTargets.colors().data(),geometryTargets.depthView(),geometryTargets.viewport());
                }
            };
            if(sourceArmyVisuals) {
                armyQueues=updateSelectNativeArmyPopulation(context.Get(),armyModels,armyPlan);
                drawNativeModels(context.Get(),armyQueues.ordinary49,geometryTargets.colors().data(),geometryTargets.depthView(),geometryTargets.viewport());
                drawNativeModels(context.Get(),armyQueues.alpha50,geometryTargets.colors().data(),geometryTargets.depthView(),geometryTargets.viewport());
                drawNativeModels(context.Get(),armyQueues.ordinary54,geometryTargets.colors().data(),geometryTargets.depthView(),geometryTargets.viewport());
                drawNativeModels(context.Get(),armyQueues.alpha55,geometryTargets.colors().data(),geometryTargets.depthView(),geometryTargets.viewport());

            }
            if(sourceAccessoryVisuals) {
                drawNativeSharedAccessories(context.Get(),*armyModelPool,sharedAreaAccessories,areaAccessories,geometryTargets.colors().data(),geometryTargets.depthView(),geometryTargets.viewport());
                drawNativeSharedAccessories(context.Get(),*armyModelPool,sharedCityAccessories,cityAccessories,geometryTargets.colors().data(),geometryTargets.depthView(),geometryTargets.viewport());
            }
            drawNativeModels(context.Get(),ordinaryModelQueues.ordinary54,geometryTargets.colors().data(),geometryTargets.depthView(),geometryTargets.viewport());
            drawGroundNode(54);
            drawNativeModels(context.Get(),ordinaryModelQueues.alpha55,geometryTargets.colors().data(),geometryTargets.depthView(),geometryTargets.viewport());
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

        territory.draw(context.Get(),staticCameraRuntime.zoom.band,staticCameraRuntime.zoom.fraction,
            staticCameraRuntime.pose.distance,staticCamera.prefix,heldMainB1C,
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
        primaryForwardFrame=prepareNativePrimaryForwardCurrent(primaryForwardSource,primaryModels.source,cityPlan,
            mainDirectPrefix.data(),globals->bytes.data(),globals->bytes.size(),
            shadows.geometryReceiverConstants().data(),shadows.geometryReceiverConstants().size());
         //Allocation observation, not a source-pixel render.
        executeNativePrimaryForwardGpu(context.Get(),sourceDepth,primaryForwardGpu,primaryForwardFrame,primaryModels.currentModels,
            pass->target.Get(),geometryTargets.depthView(),geometryTargets.viewport(),shadows.shadowView(),shadows.directionalView(),hdr166.bank().ggx.Get(),
            clouds.ambientView(),fogAcceleration.view.Get());

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
            shadows.updateAcceptedFrame(context.Get(),sourceDepth,primaryPopulationChanged,currentSeason,staticCamera);
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
