#pragma once
#include "TerritoryRenderer.h"
#include "TerritoryOwnership.h"
#include <optional>

// Held ownership layers, color allocation and overlay resources belong to the
// map overlay. Main supplies its current camera, exposure and target bindings.
class TerritoryPass {
public:
    TerritoryPass()=default;
    TerritoryPass(const TerritoryPass&)=delete;
    TerritoryPass& operator=(const TerritoryPass&)=delete;

    void prepareZones(const std::filesystem::path& directory) {
        controls_.providerFlags=1;
        zoneProfile_=source_zone::load(directory);
        source_zone::updateUntilSettled(zoneProfile_,zoneOwner_,zoneCurrent_);
    }

    void setVisible(bool visible) { visible_=visible; }

    void publishOwnership(ID3D11DeviceContext* context,
        const San14::Render::TerritoryOwnershipPublication& publication) {
        if(!palette_)return;
        if(San14::Render::publishCurrentTerritoryOwnership(publication,*palette_,
            overlay_.profile,zoneProfile_,zoneOwner_,zoneCurrent_,appliedRevision_)) {
            // Publish only layer0. Other channels keep their existing producers.
            D3D11_BOX layer0{0,0,0,source_zone::cells,1,1};
            context->UpdateSubresource(overlay_.zones.Get(),0,&layer0,zoneOwner_.zones.data(),0,0);
        }
    }

    void prepare(ID3D11Device* device,ID3D11DeviceContext* context,
        const std::filesystem::path& directory,source_territory_gpu::Targets targets,
        const D3D11_DEPTH_STENCIL_DESC& depth,const D3D11_RASTERIZER_DESC& raster,
        const San14::Render::TerritoryOwnershipPublication& publication) {
        if(!publication.revision)throw std::runtime_error("Initial territory ownership publication absent");
        targets_=targets;
        overlay_=source_territory_gpu::prepare(device,directory,targets_,zoneOwner_.zones,depth,raster);
        palette_.emplace(overlay_.profile);
        publishOwnership(context,publication);
    }

    void draw(ID3D11DeviceContext* context,int band,int fraction,double distance,
        const source_camera::Prefix& camera,float cameraEffect,
        ID3D11ShaderResourceView* exposure,UINT providerDrawFlags,NativeSourceDepthState& depth) {
        controls_.styleSelector=style_;
        controls_.screenMapMask=source_zone::displayMask(visible_,style_,
            zoneCurrent_.membership!=nullptr,!zoneCurrent_.referenceLists[0].empty()||!zoneCurrent_.referenceLists[1].empty());
        source_territory::Selection selection{band,fraction,distance,controls_};
        source_territory_gpu::update(context,overlay_,selection,camera,cameraEffect);
        source_territory_gpu::execute(context,overlay_,targets_,exposure,providerDrawFlags,source_territory::initializedStencilMask,depth);
    }

private:
    source_territory_gpu::Owner overlay_;
    source_territory_gpu::Targets targets_;
    source_territory::Controls controls_;
    source_zone::Profile zoneProfile_;
    source_zone::Owner zoneOwner_;
    source_zone::Current zoneCurrent_;
    std::optional<San14::Render::TerritoryOwnershipPalette> palette_;
    std::uint64_t appliedRevision_=0;
    bool visible_=true;
    int style_=0;
};
