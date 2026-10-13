#pragma once
#include "CityGateWorldPublication.h"

namespace San14::Render {
// The current publication supplies requested identities. Only completed GPU
// producer entries may be installed in the current force association.
inline void prepareCurrentCityGateFlagMaterials(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeCityAccessoryGpu& owner,const std::vector<CityGateFlagKey>& current) {
    std::map<int,CityGateFlagKey> selected;
    for(const auto& flag:current) {
        auto [entry,added]=selected.emplace(flag.force,flag);
        if(!added&&entry->second!=flag)throw std::runtime_error("Current flag publication has conflicting force identities");
        owner.materials->prepare(device,context,flag);
    }
    owner.currentFlags=std::move(selected);
}
}
