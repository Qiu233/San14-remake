#pragma once
#include "../Camera/CameraRig.h"
#include "../Scene/SceneEnvironmentPacket.h"

namespace San14::Render {

// Session-owned CPU frame state. Resource preparation is separate from accepted
// requests; pass/world owners remain external and are borrowed only during the
// ordered population preparation below. Include after the concrete pass owners.
class SceneFrameCoordinator {
public:
    explicit SceneFrameCoordinator(const source_current_season::Frame* initialSeason):season_(initialSeason){}
    SceneFrameCoordinator(const SceneFrameCoordinator&)=delete;
    SceneFrameCoordinator& operator=(const SceneFrameCoordinator&)=delete;

    void prepareCamera(const PreparedResourceDirectories& resources) {
        cameraRig_.prepare(resources.directory(ResourceGroup::Camera),
            resources.directory(ResourceGroup::DistantMap),resources.directory(ResourceGroup::CameraMotion));
        // Initialize only this renderer's consumed fields from project rules
        // and the current resource declarations; season publishes live fields.
        environment_.initialize(SceneEnvironmentPacket::decodeInitial(
            load(resources.directory(ResourceGroup::CameraInitialState)/"profile.bin")));
    }
    void prepareLightingProfiles(const PreparedResourceDirectories& resources) {
        deferredProfile_=prepareNativeDeferredCameraProfile(resources.directory(ResourceGroup::DeferredCamera));
        weatherProfile_=source_weather_fog::readProfile(resources.directory(ResourceGroup::Weather)/"initial"/"weather");
        weatherConsumers_=prepareNativeWeatherFogConsumers(resources.directory(ResourceGroup::WeatherFog));
    }
    // Accessories extend these retained profiles once. The mutable profile
    // borrow is confined to this resource preparation call, never exposed.
    void prepareBaseAccessories(ID3D11Device* device,ID3D11DeviceContext* context,
        const PreparedResourceDirectories& resources,const std::optional<AreaCapitalWorldPublication>& areaWorld,
        const source_default_player_phase::Profile& defaults,BaseRenderWorld& bases) {
        bases.prepareAccessories(device,context,resources,areaWorld,defaults,deferredProfile_,weatherConsumers_);
    }
    void prepareWeatherLight(const PreparedResourceDirectories& resources) {
        const auto globalRgb=load(resources.directory(ResourceGroup::Weather)/"initial"/"weather"/"effective-light-rgb.bin");
        if(globalRgb.size()!=sizeof(globalLight_))throw std::runtime_error("Original global80 RGB extent differs");
        std::memcpy(&globalLight_,globalRgb.data(),globalRgb.size());
    }
    void prepareView(const PreparedResourceDirectories& resources) {
        viewProfile_=source_view::loadProfile(resources.directory(ResourceGroup::View)/"profile.bin");
    }
    void bindFieldDisplay(ShadowPass& shadows,const NativeModelContinuous& catalog) {
        shadows.bindFieldDisplay(fieldDisplay_,cameraRig_.distantProfile(),catalog);
    }
    // The host keeps this accepted season alive until the request yields. No
    // pointer to RenderHost or its mutable RenderInput is retained.
    void acceptRequest(const RenderInput& input,const source_current_season::Frame* season) {
        season_=season;cloudLights_={input.currentRawSunPosition,input.currentCloudGeometry40};
    }
    void acceptCamera(const RenderInput& input,ShadowPass& shadows) {
        cameraRig_.accept(input);
        const int sceneMode=cameraRig_.display().mode.sceneMode;
        shadows.selectFamily(sceneMode);
        fieldDisplay_.capture(cameraRig_.display());
        if(season_) {
            SceneEnvironmentPacket::acceptSeason(environment_,*season_);
            shadows.publishSeason(*season_);
            weather_.fog=SceneEnvironmentPacket::decodeSeasonFog(*season_);
        }else {
            const auto directCamera=cameraRig_.prepareDirectPrefix();
            environment_.acceptCamera(directCamera,SceneEnvironmentPacket::decodeCamera(directCamera));
            weather_=source_weather_fog::prepareCurrent(weatherProfile_,cameraRig_.frame().eye.Y,environment_.camera().weatherHeight);
        }
    }
    // Atmosphere publication remains between acceptCamera and this call.
    void preparePopulation(ID3D11DeviceContext* context,StaticModelResources& models,
        BaseRenderWorld& bases,ArmyRenderWorld& armies,ShadowPass& shadows,
        ground_details::GroundDetails& groundDetails,const NativeFrameClockPublication& clock,UINT settingsFlags) {
        models.selectCamera(cameraRig_.frame(),fieldDisplay_);
        bases.selectCamera(cameraRig_.frame());
        shadows.prepareCamera(cameraRig_.frame(),fieldDisplay_);
        ordinaryCounts_=shadows.frame().ordinaryRecordCounts;
        bases.withAccessories([&](auto& cityAccessories,auto& areaAccessories) {
            armies.selectCamera(context,cameraRig_.frame(),cityAccessories,areaAccessories,
                viewProfile_,ordinaryCounts_,accessoryCount_);
        });
        if(ordinaryCounts_.size()!=bases.plan().ordinaryCounts.size())throw std::runtime_error("Field/City ordinary record identities differ");
        // Vegetation follows the accepted camera and retains current weather
        // and wind. Geometry uses the ordinary camera transform.
        environment_.publishVegetationCamera();
        const auto& mode=cameraRig_.display().mode;
        const auto& groundCaller=bases.citySource().caller;
        const auto& groundPopulation=groundDetails.prepare({cameraRig_.frame(),
            environment_.vegetation(),clock,
            {viewProfile_.commonPresent!=0,viewProfile_.commonFlags,viewProfile_.commonExclusion},settingsFlags,
            {groundCaller.callerFirst,groundCaller.callerSecond,groundCaller.callerShadow,groundCaller.registered3330,groundCaller.successful3340,0},
            {mode.sceneMode,mode.zoomLevel,true,false,0},season_?season_->season:0});
        if(groundPopulation.seasonPopulationChanged) {
            for(size_t record=0;record<viewProfile_.records.size();++record)
                ordinaryCounts_[record]+=groundDetails.preparedCountForRecord(viewProfile_.records[record].mask);
        }
        // The joint source profile already counts every admitted primary World
        // once before its ordinary54/55 split. Do not add the rows again here.
        source_view::Inputs population;
        population.ordinaryCounts=ordinaryCounts_;
        population.collectionPrimitiveCounts=shadows.frame().primitiveCounts;
        population.subWrapperCount1BFC=0; // This finite renderer owns no optional wrapper.
        population.terrainPresent=!cameraRig_.frame().instances.empty();
        population.selectedCompactPresent=shadows.frame().passes[0].shadowAvailable!=0;
        population.selectedLitPresent=shadows.directionalView()!=nullptr;
        population.selectedReceiveShadowMode=viewProfile_.initialReceiveShadowMode; // Initial non-season import only.
        if(season_) {
            viewProfile_.mainA00=shadows.seasonParameters().selectedLightGroup;
            viewProfile_.mainA28=shadows.seasonParameters().planeScale;
            viewProfile_.mainA3C=static_cast<uint32_t>(source_current_season_shadow::required(season_->shadow.compactMode));
            population.selectedReceiveShadowMode=0; // Disabled selection does not query World.
            if(viewProfile_.mainA00>0) {
                // publishSeason already selected and admitted this same ordered World light.
                const auto& selected=*source_current_season::firstMatching(*season_,shadows.seasonParameters().selectedMask);
                population.selectedReceiveShadowMode=selected.shadowModes.receive;
            }
        }
        view_=source_view::prepareCurrent(viewProfile_,population);

        deferredFields_=prepareNativeDeferredCameraFields(cameraRig_.frame());
        fieldDisplay_.finishPreparation();
    }
    AtmosphereFrame atmosphereFrame(CloudEnvironmentPass& clouds) {
        return clouds.atmosphereFrame(season_,cloudLights_,environment_.camera().weatherHeight,
            weather_,globalLight_,cameraRig_.frame());
    }
    DeferredLightingFrame lightingFrame(const source_water_camera::RefractionDerived* refraction=nullptr)const {
        return {deferredProfile_,deferredFields_,season_,weatherConsumers_,weather_,refraction};
    }
    source_camera::Frame publishedEventCamera(const RenderInput& input)const {return cameraRig_.publishedEventCamera(input);}
    const source_camera::Frame& camera()const {return cameraRig_.frame();}
    const SceneEnvironment& environment()const {return environment_;}
    const source_view::Profile& viewProfile()const {return viewProfile_;}
    const source_view::Frame& view()const {return view_;}
    const NativeDeferredCameraProfile& deferredProfile()const {return deferredProfile_;}
    const NativeWeatherFogConsumers& weatherConsumers()const {return weatherConsumers_;}
    const source_current_season::Frame* season()const {return season_;}
    const source_current_season_atmosphere::CloudLightInput& cloudLights()const {return cloudLights_;}
    source_camera::Zoom zoom()const {return cameraRig_.zoom();}
    double distance()const {return cameraRig_.distance();}
private:
    CameraRig cameraRig_;
    FieldDisplay fieldDisplay_;
    SceneEnvironment environment_;
    NativeDeferredCameraProfile deferredProfile_;
    NativeDeferredCameraFields deferredFields_;
    source_weather_fog::Profile weatherProfile_;
    source_weather_fog::Frame weather_;
    source_camera::V3 globalLight_{};
    NativeWeatherFogConsumers weatherConsumers_;
    source_view::Profile viewProfile_;
    source_view::Frame view_;
    std::vector<unsigned> ordinaryCounts_;
    unsigned accessoryCount_{};
    const source_current_season::Frame* season_{};
    source_current_season_atmosphere::CloudLightInput cloudLights_{};
};
} // namespace San14::Render
