#pragma once
#include "AreaPrimaryPopulation.h"

// Facilities and connected CUnitSubObjects form one complete packed roster.
// Physical receiver identity remains owned by the managed World publication.
inline void validateNativeFacilityPrimaryRows(const NativeCityPopulationSource& bank,
        const std::vector<NativePrimaryUpdate>& current) {
    for(const auto& row:current) {
        const auto& asset=bank.assets.at(row.asset);
        if(asset.category<61||asset.category>88)throw std::runtime_error("Facility primary requires its category61..88 profile");
    }
}
inline NativeCityPopulationSource prepareNativeFacilityPrimarySource(
        const NativeCityPopulationSource& bank,const std::vector<NativePrimaryUpdate>& current) {
    validateNativeFacilityPrimaryRows(bank,current);
    NativeCityTemplateBank templates=bank;templates.maxDraws=0;
    for(const auto& asset:templates.assets)if(asset.category>=61&&asset.category<=88)
        templates.maxDraws=std::max(templates.maxDraws,unsigned(asset.draws.size()));
    auto out=prepareNativeCityPopulationSource(std::move(templates),current);
    for(unsigned index=0;index<out.identities.size();++index)
        out.identities[index]="facility-primary/"+std::to_string(index);
    out.caller=bank.caller;
    return out;
}
inline NativeCityPopulation prepareNativeFacilityPrimaryPopulation(ID3D11Device* device,
        const NativeCityPopulation& bank,const std::vector<NativePrimaryUpdate>& current) {
    return prepareNativeSharedPrimaryPopulation(device,bank,prepareNativeFacilityPrimarySource(bank.source,current));
}
inline bool publishNativeFacilityPrimaryPopulation(ID3D11Device* device,NativeCityPopulation& owner,
        const NativeCityPopulation& bank,const std::vector<NativePrimaryUpdate>& current) {
    validateNativeFacilityPrimaryRows(bank.source,current);
    if(current.size()!=owner.source.worlds.size()) {
        // Actual construction/demolition changes resident mutable CB owners.
        // Geometry, shaders and textures remain borrowed from the one bank.
        owner=prepareNativeFacilityPrimaryPopulation(device,bank,current);
        return true; // New resident CB owners must replace every joined binding.
    }
    return updateNativeCityPopulation(owner,current);
}
