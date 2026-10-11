#pragma once
// Include after the Ocean geometry, FFT, water camera and refraction writers.
#include "Interop/OceanGridInput.h"
#include "Interop/OceanWaveInput.h"
#include "Interop/OceanSurfaceInput.h"
#include "Interop/OceanOpticsInput.h"
#include "Interop/OceanTextureMaterialInput.h"
#include <optional>
namespace San14::Render {

// Inputs are borrowed only during preparation or the accepted-camera call.
struct OceanSources {
    fs::path textures,water,depthCopy,grid;
};
struct OceanTargets {
    const std::array<ComPtr<ID3D11Texture2D>,5>& textures;
    const std::array<ID3D11RenderTargetView*,5>& colors;
    DXGI_FORMAT format;
    ID3D11DepthStencilView* depth;
    UINT width,height;
};

class OceanPass {
  public:
    OceanPass()=default;
    OceanPass(const OceanPass&)=delete;
    OceanPass& operator=(const OceanPass&)=delete;

    void prepareSceneCopy(ID3D11Device* device,ID3D11DeviceContext* context,
        NativeSourceDepthState& logical,const fs::path& source,ID3D11Texture2D* hdr,DXGI_FORMAT format) {
        sceneCopy_=prepareNativeRefractionScene97(device,source,hdr,format);
        colors_.scene97=executeNativeRefractionScene97(*sceneCopy_,context,logical);
    }
    void prepareDepthCopy(ID3D11Device* device,ID3D11DeviceContext* context,
        NativeSourceDepthState& logical,const fs::path& source,ID3D11ShaderResourceView* depth,UINT width,UINT height) {
        depthCopy_=prepareNativeRefractionDepth(device,source,depth,width,height);
        depth_=executeNativeRefractionDepth(*depthCopy_,context,logical);
    }
    void copyScene(ID3D11DeviceContext* context,NativeSourceDepthState& logical) {
        executeNativeRefractionScene97(sceneCopy_.value(),context,logical);
    }
    void copyDepth(ID3D11DeviceContext* context,NativeSourceDepthState& logical) {
        executeNativeRefractionDepth(depthCopy_.value(),context,logical);
    }

    void prepareWater(ID3D11Device* device,ID3D11DeviceContext* context,
        const OceanSources& source,const OceanTargets& targets,const source_camera::CameraTransform& camera,float clock,
        RenderRandomContext& randomContext,uint32_t startupSeed,const OceanWaveSettings* currentWave,
        const OceanSurfaceSettings* currentSurface,const OceanOpticsSettings* currentOptics,
        const OceanTextureMaterialSettings* currentTextureMaterial) {
        const auto surface=currentSurface?*currentSurface:Interop::readOceanSurfaceSettings(source.grid/"ocean-surface.bin");
        grid_=constructOceanGrid(Interop::readOceanGridSettings(source.grid/"ocean-grid.bin"),surface);
        const auto wave=currentWave?*currentWave:Interop::readOceanWaveSettings(source.grid/"ocean-wave.bin");
        const auto optics=currentOptics?*currentOptics:Interop::readOceanOpticsSettings(source.grid/"ocean-optics.bin");
        const auto material=currentTextureMaterial?*currentTextureMaterial:
            Interop::readOceanTextureMaterialSettings(source.grid/"ocean-texture-material.bin");
        waves_=prepareNativeOceanFftSchedule(grid_.settings,wave,randomContext,startupSeed);
        const auto initialSide=waves_.fft.activeSide;
        textures_=loadOceanTextures(device,source.textures);
        water_=prepareNativeOcean116(device,source.water,textures_,grid_,waves_.fft.vertices[initialSide].data(),
            depth_.view.Get(),source.depthCopy/"target-sampler-desc.bin",targets.textures,targets.colors,targets.format,targets.depth,targets.width,targets.height,
            surface,optics,sceneCopy_.has_value()&&depthCopy_.has_value());
        applyNativeOceanTextureMaterialSettings(water_,material);
        camera_=prepareNativeWaterCameraGpu(device,grid_,water_);
        // The first displayed frame follows the same publication cadence as
        // later frames: phase0 is published once, then the first Common delta
        // advances the clock and queues the elapsed job on the other side.
        publishCompletedGeometry(context,clock,&wave,&surface,&optics,&material);
        updateNativeWaterCameraGpu(camera_,context,water_,camera,completedWaveBounds_);
    }

    // Completion is published before the clock advances and queues its next
    // wave job. This cadence precedes accepted Main camera preparation.
    void publishCompletedGeometry(ID3D11DeviceContext* context,float clock,const OceanWaveSettings* currentWave,
        const OceanSurfaceSettings* currentSurface,const OceanOpticsSettings* currentOptics,
        const OceanTextureMaterialSettings* currentTextureMaterial) {
        const auto& published=publishNativeOceanFftCompleted(waves_);
        updateNativeOceanGeometry(context,water_,published.data(),UINT(published.size()*sizeof(NativeOceanFftVertex)));
        completedWaveBounds_=waves_.publishedBounds;
        if(currentWave)applyNativeOceanWaveSettings(waves_.fft,*currentWave);
        if(currentSurface) {
            applyOceanSurfaceSettings(grid_.visibility,*currentSurface);
            applyOceanSurfaceSettings(camera_.profile,*currentSurface);
            applyNativeOceanSurfaceSettings(water_,*currentSurface);
        }
        if(currentOptics)applyNativeOceanOpticsSettings(water_,*currentOptics);
        if(currentTextureMaterial)applyNativeOceanTextureMaterialSettings(water_,*currentTextureMaterial);
        water_.clock.advance(clock);
        writeNativeOceanAnimation(water_);
        queueNativeOceanFft(waves_,water_.clock.accumulatedTime);
    }
    void updateCamera(ID3D11DeviceContext* context,const source_camera::CameraTransform& camera) {
        updateNativeWaterCameraGpu(camera_,context,water_,camera,completedWaveBounds_);
    }
    void executeWater(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical) {
        executeNativeOcean116(device,context,logical,water_);
    }

    // Filtering remains after sky/cloud/fog, separate from the initial copies.
    void prepareFilters(ID3D11Device* device,const fs::path& source,
        ID3D11Texture2D* hdr,DXGI_FORMAT format,ID3D11ShaderResourceView* depth) {
        filters_=prepareNativeRefractionFilters137(device,source,hdr,format,depth);
    }
    void executeFilters(ID3D11DeviceContext* context,NativeSourceDepthState& logical) {
        executeNativeRefractionFilters137(filters_,context,logical,colors_);
    }

    ID3D11ShaderResourceView* refractionDepth() const {return depth_.view.Get();}
    ID3D11DepthStencilView* depthTarget() const {return depth_.depth.Get();}
    ID3D11ShaderResourceView* transmittedScene() const {return colors_.scene97.view.Get();}
    ID3D11ShaderResourceView* filteredSpecular() const {return colors_.specular.view.Get();}
    ID3D11ShaderResourceView* blurredDepth() const {return colors_.blurredDepth.view.Get();}
    const source_water_camera::RefractionDerived& refractionCamera() const {return camera_.refraction;}

  private:
    // Engaged only by successful real preparation. Material binding depends on
    // both owners, independently of whether either owner's child view is null.
    std::optional<NativePreparedRefractionScene97> sceneCopy_;
    std::optional<NativePreparedRefractionDepth> depthCopy_;
    NativePreparedRefractionFilters137 filters_;
    NativeRefractionDepth depth_;
    NativeRefractionColors colors_;
    OceanNativeTextures textures_;
    OceanGrid grid_;
    NativeOcean116 water_;
    NativeWaterCameraGpu camera_;
    std::array<float,8> completedWaveBounds_{};
    NativeOceanFftSchedule waves_;
};
} // namespace San14::Render
