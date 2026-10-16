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
#include "RenderRandomClock.h"
#include "../Camera/CameraRig.h"
#include "../Scene/Models/ModelFrame.h"
#include "../Scene/Models/ModelStencilRules.h"
#include "../Scene/Models/StaticModelResources.h"
#include "../Scene/Terrain/TerrainCameraConstants.h"
#include "../Passes/Shadows/ShadowMap.h"
#include "../Device/DepthMirror.h"
#include "../Passes/Shadows/ShadowReceiver.h"
#include "../Passes/Environment/Ocean/RefractionDepth.h"
#include "../Passes/Environment/Ocean/OceanTextures.h"
#include "../Passes/Lighting/HdrRenderer.h"
#include "../Passes/Lighting/DeferredCameraFrame.h"
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
#include "../Passes/Environment/WeatherFogProfileReader.h"
#include "../Passes/Environment/WeatherFogConstants.h"
#include "../Passes/Environment/FogAccelerationPass.h"
#include "MainRenderPhaseState.h"
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
#include "../Scene/Models/ArmyCategoryResources.h"
#include "../Scene/Models/ArmyWorldModeBounds.h"
#include "../Scene/Models/ArmyWorldModelPool.h"
#include "../Scene/Models/SharedCategory2Accessories.h"
#include "../Passes/Shadows/SeasonShadow.h"
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
#include "../Device/GraphicsClipState.h"
#include "../Passes/Lighting/DeferredLightingPass.h"
#include "../Passes/Environment/AtmospherePass.h"
#include "../Passes/Environment/CloudEnvironmentPass.h"
#include "../Passes/Shadows/ShadowPass.h"
#include "../Passes/PostProcessing/NormalPostProcess.h"
#include "../Scene/Terrain/TerritoryPass.h"
#include "../Scene/Models/ArmyRenderWorld.h"
#include "../Scene/Models/BaseRenderWorld.h"
#include "SceneFrameCoordinator.h"
San14::Render::RenderSession San14::Render::createRenderSession(RenderHost& host, RenderConfiguration configuration) {
    RenderCallbackScope callbacks{host};
    const auto& resources=configuration.resources;
    const auto& input=resources.terrain();
    const auto modelSource=resources.directory(ResourceGroup::Models);
    const auto shadowSource=resources.directory(ResourceGroup::ShadowCasters);
    const bool sourceArmyVisuals=configuration.world.armies;
    const bool sourceAccessoryVisuals=configuration.world.accessories;
    UINT width,height,instanceCount,indexCount;std::array<DXGI_FORMAT,5> formats;DXGI_FORMAT depthFormat;
    std::ifstream scene(input/"scene.txt");if(!scene)throw std::runtime_error("scene.txt absent");
    scene>>width>>height>>instanceCount>>indexCount;
    for(auto& format:formats){int n;scene>>n;format=DXGI_FORMAT(n);}int df;scene>>df;depthFormat=DXGI_FORMAT(df);
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
    ContextEnvironmentResources contextEnvironment(device.Get());
    fs::path lightingSource;
    ComPtr<ID3D11Texture2D> ggx;
    NativeSourceHdrTarget lightingTarget;
    ComPtr<ID3D11Texture2D> litColor,finalColor;
    DXGI_FORMAT lightingShaderFormat=DXGI_FORMAT_UNKNOWN,lightingTargetFormat=DXGI_FORMAT_UNKNOWN;
    StarTwinklePhase starTwinkle;
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

    const auto& sourceHostFlags3E4=host.mainFlags;
    auto& phase=host.mainPhase;
    FogAccelerationPass fogAcceleration;NativeSpecialEffects specialEffects;
    NormalPostProcess postProcessing;
    TerritoryPass territory;
    AtmospherePass atmosphere;
    std::optional<StaticModelResources> staticModels;
    BaseRenderWorld bases{sourceAccessoryVisuals};
    source_default_player_phase::Profile defaultPlayerPhases;
    ground_details::GroundDetails groundDetails;
    ArmyRenderWorld armies{sourceArmyVisuals,sourceAccessoryVisuals};
    ShadowPass shadows;
    const auto jointShadowInput=resources.directory(ResourceGroup::BaseShadows);
    SceneFrameCoordinator frameState{host.currentSeason?&*host.currentSeason:nullptr};
    CloudEnvironmentPass clouds; // Retires before its borrowed Star and numerical resource owners.
    GraphicsClipState frontendClip;
    const auto& staticCamera=frameState.camera();
    UINT64 completedMainRequests=1;
    {
        frameState.prepareCamera(resources);
        // The source map reaches a settled display via its first-dirty updater.
        // Prepare the held World once; accepted camera requests reuse this owner.
        territory.prepareZones(resources.directory(ResourceGroup::TerritoryZones));
        staticModels.emplace(modelSource);
        bases.preparePrimary(device.Get(),resources,host.cityGateWorld,host.areaCapitalWorld);
        groundDetails.prepareResources(device.Get(),resources.directory(ResourceGroup::GroundDetails),
            frameState.season()?resources.directory(ResourceGroup::SeasonalGroundDetails):fs::path{});
        frameState.prepareWeatherProfile(resources);
        if(sourceAccessoryVisuals||sourceArmyVisuals)
            defaultPlayerPhases=source_default_player_phase::readProfile(
                resources.directory(ResourceGroup::AnimationDefaults)/"default-player-loop-source.bin");
        frameState.prepareBaseAccessories(device.Get(),context.Get(),resources,host.cityGateWorld,host.areaCapitalWorld,
            defaultPlayerPhases,bases);
        bases.withAccessories([&](auto& cityAccessories,auto& areaAccessories) {
            armies.prepare(device.Get(),context.Get(),resources,cityAccessories,areaAccessories);
        });
        frameState.prepareWeatherLight(resources);
        shadows.preparePopulation(jointShadowInput,staticModels->catalog(),bases.citySource(),bases.primary());
        frameState.bindFieldDisplay(shadows,staticModels->catalog());
        shadows.prepareRaster(resources.directory(ResourceGroup::ShadowRaster));
        frameState.prepareView(resources);
        // Normal ready STA, Camera kind0: D57E50/EBD510 select K1890's
        // original white Static. FE8 belongs to the older empty-registry lab.
        shadows.prepareReceiverResources(device.Get(),resources.directory(ResourceGroup::ShadowReceivers));
    }
    auto publishCurrentModels=[&]() {
        armies.publishArmy(device.Get(),context.Get(),host.armyWorld);
        bases.publish(device.Get(),context.Get(),host.cityGateWorld,host.areaCapitalWorld,armies);
        if(bases.populationChanged()) {
            shadows.publishPrimaryPopulation(staticModels->catalog(),bases.primary(),
                *staticModels);
        }
        if(sourceArmyVisuals)armies.synchronize(device.Get(),*host.armyWorld);
    };
    auto prepareAcceptedCamera=[&]() {

        territory.setVisible(!host.input.contains(FrameInputFlag::HideTerritory));
        frameState.acceptRequest(host.input,host.currentSeason?&*host.currentSeason:nullptr);
        territory.publishOwnership(context.Get(),host.territoryOwnership);
        publishCurrentModels();
        frameState.acceptCamera(host.input,shadows);
        atmosphere.publishAcceptedCamera(frameState.atmosphereFrame(clouds));
        frameState.preparePopulation(context.Get(),*staticModels,bases,armies,shadows,groundDetails,
            commonPublication,commonClock.settingsFlags10);
        frameState.publishHdrAmbientSelection();

    };
    const auto animationFps=source_animation_cadence::readProfile(
        resources.directory(ResourceGroup::AnimationTiming)/"fps-source.bin");
    armies.requireAnimationTiming(animationFps);
    host.animationUpdate=[&](unsigned fpsMode) {
        if(fpsMode>=animationFps.fps.size())throw std::runtime_error("Actual FPS manager mode range");
        publishCurrentModels();
        const auto eventCamera=frameState.publishedEventCamera(host.input);
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
        postProcessing.updateMapExposure(context.Get(),frameState.season(),
            {frameState.environment().camera().exposure,frameState.environment().camera().luminance},frameState.environment().exposureRectangleFlags());
        fogAcceleration.updateCamera(context.Get(),staticCamera);
        phase.beginMain();

        fogAcceleration.execute(device.Get(),context.Get());
        const auto node3Delta=clouds.updateAcceptedFrame(device.Get(),context.Get(),
            {resources.directory(ResourceGroup::CloudWeather),resources.directory(ResourceGroup::AmbientFrame)},
            {frameState.season(),frameState.cloudLights(),currentClock});
        atmosphere.updateMainStars(device.Get(),frameState.atmosphereFrame(clouds),node3Delta);
        if (const auto* current = frameState.season())
            clouds.executePattern(device.Get(),context.Get(),current->pattern,frameState.camera(),
                atmosphere.currentHalfFovTangent(),frameState.preparedView().baseLight(),
                frameState.commonRenderPolicy(),frontendClip.shaderPlane(0));
        clouds.generateAmbient(context.Get(),currentClock,frameState.commonRenderPolicy());
    };
    auto publishThinSurface=[&]() {
        phase.publishThinSurface(frameState.view().rainGroups!=0);
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
        atmosphere.prepareMainStars(device.Get(),resources.directory(ResourceGroup::Sky),
            resources.directory(ResourceGroup::AtmosphereCamera)/"initial",starTwinkle);
        clouds.prepareAmbient(device.Get(),context.Get(),resources.directory(ResourceGroup::Ambient),
            atmosphere.stars(),frameState.framePower());
        clouds.prepareAtmosphereResources(device.Get(),resources.directory(ResourceGroup::SkyAtmosphere));
        clouds.bootstrapAmbient(context.Get(),frameState.commonRenderPolicy());
        clouds.preparePatternTargets(resources.directory(ResourceGroup::SkyPattern));
        clouds.preparePatternPrograms(resources.directory(ResourceGroup::SkyPattern)/"program-bank");
        context->ClearState(); // Bootstrap ends before any Main entry; not a per-request operation.
        {

            specialEffects=createNativeSpecialEffectsWithoutParticleOwner(sourceHostFlags3E4);
            fogAcceleration.prepare(device.Get(),resources.directory(ResourceGroup::FogAcceleration));
        }
        postProcessing.prepareExposure(device.Get(),context.Get(),resources.directory(ResourceGroup::MapExposure),normalInitialSource);
        lightingTarget=createSourceHdr(device.Get(),resources.directory(ResourceGroup::HdrProgramBank),width,height);
        litColor=lightingTarget.texture;lightingShaderFormat=lightingTarget.shaderFormat;lightingTargetFormat=lightingTarget.targetFormat;
        clouds.prepareClouds(device.Get(),resources.directory(ResourceGroup::Clouds),
            resources.directory(ResourceGroup::CloudAnimation),resources.directory(ResourceGroup::AmbientClouds),litColor.Get());
        beginAcceptedMain(initialCommonClock);
    }
    TerrainPass terrainPass{device.Get(),context.Get(),input,groupedTerrain};
    GBufferTargets geometryTargets{device.Get(),context.Get(),
        {width,height,resourceFormats,formats,depthFormat,targetNames,surfaces,
            staticCamera.optics.reversedDepth}};
    postProcessing.prepareOutput(device.Get(),resources.directory(ResourceGroup::FinalOutput),width,height);
    geometryTargets.prepareExpandedTargets(device.Get(),formats[3]);
    const bool hasVisibleModels=fs::exists(modelSource/"models.txt");
    if(hasVisibleModels){
        {
            staticModels->prepareGeometry(device.Get());
            shadows.connectGeometry(staticModels->geometry(),bases.primary().currentModels);
            staticModels->prepareStencil(device.Get(),resources.directory(ResourceGroup::View),frameState.viewDefinition(),frameState.view(),sourceHostFlags3E4);
            terrainPass.prepareCamera(device.Get(),input,indexCount,
                frameState.season()?resources.directory(ResourceGroup::SeasonalResources):fs::path{});
            shadows.prepareGpu(device.Get(),context.Get(),jointShadowInput/"gpu");

        }

    }
    // The real caller output survives the nested Main targets and is also
    // the output inherited by normal post109. Retain attachments/rects only.
    {auto raw=postProcessing.outputTarget();context->OMSetRenderTargets(1,&raw,nullptr);}
    context->RSSetViewports(1,&geometryTargets.viewport());context->RSSetScissorRects(1,&geometryTargets.scissor());
    auto callerTargets=NativeMainTargetScope::retain(context.Get());
    NativeSourceDepthState sourceDepth{};
    auto drawOpaqueGeometry=[&]() {
        geometryTargets.beginGeometry(context.Get(),staticCamera.optics.reversedDepth,sourceDepth);
        {
            const auto& staticSubmission=staticModels->updateSubmission(context.Get(),frameState.view());
            bases.updateSubmission(context.Get(),staticSubmission);
        }
        {
            groundDetails.update(context.Get());
            auto drawGroundNode=[&](uint32_t node){
                for(size_t record=0;record<frameState.viewDefinition().records.size();++record){
                    const auto& sourceRecord=frameState.viewDefinition().records[record];
                    ModelStencilBinding primitive{0,0,groundDetails.preparedExclusion(),1u<<record};
                    groundDetails.draw(context.Get(),node,{sourceRecord.groupMask,frameState.view().ordinaryCounts[record],sourceHostFlags3E4,
                        sourceRecord.usesOrdinaryCounter,selectModelStencil(frameState.viewDefinition().records,frameState.viewDefinition().modelStencil,frameState.view(),primitive,record,Interop::decodeModelStencilTarget(sourceHostFlags3E4))},geometryTargets.colors().data(),geometryTargets.depthView(),geometryTargets.viewport());
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
        for(const auto& record:frameState.viewDefinition().records) {
            if((frameState.viewDefinition().terrainExcludedGroups&record.groupMask)!=0)continue;
            uint32_t lightBits=0;
            if(terrainMaskNeedsSelectedLight(sourceHostFlags3E4,phase.deferredQueueFlags())) {
                const auto* light=frameState.season()?source_current_season::firstMatching(*frameState.season(),record.groupMask):nullptr;
                if(!light)throw std::runtime_error("Terrain current View record has no selected World light");
                lightBits=light->terrainMaskScalarBits;
            }
            const auto masks=terrainColorWriteMasks(sourceHostFlags3E4,phase.deferredQueueFlags(),lightBits,(_mm_getcsr()&0x40)!=0);
            terrainPass.draw(context.Get(),staticCamera,
                frameState.season()?std::optional<uint32_t>{frameState.season()->season}:std::nullopt,
                binding,sourceDepth,record.groupMask&255,masks);
        }
    }

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

        territory.draw(context.Get(),frameState.zoom().band,frameState.zoom().fraction,
            frameState.distance(),staticCamera.prefix,frameState.environment().territoryReferenceHeight(),
            postProcessing.mapExposureView(),providerDrawFlags,sourceDepth);
        updateNativeDepthMirror(context.Get(),mainDepth,node==122?"122 PB Pop40000007":"60 PB Pop40000007");


    };
    drawCurrentTerritory(60,1);
    publishThinSurface();
    callerTargets.restore(context.Get());
    RenderRandomContext renderRandom;
    const auto renderRandomClock=prepareNativeCameraClock();
    OceanPass ocean;
    auto currentSpecularSource=[&]() {
        const auto* current=frameState.season();
        if(!current||!current->hdrSelection)throw std::runtime_error("Current refraction Camera selection is absent");
        return current->hdrSelection->specularSourceIndex;
    };
    auto filteredSpecularImages=[&]() {
        return NativeSourceHdrSpecularImages{{ocean.filteredSpecular(0),ocean.filteredSpecular(1)},
            {ocean.filteredSpecularKey(0),ocean.filteredSpecularKey(1)}};
    };
    ocean.prepareRefractionTargets(resources.directory(ResourceGroup::HdrProgramBank)/"refraction-targets.txt");
    postProcessing.prepareSeason(resources.directory(ResourceGroup::SeasonalResources),frameState.season());
    auto executeCurrentPost=[&](float currentClock) {
        return postProcessing.execute(device.Get(),context.Get(),sourceDepth,
            resources.directory(ResourceGroup::SeasonalResources),frameState.season(),staticCamera,currentClock);
    };
    ComPtr<ID3D11Texture2D> postInputSnapshot,cloudInputSnapshot;
    NativeSourceDepthState cloudEntryLogical{};
    ComPtr<ID3D11DepthStencilState> cloudEntryDepth;
    ComPtr<ID3D11BlendState> cloudEntryBlend;
    ComPtr<ID3D11RasterizerState> cloudEntryRaster;
    UINT cloudEntryReference=0,cloudEntrySampleMask=~0u;float cloudEntryFactor[4]{};
    const auto receiverDepthObject=static_cast<std::uint32_t>(std::stoul(geometryTargets.depthKey()));
    auto receiverCurrentFrame=[&]() -> const source_current_season::Frame& {
        const auto* current=frameState.season();
        if(!current)throw std::runtime_error("Shadow receiver requires current season inputs");
        return *current;
    };
    NativeRs69Fallback fallback69;
    DeferredLightingPass hdr78,hdr123,hdr166;
    ComPtr<ID3D11RenderTargetView> indirectTarget;
    NativePrimaryForwardSource primaryForwardSource;
    NativePrimaryForwardGpu primaryForwardGpu;
    NativePrimaryForwardFrame primaryForwardFrame;
    bool primaryForwardReady=false;
    auto drawCurrentPrimaryForward=[&](float commonClock) {

        if(!primaryForwardReady) {
            const auto directory=resources.directory(ResourceGroup::ForwardLighting);
            primaryForwardSource=prepareNativePrimaryForwardCurrentBankSource(directory);
            primaryForwardGpu=prepareNativePrimaryForwardCurrentBankGpu(device.Get(),context.Get(),directory);

            primaryForwardReady=true;
        }
        if (!frameState.season()) throw std::runtime_error("Forward requires current typed lighting");
        const auto palette=clouds.receiverPatternPalette();
        primaryForwardFrame=prepareNativePrimaryForwardCurrent(primaryForwardSource,bases.primary().source,bases.plan(),
            frameState.viewDefinition(),*frameState.season(),frameState.environment().camera().eye,
            shadows.receiverShadowValues(),palette,shadows.patternResources(),commonClock);
         //Allocation observation, not a source-pixel render.
        executeNativePrimaryForwardGpu(context.Get(),sourceDepth,primaryForwardGpu,primaryForwardFrame,bases.primary().currentModels,
            hdr166.targetView(),geometryTargets.depthView(),geometryTargets.viewport(),shadows.shadowView(),
            hdr166.bank().ggx.Get(),[&]{return contextEnvironment.select(clouds.environmentImage());},fogAcceleration.samplingView());

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
        shadows.prepareGeometryReceiver(staticCamera);
        shadows.executeGeometryReceiver(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),receiverDepthObject,
            receiverCurrentFrame(),frameState.preparedView(),clouds.receiverPatternPalette(),initialCommonClock);
    }
    fallback69=prepareRs69Fallback(device.Get(),input,geometryTargets.textures()[2].Get(),formats[2],width,height);
    executeRs69Fallback(device.Get(),context.Get(),sourceDepth,fallback69,geometryTargets.depthView());
    isolateCurrentShadowVisibility();
    callerTargets.restore(context.Get()); //70
    {
        {
            auto earlySource=resources.directory(ResourceGroup::HdrProgramBank);
            D3D11_RENDER_TARGET_VIEW_DESC hdrDesc{};hdrDesc.Format=lightingTargetFormat;hdrDesc.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
            ComPtr<ID3D11RenderTargetView> hdrTarget;check(device->CreateRenderTargetView(litColor.Get(),&hdrDesc,&hdrTarget),"Main73 retained HDR target");
            auto mainTargets=NativeMainTargetScope::main73(hdrTarget.Get(),geometryTargets.depthView(),geometryTargets.viewport(),geometryTargets.scissor());
            mainTargets.restore(context.Get());
            hdr78.prepare(device.Get(),earlySource,78,litColor.Get(),geometryTargets.textures(),shaderFormats,ggx.Get());
            {
                hdr78.update(context.Get(),frameState.lightingFrame(phase.deferredQueueFlags(),initialCommonClock));
            }
            hdr78.execute(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView(),contextEnvironment.select(clouds.environmentImage()),nullptr,nullptr,
                nullptr,initialCommonClock,frameState.view().activeGroups,frameState.view().oceanGroups);
            mainTargets.restore(context.Get());
            {
                ocean.prepareSceneCopy(device.Get(),context.Get(),sourceDepth,
                    resources.directory(ResourceGroup::Refraction)/"copy-scene97",litColor.Get(),lightingShaderFormat,lightingTarget.logicalKey);
            }
            ocean.prepareDepthCopy(device.Get(),context.Get(),sourceDepth,
                resources.directory(ResourceGroup::Refraction)/"copy97",mainDepth.depth.Get(),geometryTargets.depthKey(),geometryTargets.depthSurface());
            mainTargets.restore(context.Get());
            {
                const OceanSources oceanSources{resources.directory(ResourceGroup::OceanTextures),
                    resources.directory(ResourceGroup::Water),resources.directory(ResourceGroup::Refraction)/"copy97",
                    resources.root()/"named-renderer-input"};
                ocean.prepareWater(device.Get(),context.Get(),oceanSources,geometryTargets.sixTargetScope(),
                    frameState.namedRenderer().oceanGrid(),staticCamera,initialCommonClock,
                    renderRandom,sampleRenderRandomSeed(renderRandomClock),frameState.season()?&frameState.season()->oceanWave:nullptr,
                    frameState.season()?&frameState.season()->oceanSurface:nullptr,
                    frameState.season()?&frameState.season()->oceanOptics:nullptr,
                    frameState.season()?&frameState.season()->oceanTextureMaterial:nullptr,
                    frameState.season()?&frameState.season()->oceanBubbleMaterial:nullptr,
                    frameState.oceanLightGroup(),frameState.commonRenderPolicy());
                ocean.executeWater(context.Get(),sourceDepth,frameState.oceanLightGroup(),frameState.commonRenderPolicy());
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
                shadows.prepareWaterReceiver(staticCamera);
                shadows.executeWaterReceiver(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),receiverDepthObject,
                    receiverCurrentFrame(),frameState.preparedView(),clouds.receiverPatternPalette(),initialCommonClock);
                isolateCurrentShadowVisibility();
                mainTargets.restore(context.Get()); //121
                drawCurrentTerritory(122,2);
                publishThinSurface();
                hdr123.prepare(device.Get(),earlySource,123,litColor.Get(),geometryTargets.textures(),shaderFormats,ggx.Get());
                {
                    hdr123.update(context.Get(),frameState.lightingFrame(phase.deferredQueueFlags(),initialCommonClock,&ocean.refractionCamera()));
                }
                hdr123.execute(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView(),contextEnvironment.select(clouds.environmentImage()),ocean.refractionDepth(),ocean.transmittedScene(),
                    nullptr,initialCommonClock,frameState.view().activeGroups,frameState.view().oceanGroups);
                mainTargets.restore(context.Get());
                atmosphere.prepare(device.Get(),context.Get(),resources.directory(ResourceGroup::Sky),
                    resources.directory(ResourceGroup::Atmosphere),
                    litColor.Get(),mainDepth.depth.Get(),fogAcceleration.samplingView(),geometryTargets.depthView(),width,height,frameState.atmosphereFrame(clouds));
                atmosphere.executeSky(device.Get(),context.Get(),sourceDepth,frameState.atmosphereFrame(clouds),mainTargets,
                    postProcessing.mapExposureView(),postProcessing.mapExposureSampler(),clouds.renderingAtmosphere());
                mainTargets.restore(context.Get());
                clouds.execute(device.Get(),context.Get(),sourceDepth,geometryTargets.depthView(),initialCommonClock,
                    UINT(phase.hostCompletionFlags()),frameState.commonRenderPolicy());
                mainTargets.restore(context.Get());
                atmosphere.executeFog(device.Get(),context.Get(),sourceDepth,
                    frameState.season(),initialCommonClock,
                    std::optional<unsigned>(frameState.view().activeGroups));
                ;
                mainTargets.restore(context.Get());
                ocean.prepareFilters(device.Get(),resources.directory(ResourceGroup::Refraction)/"filter137",litColor.Get(),lightingShaderFormat,mainDepth.depth.Get());
                ocean.executeFilters(context.Get(),sourceDepth,currentSpecularSource());
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
                applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,phase,141);
                mainTargets.restore(context.Get()); //142, B616 has no mirror
                geometryTargets.sixTargetScope().restore(context.Get()); //143
                std::array<ID3D11RenderTargetView*,6> sixExpected{};
                for(UINT i=0;i<6;++i)sixExpected[i]=geometryTargets.sixTargetScope().colors[i].Get();
                ;
                applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,phase,147);
                updateNativeDepthMirror(context.Get(),mainDepth,"148 after source special zero-tail state");
                mainTargets.restore(context.Get());
                // Q400 closes150/154;142 belongs to distinct B616, not MainD1.
                geometryTargets.sixTargetScope().restore(context.Get()); //156
                ;
                updateNativeDepthMirror(context.Get(),mainDepth,"160 Rain writers pending");
                mainTargets.restore(context.Get());
                publishThinSurface();
                //162/165 retain their source RS3 scope; this finite cohort
                //has no process60 writer between them.
                context->OMSetRenderTargets(1,&rsTarget,geometryTargets.depthView());
                mainTargets.restore(context.Get());
                hdr166.prepare(device.Get(),earlySource,166,litColor.Get(),geometryTargets.textures(),shaderFormats,ggx.Get());
                {
                    hdr166.update(context.Get(),frameState.lightingFrame(phase.deferredQueueFlags(),initialCommonClock,&ocean.refractionCamera(),filteredSpecularImages()));
                }
                hdr166.execute(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView(),contextEnvironment.select(clouds.environmentImage()),ocean.refractionDepth(),ocean.transmittedScene(),
                    ocean.blurredDepth(),initialCommonClock,frameState.view().activeGroups,frameState.view().oceanGroups);
                mainTargets.restore(context.Get()); //165 and late HDR return to73
                ;
                {
                    drawCurrentPrimaryForward(initialCommonClock); //184..187, before Effects188 and mirror191.
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
                    phase.completeOuterApply();
                    {
                        const auto postCaller=NativeMainTargetScope::retain(context.Get());
                        auto sameOutput=postProcessing.outputTarget();
                        context->OMSetRenderTargets(1,&sameOutput,geometryTargets.depthView()); //194, no clear
                        ;

                        postCaller.restore(context.Get()); //203
                        ComPtr<ID3D11DepthStencilView> tailDepth;context->OMGetRenderTargets(0,nullptr,&tailDepth);
                        //204 is open;205 makes no attachment change.206 only
                        //clears depth/stencil if the actual caller has a DSV.
                        if(tailDepth)context->ClearDepthStencilView(tailDepth.Get(),D3D11_CLEAR_DEPTH|D3D11_CLEAR_STENCIL,geometryDepthClear(staticCamera.optics.reversedDepth),0);

                    }
                }
            }
        }

    }
        auto executeRetainedMain=[&](float currentClock) {
        {
            // Normal1184320: publish completed geometry BEFORE the shared
            // grid clock writer starts the next job. No frame-number gate.
            ocean.publishCompletedGeometry(context.Get(),currentClock,host.currentSeason?&host.currentSeason->oceanWave:nullptr,
                host.currentSeason?&host.currentSeason->oceanSurface:nullptr,
                host.currentSeason?&host.currentSeason->oceanOptics:nullptr,
                host.currentSeason?&host.currentSeason->oceanTextureMaterial:nullptr,
                host.currentSeason?&host.currentSeason->oceanBubbleMaterial:nullptr,
                frameState.oceanLightGroup(host.currentSeason?&*host.currentSeason:nullptr),
                frameState.commonRenderPolicy(host.currentSeason?&*host.currentSeason:nullptr));

        }
        beginAcceptedMain(currentClock);
        {
            shadows.updateAcceptedFrame(context.Get(),sourceDepth,bases.populationChanged(),frameState.season(),staticCamera);
        }
        drawOpaqueGeometry();updateNativeDepthMirror(context.Get(),mainDepth,"59 new Main geometry");

        drawCurrentTerritory(60,1);
        publishThinSurface();
        callerTargets.restore(context.Get());
        auto abTarget=geometryTargets.colors()[0];context->OMSetRenderTargets(1,&abTarget,geometryTargets.depthView());
        shadows.executeContact(context.Get(),sourceDepth,geometryTargets.viewport());callerTargets.restore(context.Get());
        auto rsTarget=indirectTarget.Get();context->OMSetRenderTargets(1,&rsTarget,geometryTargets.depthView());
        shadows.executeGeometryReceiver(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),receiverDepthObject,
            receiverCurrentFrame(),frameState.preparedView(),clouds.receiverPatternPalette(),currentClock);
        executeRs69Fallback(device.Get(),context.Get(),sourceDepth,fallback69,geometryTargets.depthView());callerTargets.restore(context.Get());
        isolateCurrentShadowVisibility();
        auto mainTargets=NativeMainTargetScope::main73(postProcessing.sceneTarget(),geometryTargets.depthView(),geometryTargets.viewport(),geometryTargets.scissor());
        mainTargets.restore(context.Get());
        {
            hdr78.update(context.Get(),frameState.lightingFrame(phase.deferredQueueFlags(),currentClock));
        }
        hdr78.execute(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView(),contextEnvironment.select(clouds.environmentImage()),nullptr,nullptr,
            nullptr,currentClock,frameState.view().activeGroups,frameState.view().oceanGroups);
        mainTargets.restore(context.Get());
        ocean.copyScene(context.Get(),sourceDepth);
        ocean.copyDepth(context.Get(),sourceDepth);mainTargets.restore(context.Get());
        ocean.updateCamera(context.Get(),staticCamera);
        ocean.executeWater(context.Get(),sourceDepth,frameState.oceanLightGroup(),frameState.commonRenderPolicy());
        updateNativeDepthMirror(context.Get(),mainDepth,"117 new Main Ocean");mainTargets.restore(context.Get());
        context->OMSetRenderTargets(1,&rsTarget,geometryTargets.depthView());
        shadows.executeWaterReceiver(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),receiverDepthObject,
            receiverCurrentFrame(),frameState.preparedView(),clouds.receiverPatternPalette(),currentClock);mainTargets.restore(context.Get());
        isolateCurrentShadowVisibility();
        drawCurrentTerritory(122,2);
        publishThinSurface();
        {
            hdr123.update(context.Get(),frameState.lightingFrame(phase.deferredQueueFlags(),currentClock,&ocean.refractionCamera()));
        }
        hdr123.execute(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView(),contextEnvironment.select(clouds.environmentImage()),ocean.refractionDepth(),ocean.transmittedScene(),
            nullptr,currentClock,frameState.view().activeGroups,frameState.view().oceanGroups);
        atmosphere.updateCamera(context.Get(),frameState.atmosphereFrame(clouds));
        mainTargets.restore(context.Get());
        atmosphere.executeSky(device.Get(),context.Get(),sourceDepth,frameState.atmosphereFrame(clouds),mainTargets,
            postProcessing.mapExposureView(),postProcessing.mapExposureSampler(),clouds.renderingAtmosphere());
        mainTargets.restore(context.Get());
        clouds.execute(device.Get(),context.Get(),sourceDepth,geometryTargets.depthView(),currentClock,
            UINT(phase.hostCompletionFlags()),frameState.commonRenderPolicy());
        mainTargets.restore(context.Get());
        atmosphere.executeFog(device.Get(),context.Get(),sourceDepth,
            frameState.season(),currentClock,frameState.view().activeGroups);mainTargets.restore(context.Get());
        ocean.executeFilters(context.Get(),sourceDepth,currentSpecularSource());mainTargets.restore(context.Get());
        std::array<ID3D11ShaderResourceView*,32> empty{};context->VSSetShaderResources(0,32,empty.data());context->PSSetShaderResources(0,32,empty.data());
        context->OMSetRenderTargets(0,nullptr,ocean.depthTarget());applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,phase,141);
        mainTargets.restore(context.Get());geometryTargets.sixTargetScope().restore(context.Get());applyNativeSpecialEffects(device.Get(),context.Get(),sourceDepth,specialEffects,phase,147);
        updateNativeDepthMirror(context.Get(),mainDepth,"148 new Main special");mainTargets.restore(context.Get());
        geometryTargets.sixTargetScope().restore(context.Get());updateNativeDepthMirror(context.Get(),mainDepth,"160 new Main rain source-closed");mainTargets.restore(context.Get());
        publishThinSurface();
        context->OMSetRenderTargets(1,&rsTarget,geometryTargets.depthView());mainTargets.restore(context.Get());
        {
            hdr166.update(context.Get(),frameState.lightingFrame(phase.deferredQueueFlags(),currentClock,&ocean.refractionCamera(),filteredSpecularImages()));
        }
        hdr166.execute(device.Get(),context.Get(),sourceDepth,mainDepth.depth.Get(),geometryTargets.depthView(),contextEnvironment.select(clouds.environmentImage()),ocean.refractionDepth(),ocean.transmittedScene(),ocean.blurredDepth(),
            currentClock,frameState.view().activeGroups,frameState.view().oceanGroups);
        drawCurrentPrimaryForward(currentClock);

        mainTargets.restore(context.Get());

        updateNativeDepthMirror(context.Get(),mainDepth,"191 new Main; particle owner pending");callerTargets.restore(context.Get());
        groundDetails.finishRenderNode();
        executeCurrentPost(currentClock);
        phase.completeOuterApply();
        //194/203 alias the same Final109;206 remains closed with null callerDSV.
        const auto postCaller=NativeMainTargetScope::retain(context.Get());auto sameOutput=postProcessing.outputTarget();
        context->OMSetRenderTargets(1,&sameOutput,geometryTargets.depthView());postCaller.restore(context.Get());
        ++completedMainRequests;

        };
        {
            // All initialized owners remain in this coroutine frame. Godot
            // drives subsequent requests; no native window or polling thread.
            host.replaceAtmosphereResources=[&](const fs::path& root) {
                auto cloudImages=clouds.prepareAtmosphereResourceReplacement(device.Get(),root,atmosphere.skyImages());
                auto shared=cloudImages.textures;
                const auto& installedSky=atmosphere.skyImages();
                shared.insert(shared.end(),installedSky.begin(),installedSky.end());
                auto skyImages=atmosphere.prepareImageReplacement(device.Get(),root/"main-sky-input",shared,
                    cloudImages.atmosphere.renderingAtmosphere()!=nullptr);
                auto weather=frameState.prepareWeatherResourceReplacement(root/"weather-fog-camera-input"/"initial"/"weather",
                    cloudImages.atmosphere.selection().staticObjectKey);
                // All preparation can fail without changing installed owners.
                // These moves retain program, history, animation and clock state.
                clouds.installAtmosphereResourceReplacement(std::move(cloudImages));
                atmosphere.installImageReplacement(std::move(skyImages));
                frameState.installWeatherResourceReplacement(std::move(weather));
            };
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
