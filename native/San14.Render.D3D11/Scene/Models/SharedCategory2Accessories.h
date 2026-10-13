#pragma once
#include "ArmyWorldModelPool.h"

using NativeGraphicsModelPool=ArmyWorldModelPool<NativeArmyPopulation>;

// Immutable geometry is shared; the physical draw owns its mutable buffers.
inline NativeModelDraw& resolveNativePhysicalModel(ID3D11Device* device,
        NativeGraphicsModelPool::Slot& slot,unsigned draw,const NativeModelDraw& source,
        std::span<const char> vertexTemplate={}){
    auto found=slot.models.find(draw);
    if(found==slot.models.end()){
        auto model=source;
        D3D11_BUFFER_DESC vertex{},pixel{};source.vsConstants->GetDesc(&vertex);source.psConstants->GetDesc(&pixel);
        model.vsConstants=createNativeAccessoryCurrentCb(device,vertex.ByteWidth);
        model.psConstants=createNativeAccessoryCurrentCb(device,pixel.ByteWidth);
        if(!vertexTemplate.empty())model.retainedVs=std::make_shared<std::vector<char>>(vertexTemplate.begin(),vertexTemplate.end());
        found=slot.models.emplace(draw,std::move(model)).first;
    }
    else {
        auto& prior=found->second;
        D3D11_BUFFER_DESC oldVertex{},newVertex{},oldPixel{},newPixel{};
        prior.vsConstants->GetDesc(&oldVertex);source.vsConstants->GetDesc(&newVertex);
        prior.psConstants->GetDesc(&oldPixel);source.psConstants->GetDesc(&newPixel);
        auto vertex=prior.vsConstants,pixel=prior.psConstants;auto retained=prior.retainedVs;
        if(oldVertex.ByteWidth!=newVertex.ByteWidth){
            vertex=createNativeAccessoryCurrentCb(device,newVertex.ByteWidth);retained.reset();
        }
        if(oldPixel.ByteWidth!=newPixel.ByteWidth)pixel=createNativeAccessoryCurrentCb(device,newPixel.ByteWidth);
        if(!vertexTemplate.empty()&&!retained)
            retained=std::make_shared<std::vector<char>>(vertexTemplate.begin(),vertexTemplate.end());
        // The selected material owns programs, layout, resources and render state.
        // The physical slot retains its private buffers and animation separately.
        auto rebound=source;rebound.vsConstants=std::move(vertex);rebound.psConstants=std::move(pixel);
        rebound.retainedVs=std::move(retained);rebound.identity=std::move(prior.identity);
        prior=std::move(rebound);
    }
    return found->second;
}

struct NativeSharedAccessoryUnit {
    GraphicsModelFamily family{};
    bool priorVisible{},dirty=true,materialPending=true;
    std::optional<San14::Render::CityGateFlagKey> installedMaterial;
    std::vector<unsigned> placements;
};
struct NativeSharedAccessories {
    std::vector<NativeSharedAccessoryUnit> units;
    std::vector<GraphicsModelBindingKey> visible;
    unsigned draws{};
};

inline GraphicsModelBindingKey nativeSharedAccessoryKey(const NativeSharedAccessoryUnit& owner,
        const source_accessory_population::Placement& placement){
    return {owner.family,{int(placement.unitIndex),2,
        owner.family==GraphicsModelFamily::Gate?0x80:0x78,int(placement.ordinal)}};
}

inline NativeSharedAccessories prepareNativeSharedAccessories(
        const source_accessory_population::Profile& source,bool capitals){
    NativeSharedAccessories out;out.units.resize(source.units.size());
    for(const auto& unit:source.units){
        auto& state=out.units.at(unit.index);
        if(capitals)state.family=GraphicsModelFamily::AreaCapital;
        else if(unit.requested==4)state.family=GraphicsModelFamily::City;
        else if(unit.requested==2)state.family=GraphicsModelFamily::Gate;
        else throw std::runtime_error("Current City/Gate descriptor has no source family");
    }
    for(unsigned index=0;index<source.placements.size();++index)
        out.units.at(source.placements[index].unitIndex).placements.push_back(index);
    return out;
}

// Called only at current source publication, not on camera requests. The fixed
// roster keeps its unit identities; changed source placement dirties its setter.
inline void observeNativeSharedAccessorySource(NativeSharedAccessories& current,
        const source_accessory_population::Profile& prior,const source_accessory_population::Profile& next){
    for(const auto& unit:next.units){
        auto& state=current.units.at(unit.index);const auto& old=prior.units.at(unit.index);
        if(unit.position!=old.position)state.dirty=true;
        if(unit.force!=old.force)state.materialPending=true;
    }
    for(unsigned index=0;index<next.placements.size();++index){
        const auto& placement=next.placements[index];const auto& old=prior.placements[index];
        if(placement.root!=old.root||placement.obb!=old.obb)current.units.at(placement.unitIndex).dirty=true;
    }
}

// One family, one unit at a time: reconcile -> material -> v30 -> H. No global
// pre-release sweep can make a later unit's slots available to this caller.
inline void updateNativeSharedAccessoryFamily(ID3D11Device* device,NativeGraphicsModelPool& pool,
        NativeSharedAccessories& current,NativeCityAccessoryGpu& source,GraphicsModelFamily family,
        const source_camera::Frame& camera,int appMode,float ecB0,double distance90,
        const NativeArmyGraphicsAction& action,const NativeAccessoryPoseSource& bankPose,
        const std::array<uint32_t,2>& fps,unsigned fpsMode,
        NativeArmyAnimationGroups& groups){
    for(const auto& unit:source.population.units){
        auto& state=current.units[unit.index];if(state.family!=family)continue;
        const auto gate=source_accessory_population::unitAdmission(unit.position,
            reinterpret_cast<const char*>(camera.prefix.data()),reinterpret_cast<const char*>(camera.prefix.data()),
            appMode,ecB0,distance90);
        const bool reached=gate.visible||state.priorVisible;state.priorVisible=gate.visible;
        if(!reached)continue;
        const bool admitted=gate.visible&&unit.force>=1&&unit.force<=51;
        if(!admitted)pool.releaseUnit(family,int(unit.index));
        else for(unsigned index:state.placements){
            const auto key=nativeSharedAccessoryKey(state,source.population.placements[index]);
            if(pool.acquire(key)){state.dirty=true;state.materialPending=true;}
            if(!pool.find(key))state.dirty=true; // source descriptor pending retries next event
        }
        const bool setter=state.dirty;
        for(unsigned index:state.placements){
            const auto& placement=source.population.placements[index];
            const auto key=nativeSharedAccessoryKey(state,placement);auto* slot=pool.find(key);if(!slot)continue;
            const auto& flag=source.currentFlags.at(unit.force);
            if(!state.installedMaterial||*state.installedMaterial!=flag)state.materialPending=true;
            for(unsigned draw=0;draw<source.resources->models.draws.size();++draw){
                auto& model=resolveNativePhysicalModel(device,*slot,draw,source.resources->models.draws[draw]);
                const auto& material=nativeAccessoryMaterialForDraw(source,draw,unit.force);
                model.textures=material.textures;model.samplers=material.samplers;
            }
            if(setter){
                std::memcpy(&slot->presented.world,placement.root.data(),48);
                slot->presented.bounds=placement.obb;
            }
            consumeNativeArmyAnimation(action.player,fps,2,slot->clock,groups,
                {true,gate.animateNear,0,-1,fpsMode},[&](float requested,bool){
                    return std::optional<float>(source_default_player_phase::defaultReturnedPhase(
                        {action.player.clipDuration,action.player.loopDeclaration},requested));
                });
            prepareNativeArmyCurrentPose(bankPose,slot->pose,slot->cache,slot->clock.returnedPhase,
                slot->presented.world,slot->clock.hasSuccessfulSample,true);
            state.installedMaterial=flag;
        }
        // Capital/City/Gate v30 clears dirty at normal completion, including a
        // partial descriptor. Its next pending reconcile can dirty it again.
        state.dirty=false;state.materialPending=false;
    }
}

// Main consumes the current descriptor links and held model OBB. It does not
// allocate, release, advance H, or run another unit admission callback.
inline unsigned selectNativeSharedAccessories(ID3D11DeviceContext* context,NativeGraphicsModelPool& pool,
        NativeSharedAccessories& current,const NativeCityAccessoryGpu& source,const source_camera::Frame& camera){
    std::array<std::array<float,4>,6> planes;
    for(unsigned index=0;index<6;++index)std::memcpy(planes[index].data(),&camera.planes[index].equation,16);
    current.visible.clear();
    for(const auto& unit:source.population.units){const auto& state=current.units[unit.index];
        for(unsigned index:state.placements){
            const auto key=nativeSharedAccessoryKey(state,source.population.placements[index]);
            auto* slot=pool.find(key);if(!slot)continue;
            NativeModelContinuousInstance bounds;bounds.mode=0;bounds.obb=slot->presented.bounds;
            if(!modelContinuousVisible(bounds,planes))continue;
            for(unsigned draw=0;draw<source.resources->models.draws.size();++draw){
                auto& model=slot->models.at(draw);const auto& rows=slot->pose.frame.palettes[source.resources->bindings[draw].palette].rows;
                auto vertex=source_accessory_population::vertex(reinterpret_cast<const char*>(camera.prefix.data()),
                    reinterpret_cast<const float*>(rows.data()),unsigned(rows.size()));
                float spec[4];int material;std::memcpy(spec,source.resources->pixels[draw].data(),16);
                std::memcpy(&material,source.resources->pixels[draw].data()+64,4);
                auto pixel=source_accessory_population::regularPixel(reinterpret_cast<const char*>(camera.prefix.data()),spec,material);
                context->UpdateSubresource(model.vsConstants.Get(),0,nullptr,vertex.data(),0,0);
                context->UpdateSubresource(model.psConstants.Get(),0,nullptr,pixel.data(),0,0);
            }
            current.visible.push_back(key);
        }
    }
    return unsigned(current.visible.size()*source.resources->models.draws.size());
}

inline void drawNativeSharedAccessories(ID3D11DeviceContext* context,NativeGraphicsModelPool& pool,
        NativeSharedAccessories& current,const NativeCityAccessoryGpu& source,
        ID3D11RenderTargetView* const* targets,ID3D11DepthStencilView* depth,const D3D11_VIEWPORT& viewport){
    current.draws=0;
    for(const auto& key:current.visible){auto& slot=*pool.find(key);
        NativeModelInputs models;models.blend=source.resources->models.blend;
        for(unsigned draw=0;draw<source.resources->models.draws.size();++draw)models.draws.push_back(slot.models.at(draw));
        drawNativeModels(context,models,targets,depth,viewport);current.draws+=unsigned(models.draws.size());
    }
}
