#pragma once
#include "CityPopulation.h"
#include "StaticModelResources.h"
#include "AreaCapitalAccessories.h"
#include "AreaPrimaryPopulation.h"
#include "FacilityPrimaryPopulation.h"
#include "CityGateFlagImports.h"
#include "ArmyRenderWorld.h"
#include "ModelConstantArena.h"

namespace San14::Render {
// Session-owned City/Gate, Area and facility presentation. The Army render world owns
// the single shared accessory pool; it borrows these device objects only inside
// synchronous operations. World publications and pass targets are never held.
class BaseRenderWorld {
public:
    explicit BaseRenderWorld(bool accessories):sourceAccessoryVisuals(accessories){}
    BaseRenderWorld(const BaseRenderWorld&)=delete;
    BaseRenderWorld& operator=(const BaseRenderWorld&)=delete;

    void preparePrimary(ID3D11Device* device,const PreparedResourceDirectories& resources,
        const std::optional<CityGateWorldPublication>& cityWorld,
        const std::optional<AreaCapitalWorldPublication>& areaWorld,
        const std::shared_ptr<const NativePrimaryModelGpuMedia>& primaryGpuMedia,bool requirePrimaryGpuMedia) {
        if(!cityWorld)throw std::runtime_error("Native Main requires its actual initial City/Gate publication");
        cityModels=prepareNativeCityPopulation(device,resources.directory(ResourceGroup::BaseVisuals)/"primary-templates",
            readNativeBaseVisualPrimary(cityWorld->primary.data(),cityWorld->primary.size()),primaryGpuMedia,requirePrimaryGpuMedia);
        if(!areaWorld)throw std::runtime_error("Native Main requires its actual Area Capital publication");
        const auto& initialAreas=*areaWorld;
        areaModels=prepareNativeAreaPrimaryPopulation(device,cityModels,
            prepareNativeAreaPrimarySource(cityModels.source,
                readNativeBaseVisualPrimary(initialAreas.models.primary.data(),initialAreas.models.primary.size()),
                initialAreas.identities,cityModels.source.caller));
        auto facilities=cityWorld->facilities.empty()?std::vector<NativePrimaryUpdate>{}:
            readNativeBaseVisualPrimary(cityWorld->facilities.data(),cityWorld->facilities.size());
        facilityModels=prepareNativeFacilityPrimaryPopulation(device,cityModels,facilities);
        publishNativeCityAreaPrimary(primaryModels,cityModels,areaModels,&facilityModels);
    }

    void prepareAccessories(ID3D11Device* device,ID3D11DeviceContext* context,
        const PreparedResourceDirectories& resources,const std::optional<CityGateWorldPublication>& cityWorld,
        const std::optional<AreaCapitalWorldPublication>& areaWorld,
        const source_default_player_phase::Profile& defaultPlayerPhases) {
        if(sourceAccessoryVisuals) {
            const auto accessorySource=resources.directory(ResourceGroup::BaseAccessories);
            if(!cityWorld||!areaWorld)throw std::runtime_error("Accessories require their actual initial World publications");
            cityAccessories=prepareNativeCityAccessoryGpu(device,accessorySource,
                readNativeBaseVisualAccessories(cityWorld->accessories.data(),cityWorld->accessories.size()));
            if(cityAccessories.resources->pose.duration8!=defaultPlayerPhases.categories[2].duration8)
                throw std::runtime_error("Accessory source clip and default player duration differ");
            prepareCurrentCityGateFlagMaterials(device,context,cityAccessories,cityWorld->flags);
            areaAccessories=prepareNativeAreaCapitalAccessories(cityAccessories,
                readNativeAreaCapitalAccessories(areaWorld->models.accessories));
            prepareCurrentCityGateFlagMaterials(device,context,areaAccessories,areaWorld->models.flags);
        }
    }

    void publish(ID3D11Device* device,ID3D11DeviceContext* context,const std::optional<CityGateWorldPublication>& cityWorld,
        const std::optional<AreaCapitalWorldPublication>& areaWorld,ArmyRenderWorld& armies) {
        // Borrowed draw descriptions are consumed only until the next World
        // publication. Clear them before a facility population can replace its
        // physical draw vector; actual GPU owners remain in their populations.
        ordinaryModelQueues={};
        primaryPopulationChanged=false;

        if(!cityWorld)throw std::runtime_error("Native Main requires its actual City/Gate World publication");
        const auto& bases=*cityWorld;
        if(appliedCityGateRevision!=bases.revision) {
            primaryPublicationPending=true;
            if(sourceAccessoryVisuals)prepareCurrentCityGateFlagMaterials(device,context,cityAccessories,bases.flags);
            auto primaries=readNativeBaseVisualPrimary(bases.primary.data(),bases.primary.size());
            primaryBindingsPending|=updateNativeCityPopulation(cityModels,primaries);
            if(!bases.facilities.empty())primaryBindingsPending|=publishNativeFacilityPrimaryPopulation(device,facilityModels,cityModels,
                readNativeBaseVisualPrimary(bases.facilities.data(),bases.facilities.size()));
            if(sourceAccessoryVisuals){
                auto current=readNativeBaseVisualAccessories(bases.accessories.data(),bases.accessories.size());
                armies.observeCityAccessories(cityAccessories.population,current);
                publishNativeBaseVisualAccessories(cityAccessories,std::move(current));
            }
            primaryPopulationChanged=true;
            appliedCityGateRevision=bases.revision;

        }
        const auto& capitals=*areaWorld;
        if(appliedAreaCapitalRevision!=capitals.models.revision) {
            primaryPublicationPending=true;
            if(capitals.identities!=areaModels.source.identities)throw std::runtime_error("Current Area roster needs an actual rebind/import");
            if(sourceAccessoryVisuals)prepareCurrentCityGateFlagMaterials(device,context,areaAccessories,capitals.models.flags);
            primaryBindingsPending|=updateNativeCityPopulation(areaModels,readNativeBaseVisualPrimary(capitals.models.primary.data(),capitals.models.primary.size()));
            if(sourceAccessoryVisuals){
                auto current=readNativeAreaCapitalAccessories(capitals.models.accessories);
                armies.observeAreaAccessories(areaAccessories.population,current);
                publishNativeBaseVisualAccessories(areaAccessories,std::move(current));
            }
            appliedAreaCapitalRevision=capitals.models.revision;primaryPopulationChanged=true;
        }
        if(primaryPublicationPending) {
            publishNativeCityAreaPrimary(primaryModels,cityModels,areaModels,&facilityModels,primaryBindingsPending);
            primaryPublicationPending=false;primaryBindingsPending=false;
            primaryPopulationChanged=true;primaryShadowPublicationPending=true;
        }
    }

    template<class Operation>
    void withAccessories(Operation&& operation) {
        operation(cityAccessories,areaAccessories);
    }

    void publishCollectionCaller(const NativeModelCollectionCaller& caller) {
        cityModels.source.caller=caller;areaModels.source.caller=caller;facilityModels.source.caller=caller;primaryModels.source.caller=caller;
    }
    void selectCamera(const source_camera::Frame& staticCamera) {
        cityPlan=selectNativeCityPopulation(primaryModels.source,staticCamera);
    }

    void updateSubmission(ID3D11DeviceContext* context,const StaticModelResources::Submission& staticSubmission) {
            const auto constants=constantArena.upload(context,cityPlan.models,primaryModels.currentModels);
            if(!constants.context)uploadNativeCityAreaPrimaryConstants(context,primaryModels,cityPlan);
            ordinaryModelQueues=mergeNativeCityAreaPrimaryDrawReferences(staticSubmission.plan,staticSubmission.models,
                primaryModels,cityPlan,constants.context?&constants:nullptr);
    }

    void drawOrdinary(ID3D11DeviceContext* context,ID3D11RenderTargetView* const* colors,
        ID3D11DepthStencilView* depth,const D3D11_VIEWPORT& viewport)const {
        drawNativeModels(context,ordinaryModelQueues.ordinary54,colors,depth,viewport);
    }
    void drawAlpha(ID3D11DeviceContext* context,ID3D11RenderTargetView* const* colors,
        ID3D11DepthStencilView* depth,const D3D11_VIEWPORT& viewport)const {
        drawNativeModels(context,ordinaryModelQueues.alpha55,colors,depth,viewport);
    }

    const NativeCityAreaPrimary& primary()const{return primaryModels;}
    const NativeCityPopulationSource& citySource()const{return cityModels.source;}
    const NativeCityPopulationPlan& plan()const{return cityPlan;}
    bool populationChanged()const{return primaryPopulationChanged;}
    bool shadowPopulationPending()const{return primaryShadowPublicationPending;}
    void acknowledgeShadowPopulation(){primaryShadowPublicationPending=false;}

private:
    const bool sourceAccessoryVisuals;
    NativeCityPopulation cityModels;NativeCityPopulationPlan cityPlan;
    NativeCityPopulation areaModels,facilityModels;NativeCityAreaPrimary primaryModels;
    bool primaryPopulationChanged=false;
    bool primaryPublicationPending=false,primaryBindingsPending=false;
    bool primaryShadowPublicationPending=false;
    NativeCityAccessoryGpu cityAccessories;
    NativeCityAccessoryGpu areaAccessories;
    UINT64 appliedCityGateRevision=0;
    UINT64 appliedAreaCapitalRevision=0;
    NativeModelConstantArena constantArena;
    NativeCityModelReferenceQueues ordinaryModelQueues;
};
} // namespace San14::Render
