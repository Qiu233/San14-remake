#pragma once
// Include after the HDR, deferred-camera, water-camera and weather writers.
// Each lighting entry owns its imported bank and retained object caches.
namespace San14::Render {

struct DeferredLightingFrame {
    const NativeDeferredCameraProfile& cameraProfile;
    const NativeDeferredCameraFields& camera;
    const source_current_season::Frame* season;
    const NativeWeatherFogConsumers& weatherConsumers;
    const source_weather_fog::Frame& weather;
    const source_water_camera::RefractionDerived* refraction=nullptr;
};

class DeferredLightingPass {
  public:
    DeferredLightingPass()=default;
    DeferredLightingPass(const DeferredLightingPass&)=delete;
    DeferredLightingPass& operator=(const DeferredLightingPass&)=delete;

    // Called once at this entry's original preparation node. Resources and
    // shader identities remain with the existing importer; no frame I/O here.
    void prepare(ID3D11Device* device,const fs::path& source,const char* passList,
        ID3D11Texture2D* hdr,const std::array<ComPtr<ID3D11Texture2D>,5>& gbuffer,
        const std::array<DXGI_FORMAT,6>& formats,ID3D11Texture2D* ggx,
        const fs::path& accessorySource={}) {
        passes_=prepareSourceHdrPasses(device,source,passList,hdr,gbuffer,formats,ggx,true);
        if(!accessorySource.empty())appendAccessories(device,accessorySource,passList,hdr,gbuffer,formats,ggx);
    }

    // At the first water receiver only, camera writes precede layout admission,
    // then refraction writes precede the current season/weather writer.
    void initializeRefractionFrame(ID3D11DeviceContext* context,
        const DeferredLightingFrame& frame,const fs::path& layoutFile,
        const source_water_camera::RefractionDerived& refraction) {
        updateNativeDeferredCameraGpu(context,frame.cameraProfile,frame.camera,passes_);
        refraction_=prepareNativeWaterRefractionGpu(layoutFile,passes_);
        updateNativeWaterRefractionGpu(context,refraction_,refraction,passes_);
        writeAtmosphere(context,frame);
    }

    // Every accepted request borrows complete current inputs. No resources or
    // route files are loaded. Existing caches survive across accepted frames.
    void update(ID3D11DeviceContext* context,const DeferredLightingFrame& frame) {
        updateNativeDeferredCameraGpu(context,frame.cameraProfile,frame.camera,passes_);
        if(frame.refraction)updateNativeWaterRefractionGpu(context,refraction_,*frame.refraction,passes_);
        writeAtmosphere(context,frame);
    }

    // The target scopes remain with the frame dispatcher. This operation owns
    // only selected HDR draws and their inherited depth/raster object caches.
    void execute(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
        ID3D11ShaderResourceView* sampledD1,ID3D11DepthStencilView* dsv,
        ID3D11ShaderResourceView* ambient,ID3D11ShaderResourceView* refraction,
        ID3D11ShaderResourceView* transmitted,ID3D11ShaderResourceView* specular,
        ID3D11ShaderResourceView* blurredDepth,float currentCommonClock,
        uint32_t view19AC,uint32_t view19B0) {
        executeSourceHdrPasses(device,context,logical,passes_,sampledD1,dsv,ambient,refraction,
            transmitted,specular,blurredDepth,currentCommonClock,view19AC,view19B0);
    }

    // Forward reads the current late globals and target without mutating the
    // retained lighting bank or keeping this reference after the draw.
    const NativeSourceHdrPasses& bank() const {return passes_;}

  private:
    void writeAtmosphere(ID3D11DeviceContext* context,const DeferredLightingFrame& frame) {
        if(frame.season)updateNativeCurrentSeasonHdr(context,*frame.season,passes_);
        else updateNativeWeatherFogHdr(context,frame.weatherConsumers,frame.weather,passes_);
    }

    void appendAccessories(ID3D11Device* device,const fs::path& accessorySource,const char* list,
        ID3D11Texture2D* hdr,const std::array<ComPtr<ID3D11Texture2D>,5>& gbuffer,
        const std::array<DXGI_FORMAT,6>& formats,ID3D11Texture2D* ggx) {
        auto source=prepareSourceHdrPasses(device,accessorySource,list,hdr,gbuffer,formats,ggx,true);
        for(auto& pass:source.passes)for(auto& view:pass.fixedPixelViews) {
            for(unsigned i=0;i<source.gbuffer.size();++i)
                if(view==source.gbuffer[i].Get())view=passes_.gbuffer[i].Get();
            if(view==source.ggx.Get())view=passes_.ggx.Get();
        }
        passes_.passes.insert(passes_.passes.begin(),std::make_move_iterator(source.passes.begin()),std::make_move_iterator(source.passes.end()));
    }

    NativeSourceHdrPasses passes_;
    NativeWaterRefractionGpu refraction_;
};
} // namespace San14::Render
