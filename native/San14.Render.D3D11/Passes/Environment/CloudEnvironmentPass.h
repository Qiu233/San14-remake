#pragma once
#include "SkyPatternTargets.h"
#include "SkyAtmosphereResources.h"
#include "CloudVolumePreparation.h"
#include "SkyPatternRenderer.h"
#include "SkyPatternPalette.h"
#include "../../Scene/View/PreparedViewBaseLight.h"
// Include after the cloud, ambient, weather and atmosphere publication writers.
namespace San14::Render {

struct CloudEnvironmentSources {
    const fs::path& weather;
    const fs::path& ambientFrame;
};

// Season and lighting are borrowed for this accepted request. The pass retains
// cloud animation, weather profiles and the ambient generation history itself.
struct CloudEnvironmentFrame {
    const source_current_season::Frame* season;
    const source_current_season_atmosphere::CloudLightInput& lights;
    float clock;
};

class CloudEnvironmentPass {
  public:
    CloudEnvironmentPass()=default;
    CloudEnvironmentPass(const CloudEnvironmentPass&)=delete;
    CloudEnvironmentPass& operator=(const CloudEnvironmentPass&)=delete;

    // These targets share the retained Sky environment lifetime. Importing
    // declarations does not select a palette or produce target contents.
    void preparePatternTargets(const fs::path& source) {
        patternTargets_.emplace(Interop::readSkyPatternTargetInputs(source));
    }
    NativeSkyPatternTargets& patternTargets() {return patternTargets_.value();}

    void preparePatternPrograms(const fs::path& source) {
        NativeSkyPatternNoise noise;
        // CloudInputs is a complete imported group: its source bind points
        // name Base/Detail/Distort/Cirrus independently of any selected RDEF.
        for(UINT role=0;role<4;++role) {
            const auto found=std::find_if(environment_.textures.begin(),environment_.textures.end(),
                [&](const auto& texture){return texture.program=="clouds"&&texture.slot==role;});
            if(found==environment_.textures.end()||!found->resource||!found->view||!found->sampler)
                throw std::runtime_error("Pattern requires the complete prepared Cloud noise and cirrus groups");
            auto& image=role<3?noise.layers[role]:noise.cirrus;
            image={found->view,found->sampler};
        }
        NativeSkyPatternRenderer prepared(source);
        sharedVariant_=prepared.bank().initialVariant();
        patternRenderer_.emplace(std::move(prepared));
        patternNoise_=std::move(noise);
    }

    // Same current View, ordinary camera and single node-three weather update.
    // Fatal device errors retain earlier wrapper/matrix stores but do not
    // masquerade as a normal callback return that writes a new selector.
    bool executePattern(ID3D11Device* device,ID3D11DeviceContext* context,
        const SkyPatternCurrentInputs& inputs,const source_camera::CameraTransform& camera,
        float halfFovTangent,const PreparedViewBaseLight& baseLight,
        const CommonRenderPolicy& common,source_camera::V4 clipPlane) {
        if(!common.present)return false;
        const auto& weather=currentWeather_.value();
        if(!(weather.coverage>0)||!(weather.cirrusCoverage>0)||inputs.scale==0||
            inputs.cameraKind<1||inputs.cameraKind>4) {
            patternSelector_=4;return false;
        }
        const auto index=UINT(inputs.cameraKind-1);
        const auto& target=patternTargets_.value().prepare(device,index);
        const auto& light=baseLight.selection();
        if(!skyCloudDrawingEnabled(common)||!light) {patternSelector_=index;return false;}
        const auto& atmosphere=atmosphereResources_.value();
        patternRenderer_.value().execute(device,context,target.surface.Get(),target.description.width,
            target.description.height,sharedVariant_,patternNoise_,[&] {
                const auto& direction=light->direction;
                const auto projection=source_sky_pattern::prepareProjection(
                    {camera.eye.X,camera.eye.Z,camera.optics.farPlane,camera.optics.viewSpan,
                        camera.optics.aspectRatio,inputs.height,{direction[0],direction[1],direction[2]}},
                    halfFovTangent);
                patternMatrix_=projection.worldToPattern;
                const auto cameras=source_sky_pattern::prepareCameras(camera,projection,2);
                auto material=prepareSkyPatternMaterial(cameras,clipPlane,volume_.value(),atmosphere.settings().planetRadius);
                return std::pair{std::move(material),currentPatternSelection(inputs,light->fogMode)};
            });
        patternSelector_=index;
        return true;
    }
    UINT patternSelector() const {return patternSelector_;}
    const source_camera::Matrix& patternMatrix() const {return patternMatrix_;}
    SkyPatternPalette receiverPatternPalette() const {
        const auto& targets=patternTargets_.value();
        return {atmosphereResources_.value(),patternSelector_<4?targets.prepared(patternSelector_):nullptr,patternMatrix_};
    }

    void prepareAtmosphereResources(ID3D11Device* device,const fs::path& source) {
        auto prepared=NativeSkyAtmosphereResources::prepare(device,source,environment_.textures);
        atmosphereResources_.emplace(std::move(prepared));
    }
    const NativeSkyAtmosphereResources& atmosphereResources() const {return atmosphereResources_.value();}

    void prepareAmbient(ID3D11Device* device,ID3D11DeviceContext* context,const fs::path& source,
        MainStarPass& stars,const source_weather_fog::Power& power) {
        environment_=prepareNativeAmbient(device,source,stars,power);
        // Bootstrap supplies all three history slots once, before Main entry.
        for(UINT slot=0;slot<3;++slot)executeNativeAmbientBatch(context,environment_,slot);
        executeNativeAmbientBlend(context,environment_,0,0);
        ambient_=environment_.selectedView();
    }

    void prepareClouds(ID3D11Device* device,const fs::path& cloudSource,
        const fs::path& animationSource,const fs::path& ambientCloudSource,
        ID3D11Texture2D* hdr) {
        cloud_=prepareNativeMainCloud(device,cloudSource,animationSource,hdr);
        environmentCloud_=prepareNativeAmbientCloudContinuous(device,ambientCloudSource,environment_,cloud_);
        // This backend constructs a full-target View and has no partial-View
        // API. This rectangle belongs to that View, not a temporary RS viewport.
        D3D11_TEXTURE2D_DESC target{};hdr->GetDesc(&target);
        if(!target.Width||!target.Height)throw std::runtime_error("Main Cloud full-target View has no extent");
        viewRectangle_=FullTargetViewRectangle::construct(target.Width,target.Height);
    }

    // Atmosphere's camera and season writers require this call-scoped publication
    // target. Return the complete borrowed frame rather than a mutable cloud owner.
    AtmosphereFrame atmosphereFrame(const source_current_season::Frame* season,
        const source_current_season_atmosphere::CloudLightInput& lights,
        float altitude,const source_weather_fog::Frame& weather,
        const source_camera::V3& globalRgb,const source_camera::CameraTransform& camera,
        const CommonRenderPolicy& commonPolicy,const source_weather_fog::Power& power) {
        return AtmosphereFrame{season,lights,altitude,weather,globalRgb,camera,cloud_,commonPolicy,power};
    }

    float updateAcceptedFrame(ID3D11Device* device,ID3D11DeviceContext* context,
        const CloudEnvironmentSources& source,const CloudEnvironmentFrame& frame) {
        float currentPlanet=0;
        if(frame.season) {
            if(frame.season->ambientFilterSampleCount!=environment_.samples)
                throw std::runtime_error("Ambient sample count changed; prepare a new resource generation");
            if(!weather_ || std::none_of(weather_->profiles.seasons.begin(),
                weather_->profiles.seasons.end(),[&](const auto& profile){return profile.season==frame.season->season;}))
                weather_=readNativeCurrentCloudWeather(source.weather);
            currentWeather_=publishNativeCurrentCloudWeatherBeforeClock(*weather_,*frame.season,cloud_,environmentCloud_);
            currentPlanet=currentWeather_->planetRadius;
            if(!currentAmbient_)currentAmbient_=prepareNativeCurrentAmbientFrame(device,
                source.ambientFrame,environment_,environmentCloud_);
        }
        const float node3Delta=advanceNativeMainCloud(cloud_,frame.clock); // Once before geometry.
        if(currentWeather_) {
            CloudAnimationSnapshot phases{};
            for(const auto& axis:cloud_.axes)if(axis.weatherChannel)
                phases[std::size_t(*axis.weatherChannel)]=axis.phase;
            volume_=prepareCloudVolumeMaterial(*currentWeather_,phases);
        }
        if(frame.season) {
            prepareNativeCurrentAmbientInputs(*currentAmbient_,*frame.season,frame.lights);
            std::array<source_camera::V4,6> cloudClip{};
            for(unsigned face:{0u,1u,4u,5u})cloudClip[face]=source_work_cloud_clip::prepareCurrentWorkCloudClip(
                currentPlanet,currentAmbient_->current.faces[face].camera,2);
            publishNativeCurrentAmbientFrame(context,*currentAmbient_,environment_,environmentCloud_,&cloudClip);
        }
        updateNativeAmbientCloudContinuous(context,environmentCloud_,cloud_,volume_?&*volume_:nullptr);
        return node3Delta;
    }

    // Node 5 consumes completed Cloud and Main Star updates from node 3.
    void generateAmbient(ID3D11DeviceContext* context,float clock) {
        const auto parent=NativeMainTargetScope::retain(context);
        // Generation selection can change; retain the actual returned view.
        ambient_=executeNativeAmbientNode5(context,environment_,clock,true);
        parent.restore(context);
    }

    void execute(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
        ID3D11DepthStencilView* depth,float clock,UINT hostFlags,const CommonRenderPolicy& common) {
        const bool positive=prepareNativeMainCloudHistory(context,cloud_,hostFlags);
        const bool drawEnabled=positive&&skyCloudDrawingEnabled(common);
        if(drawEnabled) {
            // Main's reached tail setters affect Pattern's next request. Its
            // first-five selection also needs a scene reference and remains owned by
            // the existing Main media path; never substitute Pattern's inputs.
            bool fourHistory=true;
            for(const char* name:{"current-rgb","current-alpha","previous-rgb","previous-alpha"}) {
                const auto found=cloud_.surfaces.find(name);
                fourHistory=fourHistory&&found!=cloud_.surfaces.end()&&bool(found->second.target);
            }
            if(!fourHistory)throw std::runtime_error("Main Cloud two-target route requires its actual depth-assistance producer");
            sharedVariant_[5]=fourHistory;
            sharedVariant_[8]=!viewRectangle_.value().unit();
        }
        executeNativeMainCloud(device,context,logical,cloud_,depth,clock,drawEnabled);
    }

    ID3D11ShaderResourceView* ambientView() const { return ambient_.Get(); }
    const CloudVolumeMaterial& cloudVolume() const { return volume_.value(); }

  private:
    struct FullTargetViewRectangle {
        std::array<float,4> normalized;
        UINT targetWidth,targetHeight;
        static FullTargetViewRectangle construct(UINT width,UINT height) {return {{0,0,1,1},width,height};}
        bool unit() const {return normalized==std::array<float,4>{0,0,1,1};}
    };
    SkyPatternSelectionInputs currentPatternSelection(const SkyPatternCurrentInputs& inputs,UINT fogMode) const {
        if(!inputs.lowCostLevel||!inputs.rayExponent||!inputs.skyDomeLightGroupId)
            throw std::runtime_error("Current Cloud shader settings have not been published");
        return {*inputs.lowCostLevel,*inputs.rayExponent,environment_.skySampling.earth,
            *inputs.skyDomeLightGroupId!=0,bool(patternNoise_.cirrus.view),fogMode};
    }
    std::optional<NativeSkyPatternTargets> patternTargets_;
    std::optional<NativeSkyPatternRenderer> patternRenderer_;
    NativeSkyPatternNoise patternNoise_;
    // Pattern produces words0..4 on each operation; actual Main persists only
    // the shared tail values consumed here. WORK's temporary edits restore them.
    SkyPatternVariant sharedVariant_{};
    UINT patternSelector_{};
    source_camera::Matrix patternMatrix_{};
    std::optional<FullTargetViewRectangle> viewRectangle_;
    std::optional<NativeSkyAtmosphereResources> atmosphereResources_;
    NativeMainCloud cloud_;
    std::optional<NativeCurrentCloudWeather> weather_;
    std::optional<source_current_cloud_weather::Frame> currentWeather_;
    std::optional<CloudVolumeMaterial> volume_;
    NativeAmbient environment_;
    NativeAmbientCloudContinuous environmentCloud_;
    std::optional<NativeCurrentAmbientFrame> currentAmbient_;
    ComPtr<ID3D11ShaderResourceView> ambient_;
};
} // namespace San14::Render
