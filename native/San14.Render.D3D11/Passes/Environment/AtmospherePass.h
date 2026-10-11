#pragma once
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
};

class AtmospherePass {
  public:
    AtmospherePass()=default;
    AtmospherePass(const AtmospherePass&)=delete;
    AtmospherePass& operator=(const AtmospherePass&)=delete;

    void prepare(ID3D11Device* device,ID3D11DeviceContext* context,
        const fs::path& skySource,const fs::path& fogSource,const fs::path& cameraSource,
        ID3D11Texture2D* hdr,ID3D11ShaderResourceView* depth,ID3D11ShaderResourceView* acceleration,
        ID3D11DepthStencilView* dsv,UINT width,UINT height,const AtmosphereFrame& frame) {
        sky_=prepareNativeMainSky(device,skySource,hdr,dsv,width,height);
        fog_=prepareNativeMapFog(device,fogSource,hdr,depth,acceleration,dsv,width,height);
        camera_=prepareNativeAtmosphereCamera(device,cameraSource,sky_);
        publishCurrent(frame);
        ready_=true;
        updateCamera(context,frame);
    }

    // Accepted camera preparation precedes all geometry. Before the first
    // atmosphere admission there is no GPU owner to publish into.
    void publishAcceptedCamera(const AtmosphereFrame& frame) {
        if(ready_)publishCurrent(frame);
    }

    void updateCamera(ID3D11DeviceContext* context,const AtmosphereFrame& frame) {
        updateNativeAtmosphereCamera(context,camera_,frame.camera,sky_,frame.cloud);
    }

    void executeSky(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical) {
        executeNativeMainSky(device,context,logical,sky_);
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
        if(frame.season){publishNativeCurrentSeasonAtmosphere(camera_,frame.cloud,*frame.season,&frame.cloudLights);
            publishNativeCurrentMainSkyMoonFace(camera_,*frame.season);}
        else {
            publishNativeAtmosphereCurrentDirectEnvironment(camera_,frame.altitude);
            publishNativeAtmosphereWeather(camera_,frame.cloud,frame.weather,frame.globalRgb);
        }
    }

    NativeMainSky sky_;
    NativeMapFog fog_;
    NativeAtmosphereCamera camera_;
    bool ready_=false;
};
} // namespace San14::Render
