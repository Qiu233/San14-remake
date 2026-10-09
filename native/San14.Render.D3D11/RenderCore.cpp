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
#include "RenderSession.h"
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
    ComPtr<ID3D11Texture2D> ggx;ComPtr<ID3D11ShaderResourceView> ambient;
    ComPtr<ID3D11Texture2D> litColor,finalColor;
    NativeMainCloud mainCloud;
    std::optional<NativeCurrentCloudWeather> currentCloudWeather;
    NativeAmbient environment;NativeAmbientCloudContinuous environmentCloud;
    std::optional<NativeCurrentAmbientFrame> currentAmbient;
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
    NativeMapExposure mapExposure;
    NormalPostSurface retainedNormalExposure;
    source_territory_gpu::Owner territory;
    source_territory_gpu::Targets territoryTargets;
    source_territory::Controls territoryControls;
    source_zone::Profile territoryZoneProfile;
    source_zone::Owner territoryZoneOwner;
    source_zone::Current territoryZoneCurrent;
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
        }
    };
    bool territoryDisplayEnabled=true;
    int territoryDisplayStyle=0;
    float heldMainB1C=0;
    source_aux_camera::FogAccelerationConstants fogCamera;
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
    const auto currentPrimaryInput=resources.directory(ResourceGroup::BaseVisuals)/"primary-templates";
    const auto jointShadowInput=resources.directory(ResourceGroup::BaseShadows);
    NativeCityModelQueues ordinaryModelQueues;
    NativeModelContinuousPlan submittedStaticPlan;
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
    std::array<source_shadow_camera::Profile,2> shadowCameraProfiles;
    std::array<source_shadow_gpu::Profile,2> shadowGpuParameters;
    std::array<NativeShadowRasterSource,2> shadowRasterParameters;
    const source_shadow_camera::Profile* shadowCameraProfile=&shadowCameraProfiles[0];
    int currentRenderMode=0;
    unsigned currentShadowFamily=0;
    const source_current_season::Frame* currentSeason=nullptr;
    currentSeason=host.currentSeason?&*host.currentSeason:nullptr;
    source_current_season_shadow::Parameters currentShadowParameters;
    source_current_season_atmosphere::CloudLightInput currentCloudLights{};
    source_shadow_camera::Frame shadowCameraFrame;
    source_shadow_camera::Owner shadowHistory;
    source_view::Profile viewProfile;source_view::Frame currentView;NativeModelStencilContinuous modelStencil;
    ComPtr<ID3D11ShaderResourceView> directionalDiffuse;
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
        territoryControls.providerFlags=1; // Actual original node60 graph flag.
        territoryZoneProfile=source_zone::load(resources.directory(ResourceGroup::TerritoryZones));
        // The source map reaches a settled display via its first-dirty updater.
        // Prepare the held World once; accepted camera requests reuse this owner.
        source_zone::updateUntilSettled(territoryZoneProfile,territoryZoneOwner,territoryZoneCurrent);
        staticCatalog=prepareNativeModelContinuous(modelSource);
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
        const auto& jointInput=jointShadowInput;
        shadowCameraProfiles[0]=source_shadow_camera::loadProfile(jointInput/"initial"/"cpu-profile.bin");
        shadowCameraProfiles[1]=source_shadow_camera::loadProfile(jointInput/"mode1"/"cpu-profile.bin");
        shadowGpuParameters[0]=source_shadow_gpu::loadProfile(jointInput/"initial"/"gpu-profile.bin");
        shadowGpuParameters[1]=source_shadow_gpu::loadProfile(jointInput/"mode1"/"gpu-profile.bin");
        currentCityGateShadow=loadNativeCityGateShadowSource(jointInput,staticCatalog,cityModels.source);
        publishNativeCityAreaJointShadow(jointShadow,*currentCityGateShadow,staticCatalog,primaryModels,
            shadowCameraProfiles,shadowGpuParameters);
        fieldDisplay.bind(farMainProfile,staticCatalog,shadowCameraProfiles);
        const auto shadowRasterInput=resources.directory(ResourceGroup::ShadowRaster);
        shadowRasterParameters[0]=loadNativeShadowRasterSource(shadowRasterInput/"initial");
        shadowRasterParameters[1]=loadNativeShadowRasterSource(shadowRasterInput/"mode1");
        viewProfile=source_view::loadProfile(resources.directory(ResourceGroup::View)/"profile.bin");
        // Normal ready STA, Camera kind0: D57E50/EBD510 select K1890's
        // original white Static. FE8 belongs to the older empty-registry lab.
        auto diffuse=loadTexture(device.Get(),resources.directory(ResourceGroup::ShadowReceivers)/"white-80F9.tex");
        D3D11_SHADER_RESOURCE_VIEW_DESC diffuseView{};
        diffuseView.Format=DXGI_FORMAT_B8G8R8A8_UNORM;
        diffuseView.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;
        diffuseView.Texture2D.MipLevels=1;
        check(device->CreateShaderResourceView(diffuse.Get(),&diffuseView,&directionalDiffuse),"Normal source K1890 white80F9 owner");
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
            publishNativeCityAreaJointShadow(jointShadow,*currentCityGateShadow,staticCatalog,primaryModels,
                shadowCameraProfiles,shadowGpuParameters);
            if(!allStaticModels.draws.empty())connectNativeCityAreaJointShadowGeometry(jointShadow,allStaticModels,primaryModels);

        }
        syncArmyModels();
    };
    auto prepareAcceptedCamera=[&]() {

        territoryDisplayEnabled=!host.input.contains(FrameInputFlag::HideTerritory);
        currentSeason=host.currentSeason?&*host.currentSeason:nullptr;
        currentCloudLights={host.input.currentRawSunPosition,host.input.currentCloudGeometry40};
        publishCurrentWorldOwnership();
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
        currentShadowFamily=currentRenderMode==0?0u:1u;
        fieldDisplay.capture(currentDisplay);
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
        {
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

            parent.restore(context.Get());context->RSSetState(priorRaster.Get());
        }
        updateNativeFogAccelerationCamera(context.Get(),fogCamera,fogAcceleration,staticCamera.prefix);
        specialEffects.queueFlags=0;contextFlags3F4=0; // EA9300 accepted node1.

        executeNativeFogAcceleration(device.Get(),context.Get(),fogAcceleration);
        {
            float currentPlanet=0;
            if(currentSeason) {
                if(!currentCloudWeather || std::none_of(currentCloudWeather->profiles.seasons.begin(),
                    currentCloudWeather->profiles.seasons.end(),[&](const auto& profile){return profile.season==currentSeason->season;}))
                    currentCloudWeather=readNativeCurrentCloudWeather(
                    resources.directory(ResourceGroup::CloudWeather));
                const auto weather=publishNativeCurrentCloudWeatherBeforeClock(*currentCloudWeather,*currentSeason,mainCloud,environmentCloud);
                currentPlanet=weather.input[0x228/4];
                if(!currentAmbient)currentAmbient=prepareNativeCurrentAmbientFrame(device.Get(),
                    resources.directory(ResourceGroup::AmbientFrame),environment,environmentCloud);
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

        }
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
        {
            environment=prepareNativeAmbient(device.Get(),resources.directory(ResourceGroup::Ambient));
            // Explicit backend startup supplies all history slots. This does
            // not claim the original engine's elapsed initialization history.
            for(UINT slot=0;slot<3;++slot)executeNativeAmbientBatch(context.Get(),environment,slot);
            executeNativeAmbientBlend(context.Get(),environment,0,0);environment.selected=0;

            ambient=environment.selectedView();
        }
        context->ClearState(); // Bootstrap ends before any Main entry; not a per-request operation.
        {

            specialEffects=createNativeSpecialEffectsWithoutParticleOwner(sourceHostFlags3E4);
            fogAcceleration=prepareNativeFogAcceleration(device.Get(),resources.directory(ResourceGroup::FogAcceleration));
            fogCamera=prepareNativeFogAccelerationCamera(device.Get(),resources.directory(ResourceGroup::FogAcceleration),fogAcceleration);
        }
        {
            mapExposure=prepareNativeMapExposure(device.Get(),resources.directory(ResourceGroup::MapExposure));
            // Existing controlled Exposure61 prior[1,1] supplies this experiment's
            // bootstrap left value through the real stock publication. It is not
            // a claim about the original data-null allocation's first contents.
            retainedNormalExposure=prepareNormalPostExposure(device.Get(),normalInitialSource);
            const auto parent=NativeMainTargetScope::retain(context.Get());
            ComPtr<ID3D11RasterizerState> priorRaster;context->RSGetState(&priorRaster);
            publishNativeMapExposureAfterApply(context.Get(),mapExposure,retainedNormalExposure.views[0].Get());
            parent.restore(context.Get());context->RSSetState(priorRaster.Get());
        }
        litColor=createSourceHdr(device.Get(),resources.directory(ResourceGroup::DeferredLighting)/"node78-inner02",width,height);
        {
            mainCloud=prepareNativeMainCloud(device.Get(),resources.directory(ResourceGroup::Clouds),
                resources.directory(ResourceGroup::CloudAnimation),litColor.Get());
            environmentCloud=prepareNativeAmbientCloudContinuous(device.Get(),resources.directory(ResourceGroup::AmbientClouds),environment,mainCloud);
            environment.starAnimation=prepareNativeAmbientStarContinuous(resources.directory(ResourceGroup::AmbientStars));
            environment.starAnimationEnabled=true;
        }
        beginAcceptedMain(initialCommonClock);
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
        inputTextures.push_back(texture);views.push_back(view);
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
    normalOutput=createNormalPostFinalOutput(device.Get(),resources.directory(ResourceGroup::FinalOutput),width,height);
    NativeMainTargetScope sixGbufferTargets;
    {
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
        {
            allStaticModels=prepareNativeModelContinuousGpu(device.Get(),staticCatalog,modelSource);
            connectNativeJointPopulationShadowGeometry(jointShadow,allStaticModels,primaryModels.currentModels);
            modelStencil=prepareNativeModelStencilContinuous(device.Get(),resources.directory(ResourceGroup::View),staticCatalog,allStaticModels,currentView,sourceHostFlags3E4);
            terrainCamera=prepareNativeTerrainCameraGpu(device.Get(),input,layout.Get(),positions.Get(),indices.Get(),vs.Get(),ps.Get(),coarseVs.Get(),coarsePs.Get(),indexCount);
            if(currentSeason) {
                seasonPalette=source_current_season_terrain::readPaletteBank(resources.directory(ResourceGroup::SeasonalResources));
                seasonPaletteBinding=source_current_season_terrain::prepareBinding(psBytes);
            }
            const auto shadowGpuInput=jointShadowInput/"gpu";
            shadowCameraGpu=prepareNativeShadowCameraGpu(device.Get(),shadowGpuParameters[currentShadowFamily],*shadowCameraProfile,jointShadow.catalog,jointShadow.allModels,shadowGpuInput,shadowRasterParameters);
            shadowGpuPlan=source_shadow_gpu::preparePlan(shadowCameraGpu.profile,shadowCameraFrame,jointShadow.catalog);
            NativeSourceDepthState shadowEntry{ds,stencilRef};
            executeNativeShadowCameraGpu(context.Get(),shadowEntry,shadowCameraGpu,shadowCameraFrame,shadowGpuPlan,jointShadow.catalog,jointShadow.allModels,currentShadowFamily);
            shadowMap=shadowCameraGpu.map;

        }
        auto& models=visibleModels;

    }
    // The real caller output survives the nested Main targets and is also
    // the output inherited by normal post109. Retain attachments/rects only.
    {auto raw=normalOutput.targets[0].Get();context->OMSetRenderTargets(1,&raw,nullptr);}
    context->RSSetViewports(1,&vp);context->RSSetScissorRects(1,&mainScissor);
    auto callerTargets=NativeMainTargetScope::retain(context.Get());
    std::vector<std::array<UINT,4>> terrainBatches;
    if(groupedTerrain) {
        std::ifstream batches(input/"batches.txt");std::array<UINT,4> row;
        while(batches>>row[0]>>row[1]>>row[2]>>row[3])terrainBatches.push_back(row);
    }
    NativeSourceDepthState sourceDepth{ds,stencilRef};UINT drawCount=0;
    auto drawOpaqueGeometry=[&]() {
        std::array<ID3D11ShaderResourceView*,32> empty{};
        context->VSSetShaderResources(0,32,empty.data());context->PSSetShaderResources(0,32,empty.data());
        context->CSSetShaderResources(0,32,empty.data());
        std::array<ID3D11UnorderedAccessView*,8> noUav{};context->CSSetUnorderedAccessViews(0,8,noUav.data(),nullptr);
        context->OMSetRenderTargets(0,nullptr,nullptr);
        // Actual node33: depth/stencil only. Fresh color initialization stays
        // at allocation above; no invented per-request G-buffer color clear.
        context->ClearDepthStencilView(dsv.Get(),D3D11_CLEAR_DEPTH|D3D11_CLEAR_STENCIL,depthClear,0);
        {
            visibleModels=updateSelectNativeModelContinuous(context.Get(),staticPlan,allStaticModels);
            submittedStaticPlan=updateNativeModelStencilContinuous(modelStencil,currentView,staticPlan,visibleModels);
            auto cityQueues=updateSelectNativeCityAreaPrimary(context.Get(),primaryModels,cityPlan);
            ordinaryModelQueues=mergeNativeCityPopulationWithField54(submittedStaticPlan,visibleModels,primaryModels.source,cityPlan,cityQueues);
        }
        {
            groundDetails.update(context.Get());
            auto drawGroundNode=[&](uint32_t node){
                for(size_t record=0;record<viewProfile.records.size();++record){
                    const auto& sourceRecord=viewProfile.records[record];
                    source_view::Primitive primitive{0,0,ground_detail_source::effectiveExclusion(
                        {viewProfile.commonPresent!=0,viewProfile.commonFlags,groundDetails.currentStatistics().currentAux14,viewProfile.commonExclusion,groundFrustumType64}),1u<<record};
                    groundDetails.draw(context.Get(),node,{sourceRecord.mask,currentView.ordinaryCounts[record],sourceHostFlags3E4,
                        (sourceRecord.classification&1)==0,source_view::ordinary54(viewProfile,currentView,primitive,record,sourceHostFlags3E4)},rt.data(),dsv.Get(),vp);
                }
            };
            if(sourceArmyVisuals) {
                armyQueues=updateSelectNativeArmyPopulation(context.Get(),armyModels,armyPlan);
                drawNativeModels(context.Get(),armyQueues.ordinary49,rt.data(),dsv.Get(),vp);
                drawNativeModels(context.Get(),armyQueues.alpha50,rt.data(),dsv.Get(),vp);
                drawNativeModels(context.Get(),armyQueues.ordinary54,rt.data(),dsv.Get(),vp);
                drawNativeModels(context.Get(),armyQueues.alpha55,rt.data(),dsv.Get(),vp);

            }
            if(sourceAccessoryVisuals) {
                drawNativeSharedAccessories(context.Get(),*armyModelPool,sharedAreaAccessories,areaAccessories,rt.data(),dsv.Get(),vp);
                drawNativeSharedAccessories(context.Get(),*armyModelPool,sharedCityAccessories,cityAccessories,rt.data(),dsv.Get(),vp);
            }
            drawNativeModels(context.Get(),ordinaryModelQueues.ordinary54,rt.data(),dsv.Get(),vp);
            drawGroundNode(54);
            drawNativeModels(context.Get(),ordinaryModelQueues.alpha55,rt.data(),dsv.Get(),vp);
            drawGroundNode(55);
            const auto& detail=groundDetails.currentStatistics();

        }
    {
        if(currentSeason)source_current_season_terrain::patchNativeCurrent(
            seasonPalette,seasonPaletteBinding,currentSeason->season,terrainCamera);
        updateNativeTerrainCameraGpu(context.Get(),terrainCamera,staticCamera);
        NativeTerrainCameraDrawBindings binding;
        binding.colors=rt;binding.depthTarget=dsv.Get();binding.viewport=vp;
        binding.raster=raster.Get();binding.depthState=depthState.Get();binding.stencilReference=stencilRef;binding.blend=blendState.Get();
        binding.vertexViews=terrainVertexViews;binding.pixelViews=terrainPixelViews;
        binding.vertexSamplers=terrainVertexSamplers;binding.pixelSamplers=terrainPixelSamplers;
        drawCount=drawNativeTerrainCameraGpu(context.Get(),terrainCamera,binding);
    }
        sourceDepth.bind(context.Get(),depthState.Get(),ds,stencilRef);
    };
    drawOpaqueGeometry();
    auto mainDepth=createNativeDepthMirror(device.Get(),depth.Get(),shaderFormats[5]);
    updateNativeDepthMirror(context.Get(),mainDepth,"59 after geometry");
    {
        territoryTargets.storage={targets[0].Get(),targets[2].Get(),targets[1].Get(),targets[3].Get()};
        territoryTargets.output={rt[0],rt[2],rt[1],rt[3]};
        territoryTargets.sampledFormat={shaderFormats[0],shaderFormats[2],shaderFormats[1],shaderFormats[3]};
        territoryTargets.depth=mainDepth.depth.Get();
        territoryTargets.primaryDSV=dsv.Get();
        territory=source_territory_gpu::prepare(device.Get(),resources.directory(ResourceGroup::Territory),territoryTargets,sourceDepth.description,rs);
        territory.profile.zones=territoryZoneOwner.zones;
        context->UpdateSubresource(territory.zones.Get(),0,nullptr,territoryZoneOwner.zones.data(),0,0);
        ownershipPalette.emplace(territory.profile);
        publishCurrentWorldOwnership();
    }
    auto drawCurrentTerritory=[&](UINT node,UINT providerDrawFlags){

        territoryControls.styleSelector=territoryDisplayStyle;
        territoryControls.screenMapMask=source_zone::displayMask(territoryDisplayEnabled,territoryDisplayStyle,
            territoryZoneCurrent.membership!=nullptr,!territoryZoneCurrent.referenceLists[0].empty()||!territoryZoneCurrent.referenceLists[1].empty());
        source_territory::Selection selection{staticCameraRuntime.zoom.band,staticCameraRuntime.zoom.fraction,staticCameraRuntime.pose.distance,territoryControls};
        source_territory_gpu::update(context.Get(),territory,selection,staticCamera.prefix,heldMainB1C);
        source_territory_gpu::execute(context.Get(),territory,territoryTargets,mapExposure.view.Get(),providerDrawFlags,source_territory::initializedStencilMask,sourceDepth);
        updateNativeDepthMirror(context.Get(),mainDepth,node==122?"122 PB Pop40000007":"60 PB Pop40000007");


    };
    drawCurrentTerritory(60,1);
    publishThinSurfaceFlags(60);
    callerTargets.restore(context.Get());
    NativeRefractionDepth refractionDepth;
    const auto copy97Source=resources.directory(ResourceGroup::Refraction)/"copy97";
    NativeRefractionColors refractionColors;
    NativeNormalPost normalPost;
    std::vector<NativeNormalPostPass> normalPostUnion;
    NativeNormalPostContinuous postContinuous;
    NativeNormalPostCamera postCamera;NativeNormalPostCameraProjection postCameraProjection;
    source_current_season_resources::Bank seasonLuts;
    int loadedLutSeason=-1;
    source_current_season_post::CallerSources currentPostCallers;
    if(currentSeason)currentPostCallers=source_current_season_post::readCallerSources(
        resources.directory(ResourceGroup::SeasonalResources)/"post-caller"/"caller-source.bin");

    auto executeCurrentPost=[&](float currentClock) {
        if(currentSeason) {
            if(loadedLutSeason!=int(currentSeason->season)) {
                const bool loaded=std::any_of(seasonLuts.catalog.luts.begin(),seasonLuts.catalog.luts.end(),
                    [&](const auto& row){return row.season==currentSeason->season;});
                if(!loaded)source_current_season_resources::loadResources(device.Get(),seasonLuts,
                    source_current_season_resources::readCatalog(resources.directory(ResourceGroup::SeasonalResources)));
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
        } else postCameraProjection=updateNativeNormalPostCameraBeforeBegin(postCamera,staticCamera.prefix,postContinuous);
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
                if(!mapped.returnedNull){std::memcpy(mapped.bytes.data(),bytes.pData,mapped.bytes.size());context->Unmap(buffer,0);}
                readStaging=true;
            }
            applyNativeNormalPostBefore57(postContinuous,plan,readStaging?&mapped:nullptr,passes);
        };
        auto result=executeNormalPost(device.Get(),context.Get(),sourceDepth,normalPost,passes,before57);
        endNormalPost(normalPost);
        finishEndReturnedNativeNormalPostContinuous(postContinuous,plan);
        {
            const auto parent=NativeMainTargetScope::retain(context.Get());
            ComPtr<ID3D11RasterizerState> priorRaster;context->RSGetState(&priorRaster);
            publishNativeMapExposureAfterApply(context.Get(),mapExposure,normalPost.exposure.views[0].Get());

            parent.restore(context.Get());context->RSSetState(priorRaster.Get());
        }

        return result;
    };
    ComPtr<ID3D11Texture2D> postInputSnapshot,cloudInputSnapshot;
    NativeSourceDepthState cloudEntryLogical{};
    ComPtr<ID3D11DepthStencilState> cloudEntryDepth;
    ComPtr<ID3D11BlendState> cloudEntryBlend;
    ComPtr<ID3D11RasterizerState> cloudEntryRaster;
    UINT cloudEntryReference=0,cloudEntrySampleMask=~0u;float cloudEntryFactor[4]{};
    NativeSubShadow subShadow;
    NativeSourceShadowReceiver receiver68,receiver119;
    const auto receiver68Source=resources.directory(ResourceGroup::ShadowReceivers)/"initial"/"rs68";
    const auto receiver119Source=resources.directory(ResourceGroup::ShadowReceivers)/"initial"/"rs119";
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
    auto appendAccessoryHdr=[&](NativeSourceHdrPasses& destination,const char* list) {
        if(!sourceAccessoryVisuals)return;
        auto source=prepareSourceHdrPasses(device.Get(),resources.directory(ResourceGroup::BaseAccessories)/"hdr/initial",list,
            litColor.Get(),targets,shaderFormats,ggx.Get(),true);
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
            resources.directory(ResourceGroup::HeightFog)/"source-profile.bin");
        publishNativeCurrentHeightFog(*currentHeightFog,*currentSeason,mapFog);
    };
    OceanNativeTextures oceanTextures;NativeOcean116 ocean116;
    NativeWaterCameraGpu waterCamera;NativeWaterRefractionGpu waterRefraction123,waterRefraction166;
    std::array<float,8> completedWaveBounds{};
    const auto waterCameraInput=resources.directory(ResourceGroup::WaterCamera);
    NativeOceanFftSchedule oceanFft;
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
        auto pass=std::find_if(hdr166.passes.begin(),hdr166.passes.end(),[](auto& p){return p.identity=="node166-inner02";});
        if(pass==hdr166.passes.end())throw std::runtime_error("Forward actual current inner02 producer absent");
        auto globals=std::find_if(pass->constants.begin(),pass->constants.end(),[](auto& cb){return !cb.vertex&&cb.slot==0;});
        if(globals==pass->constants.end())throw std::runtime_error("Forward actual current HDR globals absent");
        primaryForwardFrame=prepareNativePrimaryForwardCurrent(primaryForwardSource,primaryModels.source,cityPlan,
            mainDirectPrefix.data(),globals->bytes.data(),globals->bytes.size(),
            shadowReceiver68Camera.current.data(),shadowReceiver68Camera.current.size());
         //Allocation observation, not a source-pixel render.
        executeNativePrimaryForwardGpu(context.Get(),sourceDepth,primaryForwardGpu,primaryForwardFrame,primaryModels.currentModels,
            pass->target.Get(),dsv.Get(),vp,shadowCameraGpu.map.view.Get(),directionalDiffuse.Get(),hdr166.ggx.Get(),
            ambient.Get(),fogAcceleration.view.Get());

    };
    auto isolateCurrentShadowVisibility=[&]() {

    };
    {
        auto abTarget=rt[0];context->OMSetRenderTargets(1,&abTarget,dsv.Get()); //62
        subShadow=prepareNativeSubShadow63(device.Get(),resources.directory(ResourceGroup::ContactShadows),sourceDepth,rs,blend,
            mainDepth,targets[1].Get(),shaderFormats[1],targets[0].Get(),dsv.Get());
        executeNativeSubShadow63(context.Get(),sourceDepth,subShadow,vp);
        D3D11_RENDER_TARGET_VIEW_DESC rsDesc{};rsDesc.Format=formats[2];rsDesc.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
        rsDesc.Texture2DArray.FirstArraySlice=3;rsDesc.Texture2DArray.ArraySize=1;
        check(device->CreateRenderTargetView(targets[2].Get(),&rsDesc,&indirectTarget),"Main indirect RS3 target");
    }
    callerTargets.restore(context.Get()); //64
    {auto raw=indirectTarget.Get();context->OMSetRenderTargets(1,&raw,dsv.Get());} //67
    {
        if(!directionalDiffuse){auto diffuseTexture=loadTexture(device.Get(),input/"rs68"/"directional-diffuse.tex");
            check(device->CreateShaderResourceView(diffuseTexture.Get(),nullptr,&directionalDiffuse),"held original directional diffuse");}
        receiver68=prepareSourceShadowReceiver(device.Get(),receiver68Source,shadowMap.view.Get(),directionalDiffuse.Get(),targets[2].Get(),formats[2],width,height);
        {
            shadowReceiver68Camera=prepareNativeShadowCameraReceiver(device.Get(),receiver68,receiver68Source);
            updateCurrentShadowReceiver(receiver68,shadowReceiver68Camera);
        }
        executeSourceShadowReceiver(device.Get(),context.Get(),sourceDepth,receiver68,mainDepth.depth.Get(),dsv.Get());
    }
    fallback69=prepareRs69Fallback(device.Get(),input,targets[2].Get(),formats[2],width,height);
    executeRs69Fallback(device.Get(),context.Get(),sourceDepth,fallback69,dsv.Get());
    isolateCurrentShadowVisibility();
    callerTargets.restore(context.Get()); //70
    DXGI_FORMAT lightingShaderFormat=DXGI_FORMAT_UNKNOWN,lightingTargetFormat=DXGI_FORMAT_UNKNOWN;
    {
        {
            auto earlySource=resources.directory(ResourceGroup::DeferredLighting);
            D3D11_RENDER_TARGET_VIEW_DESC hdrDesc{};hdrDesc.Format=DXGI_FORMAT_R11G11B10_FLOAT;hdrDesc.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
            ComPtr<ID3D11RenderTargetView> hdrTarget;check(device->CreateRenderTargetView(litColor.Get(),&hdrDesc,&hdrTarget),"Main73 retained HDR target");
            auto mainTargets=NativeMainTargetScope::main73(hdrTarget.Get(),dsv.Get(),vp,mainScissor);
            mainTargets.restore(context.Get());
            hdr78=prepareSourceHdrPasses(device.Get(),earlySource,"passes.txt",litColor.Get(),targets,shaderFormats,ggx.Get(),true);
            appendAccessoryHdr(hdr78,"passes.txt");
            {
                updateNativeDeferredCameraGpu(context.Get(),deferredCameraProfile,deferredCameraFields,hdr78);
                updateCurrentHdrLighting(hdr78);
            }
            executeSourceHdrPasses(device.Get(),context.Get(),sourceDepth,hdr78,mainDepth.depth.Get(),dsv.Get(),ambient.Get(),nullptr,nullptr,
                nullptr,nullptr,initialCommonClock,currentView.flags19AC,currentView.ocean19B0);
            mainTargets.restore(context.Get());
            lightingShaderFormat=lightingTargetFormat=DXGI_FORMAT_R11G11B10_FLOAT;
            {
                preparedScene97=prepareNativeRefractionScene97(device.Get(),resources.directory(ResourceGroup::Refraction)/"copy-scene97",litColor.Get(),lightingShaderFormat);
                refractionColors.scene97=executeNativeRefractionScene97(preparedScene97,context.Get(),sourceDepth);
            }
            preparedDepth97=prepareNativeRefractionDepth(device.Get(),copy97Source,mainDepth.depth.Get(),width,height);
            refractionDepth=executeNativeRefractionDepth(preparedDepth97,context.Get(),sourceDepth);
            mainTargets.restore(context.Get());
            {
                oceanTextures=loadOceanTextures(device.Get(),resources.directory(ResourceGroup::OceanTextures));
                ocean116=prepareNativeOcean116(device.Get(),resources.directory(ResourceGroup::Water),oceanTextures,
                    refractionDepth.view.Get(),copy97Source/"target-sampler-desc.bin",targets,rt,formats[3],dsv.Get(),width,height,
                    resources.directory(ResourceGroup::OceanClock)/"ocean-clock.txt",initialCommonClock);
                {
                    waterCamera=prepareNativeWaterCameraGpu(device.Get(),waterCameraInput/"geometry-profile.bin",ocean116);
                    const auto bounds=load(waterCameraInput/"initial"/"runtime-geometry"/"ocean-current-phase-wave-bounds.bin");
                    if(bounds.size()!=sizeof(completedWaveBounds))throw std::runtime_error("Completed Ocean wave AABB source extent differs");
                    std::memcpy(completedWaveBounds.data(),bounds.data(),bounds.size());
                    updateNativeWaterCameraGpu(waterCamera,context.Get(),ocean116,staticCamera.prefix,completedWaveBounds);
                }
                oceanFft=prepareNativeOceanFftSchedule(resources.directory(ResourceGroup::OceanWaves));
                executeNativeOcean116(device.Get(),context.Get(),sourceDepth,ocean116,initialCommonClock);
                ;
                updateNativeDepthMirror(context.Get(),mainDepth,"117 after Ocean");
                mainTargets.restore(context.Get());
            }
            {
                // 118 binds the existing RS3 and water-updated Main D1 without
                // clearing. 119 selects the Ocean group (process50). The same
                // explicitly empty SDF owner used at69 preserves RG at120;
                // 121 pops this target scope without copying depth.
                auto rsTarget=indirectTarget.Get();context->OMSetRenderTargets(1,&rsTarget,dsv.Get()); //118
                receiver119=prepareSourceShadowReceiver(device.Get(),receiver119Source,shadowMap.view.Get(),directionalDiffuse.Get(),targets[2].Get(),formats[2],width,height);
                {
                    shadowReceiver119Camera=prepareNativeShadowCameraReceiver(device.Get(),receiver119,receiver119Source);
                    updateCurrentShadowReceiver(receiver119,shadowReceiver119Camera);
                }
                executeSourceShadowReceiver(device.Get(),context.Get(),sourceDepth,receiver119,mainDepth.depth.Get(),dsv.Get());
                isolateCurrentShadowVisibility();
                mainTargets.restore(context.Get()); //121
                drawCurrentTerritory(122,2);
                publishThinSurfaceFlags(122);
                hdr123=prepareSourceHdrPasses(device.Get(),earlySource,"ocean-passes.txt",litColor.Get(),targets,shaderFormats,ggx.Get(),true);
                {
                    updateNativeDeferredCameraGpu(context.Get(),deferredCameraProfile,deferredCameraFields,hdr123);
                    waterRefraction123=prepareNativeWaterRefractionGpu(waterCameraInput/"receiver-refraction-writes.txt",hdr123);
                    updateNativeWaterRefractionGpu(context.Get(),waterRefraction123,waterCamera.refraction,hdr123);
                    updateCurrentHdrLighting(hdr123);
                }
                executeSourceHdrPasses(device.Get(),context.Get(),sourceDepth,hdr123,mainDepth.depth.Get(),dsv.Get(),ambient.Get(),refractionDepth.view.Get(),refractionColors.scene97.view.Get(),
                    nullptr,nullptr,initialCommonClock,currentView.flags19AC,currentView.ocean19B0);
                mainTargets.restore(context.Get());
                mainSky=prepareNativeMainSky(device.Get(),resources.directory(ResourceGroup::Sky),litColor.Get(),dsv.Get(),width,height);
                const auto fogSource=resources.directory(ResourceGroup::Atmosphere);
                mapFog=prepareNativeMapFog(device.Get(),fogSource,litColor.Get(),mainDepth.depth.Get(),dsv.Get(),width,height);
                {
                    atmosphereCamera=prepareNativeAtmosphereCamera(device.Get(),resources.directory(ResourceGroup::AtmosphereCamera)/"initial",mainSky,mapFog);
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

                executeNativeMainCloud(device.Get(),context.Get(),sourceDepth,mainCloud,dsv.Get(),initialCommonClock,UINT(actualHostQ7F8));
                mainTargets.restore(context.Get());
                publishCurrentHeightFog();
                executeNativeMapFog(device.Get(),context.Get(),sourceDepth,mapFog,initialCommonClock,
                    std::optional<unsigned>(currentView.flags19AC));
                ;
                mainTargets.restore(context.Get());
                preparedFilters137=prepareNativeRefractionFilters137(device.Get(),resources.directory(ResourceGroup::Refraction)/"filter137",litColor.Get(),lightingShaderFormat,mainDepth.depth.Get());
                executeNativeRefractionFilters137(preparedFilters137,context.Get(),sourceDepth,refractionColors);
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
                context->OMSetRenderTargets(0,nullptr,refractionDepth.depth.Get()); //138
                ;
                applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,141);
                mainTargets.restore(context.Get()); //142, B616 has no mirror
                sixGbufferTargets.restore(context.Get()); //143
                std::array<ID3D11RenderTargetView*,6> sixExpected{};
                for(UINT i=0;i<6;++i)sixExpected[i]=sixGbufferTargets.colors[i].Get();
                ;
                applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,147);
                updateNativeDepthMirror(context.Get(),mainDepth,"148 after source special zero-tail state");
                mainTargets.restore(context.Get());
                // Q400 closes150/154;142 belongs to distinct B616, not MainD1.
                sixGbufferTargets.restore(context.Get()); //156
                ;
                updateNativeDepthMirror(context.Get(),mainDepth,"160 Rain writers pending");
                mainTargets.restore(context.Get());
                publishThinSurfaceFlags(161);
                //162/165 retain their source RS3 scope; this finite cohort
                //has no process60 writer between them.
                context->OMSetRenderTargets(1,&rsTarget,dsv.Get());
                mainTargets.restore(context.Get());
                hdr166=prepareSourceHdrPasses(device.Get(),earlySource,"late-passes.txt",litColor.Get(),targets,shaderFormats,ggx.Get(),true);
                appendAccessoryHdr(hdr166,"late-passes.txt");
                {
                    updateNativeDeferredCameraGpu(context.Get(),deferredCameraProfile,deferredCameraFields,hdr166);
                    waterRefraction166=prepareNativeWaterRefractionGpu(waterCameraInput/"receiver-refraction-writes.txt",hdr166);
                    updateNativeWaterRefractionGpu(context.Get(),waterRefraction166,waterCamera.refraction,hdr166);
                    updateCurrentHdrLighting(hdr166);
                }
                executeSourceHdrPasses(device.Get(),context.Get(),sourceDepth,hdr166,mainDepth.depth.Get(),dsv.Get(),ambient.Get(),refractionDepth.view.Get(),refractionColors.scene97.view.Get(),
                    refractionColors.specular.view.Get(),refractionColors.blurredDepth.view.Get(),initialCommonClock,currentView.flags19AC,currentView.ocean19B0);
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
                    normalPost=prepareNormalPost(device.Get(),normalInitialSource,scene.Get(),mainDepth.depth.Get(),normalOutput,width,height,
                        &retainedNormalExposure);
                    const auto continuousSource=resources.directory(ResourceGroup::PostAnimation);
                    postContinuous=prepareNativeNormalPostContinuous(continuousSource);
                    postCamera=prepareNativeNormalPostCamera(resources.directory(ResourceGroup::PostCamera));
                    normalPostUnion=loadNativeNormalPostPasses(device.Get(),continuousSource,width,height);

                    finalColor=executeCurrentPost(initialCommonClock);
                    finishNormalApply();
                    {
                        const auto postCaller=NativeMainTargetScope::retain(context.Get());
                        auto sameOutput=normalOutput.targets[0].Get();
                        context->OMSetRenderTargets(1,&sameOutput,dsv.Get()); //194, no clear
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
            const auto& published=publishNativeOceanFftCompleted(oceanFft);
            updateNativeOceanGeometry(context.Get(),ocean116,published.data(),UINT(published.size()*sizeof(NativeOceanFftVertex)));
            completedWaveBounds=oceanFft.publishedBounds;
            advanceNativeOcean116(ocean116,currentClock);
            queueNativeOceanFft(oceanFft,ocean116.clock.accumulatedTime);

        }
        beginAcceptedMain(currentClock);
        {
            if(primaryPopulationChanged)source_shadow_gpu::bindCatalog(shadowCameraGpu.profile,jointShadow.catalog);
            source_shadow_gpu::setCurrentParameters(shadowCameraGpu.profile,shadowGpuParameters[currentShadowFamily]);
            shadowGpuPlan=source_shadow_gpu::preparePlan(shadowCameraGpu.profile,shadowCameraFrame,jointShadow.catalog);
            executeNativeShadowCameraGpu(context.Get(),sourceDepth,shadowCameraGpu,shadowCameraFrame,shadowGpuPlan,jointShadow.catalog,jointShadow.allModels,currentShadowFamily);
            shadowMap=shadowCameraGpu.map;
            updateCurrentShadowReceiver(receiver68,shadowReceiver68Camera);
            updateCurrentShadowReceiver(receiver119,shadowReceiver119Camera);
        }
        drawOpaqueGeometry();updateNativeDepthMirror(context.Get(),mainDepth,"59 new Main geometry");

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
        {
            updateNativeDeferredCameraGpu(context.Get(),deferredCameraProfile,deferredCameraFields,hdr78);
        updateCurrentHdrLighting(hdr78);
        }
        executeSourceHdrPasses(device.Get(),context.Get(),sourceDepth,hdr78,mainDepth.depth.Get(),dsv.Get(),ambient.Get(),nullptr,nullptr,
            nullptr,nullptr,currentClock,currentView.flags19AC,currentView.ocean19B0);
        mainTargets.restore(context.Get());
        executeNativeRefractionScene97(preparedScene97,context.Get(),sourceDepth);
        executeNativeRefractionDepth(preparedDepth97,context.Get(),sourceDepth);mainTargets.restore(context.Get());
        updateNativeWaterCameraGpu(waterCamera,context.Get(),ocean116,staticCamera.prefix,completedWaveBounds);
        executeNativeOcean116(device.Get(),context.Get(),sourceDepth,ocean116,currentClock);
        updateNativeDepthMirror(context.Get(),mainDepth,"117 new Main Ocean");mainTargets.restore(context.Get());
        context->OMSetRenderTargets(1,&rsTarget,dsv.Get());
        executeSourceShadowReceiver(device.Get(),context.Get(),sourceDepth,receiver119,mainDepth.depth.Get(),dsv.Get());mainTargets.restore(context.Get());
        isolateCurrentShadowVisibility();
        drawCurrentTerritory(122,2);
        publishThinSurfaceFlags(122);
        {
            updateNativeDeferredCameraGpu(context.Get(),deferredCameraProfile,deferredCameraFields,hdr123);
            updateNativeWaterRefractionGpu(context.Get(),waterRefraction123,waterCamera.refraction,hdr123);
        updateCurrentHdrLighting(hdr123);
        }
        executeSourceHdrPasses(device.Get(),context.Get(),sourceDepth,hdr123,mainDepth.depth.Get(),dsv.Get(),ambient.Get(),refractionDepth.view.Get(),refractionColors.scene97.view.Get(),
            nullptr,nullptr,currentClock,currentView.flags19AC,currentView.ocean19B0);
        updateNativeAtmosphereCamera(context.Get(),atmosphereCamera,staticCamera.prefix,mainSky,mainCloud,mapFog);
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
        {
            updateNativeDeferredCameraGpu(context.Get(),deferredCameraProfile,deferredCameraFields,hdr166);
            updateNativeWaterRefractionGpu(context.Get(),waterRefraction166,waterCamera.refraction,hdr166);
        updateCurrentHdrLighting(hdr166);
        }
        executeSourceHdrPasses(device.Get(),context.Get(),sourceDepth,hdr166,mainDepth.depth.Get(),dsv.Get(),ambient.Get(),refractionDepth.view.Get(),refractionColors.scene97.view.Get(),refractionColors.specular.view.Get(),refractionColors.blurredDepth.view.Get(),
            currentClock,currentView.flags19AC,currentView.ocean19B0);
        drawCurrentPrimaryForward();

        mainTargets.restore(context.Get());

        updateNativeDepthMirror(context.Get(),mainDepth,"191 new Main; particle owner pending");callerTargets.restore(context.Get());
        groundDetails.finishRenderNode();
        executeCurrentPost(currentClock);
        finishNormalApply();
        //194/203 alias the same Final109;206 remains closed with null callerDSV.
        const auto postCaller=NativeMainTargetScope::retain(context.Get());auto sameOutput=normalOutput.targets[0].Get();
        context->OMSetRenderTargets(1,&sameOutput,dsv.Get());postCaller.restore(context.Get());
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
