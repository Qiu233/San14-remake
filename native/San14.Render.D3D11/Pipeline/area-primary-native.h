// Independent CUnitCapital59/60 owner; include after city-model-population-native.h.
// The completed City bank owns all19 CSP1 profiles /281 device templates.
#pragma once

inline NativeCityPopulationSource prepareNativeAreaPrimarySource(
        const NativeCityPopulationSource& bank,const std::vector<NativePrimaryUpdate>& current,
        const std::vector<std::string>& identities,const NativeCityModelAdmission& caller) {
    if(current.size()!=identities.size())throw std::runtime_error("Area current roster/identity count differs");
    NativeCityPopulationSource out;
    out.assets=bank.assets;out.templates=bank.templates;out.caller=caller;
    out.recordCount=bank.recordCount;out.maxDraws=7;
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
    std::ifstream names(input/"identities.txt");std::vector<std::string> identities;std::string name;
    while(std::getline(names,name))if(!name.empty())identities.push_back(name);
    std::ifstream source(input/"current-source.txt");std::string relative;
    if(!std::getline(source,relative))throw std::runtime_error("Area source CPU1 reference absent");
    std::ifstream packet(input/fs::path(relative),std::ios::binary);
    return prepareNativeAreaPrimarySource(bank,readNativeCityPopulationUpdate(packet),identities,bank.caller);
}

inline NativeCityPopulation prepareNativeAreaPrimaryPopulation(ID3D11Device* device,
        const NativeCityPopulation& cities,NativeCityPopulationSource source) {
    NativeCityPopulation out;out.source=std::move(source);
    out.templates=cities.templates; // shallow COM borrow; no VB/IB/shader/texture import
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
        const NativeCityPopulation& cities,const fs::path& input) {
    return prepareNativeAreaPrimaryPopulation(device,cities,prepareNativeAreaPrimarySource(cities.source,input));
}

// A read-only joined publication. City/Area retain their individual mutable CBs;
// never apply a CPU1 packet to this343-owner view or reconnect its CB slots.
struct NativeCityAreaPrimary {
    NativeCityPopulationSource source;
    NativeModelInputs currentModels;
};
inline void publishNativeCityAreaPrimary(NativeCityAreaPrimary& out,
        const NativeCityPopulation& cities,const NativeCityPopulation& areas) {
    out.source=cities.source;
    const auto ownerBase=unsigned(cities.source.catalog.instances.size());
    const auto primitiveBase=unsigned(cities.source.catalog.primitives.size());
    if(ownerBase!=61||areas.source.catalog.templateCount!=out.source.catalog.templateCount)
        throw std::runtime_error("City61/Area shared prepared bank differs");
    for(auto instance:areas.source.catalog.instances) {
        instance.index+=ownerBase;instance.firstPrimitive+=primitiveBase;
        out.source.catalog.instances.push_back(instance);
    }
    for(auto primitive:areas.source.catalog.primitives) {
        primitive.instanceIndex+=ownerBase; // shared281 templateIndex stays unchanged
        out.source.catalog.primitives.push_back(primitive);
    }
    auto append=[](auto& target,const auto& rows){target.insert(target.end(),rows.begin(),rows.end());};
    append(out.source.worlds,areas.source.worlds);append(out.source.currentAssets,areas.source.currentAssets);
    append(out.source.present,areas.source.present);append(out.source.identities,areas.source.identities);
    out.currentModels.blend=cities.currentModels.blend;out.currentModels.draws=cities.currentModels.draws;
    append(out.currentModels.draws,areas.currentModels.draws);
}
inline NativeCityModelQueues updateSelectNativeCityAreaPrimary(ID3D11DeviceContext* context,
        const NativeCityAreaPrimary& owner,const NativeCityPopulationPlan& plan) {
    auto selected=updateSelectNativeModelContinuous(context,plan.models,owner.currentModels);
    NativeCityModelQueues out;out.ordinary54.blend=out.alpha55.blend=selected.blend;
    for(std::size_t i=0;i<selected.draws.size();++i) {
        const auto& p=owner.source.catalog.primitives[plan.models.draws[i].primitiveIndex];
        (owner.source.templates[p.templateIndex].queue==54?out.ordinary54:out.alpha55).draws.push_back(std::move(selected.draws[i]));
    }
    return out;
}
