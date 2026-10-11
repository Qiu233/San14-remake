#pragma once
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

    void prepareAmbient(ID3D11Device* device,ID3D11DeviceContext* context,const fs::path& source) {
        environment_=prepareNativeAmbient(device,source);
        // Bootstrap supplies all three history slots once, before Main entry.
        for(UINT slot=0;slot<3;++slot)executeNativeAmbientBatch(context,environment_,slot);
        executeNativeAmbientBlend(context,environment_,0,0);environment_.selected=0;
        ambient_=environment_.selectedView();
    }

    void prepareClouds(ID3D11Device* device,const fs::path& cloudSource,
        const fs::path& animationSource,const fs::path& ambientCloudSource,
        const fs::path& starSource,ID3D11Texture2D* hdr,StarTwinklePhase& twinkle) {
        cloud_=prepareNativeMainCloud(device,cloudSource,animationSource,hdr);
        environmentCloud_=prepareNativeAmbientCloudContinuous(device,ambientCloudSource,environment_,cloud_);
        environment_.starAnimation=prepareNativeAmbientStarContinuous(starSource,twinkle);
        environment_.starAnimationEnabled=true;
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
            if(!weather_ || std::none_of(weather_->profiles.seasons.begin(),
                weather_->profiles.seasons.end(),[&](const auto& profile){return profile.season==frame.season->season;}))
                weather_=readNativeCurrentCloudWeather(source.weather);
            const auto weather=publishNativeCurrentCloudWeatherBeforeClock(*weather_,*frame.season,cloud_,environmentCloud_);
            currentPlanet=weather.planetRadius;
            if(!currentAmbient_)currentAmbient_=prepareNativeCurrentAmbientFrame(device,
                source.ambientFrame,environment_,environmentCloud_);
        }
        const float node3Delta=advanceNativeMainCloud(cloud_,frame.clock); // Once before geometry.
        if(frame.season) {
            prepareNativeCurrentAmbientInputs(*currentAmbient_,*frame.season,frame.lights);
            std::array<source_camera::V4,6> cloudClip{};
            for(unsigned face:{0u,1u,4u,5u})cloudClip[face]=source_work_cloud_clip::prepareCurrentWorkCloudClip(
                currentPlanet,currentAmbient_->current.faces[face].camera,2);
            publishNativeCurrentAmbientFrame(context,*currentAmbient_,environment_,environmentCloud_,&cloudClip);
        }
        updateNativeAmbientCloudContinuous(context,environmentCloud_,cloud_);
        return node3Delta;
    }

    // Node 5 consumes completed Cloud and Main Star updates from node 3.
    void generateAmbient(ID3D11DeviceContext* context,float clock,float node3Delta) {
        const auto parent=NativeMainTargetScope::retain(context);
        // Generation selection can change; retain the actual returned view.
        ambient_=executeNativeAmbientNode5(context,environment_,clock,true,node3Delta);
        parent.restore(context);
    }

    void execute(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
        ID3D11DepthStencilView* depth,float clock,UINT hostFlags) {
        executeNativeMainCloud(device,context,logical,cloud_,depth,clock,hostFlags);
    }

    ID3D11ShaderResourceView* ambientView() const { return ambient_.Get(); }

  private:
    NativeMainCloud cloud_;
    std::optional<NativeCurrentCloudWeather> weather_;
    NativeAmbient environment_;
    NativeAmbientCloudContinuous environmentCloud_;
    std::optional<NativeCurrentAmbientFrame> currentAmbient_;
    ComPtr<ID3D11ShaderResourceView> ambient_;
};
} // namespace San14::Render
