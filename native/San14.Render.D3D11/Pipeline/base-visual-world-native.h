// Include after city-model-population-native.h and city-accessory-gpu-native.h.
// Actual managed World publications update these existing CPU/device owners;
// this adapter creates no GPU objects and changes no renderer ABI.
#pragma once
#include <sstream>

inline std::vector<NativePrimaryUpdate> readNativeBaseVisualPrimary(const char* data,std::size_t bytes){
    if(!data||!bytes)throw std::runtime_error("Current Base source asset is not prepared; consume RequiredPrimaryAssets before submission");
    std::istringstream input(std::string(data,bytes),std::ios::binary);
    auto rows=readNativeCityPopulationUpdate(input);
    if(input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Current Base CPU1 packet has trailing data");
    return rows;
}
inline source_accessory_population::Profile readNativeBaseVisualAccessories(const char* data,std::size_t bytes){
    if(!data||!bytes)throw std::runtime_error("Current Base accessory source operands are unavailable");
    std::istringstream input(std::string(data,bytes),std::ios::binary);
    auto unitCount=primaryRead<unsigned>(input),placementCount=primaryRead<unsigned>(input);
    if(unitCount!=61||placementCount!=224)throw std::runtime_error("Current Base accessory fixed source61/224 scope differs");
    source_accessory_population::Profile out;out.units.resize(unitCount);out.placements.resize(placementCount);
    for(auto& unit:out.units){unit.index=primaryRead<unsigned>(input);unit.force=primaryRead<int>(input);unit.requested=primaryRead<unsigned>(input);
        unit.position=primaryRead<std::array<float,3>>(input);}
    for(auto& p:out.placements){p.unitIndex=primaryRead<unsigned>(input);p.ordinal=primaryRead<unsigned>(input);
        p.root=primaryRead<std::array<float,12>>(input);p.obb=primaryRead<std::array<float,20>>(input);}
    if(input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Current Base accessory packet has trailing data");
    return out;
}
// Actual glyph/color changes must have completed their source material producer
// before this call. materials contains those current GPU bindings. The same
// force can change its glyph; this adapter rebinds the completed current entry.
// Player/local pose/controllerFrame are retained on a normal ownership change.
inline void publishNativeBaseVisualAccessories(NativeCityAccessoryGpu& owner,source_accessory_population::Profile current){
    if(current.units.size()!=owner.population.units.size()||current.placements.size()!=owner.population.placements.size())
        throw std::runtime_error("Current accessory publication changed the fixed source identities");
    for(unsigned i=0;i<current.units.size();++i){const auto& u=current.units[i];const auto& old=owner.population.units[i];
        if(u.index!=i||u.index!=old.index||u.requested!=old.requested)throw std::runtime_error("Current accessory source unit/descriptor identity differs");
        if(u.force>=1&&u.force<=51&&(!owner.materials.count({0,u.force})||!owner.materials.count({1,u.force})))
            throw std::runtime_error("Current accessory force glyph/color materials require source import/republication");}
    for(unsigned i=0;i<current.placements.size();++i){const auto& p=current.placements[i];const auto& old=owner.population.placements[i];
        if(p.unitIndex!=old.unitIndex||p.ordinal!=old.ordinal)throw std::runtime_error("Current accessory packed player identity differs");}
    owner.population=std::move(current);
    for(auto& [index,group]:owner.groups){const auto& placement=owner.population.placements.at(index);const auto& unit=owner.population.units.at(placement.unitIndex);
        if(unit.force<1||unit.force>51)continue; // The normal next prepare releases this non-requested group.
        for(unsigned draw=0;draw<group.models.draws.size()&&draw<2;++draw){const auto& material=owner.materials.at({draw==0?1:0,unit.force});
            group.models.draws[draw].textures=material.textures;group.models.draws[draw].samplers=material.samplers;}}
}
