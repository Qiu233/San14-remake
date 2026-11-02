// Independent CUnitCapital59/60 owner; include after city-model-population-native.h.
// The completed City bank owns all19 CSP1 profiles /281 device templates.
#pragma once
#include "../../Runtime/PreparedFileInput.h"

inline NativeCityPopulationSource prepareNativeAreaPrimarySource(
        const NativeCityPopulationSource& bank,const std::vector<NativePrimaryUpdate>& current,
        const std::vector<std::string>& identities,const NativeModelCollectionCaller& caller) {
    if(current.size()!=identities.size())throw std::runtime_error("Area current roster/identity count differs");
    NativeCityPopulationSource out;
    out.assets=bank.assets;out.templates=bank.templates;out.caller=caller;
    out.recordCount=bank.recordCount;
    for(const auto& asset:bank.assets)if(asset.category==59||asset.category==60)
        out.maxDraws=std::max(out.maxDraws,unsigned(asset.draws.size()));
    out.commonMode=bank.commonMode;out.sortFields=bank.sortFields;
    out.catalog.templateCount=bank.catalog.templateCount;out.catalog.commonMode=bank.catalog.commonMode;
    out.catalog.sortFields=bank.catalog.sortFields;out.catalog.instances.resize(current.size());
    out.worlds.resize(current.size());out.currentAssets.resize(current.size());out.present.resize(current.size());
    out.identities=identities;
    for(const auto& row:current) {
        const auto& asset=out.assets.at(row.asset);
        if((asset.category!=59&&asset.category!=60)||asset.draws.size()>out.maxDraws)
            throw std::runtime_error("Area imported category59/60 profile differs");
    }
    updateNativeCityPopulationSource(out,current);
    return out;
}

// Optional once-import bridge for the delivered CPU1/bootstrap identity files.
// Runtime can pass the actual production publication directly to the overload above.
inline NativeCityPopulationSource prepareNativeAreaPrimarySource(
        const NativeCityPopulationSource& bank,const fs::path& input) {
    NativePreparedInputFile names(input/"identities.txt");std::vector<std::string> identities;std::string name;
    while(std::getline(names,name))if(!name.empty())identities.push_back(name);
    NativePreparedInputFile source(input/"current-source.txt");std::string relative;
    if(!std::getline(source,relative))throw std::runtime_error("Area source CPU1 reference absent");
    NativePreparedInputFile packet(input/fs::path(relative),std::ios::binary);
    return prepareNativeAreaPrimarySource(bank,readNativeCityPopulationUpdate(packet),identities,bank.caller);
}

inline NativeCityPopulation prepareNativeSharedPrimaryPopulation(ID3D11Device* device,
        const NativeCityPopulation& bank,NativeCityPopulationSource source) {
    NativeCityPopulation out;out.source=std::move(source);
    out.templates=bank.templates; // shallow COM borrow; no VB/IB/shader/texture import
    const auto count=out.source.worlds.size()*out.source.maxDraws;
    std::array<char,160> vertex{};std::array<char,80> pixel{};
    out.vertexConstants.reserve(count);out.pixelConstants.reserve(count);
    for(std::size_t i=0;i<count;++i) {
        out.vertexConstants.push_back(modelContinuousConstantBuffer(device,vertex.data(),160));
        out.pixelConstants.push_back(modelContinuousConstantBuffer(device,pixel.data(),80));
    }
    connectNativeCityPopulationCurrentModels(out);return out;
}
inline NativeCityPopulation prepareNativeAreaPrimaryPopulation(ID3D11Device* device,
        const NativeCityPopulation& cities,NativeCityPopulationSource source) {
    return prepareNativeSharedPrimaryPopulation(device,cities,std::move(source));
}
inline NativeCityPopulation prepareNativeAreaPrimaryPopulation(ID3D11Device* device,
        const NativeCityPopulation& cities,const fs::path& input) {
    return prepareNativeAreaPrimaryPopulation(device,cities,prepareNativeAreaPrimarySource(cities.source,input));
}

// A read-only joined publication. Each population retains its mutable CBs;
// never apply a CPU1 packet to this joined view or reconnect its CB slots.
struct NativeCityAreaPrimary {
    NativeCityPopulationSource source;
    NativeModelInputs currentModels;
};
inline void appendNativeSharedPrimary(NativeCityAreaPrimary& out,const NativeCityPopulation& population,
        bool physicalBindingsChanged=true) {
    const auto ownerBase=unsigned(out.source.catalog.instances.size());
    const auto primitiveBase=unsigned(out.source.catalog.primitives.size());
    if(population.source.catalog.templateCount!=out.source.catalog.templateCount)
        throw std::runtime_error("Primary populations require the same prepared template bank");
    for(auto instance:population.source.catalog.instances) {
        instance.index+=ownerBase;instance.firstPrimitive+=primitiveBase;
        out.source.catalog.instances.push_back(instance);
    }
    for(auto primitive:population.source.catalog.primitives) {
        primitive.instanceIndex+=ownerBase; // Shared templateIndex stays unchanged.
        out.source.catalog.primitives.push_back(primitive);
    }
    auto append=[](auto& target,const auto& rows){target.insert(target.end(),rows.begin(),rows.end());};
    append(out.source.worlds,population.source.worlds);append(out.source.currentAssets,population.source.currentAssets);
    append(out.source.present,population.source.present);append(out.source.identities,population.source.identities);
    if(physicalBindingsChanged)append(out.currentModels.draws,population.currentModels.draws);
}
inline void publishNativeCityAreaPrimary(NativeCityAreaPrimary& out,
        const NativeCityPopulation& cities,const NativeCityPopulation& areas,
        const NativeCityPopulation* facilities=nullptr,bool physicalBindingsChanged=true) {
    if(cities.source.catalog.instances.size()!=61)throw std::runtime_error("City/Gate complete source61 population differs");
    out.source=cities.source;
    // Keep the joined physical owners until a producer actually reconnects.
    // Its current source catalog and all World/CB payloads still update above
    // and in append on every publication, including animation-only updates.
    if(physicalBindingsChanged) {
        out.currentModels.blend=cities.currentModels.blend;out.currentModels.draws=cities.currentModels.draws;
    }
    appendNativeSharedPrimary(out,areas,physicalBindingsChanged);
    if(facilities)appendNativeSharedPrimary(out,*facilities,physicalBindingsChanged);
}
inline void uploadNativeCityAreaPrimaryConstants(ID3D11DeviceContext* context,
        const NativeCityAreaPrimary& owner,const NativeCityPopulationPlan& plan) {
    uploadNativeModelContinuousConstants(context,plan.models,owner.currentModels);
}
inline NativeCityModelReferenceQueues mergeNativeCityAreaPrimaryDrawReferences(
        const NativeModelContinuousPlan& fieldPlan,const NativeModelInputs& fieldModels,
        const NativeCityAreaPrimary& owner,const NativeCityPopulationPlan& plan,
        const NativeModelConstantSubmission* constants=nullptr) {
    return mergeNativeModelDrawReferencesWithField54(fieldPlan,fieldModels,plan.models,
        [&](const NativeModelContinuousDraw& key,std::size_t selectedIndex) {
            const auto& primitive=owner.source.catalog.primitives.at(key.primitiveIndex);
            const auto queue=owner.source.templates.at(primitive.templateIndex).queue;
            const auto draw=constants?constants->draws->at(selectedIndex):
                NativeModelDrawReference{&owner.currentModels.draws.at(key.primitiveIndex)};
            return std::pair{queue,draw};
        },owner.currentModels.blend.Get(),constants);
}
