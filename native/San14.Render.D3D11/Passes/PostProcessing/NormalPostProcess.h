#pragma once
// Include after normal-post GPU/continuous/camera, seasonal and map-exposure writers.
namespace San14::Render {

// Owns post-processing history, pass resources, staging sides and output. Main
// supplies its accepted frame and schedules Apply; no frame input is retained.
class NormalPostProcess {
public:
    NormalPostProcess()=default;
    NormalPostProcess(const NormalPostProcess&)=delete;
    NormalPostProcess& operator=(const NormalPostProcess&)=delete;

    void prepareExposure(ID3D11Device* device,ID3D11DeviceContext* context,
        const fs::path& source,const fs::path& initialSource) {
        mapExposure_=prepareNativeMapExposure(device,source);
        // Preserve the existing bootstrap stock; this is not a claim about
        // the original data-null allocation's initial contents.
        bootstrapExposure_=prepareNormalPostExposure(device,initialSource);
        const auto parent=NativeMainTargetScope::retain(context);
        ComPtr<ID3D11RasterizerState> priorRaster;context->RSGetState(&priorRaster);
        publishNativeMapExposureAfterApply(context,mapExposure_,bootstrapExposure_.views[0].Get());
        parent.restore(context);context->RSSetState(priorRaster.Get());
    }
    void updateMapExposure(ID3D11DeviceContext* context,
        const source_current_season::Frame* season,
        const uint8_t* mainNumeric,const source_camera::Prefix& directCamera) {
        const auto parent=NativeMainTargetScope::retain(context);
        ComPtr<ID3D11RasterizerState> priorRaster;context->RSGetState(&priorRaster);
        source_map_exposure::Selection exposureSelection;
        exposureSelection.applyReferencePresent=true; // This normal pipeline owns the Apply/Exposure module.
        source_map_exposure::SelectedLight exposureLight;
        if(season&&season->exposure.present){
            exposureLight={season->exposure.type,season->exposure.flags,season->exposure.rgb};
            exposureSelection.selectedLight=&exposureLight;
        }
        if(season)mapExposure_.profile.cameraSpecial1510=source_camera::get<uint32_t>(
            reinterpret_cast<const uint8_t*>(mainNumeric)+0x1510);
        const auto packet=executeNativeMapExposure(context,mapExposure_,directCamera,exposureSelection);

        parent.restore(context);context->RSSetState(priorRaster.Get());
    }
    void prepareOutput(ID3D11Device* device,const fs::path& source,UINT width,UINT height) {
        output_=createNormalPostFinalOutput(device,source,width,height);
    }
    void prepareSeason(const fs::path& source,const source_current_season::Frame* season) {
        if(season)callers_=source_current_season_post::readCallerSources(source/"post-caller"/"caller-source.bin");
    }
    void prepare(ID3D11Device* device,const fs::path& initialSource,
        const fs::path& continuousSource,const fs::path& cameraSource,
        ID3D11ShaderResourceView* scene,ID3D11ShaderResourceView* depth,UINT width,UINT height) {
        renderer_=prepareNormalPost(device,initialSource,scene,depth,output_,width,height,
            &bootstrapExposure_);
        continuous_=prepareNativeNormalPostContinuous(continuousSource);
        camera_=prepareNativeNormalPostCamera(cameraSource);
        passUnion_=loadNativeNormalPostPasses(device,continuousSource,width,height);
    }
    ComPtr<ID3D11Texture2D> execute(ID3D11Device* device,ID3D11DeviceContext* context,
        NativeSourceDepthState& logical,const fs::path& seasonalSource,
        const source_current_season::Frame* season,
        const source_camera::Prefix& camera,float currentClock) {
        if(season) {
            if(loadedLutSeason_!=int(season->season)) {
                const bool loaded=std::any_of(seasonLuts_.catalog.luts.begin(),seasonLuts_.catalog.luts.end(),
                    [&](const auto& row){return row.season==season->season;});
                if(!loaded)source_current_season_resources::loadResources(device,seasonLuts_,
                    source_current_season_resources::readCatalog(seasonalSource));
                loadedLutSeason_=int(season->season);
            }
            source_current_season_resources::installCurrent(seasonLuts_,*season,renderer_);
            const auto caller=source_current_season_post::prepareCaller(callers_,*season,
                camera_.selectedLightPresent!=0);
            source_current_season_post::publishMode6SelectedSun(*season,caller.selection,camera_);
            const auto values=source_current_season_post::prepareSimpleCurrent(*season,
                caller.adaptationLocked,caller.lockedAdaptationBits,caller.autoExposure);
            source_current_season_post::publishSimpleCurrent(values,continuous_);
            projection_=source_current_season_post::publishProjectionBeforeBegin(
                *season,camera_,continuous_);
        } else projection_=updateNativeNormalPostCameraBeforeBegin(camera_,camera,continuous_);
        auto plan=beginNativeNormalPostContinuousFromCommon(continuous_,currentClock);
        auto passes=selectNativeNormalPostContinuousPasses(plan,passUnion_);
        uploadNativeNormalPostContinuousConstants(context,continuous_,plan,passes);
        NativeNormalPostStagingMap mapped;bool readStaging=false;
        auto before57=[&]() {
            if(plan.needStagingRead) {
                mapped.side=plan.selectedStagingSide;D3D11_MAPPED_SUBRESOURCE bytes{};
                auto buffer=renderer_.spriteStaging.at(mapped.side).Get();
                const HRESULT result=context->Map(buffer,0,D3D11_MAP_READ,0,&bytes);
                mapped.returnedNull=FAILED(result);
                if(!mapped.returnedNull){std::memcpy(mapped.bytes.data(),bytes.pData,mapped.bytes.size());context->Unmap(buffer,0);}
                readStaging=true;
            }
            applyNativeNormalPostBefore57(continuous_,plan,readStaging?&mapped:nullptr,passes);
        };
        auto result=executeNormalPost(device,context,logical,renderer_,passes,before57);
        endNormalPost(renderer_);
        finishEndReturnedNativeNormalPostContinuous(continuous_,plan);
        {
            const auto parent=NativeMainTargetScope::retain(context);
            ComPtr<ID3D11RasterizerState> priorRaster;context->RSGetState(&priorRaster);
            publishNativeMapExposureAfterApply(context,mapExposure_,renderer_.exposure.views[0].Get());

            parent.restore(context);context->RSSetState(priorRaster.Get());
        }

        return result;

    }
    ID3D11ShaderResourceView* mapExposureView() const {return mapExposure_.view.Get();}
    ID3D11RenderTargetView* outputTarget() const {return output_.targets[0].Get();}
    ID3D11RenderTargetView* sceneTarget() const {return renderer_.surfaces.at("SceneColor").targets[0].Get();}

private:
    NativeMapExposure mapExposure_;
    NormalPostSurface bootstrapExposure_,output_;
    NativeNormalPost renderer_;
    std::vector<NativeNormalPostPass> passUnion_;
    NativeNormalPostContinuous continuous_;
    NativeNormalPostCamera camera_;
    NativeNormalPostCameraProjection projection_;
    source_current_season_resources::Bank seasonLuts_;
    int loadedLutSeason_=-1;
    source_current_season_post::CallerSources callers_;
};
} // namespace San14::Render
