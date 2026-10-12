#pragma once
#include "SkyPatternTargets.h"
#include "SkyAtmosphereResources.h"
#include "CloudVolumePreparation.h"
#include "SkyPatternRenderer.h"
#include "SkyPatternPalette.h"
#include "../../Scene/View/PreparedViewBaseLight.h"
#include "../../Device/ContextEnvironmentResources.h"
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
        const auto* atmosphere=atmosphereResources();
        if(!atmosphere) {patternSelector_=4;return false;}
        const auto& weather=currentWeather_.value();
        if(!(weather.coverage>0)||!(weather.cirrusCoverage>0)||inputs.scale==0||
            inputs.cameraKind<1||inputs.cameraKind>4) {
            patternSelector_=4;return false;
        }
        const auto index=UINT(inputs.cameraKind-1);
        const auto& target=patternTargets_.value().prepare(device,index);
        const auto& light=baseLight.selection();
        if(!skyCloudDrawingEnabled(common)||!light) {patternSelector_=index;return false;}
        patternRenderer_.value().execute(device,context,target.surface.Get(),target.description.width,
            target.description.height,sharedVariant_,patternNoise_,[&] {
                const auto& direction=light->direction;
                const auto projection=source_sky_pattern::prepareProjection(
                    {camera.eye.X,camera.eye.Z,camera.optics.farPlane,camera.optics.viewSpan,
                        camera.optics.aspectRatio,inputs.height,{direction[0],direction[1],direction[2]}},
                    halfFovTangent);
                patternMatrix_=projection.worldToPattern;
                const auto cameras=source_sky_pattern::prepareCameras(camera,projection,2);
                auto material=prepareSkyPatternMaterial(cameras,clipPlane,volume_.value(),atmosphere->settings().planetRadius);
                return std::pair{std::move(material),currentPatternSelection(inputs,light->fogMode)};
            });
        patternSelector_=index;
        return true;
    }
    UINT patternSelector() const {return patternSelector_;}
    const source_camera::Matrix& patternMatrix() const {return patternMatrix_;}
    SkyPatternPalette receiverPatternPalette() const {
        const auto* atmosphere=atmosphereResources();
        return {atmosphere,patternSelector_<4?patternTargets_.value().prepared(patternSelector_):nullptr,patternMatrix_};
    }

    void prepareAtmosphereResources(ID3D11Device* device,const fs::path& source) {
        auto prepared=NativeSkyAtmosphereOwner::prepare(device,source,environment_.textures);
        if(const auto* selected=prepared.renderingAtmosphere()) {
            for(std::size_t index=0;index<selected->planeCount();++index) {
                const auto& image=selected->plane(index).texture;
                const bool shared=std::any_of(environment_.textures.begin(),environment_.textures.end(),
                    [&](const auto& existing){return existing.identity==image.identity;});
                if(!shared)environment_.textures.push_back(image);
            }
        }
        atmosphereOwner_.emplace(std::move(prepared));
    }
    const NativeSkyAtmosphereResources* atmosphereResources() const {return atmosphereOwner_.value().atmosphere();}
    const NativeSkyAtmosphereResources* renderingAtmosphere() const {return atmosphereOwner_.value().renderingAtmosphere();}

    void prepareAmbient(ID3D11Device* device,ID3D11DeviceContext* context,const fs::path& source,
        MainStarPass& stars,const source_weather_fog::Power& power) {
        auto prepared=prepareNativeAmbient(device,source,stars,power);
        prepareNativeAmbientImages(device,prepared);
        environment_=std::move(prepared);
    }

    void bootstrapAmbient(ID3D11DeviceContext* context,const CommonRenderPolicy& common) {
        // A disabled environment supplies no bootstrap pixels. A downstream
        // recipe requiring Ambient still needs its own actual fallback owner.
        if(!ambientGenerationEnabled(common)||!skyCloudDrawingEnabled(common))return;
        // Bootstrap supplies all three history slots once, before Main entry.
        for(UINT slot=0;slot<3;++slot)executeNativeAmbientBatch(context,environment_,renderingAtmosphere()!=nullptr,[&]{prepareWorkFaces(context);},slot);
        executeNativeAmbientBlend(context,environment_,0,0);
    }

    void prepareClouds(ID3D11Device* device,const fs::path& cloudSource,
        const fs::path& animationSource,const fs::path& ambientCloudSource,
        ID3D11Texture2D* hdr) {
        cloud_=prepareNativeMainCloud(device,cloudSource,animationSource,hdr,renderingAtmosphere());
        ambientCloudSource_=ambientCloudSource;workPublicationPending_=true;
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
        const CommonRenderPolicy& commonPolicy,const source_weather_fog::Power& power,bool weatherSamplingEnabled) {
        return AtmosphereFrame{season,lights,altitude,weather,globalRgb,camera,cloud_,commonPolicy,power,weatherSamplingEnabled,renderingAtmosphere()};
    }

    float updateAcceptedFrame(ID3D11Device* device,ID3D11DeviceContext* context,
        const CloudEnvironmentSources& source,const CloudEnvironmentFrame& frame) {
        if(frame.season) {
            currentSampleCount_=frame.season->ambientFilterSampleCount;
            if(!weather_ || std::none_of(weather_->profiles.seasons.begin(),
                weather_->profiles.seasons.end(),[&](const auto& profile){return profile.season==frame.season->season;}))
                weather_=readNativeCurrentCloudWeather(source.weather,cloud_);
            currentWeather_=publishNativeCurrentCloudWeatherBeforeClock(*weather_,*frame.season,cloud_,
                renderingAtmosphere()?&renderingAtmosphere()->settings():nullptr);
            // Retain current typed inputs, not a borrowed frame or an initial
            // six-face publication. First activation consumes this same update.
            acceptedEnvironment_=source_current_ambient::decodeEnvironment(*frame.season);
            acceptedCloudLight_=frame.lights;
            acceptedClamp_=frame.season->cloudWeather.clampValue;
            ambientFrameSource_=source.ambientFrame;weatherSource_=source.weather;
        }
        const float node3Delta=advanceNativeMainCloud(cloud_,frame.clock); // Once before geometry.
        if(currentWeather_) {
            CloudAnimationSnapshot phases{};
            for(const auto& axis:cloud_.axes)if(axis.weatherChannel)
                phases[std::size_t(*axis.weatherChannel)]=axis.phase;
            volume_=prepareCloudVolumeMaterial(*currentWeather_,phases);
        }
        workPublicationPending_=true;
        return node3Delta;
    }

    // Node 5 consumes completed Cloud and Main Star updates from node 3.
    void generateAmbient(ID3D11DeviceContext* context,float clock,const CommonRenderPolicy& common) {
        const auto parent=NativeMainTargetScope::retain(context);
        const bool enabled=ambientGenerationEnabled(common);
        if(enabled&&currentSampleCount_&&*currentSampleCount_!=environment_.samples)
            throw std::runtime_error("Ambient sample count changed; prepare a new resource generation");
        executeNativeAmbientNode5(context,environment_,clock,enabled,
            skyCloudDrawingEnabled(common)&&renderingAtmosphere()!=nullptr,[&]{prepareWorkFaces(context);});
        parent.restore(context);
    }

    void execute(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
        ID3D11DepthStencilView* depth,float clock,UINT hostFlags,const CommonRenderPolicy& common) {
        const bool positive=prepareNativeMainCloudHistory(context,cloud_,hostFlags);
        const bool drawEnabled=positive&&skyCloudDrawingEnabled(common)&&renderingAtmosphere();
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

    // The finite normal Main View owns this Sky CPU even when generation is
    // disabled. Its disabled query is a genuine absent wrapper override, not
    // an absent Sky CPU or a failed resource preparation.
    std::optional<EnvironmentImage> environmentImage() const {
        if(environment_.generation.disabled())return std::nullopt;
        const auto selected=environment_.generation.selectedOutput();
        const auto view=environment_.outputs.at(selected).view.Get();
        const auto sampler=environment_.outputSamplers.at(selected).Get();
        if(!view||!sampler)throw std::runtime_error("HDR Sky environment has not been prepared");
        return EnvironmentImage{view,sampler};
    }
    const CloudVolumeMaterial& cloudVolume() const { return volume_.value(); }

  private:
    void prepareWorkFaces(ID3D11DeviceContext* context) {
        ensureNativeAmbientFaces(context,environment_);
        // Bootstrap uses the actual initial face media before Main Cloud exists.
        // Current activation uses node three's accepted weather and phase; this
        // function never advances a clock or modifies Main Cloud history.
        if(ambientCloudSource_.empty())return;
        if(environmentCloud_&&!workPublicationPending_)return;
        ComPtr<ID3D11Device> device;context->GetDevice(&device);
        if(!environmentCloud_)
            environmentCloud_=prepareNativeAmbientCloudContinuous(device.Get(),ambientCloudSource_,environment_,cloud_);
        if(acceptedEnvironment_) {
            if(!currentAmbient_)
                currentAmbient_=prepareNativeCurrentAmbientFrame(device.Get(),ambientFrameSource_,environment_,*environmentCloud_);
            if(!workWeatherLayout_)
                workWeatherLayout_=source_current_cloud_weather::readLayout(weatherSource_/"work-layout.txt",true);
            publishNativeCurrentCloudWeatherToWork(currentWeather_.value(),*workWeatherLayout_,acceptedClamp_,cloud_,*environmentCloud_);
            currentAmbient_->current=source_current_ambient::prepareCurrent(
                currentAmbient_->profile,*acceptedEnvironment_,acceptedCloudLight_);
            std::array<source_camera::V4,6> cloudClip{};
            for(unsigned face:{0u,1u,4u,5u})cloudClip[face]=source_work_cloud_clip::prepareCurrentWorkCloudClip(
                renderingAtmosphere()->settings().planetRadius,currentAmbient_->current.faces[face].camera,2);
            publishNativeCurrentAmbientFrame(context,*currentAmbient_,environment_,*environmentCloud_,&cloudClip);
        }
        updateNativeAmbientCloudContinuous(context,*environmentCloud_,cloud_,volume_?&*volume_:nullptr);
        workPublicationPending_=false;
    }
    bool ambientGenerationEnabled(const CommonRenderPolicy& common) const {
        return (!common.present||common.allowAmbientGeneration)&&atmosphereResources()!=nullptr;
    }
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
    std::optional<NativeSkyAtmosphereOwner> atmosphereOwner_;
    NativeMainCloud cloud_;
    std::optional<NativeCurrentCloudWeather> weather_;
    std::optional<source_current_cloud_weather::Frame> currentWeather_;
    std::optional<CloudVolumeMaterial> volume_;
    NativeAmbient environment_;
    std::optional<NativeAmbientCloudContinuous> environmentCloud_;
    fs::path ambientCloudSource_,ambientFrameSource_,weatherSource_;
    std::optional<source_current_ambient::Environment> acceptedEnvironment_;
    source_current_season_atmosphere::CloudLightInput acceptedCloudLight_{};
    std::optional<source_current_cloud_weather::Layout> workWeatherLayout_;
    std::optional<UINT> currentSampleCount_;
    float acceptedClamp_{};
    bool workPublicationPending_{};
    std::optional<NativeCurrentAmbientFrame> currentAmbient_;
};
} // namespace San14::Render
