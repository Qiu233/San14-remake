#pragma once
#include "MainStarRenderer.h"
// Include after the sky, fog, atmosphere camera and season publication writers.
namespace San14::Render {

// These inputs are borrowed only for the call. Cloud history has its own
// cadence; this pass publishes camera fields without advancing that history.
struct AtmosphereFrame {
    const source_current_season::Frame* season;
    const source_current_season_atmosphere::CloudLightInput& cloudLights;
    float altitude;
    const source_weather_fog::Frame& weather;
    const source_camera::V3& globalRgb;
    const source_camera::CameraTransform& camera;
    NativeMainCloud& cloud;
    const CommonRenderPolicy& commonPolicy;
    const source_weather_fog::Power& power;
};

class AtmospherePass {
  public:
    AtmospherePass()=default;
    AtmospherePass(const AtmospherePass&)=delete;
    AtmospherePass& operator=(const AtmospherePass&)=delete;

    // WORK borrows the same Star resources and retained draw setters as Main.
    MainStarPass& stars() { return stars_; }

    // Main node 3 needs the current raised camera and particle owner before
    // node 124 creates Sky/Fog device passes. This preparation advances no time.
    void prepareMainStars(ID3D11Device* device,const fs::path& skySource,const fs::path& cameraSource,
        StarTwinklePhase& twinkle) {
        stars_.prepare(device,skySource/"stars",twinkle);
        camera_=prepareNativeAtmosphereCamera(cameraSource,skySource);
    }

    void updateMainStars(ID3D11Device* device,const AtmosphereFrame& frame,float node3Delta) {
        const auto camera=source_atmosphere_camera::prepareSkyCamera(camera_.packet,frame.camera);
        stars_.updateNode3(device,frame.season?&frame.season->mainStar:nullptr,node3Delta,camera,
            source_camera::M(camera_.packet.direct.altitude,1000.f),
            frame.season?frame.season->updateStarOrientation:camera_.packet.profile.updateStarOrientation,frame.power);
    }

    void prepare(ID3D11Device* device,ID3D11DeviceContext* context,
        const fs::path& skySource,const fs::path& fogSource,
        ID3D11Texture2D* hdr,ID3D11ShaderResourceView* depth,ID3D11ShaderResourceView* acceleration,
        ID3D11DepthStencilView* dsv,UINT width,UINT height,const AtmosphereFrame& frame) {
        sky_=prepareNativeMainSky(device,skySource,width,height);
        fog_=prepareNativeMapFog(device,fogSource,hdr,depth,acceleration,dsv,width,height);
        publishCurrent(frame);
        updateCamera(context,frame);
    }

    // Cold preparation precedes the first Main entry. Accepted publication
    // updates CPU values before node 3, independently of later Sky/Fog draws.
    void publishAcceptedCamera(const AtmosphereFrame& frame) {
        publishCurrent(frame);
    }

    void updateCamera(ID3D11DeviceContext* context,const AtmosphereFrame& frame) {
        updateNativeAtmosphereCamera(context,camera_,frame.camera,sky_,frame.cloud,skyCloudEnabled_);
    }

    bool executeSky(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
        const AtmosphereFrame& frame,const NativeMainTargetScope& targets,
        ID3D11ShaderResourceView* exposure,ID3D11SamplerState* exposureSampler) {
        if(!skyCloudEnabled_)return false;
        prepareNativeMainSkyReachedState(device,context,logical,sky_);
        const auto& camera=camera_.current.skyCamera;
        const auto& policy=camera_.current.skyPolicy;
        const UINT width=UINT(targets.viewports[0].Width),height=UINT(targets.viewports[0].Height);
        stars_.execute(device,context,0,camera,policy,frame.commonPolicy,width,height,sky_.width,targets.colorCount,
            exposure,exposureSampler,frame.power);
        executeNativeMainSky(device,context,sky_,policy,frame.commonPolicy,targets.colorCount);
        stars_.execute(device,context,1,camera,policy,frame.commonPolicy,width,height,sky_.width,targets.colorCount,
            exposure,exposureSampler,frame.power);
        return true;
    }

    // This operation remains after Cloud's draw and its target restoration.
    // Program replacement and semantic publication use this request's actual
    // view admission, camera, settings and light before the normal draw.
    void executeFog(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
        const source_current_season::Frame* season,
        float clock,std::optional<unsigned> viewFlags) {
        publishNativeCurrentHeightFog(device,season,fog_,camera_.current,clock,viewFlags);
        executeNativeMapFog(device,context,logical,fog_,clock,viewFlags);
    }

  private:
    void publishCurrent(const AtmosphereFrame& frame) {
        skyCloudEnabled_=skyCloudDrawingEnabled(frame.commonPolicy);
        if(!skyCloudEnabled_) {
            // Fog125 remains a separate consumer of the ordinary camera. Keep
            // its current geometric inputs without evaluating a skipped Sky.
            if(frame.season) {
                const auto environment=source_current_season_atmosphere::decodeEnvironment(*frame.season);
                camera_.packet.environment=environment.celestial;
                camera_.packet.direct=environment.direct;
                camera_.packet.skyPolicy=frame.season->skyPolicy;
            }
            return;
        }
        if(frame.season){publishNativeCurrentSeasonAtmosphere(camera_,frame.cloud,*frame.season,&frame.cloudLights);
            publishNativeCurrentMainSkyMoonFace(camera_,*frame.season);}
        else {
            publishNativeAtmosphereCurrentDirectEnvironment(camera_,frame.altitude);
            publishNativeAtmosphereWeather(camera_,frame.cloud,frame.weather,frame.globalRgb);
        }
    }

    NativeMainSky sky_;
    MainStarPass stars_;
    NativeMapFog fog_;
    NativeAtmosphereCamera camera_;
    bool skyCloudEnabled_=false;
};
} // namespace San14::Render
