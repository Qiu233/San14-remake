#pragma once
// Include after camera/season, primary population and shadow GPU writers.
namespace San14::Render {

// The pass owns its catalog, accepted camera history, depth atlas and receiver
// packets. Main owns attachment scopes and schedules each shadow contribution.
class ShadowPass {
public:
    ShadowPass()=default;
    ShadowPass(const ShadowPass&)=delete;
    ShadowPass& operator=(const ShadowPass&)=delete;

    void preparePopulation(const fs::path& source,const NativeModelContinuous& field,
        const NativeCityPopulationSource& cities,const NativeCityAreaPrimary& primary) {
        cameras_[0]=source_shadow_camera::loadProfile(source/"initial"/"cpu-profile.bin");
        cameras_[1]=source_shadow_camera::loadProfile(source/"mode1"/"cpu-profile.bin");
        parameters_[0]=source_shadow_gpu::loadProfile(source/"initial"/"gpu-profile.bin");
        parameters_[1]=source_shadow_gpu::loadProfile(source/"mode1"/"gpu-profile.bin");
        cityGate_=loadNativeCityGateShadowSource(source,field,cities);
        publishNativeCityAreaJointShadow(population_,*cityGate_,field,primary,cameras_,parameters_);
        populationPrepared_=true;
    }
    void bindFieldDisplay(FieldDisplay& display,const source_far_main::Profile& source,
        const NativeModelContinuous& field) const {
        display.bind(source,field,cameras_);
    }
    void prepareRaster(const fs::path& source) {
        raster_[0]=loadNativeShadowRasterSource(source/"initial");
        raster_[1]=loadNativeShadowRasterSource(source/"mode1");
    }
    void publishPrimaryPopulation(const NativeModelContinuous& field,
        const NativeCityAreaPrimary& primary,const StaticModelResources& models) {
        publishNativeCityAreaJointShadow(population_,*cityGate_,field,primary,cameras_,parameters_);
        if(models.hasGeometry())connectNativeCityAreaJointShadowGeometry(population_,models.geometry(),primary);
    }
    void selectFamily(int renderMode) {family_=renderMode==0?0u:1u;}
    void publishSeason(const source_current_season::Frame& season) {
        season_=source_current_season_shadow::prepareUnclampedCompact(season);
        source_current_season_shadow::publish(season_,cameras_[family_],parameters_[family_]);
    }
    void publishCollectionAdmission(const source_current_season::Frame* current,
        const MapViewCommonSettings& common,const NativeCityAreaPrimary& primary,
        const NativeModelContinuous& field) {
        auto& admission=cityGate_.value().admission[family_];
        admission.commonExclusion404=common.excludedCategories;
        if(current) {
            admission.selectedRecord1BD0=season_.selectedLightGroup>0;
            admission.lights.clear();admission.lights.reserve(current->lights.size());
            for(const auto& light:current->lights)
                admission.lights.push_back({light.mask,light.firstCasterFlags,0,0,0});
        }
        publishNativeCityAreaJointShadow(population_,*cityGate_,field,primary,cameras_,parameters_);
    }
    void prepareCamera(const source_camera::Frame& camera,const FieldDisplay& display,
        const NativeModelCollectionCaller& caller) {
        display.publishShadow(cameras_[family_],caller,cityGate_->admission[family_]);
        frame_=source_shadow_camera::prepareCurrent(cameras_[family_],camera,population_.catalog,history_);
    }
    void connectGeometry(const NativeModelInputs& field,const NativeModelInputs& primaries) {
        connectNativeJointPopulationShadowGeometry(population_,field,primaries);
    }
    void prepareGpu(ID3D11Device* device,ID3D11DeviceContext* context,const fs::path& source) {
        gpu_=prepareNativeShadowCameraGpu(device,parameters_[family_],cameras_[family_],population_.catalog,population_.allModels,source,raster_);
        plan_=source_shadow_gpu::preparePlan(gpu_.profile,frame_,population_.catalog);
        NativeSourceDepthState logical{};
        executeNativeShadowCameraGpu(context,logical,gpu_,frame_,plan_,population_.catalog,population_.allModels,family_);
        map_=gpu_.map;
    }
    void updateAcceptedFrame(ID3D11DeviceContext* context,NativeSourceDepthState& logical,
        bool populationChanged,const source_current_season::Frame* season,const source_camera::Frame& camera) {
        if(populationChanged)source_shadow_gpu::bindCatalog(gpu_.profile,population_.catalog);
        source_shadow_gpu::setCurrentParameters(gpu_.profile,parameters_[family_]);
        plan_=source_shadow_gpu::preparePlan(gpu_.profile,frame_,population_.catalog);
        executeNativeShadowCameraGpu(context,logical,gpu_,frame_,plan_,population_.catalog,population_.allModels,family_);
        map_=gpu_.map;
        if(receiver_)updateReceiver(*receiver_,camera);
        if(waterReceiver_)updateReceiver(*waterReceiver_,camera);
    }
    void prepareContact(ID3D11Device* device,const fs::path& source,
        const D3D11_RASTERIZER_DESC& raster,const D3D11_BLEND_DESC& blend,
        const NativeDepthMirror& depth,ID3D11Texture2D* normals,DXGI_FORMAT normalFormat,
        ID3D11Texture2D* ab,DXGI_FORMAT abTargetFormat,ID3D11DepthStencilView* mainDepth) {
        contact_.prepare(device,Interop::readOcclusionResources(source),raster,blend,
            depth,normals,normalFormat,ab,abTargetFormat,mainDepth);
    }
    void executeContact(ID3D11Device* device,ID3D11DeviceContext* context,
        NativeSourceDepthState& logical,const D3D11_VIEWPORT& viewport,
        const ContactOcclusionInput& input,const source_camera::CameraOptics& lens,
        unsigned activeGroups,unsigned hostClass,bool commonPresent) {
        contact_.execute(device,context,logical,viewport,input.settings,lens,activeGroups,
            hostClass,input.lights,commonPresent);
    }
    void prepareReceiverResources(ID3D11Device* device,const fs::path& source) {
        receiverResources_.prepare(device,source);
    }
    void prepareGeometryReceiver(const source_camera::Frame& camera) {
        receiver_.emplace(receiverResources_,68);updateReceiver(*receiver_,camera);
    }
    void prepareWaterReceiver(const source_camera::Frame& camera) {
        waterReceiver_.emplace(receiverResources_,119);updateReceiver(*waterReceiver_,camera);
    }
    void executeGeometryReceiver(ID3D11Device* device,ID3D11DeviceContext* context,
        NativeSourceDepthState& logical,ID3D11ShaderResourceView* depth,std::uint32_t depthObject,
        const source_current_season::Frame& current,const PreparedMapView& view,const SkyPatternPalette& palette,float commonClock) {
        executeSourceShadowReceiver(device,context,logical,receiver_.value(),receiverResources_,map_.view.Get(),
            depth,depthObject,current,view,palette,commonClock);
    }
    void executeWaterReceiver(ID3D11Device* device,ID3D11DeviceContext* context,
        NativeSourceDepthState& logical,ID3D11ShaderResourceView* depth,std::uint32_t depthObject,
        const source_current_season::Frame& current,const PreparedMapView& view,const SkyPatternPalette& palette,float commonClock) {
        executeSourceShadowReceiver(device,context,logical,waterReceiver_.value(),receiverResources_,map_.view.Get(),
            depth,depthObject,current,view,palette,commonClock);
    }
    const source_shadow_camera::Frame& frame() const {return frame_;}
    const source_shadow_camera::Profile& compactCpuProvider() const {
        if (!populationPrepared_) throw std::runtime_error("HDR shadow CPU provider has not been prepared");
        return cameras_[family_];
    }
    const source_current_season_shadow::Parameters& seasonParameters() const {return season_;}
    const ShadowReceiverShadowValues& receiverShadowValues() const {return receiver_.value().material.shadow.value();}
    ID3D11ShaderResourceView* shadowView() const {return gpu_.map.view.Get();}
    ID3D11ShaderResourceView* directionalView() const {return receiverResources_.fallback().view.Get();}
    ID3D11SamplerState* fallbackSampler() const {return receiverResources_.fallback().sampler.Get();}
    const NativeShadowReceiverResources& patternResources() const {return receiverResources_;}

private:
    void updateReceiver(NativeSourceShadowReceiver& receiver,const source_camera::Frame& camera) {
        receiver.material=source_shadow_gpu::receiverMaterial(gpu_.profile,cameras_[family_],camera,frame_,receiver.material);
    }
    NativeJointPopulationShadow population_;
    std::optional<NativeCityGateShadowSource> cityGate_;
    std::array<source_shadow_camera::Profile,2> cameras_;
    std::array<source_shadow_gpu::Profile,2> parameters_;
    std::array<NativeShadowRasterSource,2> raster_;
    unsigned family_=0;
    bool populationPrepared_{};
    source_current_season_shadow::Parameters season_;
    source_shadow_camera::Frame frame_;
    source_shadow_camera::Owner history_;
    NativeShadowMap map_;
    NativeShadowCameraGpu gpu_;
    source_shadow_gpu::Plan plan_;
    ContactOcclusionRenderer contact_;
    NativeShadowReceiverResources receiverResources_;
    std::optional<NativeSourceShadowReceiver> receiver_,waterReceiver_;
};
} // namespace San14::Render
