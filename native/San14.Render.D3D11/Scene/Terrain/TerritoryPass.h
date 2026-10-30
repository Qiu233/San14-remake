#pragma once
#include "TerritoryRenderer.h"
#include "TerritoryOwnership.h"
#include "CommandMap.h"
#include <optional>
#include <vector>

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

    // Only actual FPSManager callbacks advance the map's logical palettes.
    // Main's ordinary and water draws both consume the same current state.
    void recordMapUpdate(unsigned fpsMode) { pendingMapUpdates_.push_back(fpsMode); }

    void setVisible(bool visible) { visible_=visible; }
    void setStyle(unsigned style) {
        if(style>2)throw std::runtime_error("Map information territory style is unsupported");
        style_=int(style);
    }
    void publishCommandMap(const source_command_map::Publication& publication) {
        command_.accept(publication,overlay_.profile,overlay_.cpu,zoneOwner_,zoneCurrent_,controls_);
    }

    void publishOwnership(const San14::Render::TerritoryOwnershipPublication& publication) {
        if(!palette_)return;
        San14::Render::publishCurrentTerritoryOwnership(publication,*palette_,
            overlay_.profile,zoneProfile_,zoneOwner_,appliedRevision_);
    }

    void prepare(ID3D11Device* device,ID3D11DeviceContext* context,
        const std::filesystem::path& directory,source_territory_gpu::Targets targets,
        const D3D11_DEPTH_STENCIL_DESC& depth,const D3D11_RASTERIZER_DESC& raster,
        const San14::Render::TerritoryOwnershipPublication& publication) {
        if(!publication.revision)throw std::runtime_error("Initial territory ownership publication absent");
        targets_=targets;
        overlay_=source_territory_gpu::prepare(device,directory,targets_,zoneOwner_.zones,depth,raster);
        palette_.emplace(overlay_.profile);
        publishOwnership(publication);
    }

    void draw(ID3D11DeviceContext* context,int band,int fraction,double distance,
        const source_camera::Prefix& camera,float cameraEffect,
        ID3D11ShaderResourceView* exposure,UINT providerDrawFlags,NativeSourceDepthState& depth) {
        controls_.styleSelector=style_;
        controls_.screenMapMask=source_zone::displayMask(visible_,style_,
            zoneCurrent_.membership!=nullptr,!zoneCurrent_.referenceLists[0].empty()||!zoneCurrent_.referenceLists[1].empty());
        source_territory::Selection selection{band,fraction,distance,controls_};
        source_territory::writeCurrent(overlay_.profile,overlay_.cpu,selection,camera,cameraEffect);
        for(unsigned fpsMode:pendingMapUpdates_) {
            command_.fpsMode=fpsMode;
            int changed=source_zone::updateOne(zoneProfile_,zoneOwner_,zoneCurrent_);
            if(changed>=0) {
                UINT begin=UINT(changed)*source_zone::cells;
                D3D11_BOX box{begin,0,0,begin+source_zone::cells,1,1};
                context->UpdateSubresource(overlay_.zones.Get(),0,&box,zoneOwner_.zones.data()+begin,0,0);
            }
            command_.updatePalette(overlay_.profile,overlay_.cpu,zoneOwner_);
        }
        pendingMapUpdates_.clear();
        context->UpdateSubresource(overlay_.constants.Get(),0,nullptr,overlay_.cpu.constants.data(),0,0);
        source_territory_gpu::execute(context,overlay_,targets_,exposure,providerDrawFlags,source_territory::initializedStencilMask,depth);
    }

private:
    source_territory_gpu::Owner overlay_;
    source_territory_gpu::Targets targets_;
    source_territory::Controls controls_;
    source_zone::Profile zoneProfile_;
    source_zone::Owner zoneOwner_;
    source_zone::Current zoneCurrent_;
    source_command_map::Owner command_;
    std::vector<unsigned> pendingMapUpdates_;
    std::optional<San14::Render::TerritoryOwnershipPalette> palette_;
    std::uint64_t appliedRevision_=0;
    bool visible_=true;
    int style_=0;
};
