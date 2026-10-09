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
    }
    void bindFieldDisplay(FieldDisplay& display,const source_far_main::Profile& source,
        const NativeModelContinuous& field) const {
        display.bind(source,field,cameras_);
    }
    void prepareRaster(const fs::path& source) {
        raster_[0]=loadNativeShadowRasterSource(source/"initial");
        raster_[1]=loadNativeShadowRasterSource(source/"mode1");
    }
    void prepareDirectionalDiffuse(ID3D11Device* device,const fs::path& source) {
        auto diffuse=loadTexture(device,source/"white-80F9.tex");
        D3D11_SHADER_RESOURCE_VIEW_DESC diffuseView{};
        diffuseView.Format=DXGI_FORMAT_B8G8R8A8_UNORM;
        diffuseView.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;
        diffuseView.Texture2D.MipLevels=1;
        check(device->CreateShaderResourceView(diffuse.Get(),&diffuseView,&directionalDiffuse_),"Normal source K1890 white80F9 owner");
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
    void prepareCamera(const source_camera::Frame& camera,const FieldDisplay& display) {
        display.publishShadow(cameras_[family_],family_);
        frame_=source_shadow_camera::prepareCurrent(cameras_[family_],camera,population_.catalog,history_);
    }
    void connectGeometry(const NativeModelInputs& field,const NativeModelInputs& primaries) {
        connectNativeJointPopulationShadowGeometry(population_,field,primaries);
    }
    void prepareGpu(ID3D11Device* device,ID3D11DeviceContext* context,
        const D3D11_DEPTH_STENCIL_DESC& entryDepth,UINT stencilReference,const fs::path& source) {
        gpu_=prepareNativeShadowCameraGpu(device,parameters_[family_],cameras_[family_],population_.catalog,population_.allModels,source,raster_);
        plan_=source_shadow_gpu::preparePlan(gpu_.profile,frame_,population_.catalog);
        NativeSourceDepthState logical{entryDepth,stencilReference};
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
        updateReceiver(context,receiver_,receiverCamera_,season,camera);
        updateReceiver(context,waterReceiver_,waterReceiverCamera_,season,camera);
    }
    void prepareContact(ID3D11Device* device,const fs::path& source,
        const NativeSourceDepthState& logical,const D3D11_RASTERIZER_DESC& raster,const D3D11_BLEND_DESC& blend,
        const NativeDepthMirror& depth,ID3D11Texture2D* normals,DXGI_FORMAT normalFormat,
        ID3D11Texture2D* ab,ID3D11DepthStencilView* mainDepth) {
        contact_=prepareNativeSubShadow63(device,source,logical,raster,blend,depth,normals,normalFormat,ab,mainDepth);
    }
    void executeContact(ID3D11DeviceContext* context,NativeSourceDepthState& logical,const D3D11_VIEWPORT& viewport) {
        executeNativeSubShadow63(context,logical,contact_,viewport);
    }
    void prepareGeometryReceiver(ID3D11Device* device,ID3D11DeviceContext* context,
        const fs::path& source,const fs::path& fallbackDiffuse,ID3D11Texture2D* indirect,
        DXGI_FORMAT format,UINT width,UINT height,const source_current_season::Frame* season,const source_camera::Frame& camera) {
        if(!directionalDiffuse_){auto diffuseTexture=loadTexture(device,fallbackDiffuse);
            check(device->CreateShaderResourceView(diffuseTexture.Get(),nullptr,&directionalDiffuse_),"held original directional diffuse");}
        receiver_=prepareSourceShadowReceiver(device,source,map_.view.Get(),directionalDiffuse_.Get(),indirect,format,width,height);
        {
            receiverCamera_=prepareNativeShadowCameraReceiver(device,receiver_,source);
            updateReceiver(context,receiver_,receiverCamera_,season,camera);
        }
    }
    void prepareWaterReceiver(ID3D11Device* device,ID3D11DeviceContext* context,
        const fs::path& source,ID3D11Texture2D* indirect,DXGI_FORMAT format,UINT width,UINT height,
        const source_current_season::Frame* season,const source_camera::Frame& camera) {
        waterReceiver_=prepareSourceShadowReceiver(device,source,map_.view.Get(),directionalDiffuse_.Get(),indirect,format,width,height);
        {
            waterReceiverCamera_=prepareNativeShadowCameraReceiver(device,waterReceiver_,source);
            updateReceiver(context,waterReceiver_,waterReceiverCamera_,season,camera);
        }
    }
    void executeGeometryReceiver(ID3D11Device* device,ID3D11DeviceContext* context,
        NativeSourceDepthState& logical,ID3D11ShaderResourceView* depth,ID3D11DepthStencilView* mainDepth) {
        executeSourceShadowReceiver(device,context,logical,receiver_,depth,mainDepth);
    }
    void executeWaterReceiver(ID3D11Device* device,ID3D11DeviceContext* context,
        NativeSourceDepthState& logical,ID3D11ShaderResourceView* depth,ID3D11DepthStencilView* mainDepth) {
        executeSourceShadowReceiver(device,context,logical,waterReceiver_,depth,mainDepth);
    }
    const source_shadow_camera::Frame& frame() const {return frame_;}
    const source_current_season_shadow::Parameters& seasonParameters() const {return season_;}
    const std::array<char,768>& geometryReceiverConstants() const {return receiverCamera_.current;}
    ID3D11ShaderResourceView* shadowView() const {return gpu_.map.view.Get();}
    ID3D11ShaderResourceView* directionalView() const {return directionalDiffuse_.Get();}

private:
    void updateReceiver(ID3D11DeviceContext* context,NativeSourceShadowReceiver& receiver,
        NativeShadowCameraReceiver& owned,const source_current_season::Frame* season,const source_camera::Frame& camera) {
        if(season)source_current_season_shadow::publishNormalPs5ReceiverBase(*season,season_,owned.current);
        updateNativeShadowCameraReceiver(context,receiver,owned,gpu_,cameras_[family_],camera,frame_);
    }
    NativeJointPopulationShadow population_;
    std::optional<NativeCityGateShadowSource> cityGate_;
    std::array<source_shadow_camera::Profile,2> cameras_;
    std::array<source_shadow_gpu::Profile,2> parameters_;
    std::array<NativeShadowRasterSource,2> raster_;
    unsigned family_=0;
    source_current_season_shadow::Parameters season_;
    source_shadow_camera::Frame frame_;
    source_shadow_camera::Owner history_;
    NativeShadowMap map_;
    NativeShadowCameraGpu gpu_;
    source_shadow_gpu::Plan plan_;
    NativeSubShadow contact_;
    NativeSourceShadowReceiver receiver_,waterReceiver_;
    NativeShadowCameraReceiver receiverCamera_,waterReceiverCamera_;
    ComPtr<ID3D11ShaderResourceView> directionalDiffuse_;
};
} // namespace San14::Render
