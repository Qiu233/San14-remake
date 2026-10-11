#pragma once
#include "../Camera/CameraRig.h"
#include "../Scene/SceneEnvironmentPacket.h"
#include "../Scene/View/PreparedMapView.h"
#include "../Scene/View/Interop/MapViewProfileCodec.h"
#include "../Scene/View/Interop/NamedMapRendererInput.h"
#include "../Passes/Environment/Ocean/Interop/OceanGridInput.h"
#include "../Passes/Environment/Ocean/Interop/OceanSurfaceInput.h"
#include <memory>

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
    void prepareWeatherProfile(const PreparedResourceDirectories& resources) {
        weatherProfile_=source_weather_fog::readProfile(resources.directory(ResourceGroup::Weather)/"initial"/"weather");
    }
    // Accessories retain their device and animation owners independently from
    // HDR program selection and its current numeric layouts.
    void prepareBaseAccessories(ID3D11Device* device,ID3D11DeviceContext* context,
        const PreparedResourceDirectories& resources,const std::optional<AreaCapitalWorldPublication>& areaWorld,
        const source_default_player_phase::Profile& defaults,BaseRenderWorld& bases) {
        bases.prepareAccessories(device,context,resources,areaWorld,defaults);
    }
    void prepareWeatherLight(const PreparedResourceDirectories& resources) {
        const auto globalRgb=load(resources.directory(ResourceGroup::Weather)/"initial"/"weather"/"effective-light-rgb.bin");
        if(globalRgb.size()!=sizeof(globalLight_))throw std::runtime_error("Original global80 RGB extent differs");
        std::memcpy(&globalLight_,globalRgb.data(),globalRgb.size());
    }
    void prepareView(const PreparedResourceDirectories& resources) {
        const auto directory = resources.directory(ResourceGroup::NamedRenderer);
        const auto surface = season_ ? season_->oceanSurface
            : Interop::readOceanSurfaceSettings(directory / "ocean-surface.bin");
        namedRenderer_.emplace(
            Interop::readNamedMapRendererDeclaration(directory / "scene-renderer.bin"),
            Interop::readOceanGridSettings(directory / "ocean-grid.bin"), surface, std::nullopt);
        initialHeightFog_ = Interop::readHeightFogSettings(
            resources.directory(ResourceGroup::Atmosphere) / "initial-height.bin");
        viewPreparation_.emplace(Interop::readMapViewProfile(
            resources.directory(ResourceGroup::View) / "profile.bin"));
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
        auto& view=*viewPreparation_;
        view.beginPopulation(shadows.frame().ordinaryRecordCounts);
        bases.withAccessories([&](auto& cityAccessories,auto& areaAccessories) {
            view.withArmyPopulation([&](const auto& profile,auto& counts,auto& accessoryCount) {
                armies.selectCamera(context,cameraRig_.frame(),cityAccessories,areaAccessories,
                    profile,counts,accessoryCount);
            });
        });
        view.requirePrimaryRecordCount(bases.plan().ordinaryCounts.size());
        // Vegetation follows the accepted camera and retains current weather
        // and wind. Geometry uses the ordinary camera transform.
        environment_.publishVegetationCamera();
        const auto& mode=cameraRig_.display().mode;
        const auto& groundCaller=bases.citySource().caller;
        const auto& groundPopulation=groundDetails.prepare({cameraRig_.frame(),
            environment_.vegetation(),clock,
            {view.definition().common.present,view.definition().common.featureFlags,view.definition().common.excludedCategories},settingsFlags,
            {groundCaller.callerFirst,groundCaller.callerSecond,groundCaller.callerShadow,groundCaller.registered3330,groundCaller.successful3340,0},
            {mode.sceneMode,mode.zoomLevel,true,false,0},season_?season_->season:0});
        view.addGroundDetails(groundPopulation.seasonPopulationChanged,
            [&](unsigned mask) { return groundDetails.preparedCountForRecord(mask); });
        // The joint source profile already counts every admitted primary World
        // once before its ordinary54/55 split. Do not add the rows again here.
        MapViewPopulation population;
        population.shadowCasterCounts=shadows.frame().primitiveCounts;
        population.subShadowPrimitiveCount=0; // This finite renderer owns no optional wrapper.
        population.terrainVisible=!cameraRig_.frame().instances.empty();
        population.compactShadowAvailable=shadows.frame().passes[0].shadowAvailable!=0;
        population.directionalDiffuseAvailable=shadows.directionalView()!=nullptr;
        std::optional<MapViewShadowSelection> shadow;
        if(season_) {
            const auto& current=shadows.seasonParameters();
            shadow.emplace(MapViewShadowSelection{*season_,current.selectedLightGroup,
                current.planeScale,current.selectedMask,season_->shadow.compactMode});
        }
        auto frame = view.preparePopulation(std::move(population), shadow);
        auto camera = prepareNativeDeferredCameraFields(cameraRig_.frame());
        const auto& heightFog = season_ ? season_->heightFog : initialHeightFog_;
        std::array<std::uint32_t, 3> modes;
        for (std::size_t i = 0; i < modes.size(); ++i) modes[i] = heightFog.layers[i].mode;
        std::optional<std::size_t> selectedShadow;
        if (season_ && shadows.seasonParameters().selectedLightGroup > 0) {
            const auto* selected = source_current_season::firstMatching(*season_, shadows.seasonParameters().selectedMask);
            if (!selected) throw std::runtime_error("Prepared HDR View has no selected shadow light");
            selectedShadow = std::size_t(selected - season_->lights.data());
        }
        PreparedHdrViewInputs hdr{view.localLightQueries(),view.preRenderer(),&shadows.compactCpuProvider(),
            selectedShadow,shadows.frame().passes[0].shadowAvailable != 0};
        auto prepared = std::make_unique<PreparedMapView>(0, std::move(frame), modes, std::move(hdr));
        fieldDisplay_.finishPreparation();
        deferredFields_ = std::move(camera);
        mapView_ = std::move(prepared);
    }
    // Scene publication follows the actual View construction operation and
    // precedes Main traversal. Repeated queries never trigger publication.
    void publishHdrAmbientSelection() {
        if (mapView_) mapView_->publishAmbientSelection(
            namedRenderer_ ? &*namedRenderer_ : nullptr);
    }
    NamedMapRenderer& namedRenderer() { return namedRenderer_.value(); }
    const PreparedMapView& preparedView() const { return *mapView_; }
    AtmosphereFrame atmosphereFrame(CloudEnvironmentPass& clouds) {
        return clouds.atmosphereFrame(season_,cloudLights_,environment_.camera().weatherHeight,
            weather_,globalLight_,cameraRig_.frame());
    }
    DeferredLightingFrame lightingFrame(std::uint32_t phaseFlags, float commonClock,
        const source_water_camera::RefractionDerived* refraction=nullptr)const {
        return {deferredFields_,*mapView_,viewPreparation_->definition(),viewPreparation_->cameraSettings(),
            season_,phaseFlags,commonClock,refraction};
    }
    source_camera::Frame publishedEventCamera(const RenderInput& input)const {return cameraRig_.publishedEventCamera(input);}
    const source_camera::Frame& camera()const {return cameraRig_.frame();}
    const SceneEnvironment& environment()const {return environment_;}
    const MapViewDefinition& viewDefinition()const {return viewPreparation_->definition();}
    const MapViewFrame& view()const {return mapView_->frame();}
    const source_current_season::Frame* season()const {return season_;}
    const source_current_season_atmosphere::CloudLightInput& cloudLights()const {return cloudLights_;}
    source_camera::Zoom zoom()const {return cameraRig_.zoom();}
    double distance()const {return cameraRig_.distance();}
private:
    CameraRig cameraRig_;
    FieldDisplay fieldDisplay_;
    SceneEnvironment environment_;
    NativeDeferredCameraFields deferredFields_;
    source_weather_fog::Profile weatherProfile_;
    source_weather_fog::Frame weather_;
    source_camera::V3 globalLight_{};
    std::optional<MapViewPreparation> viewPreparation_;
    std::unique_ptr<PreparedMapView> mapView_;
    std::optional<NamedMapRenderer> namedRenderer_;
    HeightFogSettings initialHeightFog_;
    const source_current_season::Frame* season_{};
    source_current_season_atmosphere::CloudLightInput cloudLights_{};
};
} // namespace San14::Render
